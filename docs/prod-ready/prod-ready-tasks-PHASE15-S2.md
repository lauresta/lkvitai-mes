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

## Task PRD-1512: Valuation - Cost Adjustment Command + Handler

**Epic:** C - Valuation  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1511 (Valuation Aggregate)  
**SourceRefs:** Universe §4.Epic C (Commands/APIs, Approval Workflow)

### Context

- PRD-1511 created Valuation aggregate with event sourcing
- Need command to manually adjust item cost (revaluation)
- Approval workflow: Manager approval for adjustments > $1000, CFO approval for > $10,000
- Use cases: vendor price changes, market revaluation, correction of data entry errors
- CostAdjusted event emitted for downstream consumers (OnHandValue projection, Agnum export)

### Scope

**In Scope:**
- AdjustCostCommand + handler (validate, load aggregate, emit event, update state)
- API endpoint: POST /api/warehouse/v1/valuation/{itemId}/adjust-cost
- Validation: newCost > 0, reason required (min 10 chars), approver required if impact > $1000
- Approval workflow: calculate impact (qty × cost delta), require Manager/CFO approval
- Emit CostAdjusted event
- Update Valuation aggregate state (UnitCost)
- Idempotency (CommandId)

**Out of Scope:**
- Bulk cost adjustments (adjust multiple items at once, deferred to Phase 2)
- Automated cost adjustments (e.g., from supplier price feed)
- Cost approval UI (separate task)

### Requirements

**Functional:**
1. POST /api/warehouse/v1/valuation/{itemId}/adjust-cost: Adjust item cost
2. Request includes: CommandId, NewUnitCost, Reason, ApproverId (optional)
3. Validate: NewUnitCost > 0, Reason min 10 chars, ApproverId required if impact > $1000
4. Calculate impact: (NewUnitCost - OldUnitCost) × AvailableQty
5. Approval rules:
   - Impact ≤ $1000: no approval required
   - Impact > $1000 and ≤ $10,000: Manager approval required
   - Impact > $10,000: CFO approval required
6. Load Valuation aggregate from event stream (valuation-{itemId})
7. Call aggregate.AdjustCost(newCost, reason, approverId)
8. Emit CostAdjusted event
9. Return updated valuation (itemId, oldCost, newCost, impact)

**Non-Functional:**
1. API latency: < 1 second (95th percentile)
2. Idempotency: duplicate CommandId returns cached result
3. Validation errors: return 400 Bad Request with detailed error messages
4. Authorization: Inventory Accountant role required
5. Transactional: event append + projection update in single transaction

**Data Model (DTOs):**
```csharp
// Request DTO
public record AdjustCostRequest(
  Guid CommandId,
  decimal NewUnitCost,
  string Reason,
  Guid? ApproverId // Optional: required if impact > $1000
);

// Response DTO
public record AdjustCostResponse(
  Guid ItemId,
  string ItemSku,
  decimal OldUnitCost,
  decimal NewUnitCost,
  decimal CostDelta,
  decimal AvailableQty,
  decimal Impact, // CostDelta × AvailableQty
  string Reason,
  string ApprovedBy,
  DateTime AdjustedAt
);
```

**API Contract:**
```
POST /api/warehouse/v1/valuation/{itemId}/adjust-cost
Request: AdjustCostRequest
Response: 200 OK, AdjustCostResponse
Errors:
  - 400 Bad Request: validation failure (newCost ≤ 0, reason too short, missing approver)
  - 403 Forbidden: approver not Manager/CFO role
  - 404 Not Found: item not found
  - 409 Conflict: duplicate CommandId (idempotency check)
```

**Events:**
```csharp
public record CostAdjusted(
  Guid ItemId,
  decimal OldCost,
  decimal NewCost,
  string Reason,
  string ApprovedBy,
  DateTime Timestamp,
  string SchemaVersion = "v1"
);
```

### Acceptance Criteria

```gherkin
Scenario: Adjust cost without approval (impact ≤ $1000)
  Given Item "RM-0001" with UnitCost $10.00, AvailableQty 50
  When POST /api/warehouse/v1/valuation/RM-0001/adjust-cost with:
    | newUnitCost | 12.00 |
    | reason | "Vendor price increase" |
  Then response status: 200 OK
  And response body includes: oldCost 10.00, newCost 12.00, impact 100.00 (50 × 2.00)
  And CostAdjusted event emitted
  And Valuation.UnitCost updated to 12.00

Scenario: Adjust cost requires Manager approval (impact > $1000)
  Given Item "FG-0001" with UnitCost $50.00, AvailableQty 100
  When POST /api/warehouse/v1/valuation/FG-0001/adjust-cost with:
    | newUnitCost | 65.00 |
    | reason | "Market revaluation" |
    | approverId | null |
  Then response status: 400 Bad Request
  And error message: "Manager approval required for adjustments > $1000 (impact: $1500)"
  And NO event emitted

Scenario: Adjust cost with Manager approval
  Given Item "FG-0001" with UnitCost $50.00, AvailableQty 100
  And user "manager-001" has Manager role
  When POST /api/warehouse/v1/valuation/FG-0001/adjust-cost with:
    | newUnitCost | 65.00 |
    | reason | "Market revaluation" |
    | approverId | manager-001 |
  Then response status: 200 OK
  And response body includes: approvedBy "manager-001", impact 1500.00
  And CostAdjusted event emitted

Scenario: Adjust cost requires CFO approval (impact > $10,000)
  Given Item "FG-0002" with UnitCost $100.00, AvailableQty 200
  And user "manager-001" has Manager role (not CFO)
  When POST /api/warehouse/v1/valuation/FG-0002/adjust-cost with:
    | newUnitCost | 150.00 |
    | approverId | manager-001 |
  Then response status: 403 Forbidden
  And error message: "CFO approval required for adjustments > $10,000 (impact: $10,000)"
  And NO event emitted

Scenario: Validation failure - negative cost
  When POST /api/warehouse/v1/valuation/RM-0001/adjust-cost with newUnitCost -5.00
  Then response status: 400 Bad Request
  And error message: "NewUnitCost must be greater than 0"

Scenario: Validation failure - reason too short
  When POST /api/warehouse/v1/valuation/RM-0001/adjust-cost with reason "test"
  Then response status: 400 Bad Request
  And error message: "Reason must be at least 10 characters"

Scenario: Idempotency - duplicate adjustment
  Given cost adjustment already processed with CommandId "cmd-789"
  When POST /api/warehouse/v1/valuation/RM-0001/adjust-cost with same CommandId "cmd-789"
  Then response status: 200 OK
  And response body includes: cached result
  And NO new event emitted
  And response header: X-Idempotent-Replay: true
```

### Implementation Notes

- Use MediatR for AdjustCostCommandHandler
- Load Valuation aggregate from Marten event stream (valuation-{itemId})
- Query AvailableStock projection to get current qty (for impact calculation)
- Approval validation: query user roles (Manager, CFO) from identity system
- Event publishing: Marten auto-publishes events to MassTransit
- Idempotency: use processed_commands table (from PRD-1501)

### Validation / Checks

**Local Testing:**
```bash
# Adjust cost (no approval)
curl -X POST http://localhost:5000/api/warehouse/v1/valuation/<itemId>/adjust-cost \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "commandId": "test-adjust-001",
    "newUnitCost": 12.50,
    "reason": "Vendor price increase effective 2026-02-01"
  }'

# Adjust cost (with approval)
curl -X POST http://localhost:5000/api/warehouse/v1/valuation/<itemId>/adjust-cost \
  -d '{
    "commandId": "test-adjust-002",
    "newUnitCost": 75.00,
    "reason": "Market revaluation per CFO directive",
    "approverId": "<manager-guid>"
  }'

# Verify event emitted
psql -d warehouse -c "SELECT * FROM mt_events WHERE type = 'CostAdjusted' ORDER BY timestamp DESC LIMIT 5;"

# Run tests
dotnet test --filter "Category=Valuation"
```

**Metrics:**
- `cost_adjustments_total` (counter, labels: approval_required)
- `cost_adjustment_impact_dollars` (histogram)
- `cost_adjustment_duration_ms` (histogram)
- `cost_adjustment_errors_total` (counter, labels: error_type)

**Logs:**
- INFO: "Cost adjustment: Item {ItemSku}, OldCost {OldCost}, NewCost {NewCost}, Impact {Impact}, ApprovedBy {ApproverId}"
- WARN: "Cost adjustment requires approval: Impact {Impact} > threshold {Threshold}"
- ERROR: "Cost adjustment failed: {Exception}"

**Backwards Compatibility:**
- New API endpoint, no breaking changes
- New event (CostAdjusted), consumed by OnHandValue projection (PRD-1513)

### Definition of Done

