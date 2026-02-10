# Master Data Specification Review: Kiro vs Baseline

## A) EXECUTIVE REVIEW (Severity-Ordered)

### BLOCKER (Must Fix Before Implementation)

1. **Epic 9 (Reports) Missing Implementation Tasks**: Universe.md defines Epic 9 with features 9.1-9.5 (Available Stock report, Location Balance report, Reservations report, CSV export, projection timestamp display), but tasks.md has ZERO tasks for Epic 9. This is ~5-7 days of backend+frontend work unaccounted for in the plan.

2. **Epic 8 (Admin UI) Missing Tasks**: Implementation-order.md says "not detailed in tasks.md, see UI scope doc" but provides no task breakdown. Need explicit tasks for 6 sub-features (8.1-8.6) totaling ~8 days of work.

3. **Epic 10 (Operational UI) Missing Tasks**: Same issue as Epic 8. Five sub-features (10.1-10.5) totaling ~7 days of frontend work are defined in universe.md but not in tasks.md.

4. **Event Contracts Ambiguity**: Task 3.1 says "Event Contracts (1 day)" and universe.md says "8 event types" but neither document explicitly lists the 8 events. My baseline defines: GoodsReceived, StockMoved, PickCompleted, StockAdjusted, ReservationCreated, ReservationReleased, QCPassed, QCFailed. These MUST be listed to avoid implementation drift.

### MAJOR (Will Cause Integration Issues)

5. **API Authorization Missing from Tasks**: My baseline specifies role-based access (WarehouseAdmin, WarehouseManager, WarehouseOperator) for every endpoint. Kiro's tasks have no explicit task for implementing authorization middleware, role checks, or JWT validation.

6. **ProblemDetails Error Handling Missing**: My baseline mandates RFC 7807 ProblemDetails with traceId for all errors. No task exists for implementing this pattern globally (middleware, exception filters, consistent error responses).

7. **Barcode Scanner Component Not Tasked**: Universe.md Epic 10.5 mentions barcode scanner component (auto-focus, auto-submit, manual fallback), but tasks.md has no corresponding task. This is ~1 day of reusable component work.

8. **Projection Timestamp Display Missing**: My baseline requires "Stock as of HH:MM:SS" display in UI to show eventual consistency lag. Not mentioned in tasks.

### MINOR (Quality/Consistency Issues)

9. **Open Questions Already Answered**: Tasks.md includes 5 open questions, but Question 1 (downtime tolerance) is already answered by Task 0.6 (zero-downtime rebuild). Question 4 (negative stock policy) is answered in my baseline (allow with warning). These should reference baseline decisions or be removed.

10. **Test Writing Tasks Missing**: Implementation-order.md allocates Week 7-8 for testing, but tasks.md has no explicit tasks for writing tests. Need tasks for: unit test suite (~2 days), integration test suite (~2 days), E2E test suite (~1 day), performance tests (~1 day).

11. **Validation Checklist Format**: Tasks use interactive checkboxes `[ ]` which won't render properly in all Markdown viewers. Use plain `- [ ]` or bullet points instead.

12. **Duration Estimates Optimistic**: Task 1.1 (Entity Models) = 1.5 days for 15+ entities with constraints/indexes/relationships seems tight. My baseline implementation plan allocated 2 weeks to Epic 1 (Foundation). Consider adding buffer.

13. **Event Count Mismatch in Validation**: Task 3.1 acceptance says "All 8 event types defined" but validation in implementation-order.md Phase 2 also says "All 8 event types defined and serializable" - neither lists the 8. This will cause confusion during code review.

### POSITIVE FINDINGS

14. **Projection Fix Correctly Prioritized**: Epic 0 marked as CRITICAL and BLOCKING with 2-week allocation is correct. Baseline ops runbook fully supports this approach.

15. **Schema Separation Correctly Identified**: Task 0.1 correctly identifies EF Core (`public` schema) vs Marten (`warehouse_events` schema) separation as root cause mitigation. Matches baseline preventive measure #1.

---

## B) MISMATCH MAP (Baseline vs Kiro)

