# Feature Specification: Evaluation Task Sync and Portable Task Schema

- **Spec Package ID**: `FS-0002-evaluation-task-sync-and-portable-task-schema`
- **Feature ID**: `evaluation-task-sync-and-portable-task-schema`
- **Date**: 2026-03-06
- **Status**: Draft (Blocked for implementation until ADR gate is satisfied)
- **Related specs**:
  - `docs/specs/FS-0001-word-compliance-agent-tasks/FeatureSpec.md`
- **Primary surfaces**: Backend evaluation/orchestrator pipeline, task persistence layer, existing task APIs, current PDF viewer, future BlockSuite-based editor
- **Preferred persistence target**: PostgreSQL, extending `pg-naviate-agent-gateway` if compatibility and governance checks pass

## 0) ADR traceability and implementation gate

- **Referenced ADRs**:
  - `docs/architecture/decisions/ADR-0001-word-addin-task-anchoring-and-state.md`
  - `docs/architecture/decisions/ADR-0002-evaluation-task-sync-persistence-and-portable-schema.md`
- **Implementation gate**:
  - Implementation MUST NOT start until `ADR-0002` is `Accepted` or an explicit documented exception is approved.
- **Contract review gate**:
  - API-affecting implementation MUST NOT start until draft sync-ingest OpenAPI and portable-task JSON Schema artifacts have been reviewed for compatibility and security.

## 1) Outcome and scope

Introduce an additive capability that converts existing backend evaluation/orchestrator output into a durable, versioned, portable task representation so the same compliance tasks can be:
1. Persisted across sessions and reruns.
2. Served through the existing FS-0001 task APIs without breaking current consumers.
3. Reused by the current PDF viewer and a future BlockSuite-driven editor.
4. Audited, reconciled, and versioned independently from any one client surface.

### Non-goals (v1)
- Replacing the current PDF viewer with BlockSuite.
- Introducing real-time collaborative editing semantics.
- Requiring a prompt redesign when existing evaluation output can be mapped into the portable schema.
- Storing full raw document bodies in the task persistence store by default.
- Removing or breaking the existing FS-0001 Word add-in contracts.

## 2) Users and primary flows

### User roles
- Backend evaluation/orchestrator service
- Document author
- Reviewer / compliance analyst
- Client integrator for alternate renderers (current PDF viewer, future BlockSuite editor)

### Primary flows
1. A document evaluation completes in the existing backend/orchestrator flow.
2. The task-sync component validates the evaluation payload against the portable schema contract.
3. The sync component persists evaluation-run metadata, tasks, anchors, and audit records to PostgreSQL.
4. Existing and future clients request the current task snapshot for a document and receive the same canonical task representation.
5. A user updates task status or resolution metadata through an existing or compatible API.
6. A later evaluation rerun reconciles regenerated tasks with the persisted task set while preserving compatible user-managed state.

## 3) Functional requirements

## MUST requirements

