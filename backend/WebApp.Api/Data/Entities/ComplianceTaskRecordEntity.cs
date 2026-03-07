namespace WebApp.Api.Data.Entities;

public sealed class ComplianceTaskRecordEntity
{
	public Guid Id { get; set; }
	public string TaskId { get; set; } = string.Empty;
	public string LogicalTaskKey { get; set; } = string.Empty;
	public string DocumentId { get; set; } = string.Empty;
	public long EvaluationRunEntityId { get; set; }
	public EvaluationRunEntity EvaluationRun { get; set; } = null!;
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Severity { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string CitationText { get; set; } = string.Empty;
	public string ReferenceSource { get; set; } = string.Empty;
	public string? CitationUri { get; set; }
	public string AnchorKind { get; set; } = string.Empty;
	public string AnchorSelector { get; set; } = string.Empty;
	public string AnchorExcerpt { get; set; } = string.Empty;
	public double AnchorConfidence { get; set; }
	public DateTimeOffset AnchorLastValidatedAt { get; set; }
	public string? AnchorExtensionsJson { get; set; }
	public string ProvenanceSourcePipeline { get; set; } = string.Empty;
	public string? StandardId { get; set; }
	public string? ClauseId { get; set; }
	public string? PolicyId { get; set; }
	public DateTimeOffset? GeneratedAt { get; set; }
	public string? TaskExtensionsJson { get; set; }
	public bool IsSuperseded { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public long Version { get; set; }
	public List<TaskStateOverlayEntity> StateOverlays { get; set; } = new();
}