- [ ] AdjustCostCommand + handler implemented
- [ ] ValuationController created with POST /adjust-cost endpoint
- [ ] DTOs defined (AdjustCostRequest, AdjustCostResponse)
- [ ] Validation logic implemented (cost > 0, reason length, approval rules)
- [ ] Impact calculation implemented (query AvailableStock projection)
- [ ] Approval validation implemented (check user roles)
- [ ] Valuation aggregate loaded from event stream
- [ ] CostAdjusted event emitted
- [ ] Idempotency middleware applied (CommandId check)
- [ ] Authorization policy applied (Inventory Accountant role)
- [ ] Unit tests: 15+ scenarios (adjust success, approval required, validation failures, idempotency)
- [ ] Integration tests: end-to-end cost adjustment (API → event → projection)
- [ ] Metrics exposed (counters, histograms)
- [ ] Logs added (INFO, WARN, ERROR with correlation IDs)
- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] Code review completed
- [ ] Manual testing: Postman collection with adjust-cost endpoint

---
## Task PRD-1513: Valuation - OnHandValue Projection

**Epic:** C - Valuation  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1511 (Valuation Aggregate), PRD-1512 (Cost Adjustment)  
**SourceRefs:** Universe §4.Epic C (Projections, On-Hand Value Report)

### Context

- PRD-1511 and PRD-1512 created Valuation aggregate and cost adjustment workflow
- Need read-optimized projection for on-hand value (qty × cost)
- OnHandValue projection consumes Valuation events and StockMoved events
- Used for financial reporting, Agnum export, balance sheet
- Calculation: OnHandValue = AvailableStock.Qty × ItemValuation.UnitCost

### Scope

**In Scope:**
- OnHandValue projection (table: on_hand_value)
- Projection handler: consume ValuationInitialized, CostAdjusted, LandedCostAllocated, StockWrittenDown, StockMoved events
- Query endpoint: GET /api/warehouse/v1/valuation/on-hand-value (filters: category, location, date range)
- Projection rebuild support (replay events from event store)
- Projection lag monitoring (metrics)
- Reconciliation report (compare warehouse balance vs expected)

**Out of Scope:**
- Real-time updates (projection updated within 5 seconds, not real-time)
- Historical on-hand value (point-in-time queries, deferred to Phase 2)
- Multi-currency on-hand value

### Requirements

**Functional:**
1. OnHandValue projection: ItemId, SKU, Qty, UnitCost, TotalValue, LastUpdated
2. Event handlers:
   - ValuationInitialized: insert row with initial cost
   - CostAdjusted: update UnitCost, recalculate TotalValue
   - StockMoved: update Qty (from AvailableStock projection), recalculate TotalValue
3. Calculation: TotalValue = Qty × UnitCost
4. Query endpoint: GET /on-hand-value (filters: categoryId, locationId, dateFrom, dateTo)
5. Projection rebuild: replay events from event store (for schema changes or data fixes)
6. Projection lag: < 5 seconds (95th percentile)

**Non-Functional:**
1. Query latency: < 100ms (95th percentile, indexed queries)
2. Projection update latency: < 500ms per event
3. Projection rebuild time: < 5 minutes for 100k events
4. Idempotency: event handlers check event number (skip duplicates)
5. Error handling: failed events logged, retried (with exponential backoff)

**Data Model (Projection):**
```csharp
// OnHandValue projection (denormalized, read-optimized)
public class OnHandValue
{
  public Guid Id { get; set; }
  public Guid ItemId { get; set; }
  public string ItemSku { get; set; }
  public string ItemName { get; set; }
  public Guid? CategoryId { get; set; }
  public string CategoryName { get; set; }
  public decimal Qty { get; set; } // From AvailableStock projection
  public decimal UnitCost { get; set; } // From Valuation aggregate
  public decimal TotalValue { get; set; } // Qty × UnitCost
  public DateTime LastUpdated { get; set; }
}
```

**Database Schema:**
```sql
CREATE TABLE on_hand_value (
  id UUID PRIMARY KEY,
  item_id UUID NOT NULL UNIQUE,
  item_sku VARCHAR(100) NOT NULL,
  item_name VARCHAR(200) NOT NULL,
  category_id UUID,
  category_name VARCHAR(200),
  qty DECIMAL(18,3) NOT NULL,
  unit_cost DECIMAL(18,2) NOT NULL,
  total_value DECIMAL(18,2) NOT NULL,
  last_updated TIMESTAMPTZ NOT NULL,
  INDEX idx_on_hand_value_category_id (category_id),
  INDEX idx_on_hand_value_total_value (total_value),
  INDEX idx_on_hand_value_last_updated (last_updated)
);
```

**Event Handlers:**
```csharp
// OnHandValue projection handler
public class OnHandValueProjection :
  IConsumer<ValuationInitialized>,
  IConsumer<CostAdjusted>,
  IConsumer<StockMoved>
{
  public async Task Consume(ConsumeContext<ValuationInitialized> context)
  {
    var item = await _dbContext.Items.FindAsync(context.Message.ItemId);
    var availableStock = await _dbContext.AvailableStocks.FirstOrDefaultAsync(x => x.ItemId == context.Message.ItemId);
    
    var onHandValue = new OnHandValue
    {
      Id = Guid.NewGuid(),
      ItemId = context.Message.ItemId,
      ItemSku = item.SKU,
      ItemName = item.Name,
      CategoryId = item.CategoryId,
      CategoryName = item.Category?.Name,
      Qty = availableStock?.Qty ?? 0,
      UnitCost = context.Message.InitialCost,
      TotalValue = (availableStock?.Qty ?? 0) * context.Message.InitialCost,
      LastUpdated = DateTime.UtcNow
    };
    await _dbContext.OnHandValues.AddAsync(onHandValue);
    await _dbContext.SaveChangesAsync();
  }

  public async Task Consume(ConsumeContext<CostAdjusted> context)
  {
    var onHandValue = await _dbContext.OnHandValues.FirstOrDefaultAsync(x => x.ItemId == context.Message.ItemId);
    if (onHandValue != null)
    {
      onHandValue.UnitCost = context.Message.NewCost;
      onHandValue.TotalValue = onHandValue.Qty * context.Message.NewCost;
      onHandValue.LastUpdated = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync();
    }
  }

  public async Task Consume(ConsumeContext<StockMoved> context)
  {
    // Update qty from AvailableStock projection
    var availableStock = await _dbContext.AvailableStocks.FirstOrDefaultAsync(x => x.ItemId == context.Message.ItemId);
    var onHandValue = await _dbContext.OnHandValues.FirstOrDefaultAsync(x => x.ItemId == context.Message.ItemId);
    if (onHandValue != null && availableStock != null)
    {
      onHandValue.Qty = availableStock.Qty;
      onHandValue.TotalValue = availableStock.Qty * onHandValue.UnitCost;
      onHandValue.LastUpdated = DateTime.UtcNow;
      await _dbContext.SaveChangesAsync();
    }
  }
}
```

**Query Endpoint:**
```
GET /api/warehouse/v1/valuation/on-hand-value?categoryId={guid}&locationId={guid}&dateFrom=2026-01-01&dateTo=2026-12-31
Response: 200 OK, OnHandValue[]
```

### Acceptance Criteria

```gherkin
Scenario: OnHandValue projection created on ValuationInitialized
  Given Item "RM-0001" with SKU "RM-0001", Name "Raw Material A"
  And AvailableStock: Qty 100
  And ValuationInitialized event emitted with InitialCost 10.00
  When projection handler consumes event
  Then new row inserted in on_hand_value table
  And row includes: itemSku "RM-0001", qty 100, unitCost 10.00, totalValue 1000.00

Scenario: OnHandValue projection updated on CostAdjusted
  Given OnHandValue row exists for Item "RM-0001" with Qty 100, UnitCost 10.00, TotalValue 1000.00
  And CostAdjusted event emitted with NewCost 12.00
  When projection handler consumes event
  Then on_hand_value row updated: unitCost 12.00, totalValue 1200.00 (100 × 12.00)

Scenario: OnHandValue projection updated on StockMoved
  Given OnHandValue row exists for Item "RM-0001" with Qty 100, UnitCost 10.00, TotalValue 1000.00
  And StockMoved event emitted (qty changed to 150)
  When projection handler consumes event
  Then on_hand_value row updated: qty 150, totalValue 1500.00 (150 × 10.00)

Scenario: Query on-hand value by category
  Given on_hand_value table has 3 rows:
    | ItemSku | CategoryName | Qty | UnitCost | TotalValue |
    | RM-0001 | Raw Materials | 100 | 10.00 | 1000.00 |
    | FG-0001 | Finished Goods | 50 | 50.00 | 2500.00 |
    | FG-0002 | Finished Goods | 30 | 75.00 | 2250.00 |
  When GET /api/warehouse/v1/valuation/on-hand-value?categoryName=Finished Goods
  Then response includes 2 rows: FG-0001, FG-0002
  And total value sum: 4750.00

Scenario: Projection rebuild
  Given on_hand_value table is empty (or corrupted)
  When projection rebuild triggered
  Then replay all ValuationInitialized, CostAdjusted, StockMoved events
  And on_hand_value table repopulated with correct data
  And rebuild completes in < 5 minutes for 100k events

Scenario: Projection lag monitoring
  Given CostAdjusted event emitted at T0
  When projection handler consumes event at T1
  Then projection lag = T1 - T0
  And lag metric recorded (histogram)
  And if lag > 5 seconds → alert triggered
```