### Sync and persistence
- **FR-MUST-001**: The feature must introduce an explicit sync contract from existing backend evaluation/orchestrator output into durable task persistence.
- **FR-MUST-002**: The sync contract must be versioned and validated before persistence.
- **FR-MUST-003**: The persistence layer must use PostgreSQL and should extend `pg-naviate-agent-gateway` when tenancy, connectivity, lifecycle, and governance requirements are satisfied.
- **FR-MUST-004**: If `pg-naviate-agent-gateway` cannot be extended safely, the implementation must preserve the same logical contract and record the alternative persistence decision through an ADR update before implementation proceeds.
- **FR-MUST-005**: Sync processing must be idempotent for repeated submissions of the same evaluation run for the same document.
- **FR-MUST-006**: Persisted task records must include stable task identity, document identity, evaluation-run identity, schema version, status, severity, citation/reference data, anchor data, provenance, and timestamps.
- **FR-MUST-007**: The persistence model must retain user-managed task state separately enough to preserve compatible status and reviewer actions across reruns.
- **FR-MUST-008**: Rerun reconciliation must mark superseded or obsolete generated tasks explicitly instead of silently hard-deleting active history.
- **FR-MUST-018**: If `pg-naviate-agent-gateway` is reused, FS-0002 persistence must not break existing Revit or Web Agent MCP server-registration workloads already using that database.
- **FR-MUST-019**: Shared-database reuse must isolate FS-0002 objects through a dedicated schema, dedicated role(s), and migration boundaries that do not modify or drop existing MCP registration objects — including the currently evidenced `UserMcpServers`, `UserSecrets`, and `DataProtectionKeys` tables used by `NaviateGateway` — unless a separately approved migration plan explicitly covers them.
- **FR-MUST-020**: Before implementation begins against `pg-naviate-agent-gateway`, the team must inventory existing MCP registration tables, schemas, roles, and critical queries used by Revit/Web Agent flows and verify that FS-0002 changes are non-breaking against that catalog. The current minimum protected catalog includes `UserMcpServers`, `UserSecrets`, `DataProtectionKeys`, and the `ShareWithRevit` sharing behavior evidenced in the existing gateway code.

### Client and contract portability
- **FR-MUST-009**: Existing FS-0001 task retrieval, status update, citation-context, and rerun APIs must remain supported, with persistence introduced behind compatible contracts.
- **FR-MUST-010**: A canonical portable task JSON schema must be editor-neutral and must not require Word-specific, PDF-specific, or BlockSuite-specific fields for core task meaning.
- **FR-MUST-011**: The portable schema must support current PDF-viewer use and future BlockSuite-editor use through optional surface-specific extension objects or mappings layered on top of canonical task data.
- **FR-MUST-012**: The canonical schema must include portable anchor/reference structures that can represent source excerpts, document locations, and optional editor-specific bindings without changing the core schema identity.
- **FR-MUST-013**: The feature must define contract artifacts for sync ingest, task snapshot read, task status overlay update, and rerun reconciliation behavior.
- **FR-MUST-014**: Unsupported schema versions or malformed sync payloads must be rejected with actionable validation errors and without partial silent persistence.

### Traceability and auditability
- **FR-MUST-015**: Every persisted task must trace back to its originating evaluation run and source document.
- **FR-MUST-016**: Sync attempts, deduplicated submissions, reconciliation outcomes, and task-status changes must be auditable.
- **FR-MUST-017**: The system must support backward-compatible rollout from the current in-memory FS-0001 implementation to durable persistence without breaking existing consumers.

## SHOULD requirements
- **FR-SHOULD-001**: The feature should expose a stable logical task key that enables cross-run matching even when task IDs are regenerated.
- **FR-SHOULD-002**: The feature should provide a task projection shape optimized for list UIs while preserving access to the full canonical record.
- **FR-SHOULD-003**: The feature should support explicit task lifecycle markers such as `superseded`, `reopened`, or `carried_forward` in addition to user-facing resolution state when useful for diagnostics.
- **FR-SHOULD-004**: The feature should support batch sync and retrieval for multi-document evaluation scenarios without changing the per-document canonical schema.

## MAY requirements
- **FR-MAY-001**: Publish task-sync events for downstream analytics or notifications.
- **FR-MAY-002**: Support export adapters that transform the canonical task schema into editor-specific import packages.

## 4) Security, privacy, and governance requirements

These requirements align with repository governance references (`security-assessment-ref`) and the same governance family used by FS-0001: Secure Development Policy, Code Review Procedure, Development Vulnerability Management Procedure, Access Control Policy, Cryptography Policy, Test and Development Data Handling Procedure, System Policy, Threat Modelling.

