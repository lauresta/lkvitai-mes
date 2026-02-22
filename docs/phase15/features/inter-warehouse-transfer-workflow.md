# Inter-Warehouse Transfer Workflow (Sprint 7)

## Implemented
- Transfer workflow state machine aligned to explicit submission flow:
  - `DRAFT -> PENDING_APPROVAL` (SCRAP target)
  - `DRAFT -> APPROVED` (non-SCRAP target on submit)
  - `PENDING_APPROVAL -> APPROVED`
  - `APPROVED -> IN_TRANSIT -> COMPLETED`
- Added transfer submit command and API endpoint:
  - `POST /api/warehouse/v1/transfers/{id}/submit`
- Transfer creation now initializes in `DRAFT`.
- Transfer number format updated to date-sequence:
  - `TRF-yyyyMMdd-###`
- Added transfer metadata:
  - `ExecutedBy`
  - `SubmittedAt`
  - `SubmitCommandId`
- Added transfer line optional `LotId`.
- Execution now requires explicit approved state and stamps `ExecutedBy`.
- Transfer API statuses normalized to underscore format:
  - `PENDING_APPROVAL`
  - `IN_TRANSIT`

## Existing APIs (with workflow updates)
- `POST /api/warehouse/v1/transfers`
- `POST /api/warehouse/v1/transfers/{id}/submit`
- `POST /api/warehouse/v1/transfers/{id}/approve`
- `POST /api/warehouse/v1/transfers/{id}/execute`
- `GET /api/warehouse/v1/transfers`
- `GET /api/warehouse/v1/transfers/{id}`