| Area | Baseline | Kiro | Classification | Action |
|------|----------|------|----------------|--------|
| **Reports API** | Explicit endpoints: GET /stock/available, GET /stock/location-balance, GET /reservations with filters, pagination, CSV export | Epic 9 defined but NO tasks | **Bug** | Add Tasks 9.1-9.5 (~5 days) |
| **Admin UI Tasks** | 11 UI pages specified with components, forms, validation | Epic 8 defined but tasks say "see UI scope doc" | **Bug** | Add Tasks 8.1-8.6 (~8 days) |
| **Operational UI Tasks** | 11 UI pages with barcode workflows | Epic 10 defined but tasks say "see UI scope doc" | **Bug** | Add Tasks 10.1-10.5 (~7 days) |
| **Event List** | 8 events explicitly named: GoodsReceived, StockMoved, PickCompleted, StockAdjusted, ReservationCreated, ReservationReleased, QCPassed, QCFailed | "8 event types" mentioned but not listed | **Bug** | List all 8 events in Task 3.1 and universe.md Epic 3.1 |
| **Authorization** | Role-based: WarehouseAdmin, WarehouseManager, WarehouseOperator with specific permissions per endpoint | Not mentioned in tasks | **Bug** | Add Task 1.6: Authorization Middleware (~1 day) |
| **Error Handling** | RFC 7807 ProblemDetails with traceId, shown in UI ErrorBanner | Not mentioned in tasks | **Bug** | Add Task 1.7: ProblemDetails Pattern (~0.5 day) |
| **Barcode Scanner** | Reusable component: auto-focus, auto-submit on Enter, manual fallback checkbox | Mentioned in Epic 10.5 but no task | **Bug** | Add Task 10.5: Barcode Scanner Component (~1 day) |
| **Projection Timestamp** | Display "Stock as of HH:MM:SS" in UI, refresh button, staleness warning at 10 sec | Not mentioned in tasks | **Needs Decision** | Add to Task 3.7 or create Task 9.6: Projection Staleness UI (~0.5 day) |
| **Test Tasks** | 415+ tests planned: unit (200), integration (100), projection (50), workflow (30), UI (20), E2E (15) | Week 7-8 allocated but no tasks | **Bug** | Add Tasks 11.1-11.6: Test Suites (~6 days) |
| **Open Questions** | All 13 critical decisions resolved in baseline | 5 open questions, some already answered | **Acceptable Deviation** | Remove Q1, Q4 (already answered); keep Q2, Q3, Q5 if truly open |
| **Seed Data** | 5 categories: UoM (8 records), Virtual Locations (7), HU Types (3), Reason Codes (8), Item Categories (4 example) | Task 1.2 says "Seed Data" but doesn't list what to seed | **Acceptable Deviation** | Ref baseline doc for exact seed data list |
| **InternalSKU Format** | `RM-{0001}`, `FG-{0001}` with 4-digit zero-padded sequence | Task 1.4 says "InternalSKU Auto-Generation Logic" but doesn't specify format | **Acceptable Deviation** | Ref baseline doc for format rules |
| **Import Templates** | 5 templates: Items, Suppliers, SupplierItemMappings, ItemBarcodes, Locations with exact column specs | Task 2.1 says "Excel Template Generation" but doesn't list templates or columns | **Acceptable Deviation** | Ref baseline master-data-01-domain-model.md Import section |
| **Projection Rebuild Root Cause** | 4 failure modes identified: Schema Init Failure, Migration Mismatch, Concurrent Rebuild, Incomplete Cleanup | Task 0.1 says "Root Cause Analysis" but doesn't list failure modes | **Acceptable Deviation** | Ref baseline ops runbook for detailed RCA |
| **Distributed Lock** | Redis-based with 30-min expiry, fallback to DB lock if Redis unavailable | Task 0.4 says "Add Redis dependency (or use database-based lock if Redis unavailable)" | **Match** | No action |
| **Zero-Downtime Rebuild** | Marten shadow table swap (built-in) with explicit procedure | Task 0.6 says "Implement projection rebuild using shadow tables (Marten built-in)" | **Match** | No action |
| **UoM Rounding** | Default "Up" for picking, "Nearest" for reports | Not mentioned in tasks | **Acceptable Deviation** | Ref baseline for business rule |
| **Lot Tracking** | Optional per item via `Item.RequiresLotTracking` flag | Task 4.3 says "Lot Tracking" but doesn't specify optional vs mandatory | **Acceptable Deviation** | Ref baseline for optional pattern |
| **QC Gate** | Optional per item via `Item.RequiresQC` flag | Task 4.4 says "QC Pass/Fail Actions" but doesn't specify optional vs mandatory | **Acceptable Deviation** | Ref baseline for optional pattern |
| **Virtual Locations** | 7 mandatory: RECEIVING, QC_HOLD, QUARANTINE, PRODUCTION, SHIPPING, SCRAP, RETURN_TO_SUPPLIER | Task 1.2 says "Seed data (UoM, virtual locations...)" but doesn't list which ones | **Acceptable Deviation** | Ref baseline for exact list |
| **Capacity Warning** | Show warning at >80% utilization, not blocking in Phase 1 | Task 5.3 says "Capacity Warning" but doesn't specify threshold or blocking behavior | **Acceptable Deviation** | Ref baseline for 80% threshold, non-blocking |

