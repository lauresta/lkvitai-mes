# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 4 (Execution Pack)

**Version:** 1.0
**Date:** February 11, 2026
**Sprint:** Phase 1.5 Sprint 4
**Source:** prod-ready-universe.md, prod-ready-tasks-progress.md
**Status:** Ready for Execution

---

## Sprint Overview

**Sprint Goal:** Achieve operational completeness with end-to-end hardening, observability, integration tests, and production-ready reporting.

**Sprint Duration:** 2 weeks
**Total Tasks:** 20
**Estimated Effort:** 16 days

**Focus Areas:**
1. **End-to-End Workflow Tests:** Integration tests covering full operator workflows
2. **Observability Dashboards:** Health checks, metrics dashboards, alerting
3. **RBAC Polish:** Role-based access control enforcement and admin UI
4. **Reporting Completeness:** Stock movement history, transaction log, compliance reports
5. **Performance & Backfill:** Projection rebuild optimization, consistency checks
6. **Production Readiness:** Deployment guide, runbook, monitoring setup

**Dependencies:**
- Sprint 3 complete (PRD-1521 to PRD-1540)

---

## Sprint 4 Task Index

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-1541 | Testing | E2E Inbound Workflow Test | M | PRD-1523,1524 | QA | Universe §3.Workflow 1 |
| PRD-1542 | Testing | E2E Outbound Workflow Test | M | PRD-1527-1532 | QA | Universe §3.Workflow 2 |
| PRD-1543 | Testing | E2E Stock Movement Test | S | PRD-1526 | QA | Universe §3.Workflow 3 |
| PRD-1544 | Observability | Health Check Dashboard | M | PRD-1539 | Infra/DevOps | Universe §5.Observability |
| PRD-1545 | Observability | Metrics Dashboards (Grafana) | L | PRD-1539 | Infra/DevOps | Universe §5.Observability |
| PRD-1546 | Observability | Alerting Rules & Runbook | M | PRD-1544,1545 | Infra/DevOps | Universe §5.Monitoring |
| PRD-1547 | RBAC | Role-Based Access Control Enforcement | M | PRD-1521 | Backend/API | Universe §5.Security |
| PRD-1548 | RBAC | Admin User Management UI | M | PRD-1547 | UI | Universe §5.Security |
| PRD-1549 | Reports | Stock Movement History Report | M | None | UI/Backend | Universe §1.Reports |
| PRD-1550 | Reports | Transaction Log Export | S | None | Backend/API | Universe §1.Reports |
| PRD-1551 | Reports | Traceability Report (Lot → Order) | M | None | UI/Backend | Universe §1.Reports |
| PRD-1552 | Reports | Compliance Audit Report | S | None | UI/Backend | Universe §5.Compliance |
| PRD-1553 | Performance | Projection Rebuild Optimization | M | PRD-1509,1513 | Projections | Universe §5.Performance |
| PRD-1554 | Performance | Consistency Checks (Daily Job) | M | None | Backend/API | Universe §5.Data Integrity |
| PRD-1555 | Performance | Query Performance Optimization | S | None | Backend/API | Universe §5.Performance |
| PRD-1556 | Integration | ERP Integration Mock & Tests | M | PRD-1505,1508 | Integration | Universe §5.Integration |
| PRD-1557 | Documentation | Deployment Guide | S | All | Infra/DevOps | Universe §5.Deployment |
| PRD-1558 | Documentation | Operator Runbook | S | All | QA | Universe §5.Operations |
| PRD-1559 | Documentation | API Documentation (Swagger) | S | All | Backend/API | Universe §5.Documentation |
| PRD-1560 | Testing | Production Readiness Checklist | M | All above | QA | Universe §5.Deployment |

**Total Effort:** 16 days (1 developer, 3.2 weeks)

---

## Task PRD-1541: E2E Inbound Workflow Test

**Epic:** Testing
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** QA
**Dependencies:** PRD-1523 (Receiving UI), PRD-1524 (Scan & QC UI)
**SourceRefs:** Universe §3.Workflow 1 (Procure-to-Stock)

### Context

- Sprint 3 delivered inbound UI (invoice entry, receiving, QC)
- Need integration test validating end-to-end flow: Create shipment → Receive → QC → Putaway
- Test must cover happy path + failure scenarios (QC fail, qty mismatch)
- Uses API-level testing (not UI automation, which is too brittle)

### Scope

**In Scope:**
- Integration test class: `InboundWorkflowIntegrationTests`
- Test scenarios: 7 scenarios (happy path, QC pass, QC fail, qty mismatch, duplicate receive, projection consistency, audit trail)
- Test fixtures: seeded supplier, items, locations
- Assertions: database state, projection updates, event emission

**Out of Scope:**
- UI automation (Selenium/Playwright deferred to Phase 2)
- Performance load testing (separate task)
- Multi-warehouse scenarios

### Requirements

**Functional:**
1. Test setup: seed database with Supplier, Items (RequiresQC=true/false), Locations
2. Test 1: Create shipment → Receive items → Verify stock at RECEIVING → Putaway → Verify at storage location
3. Test 2: Receive item requiring QC → Verify at QC_HOLD → QC Pass → Verify at RECEIVING
4. Test 3: Receive item requiring QC → QC Fail → Verify at QUARANTINE
5. Test 4: Receive with qty mismatch (expected 100, actual 80) → Shipment status PARTIAL
6. Test 5: Duplicate receive (same CommandId) → Idempotent response
7. Test 6: Projection consistency → AvailableStock reflects received qty
8. Test 7: Audit trail → All events logged with operator, timestamp

**Non-Functional:**
1. Test isolation: each test runs in transaction, rolled back after
2. Test execution time: < 30 seconds for all scenarios
3. CI/CD integration: tests run on every commit
4. Coverage: critical paths only (not exhaustive edge cases)

**Test Pattern:**
```csharp
[Collection("Integration")]
public class InboundWorkflowIntegrationTests : IClassFixture<WarehouseTestFixture>
{
    [Fact]
    public async Task FullInboundWorkflow_HappyPath()
    {
        // Arrange
        var supplierId = await SeedSupplier();
        var itemId = await SeedItem(requiresQC: false);

        // Act: Create shipment
        var shipmentId = await CreateInboundShipment(supplierId, itemId, expectedQty: 100);

        // Act: Receive items
        await ReceiveItems(shipmentId, itemId, actualQty: 100, lot: "BATCH-A");

        // Assert: Stock at RECEIVING
        var stock = await QueryAvailableStock(itemId, "RECEIVING");
        Assert.Equal(100, stock.OnHandQty);

        // Act: Putaway
        await Putaway(itemId, fromLocation: "RECEIVING", toLocation: "A1-B1", qty: 100);

        // Assert: Stock at A1-B1
        var finalStock = await QueryAvailableStock(itemId, "A1-B1");
        Assert.Equal(100, finalStock.OnHandQty);

        // Assert: Events emitted
        var events = await GetEvents(shipmentId);
        Assert.Contains(events, e => e.EventType == "InboundShipmentCreated");
        Assert.Contains(events, e => e.EventType == "GoodsReceived");
        Assert.Contains(events, e => e.EventType == "StockMoved");
        Assert.Contains(events, e => e.EventType == "PutawayCompleted");
    }

    [Fact]
    public async Task QCFailure_StockMovedToQuarantine()
    {
        // Arrange
        var itemId = await SeedItem(requiresQC: true);
        var shipmentId = await CreateInboundShipment(supplierId, itemId, 50);

        // Act: Receive → Auto-routed to QC_HOLD
        await ReceiveItems(shipmentId, itemId, 50);
        var huId = await GetHandlingUnitId(itemId, "QC_HOLD");

        // Act: QC Fail
        await QCInspect(huId, decision: QCDecision.FAIL, reason: "DAMAGED");

        // Assert: Stock at QUARANTINE
        var stock = await QueryAvailableStock(itemId, "QUARANTINE");
        Assert.Equal(50, stock.OnHandQty);

        // Assert: QC event logged
        var events = await GetEvents(huId);
        Assert.Contains(events, e => e.EventType == "QCFailed" && e.Data["Reason"] == "DAMAGED");
    }
}
```

### Acceptance Criteria

```gherkin
Feature: End-to-End Inbound Workflow

Scenario: Complete inbound workflow (happy path)
  Given Supplier "ACME", Item "RM-0001" (RequiresQC=false)
  When create InboundShipment with expected qty 100
  And receive items: actual qty 100, lot "BATCH-A"
  And putaway to location "A1-B1"
  Then AvailableStock shows: Item RM-0001, Location A1-B1, Qty 100
  And events emitted: InboundShipmentCreated, GoodsReceived, StockMoved, PutawayCompleted

Scenario: QC pass workflow
  Given Item "FG-0001" (RequiresQC=true)
  When receive item qty 50
  Then stock auto-routed to QC_HOLD
  When QC inspector approves
  Then stock moved to RECEIVING
  And event QCPassed emitted

Scenario: QC fail workflow
  Given Item "FG-0002" (RequiresQC=true)
  When receive item qty 20
  And QC inspector rejects with reason "DAMAGED"
  Then stock moved to QUARANTINE
  And event QCFailed emitted with reason

Scenario: Partial receipt
  Given shipment with expected qty 100
  When receive actual qty 80
  Then shipment status = PARTIAL
  And remaining qty = 20

Scenario: Idempotent receive
  Given shipment already received with CommandId "abc-123"
  When receive again with same CommandId "abc-123"
  Then cached result returned
  And stock qty unchanged (no duplicate)

Scenario: Projection consistency
  Given item received qty 100
  When AvailableStock projection updated
  Then projection lag < 1 second
  And OnHandQty matches event stream aggregate

Scenario: Audit trail completeness
  Given inbound workflow executed
  When query audit log
  Then all events logged with: OperatorId, Timestamp, CorrelationId
```

### Validation / Checks

**Local Testing:**
```bash
# Run integration tests
dotnet test --filter "FullyQualifiedName~InboundWorkflowIntegrationTests" --logger "console;verbosity=detailed"

# Check test coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov

# Run in CI
# Expected: all tests pass, coverage > 80% for inbound workflow classes
```

**Metrics:**
- `integration_test_duration_ms` (histogram, test_name label)
- `integration_test_failures_total` (counter, test_name label)

**Logs:**
- INFO: "Integration test started: {TestName}"
- INFO: "Integration test completed: {TestName}, Duration: {DurationMs}ms"

### Definition of Done

- [ ] `InboundWorkflowIntegrationTests` class created in `src/tests/LKvitai.MES.Tests.Integration`
- [ ] 7 test scenarios implemented (happy path, QC pass/fail, partial, idempotent, projection, audit)
- [ ] Test fixtures: `WarehouseTestFixture` with database seeding
- [ ] All tests pass locally
- [ ] Tests integrated into CI pipeline (GitHub Actions / Azure DevOps)
- [ ] Test execution time < 30 seconds
- [ ] Code coverage report generated
- [ ] Documentation: `docs/testing-guide.md` updated with running instructions
- [ ] Code review completed

---
- PRD-1542-1543: E2E Outbound & Stock Movement Tests
- PRD-1544-1546: Observability (Health Checks, Grafana Dashboards, Alerting)
- PRD-1547-1548: RBAC Enforcement & Admin UI
- PRD-1549-1552: Reports (Movement History, Transaction Log, Traceability, Compliance)
- PRD-1553-1555: Performance (Rebuild Optimization, Consistency Checks, Query Optimization)
- PRD-1556: ERP Integration Mock
- PRD-1557-1559: Documentation (Deployment Guide, Operator Runbook, API Docs)
- PRD-1560: Production Readiness Checklist

---

## Sprint 4 Success Criteria

At the end of Sprint 4, these MUST be true:

### Operator Workflow Validation (CRITICAL)

✅ **1. Operator can create inbound invoice/shipment in UI**
- Navigate to /warehouse/inbound/shipments
- Click "Create"
- Fill form, submit successfully
- Shipment visible in list

✅ **2. Operator can receive with scan + QC in UI**
- Navigate to shipment detail
- Scan item barcode
- Enter lot/expiry
- Submit receive successfully
- If RequiresQC=true, item appears in QC Panel
- QC inspector can Pass/Fail

✅ **3. Operator can putaway/move stock in UI and see balances update**
- Navigate to /warehouse/stock/dashboard
- See stock at RECEIVING location
- Navigate to /warehouse/transfers
- Create transfer from RECEIVING to storage location
- Execute transfer via barcode scan
- Dashboard shows updated location balance

