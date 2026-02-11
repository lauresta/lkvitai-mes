# Quality Gate Review: Phase 1.5 Sprint 1 & Sprint 2 Task Packs

**Review Date:** February 11, 2026
**Reviewer:** Claude (Senior Solution Architect + QA Lead)
**Reviewed Documents:**
- prod-ready-tasks-PHASE15-S1.md (1002 lines)
- prod-ready-tasks-PHASE15-S1-summary.md (119 lines)
- prod-ready-tasks-PHASE15-S1-remaining.md (0 lines, EMPTY)
- prod-ready-tasks-PHASE15-S2.md (316 lines)
- prod-ready-tasks-PHASE15-S2-summary.md (188 lines)
- prod-ready-tasks-progress.md (215 lines)

**Verdict:** NOT OK

---

## Executive Summary

**Critical Finding:** Sprint 1 and Sprint 2 task packs are **INCOMPLETE** and **NOT EXECUTABLE** as-is. While the format, structure, and quality of the documented tasks (5 out of 20) are excellent, **75% of tasks (15 out of 20) are missing detailed specifications**.

**What Exists:**
- ✅ Sprint 1: Tasks PRD-1501 to PRD-1504 (4 tasks, ~800 lines) - FULLY DOCUMENTED
- ✅ Sprint 2: Task PRD-1511 (1 task, ~280 lines) - FULLY DOCUMENTED
- ✅ Excellent task format: Context, Scope, Requirements, Gherkin scenarios, DoD, SourceRefs

**What's Missing:**
- ❌ Sprint 1: Tasks PRD-1505 to PRD-1510 (6 tasks) - ONLY SUMMARY, no detailed specs
- ❌ Sprint 2: Tasks PRD-1512 to PRD-1520 (9 tasks) - ONLY SUMMARY, no detailed specs
- ❌ Estimated ~4,500 lines of missing specifications

**Impact:**
- Developers cannot execute tasks PRD-1505 to PRD-1510 or PRD-1512 to PRD-1520 without re-reading universe
- Missing API contracts, event payloads, state machine logic, validation rules, DoD checklists
- Risk of inconsistencies across tasks (different interpretations of universe)

**Recommendation:** Complete elaboration of remaining 15 tasks before sprint execution begins.

---

## Detailed Findings

### 1. CRITICAL: Incomplete Task Elaboration (BLOCKER)

**Finding:** Only 5 out of 20 tasks (25%) have complete, executable specifications. The remaining 15 tasks (75%) exist only as summaries.

**Detailed Breakdown:**

| File | Tasks Claimed | Tasks Fully Documented | Missing |
|------|---------------|------------------------|---------|
| PHASE15-S1.md | 10 (PRD-1501 to PRD-1510) | 4 (PRD-1501 to PRD-1504) | 6 (PRD-1505 to PRD-1510) |
| PHASE15-S2.md | 10 (PRD-1511 to PRD-1520) | 1 (PRD-1511) | 9 (PRD-1512 to PRD-1520) |
| **Total** | **20** | **5 (25%)** | **15 (75%)** |

**Evidence:**

Sprint 1 file (PHASE15-S1.md, 1002 lines):
- Contains FULL specs for PRD-1501 to PRD-1504 (Foundation + SalesOrder entities)
- Tasks PRD-1505 to PRD-1510 are MISSING from the file entirely
- File ends at line 1002 with PRD-1504's Definition of Done
- No PRD-1505 (SalesOrder APIs), no PRD-1506 (OutboundOrder entities), no PRD-1507 to PRD-1510

Sprint 2 file (PHASE15-S2.md, 316 lines):
- Contains FULL spec for PRD-1511 only (Valuation aggregate)
- Tasks PRD-1512 to PRD-1520 are MISSING from the file entirely
- File ends at line 316 with PRD-1511's Definition of Done
- No PRD-1512 (Cost Adjustment), no PRD-1513 (OnHandValue Projection), no PRD-1514 to PRD-1520

Summary files claim "all tasks include complete details" but this is **factually incorrect**.

**What's Missing Per Task:**

Each missing task needs (~300 lines):
- Context (5-8 bullets)
- Scope (in/out)
- Requirements (functional, non-functional, data model, API contracts, events)
- 5-7 Gherkin acceptance criteria scenarios (including negative cases)
- Implementation notes
- Validation/checks (local testing, metrics, logs, backwards compatibility)
- Definition of Done (15-20 checklist items)
- Source references (with line numbers)

**Estimated Missing Content:**
- Sprint 1: 6 tasks × 300 lines = ~1,800 lines
- Sprint 2: 9 tasks × 300 lines = ~2,700 lines
- **Total: ~4,500 lines of specifications**

