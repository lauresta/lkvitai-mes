# Production-Ready Warehouse Tasks - Progress Ledger

**Last Updated:** February 12, 2026  
**Session:** Phase 1.5 Sprint 3-6 Final Completion  
**Status:** ALL SPRINTS (S3-S6) SPEC COMPLETE - Ready for Codex Execution  
**BATON:** 2026-02-12T05:38:09Z-PHASE15-S3456-FINAL-23613d43

---

## ✅ FINAL STATUS: ALL SPRINTS COMPLETE

### Sprint 3-6 Completion Summary

**Sprint 3 (PRD-1521 to PRD-1540):** ✅ COMPLETE - 20 tasks
- UI completeness (Receiving, QC, Stock Dashboard, Sales Orders, Picking, Packing, Dispatch)
- Auth & validation fixes (Local dev auth, RBAC, data model alignment)
- Reports (Receiving history, Dispatch history)
- Integration (FedEx API, E2E correlation tracing, smoke tests)

**Sprint 4 (PRD-1541 to PRD-1560):** ✅ COMPLETE - 20 tasks
- E2E workflow tests (Inbound, Outbound, Stock Movement)
- Observability (Health checks, Grafana dashboards, Alerting)
- RBAC enforcement & Admin UI
- Reports (Movement history, Transaction log, Traceability, Compliance)
- Performance (Projection rebuild, Consistency checks, Query optimization)
- Production readiness (Deployment guide, Runbook, API docs, Checklist)

**Sprint 5 (PRD-1561 to PRD-1580):** ✅ COMPLETE - 20 tasks
- Reliability hardening (Idempotency audit, Projection replay, Saga checkpointing, Concurrency tests)
- Performance baseline (Database indexes, Query plans, Projection benchmarks, API SLAs)
- Observability maturity (Structured logging, Business metrics, Alert tuning)
- Integration resilience (Agnum retry, Label printer queue, ERP contract tests)
- UI quality (Empty states, Bulk operations, Advanced search)
- Security (Rate limiting, Data masking)
- Load & stress testing

**Sprint 6 (PRD-1581 to PRD-1600):** ✅ COMPLETE - 20 tasks
- Wave picking (Creation, Route optimization, UI)
- Cross-docking (Workflow, UI)
- Multi-level QC (Checklists, Defect tracking, Photo attachments)
- RMA (Creation, Inspection, UI)
- HU hierarchy (Split/merge, Nested validation)
- Serial tracking (Lifecycle, Status tracking)
- Analytics (Fulfillment KPIs, QC defects)
- Testing & documentation (Contract tests, Performance regression, Training materials)

**Total:** 80 tasks (PRD-1521 to PRD-1600) fully specified

### File Cleanup
- ✅ Removed obsolete part files: S4.part1.md, S4.part2.md, S4-REMAINING.md
- ✅ All sprints consolidated into single source-of-truth files
- ✅ No placeholders remaining in any sprint file

### Handoff Command for Next Session

```bash
# Verify all sprints complete
for sprint in 3 4 5 6; do
  echo "Sprint $sprint: $(grep -c '^## Task PRD-' docs/prod-ready/prod-ready-tasks-PHASE15-S${sprint}.md) tasks"
done

# Check for placeholders (should return nothing)
grep -ri "continue with remaining\|due to length\|due to response" docs/prod-ready/prod-ready-tasks-PHASE15-S{3,4,5,6}.md

# Start implementation from Sprint 3 (UI completeness)
# OR Sprint 5 (Reliability hardening) depending on priority
```

### Next Steps
1. **Implementation Priority:** Sprint 3 → Sprint 4 → Sprint 5 → Sprint 6
2. **Codex Execution:** Use task files directly, no need to reference universe.md
3. **Progress Tracking:** Update tasks.md files as tasks complete
4. **Issue Logging:** Document issues in `docs/prod-ready/codex-suspicions.md`
5. **Run Summary:** End each Codex run with `docs/prod-ready/codex-run-summary.md`

---

