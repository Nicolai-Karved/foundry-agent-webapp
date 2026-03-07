namespace WebApp.Api.Configuration;

public sealed class EvaluationTaskSyncOptions
{
	public const string SectionName = "EvaluationTaskSync";

	public bool Enabled { get; init; }
	public string SchemaVersion { get; init; } = "fs-0002/v1";
	public TaskSyncPayloadOptions Payload { get; init; } = new();
	public TaskSyncPersistenceOptions Persistence { get; init; } = new();
}

public sealed class TaskSyncPayloadOptions
{
	public int MaxRequestBodyBytes { get; init; } = 1048576;
	public int MaxTasksPerPayload { get; init; } = 250;
	public int MaxDescriptionLength { get; init; } = 4000;
	public int MaxCitationLength { get; init; } = 4000;
	public int MaxAnchorExcerptLength { get; init; } = 2000;
	public int MaxResolutionNoteLength { get; init; } = 2000;
}

public sealed class TaskSyncPersistenceOptions
{
	public string Provider { get; init; } = "PostgreSql";
	public string PreferredConnectionStringName { get; init; } = "Fs0002TaskPersistence";
	public string PreferredSchema { get; init; } = "fs0002";
	public bool AutoMigrateOnStartup { get; init; }
	public bool ReuseGatewayDatabaseCandidate { get; init; } = true;
	public bool RequireDedicatedSchema { get; init; } = true;
	public bool RequireDedicatedRoles { get; init; } = true;
}