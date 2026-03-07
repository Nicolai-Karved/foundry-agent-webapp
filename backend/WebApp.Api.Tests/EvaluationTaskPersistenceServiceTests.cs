using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebApp.Api.Configuration;
using WebApp.Api.Data;
using WebApp.Api.Models;
using WebApp.Api.Services;
using Xunit;

namespace WebApp.Api.Tests;

public class EvaluationTaskPersistenceServiceTests
{
	[Fact]
	public async Task IngestAsync_ShouldPersistTasks_AndReturnSnapshot()
	{
		IEvaluationTaskPersistenceService service = CreateService();
		EvaluationTaskSyncRequest request = CreateRequest("doc-sync", "run-1", "task-1", "logical-1");

		TaskSyncReceiptResponse receipt = await service.IngestAsync(request, "corr-sync", CancellationToken.None);
		CanonicalTaskSnapshotResponse? snapshot = await service.GetTaskSnapshotAsync("doc-sync", "user-1", false, CancellationToken.None);

		Assert.False(receipt.Deduplicated);
		Assert.Equal("accepted", receipt.Result);
		Assert.NotNull(snapshot);
		Assert.Equal("doc-sync", snapshot!.DocumentId);
		Assert.Single(snapshot.Tasks);
		Assert.Equal("task-1", snapshot.Tasks[0].TaskId);
	}

	[Fact]
	public async Task IngestAsync_ShouldDeduplicate_WhenSamePayloadIsSubmittedTwice()
	{
		IEvaluationTaskPersistenceService service = CreateService();
		EvaluationTaskSyncRequest request = CreateRequest("doc-dedupe", "run-1", "task-1", "logical-1");

		TaskSyncReceiptResponse first = await service.IngestAsync(request, "corr-1", CancellationToken.None);
		TaskSyncReceiptResponse second = await service.IngestAsync(request, "corr-2", CancellationToken.None);
		CanonicalTaskSnapshotResponse? snapshot = await service.GetTaskSnapshotAsync("doc-dedupe", "user-1", false, CancellationToken.None);

		Assert.False(first.Deduplicated);
		Assert.True(second.Deduplicated);
		Assert.Equal("deduplicated", second.Result);
		Assert.NotNull(snapshot);
		Assert.Single(snapshot!.Tasks);
	}

	[Fact]
	public async Task UpdateTaskStatusAsync_ShouldCarryForwardOverlay_WhenMatchingTaskIsResynced()
	{
		IEvaluationTaskPersistenceService service = CreateService();
		await service.IngestAsync(CreateRequest("doc-overlay", "run-1", "task-a", "logical-a"), "corr-1", CancellationToken.None);

		ComplianceTaskListResponse beforeUpdate = await service.GetTasksAsync("doc-overlay", "user-7", "corr-2", CancellationToken.None);
		ComplianceTask originalTask = beforeUpdate.Tasks.Single();

		(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict) update = await service.UpdateTaskStatusAsync(
			originalTask.TaskId,
			new UpdateComplianceTaskStatusRequest
			{
				DocumentId = "doc-overlay",
				Status = ComplianceTaskStatuses.Done,
				ExpectedVersion = originalTask.Version
			},
			"user-7",
			"corr-3",
			CancellationToken.None);

		Assert.False(update.NotFound);
		Assert.False(update.VersionConflict);
		Assert.NotNull(update.UpdatedTask);
		Assert.Equal(ComplianceTaskStatuses.Done, update.UpdatedTask!.Status);

		EvaluationTaskSyncRequest rerun = CreateRequest("doc-overlay", "run-2", "task-b", "logical-a");
		rerun = rerun with
		{
			Tasks = new List<PortableComplianceTask>
			{
				rerun.Tasks[0] with
				{
					Title = "Updated title from rerun",
					Description = "Updated description from rerun"
				}
			}
		};

		await service.IngestAsync(rerun, "corr-4", CancellationToken.None);
		ComplianceTaskListResponse afterRerun = await service.GetTasksAsync("doc-overlay", "user-7", "corr-5", CancellationToken.None);
		ComplianceTask rerunTask = afterRerun.Tasks.Single();

		Assert.Equal("task-a", rerunTask.TaskId);
		Assert.Equal(ComplianceTaskStatuses.Done, rerunTask.Status);
		Assert.Equal("Updated title from rerun", rerunTask.Title);
	}

	private static IEvaluationTaskPersistenceService CreateService()
	{
		ServiceCollection services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IOptions<EvaluationTaskSyncOptions>>(Options.Create(new EvaluationTaskSyncOptions
		{
			Enabled = true
		}));
		services.AddDbContextFactory<EvaluationTaskDbContext>(options =>
		{
			options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
		});
		services.AddSingleton<IEvaluationTaskPersistenceService, EvaluationTaskPersistenceService>();

		ServiceProvider serviceProvider = services.BuildServiceProvider();
		return serviceProvider.GetRequiredService<IEvaluationTaskPersistenceService>();
	}

	private static EvaluationTaskSyncRequest CreateRequest(string documentId, string runId, string taskId, string logicalTaskKey)
	{
		return new EvaluationTaskSyncRequest
		{
			SchemaVersion = "fs-0002/v1",
			DocumentId = documentId,
			DocumentVersionFingerprint = "sha256:test",
			Producer = new EvaluationTaskProducerInfo
			{
				SourcePipeline = "test-pipeline",
				ServiceVersion = "1.0.0"
			},
			EvaluationRun = new EvaluationTaskRunInfo
			{
				EvaluationRunId = runId,
				StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
				CompletedAt = DateTimeOffset.UtcNow,
				CorrelationId = $"corr-{runId}"
			},
			Tasks = new List<PortableComplianceTask>
			{
				new()
				{
					TaskId = taskId,
					LogicalTaskKey = logicalTaskKey,
					Title = "Example task",
					Description = "Task description",
					Severity = "high",
					Status = ComplianceTaskStatuses.Open,
					Citation = new PortableTaskCitation
					{
						Text = "Example citation",
						ReferenceSource = "ISO 19650"
					},
					Anchor = new PortableTaskAnchor
					{
						AnchorKind = "textSearchFallback",
						Selector = "Example selector",
						Excerpt = "Example excerpt",
						Confidence = 0.8,
						LastValidatedAt = DateTimeOffset.UtcNow
					},
					Provenance = new PortableTaskProvenance
					{
						SourcePipeline = "test-pipeline",
						StandardId = "ISO19650",
						ClauseId = "5.1"
					}
				}
			}
		};
	}
}