✅ **4. Operator can create Sales Order in UI, allocate, pick, pack, dispatch**
- Navigate to /warehouse/sales/orders
- Click "Create"
- Fill customer, items, submit
- Order auto-allocated (or Manager approves)
- Manager releases to picking
- Picker scans items via /warehouse/picking
- Packer packs via /warehouse/packing
- Dispatcher confirms via /warehouse/outbound/dispatch
- Shipment status = DISPATCHED

✅ **5. Operator can view shipment status + dispatch history in UI**
- Navigate to /warehouse/reports/dispatch-history
- See all dispatched shipments
- Click shipment detail
- See tracking number, carrier, dispatch timestamp

✅ **6. RBAC/auth flow documented and local validation steps work (no 403 surprises)**
- `docs/dev-auth-guide.md` exists
- Operator can obtain dev token via POST /api/auth/dev-token
- All documented curl commands work with token
- Production mode: endpoint returns 404

✅ **7. Idempotency and tracing are in place and validated**
- All commands include CommandId
- Duplicate commands return cached result
- All API responses include X-Correlation-ID header
- Logs include correlation ID for end-to-end tracing

### Testing & Quality

✅ **8. Integration tests validate critical workflows**
- Inbound workflow test passes
- Outbound workflow test passes
- Stock movement workflow test passes
- Tests run in CI on every commit

✅ **9. Observability dashboards operational**
- Health check endpoint returns 200 with system status
- Grafana dashboards show: API latency, event throughput, projection lag
- Alerts configured: API errors > 5%, projection lag > 5s

### Reporting & Documentation

✅ **10. Reports accessible via UI**
- Receiving history report
- Dispatch history report
- Stock movement history
- Transaction log export (CSV)
- Traceability report (Lot → Sales Order)

✅ **11. Documentation complete**
- Deployment guide (how to deploy to production)
- Operator runbook (common tasks, troubleshooting)
- API documentation (Swagger UI at /swagger)
- Dev auth guide (how to get token, use in curl)

### Performance & Reliability

✅ **12. Projection rebuild works correctly**
- POST /api/admin/projections/rebuild succeeds
- All projections rebuilt from event streams
- Rebuild time < 5 minutes for 10,000 events

✅ **13. Consistency checks run daily**
- Daily job verifies: balance integrity, no negative balances, no orphaned HUs
- Job logs results to observability platform
- Alerts on consistency violations

---

## Operational Checklist (Post-Sprint 4)

This checklist validates that the system is production-ready:

### Data Integrity
- [ ] Run consistency check job manually, verify no errors
- [ ] Verify projection lag < 1 second under normal load
- [ ] Verify all FKs enforced (no orphaned records)
- [ ] Verify audit trail complete (all events have operator, timestamp)

### Security
- [ ] Dev auth disabled in production (ASPNETCORE_ENVIRONMENT=Production)
- [ ] HTTPS enforced on all endpoints
- [ ] RBAC roles enforced (test unauthorized access returns 403)
- [ ] Sensitive data encrypted at rest (appsettings connection strings)

### Performance
- [ ] API latency < 500ms (p95) under normal load (50 requests/sec)
- [ ] Projection lag < 1 second (p95)
- [ ] Database indexes in place (verify EXPLAIN plans)
- [ ] Health check responds < 100ms

### Observability
- [ ] Logs shipped to centralized logging (Seq, ELK, Azure Monitor)
- [ ] Metrics exported to Grafana
- [ ] Alerts configured and tested (trigger test alert, verify notification)
- [ ] Correlation IDs flow through all requests

### Business Continuity
- [ ] Backup strategy in place (daily PostgreSQL backups)
- [ ] Disaster recovery plan documented
- [ ] Runbook tested (simulate outage, follow recovery steps)
- [ ] Rollback plan tested (deploy old version, verify no data loss)

### User Acceptance
- [ ] Operator training completed (demonstrate all workflows)
- [ ] Operator can complete full workflow without assistance
- [ ] Feedback collected and critical issues resolved
- [ ] Sign-off from warehouse manager and finance

---

## Risks & Mitigations (Sprint 4)

| Risk | Impact | Mitigation |
|------|--------|------------|
| Integration tests flaky | CI pipeline unreliable | Isolate tests, use test containers for DB |
| Grafana setup complex | Observability delayed | Use pre-built dashboards, defer custom dashboards to Phase 2 |
| Consistency check job too slow | Daily job times out | Optimize queries, run incrementally (only check recent data) |
| Production deployment fails | Go-live delayed | Test deployment in staging first, have rollback plan |
| Operator training insufficient | User adoption low | Record training videos, create quick-start guide |

---

## Notes for Implementation

1. **Test Priority:** Integration tests > Manual testing > Unit tests
2. **Observability:** Use existing OpenTelemetry setup, add Grafana dashboards
3. **RBAC:** Enforce at API layer (authorize attributes), not DB layer
4. **Reports:** Use existing projection tables, add CSV export endpoints
5. **Performance:** Focus on query optimization (indexes), not caching (complexity)
6. **Documentation:** Markdown in `docs/`, Swagger for API, training videos in Wiki
7. **Deployment:** Docker Compose for staging, Kubernetes for production (if required)
8. **Monitoring:** Use Seq for logs, Grafana for metrics, PagerDuty for alerts
9. **Backup:** Automated PostgreSQL backups, 30-day retention
10. **Go-Live:** Phased rollout (pilot warehouse first, then full deployment)

---

**End of Sprint 4 Task Pack**

**Next Step:** Update `prod-ready-tasks-progress.md` with new BATON token


## Task PRD-1542: E2E Outbound Workflow Test

**Epic:** Testing
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** QA
**Dependencies:** PRD-1527 (Create Sales Order UI), PRD-1528 (Sales Order List), PRD-1529 (Allocation), PRD-1530 (Picking), PRD-1531 (Packing), PRD-1532 (Dispatch)
**SourceRefs:** Universe §3.Workflow 2 (Order-to-Cash)

### Context

- Sprint 3 delivered full outbound UI (sales order → allocation → pick → pack → dispatch)
- Need integration test validating end-to-end flow: Create order → Allocate → Pick → Pack → Dispatch → Delivery
- Test must cover happy path + failure scenarios (insufficient stock, allocation conflict, packing mismatch)
- Uses API-level testing (not UI automation)

### Scope

**In Scope:**
- Integration test class: `OutboundWorkflowIntegrationTests`
- Test scenarios: 7 scenarios (happy path, allocation success, insufficient stock, picking, packing, dispatch, delivery confirmation)
- Test fixtures: seeded customer, items, stock
- Assertions: database state, projection updates, event emission

**Out of Scope:**
- UI automation (deferred to Phase 2)
- Performance load testing (separate task)
- Multi-order scenarios (wave picking)

### Requirements

**Functional:**
1. Test setup: seed database with Customer, Items, Stock (AvailableStock projection)
2. Test 1: Create sales order → Auto-allocate → Release → Pick → Pack → Dispatch → Verify SHIPPED status
3. Test 2: Create order with sufficient stock → Allocation succeeds → Reservation created (SOFT lock)
4. Test 3: Create order with insufficient stock → Order status PENDING_STOCK
5. Test 4: Release to picking → Reservation lock type changes SOFT → HARD
6. Test 5: Pick items → StockMoved (Storage → PICKING_STAGING), Reservation consumed
7. Test 6: Pack order → Shipment created, StockMoved (PICKING_STAGING → SHIPPING), Label generated
8. Test 7: Dispatch → Shipment status DISPATCHED, Customer notified, ERP event emitted

**Non-Functional:**
1. Test isolation: each test runs in transaction, rolled back after
2. Test execution time: < 45 seconds for all scenarios
3. CI/CD integration: tests run on every commit
4. Coverage: critical paths only

**Test Pattern:**
```csharp
[Collection("Integration")]
public class OutboundWorkflowIntegrationTests : IClassFixture<WarehouseTestFixture>
{
    [Fact]
    public async Task FullOutboundWorkflow_HappyPath()
    {
        // Arrange
        var customerId = await SeedCustomer("ACME Corp");
        var itemId = await SeedItem("FG-0001");
        await SeedStock(itemId, location: "A1-B1", qty: 100);

        // Act: Create sales order
        var orderId = await CreateSalesOrder(customerId, itemId, qty: 50);

        // Assert: Order created
        var order = await GetSalesOrder(orderId);
        Assert.Equal(SalesOrderStatus.DRAFT, order.Status);

        // Act: Submit for allocation
        await SubmitSalesOrder(orderId);

        // Assert: Allocated
        order = await GetSalesOrder(orderId);
        Assert.Equal(SalesOrderStatus.ALLOCATED, order.Status);
        Assert.NotNull(order.ReservationId);

        // Act: Release to picking
        await ReleaseToPicking(orderId);

        // Assert: Picking status
        order = await GetSalesOrder(orderId);
        Assert.Equal(SalesOrderStatus.PICKING, order.Status);

        // Act: Pick items
        var reservation = await GetReservation(order.ReservationId.Value);
        await PickItems(reservation.Id, itemId, qty: 50);

        // Assert: Stock moved to PICKING_STAGING
        var stock = await QueryAvailableStock(itemId, "PICKING_STAGING");
        Assert.Equal(50, stock.OnHandQty);

        // Act: Pack order
        var shipmentId = await PackOrder(orderId);

        // Assert: Shipment created, stock moved to SHIPPING
        var shipment = await GetShipment(shipmentId);
        Assert.Equal(ShipmentStatus.PACKED, shipment.Status);
        var shippingStock = await QueryAvailableStock(itemId, "SHIPPING");
        Assert.Equal(50, shippingStock.OnHandQty);

        // Act: Dispatch
        await DispatchShipment(shipmentId, carrier: "FedEx", trackingNumber: "123456789");

        // Assert: Dispatched
        shipment = await GetShipment(shipmentId);
        Assert.Equal(ShipmentStatus.DISPATCHED, shipment.Status);
        order = await GetSalesOrder(orderId);
        Assert.Equal(SalesOrderStatus.SHIPPED, order.Status);

        // Assert: Events emitted
        var events = await GetEvents(orderId);
        Assert.Contains(events, e => e.EventType == "SalesOrderCreated");
        Assert.Contains(events, e => e.EventType == "SalesOrderAllocated");
        Assert.Contains(events, e => e.EventType == "SalesOrderReleased");
        Assert.Contains(events, e => e.EventType == "ShipmentPacked");
        Assert.Contains(events, e => e.EventType == "ShipmentDispatched");
    }

    [Fact]
    public async Task InsufficientStock_OrderPendingStock()
    {
        // Arrange
        var customerId = await SeedCustomer("ACME Corp");
        var itemId = await SeedItem("FG-0002");
        await SeedStock(itemId, location: "A1-B1", qty: 10); // Only 10 available

        // Act: Create order for 50 units
        var orderId = await CreateSalesOrder(customerId, itemId, qty: 50);
        await SubmitSalesOrder(orderId);

        // Assert: Order status PENDING_STOCK
        var order = await GetSalesOrder(orderId);
        Assert.Equal(SalesOrderStatus.PENDING_STOCK, order.Status);
        Assert.Null(order.ReservationId);
    }

    [Fact]
    public async Task AllocationConflict_SecondOrderWaits()
    {
        // Arrange
        var customerId = await SeedCustomer("ACME Corp");
        var itemId = await SeedItem("FG-0003");
        await SeedStock(itemId, location: "A1-B1", qty: 100);

        // Act: Create two orders for same item
        var order1Id = await CreateSalesOrder(customerId, itemId, qty: 80);
        var order2Id = await CreateSalesOrder(customerId, itemId, qty: 50);

        await SubmitSalesOrder(order1Id);
        await SubmitSalesOrder(order2Id);

        // Assert: Order 1 allocated, Order 2 pending
        var order1 = await GetSalesOrder(order1Id);
        var order2 = await GetSalesOrder(order2Id);
        Assert.Equal(SalesOrderStatus.ALLOCATED, order1.Status);
        Assert.Equal(SalesOrderStatus.PENDING_STOCK, order2.Status); // Insufficient remaining stock
    }
}
```

### Acceptance Criteria

