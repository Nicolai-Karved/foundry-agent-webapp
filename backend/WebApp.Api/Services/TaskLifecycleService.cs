using Microsoft.Extensions.Options;
using WebApp.Api.Configuration;
using WebApp.Api.Models;

namespace WebApp.Api.Services;

public class TaskLifecycleService
{
	private const int AuditRetentionDays = 30;
	private readonly ILogger<TaskLifecycleService> _logger;
	private readonly EvaluationTaskSyncOptions _options;
	private readonly IEvaluationTaskPersistenceService? _persistenceService;
	private readonly object _gate = new();
	private readonly Dictionary<string, List<ComplianceTask>> _tasksByDocumentId = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<TaskActionAudit>> _auditsByDocumentId = new(StringComparer.OrdinalIgnoreCase);

	public TaskLifecycleService(ILogger<TaskLifecycleService> logger)
		: this(logger, Options.Create(new EvaluationTaskSyncOptions()), null)
	{
	}

	public TaskLifecycleService(
		ILogger<TaskLifecycleService> logger,
		IOptions<EvaluationTaskSyncOptions> options,
		IEvaluationTaskPersistenceService? persistenceService)
	{
		_logger = logger;
		_options = options.Value;
		_persistenceService = persistenceService;
	}

	public Task<ComplianceTaskListResponse> GetTasksAsync(string documentId, string correlationId, CancellationToken cancellationToken)
	{
		return GetTasksAsync(documentId, "unknown", correlationId, cancellationToken);
	}

	public Task<ComplianceTaskListResponse> GetTasksAsync(string documentId, string userId, string correlationId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		if (ShouldUsePersistence())
		{
			return _persistenceService!.GetTasksAsync(documentId, userId, correlationId, cancellationToken);
		}

		_ = cancellationToken;

		List<ComplianceTask> tasks;
		lock (_gate)
		{
			if (!_tasksByDocumentId.TryGetValue(documentId, out var existingTasks))
			{
				existingTasks = CreateSeedTasks(documentId);
				_tasksByDocumentId[documentId] = existingTasks;
			}

			tasks = existingTasks.Select(CloneTask).ToList();
		}

		return Task.FromResult(new ComplianceTaskListResponse
		{
			DocumentId = documentId,
			CorrelationId = correlationId,
			Tasks = tasks
		});
	}

	public Task<(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict)> UpdateTaskStatusAsync(
		string taskId,
		UpdateComplianceTaskStatusRequest request,
		string userId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
		ArgumentNullException.ThrowIfNull(request);

		if (ShouldUsePersistence())
		{
			return _persistenceService!.UpdateTaskStatusAsync(taskId, request, userId, correlationId, cancellationToken);
		}

		_ = cancellationToken;

		lock (_gate)
		{
			if (!_tasksByDocumentId.TryGetValue(request.DocumentId, out var tasks))
			{
				return Task.FromResult<(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict)>((null, true, false));
			}

			var task = tasks.FirstOrDefault(t => t.TaskId.Equals(taskId, StringComparison.OrdinalIgnoreCase));
			if (task is null)
			{
				return Task.FromResult<(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict)>((null, true, false));
			}

			if (request.ExpectedVersion != task.Version)
			{
				return Task.FromResult<(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict)>((CloneTask(task), false, true));
			}

			var updatedTask = task with
			{
				Status = request.Status.ToLowerInvariant(),
				Version = task.Version + 1,
				Anchor = task.Anchor with
				{
					LastValidatedAt = DateTimeOffset.UtcNow
				}
			};

			var index = tasks.FindIndex(t => t.TaskId.Equals(taskId, StringComparison.OrdinalIgnoreCase));
			tasks[index] = updatedTask;

			AddAuditEntry(request.DocumentId, new TaskActionAudit
			{
				ActionId = Guid.NewGuid().ToString("N"),
				TaskId = updatedTask.TaskId,
				ActionType = "status_update",
				PreviousStatus = task.Status,
				NewStatus = updatedTask.Status,
				UserId = userId,
				Timestamp = DateTimeOffset.UtcNow,
				CorrelationId = correlationId
			});

			_logger.LogInformation(
				"Task status updated. DocumentId={DocumentId}, TaskId={TaskId}, PreviousStatus={PreviousStatus}, NewStatus={NewStatus}, CorrelationId={CorrelationId}",
				request.DocumentId,
				updatedTask.TaskId,
				task.Status,
				updatedTask.Status,
				correlationId);

			return Task.FromResult<(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict)>((CloneTask(updatedTask), false, false));
		}
	}