### Implementation Notes

- Use MassTransit consumers for event handlers (IConsumer<TEvent>)
- Query AvailableStock projection to get current qty (join with on_hand_value)
- Projection rebuild: use Marten projection rebuild API (replay events from event store)
- Projection lag: measure time between event timestamp and projection update timestamp
- Error handling: if projection update fails, log error and retry (MassTransit retry policy)

### Validation / Checks

**Local Testing:**
```bash
# Emit test event (via API or test harness)
curl -X POST http://localhost:5000/api/warehouse/v1/valuation/<itemId>/adjust-cost

# Verify projection updated
psql -d warehouse -c "SELECT * FROM on_hand_value WHERE item_sku = 'RM-0001';"

# Query projection endpoint
curl http://localhost:5000/api/warehouse/v1/valuation/on-hand-value?categoryName=Raw%20Materials

# Run tests
dotnet test --filter "Category=OnHandValueProjection"
```

**Metrics:**
- `on_hand_value_projection_updates_total` (counter, labels: event_type)
- `on_hand_value_projection_update_duration_ms` (histogram)
- `on_hand_value_projection_lag_seconds` (histogram)
- `on_hand_value_projection_errors_total` (counter, labels: error_type)
- `on_hand_value_total_dollars` (gauge) - total on-hand value across all items

**Logs:**
- INFO: "OnHandValue projection updated for Item {ItemSku}, TotalValue {TotalValue}"
- WARN: "OnHandValue projection lag high: {LagSeconds}s"
- ERROR: "OnHandValue projection update failed: {Exception}"

**Backwards Compatibility:**
- New projection table, no breaking changes
- New query endpoint, no impact on existing APIs

### Definition of Done

- [ ] OnHandValue projection class created
- [ ] Event handlers implemented (OnHandValueProjection: ValuationInitialized, CostAdjusted, StockMoved)
- [ ] MassTransit consumers registered in Program.cs
- [ ] Projection table created (migration: AddOnHandValueProjection)
- [ ] Query endpoint implemented (GET /on-hand-value)
- [ ] Projection rebuild support implemented (replay events)
- [ ] Unit tests: 15+ scenarios (projection updates, event handling, idempotency, rebuild)
- [ ] Integration tests: end-to-end event → projection update
- [ ] Metrics exposed (counters, histograms for lag, gauge for total value)
- [ ] Logs added (INFO, WARN, ERROR with correlation IDs)
- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] Code review completed
- [ ] Manual testing: emit events, verify projections updated, query endpoint

---
## Task PRD-1514: Agnum Integration - Export Configuration + Scheduled Job

**Epic:** D - Agnum Integration  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** L (2 days)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1513 (OnHandValue Projection)  
**SourceRefs:** Universe §4.Epic D (Agnum Integration)

### Context

- PRD-1513 created OnHandValue projection (qty × cost)
- Need scheduled daily export to Agnum accounting system for GL posting
- Export configuration: mappings (warehouse/category → Agnum account codes)
- Scheduled job: Hangfire recurring job (daily 23:00 UTC, configurable cron)
- Export saga: query data, apply mappings, generate file, send, record history
- Retry logic: 3x with exponential backoff, manual fallback

### Scope

**In Scope:**
- AgnumExportConfig entity (schedule, format, API endpoint, mappings)
- AgnumMapping entity (source type/value → Agnum account code)
- AgnumExportHistory entity (audit log: export number, status, row count, file path)
- Scheduled job: Hangfire recurring job (configurable cron)
- AgnumExportSaga: query OnHandValue, apply mappings, generate CSV/JSON, send, record history
- API endpoints: GET/PUT /agnum/config, POST /agnum/export (manual trigger), GET /agnum/history
- Retry logic: 3x with exponential backoff (1h, 2h, 4h)
- Events: AgnumExportStarted, AgnumExportCompleted, AgnumExportFailed

**Out of Scope:**
- Real-time sync (batch daily is sufficient)
- Two-way sync (Agnum → Warehouse updates)
- Reconciliation report (separate task)

### Requirements

**Functional:**
1. AgnumExportConfig: ExportScope (BY_WAREHOUSE, BY_CATEGORY, TOTAL_ONLY), Schedule (cron), Format (CSV, JSON_API), ApiEndpoint, ApiKey, Mappings, IsActive
2. AgnumMapping: SourceType (WAREHOUSE, CATEGORY, LOGICAL_WH), SourceValue, AgnumAccountCode
3. Scheduled job: Hangfire recurring job, trigger at configured time (default: 23:00 UTC)
4. AgnumExportSaga steps:
   - Query OnHandValue projection (join with Item, Category, LogicalWarehouse)
   - Apply mappings: group by Agnum account code
   - Generate file: CSV or JSON payload
   - Send: write to blob storage OR POST to Agnum API
   - Record history: insert AgnumExportHistory (status, row count, file path)
   - Notify: email accountant on success/failure
5. Retry logic: if API fails, retry 3x with exponential backoff (1h, 2h, 4h), then mark FAILED
6. Manual trigger: POST /agnum/export (for testing or ad-hoc exports)

**Non-Functional:**
1. Export duration: < 5 minutes for 10k items
2. Scheduled job reliability: 99.9% (Hangfire persistent storage)
3. API key encryption: use Data Protection API or Azure Key Vault
4. File storage: Azure Blob Storage or local file system (configurable)
5. Idempotency: export ID included in API call header (X-Export-ID)

**Data Model:**
```csharp
public class AgnumExportConfig
{
  public Guid Id { get; set; }
  public ExportScope Scope { get; set; }
  public string Schedule { get; set; } // Cron: "0 23 * * *"
  public ExportFormat Format { get; set; }
  public string ApiEndpoint { get; set; }
  public string ApiKey { get; set; } // Encrypted
  public List<AgnumMapping> Mappings { get; set; }
  public bool IsActive { get; set; }
}

public class AgnumMapping
{
  public Guid Id { get; set; }
  public Guid AgnumExportConfigId { get; set; }
  public string SourceType { get; set; }
  public string SourceValue { get; set; }
  public string AgnumAccountCode { get; set; }
}

public class AgnumExportHistory
{
  public Guid Id { get; set; }
  public string ExportNumber { get; set; }
  public DateTime ExportedAt { get; set; }
  public ExportStatus Status { get; set; }
  public int RowCount { get; set; }
  public string FilePath { get; set; }
  public string ErrorMessage { get; set; }
  public int RetryCount { get; set; }
}

public enum ExportScope { BY_WAREHOUSE, BY_CATEGORY, BY_LOGICAL_WH, TOTAL_ONLY }
public enum ExportFormat { CSV, JSON_API }
public enum ExportStatus { SUCCESS, FAILED, RETRYING }
```

### Acceptance Criteria

```gherkin
Scenario: Daily scheduled export
  Given export config with schedule "0 23 * * *", scope BY_WAREHOUSE
  And mapping: Main → 1500-RAW-MAIN
  When scheduler triggers at 23:00
  Then AgnumExportSaga queries OnHandValue projection
  And groups by Agnum account code
  And generates CSV with 150 rows
  And saves to blob storage
  And creates AgnumExportHistory (status: SUCCESS, rowCount: 150)
  And emails accountant: "Agnum export completed: 150 rows"

Scenario: Manual export trigger
  Given export config exists
  When POST /api/warehouse/v1/agnum/export
  Then export saga triggered immediately
  And export completes within 5 minutes
  And response includes: exportNumber, status, rowCount

Scenario: API integration failure with retry
  Given export config with format JSON_API
  When export triggered
  And Agnum API returns 503 Service Unavailable
  Then saga retries after 1 hour
  And retries after 2 hours
  And retries after 4 hours
  And if still fails marks FAILED
  And alerts admin: "Agnum export failed after 3 retries"

Scenario: CSV format generation
  Given export config with format CSV
  When export triggered
  Then CSV generated with columns: ExportDate, AccountCode, SKU, ItemName, Quantity, UnitCost, OnHandValue
  And CSV saved to blob storage: /agnum-exports/2026-02-10/export-001.csv

Scenario: Mapping configuration
  Given admin navigates to /warehouse/agnum/config
  When admin adds mapping: Category "Raw Materials" → Account "1500-RAW"
  And saves config
  Then mapping stored in agnum_mappings table
  And next export uses new mapping
```

### Implementation Notes

- Use Hangfire for scheduled jobs (persistent storage: SQL Server or PostgreSQL)
- Use MassTransit saga for AgnumExportSaga (state machine: Started → Processing → Completed/Failed)
- Query OnHandValue projection with joins (Item, Category, LogicalWarehouse)
- CSV generation: use CsvHelper library
- Blob storage: use Azure.Storage.Blobs SDK or local file system
- API key encryption: use ASP.NET Core Data Protection API
- Retry logic: use Polly library (exponential backoff)