## Completed in This Session

### Sprint 4, 5, 6 Tasks (Fully Elaborated - Ready for Execution)

**Sprint 4 (PRD-1541 to PRD-1560):** 20 tasks - Observability, monitoring, health checks, dashboards, alerting, runbooks
**Sprint 5 (PRD-1561 to PRD-1580):** 20 tasks - Reliability hardening, performance optimization, integration resilience, UI quality, security
**Sprint 6 (PRD-1581 to PRD-1600):** 20 tasks - Wave picking, cross-docking, multi-level QC, RMA, HU hierarchy, serial tracking, analytics, testing, training

**Total:** 60 tasks fully specified with:
- Complete context and scope
- Detailed requirements (functional, non-functional, data model, API)
- Acceptance criteria (Gherkin scenarios)
- Validation commands
- Definition of Done checklists

**Status:** ALL PLACEHOLDERS REMOVED - All tasks fully specified, no "Continue with remaining..." lines

---

## Previously Completed Sessions

### Sprint 1 & 2 Tasks (Fully Elaborated - Ready for Execution)

**Last Updated:** February 10, 2026  
**Session:** Phase 1.5 Sprint 2 Elaboration  
**Status:** Sprint 1 & Sprint 2 tasks fully elaborated and ready for execution

### Sprint 1 Tasks (Fully Elaborated - Ready for Execution)

1. **PRD-1501** - Foundation: Idempotency Completion (M, 1 day)
2. **PRD-1502** - Foundation: Event Schema Versioning (M, 1 day)
3. **PRD-1503** - Foundation: Correlation/Trace Propagation (S, 0.5 day)
4. **PRD-1504** - Sales Orders: Customer Entity + SalesOrder Aggregate + State Machine (L, 2 days)
5. **PRD-1505** - Sales Orders: APIs for CRUD/Import and Status Transitions (L, 2 days)
6. **PRD-1506** - Outbound/Shipment: OutboundOrder + Shipment Entities + State Machine (L, 2 days)
7. **PRD-1507** - Packing MVP: Consolidate from SHIPPING to Shipment/HUs + ShipmentPacked Event (M, 1 day)
8. **PRD-1508** - Dispatch MVP: Mark Shipment as Dispatched + ShipmentDispatched Event + Audit (M, 1 day)
9. **PRD-1509** - Projections: OutboundOrderSummary + ShipmentSummary + DispatchHistory (M, 1 day)
10. **PRD-1510** - UI: Outbound Orders List + Order Detail + Packing Station + Dispatch Confirmation (L, 2 days)

**Sprint 1 Total Effort:** 13.5 days

### Sprint 2 Tasks (Fully Elaborated - Ready for Execution)

11. **PRD-1511** - Valuation: ItemValuation Aggregate + Events (M, 1 day)
12. **PRD-1512** - Valuation: Cost Adjustment Command + Handler (M, 1 day)
13. **PRD-1513** - Valuation: OnHandValue Projection (M, 1 day)
14. **PRD-1514** - Agnum Integration: Export Configuration + Scheduled Job (L, 2 days)
15. **PRD-1515** - Agnum Integration: CSV Generation + API Integration (M, 1 day)
16. **PRD-1516** - Label Printing: ZPL Template Engine + TCP 9100 Integration (M, 1 day)
17. **PRD-1517** - 3D Visualization: Location Coordinates + Static 3D Model (L, 2 days)
18. **PRD-1518** - 3D Visualization: UI Implementation (L, 2 days)
19. **PRD-1519** - Inter-Warehouse Transfers: Transfer Request Workflow (M, 1 day)
20. **PRD-1520** - Cycle Counting: Scheduled Counts + Discrepancy Resolution (M, 2 days)

**Sprint 2 Total Effort:** 12 days

**Combined Total:** 25.5 days (20 tasks)

---

## Partially Elaborated Tasks

