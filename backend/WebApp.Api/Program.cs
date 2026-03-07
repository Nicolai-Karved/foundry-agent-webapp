using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using WebApp.Api.Configuration;
using WebApp.Api.Data;
using WebApp.Api.Models;
using WebApp.Api.Services;
using System.Security.Claims;

// Load .env files for local development BEFORE building the configuration.
// In production (Docker), Container Apps injects environment variables directly.
// Supports both repository-root .env (defaults) and backend/WebApp.Api/.env (local overrides).
var currentDir = Directory.GetCurrentDirectory();
var envCandidates = new[]
{
    Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".env")),
    Path.Combine(currentDir, ".env")
};

foreach (var envFilePath in envCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
{
    if (!File.Exists(envFilePath))
        continue;

    foreach (var line in File.ReadAllLines(envFilePath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            continue;

        var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            var rawValue = parts[1].Trim();
            var value = rawValue.Trim('"').Trim('\'');
            // Set as environment variables so they're picked up by configuration system
            Environment.SetEnvironmentVariable(parts[0], value);
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

// Enable PII logging for debugging auth issues (ONLY IN DEVELOPMENT)
if (builder.Environment.IsDevelopment())
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

// Add ServiceDefaults (telemetry, health checks)
builder.AddServiceDefaults();

// Add ProblemDetails service for standardized RFC 7807 error responses
builder.Services.AddProblemDetails();

builder.Services.Configure<EvaluationTaskSyncOptions>(
    builder.Configuration.GetSection(EvaluationTaskSyncOptions.SectionName));

EvaluationTaskSyncOptions evaluationTaskSyncOptions = builder.Configuration
    .GetSection(EvaluationTaskSyncOptions.SectionName)
    .Get<EvaluationTaskSyncOptions>()
    ?? new EvaluationTaskSyncOptions();

string preferredTaskPersistenceConnectionStringName = evaluationTaskSyncOptions.Persistence.PreferredConnectionStringName;
string? evaluationTaskPersistenceConnectionString = builder.Configuration.GetConnectionString(preferredTaskPersistenceConnectionStringName)
    ?? builder.Configuration["FS0002_TASK_PERSISTENCE_CONNECTION_STRING"];
bool hasEvaluationTaskPersistence = !string.IsNullOrWhiteSpace(evaluationTaskPersistenceConnectionString);

if (hasEvaluationTaskPersistence)
{
    builder.Services.AddDbContextFactory<EvaluationTaskDbContext>(options =>
        options.UseNpgsql(
            evaluationTaskPersistenceConnectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                evaluationTaskSyncOptions.Persistence.PreferredSchema)));
    builder.Services.AddSingleton<IEvaluationTaskPersistenceService, EvaluationTaskPersistenceService>();
}

// Configure CORS for local development and production
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:8089" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // In development, allow any localhost port for flexibility
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => 
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return uri.Host == "localhost" || uri.Host == "127.0.0.1";
                }
                return false;
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Override ClientId and TenantId from environment variables if provided
// These will be set by azd during deployment or by AppHost in local dev
var clientId = builder.Configuration["ENTRA_SPA_CLIENT_ID"]
    ?? builder.Configuration["AzureAd:ClientId"];

if (!string.IsNullOrEmpty(clientId))
{
    builder.Configuration["AzureAd:ClientId"] = clientId;
    // Set audience to match the expected token audience claim
    builder.Configuration["AzureAd:Audience"] = $"api://{clientId}";
}

var tenantId = builder.Configuration["ENTRA_TENANT_ID"]
    ?? builder.Configuration["AzureAd:TenantId"];

if (!string.IsNullOrEmpty(tenantId))
{
    builder.Configuration["AzureAd:TenantId"] = tenantId;
}

const string RequiredScope = "Chat.ReadWrite";
const string ScopePolicyName = "RequireChatScope";

// Add Microsoft Identity Web authentication
// Validates JWT bearer tokens issued for the SPA's delegated scope
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(options =>
    {
        builder.Configuration.Bind("AzureAd", options);
        var configuredClientId = builder.Configuration["AzureAd:ClientId"];

        options.TokenValidationParameters.ValidAudiences = new[]
        {
            configuredClientId,
            $"api://{configuredClientId}"
        };

        options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
    }, options => builder.Configuration.Bind("AzureAd", options));

builder.Services.AddAuthorization(options =>
{
    // Use Microsoft.Identity.Web's built-in scope validation
    options.AddPolicy(ScopePolicyName, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireScope(RequiredScope);
    });
});

// Register Azure AI Agent Service for Azure AI Foundry v2 Agents
// Uses Azure.AI.Projects SDK which works with v2 Agents API (/agents/ endpoint with human-readable IDs).
builder.Services.AddScoped<AgentFrameworkService>();
builder.Services.AddSingleton<StandardsRetrievalService>();
builder.Services.AddSingleton<StandardsPromptBuilder>();
builder.Services.AddSingleton<TaskLifecycleService>();
builder.Services.AddSingleton<PiiRedactionService>();

var app = builder.Build();

if (evaluationTaskSyncOptions.Enabled
    && hasEvaluationTaskPersistence
    && evaluationTaskSyncOptions.Persistence.AutoMigrateOnStartup)
{
    IDbContextFactory<EvaluationTaskDbContext> dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<EvaluationTaskDbContext>>();
    await using EvaluationTaskDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
}

// Add exception handling middleware for production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

// Add status code pages for consistent error responses
app.UseStatusCodePages();

// Map health checks
app.MapDefaultEndpoints();

// Serve static files from wwwroot (frontend)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowFrontend");

// Note: HTTPS redirection not needed - Azure Container Apps handles SSL termination at ingress
// The container receives HTTP traffic on port 8080

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Authenticated health endpoint exposes caller identity
app.MapGet("/api/health", (HttpContext context) =>
{
    var userId = context.User.FindFirst("oid")?.Value ?? "unknown";
    var userName = context.User.FindFirst("name")?.Value ?? "unknown";

    return Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        authenticated = true,
        user = new { id = userId, name = userName }
    });
})
.RequireAuthorization(ScopePolicyName)
.WithName("GetHealth");

