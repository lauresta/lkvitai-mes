# Production-Ready Warehouse Tasks - Part 3: Epic C - Valuation

**Version:** 1.0  
**Date:** February 10, 2026  
**Source:** prod-ready-universe.md

---

## Epic C Task Index

| TaskId | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|-------|-----|--------------|-----------|------------|
| PRD-0100 | Valuation Domain Model & Events | M | PRD-0001, PRD-0002 | Backend/API | Universe §4.Epic C |
| PRD-0101 | ItemValuation Aggregate Implementation | M | PRD-0100 | Backend/API | Universe §4.Epic C |
| PRD-0102 | Cost Adjustment Command & Handler | M | PRD-0101 | Backend/API | Universe §4.Epic C |
| PRD-0103 | Landed Cost Allocation Logic | L | PRD-0101 | Backend/API | Universe §4.Epic C |
| PRD-0104 | Write-Down Workflow & Approval | M | PRD-0101 | Backend/API | Universe §4.Epic C |
| PRD-0105 | OnHandValue Projection | M | PRD-0101 | Projections | Universe §4.Epic C |
| PRD-0106 | Valuation History Projection | S | PRD-0101 | Projections | Universe §4.Epic C |
| PRD-0107 | COGS Calculation Integration | M | PRD-0101 | Integration | Universe §4.Epic C |
| PRD-0108 | Valuation API Endpoints | M | PRD-0102, PRD-0103, PRD-0104 | Backend/API | Universe §4.Epic C |
| PRD-0109 | Valuation UI - Cost Adjustment Form | M | PRD-0108 | UI | Universe §4.Epic C |
| PRD-0110 | Valuation UI - Landed Cost Allocation | M | PRD-0108 | UI | Universe §4.Epic C |
| PRD-0111 | Valuation UI - Write-Down Approval | M | PRD-0108 | UI | Universe §4.Epic C |
| PRD-0112 | On-Hand Value Report | S | PRD-0105 | UI | Universe §4.Epic C |
| PRD-0113 | Cost Adjustment History Report | S | PRD-0106 | UI | Universe §4.Epic C |
| PRD-0114 | Valuation Security & RBAC | S | PRD-0005 | Backend/API | Universe §4.Epic C |
| PRD-0115 | Valuation Integration Tests | M | PRD-0108 | QA | Universe §4.Epic C |
| PRD-0116 | Valuation Migration & Seed Data | S | PRD-0101 | Infra/DevOps | Universe §4.Epic C |

---

## Task PRD-0100: Valuation Domain Model & Events

**Epic:** C - Valuation  
**Phase:** 1.5  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-0001 (Idempotency), PRD-0002 (Event Versioning)  
**SourceRefs:** Universe §4.Epic C (Entities & Data Model, Events)

### Context

- Phase 1 has no cost tracking (only quantities)
- Need financial interpretation of stock: unit cost, on-hand value, COGS
- Valuation is event-sourced (immutable audit trail for compliance)
- Independent from physical quantity (per Decision 5, baseline doc 04)
- Supports weighted average costing (FIFO/LIFO deferred to Phase 2)

### Scope

**In Scope:**
- Valuation aggregate (event-sourced via Marten)
- Events: ValuationInitialized, CostAdjusted, LandedCostAllocated, StockWrittenDown
- Event schema with versioning (v1)
- Stream naming: `valuation-{itemId}`
- Aggregate state: ItemId, UnitCost, LastAdjustedAt, LastAdjustedBy

**Out of Scope:**
- FIFO/LIFO costing (Phase 2)
- Cost layers (per-receipt costing, Phase 2)
- Multi-currency (USD only)

### Requirements

**Functional:**
1. Valuation aggregate MUST be event-sourced (Marten)
2. Stream per item: `valuation-{itemId}` (1 stream per SKU)
3. Events MUST include: ItemId, OldCost, NewCost, Reason, AdjustedBy, Timestamp
4. Aggregate state: current UnitCost (decimal, 4 decimal places)
5. Initialization: first goods receipt triggers ValuationInitialized
6. Adjustments: manual (accountant) or automatic (landed cost allocation)
7. Write-downs: percentage-based (e.g., 20% reduction)

