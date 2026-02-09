# Master Data Implementation Order

## Recommended Execution Order (Topological)

### Phase 0: Critical Foundation (Week 1-2, BLOCKING)
**Duration**: 2 weeks
**Team**: 1 Backend Dev

**CRITICAL**: Must complete before any other work. Projection rebuild issue blocks all event-sourced features.

1. **Task 0.1**: Root Cause Analysis and Schema Separation (1 day)
2. **Task 0.2**: Orphaned Shadow Table Cleanup Automation (0.5 day)
3. **Task 0.3**: Pre-Deployment Schema Validation (0.5 day)
4. **Task 0.4**: Distributed Lock on Rebuild Operations (1 day)
5. **Task 0.5**: Startup Schema Validation Service (0.5 day)
6. **Task 0.6**: Zero-Downtime Rebuild with Shadow Tables (1 day)
7. **Task 1.1**: Entity Models with Constraints and Indexes (1.5 days)
8. **Task 1.2**: Seed Data (0.5 day)
9. **Task 1.3**: Audit Fields Pattern (0.5 day)
10. **Task 1.4**: InternalSKU Auto-Generation Logic (1 day)
11. **Task 1.5**: Database Migrations (0.5 day)

**Milestone**: Projection rebuild works without errors, master data schema complete

**Validation**:
- [ ] Projection rebuild completes on clean database without 42P01 error
- [ ] Schema separation verified (EF in `public`, Marten in `warehouse_events`)
- [ ] All master data tables created with constraints and indexes
- [ ] Seed data loaded (UoM, virtual locations, reason codes, categories, HU types)
- [ ] InternalSKU auto-generation tested (RM-0001, FG-0001)

---

### Phase 1: Import System (Week 3, CAN PARALLELIZE with Phase 2)
**Duration**: 1 week
**Team**: 1 Backend Dev

**Dependencies**: Phase 0 complete (master data schema exists)

12. **Task 2.1**: Excel Template Generation (1 day)
13. **Task 2.2**: Excel Parsing and Validation Engine (1.5 days)
14. **Task 2.3**: Upsert Logic (1 day)
15. **Task 2.4**: Dry-Run Mode (0.5 day)
16. **Task 2.5**: Error Reporting (0.5 day)
17. **Task 2.6**: Batch Insert Optimization (0.5 day)

**Milestone**: Import 500 items from Excel in <5 minutes

**Validation**:
- [ ] Excel templates downloadable for all entity types
- [ ] Import 500-row file completes in <5 minutes
- [ ] Validation catches all error types (missing columns, invalid FK, duplicate SKU)
- [ ] Dry-run mode validates without DB writes
- [ ] Error report downloadable as Excel

---

### Phase 2: Event Store & Projections (Week 4, BLOCKING for Phase 3-6)
**Duration**: 1 week
**Team**: 1 Backend Dev

**Dependencies**: Phase 0 complete (projection rebuild fixed)

18. **Task 3.1**: Event Contracts (1 day)
19. **Task 3.2**: AvailableStock Projection (1.5 days)
20. **Task 3.3**: LocationBalance Projection (1 day)
21. **Task 3.4**: ActiveReservations Projection (0.5 day)
22. **Task 3.5**: InboundShipmentSummary Projection (0.5 day)
23. **Task 3.6**: AdjustmentHistory Projection (0.5 day)
24. **Task 3.7**: Projection Health Check API (0.5 day)

**Milestone**: All projections implemented, projection lag <1 second

**Validation**:
- [ ] All 8 event types defined and serializable
- [ ] AvailableStock projection updates on all relevant events
- [ ] Projection rebuild from 1000 events completes in <1 minute
- [ ] Projection health check returns lag status
- [ ] Projection lag <1 second under normal load

---

### Phase 3: Receiving Workflow (Week 5, CAN PARALLELIZE with Phase 4-6)
**Duration**: 1 week
**Team**: 1 Backend Dev

**Dependencies**: Phase 0 (master data), Phase 2 (event store) complete

