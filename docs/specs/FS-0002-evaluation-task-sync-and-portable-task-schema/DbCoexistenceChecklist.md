# FS-0002 Shared Database Coexistence Checklist

## Purpose

Use this checklist before implementing any FS-0002 persistence changes against `pg-naviate-agent-gateway`.

The PostgreSQL server is already used by the existing `naviate-revit-ai-agent` gateway for MCP-related persistence. Current evidence shows the protected compatibility surface includes:

- `UserMcpServers`
- `UserSecrets`
- `DataProtectionKeys`
- `ShareWithRevit` behavior on `UserMcpServers`

## Step 1 baseline status (2026-03-06)

Step 1 has been started and the current gateway persistence surface has been baselined from code in `C:\Users\nickar\source\repos\naviate-revit-ai-agent`.

### Evidence captured

- `NaviateGateway\Program.cs` wires `GatewayDbContext` through `UseNpgsql(...)` using `ConnectionStrings:NaviateGatewayDb` or `NAVIATE_GATEWAY_DB_CONNECTION_STRING`.
- `GatewayDbContext` exposes `UserMcpServers`, `UserSecrets`, and `DataProtectionKeys`.
- The current migration chain creates `DataProtectionKeys` and `UserMcpServers`, adds `UserSecrets`, and then adds `ShareWithRevit` to `UserMcpServers`.
- `UserMcpServerStore.ListSharedWithRevitDescriptorsAsync(...)` reads only `UserMcpServers` rows where `UserObjectId == current user`, `ShareWithRevit == true`, and `Enabled == true`.
- `UserMcpServerStore.UpsertAsync(...)` persists encrypted MCP secrets to `UserMcpServers.ProtectedSecretJson` using ASP.NET Core Data Protection, while `UserSecrets` stores other protected per-user secrets.

### Baseline decision record

- **Decision status**: `pg-naviate-agent-gateway` remains a conditional reuse candidate, not an approved implementation target yet.
- **Reasoning**: current code evidence shows a real, active compatibility surface that FS-0002 must not disturb. No evidence currently suggests FS-0002 must use a separate PostgreSQL server, but shared-server reuse is blocked until schema isolation, dedicated roles, migration isolation, ownership approval, and operational checks are completed.
- **Current implementation posture**: treat `UserMcpServers`, `UserSecrets`, `DataProtectionKeys`, `ProtectedSecretJson`, and `ShareWithRevit` behavior as protected and non-breaking by default.

## Preconditions

- [x] `ADR-0002` is **Accepted** or a documented exception is approved.
- [ ] Draft FS-0002 OpenAPI and portable-task JSON Schema artifacts have been reviewed.
- [ ] The target deployment environment for FS-0002 is identified and approved.

## Existing dependency inventory

Validate and record the current gateway dependency catalog from `C:\Users\nickar\source\repos\naviate-revit-ai-agent`:

- [x] Confirm `NaviateGateway\Program.cs` still wires `GatewayDbContext` through `UseNpgsql(...)`.
- [x] Confirm `GatewayDbContext` still exposes `UserMcpServers`, `UserSecrets`, and `DataProtectionKeys`.
- [x] Confirm current migrations still define the MCP storage surface expected by the gateway.
- [x] Confirm whether any additional MCP-related tables, views, functions, or schemas now exist beyond the currently known catalog.
- [x] Confirm `ShareWithRevit` semantics are still active and documented.

### Catalog confirmed from current code

| Surface | Evidence | Notes |
|---|---|---|
| `UserMcpServers` | `GatewayDbContext`, `InitialGatewayDb`, `AddShareWithRevitToUserMcpServers`, `UserMcpServerStore` | Primary MCP registration table; includes endpoint, enabled flag, encrypted secret payload, and `ShareWithRevit`. |
| `UserSecrets` | `GatewayDbContext`, `AddUserSecrets` | Separate protected per-user secrets store; must remain untouched by FS-0002 migrations. |
| `DataProtectionKeys` | `GatewayDbContext`, `InitialGatewayDb`, `PersistKeysToDbContext<GatewayDbContext>()` | Critical for decrypting protected values persisted by the gateway. |
| `ProtectedSecretJson` | `UserMcpServerEntity`, `UserMcpServerStore.UpsertAsync(...)` | Encrypted payload stored in `UserMcpServers`; changes to key persistence could break reads. |
| `ShareWithRevit` behavior | `AddShareWithRevitToUserMcpServers`, `UserMcpServerStore.ListSharedWithRevitDescriptorsAsync(...)` | Revit-visible sharing is filtered by `UserObjectId`, `ShareWithRevit`, and `Enabled`. |

