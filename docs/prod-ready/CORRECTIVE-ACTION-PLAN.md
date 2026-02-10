# Production-Ready Task Plan - Corrective Action Plan

**Date:** February 10, 2026  
**Review Status:** NOT OK (per prod-ready-tasks-review.md)  
**Reviewer:** Claude (Senior Technical Program Manager + Solution Architect)

---

## Executive Summary

The initial task plan has **critical gaps** that prevent immediate execution by developers:

- **Only 4 tasks** (2%) have complete specifications
- **176+ tasks** (98%) lack detailed requirements, acceptance criteria, and validation commands
- **13 task detail files missing** for Phase 1.5-4 epics
- **Estimated work required:** ~31,000 lines of additional specifications

**Verdict:** Task plan requires substantial completion before handoff to development team.

---

## Immediate Actions Required

### Phase 1: Complete Critical Path (Phase 1.5 MVP) - Priority 1

**Goal:** Enable developers to start Phase 1.5 work (14 weeks to production MVP)

**Tasks to Complete:**

#### 1. Foundation Tasks (PRD-0002 to PRD-0015) - BLOCKING EVERYTHING
**Status:** PRD-0001 complete, PRD-0002-0003 partially complete, PRD-0004-0015 missing  
**File:** `prod-ready-tasks-02-foundation.md`  
**Work Required:** ~1,500 lines (12 tasks × 125 lines avg)

**Critical Tasks:**
- PRD-0002: Event Schema Versioning (STARTED - needs completion)
- PRD-0003: Correlation/Trace Propagation (STARTED - needs completion)
- PRD-0004: Projection Rebuild Hardening
- PRD-0005: RBAC Permission Model Finalization
- PRD-0006: Integration Test Harness
- PRD-0007: Contract Test Framework
- PRD-0008: Sample Data Seeding
- PRD-0009: Observability Metrics Setup
- PRD-0010: Backup & Disaster Recovery
- PRD-0011: Event Catalog Validation (NEW)
- PRD-0012: API Contract Validation (NEW)
- PRD-0013: Status Enum Consistency (NEW)
- PRD-0014: Migration Sequencing (NEW)
- PRD-0015: State Machine Validation (NEW)

#### 2. Valuation Tasks (PRD-0101 to PRD-0120) - BLOCKS AGNUM
**Status:** PRD-0100 partially complete, PRD-0101-0120 missing  
**File:** `prod-ready-tasks-03-valuation.md`  
**Work Required:** ~2,500 lines (20 tasks × 125 lines avg)

**Critical Tasks:**
- PRD-0101: ItemValuation Aggregate Implementation
- PRD-0102: Cost Adjustment Command & Handler
- PRD-0103: Landed Cost Allocation Logic
- PRD-0104: Write-Down Workflow & Approval
- PRD-0105: OnHandValue Projection
- PRD-0108: Valuation API Endpoints

#### 3. Agnum Integration Tasks (PRD-0200 to PRD-0215) - BLOCKS FINANCIAL RECONCILIATION
**Status:** Missing entirely  
**File:** `prod-ready-tasks-06-agnum.md` (NEW FILE REQUIRED)  
**Work Required:** ~2,000 lines (16 tasks × 125 lines avg)

**Critical Tasks:**
- PRD-0200: Agnum Export Configuration Model
- PRD-0202: Agnum Export Saga Implementation
- PRD-0203: CSV Export Generation
- PRD-0204: Agnum API Integration (JSON)

#### 4. Outbound/Shipment Tasks (PRD-0301 to PRD-0325) - BLOCKS SALES ORDERS
**Status:** PRD-0300, PRD-0304 partially complete, PRD-0301-0303, PRD-0305-0325 missing  
**File:** `prod-ready-tasks-04-outbound.md`  
**Work Required:** ~3,000 lines (25 tasks × 120 lines avg)

**Critical Tasks:**
- PRD-0301: Shipment Entity & Schema
- PRD-0302: OutboundOrder State Machine
- PRD-0303: Shipment State Machine
- PRD-0306: Dispatch Shipment Command
- PRD-0308: Outbound Events
- PRD-0312: Outbound API Endpoints

#### 5. Sales Orders Tasks (PRD-0400 to PRD-0425) - COMPLETES PHASE 1.5 MVP
**Status:** Missing entirely  
**File:** `prod-ready-tasks-05-sales-orders.md` (NEW FILE REQUIRED)  
**Work Required:** ~3,200 lines (26 tasks × 123 lines avg)

**Critical Tasks:**
- PRD-0400: Customer Entity & Schema
- PRD-0401: SalesOrder Entity & Schema
- PRD-0402: SalesOrder State Machine
- PRD-0403: Create Sales Order Command
- PRD-0404: Allocation Saga Implementation
- PRD-0409: Sales Order API Endpoints