25. **Task 4.1**: Inbound Shipment Creation (1 day)
26. **Task 4.2**: Receive Goods with Barcode Scanning (1.5 days)
27. **Task 4.3**: Lot Tracking (0.5 day)
28. **Task 4.4**: QC Pass/Fail Actions (1 day)
29. **Task 4.5**: Receiving Dashboard (1 day)

**Milestone**: Receive 10-item shipment with QC in <15 minutes

**Validation**:
- [ ] Shipment created with lines
- [ ] Goods received with barcode scanning
- [ ] Lot tracking enforced for RequiresLotTracking items
- [ ] QC pass moves stock to RECEIVING, QC fail moves to QUARANTINE
- [ ] Receiving dashboard displays shipment status

---

### Phase 4: Putaway Workflow (Week 5, CAN PARALLELIZE with Phase 3, 5, 6)
**Duration**: 0.5 week
**Team**: 1 Backend Dev

**Dependencies**: Phase 2 (event store), Phase 3 (receiving) complete

30. **Task 5.1**: Putaway Task List (0.5 day)
31. **Task 5.2**: Location Barcode Scanning (0.5 day)
32. **Task 5.3**: Capacity Warning (0.5 day)
33. **Task 5.4**: StockMoved Event Emission (0.5 day)

**Milestone**: Putaway with location capacity warnings

**Validation**:
- [ ] Putaway task list shows items in RECEIVING
- [ ] Location barcode validated
- [ ] Capacity warning shown if >80%
- [ ] StockMoved event appended

---

### Phase 5: Picking Workflow (Week 6, CAN PARALLELIZE with Phase 6)
**Duration**: 0.75 week
**Team**: 1 Backend Dev

**Dependencies**: Phase 2 (event store), Phase 4 (putaway) complete

34. **Task 6.1**: Pick Task Creation (0.5 day)
35. **Task 6.2**: Pick Execution (1.5 days)
36. **Task 6.3**: FEFO Location Suggestion (0.5 day)
37. **Task 6.4**: PickCompleted Event Emission (0.5 day)
38. **Task 6.5**: Pick History Report (0.5 day)

**Milestone**: Complete 5-item pick in <5 minutes

**Validation**:
- [ ] Pick task created
- [ ] Pick execution with barcode scanning
- [ ] FEFO location suggestion (earliest expiry first)
- [ ] PickCompleted event appended
- [ ] Pick history report displays completed picks

---

### Phase 6: Stock Adjustments (Week 6, CAN PARALLELIZE with Phase 5)
**Duration**: 0.5 week
**Team**: 1 Backend Dev

**Dependencies**: Phase 2 (event store) complete

39. **Task 7.1**: Adjustment Creation (1 day)
40. **Task 7.2**: Confirmation Dialog (0.5 day)
41. **Task 7.3**: StockAdjusted Event Emission (0.5 day)
42. **Task 7.4**: Adjustment History Report (0.5 day)

**Milestone**: Adjust stock with audit trail

**Validation**:
- [ ] Adjustment creates StockAdjusted event
- [ ] Confirmation dialog shown
- [ ] Adjustment history report displays all adjustments

---

### Phase 7: Admin UI (Week 3-7, CAN PARALLELIZE with all backend work)
**Duration**: 4 weeks (spread across backend development)
**Team**: 1 Frontend Dev

**Dependencies**: Phase 0 (master data schema), Phase 1 (import APIs) for import wizard

**Epic 8 Tasks** (not detailed in tasks.md, see UI scope doc):
- 8.1 Items management (list, create, edit, deactivate) - 1.5 days
- 8.2 Suppliers management - 1 day
- 8.3 Locations management (hierarchical tree view) - 1.5 days
- 8.4 Categories management - 0.5 day
- 8.5 Barcodes management (multi-barcode per item) - 1 day
- 8.6 Import wizard UI (tabbed interface, drag-drop upload) - 2 days

**Milestone**: Admin can manage all master data via UI