**Why This is a Blocker:**
- Developers cannot implement PRD-1505 without knowing:
  - Exact API endpoint signatures (request/response DTOs)
  - Event payloads (SalesOrderCreated, SalesOrderAllocated, etc.)
  - State machine transition logic (DRAFT → ALLOCATED → PICKING → PACKED)
  - Validation rules (credit limit checks, allocation logic)
  - Error handling (what happens if allocation fails?)
  - RBAC permissions (who can approve orders?)
  - Idempotency handling (CommandId in all API calls?)
  - Observability (metrics, logs)
- Same applies to PRD-1506 to PRD-1510 and PRD-1512 to PRD-1520

**Recommendation:**
1. Complete PRD-1505 to PRD-1510 specifications (add to PHASE15-S1.md)
2. Complete PRD-1512 to PRD-1520 specifications (add to PHASE15-S2.md)
3. Follow same format as PRD-1501 to PRD-1504 (excellent quality baseline)
4. Target file sizes: ~3,000 lines for S1, ~3,000 lines for S2
5. Verify each task includes: SourceRefs, Requirements, 5+ Gherkin scenarios, Validation steps, DoD

---

### 2. CRITICAL: Missing API Contracts (BLOCKER)

**Finding:** API endpoints referenced in summaries lack complete specifications (request/response DTOs, validation, error codes).

**Affected Tasks:**
- PRD-1505: SalesOrder APIs (8 endpoints listed in summary, no DTOs)
- PRD-1506: OutboundOrder entities (no API endpoints specified)
- PRD-1507: PackOrderCommand (command DTO missing)
- PRD-1508: DispatchShipmentCommand (command DTO missing)
- PRD-1512: AdjustCostCommand (command DTO missing)
- PRD-1514: Agnum export configuration (API endpoints missing)
- PRD-1516: Label printing API (API endpoints missing)
- PRD-1517: 3D visualization API (API endpoints missing)
- PRD-1519: Inter-warehouse transfer API (API endpoints missing)
- PRD-1520: Cycle count API (API endpoints missing)

**Example: PRD-1505 (SalesOrder APIs)**

Summary states:
```
- POST /api/warehouse/v1/sales-orders (create)
- POST /api/warehouse/v1/sales-orders/{id}/submit (trigger allocation)
- POST /api/warehouse/v1/sales-orders/{id}/approve (manager approval if > credit limit)
- POST /api/warehouse/v1/sales-orders/{id}/allocate (manual allocation)
- POST /api/warehouse/v1/sales-orders/{id}/release (SOFT → HARD lock)
- POST /api/warehouse/v1/sales-orders/{id}/cancel
- GET /api/warehouse/v1/sales-orders (list with filters)
- GET /api/warehouse/v1/sales-orders/{id} (details)
```

Missing specifications:
- Request DTOs: CreateSalesOrderRequest, SubmitSalesOrderRequest, ApproveSalesOrderRequest, etc.
- Response DTOs: CreateSalesOrderResponse (what does it return? Order ID? Order number? Full object?)
- Validation rules: What fields are required? What are min/max lengths? Allowed values?
- Error codes: 400 (validation failure), 409 (conflict), 404 (not found) - which scenarios?
- Idempotency: Does CreateSalesOrderRequest include CommandId?
- Pagination: How does GET /sales-orders pagination work? (offset/limit? cursor?)
- Filtering: What filters are supported? (status, customer, date range, order number?)
- Authorization: What RBAC roles are required for each endpoint? (e.g., approve requires Manager role)

**Why This is Critical:**
- Backend developers cannot implement controllers without complete DTOs
- Frontend developers cannot integrate without knowing request/response shapes
- API contract tests cannot be written
- OpenAPI/Swagger documentation incomplete
- Integration tests will fail or be incorrect

**Recommendation:**
- For each API endpoint, specify:
  ```
  POST /api/warehouse/v1/sales-orders
  Request:
  {
    "commandId": "uuid",
    "customerId": "uuid",
    "lines": [
      { "itemId": "uuid", "qty": 10, "unitPrice": 50.00 }
    ],
    "requestedDeliveryDate": "2026-02-15T00:00:00Z"
  }
  Response (201 Created):
  {
    "orderId": "uuid",
    "orderNumber": "SO-0001",
    "status": "DRAFT",
    "createdAt": "2026-02-11T10:00:00Z"
  }
  Error (400 Bad Request):
  {
    "error": "VALIDATION_FAILED",
    "message": "Customer not found",
    "field": "customerId"
  }
  Authorization: [Warehouse.SalesOrder.Create] permission
  Idempotency: CommandId (duplicate requests return cached response)
  ```