**Non-Functional:**
1. Event schema versioning: all events include `SchemaVersion: "v1"`
2. Idempotency: CostAdjusted event includes CommandId
3. Audit trail: immutable events, rebuildable projections
4. Precision: decimal(18,4) for unit cost (supports $0.0001 precision)

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
  Guid? ApproverId, // Required if adjustment > threshold
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

### Acceptance Criteria

```gherkin
Scenario: Initialize valuation on first goods receipt
  Given Item "RM-0001" has no valuation stream
  When goods received with supplier price $10.50 per unit
  Then ValuationInitialized event emitted
  And stream created: valuation-rm-0001
  And UnitCost set to $10.50
  And event includes: ItemId, InitialUnitCost, Source="GoodsReceipt", InboundShipmentId

Scenario: Manual cost adjustment
  Given Item "FG-0001" with current UnitCost $25.00
  When accountant adjusts cost to $27.00 with reason "Vendor price increase"
  Then CostAdjusted event emitted
  And event includes: OldUnitCost=$25.00, NewUnitCost=$27.00, Reason, AdjustedBy, CommandId
  And aggregate state updated: UnitCost=$27.00

Scenario: Landed cost allocation
  Given InboundShipment "ISH-001" with 3 items, total freight $500
  When accountant allocates landed cost (EVEN_SPLIT)
  Then LandedCostAllocated event emitted for each item
  And cost per unit = $500 / (total qty) = $1.43
  And each item's UnitCost increased by $1.43

Scenario: Write-down damaged stock
  Given Item "FG-0002" with UnitCost $50.00, Quantity 100
  When finance manager submits write-down: 20%, reason "Damaged"
  Then StockWrittenDown event emitted
  And NewUnitCost = $50.00 * 0.8 = $40.00
  And FinancialImpact = 100 * ($50 - $40) = $1000
  And event includes: ApprovedBy, QuantityAffected, FinancialImpact

Scenario: Event schema versioning
  Given all valuation events
  Then each event MUST include SchemaVersion="v1"
  And future schema changes increment version (v2, v3)
  And event handlers support multiple versions (upcasting)
```

### Implementation Notes

- Use Marten's `IDocumentSession.Events.StartStream<Valuation>()` for stream creation
- Stream ID format: `valuation-{itemId.ToString().ToLower()}`
- Aggregate versioning: Marten handles optimistic concurrency automatically
- Event upcasting: if schema changes, implement `IEventUpcaster<ValuationInitializedV1, ValuationInitializedV2>`

### Validation / Checks

**Local Testing:**
```bash
# Run valuation domain tests
dotnet test --filter "Category=Valuation&Category=Domain"

# Check Marten event store
psql -d warehouse -c "SELECT stream_id, type, data FROM mt_events WHERE stream_id LIKE 'valuation-%' LIMIT 10;"

# Verify event schema
psql -d warehouse -c "SELECT data->>'SchemaVersion' FROM mt_events WHERE type='cost_adjusted';"
```

**Metrics:**
- `valuation_events_total` (counter, labels: event_type)
- `valuation_cost_adjustments_total` (counter)
- `valuation_write_downs_total` (counter)

**Logs:**
- INFO: "Valuation initialized for Item {ItemId}, UnitCost {UnitCost}"
- INFO: "Cost adjusted for Item {ItemId}, Old {OldCost}, New {NewCost}, Reason {Reason}"
- WARN: "Large write-down detected: Item {ItemId}, Impact ${FinancialImpact}"

### Definition of Done

- [ ] Valuation aggregate class created (Valuation.cs)
- [ ] Event records defined (ValuationInitialized, CostAdjusted, LandedCostAllocated, StockWrittenDown)
- [ ] Apply methods implemented (event sourcing)
- [ ] Stream naming convention documented
- [ ] Event schema versioning implemented (SchemaVersion field)
- [ ] Unit tests: 15+ scenarios (initialization, adjustments, write-downs, edge cases)
- [ ] Marten configuration updated (register Valuation aggregate)
- [ ] Event serialization tests (JSON roundtrip)
- [ ] Documentation: ADR-002-valuation-event-sourcing.md
- [ ] Code review completed

---

