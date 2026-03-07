# Implementation Plan — FS-0001 Word Compliance Agent Tasks

- **Spec Package ID**: `FS-0001-word-compliance-agent-tasks`
- **Date**: 2026-03-05
- **Plan status**: Ready for implementation
- **ADR gate**: ✅ `docs/architecture/decisions/ADR-0001-word-addin-task-anchoring-and-state.md` is **Accepted**

## Applicable instructions and skill context (summary)

- Global repository guidance from `.github/copilot-instructions.md` (minimal/local changes, impact mapping, validate continuously, build/test before completion).
- Language/style constraints loaded for C#, Python, and React/TypeScript instruction files.
- Security governance baseline loaded from `.github/agents/security-assessment.agent.md` and `.github/skills/security-assessment-ref/SKILL.md`.
- Execution skills to apply by step: `agentic-workflows`, `frontend-dev-guidelines`, `backend-dev-guidelines`, `testing-modernization`, `verification-before-completion`.

## Implementability and testability check

- **Corrections file present**: No corrections file found in this package.
- **Hard blockers**: None.
- **Decision gaps to close early (tracked as plan tasks, not blockers)**:
  1. Final fallback relink confidence threshold (spec open question).
  2. Final status update API concurrency/idempotency shape (spec open question).
- **Testability**: Implementable with deterministic unit/integration tests and manual host validation in Word web + desktop.

---

## Ordered implementation steps

### Step 1 — Baseline architecture, scope lock, and task decomposition

- **Scope (code touchpoints)**
  - `docs/specs/FS-0001-word-compliance-agent-tasks/FeatureSpec.md`
  - `docs/architecture/decisions/ADR-0001-word-addin-task-anchoring-and-state.md`
  - Workspace structure for backend/frontend/deployment
- **Expected outputs**
  - Frozen v1 scope checklist (MUST/SHOULD/MAY split) and explicit non-goals.
  - A file-level implementation task list tied to AC-001..AC-006.
- **Tests to add/update & verification**
  - None (planning baseline).
  - Verification: documented traceability matrix from FR/SEC/OBS/NFR to planned work items.
- **Contracts/docs to update**
  - Add AC traceability section in this `Plan.md` (included below).
- **Security/compliance checkpoints**
  - Confirm SEC-MUST-001..008 are represented as explicit work items before coding begins.
- **Agent/skill context**
  - `agentic-workflows`, `plan-writing`

### Step 2 — Define API-first contracts for task lifecycle

- **Scope (code touchpoints)**
  - New contract artifacts (recommended):
    - `docs/contracts/word-compliance/openapi.yaml`
    - `docs/contracts/word-compliance/task-output.schema.json`
    - `docs/contracts/word-compliance/error-model-and-retry.md`
- **Expected outputs**
  - OpenAPI contract for:
    - `GET /api/tasks`
    - `PATCH /api/tasks/{taskId}/status`
    - `POST /api/verification/rerun`
    - `GET /api/tasks/{taskId}/citation-context`
  - JSON Schema covering `Task`, `Anchor`, `TaskActionAudit`.
  - Error taxonomy + retry guidance for add-in client.
- **Tests to add/update & verification**
  - Contract validation checks (schema/OpenAPI lint if available).
  - Golden payload examples for success and error cases.
- **Contracts/docs to update**
  - Add endpoint docs to `backend/README.md` once backend routes exist.
- **Security/compliance checkpoints**
  - Enforce least privilege scopes and correlation ID fields in contract.
  - Ensure data minimization fields (snippet-only payload model) are explicit.
- **Agent/skill context**
  - `api-patterns`, `backend-dev-guidelines`, `testing-modernization`

### Step 3 — Backend models and persistence foundations for task state + audits

- **Scope (code touchpoints)**
  - `backend/WebApp.Api/Models/` (new DTOs for task, anchor, status update, audit)
  - `backend/WebApp.Api/Services/` (task service abstraction)
  - Optional storage adapter files (in-memory first or configured persistent store)
- **Expected outputs**
  - Backend domain/DTO layer for task list, status transition, citation context, rerun trigger response.
  - Local/document-context sync metadata shape aligned with ADR decisions.
- **Tests to add/update & verification**
  - Create test project (recommended): `backend/WebApp.Api.Tests/`.
  - Unit tests for status transition validation and rejected transitions.
  - Unit tests for audit record construction and required fields.
- **Contracts/docs to update**
  - Keep model definitions synchronized with JSON schema from Step 2.
- **Security/compliance checkpoints**
  - SEC-MUST-004: audit payload includes user, action, timestamp, correlation ID.
  - SEC-SHOULD-001: correlation ID must be generated/preserved.
- **Agent/skill context**
  - `backend-dev-guidelines`, `testing-modernization`

### Step 4 — Implement backend API endpoints and authorization guards

- **Scope (code touchpoints)**
  - `backend/WebApp.Api/Program.cs`
  - `backend/WebApp.Api/Services/*` (task orchestration + citation retrieval + rerun trigger)
  - `backend/WebApp.Api/Models/*` (request/response payloads)
