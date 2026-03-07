using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApp.Api.Configuration;
using WebApp.Api.Data;
using WebApp.Api.Data.Entities;
using WebApp.Api.Models;

namespace WebApp.Api.Services;

public sealed class EvaluationTaskPersistenceService : IEvaluationTaskPersistenceService
{
	private const string AcceptedResult = "accepted";
	private const string DeduplicatedResult = "deduplicated";
	private const string OverlaySource = "user_api";
	private const string RerunActionType = "rerun_verification";
	private const string SyncActionType = "sync_ingest";
	private const string StatusActionType = "status_update";
	private readonly IDbContextFactory<EvaluationTaskDbContext> _dbContextFactory;
	private readonly EvaluationTaskSyncOptions _options;
	private readonly ILogger<EvaluationTaskPersistenceService> _logger;
	private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

	public EvaluationTaskPersistenceService(
		IDbContextFactory<EvaluationTaskDbContext> dbContextFactory,
		IOptions<EvaluationTaskSyncOptions> options,
		ILogger<EvaluationTaskPersistenceService> logger)
	{
		_dbContextFactory = dbContextFactory;
		_options = options.Value;
		_logger = logger;
	}

	public async Task<TaskSyncReceiptResponse> IngestAsync(
		EvaluationTaskSyncRequest request,
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateSyncRequest(request);

		await using EvaluationTaskDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
		string ingestHash = ComputeHash(request);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		bool duplicateRequest = await db.TaskSyncReceipts.AnyAsync(
			x => x.DocumentId == request.DocumentId
				&& x.EvaluationRunId == request.EvaluationRun.EvaluationRunId
				&& x.IngestHash == ingestHash,
			cancellationToken);

		TaskSyncReceiptEntity receipt = new TaskSyncReceiptEntity
		{
			Id = Guid.NewGuid(),
			SyncReceiptId = Guid.NewGuid().ToString("N"),
			DocumentId = request.DocumentId,
			EvaluationRunId = request.EvaluationRun.EvaluationRunId,
			IngestHash = ingestHash,
			Deduplicated = duplicateRequest,
			Result = duplicateRequest ? DeduplicatedResult : AcceptedResult,
			Timestamp = now,
			CorrelationId = correlationId
		};

		db.TaskSyncReceipts.Add(receipt);

		if (duplicateRequest)
		{
			await db.SaveChangesAsync(cancellationToken);
			return MapReceipt(receipt);
		}

		EvaluationRunEntity? evaluationRun = await db.EvaluationRuns
			.Include(x => x.Tasks)
			.FirstOrDefaultAsync(
				x => x.DocumentId == request.DocumentId
					&& x.EvaluationRunId == request.EvaluationRun.EvaluationRunId,
				cancellationToken);

		if (evaluationRun is null)
		{
			evaluationRun = new EvaluationRunEntity
			{
				EvaluationRunId = request.EvaluationRun.EvaluationRunId,
				DocumentId = request.DocumentId,
				DocumentVersionFingerprint = request.DocumentVersionFingerprint,
				SourcePipeline = request.Producer?.SourcePipeline ?? request.Tasks.First().Provenance.SourcePipeline,
				SchemaVersion = request.SchemaVersion,
				CorrelationId = request.EvaluationRun.CorrelationId,
				StartedAt = request.EvaluationRun.StartedAt,
				CompletedAt = request.EvaluationRun.CompletedAt,
				CreatedAt = now,
				UpdatedAt = now
			};
			db.EvaluationRuns.Add(evaluationRun);
		}
		else
		{
			evaluationRun.DocumentVersionFingerprint = request.DocumentVersionFingerprint;
			evaluationRun.SourcePipeline = request.Producer?.SourcePipeline ?? evaluationRun.SourcePipeline;
			evaluationRun.SchemaVersion = request.SchemaVersion;
			evaluationRun.CorrelationId = request.EvaluationRun.CorrelationId;
			evaluationRun.StartedAt = request.EvaluationRun.StartedAt;
			evaluationRun.CompletedAt = request.EvaluationRun.CompletedAt;
			evaluationRun.UpdatedAt = now;
		}

		List<ComplianceTaskRecordEntity> existingTasks = await db.ComplianceTaskRecords
			.Include(x => x.StateOverlays)
			.Where(x => x.DocumentId == request.DocumentId)
			.ToListAsync(cancellationToken);

		Dictionary<string, ComplianceTaskRecordEntity> activeByLogicalTaskKey = existingTasks
			.Where(x => !x.IsSuperseded)
			.GroupBy(x => x.LogicalTaskKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(task => task.UpdatedAt).First(),
				StringComparer.OrdinalIgnoreCase);

		HashSet<string> incomingLogicalTaskKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (PortableComplianceTask incomingTask in request.Tasks)
		{
			incomingLogicalTaskKeys.Add(incomingTask.LogicalTaskKey);
			bool hasExistingTask = activeByLogicalTaskKey.TryGetValue(incomingTask.LogicalTaskKey, out ComplianceTaskRecordEntity? existingTask);

			if (hasExistingTask && existingTask is not null)
			{
				ApplyIncomingTask(existingTask, incomingTask, request.DocumentId, evaluationRun, now, preserveTaskId: true);
				continue;
			}

			ComplianceTaskRecordEntity newTask = new ComplianceTaskRecordEntity
			{
				Id = Guid.NewGuid(),
				TaskId = incomingTask.TaskId,
				CreatedAt = now,
				Version = 1
			};

			ApplyIncomingTask(newTask, incomingTask, request.DocumentId, evaluationRun, now, preserveTaskId: false);
			db.ComplianceTaskRecords.Add(newTask);
		}

		foreach (ComplianceTaskRecordEntity existingTask in existingTasks.Where(x => !x.IsSuperseded))
		{
			if (incomingLogicalTaskKeys.Contains(existingTask.LogicalTaskKey))
			{
				continue;
			}

			existingTask.IsSuperseded = true;
			existingTask.UpdatedAt = now;
			existingTask.Version += 1;
		}

		db.TaskActionAudits.Add(new TaskActionAuditEntity
		{
			Id = Guid.NewGuid(),
			DocumentId = request.DocumentId,
			ActionType = SyncActionType,
			PreviousValue = null,
			NewValue = receipt.Result,
			UserId = "system",
			Timestamp = now,
			CorrelationId = correlationId
		});

		await db.SaveChangesAsync(cancellationToken);

		_logger.LogInformation(
			"Task sync persisted. DocumentId={DocumentId}, EvaluationRunId={EvaluationRunId}, TaskCount={TaskCount}, Deduplicated={Deduplicated}, CorrelationId={CorrelationId}",
			request.DocumentId,
			request.EvaluationRun.EvaluationRunId,
			request.Tasks.Count,
			receipt.Deduplicated,
			correlationId);

		return MapReceipt(receipt);
	}

