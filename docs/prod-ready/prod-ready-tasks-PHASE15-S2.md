# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 2 (Execution Pack)

**Version:** 1.0  
**Date:** February 10, 2026  
**Sprint:** Phase 1.5 Sprint 2  
**Source:** prod-ready-universe.md  
**Status:** Ready for Execution  
**Baton:** 2026-02-10T15:30:00Z-PHASE15-S1-COMPLETE-a7f3c9d2

---

## Sprint Overview

**Sprint Goal:** Implement financial valuation tracking and Agnum accounting integration for production-ready financial compliance. Add operational excellence features (label printing, 3D visualization).

**Sprint Duration:** 2 weeks  
**Total Tasks:** 10  
**Estimated Effort:** 12 days (M=1d, L=2d, S=0.5d)

**Task Summary:**
- Valuation: 3 tasks (Aggregate + Events, Cost Adjustment, OnHandValue Projection)
- Agnum Integration: 2 tasks (Export Config + Scheduled Job, CSV Generation + API)
- Label Printing: 1 task (ZPL Template + TCP 9100)
- 3D Visualization: 2 tasks (Location Coordinates + Static 3D Model, UI Implementation)
- Inter-Warehouse Transfers: 1 task (Transfer Workflow)
- Cycle Counting: 1 task (Scheduled Counts + Discrepancy Resolution)

**Dependencies from Sprint 1:**
- PRD-1501 (Idempotency) - Required for all commands
- PRD-1502 (Event Versioning) - Required for valuation events
- PRD-1503 (Correlation/Trace) - Required for observability

---

## Task PRD-1511: Valuation - ItemValuation Aggregate + Events

**Epic:** C - Valuation  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1501 (Idempotency), PRD-1502 (Event Versioning)  
**SourceRefs:** Universe §4.Epic C (Entities & Data Model, Events), Universe §5 (Event Sourcing)

### Context

- Phase 1 has no cost tracking (only quantities)
- Need financial interpretation of stock: unit cost, on-hand value, COGS
- Valuation is event-sourced (immutable audit trail for compliance)
- Independent from physical quantity (per Decision 5, baseline doc 04)
- Supports weighted average costing (FIFO/LIFO deferred to Phase 2)
- Critical prerequisite for Agnum integration (export on-hand value)

### Scope

**In Scope:**
- Valuation aggregate (event-sourced via Marten)
- Events: ValuationInitialized, CostAdjusted, LandedCostAllocated, StockWrittenDown
- Event schema with versioning (SchemaVersion="v1")
- Stream naming: `valuation-{itemId}` (1 stream per SKU)
- Aggregate state: ItemId, UnitCost, LastAdjustedAt, LastAdjustedBy, Version
- Apply methods (event sourcing pattern)
- Marten configuration (register aggregate, configure stream)

**Out of Scope:**
- FIFO/LIFO costing (Phase 2)
- Cost layers (per-receipt costing, Phase 2)
- Multi-currency (USD only)
- Commands/handlers (separate task PRD-1512)
- Projections (separate task PRD-1513)

### Requirements

**Functional:**
1. Valuation aggregate MUST be event-sourced (Marten)
2. Stream per item: `valuation-{itemId}` (1 stream per SKU, lowercase)
3. Events MUST include: ItemId, OldCost, NewCost, Reason, AdjustedBy, Timestamp, CommandId, SchemaVersion
4. Aggregate state: current UnitCost (decimal(18,4), 4 decimal places for precision)
5. Initialization: first goods receipt triggers ValuationInitialized
6. Adjustments: manual (accountant) or automatic (landed cost allocation)
7. Write-downs: percentage-based (e.g., 20% reduction for damaged goods)
8. All events MUST be idempotent (include CommandId for deduplication)

**Non-Functional:**
1. Event schema versioning: all events include `SchemaVersion: "v1"`
2. Idempotency: CostAdjusted event includes CommandId (deduplicate via processed_commands)
3. Audit trail: immutable events, rebuildable projections
4. Precision: decimal(18,4) for unit cost (supports $0.0001 precision)
5. Stream naming: lowercase, consistent format (valuation-{guid})
6. Aggregate versioning: Marten handles optimistic concurrency automatically

