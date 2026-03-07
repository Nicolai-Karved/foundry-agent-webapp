# Threat Model — FS-0002 Durable Evaluation Task Sync

Date: 2026-03-06  
Scope: durable evaluation-task sync, PostgreSQL-backed persistence, FS-0001-compatible task APIs, current PDF consumer, future BlockSuite-compatible consumer.

## Assets

- Canonical task records
- Evaluation run metadata
- User task-state overlays
- Sync receipts and task-action audits
- PostgreSQL schema `fs0002`
- Protected compatibility surface in shared-server scenarios:
  - `UserMcpServers`
  - `UserSecrets`
  - `DataProtectionKeys`
  - `ShareWithRevit` behavior

## Trust boundaries

1. Evaluation/orchestrator pipeline → task sync ingest API
2. User-facing client → task read/update/rerun APIs
3. Application service → PostgreSQL persistence schema
4. FS-0002 schema → existing gateway schema/table surface when a shared PostgreSQL server is reused

## Entry points

- `POST /api/task-sync/ingest`
- `GET /api/task-snapshots/{documentId}`
- `GET /api/tasks`
- `PATCH /api/tasks/{taskId}/status`
- `PATCH /api/tasks/{taskId}/overlay`
- `GET /api/tasks/{taskId}/citation-context`
- `POST /api/verification/rerun`

## Primary threats and controls

| Threat | Risk | Control |
|---|---|---|
| Replay or duplicate sync submission | Duplicate tasks, inconsistent state, noisy audit trail | Canonical payload hash + document/evaluation-run deduplication; sync receipts recorded for accepted and deduplicated submissions |
| Malformed or oversized sync payload | Validation bypass, storage abuse, downstream failures | Schema version check, payload field limits, task-count limit, request rejection before persistence |
| Unauthorized task read/write | Cross-tenant or cross-document data exposure | Existing authenticated API boundary; document-scoped access remains required; no direct DB access to clients |
| Shared-server schema interference | FS-0002 migration or runtime change breaks MCP registration | Dedicated `fs0002` schema, separate migrations, separate roles required, no modification of gateway tables |
| Secret/key material exposure through shared DB | Inability to decrypt existing gateway secrets or accidental key mutation | FS-0002 schema isolation; do not touch `DataProtectionKeys`, `UserSecrets`, or `ProtectedSecretJson` paths |
| User-entered text leakage in overlays/audits | PII or sensitive data stored too broadly | Resolution-note length limits, redaction/truncation requirements, least-privilege access |
| Silent task history loss on rerun | Loss of auditability and reviewer trust | Supersede old generated records instead of hard-delete; retain audit trail and sync receipts |
| Renderer-specific contract drift | PDF or future BlockSuite clients interpret tasks differently | Canonical schema with extension namespaces; renderer-specific data only in optional extensions |

## Abuse cases to verify

- Re-submit the exact same sync payload twice and confirm a deduplicated receipt is stored.
- Submit a different payload with the same document and logical task keys and confirm prior unmatched tasks become `superseded` rather than deleted.
- Update task state as a user, resync matching logical task keys, and confirm overlay state carries forward.
- Enable the FS-0002 feature in a shared-server environment and verify gateway MCP flows still work unchanged.
- Attempt unauthorized task reads/updates and confirm the API rejects them.

## Residual risks and required approvals

- Backend-only identity enforcement for sync ingestion still needs production identity/scoping confirmation before rollout.
- Shared-server reuse remains blocked pending operational approval, capacity review, and compatibility evidence in a real environment.
- Auto-migration is available but disabled by default; enabling it in a shared-server environment requires explicit owner approval.

## Verification hooks

- `backend/WebApp.Api.Tests/EvaluationTaskPersistenceServiceTests.cs`
- `backend/WebApp.Api.Tests/ApiAuthorizationTests.cs`
- `.github/workflows/fs0001-security-scan.yml`
- `docs/specs/FS-0002-evaluation-task-sync-and-portable-task-schema/DbCoexistenceChecklist.md`
- `backend/WebApp.Api/Data/Migrations/EvaluationTask/*`
