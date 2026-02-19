using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BimCuIngest;

public class CuIngestFunction
{
    private static readonly Regex HeadingRegex = new(@"^#+\s*(?<id>\d+(?:\.\d+)*)?\s*(?<title>.*)$", RegexOptions.Compiled);
    private static readonly Regex EscapedNewlineRegex = new(@"\\r\\n|\\n|\\r", RegexOptions.Compiled);
    private static readonly Regex ParagraphBreakRegex = new(@"\n\s*\n", RegexOptions.Compiled);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CuIngestFunction> _logger;
    private readonly CuIngestSettings _settings;
    private readonly TokenCredential _credential;

    public CuIngestFunction(IHttpClientFactory httpClientFactory, ILogger<CuIngestFunction> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = CuIngestSettings.FromConfiguration(configuration);
        _credential = _settings.IsDevelopment
            ? new ChainedTokenCredential(new AzureCliCredential(), new AzureDeveloperCliCredential())
            : new ManagedIdentityCredential();
    }

    [Function("CuIngest")]
    public async Task RunAsync(
        [BlobTrigger("%CU_INPUT_CONTAINER%/%CU_INPUT_PREFIX%{name}", Connection = "DocumentsStorage")] Stream inputBlob,
        string name,
        CancellationToken cancellationToken)
    {
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Skipping non-PDF blob: {BlobName}", name);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.DocumentsStorageAccountName) && string.IsNullOrWhiteSpace(_settings.DocumentsStorageConnectionString))
        {
            _logger.LogError("Documents storage account name/connection string not configured.");
            return;
        }

        var containerClient = GetDocumentsContainerClient();
        var blobName = $"{_settings.InputPrefix}{name}";
        var blobClient = containerClient.GetBlobClient(blobName);
        var blobUrl = blobClient.Uri.ToString();

        _logger.LogInformation("Analyzing blob: {BlobUrl}", blobUrl);
        using var analyzerResult = await AnalyzeAsync(blobUrl, cancellationToken);

        var chunkDocs = BuildChunkDocuments(analyzerResult.RootElement, blobUrl, name);
        if (chunkDocs.Count == 0)
        {
            _logger.LogWarning("No chunks produced for blob: {BlobName}", name);
            return;
        }

        var outputBlobName = $"{_settings.OutputPrefix}{Path.GetFileNameWithoutExtension(name)}.cu.jsonl";
        var outputBlobClient = containerClient.GetBlobClient(outputBlobName);

        await using var outputStream = new MemoryStream();
        await using (var writer = new StreamWriter(outputStream, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            foreach (var doc in chunkDocs)
            {
                var json = JsonSerializer.Serialize(doc, CuIngestSettings.JsonOptions);
                await writer.WriteLineAsync(json);
            }
        }

        outputStream.Position = 0;
        await outputBlobClient.UploadAsync(outputStream, overwrite: true, cancellationToken);

        _logger.LogInformation("Wrote {Count} chunks to {OutputBlob}", chunkDocs.Count, outputBlobName);
    }

    private async Task<JsonDocument> AnalyzeAsync(string blobUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var analyzePath = _settings.AnalyzePath.Replace("{analyzerId}", _settings.AnalyzerId, StringComparison.OrdinalIgnoreCase);
        var analyzeUri = new Uri(new Uri(_settings.Endpoint), analyzePath);
        var payload = _settings.RequestTemplate.Replace("{{blobUrl}}", blobUrl, StringComparison.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        await AddAuthorizationAsync(request, cancellationToken);
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            if (!response.Headers.TryGetValues("Operation-Location", out var values))
            {
                throw new InvalidOperationException("Analyzer response missing Operation-Location header.");
            }

            var operationUrl = values.First();
            return await PollAnalyzeResultAsync(client, operationUrl, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(body);
    }

    private async Task<JsonDocument> PollAnalyzeResultAsync(HttpClient client, string operationUrl, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(_settings.PollIntervalSeconds);
        var maxAttempts = _settings.MaxPollAttempts;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, operationUrl);
            await AddAuthorizationAsync(pollRequest, cancellationToken);

            using var pollResponse = await client.SendAsync(pollRequest, cancellationToken);
            pollResponse.EnsureSuccessStatusCode();

            var payload = await pollResponse.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(payload);
            if (TryGetStatus(doc.RootElement, out var status))
            {
                if (status.Equals("succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    return doc;
                }
                if (status.Equals("failed", StringComparison.OrdinalIgnoreCase) || status.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Analyzer failed with status '{status}'.");
                }
            }

            await Task.Delay(interval, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for analyzer result.");
    }

    private static bool TryGetStatus(JsonElement element, out string status)
    {
        status = string.Empty;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("status", out var statusProp))
            {
                status = statusProp.GetString() ?? string.Empty;
                return true;
            }
        }

        return false;
    }

    private async Task AddAuthorizationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
            cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private List<Dictionary<string, object?>> BuildChunkDocuments(JsonElement root, string blobUrl, string blobName)
    {
        var result = root;
        if (root.TryGetProperty("result", out var resultProp))
        {
            result = resultProp;
        }
        else if (root.TryGetProperty("analyzeResult", out var analyzeResultProp))
        {
            result = analyzeResultProp;
        }

        if (!result.TryGetProperty("contents", out var contents) || contents.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Analyzer output missing contents array.");
            return new List<Dictionary<string, object?>>();
        }

        var docs = new List<Dictionary<string, object?>>();
        var contentIndex = 0;

        foreach (var content in contents.EnumerateArray())
        {
            contentIndex++;
            var markdown = content.TryGetProperty("markdown", out var markdownProp)
                ? markdownProp.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(markdown))
            {
                continue;
            }

            var pageSpans = ExtractPageSpans(content);
            var fieldValues = ExtractFieldValues(content);

            string? currentSectionId = null;
            string? currentSectionTitle = null;
            var paragraphIndex = 0;

            foreach (var segment in SplitParagraphs(markdown))
            {
                var cleaned = CleanSegment(segment.Text);
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    continue;
                }

                var isHeading = cleaned.TrimStart().StartsWith("#", StringComparison.Ordinal);
                if (isHeading)
                {
                    var match = HeadingRegex.Match(cleaned.Trim());
                    if (match.Success)
                    {
                        currentSectionId = match.Groups["id"].Value;
                        var title = match.Groups["title"].Value.Trim();
                        currentSectionTitle = string.IsNullOrWhiteSpace(title) ? null : title;
                    }
                }

                paragraphIndex++;
                var paragraphId = $"p{paragraphIndex:0000}";
                var pageNumber = ResolvePageNumber(pageSpans, segment.StartOffset);
                var chunkType = isHeading ? "section" : "paragraph";

                var doc = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = $"{Path.GetFileNameWithoutExtension(blobName)}|{contentIndex:00}|{paragraphId}",
                    ["content"] = cleaned,
                    ["chunkType"] = chunkType,
                    ["sectionId"] = string.IsNullOrWhiteSpace(currentSectionId) ? null : currentSectionId,
                    ["sectionTitle"] = currentSectionTitle,
                    ["paragraphId"] = paragraphId,
                    ["pageNumber"] = pageNumber,
                    ["startOffset"] = segment.StartOffset,
                    ["length"] = segment.Length,
                    ["sourceUrl"] = blobUrl,
                    ["blobName"] = blobName
                };

                if (fieldValues.TryGetValue("StandardNumber", out var standardNumber))
                {
                    doc["standardId"] = standardNumber;
                }

                if (fieldValues.TryGetValue("StandardTitle", out var standardTitle))
                {
                    doc["standardTitle"] = standardTitle;
                }

                foreach (var field in fieldValues)
                {
                    doc[field.Key] = field.Value;
                }

                docs.Add(doc);
            }
        }

