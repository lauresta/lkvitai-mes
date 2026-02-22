# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 6 (Execution Pack)

**Version:** 1.0
**Date:** February 12, 2026
**Sprint:** Phase 1.5 Sprint 6
**Source:** prod-ready-universe.md, prod-ready-tasks-progress.md
**Status:** Ready for Execution

---

## Sprint Overview

**Sprint Goal:** Deliver advanced warehouse capabilities to maximize operational efficiency and enable complex workflows (wave picking, cross-docking, RMA, multi-level QC, HU hierarchy, serial tracking, analytics).

**Sprint Duration:** 2 weeks
**Total Tasks:** 20
**Estimated Effort:** 18 days

**Focus Areas:**
1. **Wave Picking:** Batch picking optimization for high-volume operations
2. **Cross-Docking:** Fast-track shipments (receive → ship without storage)
3. **Multi-Level QC:** Enhanced quality control with checklists, defect tracking, photo attachments
4. **Returns/RMA:** Complete returns workflow (create → receive → inspect → disposition)
5. **HU Hierarchy:** Nested handling units (pallet → box → item tracking)
6. **Serial Tracking:** Individual unit lifecycle management
7. **Analytics:** Fulfillment KPIs, defect analytics, late shipment reports
8. **Testing & Documentation:** Contract tests, performance regression, operator training

**Dependencies:**
- Sprint 5 complete (PRD-1561 to PRD-1580)
- Production hardening complete (reliability, performance, observability)

---

## Sprint 6 Task Index

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-1581 | Wave Picking | Wave Creation & Batch Assignment | L | PRD-1505,1530 | Backend/API | Universe §4.Epic H |
| PRD-1582 | Wave Picking | Route Optimization Algorithm | M | PRD-1581 | Backend/API | Universe §4.Epic H |
| PRD-1583 | Wave Picking | Wave Picking UI & Execution | M | PRD-1581,1582 | UI | Universe §4.Epic H |
| PRD-1584 | Cross-Docking | Cross-Dock Workflow & Routing | M | PRD-1523,1524 | Backend/API | Universe §4.Epic I |
| PRD-1585 | Cross-Docking | Cross-Dock UI & Tracking | S | PRD-1584 | UI | Universe §4.Epic I |
| PRD-1586 | QC | QC Checklist Templates | M | PRD-1524 | Backend/API | Universe §4.Epic J |
| PRD-1587 | QC | QC Defect Taxonomy & Tracking | M | PRD-1586 | Backend/API | Universe §4.Epic J |
| PRD-1588 | QC | QC Attachments & Photo Upload | S | PRD-1587 | Backend/API | Universe §4.Epic J |
| PRD-1589 | RMA | RMA Creation & Workflow | L | PRD-1528 | Backend/API | Universe §4.Epic N |
| PRD-1590 | RMA | RMA Inspection & Disposition | M | PRD-1589 | Backend/API | Universe §4.Epic N |
| PRD-1591 | RMA | RMA UI & Customer Portal | M | PRD-1589,1590 | UI | Universe §4.Epic N |
| PRD-1592 | HU Hierarchy | HU Split/Merge Operations | M | None | Backend/API | Universe §4.Epic K |
| PRD-1593 | HU Hierarchy | Nested HU Validation & Tracking | M | PRD-1592 | Backend/API | Universe §4.Epic K |
| PRD-1594 | Serial Tracking | Serial Number Lifecycle Management | L | None | Backend/API | Universe §4.Epic L |
| PRD-1595 | Serial Tracking | Serial Status Tracking & Reporting | M | PRD-1594 | Backend/API | Universe §4.Epic L |
| PRD-1596 | Analytics | Fulfillment KPIs Dashboard | M | PRD-1528,1532 | UI/Backend | Universe §4.Epic O |
| PRD-1597 | Analytics | QC Defects & Late Shipments Analytics | S | PRD-1587 | UI/Backend | Universe §4.Epic O |
| PRD-1598 | Testing | Contract Tests for External APIs | M | PRD-1538,1572 | Integration | Universe §5.Integration |
| PRD-1599 | Testing | Performance Regression Test Suite | M | PRD-1580 | QA | Universe §5.Performance |
| PRD-1600 | Documentation | Operator Training Videos & Guides | L | All above | QA | Universe §5.Documentation |

**Total Effort:** 18 days (1 developer, 3.6 weeks)

---

## Task PRD-1581: Wave Creation & Batch Assignment

**Epic:** Wave Picking
**Phase:** 1.5
**Sprint:** 6
**Estimate:** L (2 days)
**OwnerType:** Backend/API
**Dependencies:** PRD-1505 (SalesOrder), PRD-1530 (Picking UI)
**SourceRefs:** Universe §4.Epic H (Wave Picking)

### Context

- Current picking: 1 order at a time → operator walks same route multiple times (inefficient)
- Wave picking: Batch 10+ orders → pick all items in 1 trip → 3x faster
- Need wave creation logic (group orders by zone, priority, delivery date)
- Need operator assignment (assign wave to picker)

### Scope

**In Scope:**
- Wave entity (group multiple orders for batch picking)
- Wave creation API (manual or auto-triggered)
- Wave assignment (assign to operator)
- Wave status lifecycle (CREATED → ASSIGNED → PICKING → COMPLETED)
- Wave pick list (all items sorted by location)

**Out of Scope:**
- Real-time wave optimization (dynamic re-assignment deferred to Phase 2)
- Multi-warehouse wave picking (single warehouse only)

### Requirements

**Functional:**
1. Wave entity:
   ```csharp
   public class Wave {
     public Guid Id { get; set; }
     public string WaveNumber { get; set; } // WAVE-0001
     public WaveStatus Status { get; set; } // CREATED, ASSIGNED, PICKING, COMPLETED, CANCELLED
     public DateTime CreatedAt { get; set; }
     public DateTime? AssignedAt { get; set; }
     public DateTime? CompletedAt { get; set; }
     public string AssignedOperator { get; set; }
     public List<Guid> OrderIds { get; set; } // SalesOrder IDs in wave
     public int TotalLines { get; set; } // Total pick lines
     public int CompletedLines { get; set; }
   }
   ```
