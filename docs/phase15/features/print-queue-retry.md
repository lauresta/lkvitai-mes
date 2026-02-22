# Print Queue & Retry (Sprint 7)

## Implemented
- Added in-memory print queue domain in `src/LKvitai.MES.Api/Services/LabelPrintQueueServices.cs`:
  - `PrintQueueItem`
  - `PrintQueueStatus` (`PENDING`, `PRINTING`, `COMPLETED`, `FAILED`)
  - `InMemoryLabelPrintQueueStore`
- Added queue processing service:
  - `LabelPrintQueueProcessor`
  - retries pending jobs
  - marks `FAILED` after 10 attempts
- Added recurring Hangfire job:
  - `LabelPrintQueueRecurringJob`
  - registered every 5 minutes in `src/LKvitai.MES.Api/Program.cs`
- Updated print orchestration behavior:
  - when direct printer send fails after retries, print job is enqueued
  - API returns queue id message for manual/operator follow-up

## API
- `GET /api/warehouse/v1/labels/queue`
  - returns pending and failed queue items
- `POST /api/warehouse/v1/labels/queue/{id}/retry`
  - retries a queue item immediately

## Notes
- Queue storage is in-memory for Phase 1.5; persistence can be introduced in Phase 2.