None - all 20 tasks (Sprint 1 + Sprint 2) fully elaborated with:
- Complete context and scope
- Detailed requirements (functional, non-functional, data model, API)
- 5-7 Gherkin acceptance criteria scenarios (including negative cases)
- Implementation notes and validation steps
- Definition of Done checklist (15-20 items per task)
- Source references to prod-ready-universe.md

**Quality Gate Review Status:** ✅ RESOLVED
- Initial state: 5 of 20 tasks fully elaborated (25%)
- Final state: 20 of 20 tasks fully elaborated (100%)
- All critical blockers addressed (API contracts, event payloads, state machines, validation rules, migrations)

---

## Blockers / Open Questions

### 1. Carrier API Integration (PRD-1507, PRD-1508)
**Question:** Which carrier API should we integrate first (FedEx, UPS, DHL)?  
**Impact:** Affects label generation and tracking number format  
**Recommendation:** Start with FedEx API (most common in B2B), add others in Phase 2  
**Source:** Universe §4.Epic A (Carrier API Integration)  
**Status:** RESOLVED - FedEx API selected

### 2. Credit Limit Approval Workflow (PRD-1505)
**Question:** Should orders exceeding credit limit require manual approval or auto-reject?  
**Impact:** Affects SalesOrder state machine (PENDING_APPROVAL status)  
**Recommendation:** Manual approval by Manager role (configurable threshold in Phase 2)  
**Source:** Universe §4.Epic B (Sales Orders)  
**Status:** RESOLVED - Manual approval workflow

### 3. Pricing Engine (PRD-1505)
**Question:** Should we implement pricing logic (discounts, tiered pricing) in Phase 1.5?  
**Impact:** Affects SalesOrderLine.UnitPrice calculation  
**Recommendation:** Defer to Phase 2, use manual unit price entry for Phase 1.5  
**Source:** Universe §4.Epic B (Out of Scope)  
**Status:** RESOLVED - Deferred to Phase 2

### 4. Multi-Parcel Shipments (PRD-1507)
**Question:** Should we support 1 order → multiple shipments (e.g., 2 boxes)?  
**Impact:** Affects Shipment entity (1:1 vs 1:N relationship with OutboundOrder)  
**Recommendation:** Defer to Phase 2, assume 1 order = 1 shipment for Phase 1.5  
**Source:** Universe §4.Epic A (Out of Scope)  
**Status:** RESOLVED - Deferred to Phase 2

### 5. Agnum API vs CSV Export (PRD-1514, PRD-1515) - NEW
**Question:** Should we implement Agnum API integration or CSV export first?  
**Impact:** Affects export format and retry logic  
**Recommendation:** Implement both (CSV as fallback), prioritize CSV for Phase 1.5  
**Source:** Universe §4.Epic D (Agnum Integration)  
**Status:** OPEN - Needs stakeholder decision

### 6. 3D Visualization Library (PRD-1517, PRD-1518) - NEW
**Question:** Three.js or Babylon.js for 3D rendering?  
**Impact:** Affects bundle size, features, learning curve  
**Recommendation:** Three.js (lighter, MIT license, 600KB), Babylon.js for Phase 2 if needed  
**Source:** Universe §4.Epic E (3D Visualization)  
**Status:** RESOLVED - Three.js selected

---

## Next Recommended Tasks (Phase 1.5 Sprint 3 Candidates)

### High Priority (Must-Have for Production)
1. **PRD-1521** - Returns/RMA: RMA Entity + Workflow (L, 2 days)
2. **PRD-1522** - Returns/RMA: Inspection + Disposition (M, 1 day)
3. **PRD-1523** - Advanced Reporting: Traceability Report (M, 1 day)
4. **PRD-1524** - Advanced Reporting: Transaction Log Export (S, 0.5 day)
5. **PRD-1525** - Admin Config: Warehouse Settings UI (M, 1 day)

### Medium Priority (Operational Excellence)
6. **PRD-1526** - Wave Picking: Batch Picking + Route Optimization (L, 3 days)
7. **PRD-1527** - Cross-Docking: Receive → Ship Direct (M, 1 day)
8. **PRD-1528** - Multi-Level QC: Approval Workflow + Checklists (L, 2 days)