	public async Task<CanonicalTaskSnapshotResponse?> GetTaskSnapshotAsync(
		string documentId,
		string userId,
		bool includeSuperseded,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		await using EvaluationTaskDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
		EvaluationRunEntity? latestRun = await db.EvaluationRuns
			.Where(x => x.DocumentId == documentId)
			.OrderByDescending(x => x.CompletedAt)
			.ThenByDescending(x => x.UpdatedAt)
			.FirstOrDefaultAsync(cancellationToken);

		if (latestRun is null)
		{
			return null;
		}

		List<ComplianceTaskRecordEntity> tasks = await db.ComplianceTaskRecords
			.Include(x => x.StateOverlays)
			.Where(x => x.DocumentId == documentId && (includeSuperseded || !x.IsSuperseded))
			.OrderBy(x => x.Title)
			.ToListAsync(cancellationToken);

		return new CanonicalTaskSnapshotResponse
		{
			DocumentId = documentId,
			SchemaVersion = _options.SchemaVersion,
			EvaluationRunId = latestRun.EvaluationRunId,
			Tasks = tasks.Select(task => MapSnapshotTask(task, userId)).ToList()
		};
	}

	public async Task<ComplianceTaskListResponse> GetTasksAsync(
		string documentId,
		string userId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		await using EvaluationTaskDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
		List<ComplianceTaskRecordEntity> tasks = await db.ComplianceTaskRecords
			.Include(x => x.StateOverlays)
			.Where(x => x.DocumentId == documentId && !x.IsSuperseded)
			.OrderBy(x => x.Title)
			.ToListAsync(cancellationToken);

		return new ComplianceTaskListResponse
		{
			DocumentId = documentId,
			CorrelationId = correlationId,
			Tasks = tasks.Select(task => MapCompatibilityTask(task, userId)).ToList()
		};
	}

