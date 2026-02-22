# Phase 1.5 Sprint 6 - Task Summary

**Sprint Goal:** Advanced warehouse capabilities - wave picking, cross-docking, RMA, QC enhancements, HU hierarchy, serial tracking, analytics

**Total Tasks:** 20 (PRD-1581 to PRD-1600)
**Estimated Effort:** 18 days
**Sprint Duration:** 2 weeks

---

## Task List

### Wave Picking (3 tasks, 4 days)

**PRD-1581 - Wave Creation & Batch Assignment (L, 2 days)**
- Add Wave entity (group multiple orders for batch picking)
- Add wave creation logic (group by zone, priority, delivery date)
- Add operator assignment (assign wave to picker)
- Add wave status lifecycle (CREATED → ASSIGNED → PICKING → COMPLETED)

**PRD-1582 - Route Optimization Algorithm (M, 1 day)**
- Implement route optimization (minimize travel distance)
- Sort pick list by location (aisle → rack → level → bin)
- Add zone-based routing (pick all items in zone before moving)
- Benchmark: 30% reduction in pick time vs single-order picking

**PRD-1583 - Wave Picking UI & Execution (M, 1 day)**
- Add wave list page (show active waves, assigned operator)
- Add wave pick list (all items sorted by location)
- Add pick execution (scan item, confirm qty, mark complete)
- Add wave completion (all items picked, split into orders)

### Cross-Docking (2 tasks, 1.5 days)

**PRD-1584 - Cross-Dock Workflow & Routing (M, 1 day)**
- Add cross-dock flag to InboundShipment
- Add auto-routing: RECEIVING → SHIPPING (skip storage)
- Add cross-dock matching (match inbound SKU to outbound order)
- Add cross-dock report (items cross-docked, time saved)

**PRD-1585 - Cross-Dock UI & Tracking (S, 0.5 day)**
- Add cross-dock indicator on receiving UI
- Add cross-dock dashboard (pending matches, completed)
- Add cross-dock history report
- Add cross-dock metrics (% of shipments cross-docked)

### Multi-Level QC Approvals (3 tasks, 2.5 days)

**PRD-1586 - QC Checklist Templates (M, 1 day)**
- Add QCChecklistTemplate entity (define inspection steps)
- Add checklist items (step name, pass/fail, notes)
- Add template assignment (per item category or supplier)
- Add checklist execution (inspector completes each step)

**PRD-1587 - QC Defect Taxonomy & Tracking (M, 1 day)**
- Add QCDefect entity (defect type, severity, location)
- Add defect taxonomy (DAMAGED, EXPIRED, MISLABELED, CONTAMINATED)
- Add defect tracking (defect count per supplier, item, lot)
- Add defect report (top defects, supplier scorecard)

**PRD-1588 - QC Attachments & Photo Upload (S, 0.5 day)**
- Add photo upload to QC inspection (capture defect evidence)
- Store photos in blob storage (Azure Blob or S3)
- Add photo viewer in QC history
- Add photo export (include in defect report)

### Returns / RMA (3 tasks, 4 days)

**PRD-1589 - RMA Creation & Workflow (L, 2 days)**
- Add RMA entity (link to SalesOrder, return reason)
- Add RMA creation API (customer service creates RMA)
- Add RMA status lifecycle (PENDING_RECEIPT → RECEIVED → INSPECTED → RESTOCKED/SCRAPPED)
- Add RMA notification (warehouse notified of expected return)

**PRD-1590 - RMA Inspection & Disposition (M, 1 day)**
- Add RMA receiving (scan return, match to RMA)
- Add RMA inspection (RESTOCK, SCRAP, RETURN_TO_SUPPLIER)
- Add disposition logic (restock → putaway, scrap → write-down)
- Add RMA credit calculation (refund amount, restocking fee)

**PRD-1591 - RMA UI & Customer Portal (M, 1 day)**
- Add RMA list page (show pending, received, completed)
- Add RMA detail page (return items, inspection result, credit)
- Add RMA creation form (customer service)
- Add customer portal (customer views RMA status)

### Handling Unit Hierarchy (2 tasks, 2 days)

**PRD-1592 - HU Split/Merge Operations (M, 1 day)**
- Add HU split operation (create child HUs from parent)
- Add HU merge operation (consolidate child HUs into parent)
- Add HU hierarchy validation (prevent circular references)
- Add HU hierarchy report (parent → children tree view)

**PRD-1593 - Nested HU Validation & Tracking (M, 1 day)**
- Add ParentHUId to HandlingUnit entity
- Add HU hierarchy queries (get all children, get root parent)
- Add HU move validation (move parent → move all children)
- Add HU capacity validation (parent capacity >= sum of children)

### Serial Number Tracking (2 tasks, 3 days)