### Validation / Checks

**Local Testing:**
```bash
# Configure export
curl -X PUT http://localhost:5000/api/warehouse/v1/agnum/config \
  -d '{ "schedule": "0 23 * * *", "format": "CSV", "mappings": [...] }'

# Manual trigger
curl -X POST http://localhost:5000/api/warehouse/v1/agnum/export

# View history
curl http://localhost:5000/api/warehouse/v1/agnum/history

# Verify Hangfire job
# Open http://localhost:5000/hangfire, check recurring jobs

# Run tests
dotnet test --filter "Category=AgnumExport"
```

**Metrics:**
- `agnum_exports_total` (counter, labels: status)
- `agnum_export_duration_ms` (histogram)
- `agnum_export_row_count` (histogram)
- `agnum_export_errors_total` (counter, labels: error_type)

**Logs:**
- INFO: "Agnum export started: ExportNumber {ExportNumber}"
- INFO: "Agnum export completed: {RowCount} rows, FilePath {FilePath}"
- WARN: "Agnum export retry {RetryCount}/3: {ErrorMessage}"
- ERROR: "Agnum export failed: {Exception}"

### Definition of Done

- [ ] AgnumExportConfig entity created
- [ ] AgnumMapping entity created
- [ ] AgnumExportHistory entity created
- [ ] Database migration created (AddAgnumExportTables)
- [ ] Hangfire recurring job configured
- [ ] AgnumExportSaga implemented (MassTransit saga)
- [ ] CSV generation implemented (CsvHelper)
- [ ] Blob storage integration implemented
- [ ] Agnum API client implemented (with retry logic)
- [ ] API endpoints implemented (GET/PUT /config, POST /export, GET /history)
- [ ] Events defined and published (AgnumExportStarted, Completed, Failed)
- [ ] Unit tests: 15+ scenarios
- [ ] Integration tests: end-to-end export workflow
- [ ] Metrics exposed
- [ ] Logs added
- [ ] API documentation updated
- [ ] Code review completed
- [ ] Manual testing: configure, trigger, verify export

---
## Task PRD-1515: Agnum Integration - CSV Generation + API Integration

**Epic:** D - Agnum Integration  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1514 (Agnum Export Config)  
**SourceRefs:** Universe §4.Epic D (CSV Format, API Integration)

### Context

- PRD-1514 created export configuration and scheduled job
- Need CSV generation logic and Agnum API client
- CSV format: ExportDate, AccountCode, SKU, ItemName, Quantity, UnitCost, OnHandValue
- API integration: POST to Agnum REST endpoint with idempotency header
- Export history tracking for audit and reconciliation

### Scope

**In Scope:**
- CSV generation: query OnHandValue, apply mappings, format CSV
- Agnum API client: POST /api/v1/inventory/import with JSON payload
- Idempotency: include X-Export-ID header
- Export history: record success/failure, row count, file path
- Manual fallback: download CSV if API fails

**Out of Scope:**
- Reconciliation report (separate task)
- Multi-format exports (only CSV and JSON for Phase 1.5)

### Requirements

**Functional:**
1. CSV generation:
   - Query: SELECT item_sku, item_name, category_name, qty, unit_cost, total_value FROM on_hand_value
   - Apply mappings: join with agnum_mappings (category → account code)
   - Group by account code (if scope = BY_CATEGORY)
   - Format CSV: ExportDate, AccountCode, SKU, ItemName, Quantity, UnitCost, OnHandValue
   - Save to blob storage: /agnum-exports/{date}/export-{number}.csv
2. Agnum API client:
   - Endpoint: POST {ApiEndpoint}/api/v1/inventory/import
   - Headers: Content-Type: application/json, X-Export-ID: {exportId}, Authorization: Bearer {apiKey}
   - Payload: { exportDate, accountCode, items: [{ sku, qty, cost, value }] }
   - Response: 200 OK (success), 4xx/5xx (error)
3. Idempotency: Agnum API deduplicates by X-Export-ID
4. Export history: insert AgnumExportHistory (exportNumber, status, rowCount, filePath, errorMessage)

**Non-Functional:**
1. CSV generation: < 2 minutes for 10k items
2. API call latency: < 10 seconds (with retry)
3. File size: < 10MB (compress if larger)
4. Error handling: log detailed error messages, retry 3x

**CSV Format Example:**
```csv
ExportDate,AccountCode,SKU,ItemName,Quantity,UnitCost,OnHandValue
2026-02-10,1500-RAW-MAIN,RM-0001,Bolt M8,500,10.50,5250.00
2026-02-10,1500-RAW-MAIN,RM-0002,Nut M8,1000,0.25,250.00
2026-02-10,1510-FG,FG-0001,Widget A,200,45.00,9000.00
```

**API Payload Example:**
```json
{
  "exportDate": "2026-02-10",
  "accountCode": "1500-RAW-MAIN",
  "items": [
    { "sku": "RM-0001", "name": "Bolt M8", "qty": 500, "cost": 10.50, "value": 5250.00 },
    { "sku": "RM-0002", "name": "Nut M8", "qty": 1000, "cost": 0.25, "value": 250.00 }
  ]
}
```

### Acceptance Criteria

```gherkin
Scenario: CSV generation with mappings
  Given OnHandValue projection has 3 items:
    | SKU | Category | Qty | UnitCost | TotalValue |
    | RM-0001 | Raw Materials | 100 | 10.00 | 1000.00 |
    | FG-0001 | Finished Goods | 50 | 50.00 | 2500.00 |
    | FG-0002 | Finished Goods | 30 | 75.00 | 2250.00 |
  And mappings: Raw Materials → 1500-RAW, Finished Goods → 1510-FG
  When CSV generated
  Then CSV includes 3 rows with correct account codes
  And CSV saved to blob storage

Scenario: Agnum API integration success
  Given export config with format JSON_API
  And Agnum API endpoint configured
  When export triggered
  Then POST to Agnum API with JSON payload
  And X-Export-ID header included
  And Agnum API returns 200 OK
  And AgnumExportHistory created (status: SUCCESS)

Scenario: Agnum API integration failure
  Given Agnum API returns 500 Internal Server Error
  When export triggered
  Then retry 3x with exponential backoff
  And if still fails, mark FAILED
  And save CSV to blob storage for manual upload
  And alert admin

Scenario: Idempotency - duplicate export
  Given export already sent with X-Export-ID "exp-123"
  When same export triggered again
  Then Agnum API deduplicates by X-Export-ID
  And returns 200 OK (no duplicate data)
```

### Implementation Notes

- Use CsvHelper library for CSV generation
- Use HttpClient for Agnum API calls (with Polly retry policy)
- Blob storage: Azure.Storage.Blobs SDK or local file system
- Export number generation: AUTO-AGNUM-{date}-{sequence}
- Idempotency: use export ID as X-Export-ID header value

### Validation / Checks

**Local Testing:**
```bash
# Trigger export
curl -X POST http://localhost:5000/api/warehouse/v1/agnum/export

# Verify CSV generated
ls /agnum-exports/2026-02-10/

# Verify API call (mock Agnum API)
# Check logs for API request/response

# Run tests
dotnet test --filter "Category=AgnumCSV"
```

**Metrics:**
- `agnum_csv_generation_duration_ms` (histogram)
- `agnum_api_calls_total` (counter, labels: status)
- `agnum_api_latency_ms` (histogram)

**Logs:**
- INFO: "CSV generated: {RowCount} rows, FilePath {FilePath}"
- INFO: "Agnum API call succeeded: ExportId {ExportId}"
- ERROR: "Agnum API call failed: {Exception}"

### Definition of Done

- [ ] CSV generation logic implemented
- [ ] Agnum API client implemented (HttpClient + Polly)
- [ ] Idempotency header (X-Export-ID) included
- [ ] Export history tracking implemented
- [ ] Blob storage integration implemented
- [ ] Unit tests: 10+ scenarios
- [ ] Integration tests: end-to-end CSV generation + API call
- [ ] Metrics exposed
- [ ] Logs added
- [ ] Code review completed
- [ ] Manual testing: generate CSV, call API, verify history

---

## Task PRD-1516: Label Printing - ZPL Template Engine + TCP 9100 Integration

**Epic:** Operational Excellence  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §4 (Label Printing)

### Context

- Need label printing for locations, handling units, items
- ZPL (Zebra Programming Language) format for Zebra printers
- TCP 9100 protocol (raw print, send ZPL to printer IP:9100)
- Template engine: replace placeholders ({{LocationCode}}, {{Barcode}})
- Print queue: retry 3x if printer offline, fallback to PDF

### Scope

**In Scope:**
- ZPL templates: Location label, HU label, Item label
- Template engine: replace placeholders with actual data
- TCP 9100 client: send ZPL to printer (IP:9100)
- Print queue: queue print jobs, retry 3x if printer offline
- PDF fallback: generate PDF if printer unavailable
- API endpoints: POST /labels/print, GET /labels/preview (PDF)

