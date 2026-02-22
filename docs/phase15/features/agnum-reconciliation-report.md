# Agnum Reconciliation Report (Sprint 7)

## Endpoints
- `POST /api/warehouse/v1/agnum/reconcile` (multipart form: `date`, `agnumBalanceCsv`, optional filters)
- `GET /api/warehouse/v1/agnum/reconcile/{reportId}` (optional filters)

## UI
- Route: `/warehouse/agnum/reconcile`
- Features:
  - date picker (default: yesterday)
  - Agnum balance CSV upload
  - report generation
  - account/variance threshold filters
  - summary cards (total variance, count, largest variance)
  - CSV export

## Calculation
- Warehouse values are loaded from the latest successful Agnum export CSV for the selected date.
- Agnum balances are loaded from uploaded CSV.
- Variance:
  - `Variance = WarehouseValue - AgnumBalance`
  - `VariancePercent = (Variance / AgnumBalance) * 100` (or `100` when balance is zero and warehouse value is non-zero)
