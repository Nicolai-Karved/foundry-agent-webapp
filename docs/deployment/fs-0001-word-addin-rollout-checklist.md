# FS-0001 Rollout Checklist (Word Add-in + Backend)

## Pre-deployment

- [ ] Backend build passes (`backend/WebApp.sln`).
- [ ] Backend unit tests pass (`backend/WebApp.Api.Tests`).
- [ ] Task endpoint auth policy validated (`Chat.ReadWrite`).
- [ ] OpenAPI and schema docs reviewed (`docs/contracts/word-compliance/`).
- [ ] Threat model reviewed (`docs/security/threat-model-FS-0001.md`).
- [ ] Security CI workflow green (`.github/workflows/fs0001-security-scan.yml`).

## Manifest and add-in

- [ ] `manifest.xml` `SourceLocation` points to HTTPS production task pane URL.
- [ ] `AppDomain` values are minimal and environment-appropriate.
- [ ] Icon and support URLs are valid in production environment.
- [ ] Manifest version incremented for rollout.
- [ ] Add-in package uploaded via Microsoft 365 integrated apps portal.

## Functional validation (Word web + desktop)

- [ ] AC-001: tasks load and render.
- [ ] AC-002: status update persists and refreshes.
- [ ] AC-003: task selection navigates/highlights anchor.
- [ ] AC-004: fallback relink behavior shows warning on low-confidence.
- [ ] AC-005: citation comment insertion is idempotent for unchanged task/context.
- [ ] AC-006: manual re-verify accepted and task list refreshes.

## Observability and operations

- [ ] Correlation IDs visible in request/response and logs.
- [ ] Alert thresholds configured per runbook.
- [ ] On-call runbook reviewed by support team.

## Rollback readiness

- [ ] Previous manifest/package retained for rollback.
- [ ] Feature toggle or deployment rollback procedure documented.
- [ ] Communication template prepared for pilot users.