	public async Task<(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict)> UpdateTaskStatusAsync(
		string taskId,
		UpdateComplianceTaskStatusRequest request,
		string userId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		await using EvaluationTaskDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
		ComplianceTaskRecordEntity? task = await db.ComplianceTaskRecords
			.Include(x => x.StateOverlays)
			.FirstOrDefaultAsync(
				x => x.TaskId == taskId
					&& x.DocumentId == request.DocumentId
					&& !x.IsSuperseded,
				cancellationToken);

		if (task is null)
		{
			return (null, true, false);
		}

		TaskStateOverlayEntity? overlay = task.StateOverlays.FirstOrDefault(x => x.UserId == userId);
		long currentVersion = overlay?.Version ?? task.Version;
		if (request.ExpectedVersion != currentVersion)
		{
			return (MapCompatibilityTask(task, userId), false, true);
		}

		DateTimeOffset now = DateTimeOffset.UtcNow;
		string normalizedStatus = request.Status.ToLowerInvariant();
		string previousStatus = overlay?.Status ?? task.Status;

		if (overlay is null)
		{
			overlay = new TaskStateOverlayEntity
			{
				Id = Guid.NewGuid(),
				TaskRecordEntityId = task.Id,
				UserId = userId,
				Status = normalizedStatus,
				UpdatedAt = now,
				Source = OverlaySource,
				Version = currentVersion + 1
			};
			db.TaskStateOverlays.Add(overlay);
		}
		else
		{
			overlay.Status = normalizedStatus;
			overlay.UpdatedAt = now;
			overlay.Version = currentVersion + 1;
		}

		db.TaskActionAudits.Add(new TaskActionAuditEntity
		{
			Id = Guid.NewGuid(),
			TaskRecordEntityId = task.Id,
			DocumentId = task.DocumentId,
			ActionType = StatusActionType,
			PreviousValue = previousStatus,
			NewValue = normalizedStatus,
			UserId = userId,
			Timestamp = now,
			CorrelationId = correlationId
		});

		await db.SaveChangesAsync(cancellationToken);

		return (MapCompatibilityTask(task, userId), false, false);
	}

	public async Task<RerunVerificationResponse> RerunVerificationAsync(
		RerunVerificationRequest request,
		string userId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		await using EvaluationTaskDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		List<ComplianceTaskRecordEntity> tasks = await db.ComplianceTaskRecords
			.Where(x => x.DocumentId == request.DocumentId && !x.IsSuperseded)
			.ToListAsync(cancellationToken);

		foreach (ComplianceTaskRecordEntity task in tasks)
		{
			task.AnchorLastValidatedAt = now;
			task.UpdatedAt = now;
			task.Version += 1;
		}

		db.TaskActionAudits.Add(new TaskActionAuditEntity
		{
			Id = Guid.NewGuid(),
			DocumentId = request.DocumentId,
			ActionType = RerunActionType,
			UserId = userId,
			Timestamp = now,
			CorrelationId = correlationId
		});

		await db.SaveChangesAsync(cancellationToken);

		return new RerunVerificationResponse
		{
			DocumentId = request.DocumentId,
			RequestId = Guid.NewGuid().ToString("N"),
			Status = AcceptedResult,
			CorrelationId = correlationId
		};
	}