### MUST security/privacy requirements
- **SEC-MUST-001**: Sync ingestion must run behind authenticated backend trust boundaries; no anonymous task-sync ingestion is allowed.
- **SEC-MUST-002**: Tenant, user, and document scoping must be enforced for task reads and writes.
- **SEC-MUST-003**: Persist only the minimum task evidence needed for remediation and audit; full raw document content must not be stored by default.
- **SEC-MUST-004**: Apply PII redaction before persistence where configured by environment or policy.
- **SEC-MUST-005**: All sync, read, and update traffic must use HTTPS/TLS.
- **SEC-MUST-006**: Persisted task, audit, and sync metadata must follow the minimum 30-day retention requirement already established for prompt/response and task-action audits.
- **SEC-MUST-007**: Threat model artifacts must be updated to cover durable persistence, replay/idempotency, rerun reconciliation, and multi-client consumption risks before release.
- **SEC-MUST-008**: CI must include dependency, migration, and vulnerability checks for all new persistence-related packages and services.
- **SEC-MUST-009**: Sync endpoints must enforce payload size limits and schema validation before database writes.
- **SEC-MUST-010**: Database access must use least privilege and managed identity or approved secret management patterns; secrets must not be embedded in client surfaces.

### Authorization model (normative)
- **AUTH-MUST-001**: Sync ingestion must authenticate with a backend managed identity or approved service principal/workload identity; end-user delegated tokens must not be used for sync ingestion.
- **AUTH-MUST-002**: User-facing task APIs must continue to use Microsoft 365 user identity (SSO/OAuth) and preserve the current FS-0001 authorization baseline until a narrower approved scope model replaces it.
- **AUTH-MUST-003**: Citation-context and rerun paths must enforce the same tenant + document authorization boundary as task-read and task-update operations.
- **AUTH-MUST-004**: Current and future renderer clients must access task data only through approved backend APIs and must not receive direct database credentials.

### SHOULD security requirements
- **SEC-SHOULD-001**: Correlation IDs should connect evaluation run, sync attempt, persistence write, and user-visible task actions end to end.
- **SEC-SHOULD-002**: Immutable or append-only audit semantics should be used for sync receipts and task-state transitions where practical.
- **SEC-SHOULD-003**: Data at rest should rely on approved platform encryption and backup settings for the chosen PostgreSQL target.

### Security control traceability matrix

| Control ID | Control summary | Governance reference |
|---|---|---|
| SEC-MUST-001 | Authenticated backend-only sync boundary | Access Control Policy |
| SEC-MUST-002 | Tenant/document authorization | Access Control Policy |
| SEC-MUST-003 | Data minimization in persistence | Test and Development Data Handling Procedure |
| SEC-MUST-004 | PII redaction before persistence | Test and Development Data Handling Procedure |
| SEC-MUST-005 | HTTPS/TLS for all traffic | Cryptography Policy |
| SEC-MUST-006 | Minimum 30-day audit retention | System Policy |
| SEC-MUST-007 | Threat model update required | Threat Modelling |
| SEC-MUST-008 | CI vulnerability and migration scanning | Development Vulnerability Management Procedure |
| SEC-MUST-009 | Payload validation and size limits | Secure Development Policy |
| SEC-MUST-010 | Least-privilege database access | Access Control Policy |
| SEC-SHOULD-001 | End-to-end correlation IDs | Code Review Procedure |
| SEC-SHOULD-002 | Strong audit semantics | Code Review Procedure |
| SEC-SHOULD-003 | Encryption and backup posture | Cryptography Policy |

## 5) Observability and diagnostics

- **OBS-MUST-001**: Capture sync request counts, success/failure counts, and deduplicated replay counts.
- **OBS-MUST-002**: Capture validation-failure reasons for rejected sync payloads without logging prohibited raw content.
- **OBS-MUST-003**: Capture task reconciliation outcomes, including carried-forward, superseded, reopened, and newly-created counts.
- **OBS-MUST-004**: Capture API latency and failure rates for task snapshot and status update paths after persistence is introduced.
- **OBS-MUST-005**: Propagate correlation IDs across evaluator, sync, persistence, and client-serving layers.

### Operational thresholds (v1)

