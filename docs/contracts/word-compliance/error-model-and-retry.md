# FS-0001 Error Model and Retry Guidance

## Error envelope

This API uses RFC 7807 style problem responses (`application/problem+json`) for non-success responses.

Core fields:

- `type`
- `title`
- `status`
- `detail`
- `traceId` (if available)

## Status code expectations

- `400` invalid input or unsupported status transitions.
- `401` missing/invalid delegated user token.
- `404` task/document not found.
- `409` optimistic concurrency conflict (`expectedVersion` mismatch).
- `429` throttled.
- `5xx` transient server/platform failures.

## Retry policy

- **Do not retry automatically**: `400`, `401`, `403`, `404`, `409`.
- **Retry with backoff**: `429`, `500`, `502`, `503`, `504`.
- **Backoff recommendation**: exponential backoff with jitter, max 3 retries.

Example sequence (seconds):

1. attempt 1 immediately
2. attempt 2 after 0.5s + jitter
3. attempt 3 after 1.5s + jitter
4. attempt 4 after 3.5s + jitter

## Concurrency handling for status updates

`PATCH /tasks/{taskId}/status` requires `expectedVersion`.

- On success, response returns incremented `version`.
- On `409`, client should:
  1. refresh tasks for the document,
  2. surface conflict feedback,
  3. let user re-apply status.

## Correlation IDs

- Client should send `X-Correlation-Id` per action/request.
- Backend should echo/log the same ID for audit/trace continuity.