	public Task<RerunVerificationResponse> RerunVerificationAsync(
		RerunVerificationRequest request,
		string userId,
		string correlationId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (ShouldUsePersistence())
		{
			return _persistenceService!.RerunVerificationAsync(request, userId, correlationId, cancellationToken);
		}

		_ = cancellationToken;

		var requestId = Guid.NewGuid().ToString("N");

		lock (_gate)
		{
			if (!_tasksByDocumentId.TryGetValue(request.DocumentId, out var tasks))
			{
				tasks = CreateSeedTasks(request.DocumentId);
				_tasksByDocumentId[request.DocumentId] = tasks;
			}

			for (var index = 0; index < tasks.Count; index++)
			{
				var current = tasks[index];
				tasks[index] = current with
				{
					Version = current.Version + 1,
					Anchor = current.Anchor with
					{
						LastValidatedAt = DateTimeOffset.UtcNow
					}
				};
			}

			AddAuditEntry(request.DocumentId, new TaskActionAudit
			{
				ActionId = Guid.NewGuid().ToString("N"),
				TaskId = string.Empty,
				ActionType = "rerun_verification",
				PreviousStatus = string.Empty,
				NewStatus = string.Empty,
				UserId = userId,
				Timestamp = DateTimeOffset.UtcNow,
				CorrelationId = correlationId
			});
		}

		_logger.LogInformation(
			"Manual re-verify accepted. DocumentId={DocumentId}, RequestId={RequestId}, CorrelationId={CorrelationId}",
			request.DocumentId,
			requestId,
			correlationId);

		return Task.FromResult(new RerunVerificationResponse
		{
			DocumentId = request.DocumentId,
			RequestId = requestId,
			Status = "accepted",
			CorrelationId = correlationId
		});
	}

	public Task<(ComplianceTaskCitationContextResponse? Response, bool NotFound)> GetCitationContextAsync(
		string taskId,
		string documentId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		if (ShouldUsePersistence())
		{
			return _persistenceService!.GetCitationContextAsync(taskId, documentId, cancellationToken);
		}

		_ = cancellationToken;

		lock (_gate)
		{
			if (!_tasksByDocumentId.TryGetValue(documentId, out var tasks))
			{
				return Task.FromResult<(ComplianceTaskCitationContextResponse?, bool)>((null, true));
			}

			var task = tasks.FirstOrDefault(t => t.TaskId.Equals(taskId, StringComparison.OrdinalIgnoreCase));
			if (task is null)
			{
				return Task.FromResult<(ComplianceTaskCitationContextResponse?, bool)>((null, true));
			}

			var response = new ComplianceTaskCitationContextResponse
			{
				TaskId = task.TaskId,
				DocumentId = task.DocumentId,
				Citation = task.Citation,
				ReferenceSource = task.ReferenceSource,
				Context = $"{task.Citation} — {task.Description}"
			};

			return Task.FromResult<(ComplianceTaskCitationContextResponse?, bool)>((response, false));
		}
	}

	private void AddAuditEntry(string documentId, TaskActionAudit audit)
	{
		PruneExpiredAudits(documentId);

		if (!_auditsByDocumentId.TryGetValue(documentId, out var audits))
		{
			audits = new List<TaskActionAudit>();
			_auditsByDocumentId[documentId] = audits;
		}

		audits.Add(audit);
	}

	private void PruneExpiredAudits(string documentId)
	{
		if (!_auditsByDocumentId.TryGetValue(documentId, out var audits))
		{
			return;
		}

		var cutoff = DateTimeOffset.UtcNow.AddDays(-AuditRetentionDays);
		audits.RemoveAll(audit => audit.Timestamp < cutoff);
	}

	private static ComplianceTask CloneTask(ComplianceTask task)
	{
		return task with
		{
			Anchor = task.Anchor with { }
		};
	}

	private static List<ComplianceTask> CreateSeedTasks(string documentId)
	{
		return new List<ComplianceTask>
		{
			new()
			{
				TaskId = "task-1",
				DocumentId = documentId,
				Title = "Define AIR data owner for federated model exchange",
				Description = "Ownership responsibility is implied but not explicit for weekly exchange package.",
				Status = ComplianceTaskStatuses.Open,
				Citation = "ISO 19650-1 clause 5.1.7",
				ReferenceSource = "ISO 19650-1",
				Anchor = new ComplianceTaskAnchor
				{
					AnchorType = "contentControlTag",
					AnchorValue = "fs0001-air-owner",
					Confidence = 0.98,
					LastValidatedAt = DateTimeOffset.UtcNow
				},
				Version = 1
			},
			new()
			{
				TaskId = "task-2",
				DocumentId = documentId,
				Title = "Clarify EIR model exchange cadence",
				Description = "Current wording references monthly delivery while EIR appendix specifies fortnightly milestones.",
				Status = ComplianceTaskStatuses.InReview,
				Citation = "EIR Appendix C section 2.3",
				ReferenceSource = "Client EIR",
				Anchor = new ComplianceTaskAnchor
				{
					AnchorType = "textSearchFallback",
					AnchorValue = "Model exchange schedule",
					Confidence = 0.87,
					LastValidatedAt = DateTimeOffset.UtcNow
				},
				Version = 1
			}
		};
	}

	private bool ShouldUsePersistence()
	{
		return _options.Enabled && _persistenceService is not null;
	}
}