### Low Priority (Phase 2 Candidates)
9. **PRD-1529** - Handling Unit Hierarchy: Nested HUs + Split/Merge (L, 2 days)
10. **PRD-1530** - Serial Number Tracking: Serial Lifecycle (M, 2 days)

---

## Sprint Execution Recommendations

### Sprint 1 Critical Path (Completed)
1. **Week 1 (Days 1-5):**
   - PRD-1501 (Idempotency) - Day 1
   - PRD-1502 (Event Versioning) - Day 2
   - PRD-1503 (Correlation/Trace) - Day 2 (afternoon)
   - PRD-1504 (Customer + SalesOrder Entities) - Days 3-4
   - PRD-1506 (OutboundOrder + Shipment Entities) - Day 5

2. **Week 2 (Days 6-10):**
   - PRD-1505 (SalesOrder APIs) - Days 6-7
   - PRD-1507 (Packing MVP) - Day 8
   - PRD-1508 (Dispatch MVP) - Day 9
   - PRD-1509 (Projections) - Day 10

3. **Deferred to Sprint 2:**
   - PRD-1510 (UI) - 2 days

### Sprint 2 Critical Path (Current)
1. **Week 1 (Days 1-5):**
   - PRD-1511 (Valuation Aggregate) - Day 1
   - PRD-1512 (Cost Adjustment Command) - Day 2
   - PRD-1513 (OnHandValue Projection) - Day 3
   - PRD-1514 (Agnum Export Config + Job) - Days 4-5

2. **Week 2 (Days 6-10):**
   - PRD-1515 (Agnum CSV + API) - Day 6
   - PRD-1516 (Label Printing) - Day 7
   - PRD-1517 (3D Location Coords) - Days 8-9
   - PRD-1518 (3D UI) - Day 10

3. **Deferred to Sprint 3:**
   - PRD-1519 (Inter-Warehouse Transfers) - 1 day
   - PRD-1520 (Cycle Counting) - 2 days

### Risk Mitigation
- **Idempotency (PRD-1501):** Critical foundation, must complete first ✅
- **Event Versioning (PRD-1502):** Blocks all event-sourced features ✅
- **Valuation (PRD-1511-1513):** Critical for financial compliance, allocate buffer time
- **Agnum Integration (PRD-1514-1515):** External API dependency, test with mock first
- **3D Visualization (PRD-1517-1518):** Frontend-heavy, can parallelize with backend work
- **Label Printing (PRD-1516):** Hardware dependency (Zebra printer), test with simulator

### Testing Strategy
- Unit tests: 15-20 per task (included in DoD)
- Integration tests: End-to-end workflows (create order → allocate → pick → pack → dispatch → invoice)
- Manual testing: Postman collection for all APIs
- Performance testing: Idempotency check latency, event upcasting overhead, projection lag
- Financial testing: Valuation accuracy, on-hand value calculation, Agnum export reconciliation

---

### Sprint 3 Tasks (Fully Elaborated - Ready for Execution)

21. **PRD-1521** - Local Dev Auth & 403 Fix (M, 1 day)
22. **PRD-1522** - Data Model Type Alignment (Guid→int) (M, 1 day)
23. **PRD-1523** - Receiving Invoice Entry UI (L, 2 days)
24. **PRD-1524** - Receiving Scan & QC Workflow UI (L, 2 days)
25. **PRD-1525** - Stock Visibility Dashboard UI (M, 1 day)
26. **PRD-1526** - Stock Movement/Transfer UI (M, 1 day)
27. **PRD-1527** - Create Sales Order UI (L, 2 days)
28. **PRD-1528** - Sales Order List & Detail UI (M, 1 day)
29. **PRD-1529** - Allocation & Release UI (M, 1 day)
30. **PRD-1530** - Picking Workflow UI Enhancements (M, 1 day)
31. **PRD-1531** - Packing Station UI Enhancements (M, 1 day)
32. **PRD-1532** - Dispatch UI Enhancements (S, 0.5 day)
33. **PRD-1533** - Receiving History Report UI (S, 0.5 day)
34. **PRD-1534** - Dispatch History Report UI (S, 0.5 day)
35. **PRD-1535** - Stock Allocation Validation (M, 1 day)
36. **PRD-1536** - Optimistic Locking for Sales Orders (S, 0.5 day)
37. **PRD-1537** - Barcode Lookup Enhancement (S, 0.5 day)
38. **PRD-1538** - FedEx API Integration (Real) (M, 1 day)
39. **PRD-1539** - End-to-End Correlation Tracing (M, 1 day)
40. **PRD-1540** - Smoke E2E Integration Tests (L, 2 days)