**Data Model (Event Schemas):**
```csharp
// Event: ValuationInitialized
public record ValuationInitialized(
  Guid ItemId,
  decimal InitialUnitCost,
  string Source, // "GoodsReceipt", "Manual"
  Guid? InboundShipmentId,
  string InitializedBy,
  DateTime InitializedAt,
  string SchemaVersion = "v1"
);

// Event: CostAdjusted
public record CostAdjusted(
  Guid ItemId,
  decimal OldUnitCost,
  decimal NewUnitCost,
  string Reason, // "Vendor Price Increase", "Market Adjustment"
  string AdjustedBy,
  DateTime AdjustedAt,
  Guid? ApproverId, // Required if adjustment > threshold ($1000)
  Guid CommandId, // Idempotency
  string SchemaVersion = "v1"
);

// Event: LandedCostAllocated
public record LandedCostAllocated(
  Guid ItemId,
  decimal OldUnitCost,
  decimal LandedCostPerUnit,
  decimal NewUnitCost, // OldUnitCost + LandedCostPerUnit
  Guid InboundShipmentId,
  string AllocationMethod, // "EVEN_SPLIT", "WEIGHTED_BY_VALUE"
  string AllocatedBy,
  DateTime AllocatedAt,
  Guid CommandId,
  string SchemaVersion = "v1"
);

// Event: StockWrittenDown
public record StockWrittenDown(
  Guid ItemId,
  decimal OldUnitCost,
  decimal WriteDownPercentage, // e.g., 0.20 for 20%
  decimal NewUnitCost, // OldUnitCost * (1 - WriteDownPercentage)
  string Reason, // "Damaged", "Obsolete", "Shrinkage"
  string ApprovedBy, // Manager or CFO
  DateTime ApprovedAt,
  decimal QuantityAffected, // For financial impact calculation
  decimal FinancialImpact, // QuantityAffected * (OldUnitCost - NewUnitCost)
  Guid CommandId,
  string SchemaVersion = "v1"
);
```

**Aggregate State:**
```csharp
public class Valuation : AggregateRoot
{
  public Guid ItemId { get; private set; }
  public decimal UnitCost { get; private set; }
  public DateTime? LastAdjustedAt { get; private set; }
  public string LastAdjustedBy { get; private set; }
  public int Version { get; private set; }

  // Apply methods (event sourcing)
  public void Apply(ValuationInitialized e) {
    ItemId = e.ItemId;
    UnitCost = e.InitialUnitCost;
    LastAdjustedAt = e.InitializedAt;
    LastAdjustedBy = e.InitializedBy;
  }

  public void Apply(CostAdjusted e) {
    UnitCost = e.NewUnitCost;
    LastAdjustedAt = e.AdjustedAt;
    LastAdjustedBy = e.AdjustedBy;
  }

  public void Apply(LandedCostAllocated e) {
    UnitCost = e.NewUnitCost;
    LastAdjustedAt = e.AllocatedAt;
    LastAdjustedBy = e.AllocatedBy;
  }

  public void Apply(StockWrittenDown e) {
    UnitCost = e.NewUnitCost;
    LastAdjustedAt = e.ApprovedAt;
    LastAdjustedBy = e.ApprovedBy;
  }
}
```

**Marten Configuration:**
```csharp
// Program.cs
services.AddMarten(opts => {
  opts.Events.StreamIdentity = StreamIdentity.AsString;
  opts.Events.AddEventType<ValuationInitialized>();
  opts.Events.AddEventType<CostAdjusted>();
  opts.Events.AddEventType<LandedCostAllocated>();
  opts.Events.AddEventType<StockWrittenDown>();
  
  // Register aggregate
  opts.Projections.SelfAggregate<Valuation>(ProjectionLifecycle.Inline);
});
```

### Acceptance Criteria

