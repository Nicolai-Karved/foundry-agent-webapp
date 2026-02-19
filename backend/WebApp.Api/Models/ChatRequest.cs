namespace WebApp.Api.Models;

public record ChatRequest
{
    public required string Message { get; init; }
    public string? ConversationId { get; init; }
    /// <summary>
    /// Base64-encoded image data URIs (e.g., data:image/png;base64,iVBORw0KG...)
    /// Images are sent inline with the message, no file upload needed.
    /// </summary>
    public List<string>? ImageDataUris { get; init; }
    /// <summary>
    /// File attachments with metadata (filename, MIME type, base64 data).
    /// Supports documents like PDF, DOCX, TXT, etc.
    /// </summary>
    public List<FileAttachment>? FileDataUris { get; init; }
    /// <summary>
    /// MCP tool approval response (for resuming after approval request).
    /// </summary>
    public McpApprovalResponse? McpApproval { get; init; }
    /// <summary>
    /// Response ID to continue from (for MCP approval flow).
    /// </summary>
    public string? PreviousResponseId { get; init; }
    /// <summary>
    /// Standards selected for policy and retrieval (Prompt 2 + Prompt 3).
    /// </summary>
    public List<StandardSelection>? StandardsSelected { get; init; }
    /// <summary>
    /// Optional policy overrides for Prompt 2 (Policy/Dynamic).
    /// </summary>
    public PolicyConfig? Policy { get; init; }
    /// <summary>
    /// Retrieval configuration for grounded standards clauses (Prompt 3).
    /// </summary>
    public RetrievalConfig? Retrieval { get; init; }
    /// <summary>
    /// Optional route hint for selecting specialist agent behavior.
    /// Supported values: air, eir, bep.
    /// </summary>
    public string? AgentRouteHint { get; init; }
}

/// <summary>
/// Represents a user's approval/rejection decision for an MCP tool call.
/// </summary>
public record McpApprovalResponse
{
    public required string ApprovalRequestId { get; init; }
    public required bool Approved { get; init; }
}

/// <summary>
/// Represents a file attachment with metadata for document upload.
/// </summary>
public record FileAttachment
{
    /// <summary>
    /// Base64 data URI (e.g., data:application/pdf;base64,...)
    /// </summary>
    public required string DataUri { get; init; }
    /// <summary>
    /// Original filename with extension
    /// </summary>
    public required string FileName { get; init; }
    /// <summary>
    /// MIME type (e.g., application/pdf, application/vnd.openxmlformats-officedocument.wordprocessingml.document)
    /// </summary>
    public required string MimeType { get; init; }
}

/// <summary>
/// Selected standard metadata used to build the Policy prompt and retrieval filters.
/// </summary>
public record StandardSelection
{
    public required string StandardId { get; init; }
    public string? Title { get; init; }
    public string? Version { get; init; }
    public string? Jurisdiction { get; init; }
    public int Priority { get; init; } = 1;
    public bool Mandatory { get; init; } = true;
}

/// <summary>
/// Policy prompt configuration for Prompt 2 (Policy/Dynamic).
/// </summary>
public record PolicyConfig
{
    public string? DocType { get; init; }
    public string? ValidationMode { get; init; }
    public string? ScoringMethod { get; init; }
    public double? MandatoryWeight { get; init; }
    public double? NonMandatoryWeight { get; init; }
    public bool? CriticalFailsImmediate { get; init; }
    public int? MaxMajorBeforeFail { get; init; }
    public string? ScoringNotes { get; init; }
    public string? RunId { get; init; }
    public string? ProjectProfile { get; init; }
    public string? CompanyInternalStandardId { get; init; }
}

/// <summary>
/// Retrieval configuration for Prompt 3 grounded clauses.
/// </summary>
public record RetrievalConfig
{
    public int? TopKClausesPerStandard { get; init; }
    public string? ChunkType { get; init; }
}