```gherkin
Feature: End-to-End Outbound Workflow

Scenario: Complete outbound workflow (happy path)
  Given Customer "ACME Corp", Item "FG-0001" with stock 100 units at location A1-B1
  When create SalesOrder with qty 50
  And submit for allocation
  Then order status = ALLOCATED
  And reservation created with SOFT lock
  When release to picking
  Then reservation lock type = HARD
  When pick items qty 50
  Then stock moved to PICKING_STAGING
  When pack order
  Then shipment created with status PACKED
  And stock moved to SHIPPING
  When dispatch shipment with carrier "FedEx"
  Then shipment status = DISPATCHED
  And order status = SHIPPED
  And events emitted: SalesOrderCreated, SalesOrderAllocated, SalesOrderReleased, ShipmentPacked, ShipmentDispatched

Scenario: Insufficient stock allocation
  Given Item "FG-0002" with stock 10 units
  When create SalesOrder with qty 50
  And submit for allocation
  Then order status = PENDING_STOCK
  And no reservation created

Scenario: Allocation conflict (two orders, limited stock)
  Given Item "FG-0003" with stock 100 units
  When create Order1 with qty 80
  And create Order2 with qty 50
  And submit both orders
  Then Order1 status = ALLOCATED (first come, first served)
  And Order2 status = PENDING_STOCK (insufficient remaining)

Scenario: Picking reduces available stock
  Given allocated order with qty 50
  When pick items
  Then AvailableStock at storage location reduced by 50
  And stock appears at PICKING_STAGING location

Scenario: Packing consolidates items
  Given picked items at PICKING_STAGING
  When pack order
  Then shipment HU created
  And stock moved from PICKING_STAGING to SHIPPING
  And shipping label generated

Scenario: Dispatch triggers notifications
  Given packed shipment
  When dispatch with carrier "FedEx", tracking "123456789"
  Then ShipmentDispatched event emitted
  And customer notification sent (email/SMS)
  And ERP integration event published

Scenario: Delivery confirmation completes order
  Given dispatched shipment
  When confirm delivery with signature + photo
  Then shipment status = DELIVERED
  And order status = DELIVERED
  And DeliveryConfirmed event emitted
```

### Validation / Checks

**Local Testing:**
```bash
# Run integration tests
dotnet test --filter "FullyQualifiedName~OutboundWorkflowIntegrationTests" --logger "console;verbosity=detailed"

# Check test coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov

# Run in CI
# Expected: all tests pass, coverage > 80% for outbound workflow classes
```

**Metrics:**
- `integration_test_duration_ms` (histogram, test_name label)
- `integration_test_failures_total` (counter, test_name label)

**Logs:**
- INFO: "Integration test started: {TestName}"
- INFO: "Integration test completed: {TestName}, Duration: {DurationMs}ms"

### Definition of Done

- [ ] `OutboundWorkflowIntegrationTests` class created in `src/tests/LKvitai.MES.Tests.Integration`
- [ ] 7 test scenarios implemented (happy path, allocation, insufficient stock, picking, packing, dispatch, delivery)
- [ ] Test fixtures: `WarehouseTestFixture` with database seeding
- [ ] All tests pass locally
- [ ] Tests integrated into CI pipeline
- [ ] Test execution time < 45 seconds
- [ ] Code coverage report generated
- [ ] Documentation: `docs/testing-guide.md` updated
- [ ] Code review completed

---

## Task PRD-1543: E2E Stock Movement Test

**Epic:** Testing
**Phase:** 1.5
**Sprint:** 4
**Estimate:** S (0.5 day)
**OwnerType:** QA
**Dependencies:** PRD-1526 (Stock Movement UI)
**SourceRefs:** Universe §3.Workflow 3 (Stock Movement)

### Context

- Sprint 3 delivered stock movement/transfer UI
- Need integration test validating: Transfer request → Approval → Execution → Stock moved
- Simpler than inbound/outbound workflows (fewer steps)
- Test must cover inter-location transfers and logical warehouse transfers

### Scope

**In Scope:**
- Integration test class: `StockMovementIntegrationTests`
- Test scenarios: 5 scenarios (simple transfer, approval workflow, logical warehouse transfer, capacity check, audit trail)
- Test fixtures: seeded items, locations, stock

**Out of Scope:**
- Physical inter-building transfers (single warehouse assumption)
- Wave picking transfers (separate epic)

### Requirements

**Functional:**
1. Test 1: Simple transfer (A1-B1 → A2-B2) → StockMoved event, AvailableStock updated
2. Test 2: Transfer requiring approval (SCRAP destination) → Manager approval required
3. Test 3: Logical warehouse transfer (RES → PROD) → Logical warehouse field updated
4. Test 4: Transfer to full location → Warning shown but allowed
5. Test 5: Audit trail → All transfers logged with operator, timestamp, reason

**Non-Functional:**
1. Test execution time: < 15 seconds
2. Test isolation: transactions rolled back

**Test Pattern:**
```csharp
[Collection("Integration")]
public class StockMovementIntegrationTests : IClassFixture<WarehouseTestFixture>
{
    [Fact]
    public async Task SimpleTransfer_StockMoved()
    {
        // Arrange
        var itemId = await SeedItem("RM-0001");
        await SeedStock(itemId, location: "A1-B1", qty: 100);

        // Act: Create transfer
        var transferId = await CreateTransfer(itemId, fromLocation: "A1-B1", toLocation: "A2-B2", qty: 50);
        await ExecuteTransfer(transferId);

        // Assert: Stock moved
        var fromStock = await QueryAvailableStock(itemId, "A1-B1");
        var toStock = await QueryAvailableStock(itemId, "A2-B2");
        Assert.Equal(50, fromStock.OnHandQty);
        Assert.Equal(50, toStock.OnHandQty);

        // Assert: Event emitted
        var events = await GetEvents(transferId);
        Assert.Contains(events, e => e.EventType == "StockMoved");
    }

    [Fact]
    public async Task TransferToScrap_RequiresApproval()
    {
        // Arrange
        var itemId = await SeedItem("RM-0002");
        await SeedStock(itemId, location: "A1-B1", qty: 20);

        // Act: Create transfer to SCRAP
        var transferId = await CreateTransfer(itemId, fromLocation: "A1-B1", toLocation: "SCRAP", qty: 10);

        // Assert: Status PENDING_APPROVAL
        var transfer = await GetTransfer(transferId);
        Assert.Equal(TransferStatus.PENDING_APPROVAL, transfer.Status);

        // Act: Approve
        await ApproveTransfer(transferId, approver: "Manager");
        await ExecuteTransfer(transferId);

        // Assert: Stock moved to SCRAP
        var scrapStock = await QueryAvailableStock(itemId, "SCRAP");
        Assert.Equal(10, scrapStock.OnHandQty);
    }

    [Fact]
    public async Task LogicalWarehouseTransfer()
    {
        // Arrange
        var itemId = await SeedItem("RM-0003");
        await SeedStock(itemId, location: "A1-B1", logicalWarehouse: "RES", qty: 50);

        // Act: Transfer RES → PROD
        var transferId = await CreateLogicalWarehouseTransfer(itemId, fromWH: "RES", toWH: "PROD", qty: 30);
        await ExecuteTransfer(transferId);

        // Assert: Logical warehouse updated
        var stock = await QueryAvailableStock(itemId, "A1-B1");
        Assert.Equal("PROD", stock.LogicalWarehouse);
        Assert.Equal(30, stock.OnHandQty);
    }
}
```

### Acceptance Criteria

```gherkin
Feature: Stock Movement

Scenario: Simple location transfer
  Given Item "RM-0001" with stock 100 units at A1-B1
  When create transfer: A1-B1 → A2-B2, qty 50
  And execute transfer
  Then stock at A1-B1 = 50
  And stock at A2-B2 = 50
  And StockMoved event emitted

Scenario: Transfer to SCRAP requires approval
  Given Item "RM-0002" with stock 20 units at A1-B1
  When create transfer: A1-B1 → SCRAP, qty 10
  Then transfer status = PENDING_APPROVAL
  When manager approves
  And execute transfer
  Then stock at SCRAP = 10

Scenario: Logical warehouse transfer
  Given Item "RM-0003" with stock 50 units at A1-B1, logical warehouse RES
  When create logical warehouse transfer: RES → PROD, qty 30
  And execute transfer
  Then stock logical warehouse = PROD
  And qty = 30

Scenario: Transfer to full location shows warning
  Given Location A2-B2 at 85% capacity
  When create transfer to A2-B2
  Then warning shown: "Location near capacity"
  And transfer allowed (not blocked)

Scenario: Audit trail completeness
  Given transfer executed
  When query audit log
  Then transfer logged with: operator, timestamp, from/to locations, qty, reason
```

### Validation / Checks

**Local Testing:**
```bash
# Run integration tests
dotnet test --filter "FullyQualifiedName~StockMovementIntegrationTests" --logger "console;verbosity=detailed"

# Expected: all tests pass, execution time < 15 seconds
```

### Definition of Done

- [ ] `StockMovementIntegrationTests` class created
- [ ] 5 test scenarios implemented
- [ ] All tests pass locally
- [ ] Tests integrated into CI pipeline
- [ ] Test execution time < 15 seconds
- [ ] Code review completed

---

## Task PRD-1544: Health Check Dashboard

**Epic:** Observability
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** Infra/DevOps
**Dependencies:** PRD-1539 (Correlation Tracing)
**SourceRefs:** Universe §5.Observability

### Context

- Phase 1 has basic `/health` endpoint (200 OK)
- No detailed health checks (database, event store, message queue, external APIs)
- Need comprehensive health dashboard showing system status
- Must support Kubernetes liveness/readiness probes

### Scope

**In Scope:**
- Health check endpoint: `/health` (summary), `/health/detailed` (full status)
- Checks: Database (PostgreSQL), Event Store (Marten), Message Queue (MassTransit), Blob Storage, External APIs (FedEx, Agnum)
- Health UI: `/health-ui` (HTML dashboard with auto-refresh)
- Kubernetes probes: liveness (basic), readiness (detailed)
- Degraded state handling (some checks fail but system operational)

**Out of Scope:**
- Custom health check logic per feature (use built-in checks)
- Historical health data (use monitoring system)

### Requirements

**Functional:**
1. `/health` endpoint: returns 200 if all critical checks pass, 503 if any critical check fails
2. `/health/detailed` endpoint: returns JSON with per-check status (Healthy, Degraded, Unhealthy)
3. Health checks:
   - Database: SELECT 1 query (timeout 5s)
   - Event Store: Query stream count (timeout 5s)
   - Message Queue: Check connection status
   - Blob Storage: List containers (timeout 5s)
   - FedEx API: HEAD request to API endpoint (timeout 10s)
   - Agnum API: HEAD request (timeout 10s)
4. Health UI: HTML page with table (Check Name, Status, Duration, Last Check Time)
5. Auto-refresh: every 30 seconds
6. Degraded state: If non-critical check fails (e.g., FedEx API), return 200 but mark check as Degraded

**Non-Functional:**
1. Performance: Health check execution < 10 seconds total
2. Timeout: Individual checks timeout after 5-10 seconds
3. Caching: Cache health check results for 10 seconds (avoid overload)
4. Logging: Log health check failures at WARN level

**API Response:**
```json
// GET /health
{
  "status": "Healthy",
  "totalDuration": "00:00:02.345"
}

// GET /health/detailed
{
  "status": "Healthy",
  "totalDuration": "00:00:02.345",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.123",
      "description": "PostgreSQL connection OK"
    },
    "event_store": {
      "status": "Healthy",
      "duration": "00:00:00.234",
      "description": "Marten event store OK"
    },
    "message_queue": {
      "status": "Healthy",
      "duration": "00:00:00.045",
      "description": "MassTransit connected"
    },
    "fedex_api": {
      "status": "Degraded",
      "duration": "00:00:10.000",
      "description": "FedEx API timeout (non-critical)"
    }
  }
}
```

**Implementation:**
```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database", timeout: TimeSpan.FromSeconds(5))
    .AddCheck<MartenHealthCheck>("event_store")
    .AddCheck<MassTransitHealthCheck>("message_queue")
    .AddCheck<BlobStorageHealthCheck>("blob_storage")
    .AddCheck<FedExApiHealthCheck>("fedex_api", tags: new[] { "external", "non-critical" })
    .AddCheck<AgnumApiHealthCheck>("agnum_api", tags: new[] { "external", "non-critical" });

app.MapHealthChecks("/health", new HealthCheckOptions {
    Predicate = _ => true,
    ResponseWriter = (context, report) => {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString()
        });
        return context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/detailed", new HealthCheckOptions {
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(options => options.UIPath = "/health-ui");
```