#### 6. 3D Visualization Tasks (PRD-0500 to PRD-0515) - COMPLETES PHASE 1.5 USP
**Status:** Missing entirely  
**File:** `prod-ready-tasks-07-3d-viz.md` (NEW FILE REQUIRED)  
**Work Required:** ~2,000 lines (16 tasks × 125 lines avg)

**Critical Tasks:**
- PRD-0500: Location Coordinates Schema
- PRD-0502: 3D Visualization API Endpoint
- PRD-0504: Three.js Integration
- PRD-0506: Interactive Click-to-Details

**Phase 1 Total Work Required:** ~14,200 lines

---

### Phase 2: Add Quality Gates to All Tasks - Priority 2

**Goal:** Enable QA and local verification

**Work Required for ALL 180+ tasks:**

1. **Acceptance Criteria** (~9,000 lines)
   - Add 3-5 Gherkin scenarios per task
   - Template: Happy path, error case, edge case, idempotency, concurrency

2. **Validation Commands** (~3,600 lines)
   - Add 3-5 bash commands per task
   - Template: dotnet test, psql queries, curl API calls, metrics check

3. **Precise SourceRefs** (~360 lines)
   - Update all references to include section + subsection + line range
   - Format: `Universe §4.Epic C > Entities & Data Model Changes (lines 1295-1350)`

**Phase 2 Total Work Required:** ~12,960 lines

---

### Phase 3: Complete Phase 2-4 Epics - Priority 3

**Goal:** Enable full universe implementation

**Files to Create (13 new files):**

1. `prod-ready-tasks-08-cycle-counting.md` (PRD-0600 to PRD-0615, 16 tasks, ~2,000 lines)
2. `prod-ready-tasks-09-returns.md` (PRD-0700 to PRD-0715, 16 tasks, ~2,000 lines)
3. `prod-ready-tasks-10-label-printing.md` (PRD-0800 to PRD-0810, 11 tasks, ~1,400 lines)
4. `prod-ready-tasks-11-transfers.md` (PRD-0900 to PRD-0910, 11 tasks, ~1,400 lines)
5. `prod-ready-tasks-12-reporting.md` (PRD-1000 to PRD-1015, 16 tasks, ~2,000 lines)
6. `prod-ready-tasks-13-wave-picking.md` (PRD-1100 to PRD-1115, 16 tasks, ~2,000 lines)
7. `prod-ready-tasks-14-cross-docking.md` (PRD-1200 to PRD-1210, 11 tasks, ~1,400 lines)
8. `prod-ready-tasks-15-multi-level-qc.md` (PRD-1300 to PRD-1315, 16 tasks, ~2,000 lines)
9. `prod-ready-tasks-16-hu-hierarchy.md` (PRD-1400 to PRD-1415, 16 tasks, ~2,000 lines)
10. `prod-ready-tasks-17-serial-tracking.md` (PRD-1500 to PRD-1520, 21 tasks, ~2,600 lines)
11. `prod-ready-tasks-18-admin-config.md` (PRD-1600 to PRD-1610, 11 tasks, ~1,400 lines)
12. `prod-ready-tasks-19-security.md` (PRD-1700 to PRD-1720, 21 tasks, ~2,600 lines)

**Phase 3 Total Work Required:** ~22,800 lines

---

## Total Work Required

| Phase | Description | Lines | Priority |
|-------|-------------|-------|----------|
| Phase 1 | Complete Phase 1.5 critical path (6 epics) | ~14,200 | **CRITICAL** |
| Phase 2 | Add quality gates to all tasks | ~12,960 | **HIGH** |
| Phase 3 | Complete Phase 2-4 epics (12 epics) | ~22,800 | MEDIUM |
| **Total** | **Complete task plan** | **~49,960** | - |

---

## Recommended Approach

### Option 1: Incremental Completion (RECOMMENDED)

**Strategy:** Complete tasks in phases, enable development to start ASAP

**Week 1-2: Foundation + Valuation**
- Complete PRD-0002 to PRD-0015 (Foundation)
- Complete PRD-0101 to PRD-0120 (Valuation)
- **Deliverable:** Developers can start Foundation + Valuation work
- **Lines:** ~4,000

**Week 3: Agnum + Outbound**
- Complete PRD-0200 to PRD-0215 (Agnum)
- Complete PRD-0301 to PRD-0325 (Outbound)
- **Deliverable:** Developers can start Agnum + Outbound work
- **Lines:** ~5,000

**Week 4: Sales Orders + 3D Viz**
- Complete PRD-0400 to PRD-0425 (Sales Orders)
- Complete PRD-0500 to PRD-0515 (3D Viz)
- **Deliverable:** Phase 1.5 MVP fully specified
- **Lines:** ~5,200

**Week 5-6: Quality Gates**
- Add acceptance criteria to all Phase 1.5 tasks
- Add validation commands to all Phase 1.5 tasks
- Update SourceRefs for precision
- **Deliverable:** Phase 1.5 tasks execution-ready
- **Lines:** ~6,500