- **NFR-OBS-001**: Sync ingest p95 latency ≤ 5s for normal single-document evaluation payloads.
- **NFR-OBS-002**: Canonical task snapshot read p95 latency ≤ 2.5s under normal internal usage.
- **NFR-OBS-003**: Sync deduplication false-positive rate = 0 for identical run replay detection within the same document scope.
- **NFR-OBS-004**: Sync success rate ≥ 99.0% per rolling 24h excluding invalid client payloads.
- **NFR-OBS-005**: Audit receipt write success rate ≥ 99.9% per rolling 24h.

## 6) Data and integration requirements

### Data entities (minimum)
- **EvaluationRun**: `evaluationRunId`, `documentId`, `documentVersionFingerprint`, `sourcePipeline`, `schemaVersion`, `startedAt`, `completedAt`, `correlationId`.
- **ComplianceTaskRecord**: `taskRecordId`, `logicalTaskKey`, `documentId`, `evaluationRunId`, `title`, `description`, `severity`, `status`, `citation`, `referenceSource`, `provenance`, `createdAt`, `updatedAt`.
- **PortableAnchor**: `anchorKind`, `selector`, `excerpt`, `confidence`, `lastValidatedAt`, `extensions`.
- **TaskStateOverlay**: `taskRecordId`, `userId`, `status`, `resolutionNote`, `updatedAt`, `source`.
- **TaskSyncReceipt**: `syncReceiptId`, `documentId`, `evaluationRunId`, `ingestHash`, `deduplicated`, `result`, `timestamp`, `correlationId`.
- **TaskActionAudit**: `actionId`, `taskRecordId`, `actionType`, `previousValue`, `newValue`, `userId`, `timestamp`, `correlationId`.

### Field-level privacy and retention handling
- **DATA-MUST-001**: `description`, `citation`, and `PortableAnchor.excerpt` must contain remediation-relevant snippets only and must respect approved maximum lengths.
- **DATA-MUST-002**: `TaskStateOverlay.resolutionNote`, `TaskActionAudit.previousValue`, and `TaskActionAudit.newValue` must be truncated or redacted before persistence when they may contain user-entered or echoed free text.
- **DATA-MUST-003**: `userId` values stored in audit or export contexts must use approved internal identifiers and must not expose unnecessary profile attributes.
- **DATA-MUST-004**: Canonical task records may persist for the governed document lifecycle, but sync receipts and task-action audits must be purged or archived after the approved retention window, with 30 days as the minimum floor and 180 days as the default ceiling unless legal hold or approved exception applies.
- **DATA-MUST-005**: Reviewer notes and renderer-specific extensions must be excluded from broad client projections unless the caller is authorized to view them.

### Integration constraints
- Existing backend evaluation/orchestrator output is the source for generated tasks.
- Existing FS-0001 endpoints remain externally compatible and may be reimplemented over durable persistence.
- The portable task schema must be reusable by both the current PDF viewer and a future BlockSuite editor.
- Persistence should extend `pg-naviate-agent-gateway` when feasible, but the logical task contract must not be coupled to a single infrastructure assumption.
- Existing Revit and Web Agent MCP server-registration data, if hosted in `pg-naviate-agent-gateway`, must remain operational and isolated from FS-0002 schema evolution.
- Existing Revit and Web Agent MCP server-registration data in the current gateway implementation is backed by PostgreSQL tables `UserMcpServers` and `UserSecrets`, plus `DataProtectionKeys` for protected secret handling; these must remain operational and isolated from FS-0002 schema evolution.

## 7) Compatibility and rollout