**Sprint 3 Total Effort:** 17 days

### Sprint 4 Tasks (Fully Elaborated - Ready for Execution)

41. **PRD-1541** - E2E Inbound Workflow Test (M, 1 day)
42. **PRD-1542** - E2E Outbound Workflow Test (M, 1 day)
43. **PRD-1543** - E2E Stock Movement Test (S, 0.5 day)
44. **PRD-1544** - Health Check Dashboard (M, 1 day)
45. **PRD-1545** - Metrics Dashboards (Grafana) (L, 2 days)
46. **PRD-1546** - Alerting Rules & Runbook (M, 1 day)
47. **PRD-1547** - RBAC Enforcement (M, 1 day)
48. **PRD-1548** - Admin User Management UI (M, 1 day)
49. **PRD-1549** - Stock Movement History Report (M, 1 day)
50. **PRD-1550** - Transaction Log Export (S, 0.5 day)
51. **PRD-1551** - Traceability Report (Lot → Order) (M, 1 day)
52. **PRD-1552** - Compliance Audit Report (S, 0.5 day)
53. **PRD-1553** - Projection Rebuild Optimization (M, 1 day)
54. **PRD-1554** - Consistency Checks (Daily Job) (M, 1 day)
55. **PRD-1555** - Query Performance Optimization (S, 0.5 day)
56. **PRD-1556** - ERP Integration Mock & Tests (M, 1 day)
57. **PRD-1557** - Deployment Guide (S, 0.5 day)
58. **PRD-1558** - Operator Runbook (S, 0.5 day)
59. **PRD-1559** - API Documentation (Swagger) (S, 0.5 day)
60. **PRD-1560** - Production Readiness Checklist (M, 1 day)

**Sprint 4 Total Effort:** 16 days

### Sprint 5 Tasks (Fully Elaborated - Ready for Execution)

61. **PRD-1561** - Command Handler Idempotency Audit (M, 1 day)
62. **PRD-1562** - Projection Replay Safety (M, 1 day)
63. **PRD-1563** - Saga Step Checkpointing (M, 1 day)
64. **PRD-1564** - Aggregate Concurrency Tests (S, 0.5 day)
65. **PRD-1565** - Database Index Strategy (M, 1 day)
66. **PRD-1566** - Query Execution Plan Review (S, 0.5 day)
67. **PRD-1567** - Projection Rebuild Benchmarks (S, 0.5 day)
68. **PRD-1568** - API Response Time SLAs (M, 1 day)
69. **PRD-1569** - Structured Logging Enhancement (M, 1 day)
70. **PRD-1570** - Business Metrics Coverage (S, 0.5 day)
71. **PRD-1571** - Alert Tuning & Escalation (S, 0.5 day)
72. **PRD-1572** - Agnum Export Retry Hardening (M, 1 day)
73. **PRD-1573** - Label Printer Queue Resilience (M, 1 day)
74. **PRD-1574** - ERP Event Contract Tests (M, 1 day)
75. **PRD-1575** - Empty State & Error Handling UI (M, 1 day)
76. **PRD-1576** - Bulk Operations (Multi-Select) (M, 1 day)
77. **PRD-1577** - Advanced Search & Filters (M, 1 day)
78. **PRD-1578** - API Rate Limiting (S, 0.5 day)
79. **PRD-1579** - Sensitive Data Masking in Logs (S, 0.5 day)
80. **PRD-1580** - Load & Stress Testing Suite (L, 2 days)

