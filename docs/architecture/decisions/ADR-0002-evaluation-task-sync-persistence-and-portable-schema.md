# ADR-0002: Evaluation task sync persistence and portable task schema

- **Status**: Accepted
- **Date**: 2026-03-06
- **Deciders**: Product owner + engineering lead + security reviewer (pending confirmation)
- **Related spec**: `docs/specs/FS-0002-evaluation-task-sync-and-portable-task-schema/FeatureSpec.md`
- **Related prior ADR**: `docs/architecture/decisions/ADR-0001-word-addin-task-anchoring-and-state.md`

## Context

`FS-0001` introduced Word-oriented task lifecycle behavior and compatible APIs, but the current implementation remains centered on in-memory backend state and a Word-specific consumption path.

We now need an additive architecture that:
- persists evaluation-generated tasks durably across sessions and reruns,
- keeps current FS-0001 consumers working,
- provides a canonical task schema reusable by the current PDF viewer and a future BlockSuite-driven editor,
- preserves auditability, idempotency, and reconciliation semantics,
- prefers reuse of the existing PostgreSQL environment (`pg-naviate-agent-gateway`) when safe to do so.

Constraints:
- Existing backend evaluation/orchestrator output is the source for generated tasks.
- FS-0001 client contracts must not be broken.
- BlockSuite is a future schema portability target, not the required system of record.
- Security governance requires authenticated sync boundaries, least privilege, auditability, and data minimization.

## Decision

1. **Canonical task model and sync boundary**
   - Introduce a versioned, canonical task schema and an explicit sync boundary between evaluation/orchestrator output and persisted task records.
   - Rationale: this separates generation concerns from client-specific rendering and enables validation, idempotency, and contract stability.

2. **Persistence strategy**
   - Use PostgreSQL as the durable store.
   - Prefer extending `pg-naviate-agent-gateway` if tenancy, lifecycle, performance, access control, backup, environment segregation, networking, and governance checks pass.
   - The currently evidenced gateway persistence surface includes `UserMcpServers`, `UserSecrets`, and `DataProtectionKeys`, with `ShareWithRevit` as an explicit sharing flag for Revit use.
   - If `pg-naviate-agent-gateway` is already used for Revit/Web Agent MCP server registration, FS-0002 must coexist through dedicated schema and role boundaries and must not require destructive changes to those existing registration objects.
   - If those checks fail, choose an alternative PostgreSQL target only through an ADR update while preserving the same logical contract.

3. **Authorization strategy**
   - Sync ingestion uses a backend managed identity or approved service principal/workload identity.
   - Viewer/editor clients continue to use delegated Microsoft 365 user identity through approved backend APIs.
   - No client surface receives direct database credentials.

4. **Client portability strategy**
   - Keep the canonical schema editor-neutral.
   - Represent client-specific needs through optional extension namespaces/mappings rather than embedding Word, PDF, or BlockSuite semantics into the core schema.

5. **Rerun reconciliation strategy**
   - Separate generated task facts from user-managed task state sufficiently to carry forward compatible reviewer actions across reruns.
   - Mark superseded tasks explicitly rather than hard-deleting active history.

6. **Compatibility strategy**
   - Keep existing FS-0001 task APIs as the compatibility facade during rollout.
   - Introduce durable persistence behind those compatible contracts first, then add new sync-specific APIs/artifacts as needed.
   - Treat existing Revit/Web Agent MCP registration usage of `pg-naviate-agent-gateway`, if confirmed, as a protected compatibility surface during design, migration, and rollback planning.

7. **Privacy and retention strategy**
   - Persist only remediation-relevant snippets and approved identifiers.
   - Redact or truncate free-text reviewer/audit fields before persistence where they may contain unnecessary personal or document data.
   - Keep canonical task records for the governed document lifecycle while applying bounded retention/purge or archive rules to sync receipts and task-action audits.

## Consequences

### Positive
- Establishes a durable system of record for compliance tasks.
- Avoids coupling task meaning to a single client surface.
- Enables current PDF-viewer reuse and future BlockSuite adoption without redefining task semantics.
- Improves auditability, replay handling, and rerun reconciliation.
- Preserves backward compatibility for FS-0001 consumers.

### Negative / trade-offs
- Introduces reconciliation complexity between generated tasks and user-managed state.
- Requires schema versioning, migration planning, and persistence governance.
- May require additional operational controls if the preferred PostgreSQL target cannot be reused as-is.

## Alternatives considered

1. **Continue with in-memory backend task state only**
   - Rejected: does not satisfy durable persistence, replay safety, or multi-client reuse.

2. **Create separate client-specific schemas for Word, PDF, and BlockSuite**
   - Rejected: duplicates business meaning and increases migration/maintenance cost.

3. **Adopt BlockSuite as the primary system of record immediately**
   - Rejected: BlockSuite is a future editor target, not a backend persistence requirement.

4. **Replace FS-0001 APIs with a new incompatible API surface**
   - Rejected: violates backward compatibility goals and increases rollout risk.

## Security and governance impact

This decision supports governance-aligned controls by enabling:
- authenticated backend-only ingestion,
- least-privilege database access,
- data minimization for persisted evidence,
- explicit audit trails for sync and state changes,
- threat-model-driven handling of replay, reconciliation, and multi-client data consumption.

## Follow-up actions

- Confirm whether `pg-naviate-agent-gateway` is appropriate for the new schema and workload.
- Inventory existing Revit/Web Agent MCP registration schemas, tables, roles, and queries if they live in `pg-naviate-agent-gateway`.
- Preserve the current `UserMcpServers` / `UserSecrets` / `DataProtectionKeys` tables and `ShareWithRevit` semantics unless an explicitly approved migration supersedes them.
- Define the canonical portable task schema and extension namespace conventions.
- Define stable logical task-key and reconciliation rules.
- Produce/update threat model and contract artifacts before implementation.
- After stakeholder approval, update ADR status to **Accepted**.

## Acceptance conditions before status changes to Accepted

- Confirm deciders and record approval outcome.
- Validate `pg-naviate-agent-gateway` against tenancy, lifecycle ownership, capacity, networking, backup/restore, environment segregation, and access-control requirements.
- Validate non-interference with existing Revit/Web Agent MCP registration workloads, including schema isolation, role isolation, migration rollback, and regression checks of current registration paths.
- Approve the logical task-key matching strategy and collision-handling rules.
- Approve the extension namespace convention for editor-specific enrichments.
- Confirm the concrete authorization model for sync producers and delegated clients.