2. Wave creation logic:
   - Group orders by: Zone (storage zone), Priority (urgent orders first), Delivery date (same-day first)
   - Max orders per wave: 10 (configurable)
   - Max lines per wave: 50 (configurable)
3. Wave assignment: Assign wave to operator (manual or auto-assign to available picker)
4. Wave pick list: All items sorted by location (aisle → rack → level → bin)

**Non-Functional:**
1. Performance: Wave creation < 2 seconds for 10 orders
2. Scalability: Support 100+ waves per day
3. Reliability: Wave creation idempotent (same orders → same wave)

**Data Model:**
```sql
CREATE TABLE waves (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  wave_number VARCHAR(50) UNIQUE NOT NULL,
  status VARCHAR(20) NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT NOW(),
  assigned_at TIMESTAMP,
  completed_at TIMESTAMP,
  assigned_operator VARCHAR(100),
  order_ids JSONB NOT NULL, -- Array of SalesOrder IDs
  total_lines INT NOT NULL,
  completed_lines INT NOT NULL DEFAULT 0
);

CREATE INDEX idx_waves_status ON waves(status);
CREATE INDEX idx_waves_assigned_operator ON waves(assigned_operator);
```

**API:**
```http
POST /api/warehouse/v1/waves
Content-Type: application/json
Authorization: Bearer {token}

{
  "orderIds": ["order-001", "order-002", "order-003"],
  "assignedOperator": "picker-01"
}

Response 200:
{
  "id": "wave-001",
  "waveNumber": "WAVE-0001",
  "status": "ASSIGNED",
  "totalLines": 25,
  "pickList": [
    {"itemId": 1, "qty": 10, "location": "A1-B1"},
    {"itemId": 2, "qty": 5, "location": "A1-B2"}
  ]
}
```

### Acceptance Criteria

```gherkin
Feature: Wave Creation & Batch Assignment

Scenario: Create wave from multiple orders
  Given 3 SalesOrders with status ALLOCATED
  When POST /api/warehouse/v1/waves with orderIds
  Then Wave created with status CREATED
  And Wave includes all 3 orders
  And Wave pick list generated (sorted by location)

Scenario: Assign wave to operator
  Given Wave "WAVE-0001" with status CREATED
  When assign to operator "picker-01"
  Then Wave status updated to ASSIGNED
  And AssignedOperator = "picker-01"
  And AssignedAt timestamp recorded

Scenario: Wave pick list sorted by location
  Given Wave with items at locations: A3-B1, A1-B2, A2-B3
  When pick list generated
  Then items sorted: A1-B2, A2-B3, A3-B1 (aisle order)

Scenario: Wave creation with max limits
  Given 15 orders available
  When create wave with max 10 orders
  Then Wave includes first 10 orders (by priority)
  And remaining 5 orders available for next wave

Scenario: Wave idempotency
  Given Wave creation command with CommandId "cmd-001"
  When command executed 3 times
  Then only 1 Wave created
  And all 3 responses return same WaveId
```

### Validation / Checks

```bash
# Create wave
curl -X POST http://localhost:5000/api/warehouse/v1/waves \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "orderIds": ["order-001", "order-002"],
    "assignedOperator": "picker-01"
  }'

# Expected: 200 OK, Wave created

# Verify wave in database
psql -d warehouse -c "SELECT * FROM waves WHERE wave_number = 'WAVE-0001';"
# Expected: 1 row, status=ASSIGNED, order_ids=[order-001, order-002]
```

### Definition of Done

- [ ] Migration created: AddWaves
- [ ] waves table created with indexes
- [ ] Wave entity and WaveStatus enum implemented
- [ ] Wave creation API implemented
- [ ] Wave assignment logic implemented
- [ ] Wave pick list generation (sorted by location)
- [ ] Unit tests: Wave creation, assignment, pick list sorting
- [ ] Integration tests: Create wave, assign operator, verify pick list
- [ ] API documentation updated (Swagger)
- [ ] Code review approved
- [ ] No regressions in existing tests

---

## Task PRD-1582: Route Optimization Algorithm

