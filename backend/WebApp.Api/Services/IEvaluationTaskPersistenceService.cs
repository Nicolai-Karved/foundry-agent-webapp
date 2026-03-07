using WebApp.Api.Models;

namespace WebApp.Api.Services;

public interface IEvaluationTaskPersistenceService
{
	Task<TaskSyncReceiptResponse> IngestAsync(EvaluationTaskSyncRequest request, string correlationId, CancellationToken cancellationToken);
	Task<CanonicalTaskSnapshotResponse?> GetTaskSnapshotAsync(string documentId, string userId, bool includeSuperseded, CancellationToken cancellationToken);
	Task<ComplianceTaskListResponse> GetTasksAsync(string documentId, string userId, string correlationId, CancellationToken cancellationToken);
	Task<(ComplianceTask? UpdatedTask, bool NotFound, bool VersionConflict)> UpdateTaskStatusAsync(
		string taskId,
		UpdateComplianceTaskStatusRequest request,
		string userId,
		string correlationId,
		CancellationToken cancellationToken);
	Task<RerunVerificationResponse> RerunVerificationAsync(
		RerunVerificationRequest request,
		string userId,
		string correlationId,
		CancellationToken cancellationToken);
	Task<(ComplianceTaskCitationContextResponse? Response, bool NotFound)> GetCitationContextAsync(
		string taskId,
		string documentId,
		CancellationToken cancellationToken);
	Task<TaskOverlayUpdateResponse?> GetOverlayAsync(
		string taskId,
		string documentId,
		string userId,
		CancellationToken cancellationToken);
}