// Standards catalog endpoint: returns selectable standards from Azure AI Search index content
app.MapGet("/api/standards", async (
    StandardsRetrievalService standardsService,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var standards = await standardsService.GetStandardsCatalogAsync(cancellationToken);

        var payload = standards.Select((s, index) => new
        {
            standardId = s.StandardNumber,
            title = s.StandardTitle,
            version = s.PublicationDate,
            jurisdiction = s.IssuingOrganization,
            publicationDate = s.PublicationDate,
            issuingOrganization = s.IssuingOrganization,
            priority = index + 1,
            mandatory = true
        });

        return Results.Ok(payload);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions);
    }
})
.WithName("GetStandardsCatalog");

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/standards/debug", async (
        StandardsRetrievalService standardsService,
        CancellationToken cancellationToken) =>
    {
        var diagnostics = await standardsService.GetCatalogDiagnosticsAsync(cancellationToken);
        return Results.Ok(diagnostics);
    })
    .WithName("GetStandardsCatalogDebug");

    app.MapGet("/api/standards/debug-clauses", async (
        StandardsRetrievalService standardsService,
        string standardId,
        string? q,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(standardId))
        {
            return Results.BadRequest(new { message = "standardId is required" });
        }

        var clauses = await standardsService.RetrieveClausesAsync(
            q ?? "*",
            new List<StandardSelection>
            {
                new() { StandardId = standardId, Priority = 1, Mandatory = true }
            },
            null,
            cancellationToken);

        return Results.Ok(new
        {
            standardId,
            query = q ?? "*",
            count = clauses.Count,
            sample = clauses.Take(3)
        });
    })
    .WithName("GetStandardsClausesDebug");
}