- **ROL-MUST-001**: Rollout must be additive and backward compatible for current FS-0001 consumers.
- **ROL-MUST-002**: The persistence-backed implementation must support a phased migration from the in-memory task lifecycle service.
- **ROL-MUST-003**: Schema versioning must allow old consumers to keep functioning while newer clients adopt portable-schema extensions.
- **ROL-MUST-004**: The rollout plan must include a safe fallback path to the current behavior if persistence sync fails during staged adoption.
- **ROL-MUST-005**: Production task persistence must be segregated from lower environments, and the approved PostgreSQL target must document networking, backup/restore, access-role boundaries, and environment ownership before go-live.
- **ROL-MUST-006**: Reuse of `pg-naviate-agent-gateway` requires an explicit suitability checklist covering tenancy, lifecycle ownership, capacity, backup posture, and governance approval.
- **ROL-MUST-007**: The suitability checklist for `pg-naviate-agent-gateway` must include compatibility with existing Revit/Web Agent MCP registration workloads, including schema isolation, role isolation, migration rollback, and regression verification of the current `UserMcpServers` / `UserSecrets` / `DataProtectionKeys` paths and `ShareWithRevit` behavior.

## 8) Acceptance criteria (Given/When/Then)

### AC-001: Evaluation output sync persists canonical tasks
- **Given** an evaluation run completes through the existing backend/orchestrator flow
- **When** the sync contract is invoked with a valid portable task payload
- **Then** the evaluation run, sync receipt, and canonical task records are persisted successfully and become available through compatible task-read APIs.

### AC-002: Replay is idempotent
- **Given** a previously processed evaluation run for the same document
- **When** the identical sync payload is submitted again
- **Then** the system records a deduplicated sync receipt and does not create duplicate active task records.

### AC-003: Rerun preserves compatible user state
- **Given** a reviewer has updated task statuses for a document
- **When** a later evaluation rerun produces matching or equivalent tasks
- **Then** compatible user-managed state is carried forward according to documented reconciliation rules.

### AC-004: Legacy task APIs remain usable
- **Given** an FS-0001-compatible client requests tasks or updates task status
- **When** the persistence-backed implementation is active
- **Then** the client continues to function without a breaking contract change.

### AC-005: Portable schema supports multiple renderers
- **Given** the canonical task payload for a document
- **When** the current PDF viewer and a future BlockSuite editor or approved compatibility harness consume the payload
- **Then** both can resolve the same task meaning from canonical fields while using optional surface-specific extensions only where needed.

### AC-006: Invalid payloads fail safely
- **Given** a sync payload is malformed or uses an unsupported schema version
- **When** the sync endpoint validates the payload
- **Then** the request fails with actionable errors and no partial task persistence occurs.

### AC-007: Audit and telemetry are preserved
- **Given** sync, reconciliation, and user task actions occur
- **When** operators inspect logs and audit records
- **Then** correlation IDs, sync receipts, and task-action audit data allow end-to-end tracing.

### AC-010: Shared database reuse does not break MCP registration
- **Given** `pg-naviate-agent-gateway` is already used by Revit and Web Agent MCP server-registration flows
- **And given** those flows are backed by `UserMcpServers`, `UserSecrets`, and `DataProtectionKeys`
- **When** FS-0002 persistence is introduced into that same database
- **Then** existing registration reads/writes continue to succeed unchanged, and FS-0002 objects remain isolated to approved schema and role boundaries.

### AC-008: Citation-context compatibility is preserved
- **Given** an FS-0001-compatible client requests citation/reference context for a persisted task
- **When** the persistence-backed implementation is active
- **Then** the client receives a compatible citation-context response without requiring a breaking contract change.

### AC-009: Rerun compatibility is preserved
- **Given** an FS-0001-compatible client invokes manual rerun for a document with persisted tasks
- **When** the persistence-backed implementation is active
- **Then** the rerun completes through compatible request/response semantics and returns refreshed task state without a breaking contract change.

## 9) Required contract artifacts

The following artifacts are required before implementation completion:
1. OpenAPI (or equivalent API contract) for:
   - sync ingest endpoint for evaluation-task persistence,
   - canonical task snapshot read endpoint or compatible extension to existing task-read APIs,
   - task status/state overlay update endpoint,
   - rerun reconciliation response semantics.
