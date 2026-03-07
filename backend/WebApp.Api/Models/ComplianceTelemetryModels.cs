namespace WebApp.Api.Models;

public record ComplianceTelemetryEventRequest
{
    public required string EventName { get; init; }
    public DateTimeOffset? OccurredAtUtc { get; init; }
    public Dictionary<string, string?> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public record ComplianceTelemetryAcceptedResponse
{
    public required string Status { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset ReceivedAtUtc { get; init; }
}
