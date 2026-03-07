# FS-0002 Implementation Plan

## Goal
Implement durable evaluation-task sync and a portable task schema behind backward-compatible FS-0001 APIs without breaking the existing `pg-naviate-agent-gateway` MCP registration usage shared with `naviate-revit-ai-agent`.

## Entry conditions and implementation guardrails

- `ADR-0002` is currently marked **Accepted** and is treated as the architecture baseline for this plan.
- API-affecting implementation remains gated by the draft sync-ingest OpenAPI and portable-task JSON Schema review required by `FeatureSpec.md`.
- Shared database reuse is **not** assumed safe by default; implementation must first prove non-interference with the existing gateway compatibility surface: `UserMcpServers`, `UserSecrets`, `DataProtectionKeys`, and `ShareWithRevit` behavior.
- If `pg-naviate-agent-gateway` fails suitability checks, the plan must preserve the logical contract while switching the physical PostgreSQL target via ADR/spec update before code relying on that target ships.

## Ordered steps

### Step 1 — Baseline the current persistence and compatibility surface
- **Scope**: `docs/specs/FS-0002-evaluation-task-sync-and-portable-task-schema/DbCoexistenceChecklist.md`, `c:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Program.cs`, `...\GatewayDbContext.cs`, `...\Data\Migrations\*.cs`, any new findings notes under the FS-0002 package.
- **Expected outputs**:
  - Completed shared-database inventory covering current gateway tables, roles, migration chain, and `ShareWithRevit` semantics.
  - Decision record stating whether `pg-naviate-agent-gateway` can be reused safely or whether a separate PostgreSQL target is required.
- **Tests to add/update and verify**:
  - No production code tests yet; verification is evidence-based review.
  - Verify the inventory matches the current gateway code and migration history.
- **Contracts/docs to update**:
  - Update `DbCoexistenceChecklist.md` with the completed findings.
  - If the target DB changes, update `FeatureSpec.md`, `FeatureSpec.json`, and ADR/spec references accordingly.
- **Security/compliance controls and checkpoints**:
  - Satisfy `FR-MUST-018`..`FR-MUST-020`, `ROL-MUST-006`, `ROL-MUST-007`.
  - Confirm least-privilege, schema isolation, rollback isolation, and ownership boundaries before any shared-server implementation starts.
- **Agent/skill context**: `agentic-workflows`, `plan-writing`, `SecurityAssessment` governance expectations.

### Step 2 — Finalize canonical contracts before API-affecting code
- **Scope**: new contract artifacts under `docs/contracts/` for FS-0002; possible updates to `docs/specs/FS-0002-evaluation-task-sync-and-portable-task-schema/FeatureSpec.md` and `FeatureSpec.json` if review feedback changes details.
- **Expected outputs**:
  - Draft OpenAPI for sync ingest, canonical task snapshot read, task state overlay update, and rerun reconciliation.
  - Portable canonical task JSON Schema with extension points for current PDF viewer and future BlockSuite editor.
  - Retry/error/idempotency guidance.
- **Tests to add/update and verify**:
  - Schema validation fixtures for valid and invalid payloads.
  - Contract lint/validation checks for OpenAPI and JSON Schema.
- **Contracts/docs to update**:
  - Create/update the FS-0002 contract set.
  - Update `Plan.md` or spec references only if review introduces materially new scope.
- **Security/compliance controls and checkpoints**:
  - Satisfy the contract review gate.
  - Verify payload-size limits, version negotiation, auth boundary expectations, and portable extension namespace rules are encoded in the contracts.
- **Agent/skill context**: `api-patterns`, `repo-docs`, `SpecVerifier` compliance expectations.

### Step 3 — Introduce isolated persistence model and migration strategy
- **Scope**: backend persistence layer in `backend/WebApp.Api` (or a dedicated persistence project if introduced), EF/Npgsql model and migrations, configuration for PostgreSQL connection and schema isolation.
- **Expected outputs**:
  - Persistence model for `EvaluationRun`, canonical task records, state overlays, sync receipts, and audits.
  - Migration strategy that uses a dedicated FS-0002 schema/role boundary and avoids modifying existing gateway MCP tables.
  - Configuration wiring for the approved PostgreSQL target.
