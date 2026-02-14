# Phase 1.5 Coverage Matrix

**Date:** February 14, 2026  
**Purpose:** Audit of all epics/features from prod-ready-universe.md against Sprint 1-9 task coverage  
**Status:** COMPLETE

---

## Executive Summary

**Total Epics in Universe:** 17 epics (A through Q)  
**Epics Fully Covered:** 17 (100%)  
**Epics Partially Covered:** 0  
**Epics Not Covered:** 0  
**Remaining Task Count Estimate:** 0 tasks

**Conclusion:** ALL universe epics are covered by Sprints 1-9 (PRD-1501 to PRD-1660). No additional sprints required.

---

## Epic Coverage Analysis

### Epic A: Outbound / Shipment / Dispatch
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 1, 3  
**TaskIds:** PRD-1506 (OutboundOrder + Shipment entities), PRD-1507 (Packing MVP), PRD-1508 (Dispatch MVP), PRD-1509 (Projections), PRD-1510 (UI), PRD-1531 (Packing UI enhancements), PRD-1532 (Dispatch UI enhancements), PRD-1538 (FedEx API integration)  
**Features Implemented:**
- OutboundOrder and Shipment entities with state machines
- Packing workflow (scan items, verify, pack into shipping HU)
- Dispatch confirmation (carrier, vehicle, tracking)
- Proof of delivery capture
- Carrier API integration (FedEx)
- UI: Outbound orders list, packing station, dispatch confirmation

---

### Epic B: Sales Orders / Customer Orders
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 1, 3  
**TaskIds:** PRD-1504 (Customer + SalesOrder entities), PRD-1505 (CRUD APIs + status transitions), PRD-1527 (Create Sales Order UI), PRD-1528 (Sales Order list + detail UI), PRD-1529 (Allocation + release UI), PRD-1535 (Stock allocation validation), PRD-1536 (Optimistic locking)  
**Features Implemented:**
- Customer entity (master data)
- SalesOrder entity with full lifecycle (DRAFT → INVOICED)
- Order allocation (auto-allocate stock, SOFT → HARD lock)
- Approval workflow (credit limit checks)
- UI: Sales orders list, create order, order details, allocation dashboard

---

### Epic C: Valuation / Revaluation
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 2, 7  
**TaskIds:** PRD-1511 (ItemValuation aggregate), PRD-1512 (Cost adjustment command), PRD-1513 (OnHandValue projection), PRD-1601 (Valuation stream + events), PRD-1602 (Cost adjustment command), PRD-1603 (Landed cost allocation), PRD-1604 (Write-down command), PRD-1605 (Valuation UI + reports)  
**Features Implemented:**
- ItemValuation aggregate (event-sourced)
- Cost adjustment with approval workflow
- Landed cost allocation (proportional distribution)
- Write-down command (damage, obsolescence)
- OnHandValue projection (qty × cost)
- UI: Valuation dashboard, cost adjustment/landed cost/write-down forms, on-hand value report, cost history report

---

### Epic D: Agnum Accounting Integration
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 2, 7  
**TaskIds:** PRD-1514 (Export config + scheduled job), PRD-1515 (CSV generation + API integration), PRD-1606 (Agnum configuration UI), PRD-1607 (Agnum export job), PRD-1608 (Agnum reconciliation report)  
**Features Implemented:**
- Scheduled daily export (23:00, configurable)
- Mapping configuration (warehouse/category → GL account codes)
- CSV generation (SKU, qty, cost, value, account code)
- API integration (POST to Agnum REST endpoint)
- Export history log
- Retry logic (3x with exponential backoff)
- Reconciliation report (warehouse vs Agnum balance)
- UI: Configuration page, export history, reconciliation report

---

### Epic E: 3D/2D Warehouse Visualization
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 2, 7  
**TaskIds:** PRD-1517 (Location 3D coordinates), PRD-1518 (3D UI implementation), PRD-1609 (Location 3D coordinates), PRD-1610 (3D warehouse rendering), PRD-1611 (2D/3D toggle + interaction)  
**Features Implemented:**
- Location coordinates (X, Y, Z) in Location entity
- WarehouseLayout configuration (aisles, racks, bins)
- Static 3D model (Three.js)
- Interactive: click bin → show HU details
- Color coding (empty, low, full, reserved)
- 2D floor plan (top-down view, toggle with 3D)
- Search location by code → highlight in 3D
- UI: 3D warehouse view, 2D floor plan, configure layout

---

### Epic F: Inter-Warehouse Transfers (Logical Warehouse Reclassification)
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 2, 7  
**TaskIds:** PRD-1519 (Transfer request workflow), PRD-1619 (Inter-warehouse transfer workflow), PRD-1620 (Inter-warehouse transfer UI)  
**Features Implemented:**
- Logical warehouse transfer (virtual location change)
- Transfer request workflow (request → approve → execute)
- In-transit virtual location (IN_TRANSIT_{transferId})
- Approval rules (Manager approval for SCRAP transfers)
- UI: Transfer request form, transfer list, approval workflow

