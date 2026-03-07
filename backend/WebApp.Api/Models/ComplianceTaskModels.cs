namespace WebApp.Api.Models;

public static class ComplianceTaskStatuses
{
	public const string Open = "open";
	public const string InReview = "in_review";
	public const string Done = "done";
	public const string Skipped = "skipped";
	public const string Blocked = "blocked";

	private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
	{
		Open,
		InReview,
		Done,
		Skipped,
		Blocked
	};

	public static bool IsValid(string status)
	{
		return !string.IsNullOrWhiteSpace(status) && AllowedStatuses.Contains(status);
	}
}

public record ComplianceTaskAnchor
{
	public required string AnchorType { get; init; }
	public required string AnchorValue { get; init; }
	public required double Confidence { get; init; }
	public required DateTimeOffset LastValidatedAt { get; init; }
}

public record ComplianceTask
{
	public required string TaskId { get; init; }
	public required string DocumentId { get; init; }
	public required string Title { get; init; }
	public required string Description { get; init; }
	public required string Status { get; init; }
	public required string Citation { get; init; }
	public required string ReferenceSource { get; init; }
	public required ComplianceTaskAnchor Anchor { get; init; }
	public long Version { get; init; }
}

public record ComplianceTaskListResponse
{
	public required string DocumentId { get; init; }
	public required string CorrelationId { get; init; }
	public required List<ComplianceTask> Tasks { get; init; }
}

public record UpdateComplianceTaskStatusRequest
{
	public required string DocumentId { get; init; }
	public required string Status { get; init; }
	public long ExpectedVersion { get; init; }
}

public record RerunVerificationRequest
{
	public required string DocumentId { get; init; }
	public string? Snippet { get; init; }
	public bool IncludeSuggestions { get; init; } = true;
}

public record RerunVerificationResponse
{
	public required string DocumentId { get; init; }
	public required string RequestId { get; init; }
	public required string Status { get; init; }
	public required string CorrelationId { get; init; }
}

public record ComplianceTaskCitationContextResponse
{
	public required string TaskId { get; init; }
	public required string DocumentId { get; init; }
	public required string Citation { get; init; }
	public required string ReferenceSource { get; init; }
	public required string Context { get; init; }
}

public record TaskActionAudit
{
	public required string ActionId { get; init; }
	public required string TaskId { get; init; }
	public required string ActionType { get; init; }
	public required string PreviousStatus { get; init; }
	public required string NewStatus { get; init; }
	public required string UserId { get; init; }
	public required DateTimeOffset Timestamp { get; init; }
	public required string CorrelationId { get; init; }
}