### Additional source-level scan result

- A source scan of `NaviateGateway` found no additional MCP-specific tables, schemas, views, or migration-defined database objects beyond the currently catalogued `UserMcpServers`, `UserSecrets`, and `DataProtectionKeys` surface.
- The scan also confirmed frontend/admin UI dependencies on `ShareWithRevit` in `wwwroot/mcp-settings.html` and `wwwroot/index.html`, reinforcing that this behavior is part of the live compatibility surface.
- This result is limited to source-defined objects in the checked-in repo; runtime-only/manual database changes would still need environment verification before final approval.

## Isolation requirements

If FS-0002 reuses the same PostgreSQL server:

- [ ] Create FS-0002 tables in a **dedicated schema**.
- [ ] Use **dedicated DB roles** for FS-0002.
- [ ] Do not modify, rename, repurpose, or drop `UserMcpServers`, `UserSecrets`, or `DataProtectionKeys`.
- [ ] Do not reuse existing gateway EF migrations or DbContext for FS-0002 persistence.
- [ ] Keep migration history isolated from the existing gateway migration chain.
- [ ] Ensure rollback for FS-0002 cannot roll back or mutate gateway MCP registration objects.

## Compatibility checks

Before rollout, verify:

- [ ] Existing gateway MCP server list still loads successfully.
- [ ] Existing MCP server upsert/update still works.
- [ ] Existing delete behavior still works.
- [ ] `ListSharedWithRevitDescriptorsAsync(...)` behavior remains unchanged.
- [ ] Protected secret handling still works for `UserSecrets` and `ProtectedSecretJson`.
- [ ] Data Protection key persistence remains intact.
- [ ] Revit-facing shared MCP server behavior still respects `ShareWithRevit`.

## Operational checks

- [ ] Capacity impact on the shared PostgreSQL server is reviewed.
- [ ] Backup and restore scope is verified for both gateway data and FS-0002 data.
- [ ] Monitoring and alerts can distinguish gateway MCP failures from FS-0002 persistence failures.
- [ ] Runbook updates include shared-database troubleshooting guidance.

## Decision outcome

Choose one and document it in the implementation PR/spec update:

- [ ] Reuse `pg-naviate-agent-gateway` with isolated FS-0002 schema and roles.
- [ ] Do **not** reuse `pg-naviate-agent-gateway`; choose a separate PostgreSQL target and update ADR/spec accordingly.

Current Step 1 result: **no final target selected yet**. Shared-server reuse is still viable in principle, but only as an additive, isolated schema/role deployment with explicit regression evidence against the protected gateway surface.

## Evidence to attach

- [ ] Compatibility test results
- [ ] Schema/role design
- [ ] Migration plan and rollback plan
- [ ] Capacity and operational review
- [ ] Approval from relevant owners for both FS-0002 and the existing gateway usage

### Evidence used for the current baseline

- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Program.cs`
- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\GatewayDbContext.cs`
- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Entities\UserMcpServerEntity.cs`
- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Entities\UserSecretEntity.cs`
- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Migrations\20260108125511_InitialGatewayDb.cs`
- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Migrations\20260108160658_AddUserSecrets.cs`
- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Data\Migrations\20260109170315_AddShareWithRevitToUserMcpServers.cs`
- `C:\Users\nickar\source\repos\naviate-revit-ai-agent\NaviateGateway\Services\UserMcpServerStore.cs`
- `C:\Users\nickar\source\repos\foundry-agent-webapp\backend\WebApp.Api\Data\Migrations\EvaluationTask\20260306161248_InitialEvaluationTaskPersistence.cs`
- `C:\Users\nickar\source\repos\foundry-agent-webapp\.config\dotnet-tools.json`