---

## C) DETAILED ISSUES LIST

### BLOCKER-1: Epic 9 Reports Missing Tasks
**Severity**: BLOCKER  
**Document**: tasks.md (Epic 9 section missing)  
**Problem**: Universe.md defines Epic 9 with 5 features (9.1-9.5) but tasks.md has no corresponding tasks. This represents ~5-7 days of work unaccounted for.  
**Proposed Fix**:
Add to tasks.md after Epic 7:
```
## Epic 9: Stock Visibility & Reports

### Task 9.1: Available Stock Report API (1 day)
- Create query endpoint GET /api/warehouse/v1/stock/available
- Add filters: itemId, locationId, categoryId, includeReserved, includeVirtualLocations, expiringBefore
- Add pagination
- Query AvailableStock projection
- Join with Items, Locations for display names
- Ref: master-data-02-api-contracts.md (GET /stock/available)

### Task 9.2: Location Balance Report API (0.5 day)
- Create query endpoint GET /api/warehouse/v1/stock/location-balance
- Query LocationBalance projection
- Show capacity utilization (weight/volume)
- Ref: master-data-02-api-contracts.md (GET /stock/location-balance)

### Task 9.3: Reservations Report API (0.5 day)
- Create query endpoint GET /api/warehouse/v1/reservations
- Query ActiveReservations projection
- Add filters: itemId, orderId, status
- Ref: master-data-02-api-contracts.md (existing reservations endpoint)

### Task 9.4: CSV Export for Reports (1 day)
- Implement client-side CSV generation (JavaScript)
- Add export button to all report pages
- Format: headers, data rows, UTF-8 BOM for Excel compatibility
- Ref: master-data-04-ui-scope.md (CSV export pattern)

### Task 9.5: Projection Timestamp Display (0.5 day)
- Add projection timestamp to all stock reports
- Display format: "Stock as of 16:15:32"
- Add refresh button (polls projection API)
- Add staleness warning banner (if lag >10 seconds)
- Ref: master-data-04-ui-scope.md (Projection staleness section)
```

---