**Out of Scope:**
- Label designer UI (use pre-defined templates)
- Multi-printer support (single printer for Phase 1.5)

### Requirements

**Functional:**
1. ZPL templates stored in database or config files
2. Template engine: replace {{Placeholder}} with actual values
3. TCP 9100 client: connect to printer IP:9100, send ZPL, close connection
4. Print queue: if printer offline, queue job and retry 3x (1min, 2min, 4min)
5. PDF fallback: if printer still offline, generate PDF and return URL
6. API: POST /labels/print (labelType, data), GET /labels/preview (PDF)

**Non-Functional:**
1. Print latency: < 2 seconds (printer online)
2. Retry logic: 3x with exponential backoff
3. PDF generation: < 1 second
4. Printer connection timeout: 5 seconds

**ZPL Template Example (Location Label):**
```zpl
^XA
^FO50,50^A0N,50,50^FD{{LocationCode}}^FS
^FO50,120^BY3^BCN,100,Y,N,N^FD{{Barcode}}^FS
^FO50,240^A0N,30,30^FDCapacity: {{Capacity}} kg^FS
^XZ
```

**API Contract:**
```
POST /api/warehouse/v1/labels/print
Request: { labelType: "LOCATION", data: { locationCode: "R3-C6-L3", barcode: "LOC-001", capacity: 1000 } }
Response: 200 OK, { status: "PRINTED" } OR { status: "QUEUED" } OR { status: "PDF_FALLBACK", pdfUrl: "..." }

GET /api/warehouse/v1/labels/preview?labelType=LOCATION&locationCode=R3-C6-L3
Response: 200 OK, PDF file
```

### Acceptance Criteria

```gherkin
Scenario: Print location label successfully
  Given printer at IP 192.168.1.100:9100 is online
  When POST /labels/print with labelType "LOCATION", data { locationCode: "R3-C6-L3", barcode: "LOC-001" }
  Then ZPL template loaded
  And placeholders replaced with actual data
  And ZPL sent to printer via TCP 9100
  And response: { status: "PRINTED" }

Scenario: Printer offline - queue and retry
  Given printer is offline
  When POST /labels/print
  Then print job queued
  And retry after 1 minute
  And retry after 2 minutes
  And retry after 4 minutes
  And if still offline, generate PDF fallback

Scenario: PDF fallback
  Given printer offline after 3 retries
  When print job processed
  Then PDF generated from ZPL template
  And PDF saved to blob storage
  And response: { status: "PDF_FALLBACK", pdfUrl: "/labels/pdf/label-001.pdf" }

Scenario: Preview label as PDF
  When GET /labels/preview?labelType=LOCATION&locationCode=R3-C6-L3
  Then ZPL template loaded
  And placeholders replaced
  And PDF generated
  And PDF returned in response
```

### Implementation Notes

- Use TcpClient for TCP 9100 connection
- Use Hangfire for print queue and retry logic
- Use ZPL-to-PDF converter library (e.g., Labelary API or local converter)
- Store templates in database (label_templates table) or config files
- Template engine: simple string replacement ({{Placeholder}} → value)

### Validation / Checks

**Local Testing:**
```bash
# Print label (requires Zebra printer or simulator)
curl -X POST http://localhost:5000/api/warehouse/v1/labels/print \
  -d '{ "labelType": "LOCATION", "data": { "locationCode": "R3-C6-L3", "barcode": "LOC-001" } }'

# Preview label as PDF
curl http://localhost:5000/api/warehouse/v1/labels/preview?labelType=LOCATION&locationCode=R3-C6-L3 > label.pdf

# Run tests
dotnet test --filter "Category=LabelPrinting"
```

**Metrics:**
- `label_prints_total` (counter, labels: label_type, status)
- `label_print_duration_ms` (histogram)
- `printer_offline_total` (counter)

**Logs:**
- INFO: "Label printed: {LabelType}, {LocationCode}"
- WARN: "Printer offline, queuing print job"
- ERROR: "Label print failed: {Exception}"

### Definition of Done

- [ ] ZPL templates created (Location, HU, Item)
- [ ] Template engine implemented (placeholder replacement)
- [ ] TCP 9100 client implemented
- [ ] Print queue implemented (Hangfire)
- [ ] Retry logic implemented (3x with backoff)
- [ ] PDF fallback implemented
- [ ] API endpoints implemented (POST /print, GET /preview)
- [ ] Unit tests: 10+ scenarios
- [ ] Integration tests: end-to-end print workflow
- [ ] Metrics exposed
- [ ] Logs added
- [ ] Code review completed
- [ ] Manual testing: print to real printer or simulator

---
## Task PRD-1517: 3D Visualization - Location Coordinates + Static 3D Model

**Epic:** E - 3D Visualization  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** L (2 days)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §4.Epic E (3D Visualization)

### Context

- Phase 1 has no visual warehouse map
- Need 3D coordinates for locations (X, Y, Z in meters)
- WarehouseLayout entity to define warehouse dimensions and zones
- 3D API endpoint to provide data for frontend rendering
- Foundation for 3D UI (PRD-1518)

### Scope

**In Scope:**
- Update Location entity: add CoordinateX, CoordinateY, CoordinateZ, Aisle, Rack, Level, Bin, CapacityWeight, CapacityVolume
- WarehouseLayout entity: WarehouseCode, Dimensions, Zones
- ZoneDefinition: ZoneType, Bounds (X1, Y1, X2, Y2), Color
- Database migration: add columns to locations table, create warehouse_layout table
- API endpoints: GET /layout, PUT /layout, GET /visualization/3d
- 3D API response: warehouse dimensions, bins (code, coords, capacity, status, color, HUs), zones

**Out of Scope:**
- 3D UI rendering (PRD-1518)
- Real-time updates (manual refresh only)

### Requirements

**Functional:**
1. Location entity updates: CoordinateX, CoordinateY, CoordinateZ (nullable, meters from origin)
2. Location entity: Aisle, Rack, Level, Bin (for structured naming)
3. WarehouseLayout: WarehouseCode, WidthMeters, LengthMeters, HeightMeters, Zones
4. ZoneDefinition: ZoneType (RECEIVING, STORAGE, SHIPPING, QUARANTINE), Bounds, Color
5. API: GET /layout (returns warehouse config), PUT /layout (admin only)
6. API: GET /visualization/3d (returns bins with coords, status, HUs for rendering)
7. Bin status calculation: EMPTY (no HUs), LOW (<50% capacity), FULL (>80%), RESERVED (has HARD locks)

**Non-Functional:**
1. 3D API latency: < 2 seconds for 1000 bins
2. Coordinate precision: 2 decimal places (centimeters)
3. Validation: no overlapping bins (check 3D bounding boxes)

**Data Model:**
```csharp
public class Location
{
  // Existing fields...
  public decimal? CoordinateX { get; set; }
  public decimal? CoordinateY { get; set; }
  public decimal? CoordinateZ { get; set; }
  public string Aisle { get; set; }
  public string Rack { get; set; }
  public string Level { get; set; }
  public string Bin { get; set; }
  public decimal? CapacityWeight { get; set; }
  public decimal? CapacityVolume { get; set; }
}

public class WarehouseLayout
{
  public Guid Id { get; set; }
  public string WarehouseCode { get; set; }
  public decimal WidthMeters { get; set; }
  public decimal LengthMeters { get; set; }
  public decimal HeightMeters { get; set; }
  public List<ZoneDefinition> Zones { get; set; }
}

public class ZoneDefinition
{
  public string ZoneType { get; set; }
  public decimal X1 { get; set; }
  public decimal Y1 { get; set; }
  public decimal X2 { get; set; }
  public decimal Y2 { get; set; }
  public string Color { get; set; }
}
```

**3D API Response:**
```json
{
  "warehouse": {
    "code": "Main",
    "dimensions": { "width": 50, "length": 100, "height": 10 }
  },
  "bins": [
    {
      "code": "R3-C6-L3B3",
      "coordinates": { "x": 15.5, "y": 32.0, "z": 6.0 },
      "capacity": { "weight": 1000, "volume": 2.0 },
      "status": "FULL",
      "color": "#FFA500",
      "handlingUnits": [
        { "id": "HU-001", "sku": "RM-0001", "qty": 50 }
      ]
    }
  ],
  "zones": [
    { "type": "RECEIVING", "bounds": { "x1": 0, "y1": 0, "x2": 10, "y2": 100 }, "color": "#ADD8E6" }
  ]
}
```

### Acceptance Criteria