	public async Task<(ComplianceTaskCitationContextResponse? Response, bool NotFound)> GetCitationContextAsync(
		string taskId,
		string documentId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		await using EvaluationTaskDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
		ComplianceTaskRecordEntity? task = await db.ComplianceTaskRecords
			.FirstOrDefaultAsync(
				x => x.TaskId == taskId && x.DocumentId == documentId && !x.IsSuperseded,
				cancellationToken);

		if (task is null)
		{
			return (null, true);
		}

		return (new ComplianceTaskCitationContextResponse
		{
			TaskId = task.TaskId,
			DocumentId = task.DocumentId,
			Citation = task.CitationText,
			ReferenceSource = task.ReferenceSource,
			Context = $"{task.CitationText} — {task.Description}"
		}, false);
	}

	public async Task<TaskOverlayUpdateResponse?> GetOverlayAsync(
		string taskId,
		string documentId,
		string userId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		await using EvaluationTaskDbContext db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
		var result = await db.ComplianceTaskRecords
			.Include(x => x.StateOverlays)
			.Where(x => x.TaskId == taskId && x.DocumentId == documentId && !x.IsSuperseded)
			.Select(x => new
			{
				Task = x,
				Overlay = x.StateOverlays.FirstOrDefault(overlay => overlay.UserId == userId)
			})
			.FirstOrDefaultAsync(cancellationToken);

		if (result is null)
		{
			return null;
		}

		TaskStateOverlayEntity? overlay = result.Overlay;
		return new TaskOverlayUpdateResponse
		{
			TaskId = result.Task.TaskId,
			DocumentId = result.Task.DocumentId,
			Status = overlay?.Status ?? result.Task.Status,
			ResolutionNote = overlay?.ResolutionNote,
			Version = overlay?.Version ?? result.Task.Version,
			UpdatedAt = overlay?.UpdatedAt ?? result.Task.UpdatedAt
		};
	}

	private void ValidateSyncRequest(EvaluationTaskSyncRequest request)
	{
		if (!string.Equals(request.SchemaVersion, _options.SchemaVersion, StringComparison.Ordinal))
		{
			throw new ArgumentException($"Unsupported schema version '{request.SchemaVersion}'.", nameof(request));
		}

		if (request.Tasks.Count == 0)
		{
			throw new ArgumentException("At least one task is required.", nameof(request));
		}

		if (request.Tasks.Count > _options.Payload.MaxTasksPerPayload)
		{
			throw new ArgumentException("Payload exceeds the configured maximum task count.", nameof(request));
		}

		foreach (PortableComplianceTask task in request.Tasks)
		{
			if (task.Description.Length > _options.Payload.MaxDescriptionLength)
			{
				throw new ArgumentException("Task description exceeds the configured maximum length.", nameof(request));
			}

			if (task.Citation.Text.Length > _options.Payload.MaxCitationLength)
			{
				throw new ArgumentException("Task citation exceeds the configured maximum length.", nameof(request));
			}

			if (task.Anchor.Excerpt.Length > _options.Payload.MaxAnchorExcerptLength)
			{
				throw new ArgumentException("Task anchor excerpt exceeds the configured maximum length.", nameof(request));
			}
		}
	}

