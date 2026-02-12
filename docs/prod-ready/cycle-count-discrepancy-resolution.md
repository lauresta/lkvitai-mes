# Cycle Count Discrepancy Resolution (Sprint 7)

## API
- `GET /api/warehouse/v1/cycle-counts/{id}/discrepancies`
- `POST /api/warehouse/v1/cycle-counts/{id}/approve-adjustment`

## Discrepancy Rules
- A line is a discrepancy when:
  - `abs(variancePercent) > 5`, or
  - `abs(variance) > 10`
- `valueImpact = variance * unitCost`

## Approval Rules
- `lineIds` and `reason` are required.
- If `abs(valueImpact) > 1000`, current user must have `CFO` role.
- Approved lines publish `StockAdjustedEvent` and persist:
  - `AdjustmentApprovedBy`
  - `AdjustmentApprovedAt`