```gherkin
Scenario: Update location with 3D coordinates
  Given Location "R3-C6-L3" exists
  When admin updates coordinates: X=15.5, Y=32.0, Z=6.0
  Then location saved with coordinates
  And coordinates visible in 3D API response

Scenario: Configure warehouse layout
  Given admin navigates to /warehouse/admin/layout
  When admin sets dimensions: Width=50m, Length=100m, Height=10m
  And adds zone: RECEIVING (X1=0, Y1=0, X2=10, Y2=100, Color=#ADD8E6)
  And saves layout
  Then warehouse_layout table updated
  And layout visible in GET /layout API

Scenario: 3D API returns bins with status
  Given 3 locations with coordinates and HUs:
    | Code | X | Y | Z | HUs | Capacity | Utilization |
    | R1-C1-L1 | 5 | 10 | 2 | 0 | 1000kg | 0% |
    | R2-C2-L2 | 10 | 20 | 4 | 1 | 1000kg | 40% |
    | R3-C3-L3 | 15 | 30 | 6 | 2 | 1000kg | 85% |
  When GET /visualization/3d
  Then response includes 3 bins
  And R1-C1-L1 status=EMPTY, color=gray
  And R2-C2-L2 status=LOW, color=yellow
  And R3-C3-L3 status=FULL, color=orange
```

### Implementation Notes

- Migration: ALTER TABLE locations ADD COLUMN coordinate_x, coordinate_y, coordinate_z
- Status calculation: query HandlingUnits, sum weight/volume, compare to capacity
- Color mapping: EMPTY=gray, LOW=yellow, FULL=orange, RESERVED=blue
- Validation: check no overlapping bins (3D bounding box collision detection)

### Validation / Checks

**Local Testing:**
```bash
# Update location coordinates
curl -X PUT http://localhost:5000/api/warehouse/v1/locations/R3-C6-L3 \
  -d '{ "coordinateX": 15.5, "coordinateY": 32.0, "coordinateZ": 6.0 }'

# Get 3D data
curl http://localhost:5000/api/warehouse/v1/visualization/3d

# Run tests
dotnet test --filter "Category=3DVisualization"
```

**Metrics:**
- `visualization_3d_api_calls_total` (counter)
- `visualization_3d_api_duration_ms` (histogram)

**Logs:**
- INFO: "Location coordinates updated: {LocationCode}, X={X}, Y={Y}, Z={Z}"
- INFO: "3D API called: {BinCount} bins returned"

### Definition of Done

- [ ] Location entity updated with coordinate fields
- [ ] WarehouseLayout entity created
- [ ] ZoneDefinition entity created
- [ ] Database migration created
- [ ] API endpoints implemented (GET/PUT /layout, GET /visualization/3d)
- [ ] Bin status calculation implemented
- [ ] Unit tests: 10+ scenarios
- [ ] Integration tests: end-to-end 3D API
- [ ] Metrics exposed
- [ ] Logs added
- [ ] Code review completed
- [ ] Manual testing: configure layout, query 3D API

---
## Task PRD-1518: 3D Visualization - UI Implementation

**Epic:** E - 3D Visualization  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** L (2 days)  
**OwnerType:** Frontend/UI  
**Dependencies:** PRD-1517 (3D Location Coordinates)  
**SourceRefs:** Universe §4.Epic E (UI/UX Pages)

### Context

- PRD-1517 created 3D API endpoint with bin coordinates and status
- Need interactive 3D warehouse view using Three.js
- Features: rotate/zoom/pan, click bin for details, search location, color-coded status
- 2D floor plan toggle for alternative view
- Manual refresh (no real-time updates in Phase 1.5)

### Scope

**In Scope:**
- 3D view: Three.js rendering with OrbitControls
- Color-coded bins: empty=gray, low=yellow, full=orange, reserved=blue
- Click bin: highlight + show details panel (location, capacity, HUs, items)
- Search: location code → fly to location + highlight
- 2D view: top-down SVG/Canvas floor plan
- Toggle button: switch between 2D/3D
- Refresh button: reload data from API
- UI routes: /warehouse/visualization/3d, /warehouse/visualization/2d

**Out of Scope:**
- Real-time updates (WebSockets, deferred to Phase 2)
- Operator location tracking (RTLS)
- Path optimization

### Requirements

**Functional:**
1. 3D view: render warehouse with bins as 3D boxes
2. OrbitControls: rotate (drag), zoom (scroll), pan (shift+drag)
3. Color coding: bins colored by status (from API)
4. Click bin: highlight in gold, show details panel
5. Details panel: location code, capacity utilization, HUs list, items list, "View Details" link
6. Search: input location code, fly camera to location, highlight bin
7. 2D view: top-down floor plan (SVG or Canvas)
8. Toggle button: switch between 2D/3D views
9. Refresh button: reload data from API (polling every 30 seconds optional)

**Non-Functional:**
1. Initial load time: < 3 seconds for 1000 bins
2. Frame rate: 60 FPS (smooth rotation/zoom)
3. Browser support: Chrome, Firefox, Safari, Edge (latest 2 versions)
4. Responsive: desktop and tablet (not phone)

**UI Components (React + Three.js):**
```tsx
// 3DWarehouseView.tsx
export const 3DWarehouseView = () => {
  const [warehouseData, setWarehouseData] = useState(null);
  const [selectedBin, setSelectedBin] = useState(null);
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    fetchWarehouseData();
  }, []);

  const fetchWarehouseData = async () => {
    const response = await api.get('/visualization/3d');
    setWarehouseData(response.data);
  };

  const handleBinClick = (bin) => {
    setSelectedBin(bin);
    // Highlight bin in 3D scene
  };

  const handleSearch = (locationCode) => {
    const bin = warehouseData.bins.find(b => b.code === locationCode);
    if (bin) {
      // Fly camera to bin
      // Highlight bin
      setSelectedBin(bin);
    }
  };

  return (
    <div style={{ display: 'flex', height: '100vh' }}>
      <div style={{ flex: 1 }}>
        <SearchBar onSearch={handleSearch} />
        <ThreeJSCanvas data={warehouseData} onBinClick={handleBinClick} />
      </div>
      {selectedBin && (
        <DetailsPanel bin={selectedBin} onClose={() => setSelectedBin(null)} />
      )}
    </div>
  );
};

// ThreeJSCanvas.tsx
const ThreeJSCanvas = ({ data, onBinClick }) => {
  const mountRef = useRef(null);

  useEffect(() => {
    if (!data) return;

    // Initialize Three.js scene
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 1000);
    const renderer = new THREE.WebGLRenderer();
    renderer.setSize(window.innerWidth, window.innerHeight);
    mountRef.current.appendChild(renderer.domElement);

    // Add OrbitControls
    const controls = new OrbitControls(camera, renderer.domElement);

    // Render bins
    data.bins.forEach(bin => {
      const geometry = new THREE.BoxGeometry(1, 1, 1);
      const material = new THREE.MeshBasicMaterial({ color: bin.color });
      const cube = new THREE.Mesh(geometry, material);
      cube.position.set(bin.coordinates.x, bin.coordinates.y, bin.coordinates.z);
      cube.userData = bin; // Store bin data for click handling
      scene.add(cube);
    });

    // Raycaster for click detection
    const raycaster = new THREE.Raycaster();
    const mouse = new THREE.Vector2();

    const onMouseClick = (event) => {
      mouse.x = (event.clientX / window.innerWidth) * 2 - 1;
      mouse.y = -(event.clientY / window.innerHeight) * 2 + 1;
      raycaster.setFromCamera(mouse, camera);
      const intersects = raycaster.intersectObjects(scene.children);
      if (intersects.length > 0) {
        const clickedBin = intersects[0].object.userData;
        onBinClick(clickedBin);
      }
    };

    renderer.domElement.addEventListener('click', onMouseClick);

    // Animation loop
    const animate = () => {
      requestAnimationFrame(animate);
      controls.update();
      renderer.render(scene, camera);
    };
    animate();

    return () => {
      mountRef.current.removeChild(renderer.domElement);
    };
  }, [data]);

  return <div ref={mountRef} />;
};
```

### Acceptance Criteria

```gherkin
Scenario: View 3D warehouse model
  Given warehouse has 200 bins configured with coordinates
  When user navigates to /warehouse/visualization/3d
  Then 3D model renders showing all bins
  And bins colored by status (empty=gray, low=yellow, full=orange)
  And user can rotate camera with mouse drag
  And user can zoom with scroll wheel

Scenario: Click bin to view details
  Given 3D warehouse view loaded
  When user clicks bin "R3-C6-L3B3"
  Then bin highlights in gold
  And right panel shows:
    - Location code: R3-C6-L3B3
    - Capacity utilization: 85%
    - Handling units: 2 HUs (HU-001, HU-002)
    - Items: RM-0001 (50 units), RM-0002 (30 units)
  And "View Details" button links to /warehouse/locations/{id}

Scenario: Search location and fly to it
  Given 3D warehouse view loaded
  When user types "R5-C2" in search box
  And presses Enter
  Then camera flies to bin R5-C2 (animated)
  And bin highlights
  And details panel opens

Scenario: Toggle 2D view
  Given 3D warehouse view loaded
  When user clicks "2D View" button
  Then view switches to top-down floor plan (SVG)
  And bins displayed as rectangles with same color coding
  And click bin still shows details panel

Scenario: Refresh data
  Given 3D warehouse view loaded
  When user clicks "Refresh" button
  Then API called: GET /visualization/3d
  And 3D model updates with new data
  And bin colors update based on new status
```