---

### Epic G: Label Printing (ZPL Integration)
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 2, 7  
**TaskIds:** PRD-1516 (ZPL template engine + TCP 9100 integration), PRD-1616 (ZPL template engine), PRD-1617 (TCP 9100 printer integration), PRD-1618 (Print queue + retry)  
**Features Implemented:**
- ZPL template engine (location labels, HU labels, item labels)
- TCP 9100 printer integration (Zebra printers)
- Print queue (retry 3x if printer offline)
- Manual fallback (download PDF if print fails)
- UI: Print preview, print queue status

---

### Epic H: Wave Picking (Batch Picking)
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 6  
**TaskIds:** PRD-1581 (Wave creation + batch assignment), PRD-1582 (Route optimization algorithm), PRD-1583 (Wave picking UI + execution)  
**Features Implemented:**
- Wave creation (group orders by zone, priority)
- Operator assignment
- Batch pick list (all items sorted by location)
- Route optimization (shortest path algorithm)
- Split picked items into orders (post-pick sorting)
- UI: Wave creation, wave picking execution, operator assignment

---

### Epic I: Cross-Docking
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 6  
**TaskIds:** PRD-1584 (Cross-dock workflow + routing), PRD-1585 (Cross-dock UI + tracking)  
**Features Implemented:**
- Cross-dock flag on InboundShipment
- Auto-route: RECEIVING → SHIPPING (skip storage)
- Match inbound → outbound orders (by SKU)
- UI: Cross-dock dashboard, inbound/outbound matching

---

### Epic J: Multi-Level QC Approvals
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 6  
**TaskIds:** PRD-1586 (QC checklist templates), PRD-1587 (QC defect taxonomy + tracking), PRD-1588 (QC attachments + photo upload)  
**Features Implemented:**
- QC checklist templates (configurable per item category)
- Multi-level approval workflow (Inspector → Manager → Quality Head)
- Defect categorization (taxonomy)
- Photo/document attachments (blob storage)
- UI: QC checklist execution, defect tracking, approval workflow

---

### Epic K: Handling Unit Hierarchy (Nested HUs)
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 6  
**TaskIds:** PRD-1592 (HU split/merge operations), PRD-1593 (Nested HU validation + tracking)  
**Features Implemented:**
- HandlingUnit.ParentHUId (nullable for hierarchy)
- Split operation (create child HUs from parent)
- Merge operation (consolidate child HUs into parent)
- Validation (prevent circular references, max depth)
- UI: HU hierarchy tree view, split/merge operations

---

### Epic L: Serial Number Tracking
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 6  
**TaskIds:** PRD-1594 (Serial number lifecycle management), PRD-1595 (Serial status tracking + reporting)  
**Features Implemented:**
- SerialNumber entity (activated in Phase 1, unused)
- Serial → Lot mapping
- Serial lifecycle (received → issued → returned → scrapped)
- Status tracking (AVAILABLE, RESERVED, ISSUED, RETURNED, SCRAPPED)
- UI: Serial number list, lifecycle tracking, status reports

---

### Epic M: Cycle Counting (Scheduled Physical Inventory)
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 2, 7  
**TaskIds:** PRD-1520 (Scheduled counts + discrepancy resolution), PRD-1612 (Cycle count scheduling), PRD-1613 (Cycle count execution), PRD-1614 (Discrepancy resolution), PRD-1615 (Cycle count UI)  
**Features Implemented:**
- Cycle count scheduling (ABC classification: A-monthly, B-quarterly, C-annual)
- Count execution (scan location, count items, compare to system)
- Discrepancy report (variance > 5% flagged)
- Auto-adjustment workflow (approve discrepancies)
- UI: Cycle count scheduling, execution, discrepancy resolution

---

### Epic N: Returns / RMA
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 6  
**TaskIds:** PRD-1589 (RMA creation + workflow), PRD-1590 (RMA inspection + disposition), PRD-1591 (RMA UI + customer portal)  
**Features Implemented:**
- RMA entity (link to SalesOrder)
- Return receiving (scan return, match to RMA)
- Inspection workflow (pass → restock, fail → scrap)
- Disposition tracking (restock, scrap, return to supplier)
- UI: RMA creation, inspection, disposition, customer portal (basic)

---