### BLOCKER-2: Epic 8 Admin UI Missing Tasks
**Severity**: BLOCKER  
**Document**: tasks.md (Epic 8 section missing detailed tasks)  
**Problem**: Implementation-order.md mentions Epic 8 but says "not detailed in tasks.md". Need explicit task breakdown for tracking.  
**Proposed Fix**:
Add to tasks.md after Epic 2:
```
## Epic 8: Admin UI (Master Data CRUD)

### Task 8.1: Items Management UI (1.5 days)
- Create Items list page with filters (search, category, status)
- Create Add/Edit Item modal form
- Implement barcode scanner input for PrimaryBarcode field
- Add deactivate action (set Status=Discontinued)
- Add "View Stock" link (navigate to /stock?itemId=X)
- Ref: master-data-04-ui-scope.md (Items Management section)

### Task 8.2: Suppliers Management UI (1 day)
- Create Suppliers list page with filters
- Create Add/Edit Supplier modal form
- Add "View Mappings" link (navigate to supplier-item mappings)
- Ref: master-data-04-ui-scope.md (Suppliers Management section)

### Task 8.3: Locations Management UI (1.5 days)
- Create Locations list page (tree view or flat with hierarchy indicators)
- Create Add/Edit Location modal form
- Implement QR code barcode generation button
- Add location hierarchy validation (parent must exist)
- Ref: master-data-04-ui-scope.md (Locations Management section)

### Task 8.4: Categories Management UI (0.5 day)
- Create Categories tree view
- Create Add/Edit Category modal form
- Support hierarchical parent selection
- Ref: master-data-04-ui-scope.md (Categories Management section)

### Task 8.5: Barcodes Management UI (1 day)
- Create Barcodes list per item (GET /api/warehouse/v1/items/{id}/barcodes)
- Add barcode entry form (barcode value, type, isPrimary)
- Enforce: only 1 primary barcode per item
- Ref: master-data-04-ui-scope.md (Barcodes section)

### Task 8.6: Import Wizard UI (2 days)
- Create tabbed interface (Items, Suppliers, Mappings, Barcodes, Locations)
- Implement drag-drop file upload component
- Display validation results (errors table with row/column/message)
- Add "Download Template" button per tab
- Add "Dry Run" checkbox
- Add "Download Error Report" button (generates Excel with errors)
- Ref: master-data-04-ui-scope.md (Import Wizard section)
```

---

### BLOCKER-3: Epic 10 Operational UI Missing Tasks
**Severity**: BLOCKER  
**Document**: tasks.md (Epic 10 section missing)  
**Problem**: Same as Epic 8 - implementation-order.md references Epic 10 but provides no task breakdown.  
**Proposed Fix**:
Add to tasks.md after Epic 7:
```
## Epic 10: Operational Workflows UI

### Task 10.1: Receiving Pages UI (2 days)
- Create Inbound Shipments list page with filters
- Create Shipment detail page (view lines, status tracking)
- Create Receive Goods modal (barcode scan, qty input, lot fields if required)
- Create QC Panel page (list items in QC_HOLD, Pass/Fail buttons)
- Ref: master-data-04-ui-scope.md (Receiving screens)

### Task 10.2: Putaway Page UI (1 day)
- Create Putaway Tasks list (items in RECEIVING location)
- Create Putaway modal (scan location barcode, show capacity info, confirm)
- Display capacity warning if >80% (not blocking)
- Ref: master-data-04-ui-scope.md (Putaway screens)

### Task 10.3: Picking Pages UI (2 days)
- Create Pick Tasks list with filters (assigned to me, status)
- Create Pick Execution page (location selection table, barcode scan workflow, qty confirm)
- Display available stock locations sorted by FEFO (earliest expiry first)
- Ref: master-data-04-ui-scope.md (Picking screens)

### Task 10.4: Adjustments Pages UI (1 day)
- Create Adjustments page with two tabs: Create Adjustment, Adjustment History
- Create adjustment form (item selector, location selector, qty delta, reason dropdown, notes)
- Add confirmation dialog with warning "This action cannot be undone"
- Create history list with filters (item, location, reason, user, date range)
- Ref: master-data-04-ui-scope.md (Adjustments screens)

### Task 10.5: Barcode Scanner Component (1 day)
- Create reusable BarcodeScanner.razor component
- Features: auto-focus input, auto-submit on Enter key, camera button (WebRTC optional)
- Add manual entry checkbox (reveals item/location dropdown fallback)
- Add validation feedback (green checkmark or red X on scan)
- Ref: master-data-04-ui-scope.md (Barcode Scanner Component section)
```

---