**Epic:** Wave Picking
**Phase:** 1.5
**Sprint:** 6
**Estimate:** M (1 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1581 (Wave creation)
**SourceRefs:** Universe §4.Epic H (Wave Picking)

### Context

- Wave pick list must be sorted by location to minimize travel distance
- Current sorting: Simple aisle order (A1, A2, A3)
- Need smarter routing: Consider rack, level, bin within aisle
- Goal: 30% reduction in pick time vs single-order picking

### Scope

**In Scope:**
- Route optimization algorithm (sort pick list by location)
- Zone-based routing (pick all items in zone before moving to next)
- Benchmark: Measure pick time reduction

**Out of Scope:**
- Real-time operator location tracking (RTLS deferred to Phase 2)
- Dynamic re-routing (adjust route mid-pick deferred to Phase 2)

### Requirements

**Functional:**
1. Route optimization algorithm:
   - Parse location code: "A3-C6-L2B4" → Aisle=A3, Rack=C6, Level=L2, Bin=B4
   - Sort by: Aisle (ascending), Rack (ascending), Level (descending for efficiency), Bin (ascending)
   - Example: A1-C1-L3B1, A1-C1-L2B2, A1-C2-L3B1, A2-C1-L3B1
2. Zone-based routing:
   - Group items by zone (STORAGE-A, STORAGE-B)
   - Pick all items in zone before moving to next
3. Benchmark: Compare pick time (wave vs single-order)

**Non-Functional:**
1. Performance: Route optimization < 100ms for 50 items
2. Accuracy: Route reduces travel distance by 30%

**Algorithm:**
```csharp
public List<PickLine> OptimizeRoute(List<PickLine> pickLines)
{
    return pickLines
        .OrderBy(p => ParseAisle(p.Location))
        .ThenBy(p => ParseRack(p.Location))
        .ThenByDescending(p => ParseLevel(p.Location)) // Top-down picking
        .ThenBy(p => ParseBin(p.Location))
        .ToList();
}

private string ParseAisle(string location) => location.Split('-')[0]; // "A3"
private string ParseRack(string location) => location.Split('-')[1]; // "C6"
private int ParseLevel(string location) => int.Parse(location.Split('-')[2].Substring(1, 1)); // "L2" → 2
private string ParseBin(string location) => location.Split('-')[2].Substring(2); // "B4"
```

### Acceptance Criteria

```gherkin
Feature: Route Optimization Algorithm

Scenario: Sort pick list by location
  Given pick list with locations: A3-C1-L2B1, A1-C2-L3B2, A2-C1-L1B3
  When route optimization applied
  Then pick list sorted: A1-C2-L3B2, A2-C1-L1B3, A3-C1-L2B1

Scenario: Top-down picking (level descending)
  Given pick list with locations: A1-C1-L1B1, A1-C1-L3B1, A1-C1-L2B1
  When route optimization applied
  Then pick list sorted: A1-C1-L3B1, A1-C1-L2B1, A1-C1-L1B1 (top to bottom)

Scenario: Zone-based routing
  Given pick list with zones: STORAGE-B, STORAGE-A, STORAGE-B
  When route optimization applied
  Then items grouped by zone: STORAGE-A (all items), STORAGE-B (all items)

Scenario: Benchmark pick time reduction
  Given 10 orders with 50 total lines
  When picked as wave (optimized route)
  Then pick time = 15 minutes
  And single-order pick time = 22 minutes (baseline)
  And reduction = 32% (meets 30% target)
```

### Validation / Checks

```bash
# Run route optimization tests
dotnet test --filter "FullyQualifiedName~RouteOptimizationTests"

# Expected: Tests pass (sort order correct, benchmark meets target)
```

### Definition of Done

- [ ] Route optimization algorithm implemented
- [ ] Location parsing logic (aisle, rack, level, bin)
- [ ] Sort logic (aisle → rack → level desc → bin)
- [ ] Zone-based routing implemented
- [ ] Unit tests: Sort order, zone grouping
- [ ] Benchmark test: Pick time reduction ≥ 30%
- [ ] Documentation: Route optimization algorithm explained
- [ ] Code review approved
- [ ] No regressions in existing tests

---

## Task PRD-1583: Wave Picking UI & Execution

**Epic:** Wave Picking
**Phase:** 1.5
**Sprint:** 6
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** PRD-1581 (Wave creation), PRD-1582 (Route optimization)
**SourceRefs:** Universe §4.Epic H (Wave Picking)

### Context

- Need UI for operators to execute wave picks
- Show wave pick list (all items sorted by location)
- Scan item, confirm qty, mark complete
- Track progress (completed lines / total lines)

### Scope

**In Scope:**
- Wave list page (show active waves, assigned operator)
- Wave pick list page (all items sorted by location)
- Pick execution (scan item, confirm qty, mark complete)
- Wave completion (all items picked, split into orders)

**Out of Scope:**
- Mobile app (Blazor Server responsive design for tablets)
- Offline mode (deferred to Phase 2)

### Requirements

**Functional:**
1. Wave list page (`/warehouse/waves`):
   - Show waves: Wave Number, Status, Assigned Operator, Total Lines, Completed Lines, Actions
   - Filter by: Status (ASSIGNED, PICKING), Operator
   - Actions: Start Picking (if ASSIGNED), View Details
2. Wave pick list page (`/warehouse/waves/{id}/pick`):
   - Show pick list: Item, Qty, Location, Status (PENDING, COMPLETED)
   - Sort by location (optimized route)
   - Scan item barcode → highlight item
   - Confirm qty → mark complete
   - Progress bar: Completed lines / Total lines
3. Wave completion:
   - When all lines completed → Wave status = COMPLETED
   - Split picked items into orders (post-pick sorting)

**Non-Functional:**
1. UX: Large buttons for tablet use (touch-friendly)
2. Performance: Pick list loads in < 1 second
3. Accessibility: Keyboard navigation, screen reader support

**UI Mockup:**
```
Wave Pick List - WAVE-0001
Progress: 15 / 25 lines completed (60%)

[Scan Item Barcode: ____________] [Scan]

Pick List (sorted by location):
┌─────────────────────────────────────────────────────┐
│ ✅ A1-C1-L3B1 | RM-0001 | Qty: 10 | COMPLETED      │
│ ⏳ A1-C1-L2B2 | RM-0002 | Qty: 5  | PENDING        │
│ ⏳ A1-C2-L3B1 | FG-0001 | Qty: 20 | PENDING        │
└─────────────────────────────────────────────────────┘

[Complete Wave]
```

### Acceptance Criteria

```gherkin
Feature: Wave Picking UI & Execution

Scenario: View wave list
  Given 3 waves: WAVE-0001 (ASSIGNED), WAVE-0002 (PICKING), WAVE-0003 (COMPLETED)
  When navigate to /warehouse/waves
  Then see 3 waves in list
  And WAVE-0001 has "Start Picking" button
  And WAVE-0002 has "Continue Picking" button
  And WAVE-0003 has "View Details" button

Scenario: Start wave picking
  Given Wave "WAVE-0001" with status ASSIGNED
  When click "Start Picking"
  Then navigate to /warehouse/waves/wave-001/pick
  And Wave status updated to PICKING
  And pick list displayed (sorted by location)

Scenario: Scan item and mark complete
  Given Wave pick list with item "RM-0001" at location "A1-C1-L3B1"
  When scan barcode "RM-0001"
  And confirm qty 10
  Then item marked COMPLETED (green checkmark)
  And progress updated: 1 / 25 lines completed

Scenario: Complete wave
  Given Wave with all 25 lines completed
  When click "Complete Wave"
  Then Wave status updated to COMPLETED
  And picked items split into orders
  And navigate to /warehouse/waves (list page)
```

### Validation / Checks

```bash
# Manual UI test
# 1. Navigate to http://localhost:5000/warehouse/waves
# 2. Click "Start Picking" on WAVE-0001
# 3. Scan item barcode (or enter manually)
# 4. Confirm qty
# 5. Verify item marked complete
# 6. Complete all lines
# 7. Click "Complete Wave"
# 8. Verify wave status = COMPLETED
```

### Definition of Done

- [ ] Wave list page implemented (`/warehouse/waves`)
- [ ] Wave pick list page implemented (`/warehouse/waves/{id}/pick`)
- [ ] Pick execution logic (scan, confirm, mark complete)
- [ ] Progress tracking (completed / total lines)
- [ ] Wave completion logic (split into orders)
- [ ] UI responsive (tablet-friendly)
- [ ] Manual testing complete (full wave pick workflow)
- [ ] Code review approved
- [ ] No regressions in existing tests

---

## Task PRD-1584: Cross-Dock Workflow & Routing

**Epic:** Cross-Docking | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1523, PRD-1524 | **SourceRefs:** Universe §4.Epic I

### Context
Cross-docking: Receive goods → ship immediately without storage. Reduces handling time and storage costs.

### Scope
**In Scope:** Cross-dock flag on InboundShipment, auto-routing (RECEIVING → SHIPPING), cross-dock matching (inbound → outbound), cross-dock report
**Out of Scope:** Automated cross-dock scheduling (manual matching only)

### Requirements
**Functional:** 1) Add IsCrossDock flag to InboundShipment, 2) Auto-route: RECEIVING → SHIPPING (skip putaway), 3) Match inbound to outbound orders, 4) Cross-dock report (items, time saved)
**Non-Functional:** Cross-dock processing < 5 minutes (receive → ship)

