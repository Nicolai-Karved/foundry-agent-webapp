# FS-0002 Retry and Idempotency Guidance

## Scope

This note defines the draft producer and consumer behavior for FS-0002 task sync before the persistence-backed implementation is enabled.

## Sync producer expectations

- Producers must send `schemaVersion = "fs-0002/v1"`.
- Producers should send `X-Correlation-Id` on every request.
- Producers may send `Idempotency-Key` for network retry safety.
- Producers must treat HTTP `202 Accepted` as the success response for accepted async persistence.
- Producers must not retry malformed payloads that fail schema validation until the payload is corrected.

## Idempotency model

The server-side deduplication key is expected to be based on the tuple below:

- `documentId`
- `evaluationRun.evaluationRunId`
- canonical payload hash

`Idempotency-Key` is advisory and should help correlate intentional retries, but the canonical deduplication decision must be based on the persisted request identity rather than trusting the caller alone.

## Draft payload limits

These limits are part of the contract review gate and should be enforced before database writes:

- Maximum request body size: **1 MiB** uncompressed JSON
- Maximum task count per payload: **250**
- Maximum `description` length per task: **4000** characters
- Maximum `citation.text` length per task: **4000** characters
- Maximum `anchor.excerpt` length per task: **2000** characters
- Maximum `resolutionNote` length for overlay updates: **2000** characters

## Version negotiation

- Unsupported schema versions must return `422 Unprocessable Content` with actionable validation detail.
- Minor additive evolution should use a new `schemaVersion` string and preserve existing canonical meanings.
- Renderer-specific requirements must stay inside approved extension namespaces such as `pdf` or `blocksuite` and must not redefine core task fields.

## Retry guidance

### Safe to retry

- `408`, `429`, and `5xx` responses
- connection resets or transport timeouts where the caller does not know whether the request reached the server
- `409` only when the caller first refreshes server state and confirms the retry is still valid

### Do not retry unchanged

- `400` malformed payload
- `401` or `403` auth or authorization failure
- `413` payload too large
- `422` unsupported schema version or semantic validation failure

## Consumer notes

- Snapshot and overlay clients must treat FS-0001-compatible task endpoints as the external compatibility baseline during rollout.
- Canonical snapshot consumers must ignore unknown extension namespaces they do not understand.
- Consumers must never require direct database access or secrets.
