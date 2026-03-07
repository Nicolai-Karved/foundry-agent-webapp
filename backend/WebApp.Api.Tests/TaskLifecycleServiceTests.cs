using Microsoft.Extensions.Logging.Abstractions;
using WebApp.Api.Models;
using WebApp.Api.Services;
using Xunit;

namespace WebApp.Api.Tests;

public class TaskLifecycleServiceTests
{
	private readonly TaskLifecycleService _service = new(NullLogger<TaskLifecycleService>.Instance);

	[Fact]
	public async Task GetTasksAsync_ShouldSeedTasks_WhenDocumentNotKnown()
	{
		var response = await _service.GetTasksAsync("doc-test", "corr-1", CancellationToken.None);

		Assert.Equal("doc-test", response.DocumentId);
		Assert.Equal("corr-1", response.CorrelationId);
		Assert.NotEmpty(response.Tasks);
	}

	[Fact]
	public async Task UpdateTaskStatusAsync_ShouldReturnVersionConflict_WhenExpectedVersionMismatch()
	{
		var seeded = await _service.GetTasksAsync("doc-conflict", "corr-2", CancellationToken.None);
		var task = seeded.Tasks.First();

		var result = await _service.UpdateTaskStatusAsync(
			task.TaskId,
			new UpdateComplianceTaskStatusRequest
			{
				DocumentId = task.DocumentId,
				Status = ComplianceTaskStatuses.Done,
				ExpectedVersion = task.Version + 1
			},
			"user-1",
			"corr-2",
			CancellationToken.None);

		Assert.False(result.NotFound);
		Assert.True(result.VersionConflict);
		Assert.NotNull(result.UpdatedTask);
	}

	[Fact]
	public async Task UpdateTaskStatusAsync_ShouldUpdateStatusAndVersion_WhenExpectedVersionMatches()
	{
		var seeded = await _service.GetTasksAsync("doc-update", "corr-3", CancellationToken.None);
		var task = seeded.Tasks.First();

		var result = await _service.UpdateTaskStatusAsync(
			task.TaskId,
			new UpdateComplianceTaskStatusRequest
			{
				DocumentId = task.DocumentId,
				Status = ComplianceTaskStatuses.InReview,
				ExpectedVersion = task.Version
			},
			"user-2",
			"corr-3",
			CancellationToken.None);

		Assert.False(result.NotFound);
		Assert.False(result.VersionConflict);
		Assert.NotNull(result.UpdatedTask);
		Assert.Equal(ComplianceTaskStatuses.InReview, result.UpdatedTask!.Status);
		Assert.Equal(task.Version + 1, result.UpdatedTask.Version);
	}

	[Fact]
	public async Task GetCitationContextAsync_ShouldReturnNotFound_WhenTaskDoesNotExist()
	{
		var result = await _service.GetCitationContextAsync("missing-task", "doc-missing", CancellationToken.None);
		Assert.True(result.NotFound);
		Assert.Null(result.Response);
	}
}