// Streaming Chat endpoint: Streams agent response via SSE (conversationId → chunks → usage → done)
// Supports MCP tool approval flow with previousResponseId and mcpApproval parameters
app.MapPost("/api/chat/stream", async (
    ChatRequest request,
    AgentFrameworkService agentService,
    HttpContext httpContext,
    ILoggerFactory loggerFactory,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("ChatStreamEndpoint");

    try
    {
        httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
        httpContext.Response.Headers.Append("Cache-Control", "no-cache");
        httpContext.Response.Headers.Append("Connection", "keep-alive");

        var conversationId = request.ConversationId
            ?? await agentService.CreateConversationAsync(request.Message, cancellationToken);

        await WriteConversationIdEvent(httpContext.Response, conversationId, cancellationToken);

        var startTime = DateTime.UtcNow;

        await foreach (var chunk in agentService.StreamMessageAsync(
            conversationId,
            request.Message,
            request.ImageDataUris,
            request.FileDataUris,
            request.PreviousResponseId,
            request.McpApproval,
            request.StandardsSelected,
            request.Policy,
            request.Retrieval,
            request.AgentRouteHint,
            cancellationToken))
        {
            if (chunk.IsText && chunk.TextDelta != null)
            {
                await WriteChunkEvent(httpContext.Response, chunk.TextDelta, cancellationToken);
            }
            else if (chunk.HasAnnotations && chunk.Annotations != null)
            {
                await WriteAnnotationsEvent(httpContext.Response, chunk.Annotations, cancellationToken);
            }
            else if (chunk.IsMcpApprovalRequest && chunk.McpApprovalRequest != null)
            {
                await WriteMcpApprovalRequestEvent(httpContext.Response, chunk.McpApprovalRequest, cancellationToken);
            }
            else if (chunk.HasAgentInfo && chunk.Agent != null)
            {
                try
                {
                    await WriteAgentInfoEvent(httpContext.Response, chunk.Agent, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed to write agent info event: {ex.Message}");
                }
            }
        }

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var usage = agentService.GetLastUsage();
        await WriteUsageEvent(
            httpContext.Response,
            duration,
            usage?.InputTokens ?? 0,
            usage?.OutputTokens ?? 0,
            usage?.TotalTokens ?? 0,
            cancellationToken);

        await WriteDoneEvent(httpContext.Response, cancellationToken);
    }
    catch (OperationCanceledException) when (
        cancellationToken.IsCancellationRequested ||
        httpContext.RequestAborted.IsCancellationRequested)
    {
        logger.LogInformation(
            "Chat stream was cancelled by client/request lifecycle. ConversationId={ConversationId}",
            request.ConversationId ?? "(new)");
    }
    catch (ArgumentException ex) when (ex.Message.Contains("Invalid") && (ex.Message.Contains("attachments") || ex.Message.Contains("image") || ex.Message.Contains("file")))
    {
        logger.LogWarning(ex, "Invalid chat stream request payload");

        // Validation errors from image/file processing - return 400 Bad Request
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex, 
            400, 
            environment.IsDevelopment());
        
        await WriteErrorEvent(
            httpContext.Response, 
            "BAD_REQUEST",
            errorResponse.Detail ?? errorResponse.Title, 
            cancellationToken);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Chat stream failed unexpectedly");

        var streamErrorCode = ex.Message.Contains("invalid_payload", StringComparison.OrdinalIgnoreCase)
            ? "INVALID_PAYLOAD"
            : ex.Message.Contains("No standards clauses were retrieved", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("No requirements were retrieved", StringComparison.OrdinalIgnoreCase)
            ? "STANDARDS_EMPTY"
            : "STREAM_FAILURE";

        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex, 
            500, 
            environment.IsDevelopment());
        
        await WriteErrorEvent(
            httpContext.Response, 
            streamErrorCode,
            errorResponse.Detail ?? errorResponse.Title, 
            cancellationToken);
    }
})
.RequireAuthorization(ScopePolicyName)
.WithName("StreamChatMessage");

