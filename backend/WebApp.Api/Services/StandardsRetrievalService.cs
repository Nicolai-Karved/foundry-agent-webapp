using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Identity;
using System.Text.Json.Serialization;
using System.Diagnostics;
using WebApp.Api.Models;

namespace WebApp.Api.Services;

public record GroundedClause(
    string StandardId,
    string? Version,
    string? ClauseRef,
    string SourceDoc,
    string ClauseText
);

public record StandardCatalogItem(
    string StandardNumber,
    string StandardTitle,
    string? PublicationDate,
    string? IssuingOrganization
);

public record StandardsCatalogDiagnostics(
    bool IsConfigured,
    string AuthMode,
    string? DisabledReason,
    int TotalRetrievedRows,
    int DistinctCatalogItems,
    IReadOnlyList<object> SampleRows
);

public class StandardsRetrievalService
{
    private static readonly ActivitySource ActivitySource = new("WebApp.Api.StandardsRetrieval");
    private readonly SearchClient? _searchClient;
    private readonly ILogger<StandardsRetrievalService> _logger;
    private readonly int _defaultTopK;
    private readonly string _defaultChunkType;
    private readonly string? _semanticConfiguration;
    private readonly bool _isConfigured;
    private readonly string _authMode;
    private readonly string? _disabledReason;

    public bool IsConfigured => _isConfigured && _searchClient != null;
    public string AuthMode => _authMode;
    public string? DisabledReason => _disabledReason;

    public StandardsRetrievalService(IConfiguration configuration, ILogger<StandardsRetrievalService> logger)
    {
        _logger = logger;

        _defaultTopK = configuration.GetValue<int?>("StandardsRetrieval:TopKClausesPerStandard") ?? 6;
        _defaultChunkType = configuration["StandardsRetrieval:ChunkType"] ?? "paragraph";
        _semanticConfiguration = configuration["StandardsRetrieval:SemanticConfiguration"];

        var endpoint = configuration["AI_SEARCH_ENDPOINT"];
        var serviceName = configuration["SEARCH_SERVICE_NAME"];
        var indexName = configuration["AI_SEARCH_INDEX"];
        var apiKey = configuration["AI_SEARCH_KEY"];

        if (string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(serviceName))
        {
            endpoint = $"https://{serviceName}.search.windows.net";
        }

        if (string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(indexName)
            || endpoint.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            _isConfigured = false;
            _authMode = "none";
            _disabledReason = "Missing or invalid AI_SEARCH_ENDPOINT/AI_SEARCH_INDEX configuration";
            _searchClient = null;
            _logger.LogWarning("Azure Search is not configured. Standards retrieval is disabled.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(apiKey) && !apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            _searchClient = new SearchClient(endpointUri, indexName, new AzureKeyCredential(apiKey));
            _authMode = "api-key";
            _disabledReason = null;
            _logger.LogInformation("Azure Search configured with API key auth: endpoint={Endpoint}, index={Index}", endpointUri, indexName);
        }
        else
        {
            var credential = new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzureDeveloperCliCredential(),
                new ManagedIdentityCredential());

            _searchClient = new SearchClient(endpointUri, indexName, credential);
            _authMode = "entra";
            _disabledReason = null;
            _logger.LogInformation("Azure Search configured with Entra credential auth: endpoint={Endpoint}, index={Index}", endpointUri, indexName);
        }

        _isConfigured = true;
    }

