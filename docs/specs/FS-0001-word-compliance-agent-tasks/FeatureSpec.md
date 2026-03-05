# Feature Specification: Word Compliance Agent Tasks

- **Spec Package ID**: `FS-0001-word-compliance-agent-tasks`
- **Feature ID**: `word-compliance-agent-tasks`
- **Date**: 2026-03-05
- **Status**: Draft (Blocked for implementation until ADR gate is satisfied)
- **Primary host scope**: Word on the web + Word desktop
- **Deployment target**: Internal tenant deployment via Microsoft 365 admin center (Integrated apps)

## 0) ADR traceability and implementation gate

- **Referenced ADRs**:
  - `docs/architecture/decisions/ADR-0001-word-addin-task-anchoring-and-state.md`
- **Implementation gate**:
  - Implementation MUST NOT start until the referenced ADR is `Accepted` or an explicit documented exception is approved.

## 1) Outcome and scope

Enable a Word task pane add-in that works with an existing agent backend so users can:
1. Review all verification tasks for a document.
2. See citation/context from standards or reference documents.
3. Navigate/highlight linked content in the Word document.
4. Edit document content directly in Word.
5. Re-run verification manually.
6. Set task status (`open`, `in_review`, `done`, `skipped`, `blocked`).

### Non-goals (v1)
User provided: none explicitly excluded.  
For v1 scope control, the following are explicitly out of scope unless added by a later approved change:
- Public marketplace publishing.
- Fully automated re-verification on each edit.
- Cross-document bulk operations.

## 2) Users and primary flows

### User roles
- Document author
- Reviewer / compliance analyst

### Primary user flows
1. Open document and launch task pane add-in.
2. Task pane loads all current tasks from backend.
3. User selects a task.
4. Add-in highlights linked document location and shows citation/reference context.
5. User edits document directly in Word.
6. User sets task status.
7. User clicks **Re-verify** to regenerate/refresh tasks.

## 3) Functional requirements

## MUST requirements

### Task visibility and interaction
- **FR-MUST-001**: The add-in must display all active tasks in a persistent task panel while open.
- **FR-MUST-002**: Each task must show at least: title/summary, status, citation/reference snippet, and source identifier.
- **FR-MUST-003**: The user must be able to set status to one of: `open`, `in_review`, `done`, `skipped`, `blocked`.
- **FR-MUST-004**: Status changes must persist locally in document context and sync to backend.

### Document anchoring and navigation
- **FR-MUST-005**: Primary anchoring must use Word content controls with deterministic tags.
- **FR-MUST-006**: Selecting a task must navigate to and highlight the linked document range.
- **FR-MUST-007**: If a content-control anchor is missing, the system must attempt text-search relink and rebind when confidence threshold is met.

### Re-verification
- **FR-MUST-008**: The add-in must provide manual **Re-verify** action.
- **FR-MUST-009**: Re-verification response must refresh task list without requiring add-in restart.

### Reference context
- **FR-MUST-010**: For each task, the user must be able to view citation/reference context from standards or other reference documents.
- **FR-MUST-011**: If the task maps to in-document text, the add-in must create or update a **Word comment** anchored to the mapped range containing citation/reference context.

### Backend contract
- **FR-MUST-012**: Because status endpoints are not yet defined, the implementation must introduce explicit API contract artifacts for:
  - list tasks,
  - update task status,
  - trigger re-verification,
  - retrieve citations/reference context.

## SHOULD requirements
- **FR-SHOULD-001**: Task cards should support filtering and sorting (e.g., by status, severity, source standard).
- **FR-SHOULD-002**: UI should preserve current task selection after refresh when possible.
- **FR-SHOULD-003**: The add-in should show “anchor quality” metadata (exact tag match vs search fallback).

## MAY requirements
- **FR-MAY-001**: Batch status update operations.
- **FR-MAY-002**: User-configurable highlight colors by status.

## 4) Security, privacy, and governance requirements

These requirements align with repository security governance references (`security-assessment-ref`) and policy index names:
Secure Development Policy, Code Review Procedure, Development Vulnerability Management Procedure, Access Control Policy, Cryptography Policy, Test and Development Data Handling Procedure, System Policy, Threat Modelling.

### MUST security/privacy requirements
- **SEC-MUST-001**: Backend calls must use Microsoft 365 user identity (SSO/OAuth); no client-embedded static secret keys.
- **SEC-MUST-002**: Do not send full document content to backend by default; send minimum necessary snippets/context.
- **SEC-MUST-003**: Apply PII redaction before agent/backend submission where configured.
- **SEC-MUST-004**: Prompt/response and task-action audit logs must be retained for 30 days.
- **SEC-MUST-005**: Transport must be HTTPS/TLS for all add-in assets and API calls.
- **SEC-MUST-006**: Manifest/domain allowlist must explicitly include all domains used by task pane navigation and Office.js-capable frames.
- **SEC-MUST-007**: Threat model artifact must be produced/updated for this feature before release.
- **SEC-MUST-008**: CI must include dependency and vulnerability scanning for affected packages/services.

### SHOULD security requirements
- **SEC-SHOULD-001**: Correlate user action, request, and backend processing via correlation IDs.
- **SEC-SHOULD-002**: Use least-privilege permissions in manifest and backend scopes.

### Security control traceability matrix