if (evaluationTaskSyncOptions.Enabled && hasEvaluationTaskPersistence)
{
    app.MapPost("/api/task-sync/ingest", async (
        EvaluationTaskSyncRequest request,
        IEvaluationTaskPersistenceService persistenceService,
        HttpContext httpContext,
        IHostEnvironment environment,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var correlationId = GetCorrelationId(httpContext.Request);
            SetCorrelationIdHeader(httpContext.Response, correlationId);
            var response = await persistenceService.IngestAsync(request, correlationId, cancellationToken);
            return Results.Accepted($"/api/task-snapshots/{Uri.EscapeDataString(request.DocumentId)}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.UnprocessableEntity(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            var errorResponse = ErrorResponseFactory.CreateFromException(
                ex,
                500,
                environment.IsDevelopment());

            return Results.Problem(
                title: errorResponse.Title,
                detail: errorResponse.Detail,
                statusCode: errorResponse.Status,
                extensions: errorResponse.Extensions);
        }
    })
    .RequireAuthorization(ScopePolicyName)
    .WithName("IngestEvaluationTasks");

    app.MapGet("/api/task-snapshots/{documentId}", async (
        string documentId,
        bool includeSuperseded,
        IEvaluationTaskPersistenceService persistenceService,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var correlationId = GetCorrelationId(httpContext.Request);
        SetCorrelationIdHeader(httpContext.Response, correlationId);
        var userId = GetUserId(httpContext.User);
        var snapshot = await persistenceService.GetTaskSnapshotAsync(documentId, userId, includeSuperseded, cancellationToken);
        return snapshot is null ? Results.NotFound(new { message = "task snapshot not found" }) : Results.Ok(snapshot);
    })
    .RequireAuthorization(ScopePolicyName)
    .WithName("GetTaskSnapshot");

    app.MapPatch("/api/tasks/{taskId}/overlay", async (
        string taskId,
        UpdateComplianceTaskStatusRequest request,
        TaskLifecycleService taskService,
        IEvaluationTaskPersistenceService persistenceService,
        HttpContext httpContext,
        IHostEnvironment environment,
        CancellationToken cancellationToken) =>
    {
        try
        {
            if (!ComplianceTaskStatuses.IsValid(request.Status))
            {
                return Results.BadRequest(new
                {
                    message = "status must be one of: open, in_review, done, skipped, blocked"
                });
            }

            var correlationId = GetCorrelationId(httpContext.Request);
            SetCorrelationIdHeader(httpContext.Response, correlationId);
            var userId = GetUserId(httpContext.User);

            var result = await taskService.UpdateTaskStatusAsync(
                taskId,
                request,
                userId,
                correlationId,
                cancellationToken);

            if (result.NotFound)
            {
                return Results.NotFound(new { message = "task not found" });
            }

            if (result.VersionConflict)
            {
                return Results.Problem(
                    title: "Version conflict",
                    detail: "Task version mismatch. Refresh tasks and retry.",
                    statusCode: 409,
                    extensions: new Dictionary<string, object?>
                    {
                        ["task"] = result.UpdatedTask,
                        ["correlationId"] = correlationId
                    });
            }

            TaskOverlayUpdateResponse? overlay = await persistenceService.GetOverlayAsync(
                taskId,
                request.DocumentId,
                userId,
                cancellationToken);

            return overlay is null ? Results.NotFound(new { message = "task overlay not found" }) : Results.Ok(overlay);
        }
        catch (Exception ex)
        {
            var errorResponse = ErrorResponseFactory.CreateFromException(
                ex,
                500,
                environment.IsDevelopment());

            return Results.Problem(
                title: errorResponse.Title,
                detail: errorResponse.Detail,
                statusCode: errorResponse.Status,
                extensions: errorResponse.Extensions);
        }
    })
    .RequireAuthorization(ScopePolicyName)
    .WithName("UpdateTaskOverlay");
}