### Acceptance Criteria
```gherkin
Scenario: Cross-dock inbound shipment
  Given InboundShipment with IsCrossDock = true
  When goods received
  Then stock moved to SHIPPING location (not RECEIVING)
  And putaway task not created

Scenario: Match inbound to outbound
  Given OutboundOrder for item "RM-0001" qty 10
  And InboundShipment receiving "RM-0001" qty 10 (cross-dock)
  When goods received
  Then inbound matched to outbound
  And shipment ready for dispatch
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/inbound-shipments \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"supplierId": "sup-001", "isCrossDock": true, "lines": [...]}'
# Expected: Stock routed to SHIPPING
```

### Definition of Done
- [ ] IsCrossDock flag added to InboundShipment
- [ ] Auto-routing logic (RECEIVING → SHIPPING)
- [ ] Cross-dock matching (inbound → outbound)
- [ ] Cross-dock report
- [ ] Tests pass

---
## Task PRD-1585: Cross-Dock UI & Tracking

**Epic:** Cross-Docking | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** S (0.5 day) | **OwnerType:** UI
**Dependencies:** PRD-1584 | **SourceRefs:** Universe §4.Epic I

### Context
Need UI to track cross-dock operations and show pending matches.

### Scope
**In Scope:** Cross-dock indicator on receiving UI, cross-dock dashboard (pending matches, completed), cross-dock status tracking
**Out of Scope:** Real-time updates (SignalR deferred)

### Requirements
**Functional:** 1) Cross-dock indicator on receiving page, 2) Cross-dock dashboard (pending matches, completed cross-docks), 3) Cross-dock status (PENDING, MATCHED, SHIPPED)
**Non-Functional:** Dashboard loads < 1 second

### Acceptance Criteria
```gherkin
Scenario: Cross-dock indicator
  Given InboundShipment with IsCrossDock = true
  When view receiving page
  Then see cross-dock badge: "CROSS-DOCK"

Scenario: Cross-dock dashboard
  Given 5 pending cross-dock matches
  When navigate to /warehouse/cross-dock
  Then see 5 pending matches (inbound → outbound)
  And see completed cross-docks (last 7 days)
```

### Validation
```bash
# Manual UI test
# Navigate to /warehouse/cross-dock
# Verify pending matches shown
```

### Definition of Done
- [ ] Cross-dock indicator on receiving UI
- [ ] Cross-dock dashboard page
- [ ] Cross-dock status tracking
- [ ] Manual testing complete

---
## Task PRD-1586: QC Checklist Templates

**Epic:** QC | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1524 | **SourceRefs:** Universe §4.Epic J

### Context
Current QC is simple pass/fail. Need structured checklists for detailed inspections.

### Scope
**In Scope:** QCChecklistTemplate entity, checklist items (step name, pass/fail, notes), checklist execution, template assignment to item categories
**Out of Scope:** Photo attachments (covered in PRD-1588)

### Requirements
**Functional:** 1) QCChecklistTemplate entity (name, items), 2) ChecklistItem (step name, required, pass/fail), 3) Assign template to item category, 4) Execute checklist during QC inspection
**Non-Functional:** Checklist execution < 30 seconds

### Acceptance Criteria
```gherkin
Scenario: Create QC checklist template
  Given admin user
  When create template "Electronics Inspection"
  And add items: "Check packaging", "Verify serial number", "Power on test"
  Then template saved
  And assigned to category "Electronics"

Scenario: Execute QC checklist
  Given item "LAPTOP-001" with template "Electronics Inspection"
  When QC inspector executes checklist
  Then inspector completes each step (pass/fail)
  And adds notes for failed steps
  And checklist result saved
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/qc/templates \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name": "Electronics Inspection", "items": [...]}'
```

### Definition of Done
- [ ] QCChecklistTemplate entity created
- [ ] ChecklistItem entity created
- [ ] Template assignment to categories
- [ ] Checklist execution API
- [ ] Tests pass

---
## Task PRD-1587: QC Defect Taxonomy & Tracking

**Epic:** QC | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1586 | **SourceRefs:** Universe §4.Epic J

### Context
Need structured defect tracking for quality analysis and supplier scorecards.

### Scope
**In Scope:** QCDefect entity, defect taxonomy (DAMAGED, EXPIRED, MISLABELED, CONTAMINATED), defect severity (MINOR, MAJOR, CRITICAL), defect report
**Out of Scope:** Automated defect detection (AI deferred)

### Requirements
**Functional:** 1) QCDefect entity (type, severity, description, item, supplier), 2) Defect taxonomy (4 types), 3) Severity levels (3 levels), 4) Defect report (top defects, supplier scorecard)
**Non-Functional:** Defect tracking overhead < 5 seconds per inspection

### Acceptance Criteria
```gherkin
Scenario: Record QC defect
  Given QC inspection for item "RM-0001"
  When defect found: type=DAMAGED, severity=MAJOR
  Then defect recorded with description
  And linked to item and supplier

Scenario: Defect report
  Given 100 defects recorded (last 30 days)
  When generate defect report
  Then report shows: top defect types, defects by supplier, defects by severity
  And supplier scorecard (defect rate per supplier)
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/qc/defects \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"type": "DAMAGED", "severity": "MAJOR", "itemId": 1, "supplierId": "sup-001"}'
```

