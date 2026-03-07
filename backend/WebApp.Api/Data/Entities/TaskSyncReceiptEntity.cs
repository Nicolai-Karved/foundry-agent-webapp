namespace WebApp.Api.Data.Entities;

public sealed class TaskSyncReceiptEntity
{
	public Guid Id { get; set; }
	public string SyncReceiptId { get; set; } = string.Empty;
	public string DocumentId { get; set; } = string.Empty;
	public string EvaluationRunId { get; set; } = string.Empty;
	public string IngestHash { get; set; } = string.Empty;
	public bool Deduplicated { get; set; }
	public string Result { get; set; } = string.Empty;
	public DateTimeOffset Timestamp { get; set; }
	public string CorrelationId { get; set; } = string.Empty;
}