static async Task WriteConversationIdEvent(HttpResponse response, string conversationId, CancellationToken ct)
{
    await response.WriteAsync(
        $"data: {{\"type\":\"conversationId\",\"conversationId\":\"{conversationId}\"}}\n\n",
        ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteChunkEvent(HttpResponse response, string content, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "chunk", content });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteAnnotationsEvent(HttpResponse response, List<WebApp.Api.Models.AnnotationInfo> annotations, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        type = "annotations",
        annotations = annotations.Select(a => new
        {
            type = a.Type,
            label = a.Label,
            url = a.Url,
            fileId = a.FileId,
            textToReplace = a.TextToReplace,
            startIndex = a.StartIndex,
            endIndex = a.EndIndex,
            quote = a.Quote
        })
    });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteMcpApprovalRequestEvent(HttpResponse response, WebApp.Api.Models.McpApprovalRequest approval, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        type = "mcpApprovalRequest",
        approvalRequest = new
        {
            id = approval.Id,
            toolName = approval.ToolName,
            serverLabel = approval.ServerLabel,
            arguments = approval.Arguments,
            previousResponseId = approval.ResponseId
        }
    });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteAgentInfoEvent(HttpResponse response, WebApp.Api.Models.AgentInfo agent, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        type = "agent",
        agent = new
        {
            name = agent.Name,
            route = agent.Route
        }
    });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteUsageEvent(HttpResponse response, double duration, int promptTokens, int completionTokens, int totalTokens, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new
    {
        type = "usage",
        duration,
        promptTokens,
        completionTokens,
        totalTokens
    });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteDoneEvent(HttpResponse response, CancellationToken ct)
{
    await response.WriteAsync("data: {\"type\":\"done\"}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static async Task WriteErrorEvent(HttpResponse response, string code, string message, CancellationToken ct)
{
    var json = System.Text.Json.JsonSerializer.Serialize(new { type = "error", code, message });
    await response.WriteAsync($"data: {json}\n\n", ct);
    await response.Body.FlushAsync(ct);
}

static string GetCorrelationId(HttpRequest request)
{
    if (request.Headers.TryGetValue("X-Correlation-Id", out var values))
    {
        var value = values.ToString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    return Guid.NewGuid().ToString("N");
}

static string GetUserId(ClaimsPrincipal user)
{
    return user.FindFirst("oid")?.Value
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? "unknown";
}

static void SetCorrelationIdHeader(HttpResponse response, string correlationId)
{
    response.Headers["X-Correlation-Id"] = correlationId;
}

app.MapGet("/api/tasks", async (
    string documentId,
    TaskLifecycleService taskService,
    HttpContext httpContext,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Results.BadRequest(new
            {
                message = "documentId is required"
            });
        }

        var correlationId = GetCorrelationId(httpContext.Request);
        SetCorrelationIdHeader(httpContext.Response, correlationId);
        var userId = GetUserId(httpContext.User);
        var response = await taskService.GetTasksAsync(documentId, userId, correlationId, cancellationToken);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions);
    }
})
.RequireAuthorization(ScopePolicyName)
.WithName("ListComplianceTasks");

app.MapPatch("/api/tasks/{taskId}/status", async (
    string taskId,
    UpdateComplianceTaskStatusRequest request,
    TaskLifecycleService taskService,
    HttpContext httpContext,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (!ComplianceTaskStatuses.IsValid(request.Status))
        {
            return Results.BadRequest(new
            {
                message = "status must be one of: open, in_review, done, skipped, blocked"
            });
        }

        var correlationId = GetCorrelationId(httpContext.Request);
        SetCorrelationIdHeader(httpContext.Response, correlationId);
        var userId = GetUserId(httpContext.User);

        var result = await taskService.UpdateTaskStatusAsync(
            taskId,
            request,
            userId,
            correlationId,
            cancellationToken);

        if (result.NotFound)
        {
            return Results.NotFound(new
            {
                message = "task not found"
            });
        }

        if (result.VersionConflict)
        {
            return Results.Problem(
                title: "Version conflict",
                detail: "Task version mismatch. Refresh tasks and retry.",
                statusCode: 409,
                extensions: new Dictionary<string, object?>
                {
                    ["task"] = result.UpdatedTask,
                    ["correlationId"] = correlationId
                });
        }

        return Results.Ok(result.UpdatedTask);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions);
    }
})
.RequireAuthorization(ScopePolicyName)
.WithName("UpdateComplianceTaskStatus");