**Validation**:
- [ ] Items CRUD functional
- [ ] Import wizard uploads Excel and displays validation results
- [ ] All admin pages follow existing UI patterns (pagination, filters, CSV export)

---

### Phase 8: Operational Workflows UI (Week 5-7, DEPENDS on backend workflows)
**Duration**: 2 weeks
**Team**: 1 Frontend Dev

**Dependencies**: Phase 3 (receiving), Phase 4 (putaway), Phase 5 (picking), Phase 6 (adjustments) complete

**Epic 10 Tasks** (not detailed in tasks.md, see UI scope doc):
- 10.1 Receiving pages (shipment list, detail, receive modal, QC panel) - 2 days
- 10.2 Putaway page (task list, location scan modal) - 1 day
- 10.3 Picking pages (task list, execution page with barcode scan) - 2 days
- 10.4 Adjustments pages (create form, history list) - 1 day
- 10.5 Barcode scanner component (auto-focus, auto-submit, manual fallback) - 1 day

**Milestone**: All operational workflows accessible via UI

**Validation**:
- [ ] Receiving workflow complete (shipment → receive → QC → putaway)
- [ ] Picking workflow complete (task → execute → complete)
- [ ] Barcode scanner component works (auto-submit on Enter)

---

### Phase 9: Reports & Stock Visibility (Week 4-7, CAN PARALLELIZE with all work)
**Duration**: 1 week (spread across backend development)
**Team**: 1 Backend Dev or Frontend Dev

**Dependencies**: Phase 2 (projections) complete

**Epic 9 Tasks** (not detailed in tasks.md, see UI scope doc):
- 9.1 Available Stock report (filterable by item, location, category, expiry) - 1 day
- 9.2 Location Balance report (capacity utilization) - 0.5 day
- 9.3 Reservations report (active locks) - 0.5 day
- 9.4 CSV export for all reports - 0.5 day
- 9.5 Projection timestamp display (staleness indicator) - 0.5 day

**Milestone**: Real-time stock visibility with CSV export

**Validation**:
- [ ] Available Stock report displays all stock
- [ ] Filters work (item, location, category, expiry)
- [ ] CSV export works for 10k rows in <3 seconds
- [ ] Projection timestamp displayed ("Stock as of HH:MM:SS")

---

### Phase 10: Testing & Deployment (Week 7-8)
**Duration**: 2 weeks
**Team**: 1 QA Engineer + all devs

**Dependencies**: All phases complete

**Tasks**:
- Write unit tests (200+ tests) - 2 days
- Write integration tests (100+ tests) - 2 days
- Write projection tests (50+ tests) - 1 day
- Write workflow tests (30+ tests) - 1 day
- Write UI tests (20+ tests) - 1 day
- Write E2E tests (15+ tests) - 1 day
- Performance testing (load test, projection lag) - 1 day
- UAT with warehouse operators - 2 days
- Bug fixes from UAT - 2 days
- Deployment to staging - 0.5 day
- Deployment to production - 0.5 day

**Milestone**: Production deployment successful

**Validation**:
- [ ] Test coverage >80%
- [ ] Zero critical bugs from UAT
- [ ] Performance targets met (import <5 min, projection lag <1 sec, API <500ms)
- [ ] Staging deployment successful
- [ ] Production deployment successful

---

## Can Parallelize Sections

### Week 1-2: Phase 0 (BLOCKING - no parallelization)
**Critical path**: Fix projection rebuild issue, create master data schema

### Week 3: Phase 1 (Import) + Phase 7 (Admin UI start)
**Parallel tracks**:
- Backend Dev: Import system (Tasks 12-17)
- Frontend Dev: Admin UI foundation (layout, navigation, pagination component)

### Week 4: Phase 2 (Event Store) + Phase 7 (Admin UI continue) + Phase 9 (Reports start)
**Parallel tracks**:
- Backend Dev: Event store & projections (Tasks 18-24)
- Frontend Dev: Admin UI (Items management, Suppliers management)
- Backend Dev (part-time): Reports API endpoints (Tasks 9.1-9.4)

