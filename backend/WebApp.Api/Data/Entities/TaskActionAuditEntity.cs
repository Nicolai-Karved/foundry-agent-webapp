namespace WebApp.Api.Data.Entities;

public sealed class TaskActionAuditEntity
{
	public Guid Id { get; set; }
	public Guid? TaskRecordEntityId { get; set; }
	public ComplianceTaskRecordEntity? TaskRecord { get; set; }
	public string DocumentId { get; set; } = string.Empty;
	public string ActionType { get; set; } = string.Empty;
	public string? PreviousValue { get; set; }
	public string? NewValue { get; set; }
	public string UserId { get; set; } = string.Empty;
	public DateTimeOffset Timestamp { get; set; }
	public string CorrelationId { get; set; } = string.Empty;
}