### Acceptance Criteria

```gherkin
Feature: Health Check Dashboard

Scenario: All checks pass
  Given all systems operational
  When GET /health
  Then response status 200
  And response body: {"status":"Healthy"}

Scenario: Critical check fails
  Given database connection fails
  When GET /health
  Then response status 503
  And response body: {"status":"Unhealthy"}

Scenario: Non-critical check fails (degraded)
  Given FedEx API unavailable
  When GET /health
  Then response status 200 (system still operational)
  And GET /health/detailed shows fedex_api status "Degraded"

Scenario: Detailed health check
  Given all systems operational
  When GET /health/detailed
  Then response includes entries for: database, event_store, message_queue, blob_storage, fedex_api, agnum_api
  And each entry includes: status, duration, description

Scenario: Health UI dashboard
  Given browser navigates to /health-ui
  Then HTML page renders with table
  And table shows: Check Name, Status (color-coded), Duration, Last Check Time
  And page auto-refreshes every 30 seconds

Scenario: Kubernetes liveness probe
  Given Kubernetes liveness probe configured: GET /health
  When database fails
  Then probe returns 503
  And Kubernetes restarts pod

Scenario: Kubernetes readiness probe
  Given Kubernetes readiness probe configured: GET /health/detailed
  When FedEx API degraded (non-critical)
  Then probe returns 200
  And pod remains in service (traffic continues)
```

### Validation / Checks

**Local Testing:**
```bash
# Start API
dotnet run --project src/LKvitai.MES.Api

# Test health endpoint
curl http://localhost:5000/health
# Expected: {"status":"Healthy","totalDuration":"00:00:02.345"}

# Test detailed health
curl http://localhost:5000/health/detailed | jq
# Expected: JSON with all checks

# Test health UI
open http://localhost:5000/health-ui
# Expected: HTML dashboard with table

# Simulate database failure
docker stop warehouse-postgres
curl http://localhost:5000/health
# Expected: 503 {"status":"Unhealthy"}

# Restart database
docker start warehouse-postgres
curl http://localhost:5000/health
# Expected: 200 {"status":"Healthy"}
```

**Metrics:**
- `health_check_duration_ms` (histogram, check_name label)
- `health_check_failures_total` (counter, check_name label)

**Logs:**
- WARN: "Health check failed: {CheckName}, Duration: {Duration}, Error: {ErrorMessage}"
- INFO: "Health check passed: {CheckName}, Duration: {Duration}"

### Definition of Done

- [ ] Health checks configured: database, event_store, message_queue, blob_storage, fedex_api, agnum_api
- [ ] `/health` endpoint returns 200/503 based on critical checks
- [ ] `/health/detailed` endpoint returns JSON with per-check status
- [ ] Health UI at `/health-ui` renders HTML dashboard
- [ ] Auto-refresh every 30 seconds
- [ ] Degraded state handling (non-critical checks)
- [ ] Kubernetes liveness/readiness probe examples in docs
- [ ] Unit tests: health check logic
- [ ] Integration tests: simulate failures
- [ ] Documentation: `docs/health-checks.md`
- [ ] Code review completed

---

## Task PRD-1545: Metrics Dashboards (Grafana)

**Epic:** Observability
**Phase:** 1.5
**Sprint:** 4
**Estimate:** L (2 days)
**OwnerType:** Infra/DevOps
**Dependencies:** PRD-1544 (Health Checks)
**SourceRefs:** Universe §5.Observability

### Context

- Phase 1 has OpenTelemetry metrics export configured
- No visualization dashboards (operators/managers cannot see system health)
- Need Grafana dashboards showing: API latency, event throughput, projection lag, business metrics
- Must support real-time monitoring and historical analysis

### Scope

**In Scope:**
- Grafana setup (Docker Compose for local, Kubernetes for production)
- 4 dashboards: System Health, API Performance, Event Sourcing, Business Metrics
- Prometheus data source configuration
- Pre-built dashboard JSON templates
- Alert rules (configured in PRD-1546)

**Out of Scope:**
- Custom alerting (separate task PRD-1546)
- Log aggregation (use Seq separately)
- APM tracing visualization (use Jaeger separately)

### Requirements

**Functional:**
1. **System Health Dashboard:**
   - Panels: CPU usage, memory usage, disk I/O, network I/O
   - Health check status (from PRD-1544)
   - Pod/container status (if Kubernetes)
2. **API Performance Dashboard:**
   - Panels: Request rate (req/sec), Latency (p50, p95, p99), Error rate (%)
   - Breakdown by endpoint (top 10 slowest)
   - HTTP status code distribution
3. **Event Sourcing Dashboard:**
   - Panels: Event publish rate (events/sec), Projection lag (seconds), Saga duration (ms)
   - Event types breakdown (top 10)
   - Failed saga count
4. **Business Metrics Dashboard:**
   - Panels: Stock movements (count/hour), Orders created (count/day), Picks completed (count/hour)
   - On-hand value (gauge), Available stock (gauge)
   - Receiving throughput (items/hour), Dispatch throughput (shipments/hour)

**Non-Functional:**
1. Dashboard refresh: 10 seconds
2. Data retention: 30 days (Prometheus)
3. Query performance: < 1 second per panel

**Grafana Setup:**
```yaml
# docker-compose.yml
services:
  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_INSTALL_PLUGINS=grafana-piechart-panel
    volumes:
      - ./grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./grafana/datasources:/etc/grafana/provisioning/datasources
      - grafana-data:/var/lib/grafana

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.retention.time=30d'
```

**Prometheus Config:**
```yaml
# prometheus.yml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'warehouse-api'
    static_configs:
      - targets: ['warehouse-api:5000']
    metrics_path: '/metrics'
```

**Dashboard JSON Template (System Health):**
```json
{
  "dashboard": {
    "title": "Warehouse System Health",
    "panels": [
      {
        "title": "API Request Rate",
        "targets": [
          {
            "expr": "rate(http_requests_total[5m])"
          }
        ],
        "type": "graph"
      },
      {
        "title": "API Latency (p95)",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_request_duration_ms_bucket[5m]))"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Health Check Status",
        "targets": [
          {
            "expr": "health_check_status"
          }
        ],
        "type": "stat"
      }
    ]
  }
}
```

### Acceptance Criteria

```gherkin
Feature: Grafana Dashboards

Scenario: System Health Dashboard
  Given Grafana running at http://localhost:3000
  When navigate to "Warehouse System Health" dashboard
  Then panels show: CPU usage, memory usage, API request rate, API latency, health check status
  And data refreshes every 10 seconds

Scenario: API Performance Dashboard
  Given API receiving traffic
  When view "API Performance" dashboard
  Then panels show: request rate, latency (p50/p95/p99), error rate
  And top 10 slowest endpoints listed
  And HTTP status code distribution (200, 400, 500)

Scenario: Event Sourcing Dashboard
  Given events being published
  When view "Event Sourcing" dashboard
  Then panels show: event publish rate, projection lag, saga duration
  And event types breakdown (top 10)
  And failed saga count

Scenario: Business Metrics Dashboard
  Given warehouse operations running
  When view "Business Metrics" dashboard
  Then panels show: stock movements, orders created, picks completed
  And on-hand value gauge
  And receiving/dispatch throughput

Scenario: Historical data query
  Given 7 days of metrics data
  When select time range "Last 7 days"
  Then all panels show historical trends
  And query completes in < 1 second per panel
```

### Validation / Checks

**Local Testing:**
```bash
# Start Grafana + Prometheus
docker-compose up -d grafana prometheus

# Verify Prometheus scraping
curl http://localhost:9090/api/v1/targets
# Expected: warehouse-api target UP

# Access Grafana
open http://localhost:3000
# Login: admin / admin

# Import dashboards
# Navigate to Dashboards → Import → Upload JSON files from grafana/dashboards/

# Generate test traffic
for i in {1..100}; do
  curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items
done

# Verify metrics in Grafana
# Expected: API request rate increases, latency visible
```

**Metrics:**
- All metrics from OpenTelemetry instrumentation (configured in Sprint 1)

**Logs:**
- INFO: "Grafana dashboard provisioned: {DashboardName}"

### Definition of Done

- [ ] Grafana Docker Compose configuration created
- [ ] Prometheus configuration created (scrape warehouse-api)
- [ ] 4 dashboard JSON templates created: System Health, API Performance, Event Sourcing, Business Metrics
- [ ] Dashboards provisioned automatically on Grafana startup
- [ ] Data source configured (Prometheus)
- [ ] All panels render correctly with test data
- [ ] Documentation: `docs/grafana-setup.md` with screenshots
- [ ] Code review completed

---

## Task PRD-1546: Alerting Rules & Runbook

**Epic:** Observability
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** Infra/DevOps
**Dependencies:** PRD-1544 (Health Checks), PRD-1545 (Grafana Dashboards)
**SourceRefs:** Universe §5.Monitoring

### Context

- Dashboards show metrics but no proactive alerting
- Need alerts for: API errors, projection lag, saga failures, health check failures
- Alerts must route to appropriate channels (email, Slack, PagerDuty)
- Runbook must document response procedures

### Scope

**In Scope:**
- Prometheus alert rules (5 critical alerts)
- Alertmanager configuration (routing, grouping, throttling)
- Notification channels: Email, Slack webhook
- Runbook document: `docs/runbook.md` (troubleshooting procedures)

**Out of Scope:**
- PagerDuty integration (Phase 2)
- SMS alerts (Phase 2)
- Custom alert logic (use Prometheus rules only)

### Requirements

**Functional:**
1. **Alert Rules:**
   - API Error Rate > 5% (critical)
   - API Latency p95 > 2 seconds (warning)
   - Projection Lag > 5 seconds (critical)
   - Saga Failure Rate > 10/hour (critical)
   - Health Check Failed (critical)
2. **Alertmanager:**
   - Route critical alerts to Slack + Email
   - Route warnings to Email only
   - Group alerts by service (avoid spam)
   - Throttle: max 1 alert per 5 minutes per rule
3. **Runbook:**
   - Per alert: Symptoms, Causes, Resolution steps, Escalation path
   - Common issues: Database connection lost, Event store full, Message queue down

**Non-Functional:**
1. Alert latency: < 1 minute from condition to notification
2. False positive rate: < 5% (tune thresholds)
3. Runbook accessibility: Markdown in repo, linked from alerts

**Prometheus Alert Rules:**
```yaml
# prometheus/alerts.yml
groups:
  - name: warehouse_api
    interval: 30s
    rules:
      - alert: HighAPIErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.05
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "High API error rate (> 5%)"
          description: "API error rate is {{ $value | humanizePercentage }} for the last 5 minutes"
          runbook_url: "https://github.com/yourorg/warehouse/blob/main/docs/runbook.md#high-api-error-rate"

      - alert: HighAPILatency
        expr: histogram_quantile(0.95, rate(http_request_duration_ms_bucket[5m])) > 2000
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High API latency (p95 > 2s)"
          description: "API p95 latency is {{ $value }}ms"
          runbook_url: "https://github.com/yourorg/warehouse/blob/main/docs/runbook.md#high-api-latency"

      - alert: ProjectionLagHigh
        expr: projection_lag_seconds > 5
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Projection lag > 5 seconds"
          description: "Projection {{ $labels.projection_name }} lag is {{ $value }}s"
          runbook_url: "https://github.com/yourorg/warehouse/blob/main/docs/runbook.md#projection-lag"

      - alert: SagaFailureRateHigh
        expr: rate(saga_failures_total[1h]) > 10
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Saga failure rate > 10/hour"
          description: "Saga {{ $labels.saga_name }} failing at {{ $value }}/hour"
          runbook_url: "https://github.com/yourorg/warehouse/blob/main/docs/runbook.md#saga-failures"

      - alert: HealthCheckFailed
        expr: health_check_status{check_name!~"fedex_api|agnum_api"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Health check failed: {{ $labels.check_name }}"
          description: "Critical health check {{ $labels.check_name }} is failing"
          runbook_url: "https://github.com/yourorg/warehouse/blob/main/docs/runbook.md#health-check-failed"
```