        return docs;
    }

    private static Dictionary<string, string> ExtractFieldValues(JsonElement content)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!content.TryGetProperty("fields", out var fieldsProp) || fieldsProp.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var field in fieldsProp.EnumerateObject())
        {
            var value = ExtractFieldValue(field.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields[field.Name] = value;
            }
        }

        return fields;
    }

    private static string? ExtractFieldValue(JsonElement field)
    {
        if (field.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (field.TryGetProperty("valueString", out var valueString))
        {
            return valueString.GetString();
        }

        if (field.TryGetProperty("valueDate", out var valueDate))
        {
            return valueDate.GetString();
        }

        if (field.TryGetProperty("valueNumber", out var valueNumber))
        {
            return valueNumber.GetDouble().ToString(CultureInfo.InvariantCulture);
        }

        if (field.TryGetProperty("value", out var value))
        {
            return value.ToString();
        }

        return null;
    }

    private static List<PageSpan> ExtractPageSpans(JsonElement content)
    {
        var spans = new List<PageSpan>();
        if (!content.TryGetProperty("pages", out var pagesProp) || pagesProp.ValueKind != JsonValueKind.Array)
        {
            return spans;
        }

        foreach (var page in pagesProp.EnumerateArray())
        {
            if (!page.TryGetProperty("pageNumber", out var pageNumberProp))
            {
                continue;
            }

            var pageNumber = pageNumberProp.GetInt32();
            if (!page.TryGetProperty("spans", out var spansProp) || spansProp.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var span in spansProp.EnumerateArray())
            {
                if (span.TryGetProperty("offset", out var offsetProp) && span.TryGetProperty("length", out var lengthProp))
                {
                    spans.Add(new PageSpan(pageNumber, offsetProp.GetInt32(), lengthProp.GetInt32()));
                }
            }
        }

        return spans;
    }

    private static int? ResolvePageNumber(List<PageSpan> spans, int startOffset)
    {
        foreach (var span in spans)
        {
            if (startOffset >= span.Offset && startOffset < span.Offset + span.Length)
            {
                return span.PageNumber;
            }
        }

        return null;
    }

    private static IEnumerable<ParagraphSegment> SplitParagraphs(string markdown)
    {
        var normalized = NormalizeMarkdown(markdown);
        var index = 0;

        while (index < normalized.Length)
        {
            var breakMatch = ParagraphBreakRegex.Match(normalized, index);
            var nextBreak = breakMatch.Success ? breakMatch.Index : -1;
            if (nextBreak == -1)
            {
                yield return new ParagraphSegment(index, normalized.Length - index, normalized[index..]);
                yield break;
            }

            var length = nextBreak - index;
            var segmentText = normalized.Substring(index, length);
            yield return new ParagraphSegment(index, length, segmentText);
            index = breakMatch.Index + breakMatch.Length;
        }
    }

    private static string NormalizeMarkdown(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n").Replace("\r", "\n");

        // Some analyzer responses contain escaped newlines ("\\n") instead of literal newlines.
        // Decode those so paragraph splitting can produce multiple chunks.
        if (!normalized.Contains('\n') && normalized.Contains("\\n", StringComparison.Ordinal))
        {
            normalized = EscapedNewlineRegex.Replace(normalized, "\n");
        }

        return normalized;
    }

    private static string CleanSegment(string segment)
    {
        var lines = segment.Split('\n');
        var cleanedLines = lines
            .Where(line => !line.TrimStart().StartsWith("<!--", StringComparison.Ordinal));
        return string.Join("\n", cleanedLines).Trim();
    }

    private BlobContainerClient GetDocumentsContainerClient()
    {
        if (!string.IsNullOrWhiteSpace(_settings.DocumentsStorageConnectionString))
        {
            var client = new BlobServiceClient(_settings.DocumentsStorageConnectionString);
            return client.GetBlobContainerClient(_settings.InputContainer);
        }

        var accountName = _settings.DocumentsStorageAccountName;
        var endpoint = new Uri($"https://{accountName}.blob.core.windows.net");
        var clientWithIdentity = new BlobServiceClient(endpoint, _credential);
        return clientWithIdentity.GetBlobContainerClient(_settings.InputContainer);
    }

    private sealed record PageSpan(int PageNumber, int Offset, int Length);
    private sealed record ParagraphSegment(int StartOffset, int Length, string Text);

    private sealed class CuIngestSettings
    {
        public string Endpoint { get; init; } = string.Empty;
        public string AnalyzerId { get; init; } = string.Empty;
        public string InputContainer { get; init; } = "bim-standards";
        public string InputPrefix { get; init; } = "source/";
        public string OutputPrefix { get; init; } = "cu-output/";
        public string AnalyzePath { get; init; } = "contentunderstanding/analyzers/{analyzerId}:analyze?api-version=2025-11-01";
        public string RequestTemplate { get; init; } = "{\"input\":{\"url\":\"{{blobUrl}}\"}}";
        public int PollIntervalSeconds { get; init; } = 2;
        public int MaxPollAttempts { get; init; } = 60;
        public bool IsDevelopment { get; init; }
        public string DocumentsStorageAccountName { get; init; } = string.Empty;
        public string DocumentsStorageConnectionString { get; init; } = string.Empty;

        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        public static CuIngestSettings FromConfiguration(IConfiguration configuration)
        {
            var env = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") ?? "Production";

            return new CuIngestSettings
            {
                Endpoint = configuration["CU_ENDPOINT"] ?? string.Empty,
                AnalyzerId = configuration["CU_ANALYZER_ID"] ?? string.Empty,
                InputContainer = configuration["CU_INPUT_CONTAINER"] ?? "bim-standards",
                InputPrefix = configuration["CU_INPUT_PREFIX"] ?? "source/",
                OutputPrefix = configuration["CU_OUTPUT_PREFIX"] ?? "cu-output/",
                AnalyzePath = configuration["CU_ANALYZE_PATH"] ?? "contentunderstanding/analyzers/{analyzerId}:analyze?api-version=2025-11-01",
                RequestTemplate = configuration["CU_REQUEST_TEMPLATE"] ?? "{\"input\":{\"url\":\"{{blobUrl}}\"}}",
                PollIntervalSeconds = int.TryParse(configuration["CU_POLL_INTERVAL_SECONDS"], out var poll) ? poll : 2,
                MaxPollAttempts = int.TryParse(configuration["CU_MAX_POLL_ATTEMPTS"], out var max) ? max : 60,
                IsDevelopment = env.Equals("Development", StringComparison.OrdinalIgnoreCase),
                DocumentsStorageAccountName = configuration["DocumentsStorage__accountName"] ?? configuration["DOCUMENTS_STORAGE_ACCOUNT_NAME"] ?? string.Empty,
                DocumentsStorageConnectionString = configuration["DocumentsStorage"] ?? string.Empty
            };
        }
    }
}
