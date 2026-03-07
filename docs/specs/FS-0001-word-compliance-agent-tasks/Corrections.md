# Corrections — FS-0001 Word Compliance Agent Tasks

- **Spec Package ID**: `FS-0001-word-compliance-agent-tasks`
- **Mode**: Implementation-started verification
- **Date**: 2026-03-06

This file lists *remaining* gaps between current implementation state and required spec/instruction/ADR/security expectations.

---

## 1) Manual host acceptance validation not yet executed (MUST)

- **Severity**: MUST
- **Rule reference**: `FeatureSpec.md` → `AC-001..AC-006`, `ROL-MUST-001`, test strategy E2E section
- **Evidence**:
  - `docs/testing/fs-0001-word-host-validation-matrix.md` rows remain `Pending`.
  - `docs/testing/fs-0001-word-host-validation-evidence.csv` not populated.
- **Explanation**:
  - Acceptance criteria requiring Word host behavior cannot be considered verified until execution evidence exists.
- **Fix guidance**:
  - Execute matrix in Word Web and Desktop.
  - Capture evidence and correlation IDs per row and complete sign-off table.
- **Verification**:
  - All matrix rows marked `Pass` or formally dispositioned with approved defects/risks.

## 2) Filter/sort capability not implemented (SHOULD)

- **Severity**: SHOULD
- **Rule reference**: `FeatureSpec.md` → `FR-SHOULD-001`
- **Evidence**:
  - `frontend/word-addin/src/taskpane/taskpane.html` and `taskpane.ts` do not include filtering/sorting UI or data transforms.
- **Explanation**:
  - Spec recommends filter/sort for usability; implementation currently lists tasks without controls.
- **Fix guidance**:
  - Add status/source sort and basic filter controls with persisted preference during session.
- **Verification**:
  - UI tests or manual evidence showing sort/filter behavior.

## 3) Full endpoint integration test coverage incomplete (SHOULD)

- **Severity**: SHOULD
- **Rule reference**: `FeatureSpec.md` → Integration test strategy section
- **Evidence**:
  - `backend/WebApp.Api.Tests/ApiAuthorizationTests.cs` covers unauthorized checks.
  - No integration tests for authorized success/error contract paths of task endpoints.
- **Explanation**:
  - Current tests prove auth guard behavior but not full endpoint behavior under authenticated flows.
- **Fix guidance**:
  - Add integration tests for success + conflict + not-found scenarios per endpoint contracts.
- **Verification**:
  - Passing integration suite covering API contract behavior and error models.

---

## Overall verification verdict

- **Result**: **Partially compliant**
- **Blocking findings**: Item 1 (MUST, external/manual execution)
- **Non-blocking improvements**: Items 2–3 (SHOULD)
