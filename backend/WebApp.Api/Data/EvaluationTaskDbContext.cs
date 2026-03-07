using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApp.Api.Configuration;
using WebApp.Api.Data.Entities;

namespace WebApp.Api.Data;

public sealed class EvaluationTaskDbContext : DbContext
{
	private readonly string _schema;

	public EvaluationTaskDbContext(
		DbContextOptions<EvaluationTaskDbContext> options,
		IOptions<EvaluationTaskSyncOptions> syncOptions)
		: base(options)
	{
		_schema = syncOptions.Value.Persistence.PreferredSchema;
	}

	public DbSet<EvaluationRunEntity> EvaluationRuns => Set<EvaluationRunEntity>();
	public DbSet<ComplianceTaskRecordEntity> ComplianceTaskRecords => Set<ComplianceTaskRecordEntity>();
	public DbSet<TaskStateOverlayEntity> TaskStateOverlays => Set<TaskStateOverlayEntity>();
	public DbSet<TaskSyncReceiptEntity> TaskSyncReceipts => Set<TaskSyncReceiptEntity>();
	public DbSet<TaskActionAuditEntity> TaskActionAudits => Set<TaskActionAuditEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		if (!string.IsNullOrWhiteSpace(_schema))
		{
			modelBuilder.HasDefaultSchema(_schema);
		}

		modelBuilder.Entity<EvaluationRunEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.EvaluationRunId).HasMaxLength(128);
			entity.Property(x => x.DocumentId).HasMaxLength(256);
			entity.Property(x => x.DocumentVersionFingerprint).HasMaxLength(256);
			entity.Property(x => x.SourcePipeline).HasMaxLength(128);
			entity.Property(x => x.SchemaVersion).HasMaxLength(64);
			entity.Property(x => x.CorrelationId).HasMaxLength(128);
			entity.HasIndex(x => new { x.DocumentId, x.EvaluationRunId }).IsUnique();
			entity.HasIndex(x => new { x.DocumentId, x.CompletedAt });
		});

		modelBuilder.Entity<ComplianceTaskRecordEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.TaskId).HasMaxLength(128);
			entity.Property(x => x.LogicalTaskKey).HasMaxLength(512);
			entity.Property(x => x.DocumentId).HasMaxLength(256);
			entity.Property(x => x.Title).HasMaxLength(300);
			entity.Property(x => x.Description).HasMaxLength(4000);
			entity.Property(x => x.Severity).HasMaxLength(32);
			entity.Property(x => x.Status).HasMaxLength(32);
			entity.Property(x => x.CitationText).HasMaxLength(4000);
			entity.Property(x => x.ReferenceSource).HasMaxLength(512);
			entity.Property(x => x.CitationUri).HasMaxLength(2048);
			entity.Property(x => x.AnchorKind).HasMaxLength(64);
			entity.Property(x => x.AnchorSelector).HasMaxLength(4096);
			entity.Property(x => x.AnchorExcerpt).HasMaxLength(2000);
			entity.Property(x => x.ProvenanceSourcePipeline).HasMaxLength(128);
			entity.Property(x => x.StandardId).HasMaxLength(128);
			entity.Property(x => x.ClauseId).HasMaxLength(128);
			entity.Property(x => x.PolicyId).HasMaxLength(128);
			entity.HasIndex(x => x.TaskId).IsUnique();
			entity.HasIndex(x => new { x.DocumentId, x.LogicalTaskKey, x.IsSuperseded });
			entity.HasOne(x => x.EvaluationRun)
				.WithMany(x => x.Tasks)
				.HasForeignKey(x => x.EvaluationRunEntityId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<TaskStateOverlayEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.UserId).HasMaxLength(128);
			entity.Property(x => x.Status).HasMaxLength(32);
			entity.Property(x => x.ResolutionNote).HasMaxLength(2000);
			entity.Property(x => x.Source).HasMaxLength(64);
			entity.HasIndex(x => new { x.TaskRecordEntityId, x.UserId }).IsUnique();
			entity.HasOne(x => x.TaskRecord)
				.WithMany(x => x.StateOverlays)
				.HasForeignKey(x => x.TaskRecordEntityId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<TaskSyncReceiptEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.SyncReceiptId).HasMaxLength(128);
			entity.Property(x => x.DocumentId).HasMaxLength(256);
			entity.Property(x => x.EvaluationRunId).HasMaxLength(128);
			entity.Property(x => x.IngestHash).HasMaxLength(128);
			entity.Property(x => x.Result).HasMaxLength(32);
			entity.Property(x => x.CorrelationId).HasMaxLength(128);
			entity.HasIndex(x => x.SyncReceiptId).IsUnique();
			entity.HasIndex(x => new { x.DocumentId, x.EvaluationRunId, x.IngestHash });
		});

		modelBuilder.Entity<TaskActionAuditEntity>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.DocumentId).HasMaxLength(256);
			entity.Property(x => x.ActionType).HasMaxLength(64);
			entity.Property(x => x.PreviousValue).HasMaxLength(2000);
			entity.Property(x => x.NewValue).HasMaxLength(2000);
			entity.Property(x => x.UserId).HasMaxLength(128);
			entity.Property(x => x.CorrelationId).HasMaxLength(128);
			entity.HasIndex(x => new { x.DocumentId, x.Timestamp });
			entity.HasOne(x => x.TaskRecord)
				.WithMany()
				.HasForeignKey(x => x.TaskRecordEntityId)
				.OnDelete(DeleteBehavior.SetNull);
		});

		base.OnModelCreating(modelBuilder);
	}
}
