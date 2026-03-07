# Threat Model — FS-0001 Word Compliance Agent Tasks

## Feature summary

FS-0001 adds Word add-in task lifecycle operations for compliance tasks:

- read task list
- update task status
- retrieve citation context
- trigger manual re-verification
- navigate/highlight anchors and insert citation comments

## Assets

- Task state and status history
- Citation/reference context
- Audit metadata (`userId`, `timestamp`, `correlationId`)
- Access tokens for delegated user sessions

## Trust boundaries

1. Word add-in task pane (client runtime)
2. Backend API (`/api/*`) protected by JWT scope policy
3. Foundry/retrieval services and downstream stores
4. Logging/observability platform

## Threats and mitigations

## 1) Unauthorized API access

- **Threat**: caller invokes task endpoints without delegated permission.
- **Mitigations**:
  - all task endpoints require policy `RequireChatScope`
  - required scope: `Chat.ReadWrite`
  - JWT validation through Microsoft Identity Web
- **Verification**:
  - automated auth tests in API test suite (to be expanded)
  - manual token scope verification in pre-release checklist

## 2) Correlation/audit trace tampering

- **Threat**: missing or inconsistent trace IDs reduce forensic value.
- **Mitigations**:
  - backend accepts inbound `X-Correlation-Id` or generates one
  - backend echoes `X-Correlation-Id` in task endpoint responses
  - backend audit entries include correlation ID
- **Verification**:
  - header propagation checks
  - log sampling by correlation ID

## 3) Fallback anchor false positives

- **Threat**: text-search fallback points to wrong range and causes incorrect action/comment.
- **Mitigations**:
  - content control tag anchor preferred
  - low-confidence fallback (`< 0.8`) flagged in UI
  - low-confidence fallback blocked for auto-comment insertion
- **Verification**:
  - manual host tests for anchor quality paths
  - unit tests around resolver quality states (planned expansion)

## 4) Duplicate comment insertion

- **Threat**: repeated operations create noisy/duplicate citation comments.
- **Mitigations**:
  - deterministic task marker prefix in comment text (`[FS0001:{taskId}]`)
  - idempotent signature guard persisted in memory + local storage
- **Verification**:
  - repeat-selection tests across reloads

## 5) Excessive document data exposure

- **Threat**: unnecessary document content leaks through telemetry or API requests.
- **Mitigations**:
  - minimize payloads to identifiers/snippets only
  - no raw full-document logging in add-in telemetry
  - error/retry guidance enforces bounded retries and strict error handling
- **Verification**:
  - code review of request models and telemetry payloads
  - runbook checks before release

## Residual risks

- Document-native comment reconciliation is currently idempotent by signature, not deep semantic diff of existing comments.
- Client telemetry currently logs to console; production telemetry sink integration remains pending.

## Security sign-off evidence links

- Contracts: `docs/contracts/word-compliance/`
- API implementation: `backend/WebApp.Api/Program.cs`
- Task lifecycle service: `backend/WebApp.Api/Services/TaskLifecycleService.cs`
- Add-in behavior: `frontend/word-addin/src/taskpane/taskpane.ts`
- Observability runbook: `docs/operations/fs-0001-observability-runbook.md`
