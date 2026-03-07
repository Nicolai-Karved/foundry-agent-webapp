# FS-0002 Rollout Checklist

Use this checklist before enabling durable task sync outside local development.

## Feature gates

- [ ] `EvaluationTaskSync:Enabled` reviewed and explicitly enabled for the target environment
- [ ] `EvaluationTaskSync:Persistence:AutoMigrateOnStartup` remains `false` unless environment owners approve startup migrations
- [ ] `FS0002_TASK_PERSISTENCE_CONNECTION_STRING` or the configured connection-string entry is supplied through approved secret management

## Database readiness

- [ ] Target database decision is recorded: shared `pg-naviate-agent-gateway` or separate PostgreSQL target
- [ ] FS-0002 schema is `fs0002`
- [ ] Dedicated DB roles for FS-0002 are provisioned
- [ ] Migration artifacts under `backend/WebApp.Api/Data/Migrations/EvaluationTask/` are reviewed
- [ ] Rollback path is reviewed using the EF migration `Down` path and operational restore guidance

## Shared-server coexistence

- [ ] `DbCoexistenceChecklist.md` is completed with environment-specific evidence
- [ ] Existing gateway MCP list/upsert/delete flows are regression-tested
- [ ] `ShareWithRevit` behavior is regression-tested
- [ ] `UserSecrets` and `DataProtectionKeys` behavior is regression-tested
- [ ] Monitoring can distinguish gateway failures from FS-0002 failures

## Application verification

- [ ] `dotnet build` passes for `backend/WebApp.Api`
- [ ] Backend tests pass, including `EvaluationTaskPersistenceServiceTests`
- [ ] Contract artifacts in `docs/contracts/evaluation-task-sync/` are accepted for implementation use
- [ ] Sync ingest rejects unsupported schema versions and oversized payloads
- [ ] Existing FS-0001-compatible task endpoints still behave correctly with persistence enabled

## Security and compliance

- [ ] `docs/security/threat-model-FS-0002.md` is reviewed
- [ ] Security report action items are rechecked for the target environment
- [ ] Retention, redaction, and least-privilege settings are confirmed
- [ ] No direct database credentials are exposed to frontend or renderer clients

## Release decision

- [ ] Product owner approval recorded
- [ ] Database owner approval recorded
- [ ] Security/governance approval recorded
- [ ] Release note documents whether FS-0002 uses a shared or separate PostgreSQL target