### Implementation Notes

- Use Three.js for 3D rendering (MIT license, ~600KB)
- Use OrbitControls for camera manipulation
- Use Raycaster for click detection (bin selection)
- Use React hooks (useState, useEffect, useRef) for state management
- 2D view: use SVG or Canvas for top-down rendering
- Camera fly-to animation: use TWEEN.js or custom interpolation

### Validation / Checks

**Local Testing:**
```bash
# Start frontend dev server
cd src/LKvitai.MES.UI
npm run dev

# Navigate to 3D view
http://localhost:3000/warehouse/visualization/3d

# Test interactions:
# - Rotate camera (drag)
# - Zoom (scroll)
# - Click bin (details panel)
# - Search location (fly-to)
# - Toggle 2D view
# - Refresh data
```

**Metrics:**
- N/A (frontend, no backend metrics)

**Logs:**
- Browser console logs for errors

### Definition of Done

- [ ] 3DWarehouseView component created
- [ ] ThreeJSCanvas component created (Three.js rendering)
- [ ] OrbitControls integrated
- [ ] Click detection implemented (Raycaster)
- [ ] Details panel component created
- [ ] Search functionality implemented (fly-to animation)
- [ ] 2D floor plan view implemented (SVG/Canvas)
- [ ] Toggle button implemented (2D/3D switch)
- [ ] Refresh button implemented
- [ ] React Router routes configured
- [ ] Unit tests: 5+ scenarios (component rendering, interactions)
- [ ] Manual testing: all interactions, all browsers
- [ ] Code review completed
- [ ] Documentation: UI screenshots added to docs/

---
## Task PRD-1519: Inter-Warehouse Transfers - Transfer Request Workflow

**Epic:** F - Inter-Warehouse Transfers  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §4.Epic F (Inter-Warehouse Transfers)

### Context

- Need to move stock between logical warehouses (RES → PROD, NLQ → SCRAP)
- Transfer workflow: request → approve → execute
- In-transit virtual location: IN_TRANSIT_{transferId}
- Approval rules: Manager approval for SCRAP transfers (write-off)
- Use case: release reserved stock, quarantine to scrap, production return

### Scope

**In Scope:**
- Transfer entity: TransferNumber, FromWarehouse, ToWarehouse, Status, Lines
- TransferLine entity: ItemId, Qty, FromLocationId, ToLocationId
- Commands: CreateTransferCommand, ApproveTransferCommand, ExecuteTransferCommand
- State machine: DRAFT → PENDING_APPROVAL → APPROVED → IN_TRANSIT → COMPLETED
- Virtual location: IN_TRANSIT_{transferId} (during transfer)
- API endpoints: POST /transfers, POST /transfers/{id}/approve, POST /transfers/{id}/execute
- Events: TransferCreated, TransferApproved, TransferExecuted, TransferCompleted

**Out of Scope:**
- Physical inter-building transfers (single warehouse assumption for Phase 1.5)
- Multi-step transfers (direct transfer only)

### Requirements

**Functional:**
1. Transfer entity: TransferNumber, FromWarehouse, ToWarehouse, Status, RequestedBy, ApprovedBy, Lines
2. TransferLine: ItemId, Qty, FromLocationId, ToLocationId
3. CreateTransferCommand: validate from/to warehouses, create transfer (status=DRAFT)
4. ApproveTransferCommand: Manager approval required if ToWarehouse=SCRAP
5. ExecuteTransferCommand: move stock (StockMoved events), update status=IN_TRANSIT, then COMPLETED
6. State machine: DRAFT → PENDING_APPROVAL (if SCRAP) → APPROVED → IN_TRANSIT → COMPLETED
7. Virtual location: create IN_TRANSIT_{transferId} location during transfer

**Non-Functional:**
1. API latency: < 2 seconds for execute (includes stock moves)
2. Idempotency: all commands include CommandId
3. Authorization: Warehouse Operator can create, Manager can approve
4. Transactional: all stock moves in single transaction

**Data Model:**
```csharp
public class Transfer
{
  public Guid Id { get; set; }
  public string TransferNumber { get; set; }
  public string FromWarehouse { get; set; }
  public string ToWarehouse { get; set; }
  public TransferStatus Status { get; set; }
  public string RequestedBy { get; set; }
  public string ApprovedBy { get; set; }
  public DateTime RequestedAt { get; set; }
  public DateTime? ApprovedAt { get; set; }
  public DateTime? ExecutedAt { get; set; }
  public DateTime? CompletedAt { get; set; }
  public List<TransferLine> Lines { get; set; }
}

public class TransferLine
{
  public Guid Id { get; set; }
  public Guid TransferId { get; set; }
  public Guid ItemId { get; set; }
  public decimal Qty { get; set; }
  public Guid FromLocationId { get; set; }
  public Guid ToLocationId { get; set; }
}

public enum TransferStatus
{
  DRAFT,
  PENDING_APPROVAL,
  APPROVED,
  IN_TRANSIT,
  COMPLETED,
  CANCELLED
}
```

### Acceptance Criteria

```gherkin
Scenario: Create transfer request (no approval required)
  Given FromWarehouse "RES", ToWarehouse "PROD"
  When POST /transfers with lines: [{ itemId: ITEM-001, qty: 10, fromLocationId: LOC-RES-001, toLocationId: LOC-PROD-001 }]
  Then Transfer created with status DRAFT
  And TransferCreated event emitted

Scenario: Create transfer requires approval (SCRAP)
  Given FromWarehouse "NLQ", ToWarehouse "SCRAP"
  When POST /transfers
  Then Transfer created with status PENDING_APPROVAL
  And approval required before execution

Scenario: Approve transfer
  Given Transfer "TRF-001" with status PENDING_APPROVAL
  And user has Manager role
  When POST /transfers/TRF-001/approve
  Then Transfer status updated to APPROVED
  And TransferApproved event emitted

Scenario: Execute transfer
  Given Transfer "TRF-001" with status APPROVED
  When POST /transfers/TRF-001/execute
  Then virtual location IN_TRANSIT_TRF-001 created
  And StockMoved events emitted (from LOC-RES-001 → IN_TRANSIT_TRF-001)
  And Transfer status updated to IN_TRANSIT
  And StockMoved events emitted (IN_TRANSIT_TRF-001 → LOC-PROD-001)
  And Transfer status updated to COMPLETED
  And TransferCompleted event emitted

Scenario: Validation failure - insufficient stock
  Given Transfer with qty 10
  And FromLocation has qty 5
  When POST /transfers/{id}/execute
  Then error: "Insufficient stock at location LOC-RES-001"
  And Transfer status remains APPROVED
```

### Implementation Notes

- Use MediatR for command handlers
- State machine validation: use Transfer.Approve(), Transfer.Execute() methods
- Virtual location: create Location with code IN_TRANSIT_{transferId}, type VIRTUAL
- Stock moves: emit StockMoved events (from → in-transit → to)
- Approval validation: check user role (Manager) and ToWarehouse (SCRAP)
- Transactional: wrap all stock moves in single EF Core transaction

### Validation / Checks

**Local Testing:**
```bash
# Create transfer
curl -X POST http://localhost:5000/api/warehouse/v1/transfers \
  -d '{ "commandId": "test-001", "fromWarehouse": "RES", "toWarehouse": "PROD", "lines": [...] }'

# Approve transfer
curl -X POST http://localhost:5000/api/warehouse/v1/transfers/<id>/approve

# Execute transfer
curl -X POST http://localhost:5000/api/warehouse/v1/transfers/<id>/execute

# Run tests
dotnet test --filter "Category=Transfers"
```

**Metrics:**
- `transfers_created_total` (counter)
- `transfers_executed_total` (counter)
- `transfer_execution_duration_ms` (histogram)

**Logs:**
- INFO: "Transfer created: {TransferNumber}, From {FromWarehouse} To {ToWarehouse}"
- INFO: "Transfer executed: {TransferNumber}, {LineCount} lines"

### Definition of Done

- [ ] Transfer entity created
- [ ] TransferLine entity created
- [ ] CreateTransferCommand + handler implemented
- [ ] ApproveTransferCommand + handler implemented
- [ ] ExecuteTransferCommand + handler implemented
- [ ] State machine methods implemented
- [ ] Virtual location creation implemented
- [ ] Stock move events emitted
- [ ] API endpoints implemented
- [ ] Events defined and published
- [ ] Unit tests: 10+ scenarios
- [ ] Integration tests: end-to-end transfer workflow
- [ ] Metrics exposed
- [ ] Logs added
- [ ] Code review completed
- [ ] Manual testing: create, approve, execute transfer

---
## Task PRD-1520: Cycle Counting - Scheduled Counts + Discrepancy Resolution