	private static string ComputeHash(EvaluationTaskSyncRequest request)
	{
		byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonSerializerOptions);
		byte[] hash = SHA256.HashData(jsonBytes);
		return Convert.ToHexString(hash);
	}

	private static TaskSyncReceiptResponse MapReceipt(TaskSyncReceiptEntity receipt)
	{
		return new TaskSyncReceiptResponse
		{
			SyncReceiptId = receipt.SyncReceiptId,
			DocumentId = receipt.DocumentId,
			EvaluationRunId = receipt.EvaluationRunId,
			Deduplicated = receipt.Deduplicated,
			Result = receipt.Result,
			CorrelationId = receipt.CorrelationId,
			AcceptedAt = receipt.Timestamp
		};
	}

	private void ApplyIncomingTask(
		ComplianceTaskRecordEntity entity,
		PortableComplianceTask incomingTask,
		string documentId,
		EvaluationRunEntity evaluationRun,
		DateTimeOffset now,
		bool preserveTaskId)
	{
		if (!preserveTaskId)
		{
			entity.TaskId = incomingTask.TaskId;
		}

		entity.LogicalTaskKey = incomingTask.LogicalTaskKey;
		entity.DocumentId = documentId;
		entity.EvaluationRun = evaluationRun;
		entity.Title = incomingTask.Title;
		entity.Description = incomingTask.Description;
		entity.Severity = incomingTask.Severity;
		entity.Status = incomingTask.Status;
		entity.CitationText = incomingTask.Citation.Text;
		entity.ReferenceSource = incomingTask.Citation.ReferenceSource;
		entity.CitationUri = incomingTask.Citation.Uri;
		entity.AnchorKind = incomingTask.Anchor.AnchorKind;
		entity.AnchorSelector = incomingTask.Anchor.Selector;
		entity.AnchorExcerpt = incomingTask.Anchor.Excerpt;
		entity.AnchorConfidence = incomingTask.Anchor.Confidence;
		entity.AnchorLastValidatedAt = incomingTask.Anchor.LastValidatedAt;
		entity.AnchorExtensionsJson = SerializeExtensions(incomingTask.Anchor.Extensions);
		entity.ProvenanceSourcePipeline = incomingTask.Provenance.SourcePipeline;
		entity.StandardId = incomingTask.Provenance.StandardId;
		entity.ClauseId = incomingTask.Provenance.ClauseId;
		entity.PolicyId = incomingTask.Provenance.PolicyId;
		entity.GeneratedAt = incomingTask.Provenance.GeneratedAt;
		entity.TaskExtensionsJson = SerializeExtensions(incomingTask.Extensions);
		entity.IsSuperseded = false;
		entity.UpdatedAt = now;
		entity.Version = entity.Version == 0 ? 1 : entity.Version + 1;
	}

	private static string? SerializeExtensions(Dictionary<string, JsonElement>? extensions)
	{
		if (extensions is null || extensions.Count == 0)
		{
			return null;
		}

		return JsonSerializer.Serialize(extensions, JsonSerializerOptions);
	}

	private static Dictionary<string, JsonElement>? DeserializeExtensions(string? extensionsJson)
	{
		if (string.IsNullOrWhiteSpace(extensionsJson))
		{
			return null;
		}

		return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(extensionsJson, JsonSerializerOptions);
	}

	private static ComplianceTask MapCompatibilityTask(ComplianceTaskRecordEntity task, string userId)
	{
		TaskStateOverlayEntity? overlay = task.StateOverlays.FirstOrDefault(x => x.UserId == userId);
		return new ComplianceTask
		{
			TaskId = task.TaskId,
			DocumentId = task.DocumentId,
			Title = task.Title,
			Description = task.Description,
			Status = overlay?.Status ?? task.Status,
			Citation = task.CitationText,
			ReferenceSource = task.ReferenceSource,
			Anchor = new ComplianceTaskAnchor
			{
				AnchorType = task.AnchorKind,
				AnchorValue = task.AnchorSelector,
				Confidence = task.AnchorConfidence,
				LastValidatedAt = task.AnchorLastValidatedAt
			},
			Version = overlay?.Version ?? task.Version
		};
	}

	private static CanonicalTaskProjection MapSnapshotTask(ComplianceTaskRecordEntity task, string userId)
	{
		TaskStateOverlayEntity? overlay = task.StateOverlays.FirstOrDefault(x => x.UserId == userId);
		string effectiveStatus = overlay?.Status ?? (task.IsSuperseded ? "superseded" : task.Status);
		return new CanonicalTaskProjection
		{
			TaskId = task.TaskId,
			LogicalTaskKey = task.LogicalTaskKey,
			Title = task.Title,
			Description = task.Description,
			Severity = task.Severity,
			Status = effectiveStatus,
			Citation = task.CitationText,
			ReferenceSource = task.ReferenceSource,
			Excerpt = task.AnchorExcerpt,
			Extensions = DeserializeExtensions(task.TaskExtensionsJson)
		};
	}
}
