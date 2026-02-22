# Agnum Export Job (Sprint 7)

## Job
- Hangfire recurring job id: `agnum-daily-export`
- Default cron: `0 23 * * *` (UTC)
- Manual trigger endpoint: `POST /api/warehouse/v1/agnum/export`

## Data Source
- Preferred source: `AvailableStockView` (on-hand quantity) joined with item master-data and valuation cost (`on_hand_value`).
- Fallback source: `on_hand_value` projection only (when event-store query is unavailable).

## Output
- CSV columns: `ExportDate`, `AccountCode`, `SKU`, `ItemName`, `Quantity`, `UnitCost`, `OnHandValue`
- JSON API payload grouped by account code
- API header for idempotency: `X-Export-ID`

## History APIs
- `GET /api/warehouse/v1/agnum/history`
- `GET /api/warehouse/v1/agnum/history/{exportId}`

## Retry Policy
- Retries up to 3 attempts
- Exponential backoff: 1h, 2h, 4h