2. JSON Schema for the portable canonical task payload, including extension points for current PDF viewer and future BlockSuite editor.
3. Error model and retry/idempotency guidance for sync producers and client consumers.
4. Persistence model documentation covering task identity, evaluation-run linkage, and reconciliation rules.
5. Draft security review notes covering authorization model, field-level privacy handling, and PostgreSQL target suitability before API-affecting implementation begins.
6. Shared-database compatibility notes documenting existing MCP registration dependencies, the non-breaking isolation strategy, and rollback checks if `pg-naviate-agent-gateway` is reused.

## 10) Test strategy

### Unit tests
- Schema validation and version negotiation.
- Logical task-key matching and rerun reconciliation rules.
- Idempotency and deduplication behavior.
- Portable anchor serialization/deserialization.

### Integration tests
- Evaluation output → sync ingest → PostgreSQL persistence.
- Existing FS-0001-compatible task-read and task-update APIs over persistent storage.
- Existing FS-0001-compatible citation-context API over persistent storage.
- Existing FS-0001-compatible rerun API over persistent storage.
- Shared-database regression coverage proving existing Revit/Web Agent MCP registration paths remain unaffected when FS-0002 objects exist in the same PostgreSQL server.
- Replay submission handling and dedup receipt creation.
- Migration/fallback behavior from in-memory service to persistent storage.

### End-to-end tests
- Evaluate a document, persist tasks, load tasks in an existing client, update status, rerun evaluation, and verify carried-forward state.
- Retrieve citation context and rerun tasks from an FS-0001-compatible client against the persistence-backed implementation.
- Render the same canonical task payload in the current PDF viewer and a compatibility harness representing future BlockSuite consumption.

### Security tests
- Authorization and trust-boundary enforcement for sync ingestion.
- Data minimization and PII redaction verification.
- Audit retention and privileged database-access checks.
- Input-size and schema-validation rejection behavior.

## 11) Open decisions / follow-ups

1. Confirm whether `pg-naviate-agent-gateway` can host the new schema and workload without violating tenancy, lifecycle, or governance expectations.
2. Finalize the stable logical task-key strategy for cross-run matching.
3. Decide whether reconciliation status markers such as `superseded` remain internal-only or become part of externally visible diagnostics.
4. Define the exact extension namespace convention for editor-specific schema enrichments (for example `extensions.pdf`, `extensions.word`, `extensions.blocksuite`).
5. Confirm whether `pg-naviate-agent-gateway` currently hosts Revit/Web Agent MCP registration tables and capture the exact non-breaking coexistence plan if reuse proceeds.
6. Preserve the existing `ShareWithRevit` contract semantics if the same PostgreSQL server is reused.

## 12) Assumptions recorded for this draft

1. The existing backend evaluation/orchestrator output is sufficiently structured to be mapped into the portable task schema without a prompt redesign.
2. FS-0001 remains the current consumer contract baseline and must not be broken by FS-0002.
3. BlockSuite compatibility is a portability target for schema design, not a requirement to adopt BlockSuite as the system of record.

## 13) Source references used

- `docs/specs/FS-0001-word-compliance-agent-tasks/FeatureSpec.md`
- `docs/contracts/word-compliance/task-output.schema.json`
- `docs/agent-prompts/document-compliance-checker.md`
- `docs/agent-prompts/standard-compliance-checker.md`
- `docs/artifacts/azure-resource-inventory-2026-02-23_180442.json`
- `docs/security-reports/2026-03-06-foundry-agent-webapp.md`
- `docs/specs/FS-0002-evaluation-task-sync-and-portable-task-schema/DbCoexistenceChecklist.md`
- `c:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Program.cs`
- `c:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Services\UserMcpServerStore.cs`
- `c:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\GatewayDbContext.cs`
- `c:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Migrations\20260108125511_InitialGatewayDb.cs`
- `c:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Migrations\20260108160658_AddUserSecrets.cs`
- `c:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Migrations\20260109170315_AddShareWithRevitToUserMcpServers.cs`