**Epic:** Operational Excellence  
**Phase:** 1.5  
**Sprint:** 2  
**Estimate:** M (2 days)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §4 (Cycle Counting)

### Context

- Need periodic physical counts to verify system accuracy
- ABC classification: A-items monthly, B-items quarterly, C-items annually
- Cycle count workflow: schedule → count → compare → approve discrepancy → adjust stock
- Approval required for discrepancies > 5% or $1000
- Use case: detect shrinkage, damage, data entry errors

### Scope

**In Scope:**
- CycleCount entity: CountNumber, Status, Locations, Lines, ScheduledDate
- CycleCountLine entity: LocationId, ItemId, SystemQty, PhysicalQty, Delta, Status
- Commands: ScheduleCycleCountCommand, RecordCountCommand, ApplyAdjustmentCommand
- ABC classification: configurable (A=monthly, B=quarterly, C=annual)
- Approval workflow: Manager approval for discrepancies > 5% or $1000
- API endpoints: POST /cycle-counts/schedule, POST /cycle-counts/{id}/record-count, POST /cycle-counts/{id}/apply-adjustment
- Events: CycleCountScheduled, CountRecorded, CycleCountCompleted, StockAdjusted

**Out of Scope:**
- Blind counts (operator doesn't see system qty, deferred to Phase 2)
- Mobile app for counting (use web UI)

### Requirements

**Functional:**
1. CycleCount entity: CountNumber, Status (SCHEDULED, IN_PROGRESS, COMPLETED), Locations, Lines, ScheduledDate
2. CycleCountLine: LocationId, ItemId, SystemQty, PhysicalQty, Delta, Status (PENDING, APPROVED, REJECTED)
3. ScheduleCycleCountCommand: select locations based on ABC classification, create cycle count
4. RecordCountCommand: operator scans location, counts items, records physical qty
5. Compare: Delta = PhysicalQty - SystemQty
6. ApplyAdjustmentCommand: if approved, emit StockAdjusted event (adjust system qty)
7. Approval rules: Manager approval if |Delta| > 5% OR |Delta × UnitCost| > $1000
8. ABC classification: A-items (high value) monthly, B-items quarterly, C-items annually

**Non-Functional:**
1. API latency: < 1 second for record count
2. Approval latency: < 5 seconds for apply adjustment
3. Idempotency: all commands include CommandId
4. Authorization: Warehouse Operator can count, Manager can approve

**Data Model:**
```csharp
public class CycleCount
{
  public Guid Id { get; set; }
  public string CountNumber { get; set; }
  public CycleCountStatus Status { get; set; }
  public DateTime ScheduledDate { get; set; }
  public DateTime? StartedAt { get; set; }
  public DateTime? CompletedAt { get; set; }
  public List<CycleCountLine> Lines { get; set; }
  public string CountedBy { get; set; }
  public string ApprovedBy { get; set; }
}

public class CycleCountLine
{
  public Guid Id { get; set; }
  public Guid CycleCountId { get; set; }
  public Guid LocationId { get; set; }
  public Guid ItemId { get; set; }
  public decimal SystemQty { get; set; }
  public decimal PhysicalQty { get; set; }
  public decimal Delta { get; set; }
  public CycleCountLineStatus Status { get; set; }
  public string Reason { get; set; }
}

public enum CycleCountStatus { SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED }
public enum CycleCountLineStatus { PENDING, APPROVED, REJECTED, RECOUNT }
```

### Acceptance Criteria

```gherkin
Scenario: Schedule cycle count (ABC classification)
  Given 100 A-items (high value), 500 B-items, 1000 C-items
  And ABC config: A=monthly, B=quarterly, C=annual
  When POST /cycle-counts/schedule with date 2026-02-15
  Then CycleCount created with 100 A-items
  And status SCHEDULED
  And CycleCountScheduled event emitted

Scenario: Record physical count
  Given CycleCount "CC-001" with status SCHEDULED
  And line: Location LOC-001, Item ITEM-001, SystemQty 100
  When operator scans location LOC-001
  And counts physical qty: 95
  And POST /cycle-counts/CC-001/record-count with physicalQty 95
  Then CycleCountLine updated: PhysicalQty=95, Delta=-5
  And CountRecorded event emitted

Scenario: Apply adjustment (no approval required)
  Given CycleCountLine with Delta=-5 (5% discrepancy, low value)
  When POST /cycle-counts/CC-001/apply-adjustment
  Then StockAdjusted event emitted (adjust system qty from 100 to 95)
  And CycleCountLine status=APPROVED
  And CycleCount status=COMPLETED

Scenario: Discrepancy requires approval
  Given CycleCountLine with Delta=-20 (20% discrepancy)
  When POST /cycle-counts/CC-001/apply-adjustment without approval
  Then error: "Manager approval required for discrepancy > 5%"
  And CycleCountLine status=PENDING

Scenario: Manager approves discrepancy
  Given CycleCountLine with Delta=-20, status PENDING
  And user has Manager role
  When POST /cycle-counts/CC-001/apply-adjustment with approverId
  Then StockAdjusted event emitted
  And CycleCountLine status=APPROVED

Scenario: Recount requested
  Given CycleCountLine with large discrepancy
  When manager requests recount
  Then CycleCountLine status=RECOUNT
  And operator must count again
```

### Implementation Notes

- Use MediatR for command handlers
- ABC classification: query Item.Category or Item.Value, classify as A/B/C
- Scheduled job: Hangfire recurring job (daily, check for scheduled counts)
- Approval validation: calculate |Delta| / SystemQty (percentage), |Delta × UnitCost| (dollar impact)
- Stock adjustment: emit StockAdjusted event (consumed by StockLedger aggregate)

### Validation / Checks

**Local Testing:**
```bash
# Schedule cycle count
curl -X POST http://localhost:5000/api/warehouse/v1/cycle-counts/schedule \
  -d '{ "commandId": "test-001", "scheduledDate": "2026-02-15" }'

# Record count
curl -X POST http://localhost:5000/api/warehouse/v1/cycle-counts/<id>/record-count \
  -d '{ "commandId": "test-002", "locationId": "<guid>", "itemId": "<guid>", "physicalQty": 95 }'

# Apply adjustment
curl -X POST http://localhost:5000/api/warehouse/v1/cycle-counts/<id>/apply-adjustment

# Run tests
dotnet test --filter "Category=CycleCounting"
```

**Metrics:**
- `cycle_counts_scheduled_total` (counter)
- `cycle_counts_completed_total` (counter)
- `cycle_count_discrepancies_total` (counter, labels: approval_required)
- `cycle_count_accuracy_percentage` (gauge)

**Logs:**
- INFO: "Cycle count scheduled: {CountNumber}, {LineCount} lines"
- INFO: "Count recorded: {LocationCode}, {ItemSku}, Delta {Delta}"
- WARN: "Discrepancy requires approval: Delta {Delta}, Impact ${Impact}"

### Definition of Done

- [ ] CycleCount entity created
- [ ] CycleCountLine entity created
- [ ] ScheduleCycleCountCommand + handler implemented
- [ ] RecordCountCommand + handler implemented
- [ ] ApplyAdjustmentCommand + handler implemented
- [ ] ABC classification logic implemented
- [ ] Approval validation implemented
- [ ] Stock adjustment events emitted
- [ ] API endpoints implemented
- [ ] Events defined and published
- [ ] Unit tests: 15+ scenarios
- [ ] Integration tests: end-to-end cycle count workflow
- [ ] Metrics exposed
- [ ] Logs added
- [ ] Code review completed
- [ ] Manual testing: schedule, count, approve, adjust

---

## Sprint 2 Summary

**Total Tasks:** 10  
**Total Effort:** 12 days  
**Status:** ✅ All tasks fully elaborated and ready for execution

**Task Breakdown:**
- Valuation (3 tasks, 3 days): Aggregate, Cost Adjustment, OnHandValue Projection
- Agnum Integration (2 tasks, 3 days): Export Config, CSV/API Integration
- Label Printing (1 task, 1 day): ZPL Templates + TCP 9100
- 3D Visualization (2 tasks, 4 days): Location Coordinates, UI Implementation
- Inter-Warehouse Transfers (1 task, 1 day): Transfer Workflow
- Cycle Counting (1 task, 2 days): Scheduled Counts + Discrepancy Resolution

**Next Steps:**
1. Review this execution pack with team
2. Begin Sprint 2 execution with PRD-1511 (Valuation Aggregate)
3. Follow task order: PRD-1511 → PRD-1512 → PRD-1513 → PRD-1514 → PRD-1515 → PRD-1516 → PRD-1517 → PRD-1518 → PRD-1519 → PRD-1520
4. Update progress ledger after each task completion
5. Conduct sprint review after all tasks complete

**Files:**
- Main file: `prod-ready-tasks-PHASE15-S2.md` (this file)
- Summary file: `prod-ready-tasks-PHASE15-S2-summary.md`
- Progress ledger: `prod-ready-tasks-progress.md`