**Sprint 5 Total Effort:** 15.5 days

### Sprint 6 Tasks (Fully Elaborated - Ready for Execution)

81. **PRD-1581** - Wave Creation & Batch Assignment (L, 2 days)
82. **PRD-1582** - Route Optimization Algorithm (M, 1 day)
83. **PRD-1583** - Wave Picking UI & Execution (M, 1 day)
84. **PRD-1584** - Cross-Dock Workflow & Routing (M, 1 day)
85. **PRD-1585** - Cross-Dock UI & Tracking (S, 0.5 day)
86. **PRD-1586** - QC Checklist Templates (M, 1 day)
87. **PRD-1587** - QC Defect Taxonomy & Tracking (M, 1 day)
88. **PRD-1588** - QC Attachments & Photo Upload (S, 0.5 day)
89. **PRD-1589** - RMA Creation & Workflow (L, 2 days)
90. **PRD-1590** - RMA Inspection & Disposition (M, 1 day)
91. **PRD-1591** - RMA UI & Customer Portal (M, 1 day)
92. **PRD-1592** - HU Split/Merge Operations (M, 1 day)
93. **PRD-1593** - Nested HU Validation & Tracking (M, 1 day)
94. **PRD-1594** - Serial Number Lifecycle Management (L, 2 days)
95. **PRD-1595** - Serial Status Tracking & Reporting (M, 1 day)
96. **PRD-1596** - Fulfillment KPIs Dashboard (M, 1 day)
97. **PRD-1597** - QC Defects & Late Shipments Analytics (S, 0.5 day)
98. **PRD-1598** - Contract Tests for External APIs (M, 1 day)
99. **PRD-1599** - Performance Regression Test Suite (M, 1 day)
100. **PRD-1600** - Operator Training Videos & Guides (L, 2 days)

**Sprint 6 Total Effort:** 18 days

**Combined Sprint 1+2+3+4+5+6 Total:** 104 days (100 tasks)

---

### Sprint 5 Tasks (Fully Elaborated - Ready for Execution)

61. **PRD-1561** - Command Handler Idempotency Audit (M, 1 day)
62. **PRD-1562** - Projection Replay Safety (M, 1 day)
63. **PRD-1563** - Saga Step Checkpointing (M, 1 day)
64. **PRD-1564** - Aggregate Concurrency Tests (S, 0.5 day)
65. **PRD-1565** - Database Index Strategy (M, 1 day)
66. **PRD-1566** - Query Execution Plan Review (S, 0.5 day)
67. **PRD-1567** - Projection Rebuild Benchmarks (S, 0.5 day)
68. **PRD-1568** - API Response Time SLAs (M, 1 day)
69. **PRD-1569** - Structured Logging Enhancement (M, 1 day)
70. **PRD-1570** - Business Metrics Coverage (S, 0.5 day)
71. **PRD-1571** - Alert Tuning & Escalation (S, 0.5 day)
72. **PRD-1572** - Agnum Export Retry Hardening (M, 1 day)
73. **PRD-1573** - Label Printer Queue Resilience (M, 1 day)
74. **PRD-1574** - ERP Event Contract Tests (M, 1 day)
75. **PRD-1575** - Empty State & Error Handling UI (M, 1 day)
76. **PRD-1576** - Bulk Operations (Multi-Select) (M, 1 day)
77. **PRD-1577** - Advanced Search & Filters (M, 1 day)
78. **PRD-1578** - API Rate Limiting (S, 0.5 day)
79. **PRD-1579** - Sensitive Data Masking in Logs (S, 0.5 day)
80. **PRD-1580** - Load & Stress Testing Suite (L, 2 days)

**Sprint 5 Total Effort:** 15.5 days

### Sprint 6 Tasks (Fully Elaborated - Ready for Execution)

