# Word Compliance Add-in (FS-0001)

This folder contains a minimal add-in-only Word task pane scaffold for FS-0001.

## Scope of this scaffold

- Add-in-only manifest (production-safe manifest type for Word v1 rollout)
- Task pane HTML/TS shell
- API integration placeholder targeting backend `/api/*` endpoints

## Current endpoint integration targets

- `GET /api/tasks?documentId={id}`
- `PATCH /api/tasks/{taskId}/status`
- `GET /api/tasks/{taskId}/citation-context?documentId={id}`
- `POST /api/verification/rerun`

## Notes

- The existing repository frontend remains unchanged.
- This scaffold is intentionally minimal and reversible.
- Update `SourceLocation` and `AppDomain` values in `manifest.xml` for your environment.
- Keep HTTPS for all task pane and API domains.

## Local testing reality check

- `manifest.xml` currently points to `https://localhost:3000/taskpane.html`.
- The repository's normal frontend dev server runs on `5173`, not `3000`.
- `src/taskpane/taskpane.html` imports `./taskpane.ts` directly, so the scaffold still needs a build/serve step before it can run in a real browser or Word host.
- The task pane scaffold does not yet include Microsoft 365/Entra token acquisition, while backend task endpoints require `Chat.ReadWrite`.

Because of that, the current add-in surface is best treated as a scaffold plus contract/host-behavior reference until a dedicated local build/auth slice is added.

## Current behavior details

- Anchor resolution prefers content control tags and falls back to text search.
- Low-confidence fallback anchors (`confidence < 0.85`) are highlighted with a warning and are not used for auto-comment insertion.
- When fallback rebind succeeds above threshold, anchor metadata is rebound in document-local state for subsequent operations.
- Citation comments include a task marker (`[FS0001:{taskId}]`) and use idempotent signature tracking in memory plus local storage to avoid duplicate inserts for unchanged task/context across add-in reloads.
- When supported by host APIs, existing task-marked comments are updated/replaced in-document (upsert) instead of creating duplicate comments.
- Task status and selected task are persisted in document-local settings and rehydrated on reload.
