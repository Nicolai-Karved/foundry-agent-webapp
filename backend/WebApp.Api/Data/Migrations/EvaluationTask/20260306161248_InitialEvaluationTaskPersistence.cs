using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebApp.Api.Data.Migrations.EvaluationTask
{
    /// <inheritdoc />
    public partial class InitialEvaluationTaskPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fs0002");

            migrationBuilder.CreateTable(
                name: "EvaluationRuns",
                schema: "fs0002",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvaluationRunId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DocumentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DocumentVersionFingerprint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourcePipeline = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskSyncReceipts",
                schema: "fs0002",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncReceiptId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DocumentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EvaluationRunId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IngestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Deduplicated = table.Column<bool>(type: "boolean", nullable: false),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskSyncReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceTaskRecords",
                schema: "fs0002",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LogicalTaskKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DocumentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EvaluationRunEntityId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CitationText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReferenceSource = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CitationUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AnchorKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AnchorSelector = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    AnchorExcerpt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AnchorConfidence = table.Column<double>(type: "double precision", nullable: false),
                    AnchorLastValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnchorExtensionsJson = table.Column<string>(type: "text", nullable: true),
                    ProvenanceSourcePipeline = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StandardId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClauseId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PolicyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TaskExtensionsJson = table.Column<string>(type: "text", nullable: true),
                    IsSuperseded = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceTaskRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceTaskRecords_EvaluationRuns_EvaluationRunEntityId",
                        column: x => x.EvaluationRunEntityId,
                        principalSchema: "fs0002",
                        principalTable: "EvaluationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskActionAudits",
                schema: "fs0002",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRecordEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskActionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskActionAudits_ComplianceTaskRecords_TaskRecordEntityId",
                        column: x => x.TaskRecordEntityId,
                        principalSchema: "fs0002",
                        principalTable: "ComplianceTaskRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TaskStateOverlays",
                schema: "fs0002",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRecordEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStateOverlays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskStateOverlays_ComplianceTaskRecords_TaskRecordEntityId",
                        column: x => x.TaskRecordEntityId,
                        principalSchema: "fs0002",
                        principalTable: "ComplianceTaskRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceTaskRecords_DocumentId_LogicalTaskKey_IsSuperseded",
                schema: "fs0002",
                table: "ComplianceTaskRecords",
                columns: new[] { "DocumentId", "LogicalTaskKey", "IsSuperseded" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceTaskRecords_EvaluationRunEntityId",
                schema: "fs0002",
                table: "ComplianceTaskRecords",
                column: "EvaluationRunEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceTaskRecords_TaskId",
                schema: "fs0002",
                table: "ComplianceTaskRecords",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_DocumentId_CompletedAt",
                schema: "fs0002",
                table: "EvaluationRuns",
                columns: new[] { "DocumentId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRuns_DocumentId_EvaluationRunId",
                schema: "fs0002",
                table: "EvaluationRuns",
                columns: new[] { "DocumentId", "EvaluationRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskActionAudits_DocumentId_Timestamp",
                schema: "fs0002",
                table: "TaskActionAudits",
                columns: new[] { "DocumentId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskActionAudits_TaskRecordEntityId",
                schema: "fs0002",
                table: "TaskActionAudits",
                column: "TaskRecordEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStateOverlays_TaskRecordEntityId_UserId",
                schema: "fs0002",
                table: "TaskStateOverlays",
                columns: new[] { "TaskRecordEntityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskSyncReceipts_DocumentId_EvaluationRunId_IngestHash",
                schema: "fs0002",
                table: "TaskSyncReceipts",
                columns: new[] { "DocumentId", "EvaluationRunId", "IngestHash" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskSyncReceipts_SyncReceiptId",
                schema: "fs0002",
                table: "TaskSyncReceipts",
                column: "SyncReceiptId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskActionAudits",
                schema: "fs0002");

            migrationBuilder.DropTable(
                name: "TaskStateOverlays",
                schema: "fs0002");

            migrationBuilder.DropTable(
                name: "TaskSyncReceipts",
                schema: "fs0002");

            migrationBuilder.DropTable(
                name: "ComplianceTaskRecords",
                schema: "fs0002");

            migrationBuilder.DropTable(
                name: "EvaluationRuns",
                schema: "fs0002");
        }
    }
}