- **Expected outputs**
  - Implemented authenticated endpoints for task list/status/rerun/citation-context.
  - Status updates persisted and reflected in task retrieval.
- **Tests to add/update & verification**
  - Integration tests for each endpoint success/error/auth failure paths.
  - Contract tests to ensure responses match Step 2 OpenAPI.
- **Contracts/docs to update**
  - Update `docs/contracts/word-compliance/openapi.yaml` with any final negotiated fields.
  - Update `backend/README.md` endpoint table.
- **Security/compliance checkpoints**
  - SEC-MUST-001: require user identity token on all new endpoints.
  - SEC-MUST-005: ensure HTTPS assumptions and deployment docs reflect TLS termination.
  - SEC-MUST-008: include these endpoints in security/vuln scan scope.
- **Agent/skill context**
  - `backend-dev-guidelines`, `verification-before-completion`

### Step 5 — Create Word add-in project skeleton (task pane + add-in-only manifest)

- **Scope (code touchpoints)**
  - New add-in app workspace (recommended path): `frontend/word-addin/`
  - Manifest XML (add-in-only) and task pane app bootstrap files
- **Expected outputs**
  - Buildable Word task pane add-in shell for web + desktop.
  - Manifest aligned with ADR (add-in-only, internal deployment posture).
- **Tests to add/update & verification**
  - Build/lint/typecheck for add-in project.
  - Manifest validation tooling pass.
- **Contracts/docs to update**
  - Add setup/run doc in `frontend/word-addin/README.md`.
- **Security/compliance checkpoints**
  - SEC-MUST-006: explicit domain allowlist for all task pane and Office.js domains.
  - SEC-MUST-005: only HTTPS endpoints in manifest URLs.
  - SEC-SHOULD-002: least-privilege manifest permissions.
- **Agent/skill context**
  - `frontend-dev-guidelines`, Office Add-ins references from spec sources

### Step 6 — Implement task panel UI and backend integration

- **Scope (code touchpoints)**
  - `frontend/word-addin/src/taskpane/*` (or equivalent)
  - Add-in API client and state management modules
- **Expected outputs**
  - Task list UI with status, citation snippet, source identifier.
  - Filtering/sorting (SHOULD), selection persistence after refresh (SHOULD).
  - Manual re-verify action wired to backend.
- **Tests to add/update & verification**
  - Unit tests for task state reducer/view-model logic.
  - Component tests for status transition UX and refresh behavior.
- **Contracts/docs to update**
  - UI/API mapping notes in add-in README.
- **Security/compliance checkpoints**
  - SEC-MUST-002: request payloads limited to required snippets/identifiers.
  - SEC-MUST-003: configurable redaction path before backend submission.
- **Agent/skill context**
  - `frontend-dev-guidelines`, `testing-modernization`

### Step 7 — Implement anchoring, navigation/highlighting, and fallback relink

- **Scope (code touchpoints)**
  - Word task pane Office.js integration modules
  - Anchor resolver utilities (content control tag → range, fallback text search)
- **Expected outputs**
  - Deterministic content-control anchoring.
  - Selection/highlight navigation when task is selected.
  - Fallback search relink with recorded confidence and anchor quality metadata.
- **Tests to add/update & verification**
  - Unit tests for resolver logic: exact tag, fallback success, fallback fail.
  - Manual host tests in Word web and desktop for navigation/highlight.
- **Contracts/docs to update**
  - Update JSON schema/OpenAPI examples to include anchor quality metadata.
- **Security/compliance checkpoints**
  - Threat-model checkpoint for fallback false-positive risk and user impact.
  - Log relink attempts with correlation IDs (no sensitive payload content).
- **Agent/skill context**
  - `frontend-dev-guidelines`, `agentic-workflows`

### Step 8 — Implement citation context comments in document

- **Scope (code touchpoints)**
  - Word comment creation/update integration in add-in code
  - Citation context retrieval client and rendering logic
- **Expected outputs**
  - Task-linked in-document comment containing citation/reference context.
  - Idempotent update behavior for existing mapped comments.
- **Tests to add/update & verification**
  - Unit tests for comment payload formatting/sanitization.
  - Manual validation in Word web + desktop on mapped ranges.
- **Contracts/docs to update**
  - Document comment format and lifecycle in add-in README.
- **Security/compliance checkpoints**
  - Ensure no secrets/PII leakage in comment text.
  - Data minimization applies to stored citation text snippets.
- **Agent/skill context**
  - `frontend-dev-guidelines`, Office.js reference practices

### Step 9 — End-to-end observability, auditing, and correlation

- **Scope (code touchpoints)**
  - Add-in telemetry hooks
  - Backend logging and metrics enrichment (`backend/WebApp.Api/*`)
  - Deployment/runtime config where needed
- **Expected outputs**
  - Client-side error/event telemetry for load/highlight/status/reverify.
  - API latency/failure metrics and correlation propagation.
  - 30-day audit retention policy configuration/documentation.
- **Tests to add/update & verification**
  - Automated checks for correlation ID propagation headers.
  - Observability smoke tests validating event emission on core actions.
