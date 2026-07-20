# ADR-002: Valuation Event Sourcing

## Status
Superseded (Phase 1.5 Sprint 7, PRD-1601+). The `Valuation` aggregate and `valuation-{itemId}`
stream convention described below were never removed after Sprint 7 re-implemented the same
feature as `ItemValuation` (stream `valuation-item-{itemId}`, see
`docs/phase15/features/valuation-stream-events.md`) — the WebUI and all live callers use only
the `ItemValuation` path. The `Valuation` aggregate, its `AdjustCostCommand` handler, and the
`LandedCostAllocated`/`StockWrittenDown` events were deleted as dead code; the deterministic
int-Guid `ItemId` conversion helpers they defined were preserved in
`ValuationItemId` (`Domain/Aggregates/ValuationItemId.cs`) since `ItemValuation`'s shared event
contracts still rely on them. This document is kept for historical context only.

Originally accepted (Phase 1.5 Sprint 2, PRD-1511):

## Context
Warehouse stock quantities were already modeled with event-sourced movement/reservation streams, but financial valuation (unit cost and write-down history) had no immutable audit trail.

## Decision
Valuation is implemented as a dedicated Marten self-aggregated stream per item:
- Stream identity format: `valuation-{itemId}` (lowercase)
- Aggregate: `Valuation`
- Event types:
  - `ValuationInitialized`
  - `CostAdjusted`
  - `LandedCostAllocated`
  - `StockWrittenDown`
- Event schema version uses `SchemaVersion = "v1"` via the shared `DomainEvent` contract.

## Consequences
- Cost history is immutable and replayable.
- Projection rebuilds and compliance reporting can rely on event history.
- Optimistic concurrency is delegated to Marten stream version checks.
- FIFO/LIFO layers and multi-currency remain deferred to later phases.