### Definition of Done
- [ ] QCDefect entity created
- [ ] Defect taxonomy (4 types)
- [ ] Severity levels (3 levels)
- [ ] Defect report API
- [ ] Supplier scorecard
- [ ] Tests pass

---
## Task PRD-1588: QC Attachments & Photo Upload

**Epic:** QC | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** S (0.5 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1587 | **SourceRefs:** Universe §4.Epic J

### Context
QC inspectors need to attach photos of defects for evidence and analysis.

### Scope
**In Scope:** Photo upload to blob storage (Azure Blob or local filesystem), attach photos to QC defects, photo viewer in UI
**Out of Scope:** Image recognition (AI deferred)

### Requirements
**Functional:** 1) Photo upload API (multipart/form-data), 2) Store photos in blob storage, 3) Link photos to QCDefect, 4) Photo viewer in UI
**Non-Functional:** Photo upload < 5 seconds for 5MB image

### Acceptance Criteria
```gherkin
Scenario: Upload defect photo
  Given QC defect recorded
  When inspector uploads photo (JPEG, 2MB)
  Then photo stored in blob storage
  And photo linked to defect
  And photo URL returned

Scenario: View defect photos
  Given defect with 3 photos
  When view defect details
  Then see 3 photo thumbnails
  And clicking thumbnail opens full-size photo
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/qc/defects/123/photos \
  -H "Authorization: Bearer $TOKEN" \
  -F "photo=@defect.jpg"
# Expected: Photo uploaded, URL returned
```

### Definition of Done
- [ ] Photo upload API
- [ ] Blob storage integration
- [ ] Link photos to defects
- [ ] Photo viewer UI
- [ ] Tests pass

---
## Task PRD-1589: RMA Creation & Workflow

**Epic:** RMA | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** L (2 days) | **OwnerType:** Backend/API
**Dependencies:** PRD-1528 | **SourceRefs:** Universe §4.Epic N

### Context
Need complete returns workflow: customer requests return → RMA created → goods received → inspected → disposition.

### Scope
**In Scope:** RMA entity, RMA creation API, RMA status lifecycle (CREATED → RECEIVED → INSPECTED → COMPLETED), RMA notification
**Out of Scope:** Customer self-service portal (Phase 2)

### Requirements
**Functional:** 1) RMA entity (rmaNumber, orderId, returnReason, status), 2) RMA creation API (customer service creates), 3) RMA status lifecycle, 4) RMA notification (warehouse notified)
**Non-Functional:** RMA creation < 2 seconds

### Acceptance Criteria
```gherkin
Scenario: Create RMA
  Given SalesOrder "ORD-001" shipped
  When customer service creates RMA
  And return reason = "DEFECTIVE"
  Then RMA created with status CREATED
  And RMA number generated: "RMA-0001"
  And warehouse notified

Scenario: RMA status lifecycle
  Given RMA "RMA-0001" with status CREATED
  When goods received
  Then RMA status = RECEIVED
  When inspection completed
  Then RMA status = INSPECTED
  When disposition applied
  Then RMA status = COMPLETED
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/rma \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"orderId": "ord-001", "returnReason": "DEFECTIVE", "lines": [...]}'
```

### Definition of Done
- [ ] RMA entity created
- [ ] RMA creation API
- [ ] RMA status lifecycle
- [ ] RMA notification
- [ ] Tests pass

---
## Task PRD-1590: RMA Inspection & Disposition

**Epic:** RMA | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1589 | **SourceRefs:** Universe §4.Epic N

### Context
After RMA received, need inspection and disposition (restock, scrap, return to supplier).

### Scope
**In Scope:** RMA inspection workflow, disposition options (RESTOCK, SCRAP, RETURN_TO_SUPPLIER), credit calculation, stock routing
**Out of Scope:** Automated disposition (manual decision only)

### Requirements
**Functional:** 1) RMA inspection (inspector reviews returned goods), 2) Disposition: RESTOCK (move to RECEIVING), SCRAP (move to SCRAP), RETURN_TO_SUPPLIER (move to RETURN_TO_SUPPLIER), 3) Credit calculation (refund amount, restocking fee)
**Non-Functional:** Inspection < 10 minutes per RMA

### Acceptance Criteria
```gherkin
Scenario: RMA inspection - Restock
  Given RMA "RMA-0001" received
  When inspector inspects goods
  And disposition = RESTOCK
  Then stock moved to RECEIVING location
  And full credit issued to customer

Scenario: RMA inspection - Scrap
  Given RMA "RMA-0001" received
  When inspector finds goods damaged
  And disposition = SCRAP
  Then stock moved to SCRAP location
  And partial credit issued (minus restocking fee)

Scenario: Credit calculation
  Given RMA with original order value $100
  When disposition = RESTOCK
  Then credit = $100 (full refund)
  When disposition = SCRAP
  Then credit = $80 (20% restocking fee)
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/rma/rma-001/inspect \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"disposition": "RESTOCK", "notes": "Goods in good condition"}'
```

### Definition of Done
- [ ] RMA inspection API
- [ ] Disposition options (3 types)
- [ ] Credit calculation logic
- [ ] Stock routing by disposition
- [ ] Tests pass

---
## Task PRD-1591: RMA UI & Customer Portal

**Epic:** RMA | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** UI
**Dependencies:** PRD-1589, PRD-1590 | **SourceRefs:** Universe §4.Epic N

### Context
Need UI for warehouse staff to manage RMAs and for customer service to track returns.

### Scope
**In Scope:** RMA list page, RMA detail page, RMA inspection form, RMA status tracking
**Out of Scope:** Customer self-service portal (Phase 2)

### Requirements
**Functional:** 1) RMA list page (show all RMAs, filter by status), 2) RMA detail page (return items, inspection result, credit), 3) RMA inspection form (disposition, notes), 4) RMA status tracking
**Non-Functional:** Page loads < 1 second

