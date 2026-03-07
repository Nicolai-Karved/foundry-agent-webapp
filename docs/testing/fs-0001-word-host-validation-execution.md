# FS-0001 Manual Host Validation — Execution Guide

Use this guide to execute `fs-0001-word-host-validation-matrix.md` and capture evidence for sign-off.

## 1) Start local application stack

Use the existing workspace task **Start Local Dev Servers**.

Expected endpoints:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:8089`

## 2) Prepare test document and user

- Use a test user with delegated scope `Chat.ReadWrite`.
- Prepare a Word document that includes:
  - one content-control anchor (`contentControlTag` path),
  - one fallback-search anchor (`textSearchFallback` path).
- Confirm test user can access the add-in in integrated apps.

## 3) Load add-in in Word hosts

Execute in both hosts:

- Word Web (M365)
- Word Desktop (Windows)

For each host:

1. Open test document.
2. Open **Word Compliance Tasks** pane.
3. Enter `documentId`.
4. Run all AC checks in matrix order (AC-001 .. AC-006).

## 4) Capture evidence per matrix row

For each row:

- Set `Status` to `Pass` or `Fail`.
- Save evidence link (screenshot URL/path, clip, or ticket reference).
- Capture one correlation ID for key actions:
  - Load tasks
  - Status update
  - Re-verify
  - Telemetry event forwarding

Suggested evidence artifacts:

- task pane screenshot before/after action
- Word document screenshot showing highlight/comment behavior
- browser/devtools network capture for `X-Correlation-Id`
- backend log snippet filtered by correlation ID

## 5) Complete sign-off

- Fill the Sign-off section in the matrix file.
- Attach `fs-0001-word-host-validation-evidence.csv` to your work item.
- If failures exist, create defect tickets and include ticket IDs in Notes/Evidence.

## 6) Pass criteria

Validation is complete when:

- All matrix rows are `Pass`, or
- all `Fail` rows have accepted risk/defect records and owner sign-off.