81. **PRD-1581** - Wave Creation & Batch Assignment (L, 2 days)
82. **PRD-1582** - Route Optimization Algorithm (M, 1 day)
83. **PRD-1583** - Wave Picking UI & Execution (M, 1 day)
84. **PRD-1584** - Cross-Dock Workflow & Routing (M, 1 day)
85. **PRD-1585** - Cross-Dock UI & Tracking (S, 0.5 day)
86. **PRD-1586** - QC Checklist Templates (M, 1 day)
87. **PRD-1587** - QC Defect Taxonomy & Tracking (M, 1 day)
88. **PRD-1588** - QC Attachments & Photo Upload (S, 0.5 day)
89. **PRD-1589** - RMA Creation & Workflow (L, 2 days)
90. **PRD-1590** - RMA Inspection & Disposition (M, 1 day)
91. **PRD-1591** - RMA UI & Customer Portal (M, 1 day)
92. **PRD-1592** - HU Split/Merge Operations (M, 1 day)
93. **PRD-1593** - Nested HU Validation & Tracking (M, 1 day)
94. **PRD-1594** - Serial Number Lifecycle Management (L, 2 days)
95. **PRD-1595** - Serial Status Tracking & Reporting (M, 1 day)
96. **PRD-1596** - Fulfillment KPIs Dashboard (M, 1 day)
97. **PRD-1597** - QC Defects & Late Shipments Analytics (S, 0.5 day)
98. **PRD-1598** - Contract Tests for External APIs (M, 1 day)
99. **PRD-1599** - Performance Regression Test Suite (M, 1 day)
100. **PRD-1600** - Operator Training Videos & Guides (L, 2 days)

**Sprint 6 Total Effort:** 18 days

**Combined Sprint 1+2+3+4+5+6 Total:** 104 days (100 tasks)

---

## Baton Token

**BATON:** 2026-02-12T09:15:00Z-PHASE15-S5S6-SPEC-COMPLETE-7x3k9p2w

**Instructions for Next Session:**
- ✅ Phase 1.5 Sprint 1 & Sprint 2 task elaboration COMPLETE (20 tasks, PRD-1501 to PRD-1520)
- ✅ Phase 1.5 Sprint 3 & Sprint 4 task elaboration COMPLETE (40 tasks, PRD-1521 to PRD-1560)
- ✅ Phase 1.5 Sprint 5 & Sprint 6 task elaboration COMPLETE (40 tasks, PRD-1561 to PRD-1600)
- **ALL 100 TASKS FULLY DOCUMENTED** with: Context, Scope, Requirements, Gherkin scenarios, Validation, DoD
- **Sprint 3:** UI completeness + auth/validation fixes (PRD-1521 to PRD-1540)
- **Sprint 4:** E2E testing + observability + production readiness (PRD-1541 to PRD-1560)
- **Sprint 5:** Reliability hardening + performance + observability maturity (PRD-1561 to PRD-1580)
- **Sprint 6:** Advanced features (wave picking, RMA, QC, HU, serial, analytics) (PRD-1581 to PRD-1600)
- Next: Begin implementation from PRD-1561 (Sprint 5) OR PRD-1521 (Sprint 3) depending on priority
- Codex should log issues in `docs/prod-ready/codex-suspicions.md`
- Codex should end run with summary in `docs/prod-ready/codex-run-summary.md`

---

## Sprint 3 & 4 & 5 & 6 Success Criteria

**After Sprint 3, operator can:**
1. Obtain dev token and execute all API commands (no 403 errors)
2. Create inbound shipment via UI
3. Receive goods with barcode scan + QC
4. View stock dashboard with balances by location
5. Create Sales Order via UI
6. Pick, pack, dispatch orders via UI
7. View receiving and dispatch history reports

**After Sprint 4, system is production-ready:**
1. ✅ E2E integration tests validate all workflows
2. ✅ Health checks + Grafana dashboards + alerts operational
3. ✅ RBAC enforced, admin UI available
4. ✅ All reports accessible (Movement, Transaction Log, Traceability, Compliance)
5. ✅ Projection rebuild optimized, consistency checks run daily
6. ✅ Deployment guide + operator runbook + API docs complete
7. ✅ Production readiness checklist passes 100%

