# Cycle Count Execution (Sprint 7)

## API
- `POST /api/warehouse/v1/cycle-counts/{id}/record-count`
- `GET /api/warehouse/v1/cycle-counts/{id}/lines`

## Record Count Request
- `locationCode` (or `locationId`)
- `itemBarcode` (or `itemId`)
- `physicalQty`
- `countedBy` (optional)
- `reason` (optional)

## Behavior
- Resolves location and item from human-scannable identifiers.
- Calculates variance: `PhysicalQty - SystemQty`.
- Flags discrepancy when `abs(variance) > 10` or `abs(variancePercent) > 5`.
- Stores counted metadata (`CountedAt`, `CountedBy`) on cycle count lines.
- Marks cycle count `COMPLETED` automatically when all lines are counted.