### Epic O: Advanced Reporting & Audit
**Status:** ✅ Fully Covered  
**Coverage:** Sprints 4, 8  
**TaskIds:** PRD-1549 (Stock movement history report), PRD-1550 (Transaction log export), PRD-1551 (Traceability report), PRD-1552 (Compliance audit report), PRD-1631 (Transaction log export), PRD-1632 (Lot traceability report), PRD-1633 (Variance analysis report), PRD-1634 (Compliance reports dashboard), PRD-1635 (FDA 21 CFR Part 11 compliance)  
**Features Implemented:**
- Full transaction log export (all StockMoved events)
- Lot traceability report (upstream: supplier → lot, downstream: lot → customer)
- Variance analysis (adjustment trends by location/operator)
- Compliance reports (date range, filters, PDF export)
- FDA 21 CFR Part 11 compliance (electronic signatures, audit trail)
- UI: Reports dashboard, transaction log export, traceability report, variance analysis

---

### Epic P: Admin & Configuration
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 8  
**TaskIds:** PRD-1621 (Warehouse settings entity), PRD-1622 (Reason code management), PRD-1623 (Approval rules engine), PRD-1624 (User role management), PRD-1625 (Admin configuration UI)  
**Features Implemented:**
- Warehouse-level settings (capacity thresholds, FEFO vs FIFO default)
- Reason code management (add/edit adjustment reasons, hierarchical taxonomy)
- Approval rules config (who approves what, threshold-based)
- User role management (assign permissions, custom roles)
- UI: Settings page, reason code CRUD, approval rules, role management

---

### Epic Q: Security Hardening (SSO, OAuth, MFA, API Keys)
**Status:** ✅ Fully Covered  
**Coverage:** Sprint 8  
**TaskIds:** PRD-1626 (SSO/OAuth integration), PRD-1627 (MFA implementation), PRD-1628 (API key management), PRD-1629 (RBAC granular permissions), PRD-1630 (Security audit log)  
**Features Implemented:**
- SSO integration (Azure AD, Okta via OAuth 2.0)
- MFA (TOTP, SMS)
- API key management (create, rotate, revoke)
- Role-based access control (RBAC) granular permissions
- Audit log (all user actions logged)
- UI: SSO configuration, MFA setup, API key management, audit log viewer

---

## Cross-Cutting Concerns Coverage

### Foundation & Infrastructure (Covered in Sprints 1, 4, 5, 9)
**TaskIds:** PRD-1501 (Idempotency completion), PRD-1502 (Event schema versioning), PRD-1503 (Correlation/trace propagation), PRD-1544 (Health check dashboard), PRD-1545 (Metrics dashboards), PRD-1546 (Alerting rules + runbook), PRD-1553 (Projection rebuild optimization), PRD-1554 (Consistency checks), PRD-1555 (Query performance optimization), PRD-1561-1564 (Reliability hardening), PRD-1565-1568 (Performance baseline), PRD-1569-1571 (Observability maturity), PRD-1641-1645 (Performance optimization), PRD-1646-1650 (Monitoring + alerting)  
**Features Implemented:**
- Idempotency (command deduplication, processed_commands table)
- Event schema versioning (upcasting support)
- Correlation/trace propagation (OpenTelemetry)
- Health checks (DB, event store, message queue)
- Metrics dashboards (Grafana)
- Alerting rules (projection lag, saga failures, negative stock)
- Projection rebuild optimization (distributed lock, parallel processing)
- Consistency checks (daily job)
- Query performance optimization (indexes, N+1 elimination)
- Reliability hardening (saga checkpointing, concurrency tests)
- Performance baseline (database indexes, query plans, API SLAs)
- Observability maturity (structured logging, business metrics, alert tuning)
- Monitoring + alerting (APM integration, custom dashboards, SLA monitoring)

### Integration & Resilience (Covered in Sprints 3, 5, 9)
**TaskIds:** PRD-1538 (FedEx API integration), PRD-1539 (E2E correlation tracing), PRD-1540 (Smoke E2E integration tests), PRD-1556 (ERP integration mock + tests), PRD-1572 (Agnum export retry hardening), PRD-1573 (Label printer queue resilience), PRD-1574 (ERP event contract tests), PRD-1651-1655 (Integration testing), PRD-1656-1660 (Production deployment)  
**Features Implemented:**
- FedEx API integration (real carrier API calls)
- E2E correlation tracing (distributed tracing)
- Smoke E2E integration tests (full workflow validation)
- ERP integration mock + tests (contract tests)
- Agnum export retry hardening (exponential backoff, 3x retries)
- Label printer queue resilience (retry logic, fallback to PDF)
- ERP event contract tests (schema validation)
- E2E test suite expansion (chaos engineering, failover testing)
- Production deployment (blue-green, canary releases, feature flags)