- **Tests to add/update and verify**:
  - Migration generation sanity checks.
  - Integration tests against a PostgreSQL-backed test database proving isolated schema creation.
  - Regression assertions that existing gateway tables are not touched by FS-0002 migrations.
- **Contracts/docs to update**:
  - Persistence model documentation.
  - Rollout notes if the target DB differs from the preferred target.
- **Security/compliance controls and checkpoints**:
  - Satisfy `SEC-MUST-009`, `SEC-MUST-010`, `DATA-MUST-001`..`DATA-MUST-005`.
  - Verify no direct client credentials, least-privilege DB access, and bounded retention model.
- **Agent/skill context**: `backend-dotnet`, `agentic-workflows`.

### Step 4 — Build sync ingest pipeline and idempotent receipt handling
- **Scope**: backend ingest endpoint(s), validation layer, sync application service, hashing/idempotency logic, audit receipt persistence, telemetry/logging.
- **Expected outputs**:
  - Authenticated sync ingest endpoint wired to the canonical schema.
  - Idempotent sync processing keyed by document/evaluation-run/payload hash.
  - Sync receipt auditing and rejection/error paths.
- **Tests to add/update and verify**:
  - Unit tests for version validation, duplicate detection, and malformed payload rejection.
  - Integration tests for evaluation output → sync ingest → PostgreSQL persistence.
  - Security tests for trust-boundary enforcement and payload validation failures.
- **Contracts/docs to update**:
  - OpenAPI examples and error guidance.
  - Observability/runbook notes for sync failures and deduplication.
- **Security/compliance controls and checkpoints**:
  - Satisfy `FR-MUST-001`, `FR-MUST-002`, `FR-MUST-005`, `FR-MUST-014`, `SEC-MUST-001`..`SEC-MUST-005`, `OBS-MUST-001`..`OBS-MUST-003`.
  - Confirm no partial persistence on validation failure.
- **Agent/skill context**: `backend-dotnet`, `test-driven-development`.

### Step 5 — Implement reconciliation and user state overlay preservation
- **Scope**: backend reconciliation service, logical task-key matching, supersede/carry-forward handling, user state overlay read/write model.
- **Expected outputs**:
  - Matching/reconciliation logic between generated task facts and user-managed state.
  - Explicit superseded/carry-forward handling.
  - State overlay persistence separated from generated task facts.
- **Tests to add/update and verify**:
  - Unit tests for logical task-key matching and collision handling.
  - Unit/integration tests for carry-forward of status and reviewer notes across reruns.
  - Regression tests for obsolete/superseded records not being silently deleted.
- **Contracts/docs to update**:
  - Reconciliation rule documentation.
  - State model documentation and any contract clarifications for overlay semantics.
- **Security/compliance controls and checkpoints**:
  - Satisfy `FR-MUST-006`..`FR-MUST-008`, `FR-MUST-015`, `FR-MUST-016`, `DATA-MUST-002`, `DATA-MUST-004`.
  - Ensure audit trail completeness and redaction of user-entered free text where required.
- **Agent/skill context**: `backend-dotnet`, `agentic-workflows`, `test-driven-development`.

### Step 6 — Move FS-0001-compatible APIs onto durable persistence
- **Scope**: `backend/WebApp.Api/Program.cs`, supporting services/models, existing FS-0001 task lifecycle façade, existing test projects.
- **Expected outputs**:
  - `GET /api/tasks`, `PATCH /api/tasks/{taskId}/status`, `GET /api/tasks/{taskId}/citation-context`, and `POST /api/verification/rerun` backed by the persistent model.
  - Backward-compatible request/response shapes preserved for existing consumers.
- **Tests to add/update and verify**:
  - Integration tests for all four FS-0001-compatible endpoints over PostgreSQL-backed persistence.
  - Contract tests for citation-context and rerun compatibility.
  - Regression tests ensuring current client expectations still pass.
