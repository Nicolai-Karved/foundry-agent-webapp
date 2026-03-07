namespace WebApp.Api.Data.Entities;

public sealed class EvaluationRunEntity
{
	public long Id { get; set; }
	public string EvaluationRunId { get; set; } = string.Empty;
	public string DocumentId { get; set; } = string.Empty;
	public string DocumentVersionFingerprint { get; set; } = string.Empty;
	public string SourcePipeline { get; set; } = string.Empty;
	public string SchemaVersion { get; set; } = string.Empty;
	public string CorrelationId { get; set; } = string.Empty;
	public DateTimeOffset StartedAt { get; set; }
	public DateTimeOffset CompletedAt { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public List<ComplianceTaskRecordEntity> Tasks { get; set; } = new();
}