### Acceptance Criteria
```gherkin
Scenario: View RMA list
  Given 10 RMAs (5 RECEIVED, 3 INSPECTED, 2 COMPLETED)
  When navigate to /warehouse/rma
  Then see 10 RMAs in list
  And filter by status

Scenario: Inspect RMA
  Given RMA "RMA-0001" with status RECEIVED
  When click "Inspect"
  Then see inspection form
  And select disposition (RESTOCK, SCRAP, RETURN_TO_SUPPLIER)
  And add notes
  And click "Complete Inspection"
  Then RMA status = INSPECTED
```

### Validation
```bash
# Manual UI test
# Navigate to /warehouse/rma
# Click "Inspect" on RMA
# Complete inspection form
# Verify RMA status updated
```

### Definition of Done
- [ ] RMA list page
- [ ] RMA detail page
- [ ] RMA inspection form
- [ ] RMA status tracking
- [ ] Manual testing complete

---
## Task PRD-1592: HU Split/Merge Operations

**Epic:** HU Hierarchy | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** None | **SourceRefs:** Universe §4.Epic K

### Context
Need to split pallets into boxes or merge boxes into pallets for flexible handling.

### Scope
**In Scope:** HU split operation (create child HUs from parent), HU merge operation (consolidate child HUs into parent), HU hierarchy tracking
**Out of Scope:** Automated split/merge rules (manual only)

### Requirements
**Functional:** 1) Split HU: Create N child HUs from parent (split pallet into boxes), 2) Merge HU: Consolidate child HUs into parent (merge boxes into pallet), 3) Track parent-child relationships
**Non-Functional:** Split/merge operations < 5 seconds

### Acceptance Criteria
```gherkin
Scenario: Split pallet into boxes
  Given pallet HU "PAL-001" with 100 units
  When split into 10 boxes (10 units each)
  Then 10 child HUs created: "BOX-001" to "BOX-010"
  And parent HU "PAL-001" status = SPLIT
  And each child HU has ParentHUId = "PAL-001"

Scenario: Merge boxes into pallet
  Given 10 box HUs: "BOX-001" to "BOX-010"
  When merge into pallet "PAL-002"
  Then pallet HU "PAL-002" created with 100 units
  And child HUs status = MERGED
  And parent HU "PAL-002" has 10 children
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/handling-units/PAL-001/split \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"childCount": 10, "childType": "BOX"}'
```

### Definition of Done
- [ ] HU split API
- [ ] HU merge API
- [ ] Parent-child relationship tracking
- [ ] HU hierarchy queries
- [ ] Tests pass

---
## Task PRD-1593: Nested HU Validation & Tracking

**Epic:** HU Hierarchy | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1592 | **SourceRefs:** Universe §4.Epic K

### Context
Need validation rules for nested HUs (box can't contain pallet) and tracking queries.

### Scope
**In Scope:** ParentHUId field, HU hierarchy validation (type constraints), HU hierarchy queries (get all children, get root parent), HU tree view
**Out of Scope:** Multi-level nesting (max 2 levels for Phase 1.5)

### Requirements
**Functional:** 1) Add ParentHUId to HandlingUnit entity, 2) Validation: BOX can't contain PALLET, UNIT can't contain anything, 3) Queries: GetChildren(huId), GetRootParent(huId), 4) HU tree view
**Non-Functional:** Hierarchy queries < 100ms

### Acceptance Criteria
```gherkin
Scenario: Validate HU hierarchy
  Given attempt to add pallet as child of box
  When validation runs
  Then error: "Invalid hierarchy: BOX cannot contain PALLET"

Scenario: Get all children
  Given pallet "PAL-001" with 10 box children
  When query GetChildren("PAL-001")
  Then return 10 box HUs

Scenario: Get root parent
  Given box "BOX-001" with parent pallet "PAL-001"
  When query GetRootParent("BOX-001")
  Then return "PAL-001"
```

### Validation
```bash
curl http://localhost:5000/api/warehouse/v1/handling-units/PAL-001/children \
  -H "Authorization: Bearer $TOKEN"
# Expected: List of child HUs
```

### Definition of Done
- [ ] ParentHUId field added
- [ ] HU hierarchy validation
- [ ] HU hierarchy queries (GetChildren, GetRootParent)
- [ ] HU tree view API
- [ ] Tests pass

---
## Task PRD-1594: Serial Number Lifecycle Management

**Epic:** Serial Tracking | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** L (2 days) | **OwnerType:** Backend/API
**Dependencies:** None | **SourceRefs:** Universe §4.Epic L

### Context
Need individual unit tracking for serialized items (laptops, phones, equipment).

### Scope
**In Scope:** SerialNumber entity, serial lifecycle (RECEIVED → AVAILABLE → ISSUED → RETURNED → SCRAPPED), serial tracking through pick/pack/dispatch
**Out of Scope:** Warranty tracking (deferred)

### Requirements
**Functional:** 1) SerialNumber entity (serial, itemId, status, location), 2) Serial lifecycle (5 statuses), 3) Serial tracking: Receive (RECEIVED), Putaway (AVAILABLE), Pick (ISSUED), Return (RETURNED), Scrap (SCRAPPED), 4) Serial validation (unique per item)
**Non-Functional:** Serial lookup < 50ms

### Acceptance Criteria
```gherkin
Scenario: Receive serialized item
  Given InboundShipment with item "LAPTOP-001" (requires serial tracking)
  When receive goods with serial "SN-12345"
  Then SerialNumber created: serial="SN-12345", status=RECEIVED, location=RECEIVING

Scenario: Serial lifecycle
  Given serial "SN-12345" with status RECEIVED
  When putaway to location "A1-B1"
  Then status = AVAILABLE, location = "A1-B1"
  When picked for order
  Then status = ISSUED
  When returned
  Then status = RETURNED

Scenario: Serial validation
  Given serial "SN-12345" already exists for item "LAPTOP-001"
  When attempt to receive same serial
  Then error: "Serial number already exists"
```