**Alertmanager Config:**
```yaml
# alertmanager/alertmanager.yml
global:
  resolve_timeout: 5m
  slack_api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'

route:
  receiver: 'default'
  group_by: ['alertname', 'service']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  routes:
    - match:
        severity: critical
      receiver: 'critical-alerts'
    - match:
        severity: warning
      receiver: 'warning-alerts'

receivers:
  - name: 'default'
    email_configs:
      - to: 'ops@example.com'
        from: 'alertmanager@example.com'
        smarthost: 'smtp.example.com:587'
        auth_username: 'alertmanager@example.com'
        auth_password: 'password'

  - name: 'critical-alerts'
    slack_configs:
      - channel: '#warehouse-alerts'
        title: '🚨 Critical Alert: {{ .GroupLabels.alertname }}'
        text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'
    email_configs:
      - to: 'ops@example.com,manager@example.com'

  - name: 'warning-alerts'
    email_configs:
      - to: 'ops@example.com'
```

**Runbook Template:**
```markdown
# Warehouse Operations Runbook

## High API Error Rate

**Symptoms:**
- API returning 500 errors
- Grafana dashboard shows error rate > 5%
- Alert: HighAPIErrorRate

**Causes:**
- Database connection lost
- Event store unavailable
- Unhandled exception in command handler

**Resolution:**
1. Check health endpoint: `curl http://api/health/detailed`
2. If database down: restart PostgreSQL container
3. If event store down: check Marten connection string
4. Check logs: `docker logs warehouse-api | grep ERROR`
5. If persistent: restart API pod

**Escalation:**
- If unresolved in 15 minutes: page on-call engineer
- If data loss suspected: escalate to CTO

## Projection Lag

**Symptoms:**
- Projection lag > 5 seconds
- UI shows stale data
- Alert: ProjectionLagHigh

**Causes:**
- High event publish rate (backlog)
- Projection handler slow query
- Database lock contention

**Resolution:**
1. Check projection lag metric: `projection_lag_seconds{projection_name="AvailableStock"}`
2. Check event backlog: query event store for unprocessed events
3. If backlog: scale projection consumers (increase replicas)
4. If slow query: check database query plan, add indexes
5. If lock contention: review transaction isolation level

**Escalation:**
- If lag > 30 seconds: page on-call engineer
- If affecting operations: notify warehouse manager

## Saga Failures

**Symptoms:**
- Saga failure rate > 10/hour
- Orders stuck in ALLOCATED status
- Alert: SagaFailureRateHigh

**Causes:**
- External API timeout (FedEx, Agnum)
- Concurrency conflict (optimistic locking)
- Invalid state transition

**Resolution:**
1. Check saga logs: `docker logs warehouse-api | grep "Saga failed"`
2. Identify failing saga: check `saga_name` label in alert
3. If external API: check API health, retry manually
4. If concurrency: review aggregate version conflicts
5. If state transition: review saga state machine logic

**Escalation:**
- If > 50 failures/hour: page on-call engineer
- If blocking orders: notify warehouse manager + sales team
```

### Acceptance Criteria

```gherkin
Feature: Alerting & Runbook

Scenario: High API error rate alert
  Given API error rate > 5% for 2 minutes
  When Prometheus evaluates alert rules
  Then HighAPIErrorRate alert fires
  And Slack notification sent to #warehouse-alerts
  And Email sent to ops@example.com, manager@example.com

Scenario: Projection lag alert
  Given projection lag > 5 seconds for 2 minutes
  When alert fires
  Then ProjectionLagHigh alert sent
  And runbook link included in notification

Scenario: Alert grouping
  Given 10 HighAPIErrorRate alerts fire within 1 minute
  When Alertmanager processes alerts
  Then alerts grouped into single notification
  And sent once (not 10 times)

Scenario: Alert throttling
  Given HighAPIErrorRate alert fires
  And resolved after 1 minute
  And fires again after 2 minutes
  When Alertmanager processes
  Then second alert throttled (not sent until 5 minutes elapsed)

Scenario: Runbook access
  Given alert notification received
  When click runbook_url link
  Then runbook opens in browser
  And shows resolution steps for alert

Scenario: Health check alert excludes non-critical
  Given FedEx API health check fails (non-critical)
  When Prometheus evaluates HealthCheckFailed rule
  Then alert does NOT fire (excluded by check_name filter)
```

### Validation / Checks

**Local Testing:**
```bash
# Start Alertmanager
docker-compose up -d alertmanager

# Verify Alertmanager config
curl http://localhost:9093/api/v1/status
# Expected: config loaded, receivers configured

# Trigger test alert (simulate high error rate)
# Inject errors into API
for i in {1..100}; do
  curl http://localhost:5000/api/invalid-endpoint  # Returns 404
done

# Wait 2 minutes, check Prometheus alerts
curl http://localhost:9090/api/v1/alerts
# Expected: HighAPIErrorRate alert firing

# Check Alertmanager
curl http://localhost:9093/api/v1/alerts
# Expected: alert routed to critical-alerts receiver

# Verify Slack notification (check #warehouse-alerts channel)
# Verify Email notification (check ops@example.com inbox)

# Test runbook link
# Click link in alert notification
# Expected: runbook.md opens with resolution steps
```

**Metrics:**
- `alertmanager_notifications_total` (counter, receiver label)
- `alertmanager_notifications_failed_total` (counter, receiver label)

**Logs:**
- INFO: "Alert fired: {AlertName}, Severity: {Severity}"
- INFO: "Notification sent: {Receiver}, Alert: {AlertName}"
- ERROR: "Notification failed: {Receiver}, Error: {ErrorMessage}"

### Definition of Done

- [ ] Prometheus alert rules created (5 rules)
- [ ] Alertmanager configuration created (routing, grouping, throttling)
- [ ] Slack webhook configured
- [ ] Email SMTP configured
- [ ] Runbook document created: `docs/runbook.md`
- [ ] Runbook includes: High API Error Rate, High API Latency, Projection Lag, Saga Failures, Health Check Failed
- [ ] Alert notifications tested (Slack + Email)
- [ ] Alert grouping/throttling tested
- [ ] Runbook links accessible from alerts
- [ ] Documentation: `docs/alerting-setup.md`
- [ ] Code review completed

---

## Task PRD-1547: RBAC Enforcement

**Epic:** RBAC
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1521 (Dev Auth)
**SourceRefs:** Universe §5.Security

### Context

- Phase 1 has role definitions (Admin, Manager, Operator, QCInspector) but no enforcement
- All authenticated users can access all endpoints (security gap)
- Need API-level authorization checks using `[Authorize(Roles = "...")]` attributes
- Must align with operational workflows (e.g., only Manager can approve orders)

### Scope

**In Scope:**
- Authorization policies for all API endpoints
- Role-based access control (RBAC) enforcement
- Forbidden (403) responses for unauthorized access
- Unit tests for authorization logic

**Out of Scope:**
- Fine-grained permissions (e.g., "can edit item category X") - deferred to Phase 2
- Attribute-based access control (ABAC) - deferred
- User management UI (PRD-1548)

### Requirements

**Functional:**
1. **Role Definitions:**
   - Admin: Full access (all endpoints)
   - Manager: Approve orders, adjust costs, manage users
   - Operator: Receive, pick, pack, dispatch
   - QCInspector: QC pass/fail only
2. **Endpoint Authorization:**
   - `/api/warehouse/v1/items` POST/PUT/DELETE: Admin only
   - `/api/warehouse/v1/sales-orders/{id}/approve`: Manager only
   - `/api/warehouse/v1/sales-orders/{id}/release`: Manager only
   - `/api/warehouse/v1/valuation/adjust-cost`: Manager, Admin
   - `/api/warehouse/v1/qc/inspect`: QCInspector, Manager, Admin
   - `/api/warehouse/v1/picks/execute`: Operator, Manager, Admin
   - `/api/warehouse/v1/shipments/{id}/dispatch`: Operator, Manager, Admin
   - All GET endpoints: All authenticated users
3. **Authorization Middleware:**
   - Validate JWT claims (role claim)
   - Return 403 Forbidden if role not authorized
   - Log unauthorized access attempts

**Non-Functional:**
1. Performance: Authorization check < 1ms (in-memory claim validation)
2. Security: Role claims signed in JWT (cannot be tampered)
3. Auditability: Log all 403 responses with user, endpoint, timestamp

**Implementation:**
```csharp
// Controllers
[ApiController]
[Route("api/warehouse/v1/items")]
[Authorize] // All endpoints require authentication
public class ItemsController : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")] // Only Admin can create items
    public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest request) { ... }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] UpdateItemRequest request) { ... }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteItem(int id) { ... }

    [HttpGet]
    // No role restriction - all authenticated users can view items
    public async Task<IActionResult> GetItems() { ... }
}

[ApiController]
[Route("api/warehouse/v1/sales-orders")]
[Authorize]
public class SalesOrdersController : ControllerBase
{
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> ApproveOrder(Guid id) { ... }

    [HttpPost("{id}/release")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> ReleaseOrder(Guid id) { ... }
}

[ApiController]
[Route("api/warehouse/v1/qc")]
[Authorize]
public class QCController : ControllerBase
{
    [HttpPost("inspect")]
    [Authorize(Roles = "QCInspector,Manager,Admin")]
    public async Task<IActionResult> InspectQC([FromBody] QCInspectRequest request) { ... }
}

// Middleware: Log unauthorized access
app.Use(async (context, next) => {
    await next();
    if (context.Response.StatusCode == 403) {
        var user = context.User.Identity?.Name ?? "Anonymous";
        var endpoint = context.Request.Path;
        logger.LogWarning("Unauthorized access attempt: User={User}, Endpoint={Endpoint}", user, endpoint);
    }
});
```

### Acceptance Criteria

```gherkin
Feature: RBAC Enforcement

Scenario: Admin can create items
  Given user with role "Admin"
  When POST /api/warehouse/v1/items
  Then response status 201
  And item created

Scenario: Operator cannot create items
  Given user with role "Operator"
  When POST /api/warehouse/v1/items
  Then response status 403
  And error message: "Forbidden: Insufficient permissions"

Scenario: Manager can approve orders
  Given user with role "Manager"
  When POST /api/warehouse/v1/sales-orders/{id}/approve
  Then response status 200
  And order approved

Scenario: Operator cannot approve orders
  Given user with role "Operator"
  When POST /api/warehouse/v1/sales-orders/{id}/approve
  Then response status 403

Scenario: QCInspector can inspect QC
  Given user with role "QCInspector"
  When POST /api/warehouse/v1/qc/inspect
  Then response status 200
  And QC inspection recorded

Scenario: Operator can pick items
  Given user with role "Operator"
  When POST /api/warehouse/v1/picks/execute
  Then response status 200
  And pick completed

Scenario: All authenticated users can view items
  Given user with any role (Admin, Manager, Operator, QCInspector)
  When GET /api/warehouse/v1/items
  Then response status 200
  And items list returned

Scenario: Unauthorized access logged
  Given user with role "Operator"
  When POST /api/warehouse/v1/items (Admin only)
  Then response status 403
  And log entry: "Unauthorized access attempt: User=operator-user, Endpoint=/api/warehouse/v1/items"
```

### Validation / Checks

**Local Testing:**
```bash
# Get dev token with Admin role
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Test Admin can create items
curl -H "Authorization: Bearer $ADMIN_TOKEN" -X POST http://localhost:5000/api/warehouse/v1/items \
  -H "Content-Type: application/json" \
  -d '{"sku":"TEST-001","name":"Test Item"}'
# Expected: 201 Created

# Get dev token with Operator role (modify DevAuthService to support role selection)
OPERATOR_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"operator","password":"Operator123!"}' | jq -r '.token')

# Test Operator cannot create items
curl -H "Authorization: Bearer $OPERATOR_TOKEN" -X POST http://localhost:5000/api/warehouse/v1/items \
  -H "Content-Type: application/json" \
  -d '{"sku":"TEST-002","name":"Test Item 2"}'
# Expected: 403 Forbidden

# Test Operator can pick items
curl -H "Authorization: Bearer $OPERATOR_TOKEN" -X POST http://localhost:5000/api/warehouse/v1/picks/execute \
  -H "Content-Type: application/json" \
  -d '{"reservationId":"...","itemId":1,"qty":10}'
# Expected: 200 OK