---

## Notes

- Sprint 3+4 tasks address all gaps from codex-suspicions.md (403 auth, Guid→int types, UI completeness)
- All tasks include detailed Gherkin scenarios (3-7 per task)
- All tasks include validation steps with exact curl commands (using dev token from PRD-1521)
- All UI tasks target Blazor Server (`src/LKvitai.MES.WebUI`), NOT React
- Task estimates: S=0.5d, M=1d, L=2d (validated against Sprint 1+2 velocity)
- Recommended team: 2 devs → Sprint 3+4 complete in 4 weeks (2 weeks per sprint)

---

## Files Created

### Sprint 1 Files
- `prod-ready-tasks-PHASE15-S1.md` - Full task details (10 tasks, ~2,800 lines)
- `prod-ready-tasks-PHASE15-S1-summary.md` - Task summary

### Sprint 2 Files
- `prod-ready-tasks-PHASE15-S2.md` - Full task details (10 tasks, ~2,200 lines)
- `prod-ready-tasks-PHASE15-S2-summary.md` - Task summary

### Sprint 3 Files (NEW)
- `prod-ready-tasks-PHASE15-S3.md` - Full task details (20 tasks, UI completeness + validation fixes)
- `prod-ready-tasks-PHASE15-S3-summary.md` - Task summary with critical path

### Sprint 4 Files (NEW)
- `prod-ready-tasks-PHASE15-S4.md` - Full task details (20 tasks, E2E testing + production readiness)
- `prod-ready-tasks-PHASE15-S4-summary.md` - Task summary with operational checklist

### Sprint 5 Files (NEW)
- `prod-ready-tasks-PHASE15-S5.md` - Full task details (20 tasks, reliability hardening + performance + observability)
- `prod-ready-tasks-PHASE15-S5-summary.md` - Task summary with critical path

### Sprint 6 Files (NEW)
- `prod-ready-tasks-PHASE15-S6.md` - Full task details (20 tasks, advanced features: wave picking, RMA, QC, HU, serial, analytics)
- `prod-ready-tasks-PHASE15-S6-summary.md` - Task summary with go-live checklist

### Progress Tracking
- `prod-ready-tasks-progress.md` - This file (updated with Sprint 1-6 completion status)

---

## Sprint 5 & 6 Specifications Generated

**Date:** February 12, 2026
**Status:** Sprint 5 & Sprint 6 task packs complete and ready for execution

### Sprint 5 Focus (PRD-1561 to PRD-1580)
- **Reliability Hardening:** Idempotency audit, projection replay safety, saga checkpointing, concurrency tests
- **Performance Baseline:** Database indexing, query optimization, projection benchmarks, API SLAs
- **Observability Maturity:** Structured logging, business metrics, alert tuning
- **Integration Resilience:** Agnum retry hardening, label printer queue, ERP contract tests
- **UI Quality:** Empty states, error handling, bulk operations, advanced search
- **Security:** API rate limiting, sensitive data masking
- **Testing:** Load & stress testing suite

### Sprint 6 Focus (PRD-1581 to PRD-1600)
- **Wave Picking:** Wave creation, route optimization, wave picking UI (3 tasks)
- **Cross-Docking:** Workflow & routing, UI & tracking (2 tasks)
- **Multi-Level QC:** Checklist templates, defect taxonomy, photo attachments (3 tasks)
- **Returns/RMA:** Creation & workflow, inspection & disposition, UI & portal (3 tasks)
- **HU Hierarchy:** Split/merge operations, nested validation & tracking (2 tasks)
- **Serial Tracking:** Lifecycle management, status tracking & reporting (2 tasks)
- **Analytics:** Fulfillment KPIs, QC defects & late shipments (2 tasks)
- **Testing & Documentation:** Contract tests, performance regression, training materials (3 tasks)

