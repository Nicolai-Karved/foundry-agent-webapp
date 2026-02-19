namespace WebApp.Api.Models;

public record RequirementItem(
    string RequirementId,
    string StandardId,
    string? Version,
    string? ClauseRef,
    string SourceDoc,
    string RequirementText
);