using Azure.AI.Projects;
using Azure.AI.Projects.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WebApp.Api.Models;

namespace WebApp.Api.Services;

#pragma warning disable OPENAI001

/// <summary>
/// Azure AI Foundry agent service using v2 Agents API.
/// </summary>
/// <remarks>
/// Uses Microsoft.Agents.AI.AzureAI extension methods on AIProjectClient for agent loading,
/// and direct ProjectResponsesClient for streaming (required for annotations, MCP approvals).
/// See .github/skills/researching-azure-ai-sdk/SKILL.md for SDK patterns.
/// </remarks>
public class AgentFrameworkService : IDisposable
{
    private static readonly ActivitySource ActivitySource = new("WebApp.Api.AgentFramework");
    private readonly AIProjectClient _projectClient;
    private readonly string _defaultAgentId;
    private readonly string? _airAgentId;
    private readonly string? _eirAgentId;
    private readonly string? _bepAgentId;
    private readonly ILogger<AgentFrameworkService> _logger;
    private readonly StandardsRetrievalService _standardsRetrieval;
    private readonly StandardsPromptBuilder _promptBuilder;
    private readonly bool _requirementsFirstEnabled;
    private readonly int _requirementsFirstMaxPerStandard;
    private readonly int _requirementsFirstPageSize;
    private ChatClientAgent? _cachedAgent;
    private AgentMetadataResponse? _cachedMetadata;
    private readonly SemaphoreSlim _agentLock = new(1, 1);
    private bool _disposed = false;
    private ResponseTokenUsage? _lastUsage;
    private string? _resolvedAgentName;
    private string? _resolvedAgentReferenceName;

    public AgentFrameworkService(
        IConfiguration configuration,
        ILogger<AgentFrameworkService> logger,
        StandardsRetrievalService standardsRetrieval,
        StandardsPromptBuilder promptBuilder)
    {
        _logger = logger;
        _standardsRetrieval = standardsRetrieval;
        _promptBuilder = promptBuilder;

        _requirementsFirstEnabled = configuration.GetValue<bool?>("StandardsCompliance:RequirementsFirstEnabled") ?? false;
        _requirementsFirstMaxPerStandard = configuration.GetValue<int?>("StandardsCompliance:RequirementsFirstMaxPerStandard") ?? 500;
        _requirementsFirstPageSize = configuration.GetValue<int?>("StandardsCompliance:RequirementsFirstPageSize") ?? 100;

        var endpoint = configuration["AI_AGENT_ENDPOINT"]
            ?? throw new InvalidOperationException("AI_AGENT_ENDPOINT is not configured");

        _defaultAgentId = configuration["AI_AGENT_ID"]
            ?? throw new InvalidOperationException("AI_AGENT_ID is not configured");

        _airAgentId = configuration["AI_AGENT_ID_AIR"];
        _eirAgentId = configuration["AI_AGENT_ID_EIR"];
        _bepAgentId = configuration["AI_AGENT_ID_BEP"];

        _logger.LogDebug(
            "Initializing AgentFrameworkService: endpoint={Endpoint}, agentId={AgentId}", 
            endpoint, 
            _defaultAgentId);

        TokenCredential credential;
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        if (environment == "Development")
        {
            _logger.LogInformation("Development: Using ChainedTokenCredential (AzureCli -> AzureDeveloperCli)");
            credential = new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzureDeveloperCliCredential()
            );
        }
        else
        {
            _logger.LogInformation("Production: Using ManagedIdentityCredential (system-assigned)");
            credential = new ManagedIdentityCredential();
        }

