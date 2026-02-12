# Valuation Stream Events (Sprint 7)

## Stream Convention
- Stream ID format: `valuation-item-{itemId}`
- Aggregate: `ItemValuation`

## Events
- `ValuationInitialized`
- `CostAdjusted`
- `LandedCostApplied`
- `WrittenDown`

All valuation events inherit from `DomainEvent` and carry schema metadata (`Version`, `SchemaVersion`) for event versioning and upcasting compatibility.

## API Endpoints
- `POST /api/warehouse/v1/valuation/initialize`
- `POST /api/warehouse/v1/valuation/adjust-cost`
- `POST /api/warehouse/v1/valuation/apply-landed-cost`
- `POST /api/warehouse/v1/valuation/write-down`
- `GET /api/warehouse/v1/valuation/on-hand-value`
- `GET /api/warehouse/v1/valuation/cost-history`

## UI Routes
- `/warehouse/valuation/dashboard`
- `/warehouse/valuation/adjust-cost`
- `/warehouse/valuation/apply-landed-cost`
- `/warehouse/valuation/write-down`

## Cost Adjustment Rules
- `NewCost >= 0`
- `Reason` minimum length: 10 characters
- `ApprovedBy` required when absolute cost delta is greater than 20%

## Landed Cost Allocation
- Allocation basis: item value (`Qty * UnitCost`) per on-hand row.
- Fallback basis: quantity, then equal weights if values and quantities are zero.
- Rounding: each component (`Freight`, `Duty`, `Insurance`) is rounded to 2 decimals with deterministic remainder assignment to the last row.
- Invariant: sum of allocated values per component equals the input total.

## Write-Down Rules
- `NewValue >= 0`
- `NewValue < CurrentValue`
- `Reason` is required
- For write-down impact above `$1000`, `ApprovedBy` is required and caller must be `WarehouseManager` or `WarehouseAdmin`

## Audit Fields
Audit data is persisted in valuation events:
- actor (`InitializedBy`, `AdjustedBy`, `AppliedBy`, `ApprovedBy`)
- timestamp (`Timestamp`)
- reason (`Reason`)
