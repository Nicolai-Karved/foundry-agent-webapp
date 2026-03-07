# FS-0002 Local Smoke Test and Word Add-in Test Guide

This guide covers two things:

1. how to verify the new FS-0002 persistence-backed task flow locally, and
2. what you can realistically test in the current Word add-in scaffold.

## Local FS-0002 smoke test

### What it verifies

The local smoke test exercises both the new FS-0002 path and the existing FS-0001-compatible API surface:

- `POST /api/task-sync/ingest`
- `GET /api/task-snapshots/{documentId}`
- `GET /api/tasks?documentId=...`
- `PATCH /api/tasks/{taskId}/status`
- `GET /api/tasks/{taskId}/citation-context`
- `POST /api/verification/rerun`

The seeded payload deliberately uses Word-friendly anchors:

- content control tag: `fs0001-air-owner`
- fallback text search: `Model exchange schedule`

### Prerequisites

- Local stack running:
  - frontend on `http://localhost:5173`
  - backend on `http://localhost:8089`
  - PostgreSQL on `localhost:5432`
- User authenticated locally for the backend API scope `Chat.ReadWrite`
- Azure CLI signed in **interactively as a user** if you want the helper script to auto-acquire a token

### Script

Use:

- `deployment/scripts/invoke-fs0002-local-smoke-test.ps1`

The script will:

- load `.env` values if available,
- try to acquire a bearer token using Azure CLI and `ENTRA_SPA_CLIENT_ID`,
- seed a unique document ID and evaluation run,
- persist tasks into the local PostgreSQL-backed API,
- verify the compatibility endpoints still work.

> Note:
> If Azure CLI is logged in as a **service principal**, token acquisition for `api://<client-id>/Chat.ReadWrite` will fail because the backend expects a delegated user token, not an app-only token.

### If token acquisition fails

Provide a bearer token explicitly.

The required scope is:

- `api://<ENTRA_SPA_CLIENT_ID>/Chat.ReadWrite`

## Word add-in: current testability status

The repository contains a real add-in scaffold here:

- `frontend/word-addin/manifest.xml`
- `frontend/word-addin/src/taskpane/taskpane.html`
- `frontend/word-addin/src/taskpane/taskpane.ts`

But it is not yet a fully runnable local add-in application.

### What is present

- add-in-only Word manifest
- task pane HTML/CSS/TypeScript scaffold
- endpoint wiring for:
  - `GET /api/tasks`
  - `PATCH /api/tasks/{taskId}/status`
  - `GET /api/tasks/{taskId}/citation-context`
  - `POST /api/verification/rerun`
- Word anchor resolution logic for:
  - `contentControlTag`
  - `textSearchFallback`

### Current blockers for true end-to-end Word-host testing

The scaffold is not yet fully runnable as a local Word add-in because:

1. `manifest.xml` points to `https://localhost:3000/taskpane.html`, but the repo’s normal frontend dev server runs on `5173`.
2. `taskpane.html` imports `./taskpane.ts` directly, which means it still needs a bundling/transpile step before a browser or Word task pane can run it.
3. the task pane scaffold does not yet implement Microsoft 365/Entra token acquisition for backend calls, while the backend task endpoints require a bearer token with `Chat.ReadWrite`.

So, today, the Word add-in surface is best treated as a scaffold whose:

- manifest shape,
- UI contract,
- endpoint expectations,
- anchor behavior,

can be reviewed and prepared for host testing, but not yet validated as a full live authenticated add-in without one more implementation slice.

## What you can test right now

### 1. Verify backend behavior the Word add-in depends on

Run the FS-0002 smoke test first.

That confirms the Word-facing endpoints can serve persisted tasks and accept status/rerun operations.

### 2. Prepare a Word document for the scaffold’s anchor model

Create a test document containing:

- a content control tagged exactly `fs0001-air-owner`
- the text `Model exchange schedule` somewhere in the body

Those values match the smoke-test payload and the current taskpane anchor logic.

### 3. Review the manual validation matrix

The existing FS-0001 manual validation assets are still the right acceptance checklist:

- `docs/testing/fs-0001-word-host-validation-matrix.md`
- `docs/testing/fs-0001-word-host-validation-execution.md`
- `docs/testing/fs-0001-word-host-validation-evidence.csv`

They remain valid because FS-0002 preserves the FS-0001-compatible task APIs behind persistence.

## What is needed for actual live Word-host testing

To test the add-in for real in Word Web/Desktop, the next implementation slice needs to do three things:

1. add a build/serve pipeline for `frontend/word-addin`
2. serve the task pane over HTTPS at the same URL used by the manifest
3. add token acquisition and authenticated API calls from the task pane to the backend

Once that is done, the concrete manual test flow should be:

1. run the FS-0002 smoke test to seed tasks
2. open the prepared Word document
3. sideload/publish the manifest
4. open **Word Compliance Tasks**
5. enter the smoke-test `documentId`
6. validate:
   - task list load
   - status update
   - strong anchor highlight via `fs0001-air-owner`
   - low-confidence fallback warning via `Model exchange schedule`
   - citation comment behavior
   - rerun behavior

## Recommended next step

If you want, the next practical slice is to make `frontend/word-addin` actually runnable locally by:

- adding a small build/dev host,
- aligning the manifest URL with that host,
- wiring in authenticated backend calls.

That would turn this from a verified scaffold into a genuinely testable Word add-in.