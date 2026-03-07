# FS-0001 Word Host Validation Matrix

This matrix is for manual validation in real Word hosts (web and desktop).

Companion artifacts:

- Execution guide: `docs/testing/fs-0001-word-host-validation-execution.md`
- Evidence template: `docs/testing/fs-0001-word-host-validation-evidence.csv`
- Helper script: `deployment/scripts/start-fs0001-validation.ps1`

## Test environments

- Word Web (Microsoft 365)
- Word Desktop (Windows)

## Preconditions

- Deployed backend API reachable over HTTPS.
- Add-in manifest published to integrated apps catalog.
- Test user has delegated scope `Chat.ReadWrite`.
- Test document contains at least one content control tag anchor and one fallback-search anchor path.

## Validation matrix

| ID | Acceptance Criterion | Host | Steps | Expected Result | Status | Evidence |
|----|----------------------|------|-------|-----------------|--------|----------|
| AC-001-WEB | AC-001 Task list visibility | Word Web | Open task pane, enter document ID, click Load Tasks | Tasks render with title/status/citation context availability | Pending | |
| AC-001-DESK | AC-001 Task list visibility | Word Desktop | Open task pane, enter document ID, click Load Tasks | Tasks render with title/status/citation context availability | Pending | |
| AC-002-WEB | AC-002 Status update | Word Web | Set task to In Review/Done | Status update succeeds and refreshed list shows updated status/version | Pending | |
| AC-002-DESK | AC-002 Status update | Word Desktop | Set task to In Review/Done | Status update succeeds and refreshed list shows updated status/version | Pending | |
| AC-003-WEB | AC-003 Anchor navigation/highlight | Word Web | Select task with strong anchor confidence | Matching text/range is selected and highlighted | Pending | |
| AC-003-DESK | AC-003 Anchor navigation/highlight | Word Desktop | Select task with strong anchor confidence | Matching text/range is selected and highlighted | Pending | |
| AC-004-WEB | AC-004 Fallback relink behavior | Word Web | Select low-confidence fallback task | Warning shown; highlight occurs; auto-comment insertion skipped | Pending | |
| AC-004-DESK | AC-004 Fallback relink behavior | Word Desktop | Select low-confidence fallback task | Warning shown; highlight occurs; auto-comment insertion skipped | Pending | |
| AC-005-WEB | AC-005 Citation comment idempotency | Word Web | Select same task repeatedly and reload add-in | No duplicate comment inserted for unchanged signature | Pending | |
| AC-005-DESK | AC-005 Citation comment idempotency | Word Desktop | Select same task repeatedly and reload add-in | No duplicate comment inserted for unchanged signature | Pending | |
| AC-006-WEB | AC-006 Manual re-verify | Word Web | Click Re-verify | Backend accepts request and list refreshes | Pending | |
| AC-006-DESK | AC-006 Manual re-verify | Word Desktop | Click Re-verify | Backend accepts request and list refreshes | Pending | |

## Correlation/observability checks

- Capture one correlation ID per host during:
  - task load,
  - status update,
  - re-verify,
  - telemetry event forwarding.
- Verify correlation ID appears in:
  - request header (`X-Correlation-Id`),
  - backend response header,
  - backend logs.

## Sign-off

| Role | Name | Date | Notes |
|------|------|------|-------|
| QA |  |  |  |
| Security Reviewer |  |  |  |
| Feature Owner |  |  |  |