### UI Completeness (Covered in Sprint 3)
**TaskIds:** PRD-1521 (Local dev auth + 403 fix), PRD-1522 (Data model type alignment), PRD-1523 (Receiving invoice entry UI), PRD-1524 (Receiving scan + QC workflow UI), PRD-1525 (Stock visibility dashboard UI), PRD-1526 (Stock movement/transfer UI), PRD-1527-1532 (Sales order + outbound UI), PRD-1533-1534 (Reports UI), PRD-1575-1577 (UI quality: empty states, bulk operations, advanced search)  
**Features Implemented:**
- Local dev auth (JWT token generation for testing)
- Data model type alignment (Guid → int consistency)
- Receiving invoice entry UI (Blazor)
- Receiving scan + QC workflow UI (barcode scanning)
- Stock visibility dashboard UI (real-time balances)
- Stock movement/transfer UI (location-to-location transfers)
- Sales order + outbound UI (create, list, detail, packing, dispatch)
- Reports UI (receiving history, dispatch history)
- UI quality (empty states, error handling, bulk operations, advanced search)

### Data Retention & GDPR (Covered in Sprint 8)
**TaskIds:** PRD-1636 (Retention policy engine), PRD-1637 (PII encryption), PRD-1638 (GDPR erasure workflow), PRD-1639 (Backup/restore procedures), PRD-1640 (Disaster recovery plan)  
**Features Implemented:**
- Retention policy engine (configurable retention periods per entity type)
- PII encryption (customer addresses, emails encrypted at rest)
- GDPR erasure workflow (soft-delete customers, anonymize PII in events)
- Backup/restore procedures (automated daily backups, 90-day retention)
- Disaster recovery plan (RTO < 4 hours, RPO < 1 hour)

---

## Remaining Backlog Analysis

### Epics Not Covered: NONE

All 17 epics from the universe are fully covered by Sprints 1-9.

### Optional Nice-to-Have Features (Not in Universe, Not Planned)
The following features were mentioned in the universe as "Out of Scope" or "Phase 2" and are NOT included in Phase 1.5:

1. **Multi-currency support** (Epic B, D - deferred to Phase 2)
2. **Customer portal** (Epic B - self-service order entry, tracking - deferred to Phase 2)
3. **Order pricing engine** (Epic B - discounts, promotions, tiered pricing - deferred to Phase 2)
4. **Multi-parcel shipments** (Epic A - 1 order = multiple boxes - deferred to Phase 2)
5. **International shipping** (Epic A - customs forms - deferred to Phase 2)
6. **Real-time 3D updates via SignalR** (Epic E - Phase 1.5 = manual refresh)
7. **Operator location tracking (RTLS)** (Epic E - deferred to Phase 2)
8. **Heatmap** (Epic E - pick frequency, travel time - deferred to Phase 2)
9. **Path optimization** (Epic E - suggest shortest route - deferred to Phase 2)
10. **Auto-scaling (Kubernetes HPA)** (Epic G - deferred to Phase 2)
11. **Global load balancing (multi-region)** (Epic G - deferred to Phase 2)
12. **FIFO/LIFO costing** (Epic C - weighted average only for Phase 1.5)
13. **Cost layers (per-receipt costing)** (Epic C - single unit cost per item for Phase 1.5)
14. **Automated duty calculation** (Epic C - manual entry for Phase 1.5)
15. **Write-through caching** (Epic Performance - cache-aside only)
16. **Distributed cache coordination** (Epic Performance - single Redis instance)
17. **Cache warming strategies** (Epic Performance - deferred to Phase 2)
18. **Database sharding** (Epic Performance - single-instance optimization only)
19. **Read replicas** (Epic Performance - deferred to Phase 2)
20. **External connection poolers (PgBouncer)** (Epic Performance - deferred to Phase 2)

These are explicitly out of scope for Phase 1.5 and do not require additional sprints.

---

## Conclusion

**ALL 17 EPICS FROM THE UNIVERSE ARE FULLY COVERED BY SPRINTS 1-9 (PRD-1501 TO PRD-1660).**

**No additional sprints (S10-S14) are required.**

The Phase 1.5 specification is complete and ready for implementation. All must-have features for production-ready B2B/B2C warehouse operations are included in the 160 tasks across 9 sprints.

**Estimated Total Effort:** 157 days (~31 weeks with 2 developers)

**Recommended Implementation Order:** Sprint 1 → Sprint 2 → Sprint 3 → Sprint 4 → Sprint 5 → Sprint 6 → Sprint 7 → Sprint 8 → Sprint 9

---

## Verification Checklist

- [x] All 17 epics from universe analyzed
- [x] Each epic mapped to specific TaskIds
- [x] Coverage status determined (Fully Covered / Partially Covered / Not Covered)
- [x] Remaining backlog calculated (0 tasks)
- [x] Optional nice-to-have features identified (20 items, all deferred to Phase 2)
- [x] Conclusion: No additional sprints required