### BLOCKER-4: Event Contracts Not Listed
**Severity**: BLOCKER  
**Document**: tasks.md Task 3.1, universe.md Epic 3.1  
**Problem**: Task 3.1 says "Event Contracts (1 day)" and validation says "All 8 event types defined" but neither lists the 8 events. This will cause implementation drift.  
**Proposed Fix**:
```
DOC: tasks.md
SECTION: Task 3.1: Event Contracts

Replace:
"**Steps**:
1. Create event contracts: GoodsReceived, StockMoved, PickCompleted, StockAdjusted, Reservations, QC
..."

With:
"**Steps**:
1. Create event contracts for all 8 event types:
   - GoodsReceived (shipmentId, itemId, receivedQty, destinationLocationId, lotId, etc.)
   - StockMoved (itemId, qty, fromLocationId, toLocationId, movementType, lotId, etc.)
   - PickCompleted (pickTaskId, orderId, itemId, pickedQty, fromLocationId, toLocationId, lotId, etc.)
   - StockAdjusted (itemId, locationId, qtyDelta, reasonCode, notes, userId, etc.)
   - ReservationCreated (reservationId, itemId, reservedQty, orderId, expiresAt, etc.)
   - ReservationReleased (reservationId, releasedQty, releaseReason, etc.)
   - QCPassed (itemId, qty, lotId, fromLocationId=QC_HOLD, toLocationId=RECEIVING, inspectorNotes, etc.)
   - QCFailed (itemId, qty, lotId, fromLocationId=QC_HOLD, toLocationId=QUARANTINE, reasonCode, etc.)
2. Define event payload schemas (match contracts in master-data-03-events-and-projections.md)
..."

AND

DOC: universe.md
SECTION: Epic 3.1

Replace:
"3.1 Event contracts (GoodsReceived, StockMoved, PickCompleted, StockAdjusted, Reservations, QC)"

With:
"3.1 Event contracts: 8 types - GoodsReceived, StockMoved, PickCompleted, StockAdjusted, ReservationCreated, ReservationReleased, QCPassed, QCFailed (see master-data-03-events-and-projections.md for full payload schemas)"
```

---

### MAJOR-1: Authorization Middleware Missing
**Severity**: MAJOR  
**Document**: tasks.md (no task for authorization)  
**Problem**: Baseline specifies role-based access (WarehouseAdmin, WarehouseManager, WarehouseOperator) for every endpoint. No task exists for implementing this.  
**Proposed Fix**:
Add to tasks.md after Task 1.5:
```
### Task 1.6: Authorization Middleware (1 day)
**Goal**: Implement role-based access control for all API endpoints

**Steps**:
1. Create AuthorizationPolicyProvider with roles: WarehouseAdmin, WarehouseManager, WarehouseOperator, QCInspector
2. Add [Authorize(Roles = "...")] attributes to controllers per baseline spec
3. Create custom authorization handlers if needed (e.g., location-based permissions in Phase 2)
4. Test unauthorized access returns 403 Forbidden with ProblemDetails
5. Document role permissions matrix in README.md

**Acceptance**:
- All endpoints enforce role-based access
- Unauthorized requests return 403 with ProblemDetails + traceId
- Admin endpoints require WarehouseAdmin role
- Operator endpoints accessible by WarehouseOperator, WarehouseManager, WarehouseAdmin

**Tests**:
- Integration test: Call admin endpoint with operator token → 403
- Integration test: Call operator endpoint with operator token → 200

**Files**:
- src/LKvitai.MES.Api/Authorization/WarehousePolicyProvider.cs
- src/LKvitai.MES.Api/Program.cs (register authorization services)
- Ref: master-data-02-api-contracts.md (Authorization section for each endpoint)
```

---

### MAJOR-2: ProblemDetails Pattern Missing
**Severity**: MAJOR  
**Document**: tasks.md (no task for error handling)  
**Problem**: Baseline mandates RFC 7807 ProblemDetails with traceId for all API errors. No implementation task exists.  
**Proposed Fix**:
Add to tasks.md after Task 1.6:
```
### Task 1.7: ProblemDetails Error Handling (0.5 day)
**Goal**: Implement consistent error responses across all API endpoints

**Steps**:
1. Add Microsoft.AspNetCore.Mvc.ProblemDetails NuGet package
2. Configure global exception handler to return ProblemDetails
3. Include traceId in all error responses (from Activity.Current?.Id)
4. Map exception types to HTTP status codes (ValidationException → 400, NotFoundException → 404, etc.)
5. Create custom ProblemDetails subclasses for domain errors (InsufficientStockProblem, BarcodeMismatchProblem, etc.)

**Acceptance**:
- All API errors return ProblemDetails JSON
- TraceId included in all error responses
- UI can extract and display traceId from ProblemDetails

**Tests**:
- Integration test: Trigger validation error → verify ProblemDetails structure
- Integration test: Verify traceId present in error response

**Files**:
- src/LKvitai.MES.Api/Middleware/ProblemDetailsExceptionHandler.cs
- src/LKvitai.MES.Api/Program.cs (register exception handler)
- Ref: master-data-02-api-contracts.md (Error Handling section)
```