- Include validation rules (required fields, regex patterns, min/max values)
- Specify error scenarios (404, 409 conflict, 400 validation, 403 forbidden)

---

### 3. CRITICAL: Missing Event Payloads (BLOCKER)

**Finding:** Event names listed in summaries, but event schemas (payloads) are missing.

**Affected Tasks:**
- PRD-1505: SalesOrderCreated, SalesOrderAllocated, SalesOrderReleased, SalesOrderCancelled
- PRD-1507: ShipmentPacked
- PRD-1508: ShipmentDispatched
- PRD-1512: CostAdjusted (event exists in universe, but not in task spec)
- PRD-1514: AgnumExportStarted, AgnumExportCompleted, AgnumExportFailed
- PRD-1519: TransferCreated, TransferApproved, TransferExecuted, TransferCompleted
- PRD-1520: CycleCountScheduled, CountRecorded, CycleCountCompleted, StockAdjusted

**Example: PRD-1507 (Packing MVP)**

Summary states:
```
- Emit StockMoved events (PICKING_STAGING → SHIPPING)
- Emit ShipmentPacked event
```

Missing specifications:
- ShipmentPacked event payload:
  ```csharp
  public record ShipmentPacked(
    Guid ShipmentId,
    Guid OutboundOrderId,
    string ShipmentNumber,
    List<ShipmentLineDto> Lines,
    string PackedBy,
    DateTime PackedAt,
    Guid HandlingUnitId, // Shipping HU
    string TrackingNumber, // If label generated
    Guid CommandId,
    string SchemaVersion = "v1"
  );
  ```
- StockMoved event payload (from PICKING_STAGING to SHIPPING):
  - How many events? (1 per line? 1 per order?)
  - What are the From/To locations?
  - Does it include HandlingUnitId?

**Why This is Critical:**
- Event handlers cannot be implemented without knowing exact payload structure
- Projection builders need to know which fields to extract
- Event schema versioning requires defined baseline (SchemaVersion: "v1")
- Integration tests need to mock events with correct payload
- Saga handlers consume events - wrong payload = runtime errors

**Recommendation:**
- For each event, specify:
  ```csharp
  public record ShipmentPacked(
    Guid ShipmentId,
    Guid OutboundOrderId,
    string ShipmentNumber,
    List<ShipmentLineDto> Lines, // ItemId, Qty, HandlingUnitId
    string PackedBy, // User ID
    DateTime PackedAt,
    Guid ShippingHandlingUnitId,
    string TrackingNumber,
    Guid CommandId,
    string SchemaVersion = "v1"
  );
  ```
- Include all fields required for downstream projections
- Mark mandatory vs optional fields
- Specify event versioning strategy (all events include SchemaVersion)

---

### 4. CRITICAL: Missing State Machine Logic (BLOCKER)

**Finding:** State transitions mentioned in summaries, but detailed logic is missing.

**Affected Tasks:**
- PRD-1506: OutboundOrder state machine (DRAFT → ALLOCATED → PICKING → PACKED → SHIPPED → DELIVERED)
- PRD-1506: Shipment state machine (PACKING → PACKED → DISPATCHED → IN_TRANSIT → DELIVERED)
- PRD-1519: Transfer state machine (DRAFT → PENDING_APPROVAL → APPROVED → IN_TRANSIT → COMPLETED → CANCELLED)
- PRD-1520: CycleCount state machine (SCHEDULED → IN_PROGRESS → COMPLETED)

**Example: PRD-1506 (OutboundOrder State Machine)**

Summary states:
```
State machines: OutboundOrder (DRAFT → ALLOCATED → PICKING → PACKED → SHIPPED → DELIVERED),
Shipment (PACKING → PACKED → DISPATCHED → IN_TRANSIT → DELIVERED)
```

Missing specifications:
- Valid transitions:
  - DRAFT → ALLOCATED (when? trigger: AllocateOrder command? Auto via saga?)
  - ALLOCATED → PICKING (when? trigger: ReleaseForPicking command?)
  - PICKING → PACKED (when? trigger: PackOrder command, all items picked?)
  - PACKED → SHIPPED (when? trigger: DispatchShipment command?)
  - SHIPPED → DELIVERED (when? trigger: ConfirmDelivery command? Carrier webhook?)
- Invalid transitions:
  - Can you go from DRAFT → PACKED? (NO, must allocate first)
  - Can you go from SHIPPED → CANCELLED? (Depends on business rules)