# Check logs for unauthorized access
docker logs warehouse-api | grep "Unauthorized access attempt"
# Expected: log entry with user=operator, endpoint=/api/warehouse/v1/items
```

**Metrics:**
- `authorization_failures_total` (counter, endpoint, role labels)

**Logs:**
- WARN: "Unauthorized access attempt: User={User}, Endpoint={Endpoint}, Role={Role}"

### Definition of Done

- [ ] All API controllers annotated with `[Authorize]` and `[Authorize(Roles = "...")]`
- [ ] Role-based authorization enforced on all endpoints
- [ ] 403 Forbidden responses for unauthorized access
- [ ] Unauthorized access logging middleware added
- [ ] Unit tests: authorization logic (10+ tests covering all roles)
- [ ] Integration tests: test each role's access (Admin, Manager, Operator, QCInspector)
- [ ] DevAuthService updated to support role selection (for testing)
- [ ] Documentation: `docs/rbac-matrix.md` (endpoint → roles mapping)
- [ ] Code review completed

---

## Task PRD-1548: Admin User Management UI

**Epic:** RBAC
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** PRD-1547 (RBAC Enforcement)
**SourceRefs:** Universe §5.Security

### Context

- RBAC enforcement in place (PRD-1547) but no UI to manage users/roles
- Admins currently cannot create users, assign roles, or deactivate users
- Need basic user management UI: list users, create user, edit roles, deactivate

### Scope

**In Scope:**
- Users list page (`/warehouse/admin/users`)
- Create user form (username, email, password, roles)
- Edit user form (change roles, deactivate)
- Role assignment (multi-select: Admin, Manager, Operator, QCInspector)

**Out of Scope:**
- Password reset workflow (deferred)
- SSO/OAuth integration (deferred to Phase 2)
- Audit log UI (deferred)

### Requirements

**Functional:**
1. Users list: table with columns (Username, Email, Roles, Status, Actions)
2. Create user: form with fields (Username, Email, Password, Roles multi-select, Status)
3. Edit user: form to update roles, deactivate user
4. Validation: Username unique, Email format, Password min 8 chars
5. API endpoints: POST /api/admin/users, PUT /api/admin/users/{id}, GET /api/admin/users

**Non-Functional:**
1. Only Admin role can access `/warehouse/admin/users`
2. Passwords hashed (bcrypt)
3. Responsive UI (works on tablet)

**UI Wireframe:**
```
┌─────────────────────────────────────────────────────────┐
│ User Management                          [+ Create User] │
├─────────────────────────────────────────────────────────┤
│ Username │ Email │ Roles │ Status │ Actions             │
│ admin    │ admin@example.com │ Admin │ Active │ [Edit]  │
│ operator1│ op1@example.com │ Operator │ Active │ [Edit] │
│ qc1      │ qc1@example.com │ QCInspector │ Active │ [Edit]│
└─────────────────────────────────────────────────────────┘

Create User:
┌─────────────────────────────────────────────────────────┐
│ Username: [____________]                                │
│ Email: [____________]                                   │
│ Password: [____________]                                │
│ Roles: [☑ Admin] [☐ Manager] [☑ Operator] [☐ QCInspector]│
│ Status: [Active ▼]                                      │
│                                                         │
│ [Cancel] [Create User]                                  │
└─────────────────────────────────────────────────────────┘
```

**API Contract:**
```csharp
// POST /api/admin/users
public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    List<string> Roles, // ["Admin", "Operator"]
    UserStatus Status
);

// PUT /api/admin/users/{id}
public record UpdateUserRequest(
    List<string> Roles,
    UserStatus Status
);

public enum UserStatus { Active, Inactive }
```

### Acceptance Criteria

```gherkin
Feature: User Management UI

Scenario: Admin views users list
  Given user with role "Admin"
  When navigate to /warehouse/admin/users
  Then users list displayed with columns: Username, Email, Roles, Status, Actions

Scenario: Admin creates new user
  Given user with role "Admin"
  When click "Create User"
  And fill form: Username="newuser", Email="new@example.com", Password="Password123!", Roles=["Operator"]
  And click "Create User"
  Then user created
  And redirect to users list
  And toast: "User newuser created"

Scenario: Admin edits user roles
  Given user "operator1" with roles ["Operator"]
  When admin clicks "Edit" for operator1
  And adds role "Manager"
  And clicks "Save"
  Then user roles updated to ["Operator", "Manager"]
  And toast: "User operator1 updated"

Scenario: Admin deactivates user
  Given user "qc1" with status "Active"
  When admin edits qc1
  And changes status to "Inactive"
  And clicks "Save"
  Then user status = Inactive
  And user cannot login

Scenario: Non-admin cannot access user management
  Given user with role "Operator"
  When navigate to /warehouse/admin/users
  Then response status 403
  And error message: "Forbidden: Admin access required"

Scenario: Validation errors
  Given admin creating user
  When username is empty
  Then validation error: "Username required"
  When email format invalid
  Then validation error: "Invalid email format"
  When password < 8 chars
  Then validation error: "Password must be at least 8 characters"
```

### Validation / Checks

**Local Testing:**
```bash
# Get admin token
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Create user via API
curl -H "Authorization: Bearer $ADMIN_TOKEN" -X POST http://localhost:5000/api/admin/users \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","email":"test@example.com","password":"Test123!","roles":["Operator"],"status":"Active"}'
# Expected: 201 Created

# Get users list
curl -H "Authorization: Bearer $ADMIN_TOKEN" http://localhost:5000/api/admin/users
# Expected: JSON array with users

# Test UI
open http://localhost:3000/warehouse/admin/users
# Login as admin
# Expected: users list displayed
# Click "Create User", fill form, submit
# Expected: user created, toast shown
```

**Metrics:**
- `users_created_total` (counter)
- `users_updated_total` (counter)

**Logs:**
- INFO: "User created: {Username}, Roles: {Roles}, CreatedBy: {AdminUsername}"
- INFO: "User updated: {Username}, Roles: {Roles}, UpdatedBy: {AdminUsername}"

### Definition of Done

- [ ] Users list page created (`/warehouse/admin/users`)
- [ ] Create user form implemented
- [ ] Edit user form implemented
- [ ] API endpoints: POST /api/admin/users, PUT /api/admin/users/{id}, GET /api/admin/users
- [ ] Password hashing (bcrypt)
- [ ] Validation: username unique, email format, password min 8 chars
- [ ] Authorization: only Admin can access
- [ ] Unit tests: user creation, role assignment, validation
- [ ] Integration tests: full user management workflow
- [ ] Documentation: `docs/user-management.md`
- [ ] Code review completed

---

## Task PRD-1549: Stock Movement History Report

**Epic:** Reports
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** UI/Backend
**Dependencies:** None
**SourceRefs:** Universe §1.Reports

### Context

- Phase 1 has basic stock level report (current balances only)
- No historical movement report (who moved what, when, why)
- Need audit trail report for compliance and troubleshooting
- Must support filters: date range, item, location, operator

### Scope

**In Scope:**
- Stock movement history report UI (`/warehouse/reports/stock-movements`)
- Filters: Date range, Item, Location, Operator, Movement type
- Table columns: Timestamp, Item, From Location, To Location, Qty, Operator, Reason
- CSV export
- API endpoint: GET /api/warehouse/v1/reports/stock-movements

**Out of Scope:**
- Real-time updates (manual refresh)
- Graphical charts (table only)

### Requirements

**Functional:**
1. Report page: `/warehouse/reports/stock-movements`
2. Filters: Date range (default: last 7 days), Item dropdown, Location dropdown, Operator dropdown, Movement type dropdown (Receive, Pick, Transfer, Adjust)
3. Table columns: Timestamp, Item SKU, Item Name, From Location, To Location, Qty, UoM, Operator, Reason
4. Pagination: 50 rows per page
5. CSV export button: downloads filtered results
6. API: GET /api/warehouse/v1/reports/stock-movements?startDate=...&endDate=...&itemId=...&locationId=...&operatorId=...&movementType=...

**Non-Functional:**
1. Query performance: < 3 seconds for 10,000 movements
2. CSV export: < 10 seconds for 100,000 movements
3. Accessible to all authenticated users

**API Response:**
```json
{
  "movements": [
    {
      "timestamp": "2026-02-10T14:30:00Z",
      "itemSku": "RM-0001",
      "itemName": "Bolt M8",
      "fromLocation": "RECEIVING",
      "toLocation": "A1-B1",
      "qty": 100,
      "uom": "EA",
      "operator": "operator1",
      "reason": "Putaway",
      "movementType": "Transfer"
    }
  ],
  "totalCount": 1523,
  "page": 1,
  "pageSize": 50
}
```

**Database Query:**
```sql
SELECT
  sm.timestamp,
  i.sku AS item_sku,
  i.name AS item_name,
  l_from.code AS from_location,
  l_to.code AS to_location,
  sm.qty,
  u.code AS uom,
  sm.operator,
  sm.reason,
  sm.movement_type
FROM stock_movements sm
JOIN items i ON sm.item_id = i.id
LEFT JOIN locations l_from ON sm.from_location_id = l_from.id
LEFT JOIN locations l_to ON sm.to_location_id = l_to.id
JOIN units_of_measure u ON i.uom_id = u.id
WHERE sm.timestamp BETWEEN :startDate AND :endDate
  AND (:itemId IS NULL OR sm.item_id = :itemId)
  AND (:locationId IS NULL OR sm.from_location_id = :locationId OR sm.to_location_id = :locationId)
  AND (:operatorId IS NULL OR sm.operator = :operatorId)
  AND (:movementType IS NULL OR sm.movement_type = :movementType)
ORDER BY sm.timestamp DESC
LIMIT :pageSize OFFSET :offset;
```

### Acceptance Criteria

```gherkin
Feature: Stock Movement History Report

Scenario: View stock movements (last 7 days)
  Given user navigates to /warehouse/reports/stock-movements
  When page loads
  Then table shows stock movements from last 7 days
  And columns: Timestamp, Item SKU, Item Name, From Location, To Location, Qty, UoM, Operator, Reason
  And pagination: 50 rows per page

Scenario: Filter by item
  Given stock movements report loaded
  When select item "RM-0001" from dropdown
  And click "Apply Filters"
  Then table shows only movements for RM-0001

Scenario: Filter by date range
  Given stock movements report loaded
  When select date range: 2026-02-01 to 2026-02-10
  And click "Apply Filters"
  Then table shows movements within date range

Scenario: Filter by operator
  Given stock movements report loaded
  When select operator "operator1"
  And click "Apply Filters"
  Then table shows only movements by operator1

Scenario: CSV export
  Given stock movements report with 1523 results
  When click "Export CSV"
  Then CSV file downloads
  And CSV contains all 1523 movements (not just current page)
  And CSV columns match table columns

Scenario: Pagination
  Given 1523 stock movements
  When view page 1
  Then shows rows 1-50
  When click "Next Page"
  Then shows rows 51-100
```

### Validation / Checks

**Local Testing:**
```bash
# Get token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Get stock movements
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/stock-movements?startDate=2026-02-01&endDate=2026-02-10&page=1&pageSize=50"
# Expected: JSON with movements array

# Test CSV export
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/stock-movements/export?startDate=2026-02-01&endDate=2026-02-10" \
  -o stock-movements.csv
# Expected: CSV file downloaded