---

### MAJOR-3: Test Writing Tasks Missing
**Severity**: MAJOR  
**Document**: tasks.md (no tasks for test suites)  
**Problem**: Implementation-order.md allocates Week 7-8 for testing but provides no task breakdown. Baseline plans 415+ tests.  
**Proposed Fix**:
Add to tasks.md after Epic 10:
```
## Epic 11: Testing & Quality Assurance

### Task 11.1: Unit Test Suite (2 days)
**Goal**: Write unit tests for domain logic (target: 200+ tests)

**Coverage**:
- Entity validation (required fields, check constraints)
- UoM conversion logic with rounding rules
- InternalSKU generation
- Barcode validation
- Projection event handlers

**Files**:
- tests/LKvitai.MES.Warehouse.UnitTests/**/*.cs
- Ref: master-data-05-implementation-plan-and-tests.md (Unit Tests section)

### Task 11.2: Integration Test Suite (2 days)
**Goal**: Write integration tests for APIs and database (target: 100+ tests)

**Coverage**:
- EF Core CRUD operations
- Import API (upload, validate, insert/update)
- Event store append (optimistic concurrency)
- API endpoints (full request/response)

**Setup**:
- Use Testcontainers for Postgres
- Implement Docker-gated pattern (skip if Docker unavailable)

**Files**:
- tests/LKvitai.MES.Warehouse.IntegrationTests/**/*.cs
- Ref: master-data-05-implementation-plan-and-tests.md (Integration Tests section)

### Task 11.3: Projection Test Suite (1 day)
**Goal**: Write projection tests (target: 50+ tests)

**Coverage**:
- Event handler logic (apply event, verify state)
- Multi-event workflows (receive → putaway → pick → adjust)
- Projection rebuild (1000 events)

**Files**:
- tests/LKvitai.MES.Warehouse.IntegrationTests/Projections/**/*.cs
- Ref: master-data-05-implementation-plan-and-tests.md (Projection Tests section)

### Task 11.4: Workflow Test Suite (1 day)
**Goal**: Write end-to-end workflow tests (target: 30+ tests)

**Coverage**:
- Receive → Putaway → Pick → Ship
- Error scenarios (insufficient stock, barcode mismatch)
- Saga compensation (reservation failed)

**Files**:
- tests/LKvitai.MES.Warehouse.IntegrationTests/Workflows/**/*.cs
- Ref: master-data-05-implementation-plan-and-tests.md (Workflow Tests section)

### Task 11.5: E2E Test Suite (1 day)
**Goal**: Write browser-based E2E tests (target: 15+ tests)

**Tools**: Playwright (C# bindings)

**Coverage**:
- Import → Receive → Putaway → Pick (critical path)
- Browser compatibility (Chrome, Edge)

**Files**:
- tests/LKvitai.MES.Warehouse.E2ETests/**/*.cs
- Ref: master-data-05-implementation-plan-and-tests.md (E2E Tests section)

### Task 11.6: Performance Test Suite (0.5 day)
**Goal**: Load testing and benchmarks

**Tools**: k6 (load testing), BenchmarkDotNet (micro-benchmarks)

**Coverage**:
- Import API throughput (concurrent uploads)
- Projection lag measurement
- API response time (p50, p95, p99)

**Files**:
- tests/LKvitai.MES.Warehouse.PerformanceTests/**/*
- Ref: master-data-05-implementation-plan-and-tests.md (Performance Tests section)
```

---

