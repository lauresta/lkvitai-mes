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

## Audit Fields
Audit data is persisted in valuation events:
- actor (`InitializedBy`, `AdjustedBy`, `AppliedBy`, `ApprovedBy`)
- timestamp (`Timestamp`)
- reason (`Reason`)