- **Contracts/docs to update**
  - `docs/` observability runbook section for FS-0001 metrics and thresholds.
- **Security/compliance checkpoints**
  - SEC-MUST-004, OBS-MUST-001..004, NFR-OBS-001..005 verification checklist.
- **Agent/skill context**
  - `verification-before-completion`, `testing-modernization`

### Step 10 — Security hardening and threat model artifact

- **Scope (code touchpoints)**
  - Threat model document (recommended path): `docs/security/threat-model-FS-0001.md`
  - CI workflow files for dependency/vulnerability scanning (`.github/workflows/*`)
  - Manifest and auth configuration review points
- **Expected outputs**
  - Feature threat model updated and reviewed.
  - CI security scans enabled for backend/add-in dependencies.
- **Tests to add/update & verification**
  - CI pipeline pass with scan jobs active and non-ignored critical findings.
  - Auth scope tests for backend endpoint access control.
- **Contracts/docs to update**
  - Security controls evidence log linked from spec package.
- **Security/compliance checkpoints**
  - Direct mapping evidence for SEC-MUST-001..008 and SEC-SHOULD-001..002.
- **Agent/skill context**
  - `security-assessment-ref`, `verification-before-completion`

### Step 11 — Full verification, rollout readiness, and deployment prep

- **Scope (code touchpoints)**
  - Add-in manifest/package
  - Backend + add-in build/test scripts
  - Deployment docs for integrated apps portal rollout
- **Expected outputs**
  - Verified release candidate for internal deployment.
  - Rollout checklist and rollback notes.
- **Tests to add/update & verification**
  - Backend: build + all tests pass.
  - Add-in: lint/typecheck/build pass.
  - E2E manual matrix: Word web + desktop across AC-001..AC-006.
- **Contracts/docs to update**
  - Finalize deployment guide in `docs/` for internal integrated apps rollout.
  - Confirm manifest version bump policy and process.
- **Security/compliance checkpoints**
  - Final security sign-off bundle: auth, TLS, scans, threat model, retention.
- **Agent/skill context**
  - `verification-before-completion`, `agentic-workflows`

---

## Plan-to-Acceptance mapping (short)

- **AC-001 (task list visibility)**: Steps 2, 4, 6
- **AC-002 (status update local + backend)**: Steps 3, 4, 6
- **AC-003 (anchor navigation + highlight)**: Step 7
- **AC-004 (anchor recovery fallback)**: Step 7 (plus threshold decision in Step 2)
- **AC-005 (in-document citation note/comment)**: Step 8
- **AC-006 (manual re-verify + refresh)**: Steps 4, 6

## Security governance-to-plan mapping (short)

- **Access Control Policy**: Steps 4, 10, 11
- **Cryptography Policy**: Steps 5, 10, 11
- **Data Handling Procedure**: Steps 2, 6, 8, 10
- **System Policy (audit retention)**: Steps 3, 9, 11
- **Threat Modelling**: Step 10 (required before release)
- **Vulnerability Management Procedure**: Steps 10, 11

## Implementation progress snapshot (2026-03-06)

- ✅ Step 2 completed: contract artifacts added under `docs/contracts/word-compliance/`.
- ✅ Step 3 completed (initial reversible implementation): backend task models/service added with unit tests in `backend/WebApp.Api.Tests/`.
- ✅ Step 4 completed (initial API slice): authenticated task endpoints added in `backend/WebApp.Api/Program.cs`.
- ✅ Step 5 completed: add-in scaffold created under `frontend/word-addin/` with add-in-only manifest.
- ✅ Step 6 completed (initial): task pane can load tasks, update status, and trigger re-verify.
- ✅ Step 6 hardened: status state persisted in document-local settings and rehydrated on reload.
- ✅ Step 7 completed: centralized anchor resolver with thresholded fallback relink and document-local rebind metadata.
- ✅ Step 8 completed: citation comments use task marker, idempotent signature guard, and comment upsert behavior where host APIs support enumeration.
- ✅ Step 9 completed: correlation ID response echo + add-in telemetry events forwarded to backend telemetry sink + observability runbook.
- ✅ Step 10 completed: FS-0001 threat model added, CI security scan workflow created, and configurable PII redaction path implemented.
- ✅ Step 11 initial implementation: rollout checklist and release verification checklist artifacts added.

### Acceptance criteria coverage snapshot

- **AC-001**: implemented (task list retrieval + rendering).
- **AC-002**: implemented (status update flow with backend sync and document-local persistence).
- **AC-003**: implemented (navigation/highlighting via resolved anchor).
- **AC-004**: implemented (fallback relink gated by confidence threshold with rebind metadata persistence).
- **AC-005**: implemented (comment insertion with idempotent signature guard and document-native task-comment upsert behavior where host APIs support comment enumeration).
- **AC-006**: implemented for initial flow (manual re-verify and refresh).

### Remaining execution to fully close plan intent

- Execute manual host validation matrix in Word web + desktop and capture evidence using `docs/testing/fs-0001-word-host-validation-evidence.csv`.