**PRD-1594 - Serial Number Lifecycle Management (L, 2 days)**
- Add SerialNumber entity (serial, item, status, location)
- Add serial lifecycle (RECEIVED → AVAILABLE → ISSUED → RETURNED → SCRAPPED)
- Add serial assignment (assign serial to HU on receiving)
- Add serial tracking (track serial through pick/pack/dispatch)

**PRD-1595 - Serial Status Tracking & Reporting (M, 1 day)**
- Add serial status transitions (available → issued, issued → returned)
- Add serial history report (serial → all movements)
- Add serial search (find serial by number, item, status)
- Add warranty tracking (serial → warranty expiry date)

### Analytics & Reporting (2 tasks, 1.5 days)

**PRD-1596 - Fulfillment KPIs Dashboard (M, 1 day)**
- Add KPI dashboard: Orders fulfilled, On-time delivery %, Pick time avg
- Add trend charts (daily/weekly/monthly)
- Add drill-down (click KPI → see details)
- Add export (CSV, PDF)

**PRD-1597 - QC Defects & Late Shipments Analytics (S, 0.5 day)**
- Add QC defects report (defect count by supplier, item, type)
- Add late shipments report (orders past requested delivery date)
- Add root cause analysis (why late: stock shortage, pick delay, carrier delay)
- Add supplier scorecard (defect rate, on-time delivery)

### Testing & Documentation (3 tasks, 3 days)

**PRD-1598 - Contract Tests for External APIs (M, 1 day)**
- Add contract tests for carrier API (FedEx, UPS)
- Add contract tests for Agnum API
- Add contract tests for ERP events
- Verify schema compatibility (v1 → v2 upgrades)

**PRD-1599 - Performance Regression Test Suite (M, 1 day)**
- Add performance regression tests (baseline from Sprint 5)
- Test scenarios: Create 100 orders, Pick 1000 items, Pack 50 shipments
- Measure: Response time, Throughput, Error rate
- Alert on regression (> 10% slower than baseline)

**PRD-1600 - Operator Training Videos & Guides (L, 2 days)**
- Create training videos (5-10 min each): Receiving, Picking, Packing, Dispatch
- Create operator guides (PDF): Step-by-step workflows with screenshots
- Create troubleshooting guide (common errors, solutions)
- Create admin guide (configuration, user management, reports)

---

## Critical Path

1. **Week 1 (Days 1-5):**
   - PRD-1581 (Wave creation) - Days 1-2
   - PRD-1582 (Route optimization) - Day 3
   - PRD-1583 (Wave UI) - Day 4
   - PRD-1589 (RMA workflow) - Day 5

2. **Week 2 (Days 6-10):**
   - PRD-1589 (RMA workflow cont.) - Day 6
   - PRD-1590 (RMA inspection) - Day 7
   - PRD-1594 (Serial lifecycle) - Days 8-9
   - PRD-1600 (Training materials) - Day 10

3. **Parallel (can overlap):**
   - PRD-1584, 1585 (Cross-docking)
   - PRD-1586, 1587, 1588 (QC enhancements)
   - PRD-1591 (RMA UI)
   - PRD-1592, 1593 (HU hierarchy)
   - PRD-1595 (Serial reporting)
   - PRD-1596, 1597 (Analytics)
   - PRD-1598, 1599 (Testing)

---

## Known Risks

1. **Wave picking complexity:** Route optimization algorithm may be complex (consider using library like OR-Tools)
2. **Serial tracking scope:** May be too large for 2 days (consider deferring warranty tracking to Phase 2)
3. **Training materials:** Video creation time-consuming (consider screen recordings vs professional videos)

---

## Success Criteria

After Sprint 6:
- ✅ Wave picking operational (batch 10+ orders, 30% faster than single-order)
- ✅ Cross-docking workflow complete (receive → ship without storage)
- ✅ Multi-level QC with checklists, defect tracking, photo attachments
- ✅ RMA workflow complete (create → receive → inspect → restock/scrap)
- ✅ HU hierarchy operational (split/merge, nested tracking)
- ✅ Serial number tracking operational (lifecycle, warranty)
- ✅ Analytics dashboards operational (fulfillment KPIs, defects, late shipments)
- ✅ Contract tests for external APIs (carrier, Agnum, ERP)
- ✅ Performance regression tests operational (baseline from Sprint 5)
- ✅ Operator training materials complete (videos, guides)

---

## Dependencies

- Sprint 5 complete (PRD-1561 to PRD-1580)
- Production hardening complete (reliability, performance, observability)
- Load testing baseline established

---

## Handoff to Production

Sprint 6 completes Phase 1.5. System is production-ready with:
- ✅ Core workflows (inbound, outbound, stock management)
- ✅ Advanced features (wave picking, cross-docking, RMA, QC, HU, serial)
- ✅ Production hardening (reliability, performance, observability)
- ✅ Integration resilience (Agnum, label printer, ERP, carrier)
- ✅ Operator training materials

Next: Deploy to production, monitor, iterate based on operator feedback.