### MINOR-1: Open Questions Section
**Severity**: MINOR  
**Document**: tasks.md (Open Questions section at end)  
**Problem**: Questions 1 and 4 are already answered in baseline. Question 1 asks about downtime tolerance but Task 0.6 implements zero-downtime rebuild. Question 4 asks about negative stock policy but baseline specifies "allow with warning".  
**Proposed Fix**:
```
DOC: tasks.md
SECTION: Open Questions

Replace Question 1:
"### Question 1: Projection Rebuild Downtime Tolerance
**Context**: Projection rebuild may take 5-30 minutes depending on event count
**Question**: Is 5-minute downtime acceptable for projection rebuild, or do we need zero-downtime rebuild (shadow tables)?
**Impact**: Zero-downtime adds complexity (shadow table swap logic)
**Recommendation**: Start with acceptable downtime (5 min), add zero-downtime in Phase 2 if needed"

With:
"### Question 1: Projection Rebuild Downtime Tolerance [RESOLVED]
**Decision**: Implement zero-downtime rebuild using Marten shadow tables (Task 0.6). This is Phase 1 requirement per baseline.
**Rationale**: Business cannot tolerate 5+ minute stock visibility outage during peak operations.
**Ref**: master-data-06-ops-runbook-projections.md (Zero-Downtime Rebuild section)"

Replace Question 4:
"### Question 4: Negative Stock Policy
**Context**: Stock adjustments may result in negative stock (investigation needed)
**Question**: Should negative stock be blocked (hard constraint) or allowed with warning (soft constraint)?
**Impact**: Hard constraint may block legitimate corrections, soft constraint may hide data quality issues
**Recommendation**: Allow with warning in Phase 1, add approval workflow in Phase 2"

With:
"### Question 4: Negative Stock Policy [RESOLVED]
**Decision**: Allow negative stock with warning (soft constraint) in Phase 1.
**Rationale**: Investigations often require temporary negative stock. Hard blocking would prevent data quality corrections.
**Implementation**: Task 7.1 (Adjustment Creation) includes "Warn if new qty < 0 (not blocking - investigation needed)".
**Ref**: master-data-02-api-contracts.md (POST /adjustments, 422 response with warning)"
```

---

## D) PATCH SUGGESTIONS

### DOC: tasks.md

**Patch 1: Add Epic 9 Tasks (after Epic 7)**
```
- Add section: "## Epic 9: Stock Visibility & Reports"
- Insert Tasks 9.1-9.5 as detailed in BLOCKER-1 above
```

**Patch 2: Add Epic 8 Detailed Tasks (after Epic 2)**
```
- Replace: "Epic 8 tasks (not detailed in tasks.md, see UI scope doc)"
- With: Full task breakdown Tasks 8.1-8.6 as detailed in BLOCKER-2 above
```

**Patch 3: Add Epic 10 Detailed Tasks (after Epic 7)**
```
- Add section: "## Epic 10: Operational Workflows UI"
- Insert Tasks 10.1-10.5 as detailed in BLOCKER-3 above
```

**Patch 4: Fix Task 3.1 Event List**
```
- Replace: Step 1 in Task 3.1 (see BLOCKER-4 above)
- Add explicit list of all 8 events with brief payload notes
```

**Patch 5: Add Authorization & Error Handling Tasks**
```
- After Task 1.5, add Task 1.6 (Authorization Middleware) as detailed in MAJOR-1
- After Task 1.6, add Task 1.7 (ProblemDetails Error Handling) as detailed in MAJOR-2
```

**Patch 6: Add Testing Tasks**
```
- After Epic 10, add "## Epic 11: Testing & Quality Assurance"
- Insert Tasks 11.1-11.6 as detailed in MAJOR-3 above
```

**Patch 7: Fix Open Questions**
```
- Update Question 1 and Question 4 to show [RESOLVED] status
- Add reference to baseline decisions and implementing tasks
- See MINOR-1 above for exact text
```

**Patch 8: Fix Validation Checkbox Format**
```
- Replace all: "- [ ]" with "- [ ]" (ensure proper Markdown checkbox syntax)
- Or use plain bullets "- " if checkboxes not needed in static doc
```

---

### DOC: universe.md