- Cancellation:
  - Can you cancel from DRAFT? (YES)
  - Can you cancel from ALLOCATED? (YES, release reservation)
  - Can you cancel from PICKING? (MAYBE, if items not picked yet)
  - Can you cancel from PACKED? (NO, restock flow required)
- Edge cases:
  - What if allocation fails (out of stock)? → PENDING_STOCK status?
  - What if packing fails (item damaged)? → Rollback or create adjustment?
  - What if carrier rejects shipment? → PACKED status again?

**Why This is Critical:**
- Command handlers need to validate current status before allowing transitions
- Invalid transitions should return clear error messages
- State machine bugs cause critical operational failures (e.g., shipping unallocated orders)
- Testing requires all valid/invalid transition combinations

**Recommendation:**
- For each state machine, specify:
  ```
  OutboundOrder State Machine:

  States: DRAFT, ALLOCATED, PICKING, PACKED, SHIPPED, DELIVERED, CANCELLED

  Valid Transitions:
  - DRAFT → ALLOCATED (trigger: AllocationSaga completes)
  - DRAFT → CANCELLED (trigger: CancelOrder command)
  - ALLOCATED → PICKING (trigger: ReleaseForPicking command, reservation SOFT → HARD)
  - ALLOCATED → CANCELLED (trigger: CancelOrder command, release reservation)
  - PICKING → PACKED (trigger: PackOrder command, all items scanned)
  - PICKING → ALLOCATED (trigger: CancelPicking command, return items to AVAILABLE)
  - PACKED → SHIPPED (trigger: DispatchShipment command)
  - SHIPPED → DELIVERED (trigger: ConfirmDelivery command or carrier webhook)

  Invalid Transitions (throw exception):
  - DRAFT → PICKING (must allocate first)
  - PACKED → CANCELLED (must use RMA flow instead)
  - DELIVERED → any other status (final state)

  Business Rules:
  - Cancellation from PICKING allowed only if items not yet transferred to SHIPPING location
  - Shipment dispatch allowed only if carrier label generated
  - Delivery confirmation requires proof of delivery (signature/photo)
  ```
- Specify trigger for each transition (command, saga, event, webhook)
- Document invalid transitions with error messages
- Include business rules (when can you cancel? when can you skip steps?)

---

### 5. HIGH: Missing Validation Logic (SHOULD FIX)

**Finding:** Acceptance criteria mention validation failures, but detailed validation rules are missing.

**Affected Tasks:**
- PRD-1505: SalesOrder validation (credit limit, item availability, pricing)
- PRD-1507: Packing validation (barcode match, quantity match)
- PRD-1508: Dispatch validation (shipment status, carrier availability)
- PRD-1512: Cost adjustment validation (approval thresholds)
- PRD-1520: Cycle count validation (discrepancy thresholds)

**Example: PRD-1507 (Packing MVP)**

Gherkin scenario mentions:
```
Scenario: Packing - barcode mismatch
Scenario: Packing - quantity mismatch
```

Missing specifications:
- What exactly is validated during packing?
  - Barcode scan: must match ItemId from order line
  - Quantity: scanned qty must equal ordered qty (or allow overpick?)
  - Location: items must be in PICKING_STAGING location (not AVAILABLE)
  - Status: OutboundOrder.Status must be PICKING (not DRAFT or PACKED)
  - HandlingUnits: all HUs must be in PICKING_STAGING
- What are the error messages?
  - "Barcode mismatch: Expected SKU-001, scanned SKU-002"
  - "Quantity mismatch: Expected 10, scanned 8"
  - "Order not in PICKING status: current status is ALLOCATED"
- What are the error codes?
  - 400 (validation failure), 409 (conflict), 404 (not found)

**Recommendation:**
- For each command, specify validation rules:
  ```
  PackOrderCommand Validation:
  1. OutboundOrder.Status == PICKING (else: 409 Conflict "Order not ready for packing")
  2. All order lines scanned (else: 400 "Missing items: SKU-001, SKU-002")
  3. Scanned qty == ordered qty (else: 400 "Quantity mismatch: Line 1 expected 10, scanned 8")
  4. All HUs in PICKING_STAGING location (else: 409 "HUs not in staging area")
  5. Shipping HU packaging type valid (else: 400 "Invalid packaging: must be BOX or PALLET")
  ```
- Specify error codes and messages for each validation failure
- Document edge cases (what if qty mismatch? allow overpick? underpick?)

---

### 6. HIGH: Missing Dependency Specifications (SHOULD FIX)

**Finding:** Dependencies listed, but cross-task integration details are missing.

**Affected Dependencies:**
- PRD-1505 → PRD-1507: SalesOrder APIs create orders, packing workflow consumes orders
  - Missing: How does packing workflow query pending orders? Projection? Query endpoint?
