# ADR-0001: Word Add-in task anchoring, state persistence, and manifest strategy

- **Status**: Accepted
- **Date**: 2026-03-05
- **Deciders**: Product owner + engineering lead + security reviewer
- **Related spec**: `docs/specs/FS-0001-word-compliance-agent-tasks/FeatureSpec.md`

## Context

We need a production-ready Word add-in experience that allows users to review compliance tasks, navigate to relevant document text, edit directly in Word, and re-verify with an existing backend agent.

Constraints:
- Must run in Word web and Word desktop for v1.
- Existing backend provides structured verification output, but status update API contract is not defined.
- Anchors must be resilient to document edits.
- Security governance requires least privilege, auditable actions, and data minimization.

## Decision

1. **Manifest strategy**
   - Use **add-in-only manifest** for v1 production rollout.
   - Rationale: unified manifest support for Word remains preview-oriented for some scenarios; production reliability is prioritized.

2. **Document anchor strategy**
   - Primary: Word content controls identified by deterministic tags.
   - Secondary: text-search fallback relinking when original anchor is missing and confidence threshold is met.

3. **Task state persistence strategy**
   - Persist task state in two places:
     - document-local metadata/settings for immediate in-doc continuity,
     - backend for cross-session consistency and auditability.

4. **Verification trigger strategy**
   - Manual re-verification only in v1 (user-triggered).

5. **Contract formalization strategy**
   - Introduce explicit API contract artifacts for task lifecycle endpoints and status updates, in addition to current structured LLM output contract.

## Consequences

### Positive
- Better anchor resilience under document edits than text-only search.
- Safer production posture with manifest path known to be broadly supported.
- Reliable task continuity (local + server) and audit trail for compliance workflows.
- Lower backend load and clearer user intent via manual re-verification.

### Negative / trade-offs
- Dual persistence introduces sync/conflict handling complexity.
- Fallback relinking may produce occasional false positives unless thresholding and UX are carefully tuned.
- Manual-only re-verification may feel slower than auto-validation for some users.

## Alternatives considered

1. **Text-search-only anchors**
   - Rejected: too fragile under document edits and format changes.

2. **Backend-only task state**
   - Rejected: weaker offline/nearline continuity in the open document context.

3. **Unified manifest for production v1**
   - Rejected: increased platform support risk for Word production usage.

4. **Auto re-verify on every edit**
   - Rejected for v1: noisy UX and unnecessary backend churn.

## Security and governance impact

This decision supports governance-aligned controls by enabling:
- data minimization (snippet-based processing),
- explicit auth boundaries (M365 identity-based access),
- auditable task-action lifecycle,
- threat-model-driven anchor/fallback risk controls.

## Follow-up actions

- Define and approve confidence threshold and relink UX.
- Finalize OpenAPI + schemas for task/status endpoints.
- Produce/update feature-specific threat model document.
- After stakeholder approval, update ADR status to **Accepted**.