# Test UI
open http://localhost:3000/warehouse/reports/stock-movements
# Expected: report page with filters and table
```

**Metrics:**
- `report_queries_total` (counter, report_name label)
- `report_query_duration_ms` (histogram, report_name label)

**Logs:**
- INFO: "Stock movements report queried: StartDate={StartDate}, EndDate={EndDate}, Filters={Filters}, ResultCount={Count}, Duration={Duration}ms"

### Definition of Done

- [ ] Report page created (`/warehouse/reports/stock-movements`)
- [ ] Filters implemented: Date range, Item, Location, Operator, Movement type
- [ ] Table with pagination (50 rows per page)
- [ ] CSV export functionality
- [ ] API endpoint: GET /api/warehouse/v1/reports/stock-movements
- [ ] Database query optimized (indexes on timestamp, item_id, location_id)
- [ ] Query performance < 3 seconds for 10,000 movements
- [ ] Unit tests: query logic, filters
- [ ] Integration tests: full report workflow
- [ ] Documentation: `docs/reports.md` updated
- [ ] Code review completed

---

## Task PRD-1550: Transaction Log Export

**Epic:** Reports
**Phase:** 1.5
**Sprint:** 4
**Estimate:** S (0.5 day)
**OwnerType:** Backend/API
**Dependencies:** None
**SourceRefs:** Universe §1.Reports

### Context

- Need full transaction log export for compliance audits
- Must export all StockMoved events from event store
- CSV format with all event metadata (timestamp, operator, correlation ID)
- Used for external audits, forensic analysis

### Scope

**In Scope:**
- API endpoint: GET /api/warehouse/v1/reports/transaction-log/export
- CSV export with columns: Event ID, Timestamp, Event Type, Aggregate ID, Operator, Correlation ID, Payload (JSON)
- Filters: Date range, Event type
- Streaming export (large files)

**Out of Scope:**
- UI (API only, called from admin tools)
- Real-time export (batch only)

### Requirements

**Functional:**
1. API endpoint: GET /api/warehouse/v1/reports/transaction-log/export?startDate=...&endDate=...&eventType=...
2. Query Marten event store: all events in date range
3. CSV columns: EventId, Timestamp, EventType, AggregateId, Operator, CorrelationId, Payload (JSON string)
4. Streaming response (avoid loading all events into memory)
5. Authorization: Admin only

**Non-Functional:**
1. Performance: Stream 100,000 events in < 30 seconds
2. Memory: Constant memory usage (streaming, not buffering)
3. File size: Support exports up to 1GB

**API Implementation:**
```csharp
[HttpGet("transaction-log/export")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> ExportTransactionLog(
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime endDate,
    [FromQuery] string? eventType = null)
{
    var events = _eventStore.QueryEvents(startDate, endDate, eventType);

    Response.ContentType = "text/csv";
    Response.Headers.Add("Content-Disposition", $"attachment; filename=transaction-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");

    await using var writer = new StreamWriter(Response.Body);
    await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

    // Write header
    csv.WriteField("EventId");
    csv.WriteField("Timestamp");
    csv.WriteField("EventType");
    csv.WriteField("AggregateId");
    csv.WriteField("Operator");
    csv.WriteField("CorrelationId");
    csv.WriteField("Payload");
    await csv.NextRecordAsync();

    // Stream events
    await foreach (var evt in events)
    {
        csv.WriteField(evt.Id);
        csv.WriteField(evt.Timestamp);
        csv.WriteField(evt.EventType);
        csv.WriteField(evt.AggregateId);
        csv.WriteField(evt.Metadata.GetValueOrDefault("Operator"));
        csv.WriteField(evt.Metadata.GetValueOrDefault("CorrelationId"));
        csv.WriteField(JsonSerializer.Serialize(evt.Data));
        await csv.NextRecordAsync();
    }

    return new EmptyResult();
}
```

### Acceptance Criteria

```gherkin
Feature: Transaction Log Export

Scenario: Export all events in date range
  Given 10,000 events in event store between 2026-02-01 and 2026-02-10
  When admin calls GET /api/warehouse/v1/reports/transaction-log/export?startDate=2026-02-01&endDate=2026-02-10
  Then CSV file streams to response
  And CSV contains 10,000 rows (plus header)
  And columns: EventId, Timestamp, EventType, AggregateId, Operator, CorrelationId, Payload

Scenario: Filter by event type
  Given events of types: StockMoved, StockAdjusted, PickCompleted
  When admin calls export with eventType=StockMoved
  Then CSV contains only StockMoved events

Scenario: Large export (100,000 events)
  Given 100,000 events in date range
  When admin calls export
  Then export completes in < 30 seconds
  And memory usage remains constant (streaming, not buffering)

Scenario: Non-admin cannot export
  Given user with role "Operator"
  When call export endpoint
  Then response status 403
  And error: "Forbidden: Admin access required"
```

### Validation / Checks

**Local Testing:**
```bash
# Get admin token
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Export transaction log
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/transaction-log/export?startDate=2026-02-01&endDate=2026-02-10" \
  -o transaction-log.csv

# Verify CSV
head -n 5 transaction-log.csv
# Expected: Header + 4 event rows

# Count rows
wc -l transaction-log.csv
# Expected: row count matches event count + 1 (header)

# Test large export (seed 100,000 events first)
time curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/transaction-log/export?startDate=2026-01-01&endDate=2026-02-10" \
  -o transaction-log-large.csv
# Expected: completes in < 30 seconds
```

**Metrics:**
- `transaction_log_exports_total` (counter)
- `transaction_log_export_duration_ms` (histogram)
- `transaction_log_export_rows` (histogram)

**Logs:**
- INFO: "Transaction log export started: StartDate={StartDate}, EndDate={EndDate}, EventType={EventType}, RequestedBy={User}"
- INFO: "Transaction log export completed: RowCount={Count}, Duration={Duration}ms, FileSize={Size}MB"

### Definition of Done

- [ ] API endpoint created: GET /api/warehouse/v1/reports/transaction-log/export
- [ ] Streaming CSV export implemented (CsvHelper library)
- [ ] Filters: Date range, Event type
- [ ] Authorization: Admin only
- [ ] Query Marten event store
- [ ] Performance: 100,000 events in < 30 seconds
- [ ] Memory: Constant usage (streaming)
- [ ] Unit tests: query logic, CSV formatting
- [ ] Integration tests: export workflow, large files
- [ ] Documentation: `docs/reports.md` updated
- [ ] Code review completed

---

## Task PRD-1551: Traceability Report (Lot → Order)

**Epic:** Reports
**Phase:** 1.5
**Sprint:** 4
**Estimate:** M (1 day)
**OwnerType:** UI/Backend
**Dependencies:** None
**SourceRefs:** Universe §1.Reports

### Context

- Compliance requirement: trace lot from supplier → customer (forward and backward)
- Need report showing: Lot received from supplier → used in production order → shipped to customer
- Critical for recalls (identify affected customers)

### Scope

**In Scope:**
- Traceability report UI (`/warehouse/reports/traceability`)
- Search by: Lot number, Item SKU, Sales Order, Supplier
- Report shows: Upstream (supplier, receipt date) and Downstream (production orders, sales orders, customers)
- API endpoint: GET /api/warehouse/v1/reports/traceability

**Out of Scope:**
- Graphical traceability tree (table only)
- Real-time updates

### Requirements

**Functional:**
1. Report page: `/warehouse/reports/traceability`
2. Search input: Lot number (primary), Item SKU, Sales Order, Supplier
3. Report sections:
   - **Upstream:** Supplier, Receipt Date, Inbound Shipment, Qty Received
   - **Current:** Item, Lot, Current Location, Available Qty
   - **Downstream:** Production Orders (if issued to production), Sales Orders (if picked), Customers (if shipped)
4. API: GET /api/warehouse/v1/reports/traceability?lotNumber=...

**Non-Functional:**
1. Query performance: < 2 seconds
2. Accessible to all authenticated users

**API Response:**
```json
{
  "lot": {
    "lotNumber": "LOT-A-2026-02-01",
    "itemSku": "RM-0001",
    "itemName": "Bolt M8"
  },
  "upstream": {
    "supplier": "ACME Corp",
    "receiptDate": "2026-02-01T10:00:00Z",
    "inboundShipment": "ISH-0001",
    "qtyReceived": 1000
  },
  "current": {
    "location": "A1-B1",
    "availableQty": 200
  },
  "downstream": {
    "productionOrders": [
      {
        "orderNumber": "PO-0001",
        "qtyIssued": 500,
        "issuedDate": "2026-02-05T14:00:00Z"
      }
    ],
    "salesOrders": [
      {
        "orderNumber": "SO-0001",
        "customer": "Customer A",
        "qtyShipped": 300,
        "shippedDate": "2026-02-08T16:00:00Z",
        "trackingNumber": "123456789"
      }
    ]
  }
}
```

**Database Query:**
```sql
-- Upstream: Find receipt
SELECT s.name AS supplier, ish.received_at, ish.shipment_number, ishl.qty
FROM lots l
JOIN inbound_shipment_lines ishl ON l.id = ishl.lot_id
JOIN inbound_shipments ish ON ishl.inbound_shipment_id = ish.id
JOIN suppliers s ON ish.supplier_id = s.id
WHERE l.lot_number = :lotNumber;

-- Current: Find available stock
SELECT loc.code AS location, SUM(as.qty) AS available_qty
FROM available_stock as
JOIN locations loc ON as.location_id = loc.id
WHERE as.lot_id = (SELECT id FROM lots WHERE lot_number = :lotNumber)
GROUP BY loc.code;

-- Downstream: Find production orders
SELECT po.order_number, sm.qty, sm.timestamp
FROM stock_movements sm
JOIN production_orders po ON sm.production_order_id = po.id
WHERE sm.lot_id = (SELECT id FROM lots WHERE lot_number = :lotNumber)
  AND sm.to_location = 'PRODUCTION';

-- Downstream: Find sales orders
SELECT so.order_number, c.name AS customer, sm.qty, sh.shipped_at, sh.tracking_number
FROM stock_movements sm
JOIN sales_orders so ON sm.sales_order_id = so.id
JOIN customers c ON so.customer_id = c.id
JOIN shipments sh ON so.id = sh.sales_order_id
WHERE sm.lot_id = (SELECT id FROM lots WHERE lot_number = :lotNumber)
  AND sm.to_location = 'SHIPPING';
```

### Acceptance Criteria

```gherkin
Feature: Traceability Report

Scenario: Trace lot from supplier to customer
  Given lot "LOT-A-2026-02-01" received from supplier "ACME Corp" on 2026-02-01
  And 500 units issued to production order PO-0001
  And 300 units shipped to customer "Customer A" via sales order SO-0001
  When search traceability report for lot "LOT-A-2026-02-01"
  Then report shows:
    - Upstream: Supplier=ACME Corp, Receipt Date=2026-02-01, Qty=1000
    - Current: Location=A1-B1, Available Qty=200
    - Downstream: Production Orders=[PO-0001: 500 units], Sales Orders=[SO-0001: Customer A, 300 units, Tracking=123456789]

Scenario: Search by item SKU
  Given multiple lots for item "RM-0001"
  When search by item SKU "RM-0001"
  Then report shows all lots for RM-0001
  And each lot's upstream/downstream trace

Scenario: Search by sales order
  Given sales order "SO-0001" with lot "LOT-A-2026-02-01"
  When search by sales order "SO-0001"
  Then report shows lot "LOT-A-2026-02-01"
  And upstream/downstream trace

Scenario: Lot not found
  Given lot "LOT-INVALID" does not exist
  When search for "LOT-INVALID"
  Then error message: "Lot not found"
```

### Validation / Checks

**Local Testing:**
```bash
# Get token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Get traceability report
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/traceability?lotNumber=LOT-A-2026-02-01" | jq
# Expected: JSON with upstream, current, downstream sections

# Test UI
open http://localhost:3000/warehouse/reports/traceability
# Search for lot "LOT-A-2026-02-01"
# Expected: report shows supplier, production orders, sales orders
```

**Metrics:**
- `traceability_queries_total` (counter)
- `traceability_query_duration_ms` (histogram)

**Logs:**
- INFO: "Traceability report queried: LotNumber={LotNumber}, Duration={Duration}ms"

### Definition of Done

- [ ] Report page created (`/warehouse/reports/traceability`)
- [ ] Search by: Lot number, Item SKU, Sales Order, Supplier
- [ ] Report sections: Upstream, Current, Downstream
- [ ] API endpoint: GET /api/warehouse/v1/reports/traceability
- [ ] Database queries optimized (indexes on lot_id, item_id)
- [ ] Query performance < 2 seconds
- [ ] Unit tests: query logic
- [ ] Integration tests: full traceability workflow
- [ ] Documentation: `docs/reports.md` updated
- [ ] Code review completed

---

## Task PRD-1552: Compliance Audit Report

**Epic:** Reports | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** S (0.5 day) | **OwnerType:** UI/Backend | **Dependencies:** None | **SourceRefs:** Universe §5.Compliance

### Context
- Compliance audits require comprehensive activity reports
- Need audit trail for SOX, ISO, FDA compliance
- Report must cover stock movements, adjustments, valuations, user actions

### Scope
**In:** Audit report UI, date range filter, PDF/CSV export, multiple report types
**Out:** Real-time compliance monitoring, automated scoring

### Requirements
**Functional:** (1) Report page with date range filter (2) Report types: Stock Movements, Adjustments, Valuations, User Actions (3) Table with pagination (4) PDF/CSV export (5) API endpoint
**Non-Functional:** Query < 5s for 30 days, PDF < 10s, Manager/Admin only

### Acceptance Criteria
```gherkin
Scenario: Generate audit report
  Given date range 2026-02-01 to 2026-02-28
  When select "Stock Movements" and generate
  Then table shows all movements with operator, timestamp, reason

Scenario: Export PDF
  When click Export PDF
  Then formatted PDF downloads with company header

Scenario: Authorization
  Given Operator role
  When access audit report
  Then 403 Forbidden
```

### Validation
```bash
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/compliance-audit?startDate=2026-02-01&endDate=2026-02-28"
```

### Definition of Done
- [ ] Report UI created
- [ ] PDF/CSV export
- [ ] Authorization enforced
- [ ] Tests pass
- [ ] Docs updated

---

## Task PRD-1553: Projection Rebuild Optimization

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** M (1 day) | **OwnerType:** Projections | **Dependencies:** PRD-1509,1513 | **SourceRefs:** Universe §5.Performance

### Context
- Current rebuild takes 10+ minutes for 100K events
- Need parallel processing, checkpointing, incremental rebuild
- Must support production rebuild without downtime

### Scope
**In:** Parallel rebuild (4 concurrent), checkpointing, incremental mode, progress tracking, distributed lock
**Out:** Real-time updates (already exists), projection versioning

### Requirements
**Functional:** (1) Parallel processing (max 4) (2) Checkpoint every 1000 events (3) Incremental mode (4) Progress API (5) Distributed lock
**Non-Functional:** 100K events in < 5 min, constant memory, no downtime

### Acceptance Criteria
```gherkin
Scenario: Parallel rebuild
  Given 100K events, 4 projections
  When rebuild with mode=FULL
  Then completes in < 5 minutes

Scenario: Incremental rebuild
  Given last rebuilt at event 50K, 10K new events
  When rebuild mode=INCREMENTAL
  Then processes only 50K-60K, completes < 30s

Scenario: Checkpointing
  Given rebuild fails at 30K events
  When retry
  Then resumes from 30K (not 0)
```

### Validation
```bash
time curl -H "Authorization: Bearer $TOKEN" -X POST \
  http://localhost:5000/api/admin/projections/rebuild \
  -d '{"mode":"FULL"}'
```

### Definition of Done
- [ ] Parallel rebuild implemented
- [ ] Checkpointing works
- [ ] Incremental mode works
- [ ] < 5 min for 100K events
- [ ] Tests pass

---

## Task PRD-1554: Consistency Checks (Daily Job)

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** M (1 day) | **OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Data Integrity

### Context
- Need automated daily integrity checks
- Verify: no negative balances, no orphaned HUs, projection consistency
- Alert on violations, run at 2 AM daily

### Scope
**In:** Daily job (Hangfire), checks (negative balances, orphaned HUs, projection consistency), alerts, job history
**Out:** Real-time checks, auto-fix

### Requirements
**Functional:** (1) Daily job at 2 AM (2) Check negative balances (3) Check orphaned HUs (4) Check projection vs events (5) Alert on violations (6) Log results
**Non-Functional:** Job < 10 min, alert within 5 min

### Acceptance Criteria
```gherkin
Scenario: Daily consistency check
  Given scheduled job at 2 AM
  When job runs
  Then checks: negative balances, orphaned HUs, projections
  And logs results

Scenario: Violation detected
  Given negative balance found
  When check completes
  Then alert sent to ops team
  And violation logged

Scenario: All checks pass
  When job runs
  Then log: "All consistency checks passed"
```

### Validation
```bash
curl -H "Authorization: Bearer $TOKEN" -X POST \
  http://localhost:5000/api/admin/consistency-checks/run
```

### Definition of Done
- [ ] Daily job configured
- [ ] All checks implemented
- [ ] Alerts working
- [ ] Tests pass

---

## Task PRD-1555: Query Performance Optimization

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** S (0.5 day) | **OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Performance

### Context
- Some queries > 2 seconds
- Need database indexes on frequently queried columns
- Target: all queries < 500ms p95

### Scope
**In:** Index analysis (EXPLAIN), add indexes (item_id, location_id, timestamp, status), query optimization, benchmarks
**Out:** Query caching, database sharding

### Requirements
**Functional:** (1) Analyze slow queries (2) Add missing indexes (3) Rewrite slow queries (4) Benchmark improvements
**Non-Functional:** All queries < 500ms p95

### Acceptance Criteria
```gherkin
Scenario: Index optimization
  Given slow query on items table
  When add index on item_id, status
  Then query < 100ms

Scenario: Query rewrite
  Given complex join query > 2s
  When optimize query
  Then query < 500ms

Scenario: Benchmark
  Given 10K items
  When run query benchmark
  Then p95 < 500ms
```

### Validation
```bash
psql -d warehouse -c "EXPLAIN ANALYZE SELECT * FROM items WHERE status = 'ACTIVE'"
```

### Definition of Done
- [ ] Indexes added
- [ ] Slow queries optimized
- [ ] Benchmarks pass
- [ ] Tests pass

---

## Task PRD-1556: ERP Integration Mock & Tests

**Epic:** Integration | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** M (1 day) | **OwnerType:** Integration | **Dependencies:** PRD-1505,1508 | **SourceRefs:** Universe §5.Integration

### Context
- ERP integration events defined but no tests
- Need mock ERP service for testing
- Contract tests to prevent breaking changes

### Scope
**In:** Mock ERP service (WireMock), contract tests, integration tests, failure scenarios
**Out:** Real ERP integration, ERP business logic

### Requirements
**Functional:** (1) Mock ERP service (2) Contract tests for events (3) Integration tests (4) Failure scenarios (ERP down, timeout)
**Non-Functional:** Tests < 30s, contract validation

### Acceptance Criteria
```gherkin
Scenario: ERP receives SalesOrderCreated
  Given sales order created
  When event published
  Then mock ERP receives event
  And validates contract

Scenario: ERP timeout
  Given mock ERP slow (> 10s)
  When event published
  Then saga retries 3x
  And eventually succeeds

Scenario: Contract validation
  Given event schema changed
  When contract test runs
  Then test fails (breaking change detected)
```

### Validation
```bash
dotnet test --filter "FullyQualifiedName~ERPIntegrationTests"
```

### Definition of Done
- [ ] Mock ERP service
- [ ] Contract tests
- [ ] Integration tests
- [ ] Tests pass

---

## Task PRD-1557: Deployment Guide

**Epic:** Documentation | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** S (0.5 day) | **OwnerType:** Infra/DevOps | **Dependencies:** All | **SourceRefs:** Universe §5.Deployment

### Context
- No deployment documentation
- Need step-by-step production deployment guide
- Cover Docker Compose, Kubernetes, migrations, rollback

### Scope
**In:** Deployment guide (docs/deployment-guide.md), sections (Prerequisites, Docker Compose, Kubernetes, Migrations, Config, Rollback), example commands
**Out:** CI/CD setup, cloud-specific guides

### Requirements
**Functional:** (1) Prerequisites section (2) Docker Compose deployment (3) Kubernetes deployment (4) Database migrations (5) Configuration (6) Rollback procedures (7) Troubleshooting
**Non-Functional:** Clear, step-by-step, tested commands

### Acceptance Criteria
```gherkin
Scenario: Follow deployment guide
  Given fresh server
  When follow guide step-by-step
  Then warehouse system deployed successfully

Scenario: Rollback procedure
  Given deployment failed
  When follow rollback steps
  Then system reverted to previous version
```

### Validation
- Manual: Follow guide on clean VM
- Verify all commands work

### Definition of Done
- [ ] Guide created
- [ ] All sections complete
- [ ] Commands tested
- [ ] Screenshots added

---

## Task PRD-1558: Operator Runbook

**Epic:** Documentation | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** S (0.5 day) | **OwnerType:** QA | **Dependencies:** All | **SourceRefs:** Universe §5.Operations

### Context
- Operators need quick reference for common tasks
- Include screenshots, step-by-step instructions
- Troubleshooting common issues

### Scope
**In:** Operator runbook (docs/operator-runbook.md), sections (Receiving, Picking, Packing, Dispatch, QC, Troubleshooting), screenshots
**Out:** Video tutorials, interactive training

### Requirements
**Functional:** (1) Receiving workflow (2) Picking workflow (3) Packing workflow (4) Dispatch workflow (5) QC workflow (6) Troubleshooting (7) Screenshots for each
**Non-Functional:** Clear, concise, visual

### Acceptance Criteria
```gherkin
Scenario: New operator training
  Given new operator
  When reads runbook
  Then can complete receiving workflow without assistance

Scenario: Troubleshooting
  Given barcode scanner not working
  When consult troubleshooting section
  Then finds solution and resolves issue
```

### Validation
- Manual: New operator follows runbook
- Verify all workflows covered

### Definition of Done
- [ ] Runbook created
- [ ] All workflows documented
- [ ] Screenshots added
- [ ] Tested with operators

---

## Task PRD-1559: API Documentation (Swagger)

**Epic:** Documentation | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** S (0.5 day) | **OwnerType:** Backend/API | **Dependencies:** All | **SourceRefs:** Universe §5.Documentation

### Context
- API endpoints documented in code but no Swagger UI
- Need interactive API documentation at /swagger
- Include request/response examples

### Scope
**In:** Swagger UI config, XML comments on controllers/DTOs, request/response examples, auth documentation
**Out:** Postman collection, API versioning

### Requirements
**Functional:** (1) Swagger UI at /swagger (2) XML comments on all endpoints (3) Request/response examples (4) Auth documentation (dev token)
**Non-Functional:** Complete, accurate, up-to-date

### Acceptance Criteria
```gherkin
Scenario: View API docs
  When navigate to /swagger
  Then Swagger UI loads
  And shows all endpoints

Scenario: Try API endpoint
  Given Swagger UI
  When click "Try it out" on GET /items
  And enter auth token
  Then API call succeeds
  And response shown

Scenario: Request example
  Given Swagger UI
  When view POST /sales-orders
  Then request example shown with all required fields
```

### Validation
```bash
open http://localhost:5000/swagger
# Verify all endpoints documented
```

### Definition of Done
- [ ] Swagger UI configured
- [ ] XML comments added
- [ ] Examples added
- [ ] Auth documented

---

## Task PRD-1560: Production Readiness Checklist

**Epic:** Testing | **Phase:** 1.5 | **Sprint:** 4 | **Estimate:** M (1 day) | **OwnerType:** QA | **Dependencies:** All above | **SourceRefs:** Universe §5.Deployment

### Context
- Need comprehensive checklist before go-live
- Verify: tests pass, docs complete, security hardened
- Must be 100% complete before production

### Scope
**In:** Checklist (docs/production-readiness-checklist.md), categories (Testing, Security, Performance, Observability, Documentation, Data Integrity), sign-off section
**Out:** Automated validation, continuous monitoring

### Requirements
**Functional:** (1) Testing checklist (2) Security checklist (3) Performance checklist (4) Observability checklist (5) Documentation checklist (6) Data integrity checklist (7) Sign-off section
**Non-Functional:** Comprehensive, actionable, verifiable

### Acceptance Criteria
```gherkin
Scenario: Complete checklist
  Given all Sprint 4 tasks complete
  When review checklist
  Then all items checked
  And sign-offs obtained

Scenario: Checklist item verification
  Given checklist item "All tests pass"
  When run test suite
  Then verify tests pass
  And check item

Scenario: Go-live decision
  Given checklist 100% complete
  When stakeholders review
  Then approve production deployment
```

### Validation
- Manual: Review each checklist item
- Verify all items can be validated

### Definition of Done
- [ ] Checklist created
- [ ] All categories covered
- [ ] Validation steps clear
- [ ] Sign-off section added

---

**End of Sprint 4 Tasks**


## Sprint 4 Success Criteria

At the end of Sprint 4, these MUST be true:

### Operator Workflow Validation (CRITICAL)

✅ **1. Operator can create inbound invoice/shipment in UI**
✅ **2. Operator can receive with scan + QC in UI**
✅ **3. Operator can putaway/move stock in UI and see balances update**
✅ **4. Operator can create Sales Order in UI, allocate, pick, pack, dispatch**
✅ **5. Operator can view shipment status + dispatch history in UI**
✅ **6. RBAC/auth flow documented and local validation steps work (no 403 surprises)**
✅ **7. Idempotency and tracing are in place and validated**

### Testing & Quality

✅ **8. Integration tests validate critical workflows**
✅ **9. Observability dashboards operational**

### Reporting & Documentation### Reporting & Documentation### Reporting & Documentation### Reporting & Documentation#nc### Reporting & Documentation### Reportebuild works corr### Reporting & Documentation### Reporting & Documentation### Repprint 4 Task Pack**

