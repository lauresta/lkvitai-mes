# Agnum Configuration UI (Sprint 7)

## Route
- `/warehouse/agnum/config`

## API Endpoints
- `GET /api/warehouse/v1/agnum/config`
- `PUT /api/warehouse/v1/agnum/config`
- `POST /api/warehouse/v1/agnum/test-connection`

## Supported Settings
- Export scope: `BY_WAREHOUSE`, `BY_CATEGORY`, `BY_LOGICAL_WH`, `TOTAL_ONLY`
- Format: `CSV`, `JSON_API`
- Schedule: cron expression (5 fields)
- API endpoint + API key (masked input in UI)
- Active flag for recurring export

## Mapping Rules
- At least one mapping is required when scope is not `TOTAL_ONLY`.
- Mapping fields:
  - `SourceType`
  - `SourceValue`
  - `AgnumAccountCode`