| Control ID | Control summary | Governance reference |
|---|---|---|
| SEC-MUST-001 | M365 identity-based auth only | Access Control Policy |
| SEC-MUST-002 | Data minimization (snippets only) | Test and Development Data Handling Procedure |
| SEC-MUST-003 | PII redaction before backend call | Test and Development Data Handling Procedure |
| SEC-MUST-004 | 30-day audit retention | System Policy |
| SEC-MUST-005 | HTTPS/TLS for all traffic | Cryptography Policy |
| SEC-MUST-006 | Manifest/domain allowlist | Secure Development Policy |
| SEC-MUST-007 | Threat model artifact required | Threat Modelling |
| SEC-MUST-008 | CI vuln/dependency scanning | Development Vulnerability Management Procedure |
| SEC-SHOULD-001 | End-to-end correlation IDs | Code Review Procedure |
| SEC-SHOULD-002 | Least-privilege permissions | Access Control Policy |

## 5) Observability and diagnostics

- **OBS-MUST-001**: Capture client-side errors (task load, highlight, status update, re-verify).
- **OBS-MUST-002**: Capture API latency and failure rates for critical endpoints.
- **OBS-MUST-003**: Capture user interaction telemetry events: task selected, highlight invoked, status changed, re-verify invoked.
- **OBS-MUST-004**: Propagate correlation IDs across add-in and backend logs.

### Operational thresholds (v1)

- **NFR-OBS-001**: Task list load p95 latency ≤ 2.5s under normal tenant conditions.
- **NFR-OBS-002**: Re-verify request-to-refresh p95 latency ≤ 8s.
- **NFR-OBS-003**: Task status update API success rate ≥ 99.0% per rolling 24h.
- **NFR-OBS-004**: Audit log write success rate ≥ 99.9% per rolling 24h.
- **NFR-OBS-005**: Telemetry event ingestion completeness for core task actions ≥ 99%.

## 6) Data and integration requirements

### Data entities (minimum)
- **Task**: `taskId`, `documentId`, `title`, `description`, `status`, `citation`, `referenceSource`, `anchor`.
- **Anchor**: `anchorType` (`contentControlTag` | `textSearchFallback`), `anchorValue`, `confidence`, `lastValidatedAt`.
- **TaskActionAudit**: `actionId`, `taskId`, `actionType`, `previousStatus`, `newStatus`, `userId`, `timestamp`, `correlationId`.

### Integration constraints
- Existing backend already provides structured LLM output for task generation.
- Task status contract is currently missing and must be defined as part of this feature.

## 7) Compatibility and rollout

- **ROL-MUST-001**: v1 rollout is internal-only via Integrated apps in Microsoft 365 admin center.
- **ROL-MUST-002**: Use production-safe manifest type (add-in-only manifest) for Word production support; unified manifest may be considered only for non-production experimentation.
- **ROL-MUST-003**: Manifest versioning must be incremented on manifest changes requiring admin re-consent.

## 8) Acceptance criteria (Given/When/Then)

### AC-001: Task list visibility
- **Given** a document with verification results
- **When** the user opens the add-in task pane
- **Then** all returned tasks are visible in the panel with status and citation summary.

### AC-002: Task status update
- **Given** a visible task with status `open`
- **When** the user changes status to `in_review`
- **Then** status updates in the UI immediately and persists to document-local state and backend.

### AC-003: Anchor navigation and highlight
- **Given** a task linked to document content via content control tag
- **When** the user selects the task
- **Then** the add-in navigates to and highlights the mapped range in the document.

### AC-004: Anchor recovery
- **Given** a task whose original content-control anchor no longer exists
- **When** the user selects the task
- **Then** the add-in attempts text-search fallback and relinks automatically when confidence threshold is met.

### AC-005: In-document citation note
- **Given** a task mapped to in-document text
- **When** the task is rendered in UI
- **Then** citation/reference note is available in-document at or near mapped content.

### AC-006: Manual re-verification
- **Given** document edits are made
- **When** the user clicks Re-verify
- **Then** the backend re-evaluates and the task list refreshes with updated tasks/status recommendations.

## 9) Required contract artifacts

The following artifacts are required before implementation completion:
1. OpenAPI (or equivalent API contract) for:
   - `GET /tasks`
   - `PATCH /tasks/{taskId}/status`
   - `POST /verification/rerun`
   - `GET /tasks/{taskId}/citation-context`
2. JSON Schema for structured LLM task output (including anchor metadata).
3. Error model and retry guidance for client integration.

## 10) Test strategy

### Unit tests
- Task status transition validation.
- Anchor resolution logic (tag success, fallback search success/failure).
- DTO/schema validation for task payloads.

### Integration tests
- Add-in ↔ backend task retrieval.
- Status update synchronization (document-local + backend).
- Re-verify flow and task refresh.

### End-to-end tests
- Word web and desktop: open task pane, select task, highlight content, update status, re-verify.
- Anchor missing scenario with fallback relink.

### Security tests
- Authorization token handling and scope validation.
- Data minimization checks (no full-doc payload by default).
- PII redaction path verification.
- Audit retention and access checks.

## 11) Open decisions / follow-ups

1. Confirm exact confidence threshold for text-search fallback relink (e.g., 0.85).
2. Finalize backend status API shape with idempotency and optimistic concurrency fields.

## 12) Source references used

- Office Add-ins documentation: https://learn.microsoft.com/en-us/office/dev/add-ins/
- Word quickstart: https://learn.microsoft.com/en-us/office/dev/add-ins/quickstarts/word-quickstart-yo
- Word tutorial (content controls, task pane flow): https://learn.microsoft.com/en-us/office/dev/add-ins/tutorials/word-tutorial
- Office manifest guidance: https://learn.microsoft.com/en-us/office/dev/add-ins/develop/add-in-manifests
- Office JS overview: https://learn.microsoft.com/en-us/javascript/api/overview
- Word JS reference: https://learn.microsoft.com/en-us/javascript/api/word?view=word-js-preview
- Deploy/publish guidance: https://learn.microsoft.com/en-us/office/dev/add-ins/publish/publish
- Yeoman generator for Office: https://github.com/OfficeDev/generator-office