- PRD-1507 → PRD-1508: Packing creates shipment, dispatch workflow consumes shipment
  - Missing: How does dispatch workflow find packed shipments? Projection? Query endpoint?
- PRD-1511 → PRD-1513: Valuation events feed OnHandValue projection
  - Missing: Exact event handler logic (how to join AvailableStock + Valuation?)
- PRD-1513 → PRD-1514: OnHandValue projection feeds Agnum export
  - Missing: Exact query for export (SQL? Projection API? Denormalized table?)

**Example: PRD-1507 → PRD-1508 Dependency**

PRD-1507 creates Shipment entity (status: PACKED)
PRD-1508 dispatches Shipment (status: DISPATCHED)

Missing specifications:
- How does dispatch workflow find packed shipments?
  - Option 1: Query ShipmentSummary projection (GET /api/warehouse/v1/shipments?status=PACKED)
  - Option 2: Subscribe to ShipmentPacked event (event-driven dispatch)
  - Option 3: Denormalized outbound_orders view with shipment details
- What fields are required from ShipmentSummary?
  - ShipmentId, ShipmentNumber, Carrier, TrackingNumber, PackedAt, PackedBy, OutboundOrderId
- What if shipment not found?
  - Error: 404 "Shipment not found: shipment-123"
- What if shipment already dispatched?
  - Error: 409 "Shipment already dispatched at 2026-02-11T10:00:00Z"

**Recommendation:**
- For each dependency, specify integration contract:
  ```
  PRD-1507 → PRD-1508 Integration:

  Producer: PRD-1507 (Packing MVP)
  - Emits: ShipmentPacked event
  - Creates: Shipment entity (status: PACKED)
  - Updates: ShipmentSummary projection (via event handler)

  Consumer: PRD-1508 (Dispatch MVP)
  - Query: GET /api/warehouse/v1/shipments?status=PACKED (pagination: offset/limit)
  - Response: List<ShipmentSummaryDto> (ShipmentId, ShipmentNumber, Carrier, OutboundOrderId)
  - Dispatch: DispatchShipmentCommand(CommandId, ShipmentId, DispatchedBy, VehicleId)

  Edge Cases:
  - What if shipment deleted between query and dispatch? → 404 error
  - What if shipment status changed (PACKED → CANCELLED)? → 409 conflict
  ```
- Specify query endpoints, projection names, event handlers
- Document edge cases (race conditions, data inconsistencies)

---

### 7. MEDIUM: Missing Metrics/Observability Specs (SHOULD FIX)

**Finding:** Metrics section exists in documented tasks (PRD-1501 to PRD-1504, PRD-1511), but missing in summaries for PRD-1505 to PRD-1510 and PRD-1512 to PRD-1520.

**Affected Tasks:**
- PRD-1505: SalesOrder APIs (no metrics specified)
- PRD-1507: Packing workflow (no metrics specified)
- PRD-1508: Dispatch workflow (no metrics specified)
- PRD-1512: Cost adjustment (no metrics specified)
- PRD-1514: Agnum export (no metrics specified)
- PRD-1520: Cycle counting (no metrics specified)

**Example: PRD-1507 (Packing MVP)**

Missing metrics:
- `packing_operations_total` (counter, labels: status=success|failure)
- `packing_duration_seconds` (histogram, p50/p95/p99 latency)
- `packing_validation_failures_total` (counter, labels: reason=barcode_mismatch|quantity_mismatch)
- `shipments_created_total` (counter)
- `shipping_hus_created_total` (counter)

Missing logs:
- INFO: "Packing started: Order {OrderNumber}, Items {ItemCount}, By {PackedBy}"
- INFO: "Shipment created: {ShipmentNumber}, HU {HUId}, Items {ItemCount}"
- WARN: "Packing validation warning: Quantity mismatch on Line {LineNumber}, Expected {Expected}, Scanned {Scanned}"
- ERROR: "Packing failed: Order {OrderNumber}, Error {ErrorMessage}"

**Why This Matters:**
- Observability is critical for production systems
- Metrics enable alerting (e.g., packing failure rate > 5% → alert warehouse manager)
- Logs enable debugging (e.g., why did packing fail for order SO-0123?)
- Performance monitoring (e.g., packing latency p95 > 5s → investigate)

**Recommendation:**
- For each task, specify:
  - Counters: total operations, success/failure counts
  - Histograms: duration (latency), sizes (items per order, qty per line)
  - Gauges: current state (pending orders, active shipments)
- Logs: INFO (happy path), WARN (recoverable errors), ERROR (failures)
- Correlation IDs in all logs (for distributed tracing)