### Validation
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/serial-numbers \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"serial": "SN-12345", "itemId": 1, "status": "RECEIVED"}'
```

### Definition of Done
- [ ] SerialNumber entity created
- [ ] Serial lifecycle (5 statuses)
- [ ] Serial tracking through workflows
- [ ] Serial validation (uniqueness)
- [ ] Tests pass

---
## Task PRD-1595: Serial Status Tracking & Reporting

**Epic:** Serial Tracking | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1594 | **SourceRefs:** Universe §4.Epic L

### Context
Need serial status transitions and history reporting for traceability.

### Scope
**In Scope:** Serial status transitions (available → issued, issued → returned), serial history report (serial → all movements), serial search
**Out of Scope:** Serial warranty tracking (deferred)

### Requirements
**Functional:** 1) Serial status transitions (validate allowed transitions), 2) Serial history (all movements, status changes), 3) Serial search (by serial, item, status, location)
**Non-Functional:** Serial history query < 100ms

### Acceptance Criteria
```gherkin
Scenario: Serial status transition
  Given serial "SN-12345" with status AVAILABLE
  When transition to ISSUED
  Then status = ISSUED
  And transition recorded in history

Scenario: Invalid transition
  Given serial "SN-12345" with status SCRAPPED
  When attempt to transition to AVAILABLE
  Then error: "Invalid transition: SCRAPPED → AVAILABLE"

Scenario: Serial history report
  Given serial "SN-12345" with 5 movements
  When query serial history
  Then return all movements: RECEIVED → AVAILABLE → ISSUED → RETURNED → SCRAPPED
  And each movement includes: timestamp, location, operator
```

### Validation
```bash
curl http://localhost:5000/api/warehouse/v1/serial-numbers/SN-12345/history \
  -H "Authorization: Bearer $TOKEN"
# Expected: List of all movements
```

### Definition of Done
- [ ] Serial status transition validation
- [ ] Serial history tracking
- [ ] Serial search API
- [ ] Serial history report
- [ ] Tests pass

---
## Task PRD-1596: Fulfillment KPIs Dashboard

**Epic:** Analytics | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** UI/Backend
**Dependencies:** PRD-1528, PRD-1532 | **SourceRefs:** Universe §4.Epic O

### Context
Need operational KPIs for warehouse performance monitoring.

### Scope
**In Scope:** KPI dashboard (orders fulfilled, on-time delivery %, pick time avg), trend charts (daily/weekly/monthly), export (CSV, PDF)
**Out of Scope:** Real-time updates (SignalR deferred)

### Requirements
**Functional:** 1) KPIs: Orders fulfilled (count), On-time delivery % (orders shipped by requested date), Pick time avg (minutes), 2) Trend charts (line charts for last 30 days), 3) Export to CSV/PDF
**Non-Functional:** Dashboard loads < 2 seconds

### Acceptance Criteria
```gherkin
Scenario: View KPI dashboard
  Given 100 orders fulfilled (last 30 days)
  When navigate to /warehouse/analytics/kpis
  Then see KPIs: Orders fulfilled = 100, On-time delivery = 95%, Pick time avg = 12 min
  And see trend charts (daily orders, on-time %, pick time)

Scenario: Export KPIs
  Given KPI dashboard
  When click "Export to CSV"
  Then CSV file downloaded with KPI data (last 30 days)
```

### Validation
```bash
# Manual UI test
# Navigate to /warehouse/analytics/kpis
# Verify KPIs displayed
# Click "Export to CSV"
# Verify CSV downloaded
```

### Definition of Done
- [ ] KPI dashboard page
- [ ] KPIs: Orders fulfilled, On-time delivery %, Pick time avg
- [ ] Trend charts (line charts)
- [ ] Export to CSV/PDF
- [ ] Manual testing complete

---
## Task PRD-1597: QC Defects & Late Shipments Analytics

**Epic:** Analytics | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** S (0.5 day) | **OwnerType:** UI/Backend
**Dependencies:** PRD-1587 | **SourceRefs:** Universe §4.Epic O

### Context
Need analytics for quality issues and late shipments.

### Scope
**In Scope:** QC defects report (defect count by supplier, item, type), late shipments report (orders past requested delivery date), charts
**Out of Scope:** Predictive analytics (ML deferred)

### Requirements
**Functional:** 1) QC defects report (top defects, defects by supplier, defects by type), 2) Late shipments report (orders past due, days late), 3) Charts (bar charts, pie charts)
**Non-Functional:** Reports load < 2 seconds

### Acceptance Criteria
```gherkin
Scenario: QC defects report
  Given 50 defects recorded (last 30 days)
  When view QC defects report
  Then see: Top defect types (DAMAGED: 20, EXPIRED: 15, MISLABELED: 10, CONTAMINATED: 5)
  And defects by supplier (Supplier A: 30, Supplier B: 20)

Scenario: Late shipments report
  Given 10 orders past requested delivery date
  When view late shipments report
  Then see 10 late orders with: Order ID, Customer, Days late, Reason
```

### Validation
```bash
# Manual UI test
# Navigate to /warehouse/analytics/qc-defects
# Verify defects report displayed
# Navigate to /warehouse/analytics/late-shipments
# Verify late shipments report displayed
```

### Definition of Done
- [ ] QC defects report page
- [ ] Late shipments report page
- [ ] Charts (bar, pie)
- [ ] Manual testing complete

---
## Task PRD-1598: Contract Tests for External APIs

**Epic:** Testing | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** Integration
**Dependencies:** PRD-1538, PRD-1572 | **SourceRefs:** Universe §5.Integration

### Context
External API integrations (carrier, Agnum) need contract tests to detect breaking changes.

### Scope
**In Scope:** Contract tests for carrier API (FedEx, UPS), contract tests for Agnum API, schema validation, version compatibility
**Out of Scope:** Consumer-driven contracts (Pact deferred)

### Requirements
**Functional:** 1) Contract tests for carrier API (create shipment, get tracking), 2) Contract tests for Agnum API (export data), 3) Schema validation (JSON schema), 4) Version compatibility tests (v1 → v2)
**Non-Functional:** Contract tests run in < 1 minute

### Acceptance Criteria
```gherkin
Scenario: Carrier API contract test
  Given carrier API mock
  When call CreateShipment endpoint
  Then response matches schema
  And all required fields present (trackingNumber, labelUrl)

Scenario: Agnum API contract test
  Given Agnum API mock
  When call ExportData endpoint
  Then response matches schema
  And export format correct (CSV)