```gherkin
Scenario: Initialize valuation on first goods receipt
  Given Item "RM-0001" has no valuation stream
  When goods received with supplier price $10.50 per unit
  Then ValuationInitialized event emitted
  And stream created: valuation-{itemId}
  And UnitCost set to $10.50
  And event includes: ItemId, InitialUnitCost, Source="GoodsReceipt", InboundShipmentId, SchemaVersion="v1"

Scenario: Manual cost adjustment
  Given Item "FG-0001" with current UnitCost $25.00
  When accountant adjusts cost to $27.00 with reason "Vendor price increase"
  Then CostAdjusted event emitted
  And event includes: OldUnitCost=$25.00, NewUnitCost=$27.00, Reason, AdjustedBy, CommandId, SchemaVersion="v1"
  And aggregate state updated: UnitCost=$27.00
  And LastAdjustedAt = current timestamp

Scenario: Landed cost allocation
  Given InboundShipment "ISH-001" with 3 items, total freight $500
  When accountant allocates landed cost (EVEN_SPLIT)
  Then LandedCostAllocated event emitted for each item
  And cost per unit = $500 / (total qty) = $1.43
  And each item's UnitCost increased by $1.43
  And event includes: AllocationMethod="EVEN_SPLIT", CommandId

Scenario: Write-down damaged stock
  Given Item "FG-0002" with UnitCost $50.00, Quantity 100
  When finance manager submits write-down: 20%, reason "Damaged"
  Then StockWrittenDown event emitted
  And NewUnitCost = $50.00 * 0.8 = $40.00
  And FinancialImpact = 100 * ($50 - $40) = $1000
  And event includes: ApprovedBy, QuantityAffected, FinancialImpact, SchemaVersion="v1"

Scenario: Event schema versioning
  Given all valuation events
  Then each event MUST include SchemaVersion="v1"
  And future schema changes increment version (v2, v3)
  And event handlers support multiple versions (upcasting)

Scenario: Aggregate versioning (optimistic concurrency)
  Given Valuation aggregate for Item "RM-0003" at version 5
  When two concurrent cost adjustments attempted
  Then first adjustment succeeds (version 5 → 6)
  And second adjustment fails with concurrency exception
  And second adjustment retries with version 6

Scenario: Stream naming convention
  Given Item with Id "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  When ValuationInitialized event emitted
  Then stream created with name: "valuation-a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  And stream name is lowercase
  And stream name follows pattern: valuation-{itemId}
```

### Implementation Notes

- Use Marten's `IDocumentSession.Events.StartStream<Valuation>()` for stream creation
- Stream ID format: `valuation-{itemId.ToString().ToLower()}`
- Aggregate versioning: Marten handles optimistic concurrency automatically (no custom code)
- Event upcasting: if schema changes, implement `IEventUpcaster<ValuationInitializedV1, ValuationInitializedV2>`
- Decimal precision: use decimal(18,4) for all cost fields (4 decimal places)
- CommandId: include in all events for idempotency (deduplicate in command handlers)

### Validation / Checks

**Local Testing:**
```bash
# Run valuation domain tests
dotnet test --filter "Category=Valuation&Category=Domain"

# Check Marten event store
psql -d warehouse -c "SELECT stream_id, type, data FROM mt_events WHERE stream_id LIKE 'valuation-%' LIMIT 10;"

# Verify event schema
psql -d warehouse -c "SELECT data->>'SchemaVersion' FROM mt_events WHERE type='cost_adjusted';"

# Test aggregate versioning (concurrency)
dotnet test --filter "FullyQualifiedName~ValuationConcurrencyTests"
```

**Metrics:**
- `valuation_events_total` (counter, labels: event_type)
- `valuation_cost_adjustments_total` (counter)
- `valuation_write_downs_total` (counter)
- `valuation_aggregate_version_conflicts_total` (counter)

**Logs:**
- INFO: "Valuation initialized for Item {ItemId}, UnitCost {UnitCost}"
- INFO: "Cost adjusted for Item {ItemId}, Old {OldCost}, New {NewCost}, Reason {Reason}"
- WARN: "Large write-down detected: Item {ItemId}, Impact ${FinancialImpact}"
- ERROR: "Valuation aggregate concurrency conflict: Item {ItemId}, Version {Version}"

**Backwards Compatibility:**
- New aggregate, no breaking changes
- New event types (ValuationInitialized, CostAdjusted, LandedCostAllocated, StockWrittenDown)
- No impact on existing aggregates (StockLedger, Reservation)

### Definition of Done

- [ ] Valuation aggregate class created (Valuation.cs)
- [ ] Event records defined (ValuationInitialized, CostAdjusted, LandedCostAllocated, StockWrittenDown)
- [ ] Apply methods implemented (event sourcing pattern)
- [ ] Stream naming convention documented (valuation-{itemId})
- [ ] Event schema versioning implemented (SchemaVersion field in all events)
- [ ] Marten configuration updated (register Valuation aggregate, add event types)
- [ ] Unit tests: 20+ scenarios (initialization, adjustments, write-downs, edge cases, concurrency)
- [ ] Event serialization tests (JSON roundtrip, schema validation)
- [ ] Aggregate versioning tests (optimistic concurrency, conflict resolution)
- [ ] Documentation: ADR-002-valuation-event-sourcing.md
- [ ] Code review completed
- [ ] Manual testing: create valuation stream, emit events, verify aggregate state

---