---

### 8. MEDIUM: Missing Database Migrations (SHOULD FIX)

**Finding:** Entity schemas specified, but migration details are missing.

**Affected Tasks:**
- PRD-1506: OutboundOrder + Shipment entities (migration not specified)
- PRD-1517: Location coordinates (migration not specified)
- PRD-1519: Transfer entity (migration not specified)
- PRD-1520: CycleCount entity (migration not specified)

**Example: PRD-1506 (OutboundOrder + Shipment Entities)**

Summary states:
```
Database: outbound_orders, outbound_order_lines, shipments, shipment_lines tables
```

Missing specifications:
- Migration name: AddOutboundOrderAndShipment
- Tables:
  ```sql
  CREATE TABLE outbound_orders (
    id UUID PRIMARY KEY,
    order_number VARCHAR(50) NOT NULL UNIQUE,
    type VARCHAR(50) NOT NULL, -- SALES, TRANSFER, PRODUCTION_RETURN
    status VARCHAR(50) NOT NULL, -- DRAFT, ALLOCATED, PICKING, PACKED, SHIPPED, DELIVERED, CANCELLED
    customer_id UUID,
    reservation_id UUID,
    shipment_id UUID,
    created_at TIMESTAMPTZ NOT NULL,
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ NOT NULL,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN DEFAULT FALSE,
    INDEX idx_outbound_orders_order_number (order_number),
    INDEX idx_outbound_orders_status (status),
    INDEX idx_outbound_orders_customer_id (customer_id)
  );

  CREATE TABLE outbound_order_lines (
    id UUID PRIMARY KEY,
    outbound_order_id UUID NOT NULL REFERENCES outbound_orders(id),
    line_number INT NOT NULL,
    item_id UUID NOT NULL,
    qty DECIMAL(18,4) NOT NULL,
    unit_price DECIMAL(18,4),
    CHECK (qty > 0)
  );

  CREATE TABLE shipments (
    id UUID PRIMARY KEY,
    shipment_number VARCHAR(50) NOT NULL UNIQUE,
    outbound_order_id UUID NOT NULL REFERENCES outbound_orders(id),
    carrier VARCHAR(100),
    tracking_number VARCHAR(100),
    status VARCHAR(50) NOT NULL, -- PACKING, PACKED, DISPATCHED, IN_TRANSIT, DELIVERED
    packed_at TIMESTAMPTZ,
    dispatched_at TIMESTAMPTZ,
    delivered_at TIMESTAMPTZ,
    packed_by VARCHAR(200),
    dispatched_by VARCHAR(200),
    INDEX idx_shipments_shipment_number (shipment_number),
    INDEX idx_shipments_status (status),
    INDEX idx_shipments_tracking_number (tracking_number)
  );
  ```
- Indexes: Which columns need indexes? (order_number, status, customer_id, tracking_number)
- Constraints: Foreign keys? Check constraints? (qty > 0)
- Soft delete: Is is_deleted column needed?
- Audit fields: created_at, created_by, updated_at, updated_by?

**Recommendation:**
- For each entity task, specify:
  - Migration name (AddXxx)
  - CREATE TABLE statements (all columns, types, constraints)
  - Indexes (for query performance)
  - Foreign keys (relationships)
  - Check constraints (data validation)
  - Down() method (migration rollback)

---

### 9. MEDIUM: Inconsistent Terminology (SHOULD FIX)

**Finding:** Some terms used inconsistently between summaries and universe document.

**Inconsistencies:**

| Term in Summary | Term in Universe | Correct Term |
|----------------|------------------|--------------|
| "Pack Order Command" (PRD-1507) | "PackOrder" | PackOrderCommand |
| "Dispatch Shipment Command" (PRD-1508) | "DispatchShipment" | DispatchShipmentCommand |
| "PICKING_STAGING" (PRD-1507) | "SHIPPING" (Universe §4.Epic A) | Clarify: is packing done in PICKING_STAGING or SHIPPING? |
| "ShipmentPacked" event (PRD-1507) | "OutboundOrderPacked" (Universe) | Clarify: which event is correct? |

**Example: PICKING_STAGING vs SHIPPING Location**

PRD-1507 summary states:
```
Emit StockMoved events (PICKING_STAGING → SHIPPING)
```

Universe §4.Epic A states:
```
Packing consolidates items from SHIPPING location into shipment HU
```

Conflict:
- Does packing move items FROM PICKING_STAGING TO SHIPPING?
- Or does packing consolidate items ALREADY IN SHIPPING?
- If packing moves items, when do items go to SHIPPING? (during picking? or during packing?)