app.MapPost("/api/verification/rerun", async (
    RerunVerificationRequest request,
    TaskLifecycleService taskService,
    PiiRedactionService redactionService,
    HttpContext httpContext,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.DocumentId))
        {
            return Results.BadRequest(new
            {
                message = "documentId is required"
            });
        }

        var correlationId = GetCorrelationId(httpContext.Request);
        SetCorrelationIdHeader(httpContext.Response, correlationId);
        var userId = GetUserId(httpContext.User);

        var redactedRequest = request with
        {
            Snippet = redactionService.Redact(request.Snippet)
        };

        var response = await taskService.RerunVerificationAsync(redactedRequest, userId, correlationId, cancellationToken);
        return Results.Accepted($"/api/tasks?documentId={Uri.EscapeDataString(request.DocumentId)}", response);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions);
    }
})
.RequireAuthorization(ScopePolicyName)
.WithName("RerunComplianceVerification");

app.MapPost("/api/telemetry/events", (
    ComplianceTelemetryEventRequest request,
    PiiRedactionService redactionService,
    HttpContext httpContext,
    ILoggerFactory loggerFactory) =>
{
    if (string.IsNullOrWhiteSpace(request.EventName))
    {
        return Results.BadRequest(new
        {
            message = "eventName is required"
        });
    }

    var correlationId = GetCorrelationId(httpContext.Request);
    SetCorrelationIdHeader(httpContext.Response, correlationId);

    var sanitizedProperties = request.Properties.ToDictionary(
        entry => entry.Key,
        entry => redactionService.Redact(entry.Value),
        StringComparer.OrdinalIgnoreCase);

    var logger = loggerFactory.CreateLogger("ComplianceTelemetry");
    logger.LogInformation(
        "Compliance telemetry event received. EventName={EventName}, CorrelationId={CorrelationId}, PropertyCount={PropertyCount}",
        request.EventName,
        correlationId,
        sanitizedProperties.Count);

    return Results.Accepted(
        value: new ComplianceTelemetryAcceptedResponse
        {
            Status = "accepted",
            CorrelationId = correlationId,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        });
})
.RequireAuthorization(ScopePolicyName)
.WithName("PostComplianceTelemetryEvent");

app.MapGet("/api/tasks/{taskId}/citation-context", async (
    string taskId,
    string documentId,
    TaskLifecycleService taskService,
    HttpContext httpContext,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return Results.BadRequest(new
            {
                message = "documentId is required"
            });
        }

    var correlationId = GetCorrelationId(httpContext.Request);
    SetCorrelationIdHeader(httpContext.Response, correlationId);
    var result = await taskService.GetCitationContextAsync(taskId, documentId, cancellationToken);
        if (result.NotFound)
        {
            return Results.NotFound(new
            {
                message = "task not found"
            });
        }

        return Results.Ok(result.Response);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex,
            500,
            environment.IsDevelopment());

        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions);
    }
})
.RequireAuthorization(ScopePolicyName)
.WithName("GetComplianceTaskCitationContext");

// Get agent metadata (name, description, model, metadata)
// Used by frontend to display agent information in the UI
app.MapGet("/api/agent", async (
    AgentFrameworkService agentService,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var metadata = await agentService.GetAgentMetadataAsync(cancellationToken);
        return Results.Ok(metadata);
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex, 
            500, 
            environment.IsDevelopment());
        
        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
})
.RequireAuthorization(ScopePolicyName)
.WithName("GetAgentMetadata");

// Get agent info (for debugging)
app.MapGet("/api/agent/info", async (
    AgentFrameworkService agentService,
    IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    try
    {
        var agentInfo = await agentService.GetAgentInfoAsync(cancellationToken);
        return Results.Ok(new
        {
            info = agentInfo,
            status = "ready"
        });
    }
    catch (Exception ex)
    {
        var errorResponse = ErrorResponseFactory.CreateFromException(
            ex, 
            500, 
            environment.IsDevelopment());
        
        return Results.Problem(
            title: errorResponse.Title,
            detail: errorResponse.Detail,
            statusCode: errorResponse.Status,
            extensions: errorResponse.Extensions
        );
    }
})
.RequireAuthorization(ScopePolicyName)
.WithName("GetAgentInfo");

// Fallback route for SPA - serve index.html for any non-API routes
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