**Patch 1: Fix Epic 3.1 Event List**
```
- Replace: "3.1 Event contracts (GoodsReceived, StockMoved, PickCompleted, StockAdjusted, Reservations, QC)"
- With: "3.1 Event contracts: 8 types - GoodsReceived, StockMoved, PickCompleted, StockAdjusted, ReservationCreated, ReservationReleased, QCPassed, QCFailed (see master-data-03-events-and-projections.md for full schemas)"
```

**Patch 2: Add Epic 9 Sub-Features**
```
- Verify Epic 9 lists all 5 features: 9.1-9.5 (appears correct)
- No change needed if already present
```

**Patch 3: Add Note on Missing Tasks**
```
- After Epic 8, Epic 9, Epic 10 descriptions, add:
  "(See tasks.md Epic 8, 9, 10 for detailed task breakdown)"
- This ensures readers know where to find implementation details
```

---

### DOC: implementation-order.md

**Patch 1: Update Phase 1 Duration**
```
SECTION: Phase 1: Import System
- Replace: "**Duration**: 1 week"
- With: "**Duration**: 1 week (5 days backend)"
- Reason: Clarify that Epic 8.6 (Import Wizard UI) is 2 days frontend in Phase 7
```

**Patch 2: Add Phase for Reports**
```
- After Phase 6, add:
"### Phase 2.5: Stock Visibility & Reports (Week 4, CAN PARALLELIZE with Phase 3-6)
**Duration**: 0.75 week
**Team**: 1 Backend Dev (part-time)
**Dependencies**: Phase 2 (projections) complete
- Task 9.1: Available Stock Report API
- Task 9.2: Location Balance Report API
- Task 9.3: Reservations Report API
- Task 9.4: CSV Export
- Task 9.5: Projection Timestamp Display"
```

**Patch 3: Update Phase 7 with Detailed Tasks**
```
SECTION: Phase 7: Admin UI
- Replace: "**Epic 8 Tasks** (not detailed in tasks.md, see UI scope doc)"
- With: "**Epic 8 Tasks** (see tasks.md Epic 8 for full breakdown):"
- List Tasks 8.1-8.6 with durations
```

**Patch 4: Update Phase 8 with Detailed Tasks**
```
SECTION: Phase 8: Operational Workflows UI
- Replace: "**Epic 10 Tasks** (not detailed in tasks.md, see UI scope doc)"
- With: "**Epic 10 Tasks** (see tasks.md Epic 10 for full breakdown):"
- List Tasks 10.1-10.5 with durations
```

**Patch 5: Add Testing Phase**
```
- After Phase 9, add:
"### Phase 10: Testing & QA (Week 7-8)
**Duration**: 1.5 weeks
**Team**: All team + 1 QA Engineer
**Dependencies**: All features complete
- Task 11.1: Unit tests (2 days)
- Task 11.2: Integration tests (2 days)
- Task 11.3: Projection tests (1 day)
- Task 11.4: Workflow tests (1 day)
- Task 11.5: E2E tests (1 day)
- Task 11.6: Performance tests (0.5 day)"
```

**Patch 6: Update Timeline Total**
```
SECTION: Critical Path Summary
- Add note: "Tasks added: Epic 8 (8 days), Epic 9 (3.5 days), Epic 10 (7 days), Epic 11 (6.5 days) = 25 days
- Original estimate: 8 weeks
- Revised estimate: 9-10 weeks (with added tasks and buffer)"
```

---

## SUMMARY

**Kiro's work is 75% aligned with baseline** with strong fundamentals (Epic 0 projection fix, schema separation, import strategy, timeline realism). Main gaps are missing implementation tasks for UI (Epics 8, 9, 10) and testing (Epic 11), totaling ~25 days of unaccounted work.

**Critical Next Steps**:
1. Add Tasks for Epic 8, 9, 10, 11 (see patches above)
2. List all 8 events explicitly in Task 3.1 and universe.md
3. Add authorization and error handling tasks (1.6, 1.7)
4. Update timeline to 9-10 weeks (from 8) to account for added tasks
5. Resolve/remove answered open questions (Q1, Q4)

**Implementation can proceed** after patches applied. No fundamental architectural misalignment detected.