### Week 5: Phase 3 (Receiving) + Phase 4 (Putaway) + Phase 7 (Admin UI continue)
**Parallel tracks**:
- Backend Dev: Receiving workflow (Tasks 25-29)
- Backend Dev (part-time): Putaway workflow (Tasks 30-33)
- Frontend Dev: Admin UI (Locations management, Import wizard)

### Week 6: Phase 5 (Picking) + Phase 6 (Adjustments) + Phase 8 (Operational UI start)
**Parallel tracks**:
- Backend Dev: Picking workflow (Tasks 34-38)
- Backend Dev (part-time): Adjustments workflow (Tasks 39-42)
- Frontend Dev: Operational UI (Receiving pages)

### Week 7: Phase 8 (Operational UI continue) + Phase 9 (Reports UI) + Testing
**Parallel tracks**:
- Frontend Dev: Operational UI (Putaway, Picking, Adjustments pages)
- Frontend Dev (part-time): Reports UI (Tasks 9.1-9.5)
- QA Engineer: Write tests (unit, integration, projection, workflow)

### Week 8: Testing + Deployment
**Parallel tracks**:
- QA Engineer: E2E tests, performance tests
- All devs: Bug fixes from UAT
- DevOps: Deployment to staging, production

---

## Blocking Risks & Mitigations

### Risk 1: Projection Rebuild Issue Not Resolved (CRITICAL)
**Impact**: Blocks all event-sourced features (receiving, putaway, picking, adjustments)
**Probability**: Medium (known issue, root cause unclear)
**Mitigation**:
- Allocate 2 full weeks to Phase 0 (no shortcuts)
- Involve Marten expert if needed (community support, paid consulting)
- Fallback: Use EF Core for operational data (lose event sourcing benefits)
- Test projection rebuild on clean database BEFORE proceeding to Phase 2

**Contingency Plan**:
- If not resolved by end of Week 2: Escalate to management, consider fallback to EF Core
- If fallback to EF Core: Lose audit trail, projection lag monitoring, event replay capabilities

---

### Risk 2: Import Performance Below Target (<5 min for 500 items)
**Impact**: User frustration, delayed data preparation
**Probability**: Low (bulk insert optimization should work)
**Mitigation**:
- Implement batch insert optimization (Task 2.6) early
- Load test with 500-row file during Week 3
- If slow: Profile queries, add indexes, use EFCore.BulkExtensions
- Fallback: Increase target to 10 minutes (still acceptable)

**Contingency Plan**:
- If >10 minutes: Split import into smaller batches (100 rows each)
- If still slow: Investigate database performance (disk I/O, CPU)

---

### Risk 3: Projection Lag Exceeds 1 Second
**Impact**: Stale stock visibility, user confusion
**Probability**: Medium (depends on event volume)
**Mitigation**:
- Implement projection health check (Task 3.7) early
- Monitor projection lag during development (log every event append)
- If lag high: Add more projection workers (Marten async configuration)
- Optimize projection queries (add indexes, denormalize fields)

**Contingency Plan**:
- If lag >10 seconds: Display staleness warning in UI ("Stock as of HH:MM:SS")
- If lag >60 seconds: Block writes (read-only mode) until lag resolved

---

### Risk 4: Barcode Scanning Not Working (Hardware/Browser Issues)
**Impact**: Workflow blocked, manual entry required
**Probability**: Medium (depends on hardware, browser compatibility)
**Mitigation**:
- Test barcode scanner early (Week 5, Task 4.2)
- Support both USB scanner (keyboard wedge) and camera (WebRTC)
- Provide manual entry fallback (checkbox reveals dropdown)
- Test on target hardware (tablet, browser)

**Contingency Plan**:
- If USB scanner not working: Use camera-based scanning (WebRTC)
- If camera not working: Use manual entry (dropdown selection)
- If all fail: Defer barcode scanning to Phase 2, use manual entry only

---