**Week 7-10: Phase 2-4 Epics**
- Complete remaining 12 epic files
- Add quality gates to Phase 2-4 tasks
- **Deliverable:** Full universe specified
- **Lines:** ~29,260

**Total Duration:** 10 weeks of AI task generation

**Advantage:** Development can start in parallel (Week 3 onwards)

---

### Option 2: Complete Before Handoff

**Strategy:** Complete all tasks before development starts

**Duration:** 10-12 weeks of AI task generation  
**Advantage:** No rework, complete specifications  
**Disadvantage:** Development delayed 10-12 weeks

---

### Option 3: Hybrid (PRAGMATIC)

**Strategy:** Complete Phase 1.5 critical path + quality gates, defer Phase 2-4

**Week 1-4: Phase 1.5 Complete**
- Complete all Phase 1.5 tasks (Foundation, Valuation, Agnum, Outbound, Sales Orders, 3D Viz)
- Add acceptance criteria and validation commands
- **Deliverable:** Phase 1.5 MVP fully specified and execution-ready
- **Lines:** ~20,700

**Week 5-6: Phase 2 (Operational Excellence)**
- Complete Cycle Counting, Returns, Label Printing, Transfers, Reporting
- **Deliverable:** Phase 2 specified
- **Lines:** ~9,200

**Defer Phase 3-4:**
- Complete during Phase 1.5 development (parallel work)
- **Lines:** ~20,060

**Total Duration:** 6 weeks to Phase 1.5 + Phase 2 complete

**Advantage:** Fastest path to production MVP, Phase 3-4 completed in parallel with development

---

## Immediate Next Steps

### For Kiro (AI Task Generator)

1. **Choose Approach:** Recommend Option 3 (Hybrid) for fastest time-to-production
2. **Start Week 1:** Complete Foundation tasks (PRD-0002 to PRD-0015)
3. **Use Template:** Follow PRD-0001 format (168 lines) for all tasks
4. **Quality Standard:** Every task MUST have:
   - Context (3-8 bullets)
   - Scope (In/Out)
   - Requirements (Functional, Non-Functional, Data Model, API, UI)
   - Acceptance Criteria (3-5 Gherkin scenarios)
   - Implementation Notes
   - Validation/Checks (3-5 bash commands)
   - Definition of Done (8-15 checklist items)

### For Development Team

1. **Wait for Foundation:** Do NOT start Phase 1.5 work until Foundation tasks complete (Week 2)
2. **Review Completed Tasks:** As tasks are completed, review for accuracy and completeness
3. **Provide Feedback:** If task spec unclear, request clarification immediately
4. **Track Progress:** Use master index to track task completion status

### For Project Manager

1. **Adjust Timeline:** Add 2-6 weeks to project start (depending on approach chosen)
2. **Resource Planning:** Allocate AI task generation resources (Kiro) for 6-10 weeks
3. **Parallel Tracks:** Plan for development to start Week 3 (Option 1) or Week 7 (Option 2) or Week 5 (Option 3)
4. **Risk Mitigation:** Monitor task completion velocity, adjust approach if needed

---

## Success Criteria (Post-Correction)

### Task Completeness
- [ ] All 180+ tasks have detailed specifications
- [ ] All tasks have 3-8 bullet Context section
- [ ] All tasks have 3-5 Gherkin scenarios
- [ ] All tasks have 3-5 validation commands
- [ ] All tasks have precise SourceRefs (section + subsection + line range)

### File Completeness
- [ ] All 19 epic files exist (02-foundation through 19-security)
- [ ] All files follow consistent format (template from PRD-0001)
- [ ] All files include epic completion checklist

### Quality Gates
- [ ] Event names consistent with universe Appendix A
- [ ] API endpoints consistent with universe Appendix B
- [ ] Status enums consistent with universe Appendix C
- [ ] No dependency cycles
- [ ] Critical path validated

### Developer Readiness
- [ ] Developers can execute tasks without re-reading universe
- [ ] QA can write test cases from acceptance criteria
- [ ] DevOps can verify completion with validation commands
- [ ] Project manager can track progress with master index

---

## Conclusion

The initial task plan provided a strong **structure and framework** but lacked the **detailed specifications** needed for execution. This corrective action plan provides a clear path to completion.

**Recommended Action:** Proceed with **Option 3 (Hybrid)** approach:
- 6 weeks to complete Phase 1.5 + Phase 2 specifications
- Development starts Week 5 (Foundation + Valuation ready)
- Phase 3-4 specifications completed in parallel with Phase 1.5 development

**Estimated Completion:** 6 weeks for production-ready task plan (Phase 1.5 + Phase 2)

---

**Status:** Corrective action plan approved, ready for execution  
**Next Step:** Begin Week 1 work (Foundation tasks PRD-0002 to PRD-0015)

