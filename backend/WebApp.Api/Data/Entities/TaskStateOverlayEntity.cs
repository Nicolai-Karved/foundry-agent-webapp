namespace WebApp.Api.Data.Entities;

public sealed class TaskStateOverlayEntity
{
	public Guid Id { get; set; }
	public Guid TaskRecordEntityId { get; set; }
	public ComplianceTaskRecordEntity TaskRecord { get; set; } = null!;
	public string UserId { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? ResolutionNote { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public string Source { get; set; } = string.Empty;
	public long Version { get; set; }
}
