# FS-0001 Observability Runbook

## Scope

This runbook covers observability for Word compliance task lifecycle flows:

- `GET /api/tasks`
- `PATCH /api/tasks/{taskId}/status`
- `POST /api/verification/rerun`
- `GET /api/tasks/{taskId}/citation-context`
- `POST /api/telemetry/events`
- Word add-in task actions (load/select/highlight/status/re-verify/comment)

## Correlation strategy

- Client sends `X-Correlation-Id` on each API request.
- Backend echoes `X-Correlation-Id` in responses for task lifecycle endpoints.
- Backend logs include correlation IDs for status updates and rerun actions.
- Add-in emits lightweight telemetry events including correlation IDs (`console.info`).

## Required signals

### Backend

- Request rate by endpoint and status code.
- p95 latency per endpoint.
- Error rate (`4xx`, `5xx`) per endpoint.
- Count of conflict responses (`409`) for status updates.

### Add-in

- Event counts:
  - `api_request_succeeded`
  - `api_request_failed`
- Task action UX statuses:
  - anchor highlighted by quality
  - low-confidence fallback warning
  - comment skipped on low-confidence fallback

## Alert thresholds (initial)

- API `5xx` rate > 2% over 5 minutes.
- API p95 latency > 1.5s over 10 minutes.
- `PATCH /api/tasks/{taskId}/status` conflict ratio > 10% over 15 minutes.
- Re-verify acceptance endpoint unavailable for > 5 minutes.

## Triage workflow

1. Capture failing action and timestamp from user.
2. Locate `X-Correlation-Id` from client/network capture.
3. Query backend logs with correlation ID.
4. Classify failure:
   - auth/permission (`401`/`403`)
   - input/validation (`400`)
   - concurrency (`409`)
   - platform/transient (`429`/`5xx`)
5. Apply mitigation:
   - for `409`: refresh tasks and re-apply status
   - for transient: retry with backoff per `docs/contracts/word-compliance/error-model-and-retry.md`
   - for auth: validate token scope `Chat.ReadWrite`

## Data handling

- Do not log raw document content beyond minimal required snippets.
- Correlation IDs are non-sensitive trace metadata.
- Retain audit/trace data per policy requirements referenced in the FS-0001 spec package.

## Verification checklist

- [ ] Correlation ID appears in request and response headers.
- [ ] Correlation ID appears in backend logs for task actions.
- [ ] Add-in telemetry events are emitted for success/failure paths.
- [ ] Alert thresholds configured in monitoring platform.
- [ ] On-call triage procedure tested with a synthetic failed request.