- **Contracts/docs to update**:
  - Update backend/API docs and OpenAPI compatibility notes.
  - If response semantics change internally, document that the external contract remains stable.
- **Security/compliance controls and checkpoints**:
  - Satisfy `FR-MUST-009`, `AC-004`, `AC-008`, `AC-009`, `AUTH-MUST-002`, `AUTH-MUST-003`, `OBS-MUST-004`, `OBS-MUST-005`.
  - Preserve current authorization baseline and correlation propagation.
- **Agent/skill context**: `backend-dotnet`, `verification-before-completion`.

### Step 7 — Validate portability for current PDF viewer and future BlockSuite consumption
- **Scope**: adapter/projection layer, current PDF-viewer integration surface, compatibility harness for future BlockSuite consumption, contract examples.
- **Expected outputs**:
  - Canonical task projection consumable by current PDF flows.
  - Compatibility harness or adapter examples for future BlockSuite-based editor consumption.
  - Extension namespace guidance implemented in schema/examples.
- **Tests to add/update and verify**:
  - Projection tests proving canonical payload stability.
  - End-to-end/compatibility-harness tests for current PDF viewer consumption and BlockSuite-style extension use.
- **Contracts/docs to update**:
  - Update contract examples and portability notes.
  - Document extension namespace conventions and renderer-specific mapping rules.
- **Security/compliance controls and checkpoints**:
  - Satisfy `FR-MUST-010`..`FR-MUST-012`, `AC-005`.
  - Ensure renderer clients continue using backend APIs only (`AUTH-MUST-004`).
- **Agent/skill context**: `frontend-dev-guidelines`, `repo-docs`.

### Step 8 — Complete security, observability, rollout, and shared-database verification
- **Scope**: threat model, CI workflow updates, observability docs, rollout/runbook docs, shared-database compatibility evidence, final verification artifacts.
- **Expected outputs**:
  - FS-0002 threat model artifact.
  - Updated CI checks for persistence dependencies/migrations.
  - Rollout and rollback checklist with shared DB coexistence evidence.
  - Final evidence pack showing whether the shared PostgreSQL target is approved or rejected.
- **Tests to add/update and verify**:
  - Security tests for auth boundary, redaction, retention, and DB access.
  - Shared-database regression validation against `UserMcpServers`, `UserSecrets`, `DataProtectionKeys`, and `ShareWithRevit` behavior if the same server is reused.
  - Full targeted backend test suite and contract validation run.
- **Contracts/docs to update**:
  - Threat model, security report updates if new findings emerge.
  - Operational runbook, rollout checklist, and shared DB compatibility notes.
- **Security/compliance controls and checkpoints**:
  - Satisfy `SEC-MUST-006`..`SEC-MUST-010`, `SEC-SHOULD-001`..`SEC-SHOULD-003`, `ROL-MUST-001`..`ROL-MUST-007`, `AC-010`.
  - Confirm release readiness only after all required artifacts and tests exist.
- **Agent/skill context**: `SecurityAssessment`, `continuous-ai-agentic-ci`, `verification-before-completion`.

## Plan-to-acceptance mapping

- **AC-001** → Steps 2, 3, 4
- **AC-002** → Steps 4, 5
- **AC-003** → Steps 5, 6
- **AC-004** → Step 6
- **AC-005** → Step 7
- **AC-006** → Steps 2, 4
- **AC-007** → Steps 4, 5, 8
- **AC-008** → Step 6
- **AC-009** → Step 6
- **AC-010** → Steps 1, 3, 8

## Done when

- [ ] Shared-database suitability is explicitly approved or rejected with documented rationale.
- [ ] Contract review gate artifacts exist and are accepted for implementation use.
- [ ] PostgreSQL-backed persistence is implemented with isolation from existing gateway MCP registration data.
- [ ] FS-0001-compatible APIs run over durable persistence without breaking existing consumers.
- [ ] Portability is proven for current PDF use and future BlockSuite-compatible consumption.
- [ ] Security, observability, rollout, and shared-database verification artifacts are complete.