### Risk 5: UAT Reveals Critical Bugs (Week 8)
**Impact**: Delayed production deployment
**Probability**: Medium (complex workflows, many edge cases)
**Mitigation**:
- Allocate 2 days for bug fixes in Week 8
- Prioritize critical bugs (blocking workflows)
- Defer non-critical bugs to Phase 2
- Conduct internal testing before UAT (Week 7)

**Contingency Plan**:
- If >5 critical bugs: Extend UAT by 1 week, delay production deployment
- If >10 critical bugs: Re-evaluate scope, defer features to Phase 2

---

### Risk 6: Database Performance Issues (Slow Queries)
**Impact**: API response time >500ms, poor user experience
**Probability**: Low (indexes planned, queries optimized)
**Mitigation**:
- Add indexes for all FK columns, query filters (Task 1.1)
- Profile slow queries (pg_stat_statements)
- Add composite indexes for common filter combinations
- Use EXPLAIN ANALYZE to optimize query plans

**Contingency Plan**:
- If queries slow: Add covering indexes (include columns in index)
- If still slow: Denormalize data (add redundant columns to avoid joins)
- If still slow: Add read replicas (scale horizontally)

---

### Risk 7: Concurrent Reservation Conflicts (Retry Storms)
**Impact**: Reservation failures, order fulfillment blocked
**Probability**: Low (optimistic concurrency with retry should work)
**Mitigation**:
- Implement jittered backoff (Task 3.4 - distributed lock)
- Limit retries to 3 attempts
- Monitor retry rate (log failed reservations)
- If high conflict rate: Consider pessimistic locking (Phase 2)

**Contingency Plan**:
- If retry storms: Increase retry delay (exponential backoff)
- If still failing: Switch to pessimistic locking (database row lock)

---

### Risk 8: Team Availability (Sick Leave, Vacation)
**Impact**: Delayed timeline, missed milestones
**Probability**: Medium (8-week project, holidays possible)
**Mitigation**:
- Cross-train team members (backend dev can do frontend, vice versa)
- Document all decisions (ADRs, runbooks)
- Use pair programming for critical tasks (projection rebuild)
- Buffer 1 week in timeline (Week 8 has slack)

**Contingency Plan**:
- If backend dev unavailable: Frontend dev picks up backend tasks (or vice versa)
- If both devs unavailable: Extend timeline by 1 week

---

## Critical Path Summary

**Longest path**: Phase 0 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 8 → Phase 10

**Total duration**: 8 weeks

**Bottlenecks**:
- Phase 0 (Week 1-2): Projection rebuild fix (BLOCKING)
- Phase 2 (Week 4): Event store & projections (BLOCKING for Phase 3-6)
- Phase 8 (Week 6-7): Operational UI (DEPENDS on Phase 3-6)

**Parallelization opportunities**:
- Phase 1 (Import) can run parallel to Phase 2 (Event Store)
- Phase 3-6 (Workflows) can run partially parallel (different devs)
- Phase 7 (Admin UI) can run parallel to all backend work
- Phase 9 (Reports) can run parallel to all work

**Recommended team allocation**:
- Week 1-2: 1 Backend Dev (Phase 0 - projection fix)
- Week 3: 1 Backend Dev (Phase 1 - import), 1 Frontend Dev (Phase 7 - admin UI)
- Week 4: 1 Backend Dev (Phase 2 - event store), 1 Frontend Dev (Phase 7 - admin UI)
- Week 5: 1 Backend Dev (Phase 3-4 - receiving, putaway), 1 Frontend Dev (Phase 7 - admin UI)
- Week 6: 1 Backend Dev (Phase 5-6 - picking, adjustments), 1 Frontend Dev (Phase 8 - operational UI)
- Week 7: 1 Frontend Dev (Phase 8-9 - operational UI, reports), 1 QA Engineer (testing)
- Week 8: All team (testing, bug fixes, deployment)

**Success criteria**:
- Phase 0 complete by end of Week 2 (projection rebuild works)
- Phase 2 complete by end of Week 4 (event store & projections working)
- All backend workflows complete by end of Week 6
- All UI complete by end of Week 7
- Production deployment by end of Week 8