    public async Task<IReadOnlyList<GroundedClause>> RetrieveClausesAsync(
        string query,
        IReadOnlyList<StandardSelection> standards,
        RetrievalConfig? retrievalConfig,
        CancellationToken cancellationToken)
    {
        using var retrievalActivity = ActivitySource.StartActivity("standards_retrieval", ActivityKind.Internal);
        retrievalActivity?.SetTag("app.query.length", query?.Length ?? 0);
        retrievalActivity?.SetTag("app.standards.count", standards.Count);
        if (!_isConfigured || _searchClient == null)
        {
            _logger.LogDebug("Skipping clause retrieval because Azure Search is not configured.");
            return Array.Empty<GroundedClause>();
        }

        if (standards.Count == 0)
        {
            return Array.Empty<GroundedClause>();
        }

        var topK = retrievalConfig?.TopKClausesPerStandard ?? _defaultTopK;
        var chunkType = retrievalConfig?.ChunkType ?? _defaultChunkType;
        var safeQuery = string.IsNullOrWhiteSpace(query) ? "*" : query;

        var results = new List<GroundedClause>();

        foreach (var standard in standards)
        {
            using var standardActivity = ActivitySource.StartActivity("standards_search", ActivityKind.Internal);
            standardActivity?.SetTag("app.standard.id", standard.StandardId);
            standardActivity?.SetTag("app.top_k", topK);
            standardActivity?.SetTag("app.chunk_type", chunkType);
            _logger.LogInformation(
                "Retrieving clauses: standardId={StandardId}, topK={TopK}, chunkType={ChunkType}",
                standard.StandardId,
                topK,
                chunkType);

            var response = await _searchClient.SearchAsync<StandardsSearchDocument>(
                safeQuery,
                BuildSearchOptions(topK, BuildFilter(standard.StandardId, chunkType)),
                cancellationToken);

            var appendedForStandard = 0;

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var doc = result.Document;
                var clauseText = GetClauseText(doc);
                if (string.IsNullOrWhiteSpace(clauseText))
                {
                    continue;
                }

                results.Add(new GroundedClause(
                    doc.StandardNumber ?? doc.StandardIdLower ?? doc.StandardId ?? standard.StandardId,
                    doc.PublicationDate ?? standard.Version,
                    BuildClauseRef(doc.SectionId, doc.ParagraphId, doc.PageNumber),
                    doc.BlobName ?? doc.StandardTitleLower ?? doc.StandardTitleAlt ?? doc.StandardTitle ?? doc.StandardIdLower ?? doc.StandardId ?? standard.StandardId,
                    clauseText));
                appendedForStandard++;
            }

            // Fallback: remove chunkType filter when strict chunk filtering yields no rows.
            if (appendedForStandard == 0)
            {
                standardActivity?.AddEvent(new ActivityEvent("fallback.without_chunk_type"));
                _logger.LogWarning(
                    "No clauses found with chunk filter for {StandardId}. Retrying without chunkType filter.",
                    standard.StandardId);

                var fallbackResponse = await _searchClient.SearchAsync<StandardsSearchDocument>(
                    safeQuery,
                    BuildSearchOptions(topK, BuildStandardOnlyFilter(standard.StandardId)),
                    cancellationToken);

                await foreach (var fallback in fallbackResponse.Value.GetResultsAsync())
                {
                    var doc = fallback.Document;
                    var clauseText = GetClauseText(doc);
                    if (string.IsNullOrWhiteSpace(clauseText))
                    {
                        continue;
                    }

                    results.Add(new GroundedClause(
                        doc.StandardNumber ?? doc.StandardIdLower ?? doc.StandardId ?? standard.StandardId,
                        doc.PublicationDate ?? standard.Version,
                        BuildClauseRef(doc.SectionId, doc.ParagraphId, doc.PageNumber),
                        doc.BlobName ?? doc.StandardTitleLower ?? doc.StandardTitleAlt ?? doc.StandardTitle ?? doc.StandardIdLower ?? doc.StandardId ?? standard.StandardId,
                        clauseText));
                    appendedForStandard++;
                }
            }

            // Final fallback: query without standard filter to avoid returning no grounding when index metadata is inconsistent.
            if (appendedForStandard == 0)
            {
                standardActivity?.AddEvent(new ActivityEvent("fallback.unfiltered"));
                _logger.LogWarning(
                    "No clauses found for selected standard {StandardId} after filtered retries. Using unfiltered standards fallback.",
                    standard.StandardId);

                var unfilteredResponse = await _searchClient.SearchAsync<StandardsSearchDocument>(
                    safeQuery,
                    BuildSearchOptions(topK, null),
                    cancellationToken);

                await foreach (var fallback in unfilteredResponse.Value.GetResultsAsync())
                {
                    var doc = fallback.Document;
                    var clauseText = GetClauseText(doc);
                    if (string.IsNullOrWhiteSpace(clauseText))
                    {
                        continue;
                    }

                    results.Add(new GroundedClause(
                        doc.StandardNumber ?? doc.StandardIdLower ?? doc.StandardId ?? standard.StandardId,
                        doc.PublicationDate ?? standard.Version,
                        BuildClauseRef(doc.SectionId, doc.ParagraphId, doc.PageNumber),
                        doc.BlobName ?? doc.StandardTitleLower ?? doc.StandardTitleAlt ?? doc.StandardTitle ?? doc.StandardIdLower ?? doc.StandardId ?? standard.StandardId,
                        clauseText));
                }
            }
            standardActivity?.SetTag("app.standard.result.count", appendedForStandard);
        }