Scenario: Detect breaking change
  Given carrier API v1 returns field "trackingNumber"
  When v2 renames to "trackingId"
  Then contract test fails
  And build blocked
```

### Validation
```bash
dotnet test --filter "FullyQualifiedName~ContractTests"
# Expected: 5 tests pass (carrier, Agnum)
```

### Definition of Done
- [ ] Contract tests for carrier API (2 tests)
- [ ] Contract tests for Agnum API (2 tests)
- [ ] Schema validation
- [ ] Version compatibility tests
- [ ] CI pipeline fails on breaking changes

---
## Task PRD-1599: Performance Regression Test Suite

**Epic:** Testing | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** M (1 day) | **OwnerType:** QA
**Dependencies:** PRD-1580 | **SourceRefs:** Universe §5.Performance

### Context
Need automated performance regression tests to detect performance degradation.

### Scope
**In Scope:** Performance regression tests (baseline from Sprint 5), test scenarios (create 100 orders, pick 1000 items, pack 50 shipments), alert on regression (> 10% slower)
**Out of Scope:** Continuous performance monitoring (deferred)

### Requirements
**Functional:** 1) Performance regression tests (3 scenarios), 2) Baseline from Sprint 5 load tests, 3) Alert if > 10% slower than baseline, 4) CI integration (run on every merge to main)
**Non-Functional:** Regression tests complete in < 10 minutes

### Acceptance Criteria
```gherkin
Scenario: Performance regression test
  Given baseline: Create 100 orders in 10 seconds (p95 < 200ms)
  When run regression test
  And current: Create 100 orders in 11 seconds (p95 = 220ms)
  Then alert: "Performance regression detected: 10% slower"

Scenario: No regression
  Given baseline: Pick 1000 items in 20 seconds
  When run regression test
  And current: Pick 1000 items in 19 seconds
  Then test passes (no alert)
```

### Validation
```bash
# Run regression tests
dotnet test --filter "FullyQualifiedName~PerformanceRegressionTests"

# Expected: Tests pass if within 10% of baseline
```

### Definition of Done
- [ ] Performance regression tests (3 scenarios)
- [ ] Baseline from Sprint 5
- [ ] Alert on > 10% regression
- [ ] CI integration (run on merge to main)
- [ ] Tests pass

---
## Task PRD-1600: Operator Training Videos & Guides

**Epic:** Documentation | **Phase:** 1.5 | **Sprint:** 6 | **Estimate:** L (2 days) | **OwnerType:** QA
**Dependencies:** All above | **SourceRefs:** Universe §5.Documentation

### Context

Need comprehensive training materials for warehouse operators to learn the system.

### Scope

**In Scope:**
- Training videos (5-10 min each): Receiving, Picking, Packing, Dispatch, QC Inspection
- Operator guides (PDF): Step-by-step workflows with screenshots
- Quick reference cards (1-page cheat sheets)

**Out of Scope:**
- Interactive training modules (deferred to Phase 2)

### Requirements

**Functional:**
1. Training videos (5 videos, 5-10 min each):
   - Video 1: Receiving workflow (create shipment, scan items, QC gate)
   - Video 2: Picking workflow (view tasks, scan location, FEFO selection, complete pick)
   - Video 3: Packing workflow (scan items, print label, pack shipment)
   - Video 4: Dispatch workflow (load shipment, scan tracking, mark dispatched)
   - Video 5: QC Inspection (checklist, defect recording, photo upload)
2. Operator guides (5 PDFs with screenshots):
   - Guide 1: Receiving (step-by-step with screenshots)
   - Guide 2: Picking (step-by-step with screenshots)
   - Guide 3: Packing (step-by-step with screenshots)
   - Guide 4: Dispatch (step-by-step with screenshots)
   - Guide 5: QC Inspection (step-by-step with screenshots)
3. Quick reference cards (1-page cheat sheets):
   - Card 1: Barcode scanning tips
   - Card 2: Common error messages and solutions
   - Card 3: Keyboard shortcuts

**Non-Functional:**
1. Video quality: 1080p, clear audio
2. Guide format: PDF, printable, accessible

### Acceptance Criteria

```gherkin
Feature: Operator Training Materials

Scenario: Training video - Receiving
  Given training video "Receiving Workflow"
  When operator watches video
  Then video shows: Create shipment, Scan items, QC gate, Putaway
  And video duration 5-10 minutes
  And video quality 1080p

Scenario: Operator guide - Picking
  Given operator guide "Picking Workflow"
  When operator reads guide
  Then guide includes: Step-by-step instructions, Screenshots, Tips
  And guide format PDF (printable)

Scenario: Quick reference card
  Given quick reference card "Barcode Scanning Tips"
  When operator views card
  Then card fits on 1 page
  And card includes: Common issues, Solutions, Tips
```

### Validation / Checks

**Create Training Videos:**
```bash
# Record screen with OBS Studio or similar
# Edit with iMovie, DaVinci Resolve, or similar
# Export as MP4 (1080p, H.264)
# Upload to docs/training/videos/

# Videos:
# - receiving-workflow.mp4
# - picking-workflow.mp4
# - packing-workflow.mp4
# - dispatch-workflow.mp4
# - qc-inspection-workflow.mp4
```

**Create Operator Guides:**
```bash
# Create guides with screenshots
# Use Google Docs, Word, or Markdown → PDF
# Save to docs/training/guides/

# Guides:
# - receiving-guide.pdf
# - picking-guide.pdf
# - packing-guide.pdf
# - dispatch-guide.pdf
# - qc-inspection-guide.pdf
```

**Create Quick Reference Cards:**
```bash
# Create 1-page cheat sheets
# Save to docs/training/quick-reference/

# Cards:
# - barcode-scanning-tips.pdf
# - common-errors.pdf
# - keyboard-shortcuts.pdf
```

### Definition of Done

- [ ] 5 training videos created (5-10 min each, 1080p)
- [ ] 5 operator guides created (PDF with screenshots)
- [ ] 3 quick reference cards created (1-page PDFs)
- [ ] All materials uploaded to docs/training/
- [ ] Training materials reviewed by warehouse manager
- [ ] Training materials accessible to all operators
- [ ] README.md in docs/training/ with index of all materials

---
