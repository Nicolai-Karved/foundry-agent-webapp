using System.Text.Json;

namespace WebApp.Api.Models;

public record EvaluationTaskSyncRequest
{
	public required string SchemaVersion { get; init; }
	public required string DocumentId { get; init; }
	public required string DocumentVersionFingerprint { get; init; }
	public EvaluationTaskProducerInfo? Producer { get; init; }
	public required EvaluationTaskRunInfo EvaluationRun { get; init; }
	public required List<PortableComplianceTask> Tasks { get; init; }
	public Dictionary<string, JsonElement>? Extensions { get; init; }
}

public record EvaluationTaskProducerInfo
{
	public required string SourcePipeline { get; init; }
	public string? ServiceVersion { get; init; }
	public string? TenantId { get; init; }
}

public record EvaluationTaskRunInfo
{
	public required string EvaluationRunId { get; init; }
	public required DateTimeOffset StartedAt { get; init; }
	public required DateTimeOffset CompletedAt { get; init; }
	public required string CorrelationId { get; init; }
}

public record PortableComplianceTask
{
	public required string TaskId { get; init; }
	public required string LogicalTaskKey { get; init; }
	public required string Title { get; init; }
	public required string Description { get; init; }
	public required string Severity { get; init; }
	public required string Status { get; init; }
	public required PortableTaskCitation Citation { get; init; }
	public required PortableTaskAnchor Anchor { get; init; }
	public required PortableTaskProvenance Provenance { get; init; }
	public Dictionary<string, JsonElement>? Extensions { get; init; }
}

public record PortableTaskCitation
{
	public required string Text { get; init; }
	public required string ReferenceSource { get; init; }
	public string? Uri { get; init; }
}

public record PortableTaskAnchor
{
	public required string AnchorKind { get; init; }
	public required string Selector { get; init; }
	public required string Excerpt { get; init; }
	public required double Confidence { get; init; }
	public required DateTimeOffset LastValidatedAt { get; init; }
	public Dictionary<string, JsonElement>? Extensions { get; init; }
}

public record PortableTaskProvenance
{
	public required string SourcePipeline { get; init; }
	public string? StandardId { get; init; }
	public string? ClauseId { get; init; }
	public string? PolicyId { get; init; }
	public DateTimeOffset? GeneratedAt { get; init; }
}

public record TaskSyncReceiptResponse
{
	public required string SyncReceiptId { get; init; }
	public required string DocumentId { get; init; }
	public required string EvaluationRunId { get; init; }
	public required bool Deduplicated { get; init; }
	public required string Result { get; init; }
	public required string CorrelationId { get; init; }
	public required DateTimeOffset AcceptedAt { get; init; }
}

public record CanonicalTaskSnapshotResponse
{
	public required string DocumentId { get; init; }
	public required string SchemaVersion { get; init; }
	public required string EvaluationRunId { get; init; }
	public required List<CanonicalTaskProjection> Tasks { get; init; }
}

public record CanonicalTaskProjection
{
	public required string TaskId { get; init; }
	public required string LogicalTaskKey { get; init; }
	public required string Title { get; init; }
	public required string Description { get; init; }
	public required string Severity { get; init; }
	public required string Status { get; init; }
	public required string Citation { get; init; }
	public required string ReferenceSource { get; init; }
	public string? Excerpt { get; init; }
	public Dictionary<string, JsonElement>? Extensions { get; init; }
}

public record TaskOverlayUpdateResponse
{
	public required string TaskId { get; init; }
	public required string DocumentId { get; init; }
	public required string Status { get; init; }
	public string? ResolutionNote { get; init; }
	public required long Version { get; init; }
	public required DateTimeOffset UpdatedAt { get; init; }
}