        retrievalActivity?.SetTag("app.retrieval.result.count", results.Count);
        return results;
    }

    public async Task<IReadOnlyList<StandardCatalogItem>> GetStandardsCatalogAsync(CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("standards_catalog", ActivityKind.Internal);
        if (!_isConfigured || _searchClient == null)
        {
            _logger.LogDebug("Returning empty standards catalog because Azure Search is not configured.");
            return Array.Empty<StandardCatalogItem>();
        }

        const int maxRows = 1000;

        var options = new SearchOptions
        {
            Size = maxRows,
            IncludeTotalCount = false
        };

        options.Select.Add("StandardNumber");
        options.Select.Add("standardId");
        options.Select.Add("StandardTitle");
        options.Select.Add("standardTitle");
        options.Select.Add("PublicationDate");
        options.Select.Add("IssuingOrganization");

        var response = await _searchClient.SearchAsync<StandardsSearchDocument>("*", options, cancellationToken);

        var unique = new Dictionary<string, StandardCatalogItem>(StringComparer.OrdinalIgnoreCase);

        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            var number = (doc.StandardNumber ?? doc.StandardIdLower ?? doc.StandardId)?.Trim();
            if (string.IsNullOrWhiteSpace(number))
            {
                continue;
            }

            var title = doc.StandardTitleLower?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                title = doc.StandardTitleAlt?.Trim();
            }
            if (string.IsNullOrWhiteSpace(title))
            {
                title = doc.StandardTitle?.Trim();
            }

            unique[number] = new StandardCatalogItem(
                number,
                title ?? number,
                string.IsNullOrWhiteSpace(doc.PublicationDate) ? null : doc.PublicationDate,
                string.IsNullOrWhiteSpace(doc.IssuingOrganization) ? null : doc.IssuingOrganization);
        }

        var catalog = unique.Values
            .OrderBy(s => s.StandardNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        activity?.SetTag("app.catalog.count", catalog.Count);
        return catalog;
    }

    public async Task<IReadOnlyList<RequirementItem>> GetRequirementInventoryAsync(
        IReadOnlyList<StandardSelection> standards,
        int maxPerStandard,
        int pageSize,
        string chunkType,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("standards_requirements_inventory", ActivityKind.Internal);

        if (!_isConfigured || _searchClient == null)
        {
            _logger.LogDebug("Skipping requirements inventory because Azure Search is not configured.");
            return Array.Empty<RequirementItem>();
        }

        if (standards.Count == 0)
        {
            return Array.Empty<RequirementItem>();
        }

        var results = new List<RequirementItem>();
        var safePageSize = Math.Max(1, Math.Min(1000, pageSize));

        foreach (var standard in standards)
        {
            var appended = 0;
            var skip = 0;

            while (appended < maxPerStandard)
            {
                var options = BuildSearchOptions(safePageSize, BuildFilter(standard.StandardId, chunkType));
                options.Skip = skip;
                options.IncludeTotalCount = false;

                var response = await _searchClient.SearchAsync<StandardsSearchDocument>(
                    "*",
                    options,
                    cancellationToken);

                var batchCount = 0;

                await foreach (var result in response.Value.GetResultsAsync())
                {
                    var doc = result.Document;
                    var clauseText = GetClauseText(doc);
                    if (string.IsNullOrWhiteSpace(clauseText))
                    {
                        continue;
                    }

                    var clauseRef = BuildRequirementClauseRef(doc);
                    var requirementId = !string.IsNullOrWhiteSpace(doc.Uid)
                        ? doc.Uid
                        : string.Join("/", new[] { standard.StandardId, clauseRef ?? "clause", (skip + batchCount + 1).ToString("D4") });

                    results.Add(new RequirementItem(
                        requirementId,
                        doc.StandardNumber ?? doc.StandardIdLower ?? doc.StandardId ?? standard.StandardId,
                        doc.PublicationDate ?? standard.Version,
                        clauseRef,
                        doc.BlobName ?? doc.StandardTitleLower ?? doc.StandardTitleAlt ?? doc.StandardTitle ?? standard.StandardId,
                        clauseText));

                    appended++;
                    batchCount++;

                    if (appended >= maxPerStandard)
                    {
                        break;
                    }
                }

                if (batchCount == 0)
                {
                    break;
                }

                skip += batchCount;
            }

            _logger.LogInformation(
                "Requirement inventory loaded: standardId={StandardId}, count={Count}",
                standard.StandardId,
                appended);
        }

        activity?.SetTag("app.requirements.count", results.Count);
        return results;
    }

    public async Task<StandardsCatalogDiagnostics> GetCatalogDiagnosticsAsync(CancellationToken cancellationToken)
    {
        if (!_isConfigured || _searchClient == null)
        {
            return new StandardsCatalogDiagnostics(
                IsConfigured: false,
                AuthMode: _authMode,
                DisabledReason: _disabledReason,
                TotalRetrievedRows: 0,
                DistinctCatalogItems: 0,
                SampleRows: Array.Empty<object>());
        }

        const int maxRows = 25;
        var options = new SearchOptions
        {
            Size = maxRows,
            IncludeTotalCount = true
        };

        options.Select.Add("StandardNumber");
        options.Select.Add("standardId");
        options.Select.Add("StandardTitle");
        options.Select.Add("standardTitle");
        options.Select.Add("PublicationDate");
        options.Select.Add("IssuingOrganization");

        var response = await _searchClient.SearchAsync<StandardsSearchDocument>("*", options, cancellationToken);
        var sample = new List<object>();
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var row in response.Value.GetResultsAsync())
        {
            var doc = row.Document;
            var number = (doc.StandardNumber ?? doc.StandardIdLower ?? doc.StandardId)?.Trim();
            if (!string.IsNullOrWhiteSpace(number))
            {
                distinct.Add(number);
            }

            sample.Add(new
            {
                standardNumber = doc.StandardNumber,
                standardId = doc.StandardIdLower ?? doc.StandardId,
                standardTitle = doc.StandardTitleLower ?? doc.StandardTitleAlt ?? doc.StandardTitle,
                publicationDate = doc.PublicationDate,
                issuingOrganization = doc.IssuingOrganization
            });
        }

        return new StandardsCatalogDiagnostics(
            IsConfigured: true,
            AuthMode: _authMode,
            DisabledReason: null,
            TotalRetrievedRows: sample.Count,
            DistinctCatalogItems: distinct.Count,
            SampleRows: sample);
    }

    private static string BuildFilter(string standardId, string chunkType)
    {
        var escapedStandard = EscapeODataString(standardId);
        var escapedChunk = EscapeODataString(chunkType);
        return $"(StandardNumber eq '{escapedStandard}' or standardId eq '{escapedStandard}') and chunkType eq '{escapedChunk}'";
    }

    private static string BuildStandardOnlyFilter(string standardId)
    {
        var escapedStandard = EscapeODataString(standardId);
        return $"StandardNumber eq '{escapedStandard}' or standardId eq '{escapedStandard}'";
    }

    private SearchOptions BuildSearchOptions(int topK, string? filter)
    {
        var options = new SearchOptions
        {
            Size = topK
        };

        if (!string.IsNullOrWhiteSpace(filter))
        {
            options.Filter = filter;
        }

        options.Select.Add("standardId");
        options.Select.Add("StandardNumber");
        options.Select.Add("standardTitle");
        options.Select.Add("StandardTitle");
        options.Select.Add("PublicationDate");
        options.Select.Add("IssuingOrganization");
        options.Select.Add("uid");
        options.Select.Add("sectionId");
        options.Select.Add("paragraphId");
        options.Select.Add("pageNumber");
        options.Select.Add("snippet");
        options.Select.Add("content");
        options.Select.Add("blobName");
        options.Select.Add("sourceUrl");

        if (!string.IsNullOrWhiteSpace(_semanticConfiguration))
        {
            options.QueryType = SearchQueryType.Semantic;
            options.SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _semanticConfiguration
            };
        }

        return options;
    }

    private static string GetClauseText(StandardsSearchDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.Snippet))
        {
            return doc.Snippet;
        }

        if (!string.IsNullOrWhiteSpace(doc.Content))
        {
            return doc.Content;
        }

        // Fallback to metadata-derived text when chunk text is not present.
        var title = doc.StandardTitleLower ?? doc.StandardTitleAlt ?? doc.StandardTitle;
        if (!string.IsNullOrWhiteSpace(title))
        {
            var parts = new List<string> { title.Trim() };
            if (!string.IsNullOrWhiteSpace(doc.PublicationDate))
            {
                parts.Add($"Publication date: {doc.PublicationDate.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(doc.IssuingOrganization))
            {
                parts.Add($"Issuing organization: {doc.IssuingOrganization.Trim()}");
            }
            return string.Join(". ", parts);
        }

        return string.Empty;
    }

    private static string EscapeODataString(string value) => value.Replace("'", "''");

    private static string? BuildClauseRef(string? sectionId, string? paragraphId, int? pageNumber)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sectionId))
        {
            parts.Add(sectionId);
        }
        if (!string.IsNullOrWhiteSpace(paragraphId))
        {
            parts.Add(paragraphId);
        }
        if (pageNumber.HasValue)
        {
            parts.Add($"p.{pageNumber.Value}");
        }

        return parts.Count > 0 ? string.Join(" / ", parts) : null;
    }

    private static string? BuildRequirementClauseRef(StandardsSearchDocument doc)
    {
        var fromSections = BuildClauseRef(doc.SectionId, doc.ParagraphId, doc.PageNumber);
        if (!string.IsNullOrWhiteSpace(fromSections))
        {
            return fromSections;
        }

        if (!string.IsNullOrWhiteSpace(doc.ParagraphId))
        {
            return doc.ParagraphId;
        }

        if (!string.IsNullOrWhiteSpace(doc.SectionId))
        {
            return doc.SectionId;
        }

        if (doc.PageNumber is int page)
        {
            return $"p.{page}";
        }

        if (!string.IsNullOrWhiteSpace(doc.Uid))
        {
            var segments = doc.Uid.Split('-', StringSplitOptions.RemoveEmptyEntries);
            var lastSegment = segments.LastOrDefault();
            if (!string.IsNullOrWhiteSpace(lastSegment))
            {
                return lastSegment;
            }
        }

        return null;
    }

    private sealed class StandardsSearchDocument
    {
        [JsonPropertyName("uid")]
        public string? Uid { get; init; }
        public string? StandardId { get; init; }
        [JsonPropertyName("standardId")]
        public string? StandardIdLower { get; init; }
        public string? StandardNumber { get; init; }
        public string? StandardTitle { get; init; }
        public string? StandardTitleAlt { get; init; }
        [JsonPropertyName("standardTitle")]
        public string? StandardTitleLower { get; init; }
        public string? PublicationDate { get; init; }
        public string? IssuingOrganization { get; init; }
        public string? SectionId { get; init; }
        public string? ParagraphId { get; init; }
        public int? PageNumber { get; init; }
        public string? Snippet { get; init; }
        public string? Content { get; init; }
        public string? BlobName { get; init; }
        public string? SourceUrl { get; init; }
    }
}