**Why This Matters:**
- Inconsistent terminology causes confusion during implementation
- Developers may implement wrong workflow (e.g., packing from wrong location)
- Integration tests may fail due to incorrect assumptions

**Recommendation:**
- Cross-check all task specs against universe document
- Standardize terminology:
  - Commands: always use "CommandName" + "Command" suffix (e.g., PackOrderCommand)
  - Events: always use "EventName" format (e.g., ShipmentPacked)
  - Locations: clarify packing workflow (AVAILABLE → PICKING_STAGING → SHIPPING)
  - Statuses: use UPPER_SNAKE_CASE (DRAFT, ALLOCATED, PICKING, PACKED)
- Create glossary in README-TASKS.md if needed

---

### 10. LOW: Missing Cross-Task Validation (NICE TO HAVE)

**Finding:** No explicit validation to ensure cross-task consistency (e.g., event name in PRD-1507 must match event handler in PRD-1509).

**Missing Validations:**
- Event names: ShipmentPacked in PRD-1507 must match projection handler in PRD-1509
- API routes: POST /sales-orders in PRD-1505 must match UI calls in PRD-1510
- RBAC roles: Warehouse.SalesOrder.Create in PRD-1505 must be defined in PRD-1505 or foundation
- State machine: OutboundOrder statuses in PRD-1506 must match UI filters in PRD-1510
- Database schema: outbound_orders table in PRD-1506 must match API DTOs in PRD-1505

**Recommendation:**
- Add cross-task validation checklist to DoD:
  ```
  Cross-Task Validation Checklist:
  - [ ] Event names match across producer (PRD-1507) and consumer (PRD-1509)
  - [ ] API routes match across backend (PRD-1505) and UI (PRD-1510)
  - [ ] RBAC permissions defined and enforced
  - [ ] State machine statuses consistent across entities and UI
  - [ ] Database schema matches DTOs and projections
  ```
- Run validation before sprint starts (review all task specs together)

---

## Summary of Blockers

### Must Fix (Blocker - Cannot Execute Sprint Without These)

1. **Complete Task Elaboration** - Add 6 missing tasks to PHASE15-S1.md, 9 missing tasks to PHASE15-S2.md (~4,500 lines)
2. **API Contracts** - Specify request/response DTOs, validation, error codes for all 10+ API endpoints
3. **Event Payloads** - Specify exact event schemas (csharp records) for all 15+ events
4. **State Machine Logic** - Specify valid/invalid transitions, triggers, business rules for 4+ state machines
5. **Validation Rules** - Specify detailed validation logic (what, when, error messages) for all commands

### Should Fix (High Priority - Needed for Quality)

6. **Dependency Integration** - Specify cross-task integration contracts (queries, projections, event handlers)
7. **Metrics/Observability** - Specify metrics, logs, traces for all 15 missing tasks
8. **Database Migrations** - Specify CREATE TABLE statements, indexes, constraints for 4+ entity tasks
9. **Terminology Consistency** - Standardize command/event names, location names, status values

### Nice to Have (Medium Priority - Improves Confidence)

10. **Cross-Task Validation** - Add validation checklist to ensure consistency across tasks

---

## Recommendations

### Immediate Actions (Before Sprint 1 Execution)

1. **Expand PHASE15-S1.md** (add ~1,800 lines):
   - PRD-1505: SalesOrder APIs (8 endpoints, 5-7 Gherkin scenarios, ~400 lines)
   - PRD-1506: OutboundOrder + Shipment entities (2 entities, migrations, ~350 lines)
   - PRD-1507: Packing MVP (command, handler, events, validation, ~300 lines)
   - PRD-1508: Dispatch MVP (command, handler, events, carrier integration, ~300 lines)
   - PRD-1509: Projections (3 projections, event handlers, queries, ~250 lines)
   - PRD-1510: UI screens (4 screens, workflows, error states, ~200 lines)

2. **Expand PHASE15-S2.md** (add ~2,700 lines):
   - PRD-1512: Cost Adjustment (command, handler, approval workflow, ~300 lines)
   - PRD-1513: OnHandValue Projection (projection, query API, reconciliation, ~300 lines)
   - PRD-1514: Agnum Export Config (entities, saga, scheduled job, ~350 lines)
   - PRD-1515: Agnum CSV + API (export generation, API client, reconciliation, ~300 lines)
   - PRD-1516: Label Printing (ZPL templates, TCP client, print queue, ~300 lines)
   - PRD-1517: 3D Location Coords (location schema, migration, layout API, ~300 lines)
   - PRD-1518: 3D UI (Three.js, 2D toggle, click-to-details, ~350 lines)
   - PRD-1519: Inter-Warehouse Transfers (transfer workflow, state machine, ~250 lines)
   - PRD-1520: Cycle Counting (scheduled counts, approval workflow, ~250 lines)

