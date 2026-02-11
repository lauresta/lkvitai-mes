# ADR-002: Valuation Event Sourcing

## Status
Accepted (Phase 1.5 Sprint 2, PRD-1511)

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