        _projectClient = new AIProjectClient(new Uri(endpoint), credential);
        _logger.LogInformation("AIProjectClient initialized successfully");
    }

    /// <summary>
    /// Get agent via Microsoft Agent Framework extension methods.
    /// Uses AIProjectClient.GetAIAgentAsync() which wraps v2 Agents API.
    /// </summary>
    private async Task<(ChatClientAgent Agent, string ReferenceName, string ConfiguredAgentId)> GetAgentAsync(
        string? configuredAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var effectiveAgentId = string.IsNullOrWhiteSpace(configuredAgentId)
            ? _defaultAgentId
            : configuredAgentId;

        var isDefaultAgent = string.Equals(effectiveAgentId, _defaultAgentId, StringComparison.OrdinalIgnoreCase);

        if (isDefaultAgent && _cachedAgent != null)
            return (_cachedAgent, _resolvedAgentReferenceName ?? NormalizeAgentReferenceName(_defaultAgentId), _defaultAgentId);

        await _agentLock.WaitAsync(cancellationToken);
        try
        {
            if (isDefaultAgent && _cachedAgent != null)
                return (_cachedAgent, _resolvedAgentReferenceName ?? NormalizeAgentReferenceName(_defaultAgentId), _defaultAgentId);

            _logger.LogInformation("Loading agent via Agent Framework: {AgentId}", effectiveAgentId);

            // Use Microsoft.Agents.AI.AzureAI extension method - handles v2 Agents API internally
            var resolved = await TryResolveAgentAsync(effectiveAgentId, cancellationToken);
            var agent = resolved.Agent;
            var referenceName = resolved.ReferenceName;

            if (isDefaultAgent)
            {
                _cachedAgent = agent;
                _resolvedAgentReferenceName = referenceName;
            }

            // Get the AgentVersion from the cached agent for metadata
            var agentVersion = agent.GetService<AgentVersion>();
            var definition = agentVersion?.Definition as PromptAgentDefinition;
            
            _logger.LogInformation(
                "Loaded agent: name={AgentName}, model={Model}, version={Version}", 
                agentVersion?.Name ?? effectiveAgentId,
                definition?.Model ?? "unknown",
                agentVersion?.Version ?? "latest");

            if (isDefaultAgent)
            {
                _resolvedAgentName = agentVersion?.Name ?? effectiveAgentId;
            }

            // Log StructuredInputs at debug level for troubleshooting
            if (definition?.StructuredInputs != null && definition.StructuredInputs.Count > 0)
            {
                _logger.LogDebug("Agent has {Count} StructuredInputs: {Keys}", 
                    definition.StructuredInputs.Count, 
                    string.Join(", ", definition.StructuredInputs.Keys));
            }

            return (agent, referenceName, effectiveAgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent: {AgentId}", effectiveAgentId);
            throw;
        }
        finally
        {
            _agentLock.Release();
        }
    }

    /// <summary>
    /// Streams agent response for a message using ProjectResponsesClient (Responses API).
    /// Returns StreamChunk objects containing text deltas, annotations, or MCP approval requests.
    /// </summary>
    /// <remarks>
    /// Uses direct ProjectResponsesClient instead of IChatClient because we need access to:
    /// - McpToolCallApprovalRequestItem for MCP approval flows
    /// - FileSearchCallResponseItem for file search quotes  
    /// - MessageResponseItem.OutputTextAnnotations for citations
    /// The IChatClient abstraction doesn't expose these specialized response types.
    /// </remarks>
    public async IAsyncEnumerable<StreamChunk> StreamMessageAsync(
        string conversationId,
        string message,
        List<string>? imageDataUris = null,
        List<FileAttachment>? fileDataUris = null,
        string? previousResponseId = null,
        McpApprovalResponse? mcpApproval = null,
        List<StandardSelection>? standardsSelected = null,
        PolicyConfig? policy = null,
        RetrievalConfig? retrieval = null,
        string? agentRouteHint = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var invokeActivity = ActivitySource.StartActivity("invoke_agent", ActivityKind.Client);
        invokeActivity?.SetTag("gen_ai.operation.name", "invoke_agent");
        invokeActivity?.SetTag("gen_ai.provider.name", "azure.ai.inference");
        invokeActivity?.SetTag("gen_ai.conversation.id", conversationId);
        invokeActivity?.SetTag("gen_ai.output.type", "text");
        invokeActivity?.SetTag("app.has_images", (imageDataUris?.Count ?? 0) > 0);
        invokeActivity?.SetTag("app.has_files", (fileDataUris?.Count ?? 0) > 0);
        invokeActivity?.SetTag("app.has_mcp_approval", mcpApproval != null);

        var routeDecision = DetermineAgentRoute(agentRouteHint, message, standardsSelected, fileDataUris);
        var selectedAgentId = ResolveAgentId(routeDecision.Route);
        var isBepComparisonRoute = routeDecision.Route == AgentRoute.Bep;

        // Ensure agent can be resolved before starting streaming and use the resolved name/reference.
        var agentContext = await GetAgentAsync(selectedAgentId, cancellationToken);
        var agent = agentContext.Agent;
        var agentVersion = agent.GetService<AgentVersion>();
        var resolvedAgentName = agentVersion?.Name ?? _resolvedAgentName ?? selectedAgentId;
        var resolvedAgentReferenceName = NormalizeAgentReferenceName(agentContext.ReferenceName);

        invokeActivity?.SetTag("gen_ai.agent.id", selectedAgentId);
        invokeActivity?.SetTag("gen_ai.agent.name", resolvedAgentName);
        invokeActivity?.SetTag("app.agent.route_hint", agentRouteHint ?? "(none)");
        invokeActivity?.SetTag("app.agent.route", routeDecision.Route.ToString().ToLowerInvariant());
        invokeActivity?.SetTag("app.agent.route_reason", routeDecision.Reason);

        if (string.IsNullOrWhiteSpace(resolvedAgentReferenceName))
        {
            throw new InvalidOperationException($"Unable to derive a valid agent reference name from configured agent id '{selectedAgentId}'.");
        }

        _logger.LogInformation(
            "Streaming message to conversation: {ConversationId}, ImageCount: {ImageCount}, FileCount: {FileCount}, HasApproval: {HasApproval}, Route={Route}, RouteReason={RouteReason}, AgentId={AgentId}",
            conversationId,
            imageDataUris?.Count ?? 0,
            fileDataUris?.Count ?? 0,
            mcpApproval != null,
            routeDecision.Route,
            routeDecision.Reason,
            selectedAgentId);

        yield return StreamChunk.AgentInfo(
            resolvedAgentName,
            routeDecision.Route.ToString().ToLowerInvariant());

        // Get ProjectResponsesClient for the agent and conversation
        ProjectResponsesClient responsesClient
            = _projectClient.OpenAI.GetProjectResponsesClientForAgent(
                new AgentReference(resolvedAgentReferenceName), 
                conversationId);

        _logger.LogDebug(
            "Using agent reference name for streaming: configured={ConfiguredAgentId}, reference={ReferenceName}, resolved={ResolvedName}",
            selectedAgentId,
            resolvedAgentReferenceName,
            resolvedAgentName);

        CreateResponseOptions options = new() { StreamingEnabled = true };

        var retrievalRan = false;
        var emittedAnnotations = false;
        List<GroundedClause> retrievedClauses = [];
        string? currentResponseId = null;

        // If continuing from MCP approval, link to previous response
        if (!string.IsNullOrEmpty(previousResponseId) && mcpApproval != null)
        {
            options.PreviousResponseId = previousResponseId;
            options.InputItems.Add(ResponseItem.CreateMcpApprovalResponseItem(
                mcpApproval.ApprovalRequestId,
                mcpApproval.Approved));
            
            _logger.LogInformation(
                "Resuming with MCP approval: RequestId={RequestId}, Approved={Approved}",
                mcpApproval.ApprovalRequestId,
                mcpApproval.Approved);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Attempted to stream empty message to conversation {ConversationId}", conversationId);
                throw new ArgumentException("Message cannot be null or whitespace", nameof(message));
            }

            if (isBepComparisonRoute)
            {
                options.InputItems.Add(ResponseItem.CreateUserMessageItem(BuildBepComparisonContextPrompt()));
                invokeActivity?.SetTag("app.standards.count", 0);
            }
            else
            {
                var effectiveStandards = await ResolveStandardsAsync(standardsSelected, cancellationToken);
                invokeActivity?.SetTag("app.standards.count", effectiveStandards.Count);

                if (effectiveStandards.Count > 0)
                {
                    using (var policyActivity = ActivitySource.StartActivity("build_policy_prompt", ActivityKind.Internal))
                    {
                        policyActivity?.SetTag("app.standards.count", effectiveStandards.Count);
                        var policyPrompt = _promptBuilder.BuildPolicyPrompt(policy, effectiveStandards);
                        options.InputItems.Add(ResponseItem.CreateUserMessageItem(policyPrompt));
                    }

                    if (_requirementsFirstEnabled)
                    {
                        using var requirementsActivity = ActivitySource.StartActivity("build_requirements_inventory", ActivityKind.Internal);
                        requirementsActivity?.SetTag("app.requirements.max_per_standard", _requirementsFirstMaxPerStandard);
                        requirementsActivity?.SetTag("app.requirements.page_size", _requirementsFirstPageSize);

                        var chunkType = retrieval?.ChunkType ?? "paragraph";
                        var requirements = await _standardsRetrieval.GetRequirementInventoryAsync(
                            effectiveStandards,
                            _requirementsFirstMaxPerStandard,
                            _requirementsFirstPageSize,
                            chunkType,
                            cancellationToken);

                        if (requirements.Count == 0)
                        {
                            _logger.LogWarning(
                                "No requirements retrieved for standards-first evaluation. Falling back to document-only analysis for this request.");
                            options.InputItems.Add(ResponseItem.CreateUserMessageItem(BuildNoStandardsFallbackPrompt()));
                        }
                        else
                        {
                            var requirementsPrompt = _promptBuilder.BuildRequirementsFirstPrompt(requirements);
                            options.InputItems.Add(ResponseItem.CreateUserMessageItem(requirementsPrompt));

                            retrievedClauses = requirements
                                .Select(r => new GroundedClause(
                                    r.StandardId,
                                    r.Version,
                                    r.ClauseRef,
                                    r.SourceDoc,
                                    r.RequirementText))
                                .ToList();
                        }
                    }
                    else
                    {
                        retrievedClauses = (await _standardsRetrieval.RetrieveClausesAsync(
                            message,
                            effectiveStandards,
                            retrieval,
                            cancellationToken)).ToList();

                        if (retrievedClauses.Count == 0)
                        {
                            _logger.LogWarning(
                                "No standards clauses were retrieved. Falling back to document-only analysis for this request.");
                            options.InputItems.Add(ResponseItem.CreateUserMessageItem(BuildNoStandardsFallbackPrompt()));
                        }
                        else
                        {
                            using var groundedActivity = ActivitySource.StartActivity("build_grounded_prompt", ActivityKind.Internal);
                            groundedActivity?.SetTag("app.grounded_clauses.count", retrievedClauses.Count);
                            var groundedPrompt = _promptBuilder.BuildGroundedClausesPrompt(retrievedClauses);
                            options.InputItems.Add(ResponseItem.CreateUserMessageItem(groundedPrompt));
                        }
                    }

                    retrievalRan = retrievedClauses.Count > 0;
                }
            }

            // Build user message with optional images and files
            ResponseItem userMessage = BuildUserMessage(message, imageDataUris, fileDataUris);
            options.InputItems.Add(userMessage);
        }

        // Dictionary to collect file search results for quote extraction
        var fileSearchQuotes = new Dictionary<string, string>();

        await foreach (var update
            in responsesClient.CreateResponseStreamingAsync(
                options: options,
                cancellationToken: cancellationToken))
        {
            if (update is StreamingResponseCreatedUpdate createdUpdate
                && !string.IsNullOrWhiteSpace(createdUpdate.Response?.Id))
            {
                currentResponseId = createdUpdate.Response.Id;
            }
            else if (update is StreamingResponseInProgressUpdate inProgressUpdate
                && !string.IsNullOrWhiteSpace(inProgressUpdate.Response?.Id))
            {
                currentResponseId = inProgressUpdate.Response.Id;
            }

            if (update is StreamingResponseOutputTextDeltaUpdate deltaUpdate)
            {
                yield return StreamChunk.Text(deltaUpdate.Delta);
            }
            else if (update is StreamingResponseOutputItemDoneUpdate itemDoneUpdate)
            {
                // Check for MCP tool approval request
                if (itemDoneUpdate.Item is McpToolCallApprovalRequestItem mcpApprovalItem)
                {
                    using var mcpActivity = ActivitySource.StartActivity("mcp_approval_request", ActivityKind.Internal);
                    mcpActivity?.SetTag("gen_ai.operation.name", "execute_tool");
                    mcpActivity?.SetTag("gen_ai.tool.name", mcpApprovalItem.ToolName ?? "unknown");
                    _logger.LogInformation(
                        "MCP tool approval requested: Id={Id}, Tool={Tool}, Server={Server}",
                        mcpApprovalItem.Id,
                        mcpApprovalItem.ToolName,
                        mcpApprovalItem.ServerLabel);
                    
                    // Parse tool arguments from BinaryData to string (JSON)
                    string? argumentsJson = mcpApprovalItem.ToolArguments?.ToString();
                    
                    yield return StreamChunk.McpApproval(new McpApprovalRequest
                    {
                        Id = mcpApprovalItem.Id,
                        ToolName = mcpApprovalItem.ToolName ?? "Unknown tool",
                        ServerLabel = mcpApprovalItem.ServerLabel ?? "MCP Server",
                        Arguments = argumentsJson,
                        ResponseId = currentResponseId
                    });
                    continue;
                }
                
                // Capture file search results for quote extraction
                if (itemDoneUpdate.Item is FileSearchCallResponseItem fileSearchItem)
                {
                    foreach (var result in fileSearchItem.Results)
                    {
                        if (!string.IsNullOrEmpty(result.FileId) && !string.IsNullOrEmpty(result.Text))
                        {
                            fileSearchQuotes[result.FileId] = result.Text;
                            _logger.LogDebug(
                                "Captured file search quote for FileId={FileId}, QuoteLength={Length}", 
                                result.FileId, 
                                result.Text.Length);
                        }
                    }
                    continue;
                }
                
                // Extract annotations/citations from completed output items
                var annotations = ExtractAnnotations(itemDoneUpdate.Item, fileSearchQuotes);
                if (annotations.Count > 0)
                {
                    _logger.LogInformation("Extracted {Count} annotations from response", annotations.Count);
                    emittedAnnotations = true;
                    invokeActivity?.AddEvent(new ActivityEvent("annotations.emitted"));
                    yield return StreamChunk.WithAnnotations(annotations);
                }
            }
            else if (update is StreamingResponseCompletedUpdate completedUpdate)
            {
                if (!string.IsNullOrWhiteSpace(completedUpdate.Response?.Id))
                {
                    currentResponseId = completedUpdate.Response.Id;
                }

                _lastUsage = completedUpdate.Response?.Usage;
                invokeActivity?.SetTag("gen_ai.usage.input_tokens", _lastUsage?.InputTokenCount ?? 0);
                invokeActivity?.SetTag("gen_ai.usage.output_tokens", _lastUsage?.OutputTokenCount ?? 0);
                invokeActivity?.SetTag("gen_ai.usage.total_tokens", _lastUsage?.TotalTokenCount ?? 0);
            }
            else if (update is StreamingResponseErrorUpdate errorUpdate)
            {
                _logger.LogError("Stream error: {Error}", errorUpdate.Message);
                invokeActivity?.SetStatus(ActivityStatusCode.Error, errorUpdate.Message);
                throw new InvalidOperationException($"Stream error: {errorUpdate.Message}");
            }
        }

        if (retrievalRan && !emittedAnnotations)
        {
            var fallbackAnnotations = BuildFallbackAnnotations(retrievedClauses);
            if (fallbackAnnotations.Count > 0)
            {
                _logger.LogWarning(
                    "No annotations emitted during retrieval-backed response. Emitting fallback citations (count={Count}).",
                    fallbackAnnotations.Count);
                invokeActivity?.AddEvent(new ActivityEvent("annotations.fallback_emitted"));
                yield return StreamChunk.WithAnnotations(fallbackAnnotations);
            }
            else
            {
                invokeActivity?.SetStatus(ActivityStatusCode.Error, "Missing citations for retrieval-backed response");
                throw new InvalidOperationException(
                    "Citations are required for retrieval-backed responses, but no annotations or grounded clauses were available.");
            }
        }

        _logger.LogInformation("Completed streaming for conversation: {ConversationId}", conversationId);
    }

    /// <summary>
    /// Supported image MIME types for vision capabilities.
    /// </summary>
    private static readonly HashSet<string> AllowedImageTypes = 
        ["image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp"];

    /// <summary>
    /// Supported document MIME types for file input.
    /// Note: Office documents (docx, pptx, xlsx) are NOT supported - they cannot be parsed.
    /// </summary>
    private static readonly HashSet<string> AllowedDocumentTypes = 
        [
            "application/pdf",
            "text/plain",
            "text/markdown",
            "text/csv",
            "application/json",
            "text/html",
            "application/xml",
            "text/xml"
        ];

    /// <summary>
    /// Text-based document MIME types that should be inlined as text rather than sent as file input.
    /// The Responses API only supports PDF for CreateInputFilePart.
    /// </summary>
    private static readonly HashSet<string> TextBasedDocumentTypes = 
        [
            "text/plain",
            "text/markdown",
            "text/csv",
            "application/json",
            "text/html",
            "application/xml",
            "text/xml"
        ];

    /// <summary>
    /// MIME types that can be sent as file input (only PDF is currently supported by Responses API).
    /// </summary>
    private static readonly HashSet<string> FileInputTypes = 
        [
            "application/pdf"
        ];

    /// <summary>
    /// Maximum number of images per message.
    /// </summary>
    private const int MaxImageCount = 5;

    /// <summary>
    /// Maximum number of files per message.
    /// </summary>
    private const int MaxFileCount = 10;

    /// <summary>
    /// Maximum size per image in bytes (5MB).
    /// </summary>
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum size per document file in bytes (20MB).
    /// </summary>
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    /// <summary>
    /// Builds a ResponseItem for the user message with optional image and file attachments.
    /// Validates count, size, MIME type, and Base64 format for both images and documents.
    /// </summary>
    private static ResponseItem BuildUserMessage(
        string message, 
        List<string>? imageDataUris,
        List<FileAttachment>? fileDataUris = null)
    {
        if ((imageDataUris == null || imageDataUris.Count == 0) && 
            (fileDataUris == null || fileDataUris.Count == 0))
        {
            return ResponseItem.CreateUserMessageItem(message);
        }

        var contentParts = new List<ResponseContentPart>
        {
            ResponseContentPart.CreateInputTextPart(message)
        };

        var errors = new List<string>();

        // Process images
        if (imageDataUris != null && imageDataUris.Count > 0)
        {
            // Enforce maximum image count
            if (imageDataUris.Count > MaxImageCount)
            {
                throw new ArgumentException(
                    $"Invalid image attachments: Too many images ({imageDataUris.Count}), maximum {MaxImageCount} allowed");
            }

            for (int i = 0; i < imageDataUris.Count; i++)
            {
                var dataUri = imageDataUris[i];
                
                // Validate data URI format
                if (!dataUri.StartsWith("data:"))
                {
                    errors.Add($"Image {i + 1}: Invalid format (must be data URI)");
                    continue;
                }

                var semiIndex = dataUri.IndexOf(';');
                var commaIndex = dataUri.IndexOf(',');
                
                if (semiIndex < 0 || commaIndex < 0 || commaIndex < semiIndex)
                {
                    errors.Add($"Image {i + 1}: Malformed data URI");
                    continue;
                }

                // Extract and validate MIME type
                var mediaType = dataUri[5..semiIndex].ToLowerInvariant();
                if (!AllowedImageTypes.Contains(mediaType))
                {
                    errors.Add($"Image {i + 1}: Unsupported type '{mediaType}'. Allowed: PNG, JPEG, GIF, WebP");
                    continue;
                }

                // Validate Base64 and decode
                var base64Data = dataUri[(commaIndex + 1)..];
                try
                {
                    var bytes = Convert.FromBase64String(base64Data);
                    
                    // Enforce size limit
                    if (bytes.Length > MaxImageSizeBytes)
                    {
                        var sizeMB = bytes.Length / (1024.0 * 1024.0);
                        errors.Add($"Image {i + 1}: Size {sizeMB:F1}MB exceeds maximum 5MB");
                        continue;
                    }
                    
                    contentParts.Add(ResponseContentPart.CreateInputImagePart(
                        BinaryData.FromBytes(bytes),
                        mediaType));
                }
                catch (FormatException)
                {
                    errors.Add($"Image {i + 1}: Invalid Base64 encoding");
                }
            }
        }

        // Process file attachments
        if (fileDataUris != null && fileDataUris.Count > 0)
        {
            // Enforce maximum file count
            if (fileDataUris.Count > MaxFileCount)
            {
                throw new ArgumentException(
                    $"Invalid file attachments: Too many files ({fileDataUris.Count}), maximum {MaxFileCount} allowed");
            }

            for (int i = 0; i < fileDataUris.Count; i++)
            {
                var file = fileDataUris[i];
                var dataUri = file.DataUri;
                
                // Validate data URI format
                if (!dataUri.StartsWith("data:"))
                {
                    errors.Add($"File {i + 1} ({file.FileName}): Invalid format (must be data URI)");
                    continue;
                }

                var semiIndex = dataUri.IndexOf(';');
                var commaIndex = dataUri.IndexOf(',');
                
                if (semiIndex < 0 || commaIndex < 0 || commaIndex < semiIndex)
                {
                    errors.Add($"File {i + 1} ({file.FileName}): Malformed data URI");
                    continue;
                }

                // Extract and validate MIME type
                var mediaType = dataUri[5..semiIndex].ToLowerInvariant();
                if (!AllowedDocumentTypes.Contains(mediaType))
                {
                    errors.Add($"File {i + 1} ({file.FileName}): Unsupported type '{mediaType}'");
                    continue;
                }

                // Verify MIME type matches what was declared
                if (!string.Equals(mediaType, file.MimeType.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"File {i + 1} ({file.FileName}): MIME type mismatch (declared: {file.MimeType}, detected: {mediaType})");
                    continue;
                }

                // Validate Base64 and decode
                var base64Data = dataUri[(commaIndex + 1)..];
                try
                {
                    var bytes = Convert.FromBase64String(base64Data);
                    
                    // Enforce size limit
                    if (bytes.Length > MaxFileSizeBytes)
                    {
                        var sizeMB = bytes.Length / (1024.0 * 1024.0);
                        errors.Add($"File {i + 1} ({file.FileName}): Size {sizeMB:F1}MB exceeds maximum 20MB");
                        continue;
                    }
                    
                    // Handle text-based files by inlining their content
                    // The Responses API only supports PDF for CreateInputFilePart
                    if (TextBasedDocumentTypes.Contains(mediaType))
                    {
                        // Decode text content and add as inline text with filename context
                        var textContent = System.Text.Encoding.UTF8.GetString(bytes);
                        var inlineText = $"\n\n--- Content of {file.FileName} ---\n{textContent}\n--- End of {file.FileName} ---\n";
                        contentParts.Add(ResponseContentPart.CreateInputTextPart(inlineText));
                    }
                    else if (FileInputTypes.Contains(mediaType))
                    {
                        // PDF files can be sent as file input
                        contentParts.Add(ResponseContentPart.CreateInputFilePart(
                            BinaryData.FromBytes(bytes),
                            mediaType,
                            file.FileName));
                    }
                }
                catch (FormatException)
                {
                    errors.Add($"File {i + 1} ({file.FileName}): Invalid Base64 encoding");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ArgumentException($"Invalid attachments: {string.Join("; ", errors)}");
        }

        return ResponseItem.CreateUserMessageItem(contentParts);
    }

    /// <summary>
    /// Extracts annotation information from a completed response item.
    /// </summary>
    private List<AnnotationInfo> ExtractAnnotations(
        ResponseItem? item, 
        Dictionary<string, string>? fileSearchQuotes = null)
    {
        using var activity = ActivitySource.StartActivity("extract_annotations", ActivityKind.Internal);
        var annotations = new List<AnnotationInfo>();
        
        if (item is not MessageResponseItem messageItem)
            return annotations;

        foreach (var content in messageItem.Content)
        {
            if (content.OutputTextAnnotations == null) continue;
            
            foreach (var annotation in content.OutputTextAnnotations)
            {
                var annotationInfo = annotation switch
                {
                    UriCitationMessageAnnotation uriAnnotation => new AnnotationInfo
                    {
                        Type = "uri_citation",
                        Label = uriAnnotation.Title ?? "Source",
                        Url = uriAnnotation.Uri?.ToString(),
                        StartIndex = uriAnnotation.StartIndex,
                        EndIndex = uriAnnotation.EndIndex
                    },
                    
                    FileCitationMessageAnnotation fileCitation => new AnnotationInfo
                    {
                        Type = "file_citation",
                        Label = fileCitation.Filename ?? fileCitation.FileId ?? "File",
                        FileId = fileCitation.FileId,
                        StartIndex = fileCitation.Index,
                        EndIndex = fileCitation.Index,
                        Quote = fileSearchQuotes?.TryGetValue(fileCitation.FileId ?? string.Empty, out var quote) == true 
                            ? quote : null
                    },
                    
                    FilePathMessageAnnotation filePath => new AnnotationInfo
                    {
                        Type = "file_path",
                        Label = "Generated File",
                        FileId = filePath.FileId,
                        StartIndex = filePath.Index,
                        EndIndex = filePath.Index
                    },
                    
                    ContainerFileCitationMessageAnnotation containerCitation => new AnnotationInfo
                    {
                        Type = "container_file_citation",
                        Label = containerCitation.Filename ?? "Container File",
                        FileId = containerCitation.FileId,
                        StartIndex = containerCitation.StartIndex,
                        EndIndex = containerCitation.EndIndex,
                        Quote = fileSearchQuotes?.TryGetValue(containerCitation.FileId, out var containerQuote) == true 
                            ? containerQuote : null
                    },
                    
                    _ => null
                };
                
                if (annotationInfo != null)
                    annotations.Add(annotationInfo);
            }
        }

        activity?.SetTag("app.annotations.count", annotations.Count);
        return annotations;
    }

    private async Task<List<StandardSelection>> ResolveStandardsAsync(
        List<StandardSelection>? standardsSelected,
        CancellationToken cancellationToken)
    {
        if (standardsSelected != null && standardsSelected.Count > 0)
        {
            if (!_standardsRetrieval.IsConfigured)
            {
                throw new InvalidOperationException(
                    $"Standards retrieval requires Azure AI Search configuration. {_standardsRetrieval.DisabledReason ?? "Missing AI_SEARCH_ENDPOINT/AI_SEARCH_INDEX configuration."}");
            }

            return standardsSelected;
        }

        if (!_standardsRetrieval.IsConfigured)
        {
            throw new InvalidOperationException(
                $"Standards retrieval requires Azure AI Search configuration. {_standardsRetrieval.DisabledReason ?? "Missing AI_SEARCH_ENDPOINT/AI_SEARCH_INDEX configuration."}");
        }

        var catalog = await _standardsRetrieval.GetStandardsCatalogAsync(cancellationToken);
        if (catalog.Count == 0)
        {
            throw new InvalidOperationException(
                "No standards were found in the standards index. Ensure the knowledge source is indexed and contains standards metadata.");
        }

        return catalog
            .Select((standard, index) => new StandardSelection
            {
                StandardId = standard.StandardNumber,
                Title = standard.StandardTitle,
                Version = standard.PublicationDate,
                Jurisdiction = standard.IssuingOrganization,
                Priority = index + 1,
                Mandatory = true
            })
            .ToList();
    }

    private static List<AnnotationInfo> BuildFallbackAnnotations(IReadOnlyList<GroundedClause> clauses)
    {
        var annotations = new List<AnnotationInfo>();
        if (clauses.Count == 0)
        {
            return annotations;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var clause in clauses)
        {
            var baseLabel = string.IsNullOrWhiteSpace(clause.ClauseRef)
                ? clause.StandardId
                : $"{clause.StandardId} {clause.ClauseRef}";

            var label = string.IsNullOrWhiteSpace(clause.SourceDoc)
                ? baseLabel
                : $"{baseLabel} • {clause.SourceDoc}";

            var quote = clause.ClauseText;
            var key = $"{label}|{quote}";
            if (!seen.Add(key))
            {
                continue;
            }

            annotations.Add(new AnnotationInfo
            {
                Type = "file_citation",
                Label = label,
                Quote = quote
            });
        }

        return annotations;
    }

    /// <summary>
    /// Create a new conversation for the agent.
    /// Uses ProjectConversation from Azure.AI.Projects for server-managed state.
    /// </summary>
    public async Task<string> CreateConversationAsync(string? firstMessage = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _logger.LogInformation("Creating new conversation");
            
            ProjectConversationCreationOptions conversationOptions = new();

            if (!string.IsNullOrEmpty(firstMessage))
            {
                // Store title in metadata (truncate to 50 chars)
                var title = firstMessage.Length > 50 
                    ? firstMessage[..50] + "..."
                    : firstMessage;
                conversationOptions.Metadata["title"] = title;
            }

            ProjectConversation conversation
                = await _projectClient.OpenAI.Conversations.CreateProjectConversationAsync(
                    conversationOptions,
                    cancellationToken);

            _logger.LogInformation(
                "Created conversation: {ConversationId}", 
                conversation.Id);
            return conversation.Id;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Conversation creation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation");
            throw;
        }
    }

    /// <summary>
    /// Get the agent metadata (name, description, etc.) for display in UI.
    /// Uses Agent Framework's ChatClientAgent which provides access to AgentVersion.
    /// </summary>
    public async Task<AgentMetadataResponse> GetAgentMetadataAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Ensure agent is loaded via Agent Framework
        var agent = (await GetAgentAsync(cancellationToken: cancellationToken)).Agent;

        if (_cachedMetadata != null)
            return _cachedMetadata;

        // Get AgentVersion from the ChatClientAgent's services
        var agentVersion = agent.GetService<AgentVersion>();
        if (agentVersion == null)
            throw new InvalidOperationException("Agent version not available from ChatClientAgent");

        var definition = agentVersion.Definition as PromptAgentDefinition;
        var metadata = agentVersion.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Log metadata keys at debug level for troubleshooting
        if (metadata != null && metadata.Count > 0)
        {
            _logger.LogDebug("Agent metadata keys: {Keys}", string.Join(", ", metadata.Keys));
        }

        // Parse starter prompts from metadata
        List<string>? starterPrompts = ParseStarterPrompts(metadata);

        _cachedMetadata = new AgentMetadataResponse
        {
            Id = _defaultAgentId,
            Object = "agent",
            CreatedAt = agentVersion.CreatedAt.ToUnixTimeSeconds(),
            Name = agentVersion.Name ?? "AI Assistant",
            Description = agentVersion.Description,
            Model = definition?.Model ?? string.Empty,
            Instructions = definition?.Instructions ?? string.Empty,
            Metadata = metadata,
            StarterPrompts = starterPrompts
        };

        return _cachedMetadata;
    }

    /// <summary>
    /// Parse starter prompts from agent metadata.
    /// Azure AI Foundry stores starter prompts as newline-separated text in the "starterPrompts" metadata key.
    /// Example: "How's the weather?\nIs your fridge running?\nTell me a joke"
    /// </summary>
    private List<string>? ParseStarterPrompts(Dictionary<string, string>? metadata)
    {
        if (metadata == null)
            return null;

        // Azure AI Foundry uses camelCase "starterPrompts" key with newline-separated values
        if (!metadata.TryGetValue("starterPrompts", out var starterPromptsValue))
            return null;

        if (string.IsNullOrWhiteSpace(starterPromptsValue))
            return null;

        // Split by newlines and filter out empty entries
        var prompts = starterPromptsValue
            .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (prompts.Count > 0)
        {
            _logger.LogDebug("Parsed {Count} starter prompts from agent metadata", prompts.Count);
            return prompts;
        }

        return null;
    }

    /// <summary>
    /// Get basic agent info string (for debugging).
    /// </summary>
    public async Task<string> GetAgentInfoAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var agent = (await GetAgentAsync(cancellationToken: cancellationToken)).Agent;
        var agentVersion = agent.GetService<AgentVersion>();
        return agentVersion?.Name ?? _defaultAgentId;
    }

    private string ResolveAgentId(AgentRoute route)
    {
        var configuredAgentId = route switch
        {
            AgentRoute.Air => _airAgentId,
            AgentRoute.Eir => _eirAgentId,
            AgentRoute.Bep => _bepAgentId,
            _ => _defaultAgentId
        };

        if (!string.IsNullOrWhiteSpace(configuredAgentId))
        {
            return configuredAgentId;
        }

        if (route != AgentRoute.Default)
        {
            _logger.LogWarning(
                "Route {Route} was selected but no dedicated agent id is configured. Falling back to default agent id {DefaultAgentId}.",
                route,
                _defaultAgentId);
        }

        return _defaultAgentId;
    }

    private RouteDecision DetermineAgentRoute(
        string? agentRouteHint,
        string message,
        List<StandardSelection>? standardsSelected,
        List<FileAttachment>? fileDataUris)
    {
        var explicitRoute = ParseExplicitRouteHint(agentRouteHint);
        if (explicitRoute.HasValue)
        {
            return new RouteDecision(explicitRoute.Value, "explicit_hint");
        }

        var normalizedFileNames = (fileDataUris ?? [])
            .Select(f => f.FileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToLowerInvariant())
            .ToList();

        var hasAirFile = normalizedFileNames.Any(name => name.Contains("air", StringComparison.Ordinal));
        var hasEirFile = normalizedFileNames.Any(name => name.Contains("eir", StringComparison.Ordinal));
        var hasBepFile = normalizedFileNames.Any(name => name.Contains("bep", StringComparison.Ordinal));

        if (hasBepFile && (hasAirFile || hasEirFile))
        {
            return new RouteDecision(AgentRoute.Bep, "filename_combo:bep+air_or_eir");
        }

        if (hasEirFile)
        {
            return new RouteDecision(AgentRoute.Eir, "filename:eir");
        }

        if (hasAirFile)
        {
            return new RouteDecision(AgentRoute.Air, "filename:air");
        }

        var selectedStandardIds = (standardsSelected ?? [])
            .Select(s => s.StandardId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToLowerInvariant())
            .ToList();

        var hasEirStandard = selectedStandardIds.Any(id => id.Contains("eir", StringComparison.Ordinal));
        var hasAirStandard = selectedStandardIds.Any(id => id.Contains("air", StringComparison.Ordinal));

        if (hasEirStandard)
        {
            return new RouteDecision(AgentRoute.Eir, "standards:eir");
        }

        if (hasAirStandard)
        {
            return new RouteDecision(AgentRoute.Air, "standards:air");
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            var normalizedMessage = message.ToLowerInvariant();
            if (normalizedMessage.Contains("bep", StringComparison.Ordinal)
                && (normalizedMessage.Contains("air", StringComparison.Ordinal)
                    || normalizedMessage.Contains("eir", StringComparison.Ordinal)))
            {
                return new RouteDecision(AgentRoute.Bep, "message:bep+air_or_eir");
            }

            if (normalizedMessage.Contains("eir", StringComparison.Ordinal))
            {
                return new RouteDecision(AgentRoute.Eir, "message:eir");
            }

            if (normalizedMessage.Contains("air", StringComparison.Ordinal))
            {
                return new RouteDecision(AgentRoute.Air, "message:air");
            }
        }

        return new RouteDecision(AgentRoute.Default, "fallback:default");
    }

    private static AgentRoute? ParseExplicitRouteHint(string? agentRouteHint)
    {
        if (string.IsNullOrWhiteSpace(agentRouteHint))
        {
            return null;
        }

        return agentRouteHint.Trim().ToLowerInvariant() switch
        {
            "air" => AgentRoute.Air,
            "eir" => AgentRoute.Eir,
            "bep" => AgentRoute.Bep,
            _ => null
        };
    }

    private enum AgentRoute
    {
        Default,
        Air,
        Eir,
        Bep
    }

    private sealed record RouteDecision(AgentRoute Route, string Reason);

    private static string BuildBepComparisonContextPrompt()
    {
        return "COMPARISON_CONTEXT\n\nYou are evaluating an uploaded BEP against uploaded AIR and EIR documents.\n- Prioritize direct cross-document consistency checks between BEP, AIR, and EIR.\n- Produce a complete structured report with score, findings, and remediation tasks.\n- Do not depend on standards retrieval unless explicitly provided in the conversation.";
    }

    private static string BuildNoStandardsFallbackPrompt()
    {
        return "STANDARDS_GROUNDING_NOTICE\n\nNo grounded standards clauses were retrieved for this request. Continue with document-only analysis and explicitly state that standards grounding was unavailable for this run.";
    }

    /// <summary>
    /// Get token usage from the last streaming response.
    /// </summary>
    public (int InputTokens, int OutputTokens, int TotalTokens)? GetLastUsage() =>
        _lastUsage is null ? null : (_lastUsage.InputTokenCount, _lastUsage.OutputTokenCount, _lastUsage.TotalTokenCount);

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _agentLock.Dispose();
            _logger.LogDebug("AgentFrameworkService disposed");
        }
    }

    private async Task<(ChatClientAgent Agent, string ReferenceName)> TryResolveAgentAsync(string configuredAgentId, CancellationToken cancellationToken)
    {
        var baseName = NormalizeAgentReferenceName(configuredAgentId);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new InvalidOperationException($"Configured AI_AGENT_ID '{configuredAgentId}' cannot be normalized to a valid agent name.");
        }

        try
        {
            // Always resolve by base name to avoid version suffix and invalid character issues.
            var agent = await _projectClient.GetAIAgentAsync(
                name: baseName,
                cancellationToken: cancellationToken);

            return (agent, baseName);
        }
        catch (Exception ex) when (ShouldFallbackToLatest(configuredAgentId, ex))
        {
            _logger.LogWarning(
                ex,
                "Configured agent version was not found for {ConfiguredAgentId}. Falling back to latest version of {BaseName}.",
                configuredAgentId,
                baseName);

            var fallbackAgent = await _projectClient.GetAIAgentAsync(
                name: baseName,
                cancellationToken: cancellationToken);

            return (fallbackAgent, baseName);
        }
    }

    private static bool ShouldFallbackToLatest(string configuredAgentId, Exception ex)
    {
        if (!configuredAgentId.Contains(':'))
        {
            return false;
        }

        var message = ex.Message;
        return message.Contains("with version not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ServiceError: not_found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("\"code\": \"not_found\"", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAgentReferenceName(string configuredAgentId)
    {
        var baseName = configuredAgentId.Split(':', 2)[0].Trim();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return string.Empty;
        }

        var normalized = new StringBuilder(baseName.Length);
        foreach (var ch in baseName)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-')
            {
                normalized.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                normalized.Append('-');
            }
        }

        var collapsed = normalized.ToString().Trim('-');
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        if (collapsed.Length > 63)
        {
            collapsed = collapsed[..63].TrimEnd('-');
        }

        return collapsed;
    }
}