3. **Validation Pass**:
   - Cross-check all API routes (PRD-1505 APIs must match PRD-1510 UI calls)
   - Cross-check all events (PRD-1507 ShipmentPacked must match PRD-1509 projection handler)
   - Cross-check all state machines (PRD-1506 OutboundOrder statuses must match PRD-1510 UI filters)
   - Standardize terminology (Commands, Events, Locations, Statuses)

### Alternative Approach (If Time Constrained)

If completing all 15 tasks before sprint start is not feasible:

**Option 1: Prioritize Critical Path**
- Fully elaborate PRD-1505 to PRD-1508 (sales orders + outbound workflow) - CRITICAL for Sprint 1
- Defer PRD-1509 to PRD-1510 (projections + UI can be elaborated during backend implementation)
- Fully elaborate PRD-1512 to PRD-1515 (valuation + Agnum) - CRITICAL for Sprint 2
- Defer PRD-1516 to PRD-1520 (label printing, 3D viz, transfers, cycle count can wait)

**Option 2: Just-In-Time Elaboration**
- Elaborate tasks 1-2 days before implementation starts
- Risk: Less time for review, higher chance of errors/inconsistencies
- Benefit: Faster sprint start, can adjust based on Sprint 1 learnings

**Recommended:** Option 1 (prioritize critical path, defer non-blockers)

---

## Positive Findings

Despite the incompleteness, the **quality** of the documented tasks (PRD-1501 to PRD-1504, PRD-1511) is **excellent**:

✅ **Format & Structure**
- Clear sections: Context, Scope, Requirements, Acceptance Criteria, Implementation Notes, Validation, DoD
- Follows template format from README-TASKS.md
- Consistent structure across all 5 documented tasks

✅ **Requirements Quality**
- Functional requirements are specific and testable
- Non-functional requirements include performance, storage, retention
- Data model includes CREATE TABLE statements with indexes, constraints
- API changes include complete DTOs (csharp records)

✅ **Acceptance Criteria (Gherkin)**
- 5-7 scenarios per task (exceeds minimum of 3)
- Includes negative test cases (validation failures, concurrency conflicts)
- Scenarios are specific and testable (Given/When/Then)
- Edge cases covered (duplicate commands, concurrent requests, saga replay)

✅ **Implementation Notes**
- Technology choices specified (MediatR, Marten, EF Core, Hangfire)
- Design patterns mentioned (IPipelineBehavior, saga checkpoints, transactions)
- Edge cases documented (error handling, cleanup jobs)

✅ **Validation & Checks**
- Local testing commands (dotnet test, psql queries, curl tests)
- Metrics specified (Prometheus format, labels)
- Logs specified (INFO/WARN/ERROR levels, structured fields)
- Backwards compatibility checks

✅ **Definition of Done**
- 15-20 checklist items per task
- Covers: entity creation, migration, tests, code review, documentation
- Specific and actionable

✅ **Source References**
- References to prod-ready-universe.md sections
- Traces requirements back to source of truth

**Conclusion:** If the remaining 15 tasks match the quality of the first 5 tasks, the sprint packs will be **production-grade execution packs**. The blocker is simply **quantity** (75% of tasks missing), not **quality** (existing tasks are excellent).

---

## Final Verdict

**NOT OK** - Sprint 1 and Sprint 2 task packs are incomplete and not executable as-is.

**Gap:** 15 out of 20 tasks (75%) lack detailed specifications. Estimated ~4,500 lines of missing content.

**Impact:** Developers cannot execute tasks PRD-1505 to PRD-1510 or PRD-1512 to PRD-1520 without re-reading the full universe document for each task.

**Recommendation:** Complete elaboration of remaining 15 tasks before sprint execution begins. Prioritize critical path (PRD-1505 to PRD-1508, PRD-1512 to PRD-1515) if time constrained.

---

## Next Steps

1. **Review this report** with stakeholders
2. **Decide:** Complete all 15 tasks now, or prioritize critical path (Option 1), or defer (Option 2)?
3. **Assign elaboration work** (estimated 2-3 days for 1 solution architect)
4. **Re-run quality gate review** after elaboration complete
5. **Begin sprint execution** only after verdict = "OK"

---

**Review Complete**
**Date:** February 11, 2026
**Reviewer:** Claude (Senior Solution Architect + QA Lead)
**Verdict:** NOT OK
**Estimated Effort to Fix:** 2-3 days (elaborate 15 tasks @ ~300 lines each)
