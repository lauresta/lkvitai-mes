# Supplementary Review: master-data-universe.md

## POSITIVE FINDINGS

### Strong Alignment with Baseline

**Epic Structure**: Universe.md provides excellent high-level organization that matches baseline architecture:
- Epic 0 (Projection Fix) correctly prioritized as CRITICAL/BLOCKING
- Epic 1-7 (Backend workflows) match baseline implementation sequence
- Epic 8-10 (UI) properly separated from backend logic
- Dependencies graph accurately shows critical path

**Key Decisions Section**: Comprehensive and matches baseline on all 30 numbered decisions:
- Architectural: Schema separation, consistency model, import-first strategy ✓
- Data model: InternalSKU format, BaseUoM policy, virtual locations ✓
- Workflow: Barcode scanning, manual location selection, reservations ✓
- Technical: Zero-downtime rebuild, optimistic concurrency, ProblemDetails ✓
- Performance: All targets match baseline (5 min import, <1 sec projection lag) ✓

**Deferred Items**: Properly categorized Phase 2+ scope:
- Serial tracking, HU hierarchy, wave/zone picking, ASN, label printing ✓
- Out of scope: RFID, robotics, blockchain, voice picking ✓

**Invariants**: Critical constraints correctly identified:
- Barcode uniqueness, InternalSKU immutability, event immutability ✓
- Virtual locations undeletable, lot tracking enforcement ✓

## ISSUES REQUIRING PATCHES

### ISSUE 1: Timeline Discrepancy (Week 4 Projection Fix)
**Severity**: MAJOR  
**Location**: Timeline Summary section  
**Problem**: Says "Week 4: Fix projections + implement event store (CRITICAL)" but baseline mandates projection fix in Week 1-2 BEFORE event store work.  

**Current Text**:
```
**Week 1-2**: Foundation (EF Core model, seed data, Blazor structure)
**Week 3**: Import APIs & validation
**Week 4**: Fix projections + implement event store (CRITICAL)
```

**Should Be**:
```
**Week 1-2**: Fix projections (CRITICAL) + Foundation (EF Core model, seed data)
**Week 3**: Import APIs & validation (can parallelize with Week 4)
**Week 4**: Event store & projections (depends on Week 1-2 projection fix)
```

**Rationale**: Tasks.md Epic 0 and implementation-order.md both correctly allocate Week 1-2 for projection fix. Universe.md timeline summary conflicts with this.

---

### ISSUE 2: Event Contracts Not Listed
**Severity**: MINOR  
**Location**: Epic 3.1  
**Problem**: Says "Event contracts (GoodsReceived, StockMoved, PickCompleted, StockAdjusted, Reservations, QC)" which is vague and incomplete (only 6 mentioned, baseline has 8).

**Current Text**:
```
3.1 Event contracts (GoodsReceived, StockMoved, PickCompleted, StockAdjusted, Reservations, QC)
```

**Should Be**:
```
3.1 Event contracts: 8 types - GoodsReceived, StockMoved, PickCompleted, StockAdjusted, ReservationCreated, ReservationReleased, QCPassed, QCFailed (see master-data-03-events-and-projections.md for schemas)
```

**Rationale**: Matches baseline and provides exact count + reference to detailed spec.

---

### ISSUE 3: Epic 8, 9, 10 Features Good but Tasks Missing
**Severity**: INFO (already covered in main review)  
**Location**: Epic 8, 9, 10 sections  
**Finding**: Features correctly listed (8.1-8.6, 9.1-9.5, 10.1-10.5) but tasks.md doesn't have corresponding tasks. This is already flagged as BLOCKER-1, BLOCKER-2, BLOCKER-3 in main review.

**No patch needed here** - fixes go in tasks.md, not universe.md.

---

### ISSUE 4: Authorization Not in Key Decisions
**Severity**: MINOR  
**Location**: Key Decisions & Invariants section  
**Problem**: No mention of role-based authorization (WarehouseAdmin, WarehouseManager, WarehouseOperator) despite being critical for API security.

**Patch**: Add to Technical Decisions (after #24):
```
25. **Authorization**: Role-based access control (WarehouseAdmin, WarehouseManager, WarehouseOperator, QCInspector) with JWT authentication
26. **Error Handling**: RFC 7807 ProblemDetails with traceId for all API errors (already present as #22, renumber if adding)
```

---

## VERIFICATION CHECKLIST

### ✅ Correct in Universe.md
- [x] Epic 0 marked as CRITICAL/BLOCKING
- [x] Schema separation documented (public vs warehouse_events)
- [x] All 15 entity types identified in Epic 1
- [x] Import strategy matches baseline (Excel templates, validation, upsert)
- [x] 6 projections listed in Epic 3 (AvailableStock, LocationBalance, etc.)
- [x] Virtual locations listed (7 mandatory)
- [x] Performance targets match baseline exactly
- [x] Deferred items properly scoped (Phase 2+)
- [x] Dependencies graph shows critical path correctly

### ⚠️ Needs Minor Corrections
- [ ] Timeline: Move projection fix to Week 1-2 (not Week 4)
- [ ] Epic 3.1: List all 8 event types explicitly
- [ ] Key Decisions: Add authorization and error handling

### ❌ Missing (but covered in other docs)
- Tasks for Epic 8 (Admin UI) → add to tasks.md
- Tasks for Epic 9 (Reports) → add to tasks.md
- Tasks for Epic 10 (Operational UI) → add to tasks.md

---

## RECOMMENDED PATCHES FOR UNIVERSE.MD

### PATCH 1: Fix Timeline Summary
```
DOC: master-data-universe.md
SECTION: Timeline Summary

Replace:
"**Week 1-2**: Foundation (EF Core model, seed data, Blazor structure)
**Week 3**: Import APIs & validation
**Week 4**: Fix projections + implement event store (CRITICAL)"

With:
"**Week 1-2**: Fix projections (CRITICAL - Epic 0) + Foundation (Epic 1: EF Core model, seed data, Blazor structure)
**Week 3**: Import APIs & validation (Epic 2, can parallelize with Week 4)
**Week 4**: Event store & projections (Epic 3, depends on Week 1-2 projection fix complete)"
```

---

### PATCH 2: Fix Epic 3.1 Event List
```
DOC: master-data-universe.md
SECTION: Epic 3: Event Store & Projections

Replace:
"3.1 Event contracts (GoodsReceived, StockMoved, PickCompleted, StockAdjusted, Reservations, QC)"

With:
"3.1 Event contracts: 8 types - GoodsReceived, StockMoved, PickCompleted, StockAdjusted, ReservationCreated, ReservationReleased, QCPassed, QCFailed (ref: master-data-03-events-and-projections.md)"
```

---

### PATCH 3: Add Authorization to Key Decisions
```
DOC: master-data-universe.md
SECTION: Key Decisions & Invariants > Technical Decisions

Add after decision #24 (Testing):
"25. **Authorization**: Role-based access control with 4 roles (WarehouseAdmin, WarehouseManager, WarehouseOperator, QCInspector), JWT bearer authentication, [Authorize] attributes on controllers"
```

---

## OVERALL ASSESSMENT OF UNIVERSE.MD

**Grade**: A- (90%)

**Strengths**:
- Excellent high-level organization and epic structure
- Comprehensive key decisions section (30 numbered decisions)
- Dependencies graph clearly shows critical path
- Deferred items properly scoped to Phase 2+
- Success criteria and performance targets match baseline exactly
- Invariants section captures critical constraints well

**Weaknesses**:
- Timeline summary conflicts with Epic 0 priority (Week 4 vs Week 1-2)
- Event contracts list incomplete/vague (6 mentioned vs 8 actual)
- Authorization not mentioned in key decisions
- No reference to how Epics 8, 9, 10 map to tasks (but this is tasks.md issue, not universe.md)

**Recommendation**: Apply 3 patches above, then universe.md is ready for use as project reference document. Main implementation gaps are in tasks.md (missing Epic 8, 9, 10, 11 tasks), not in universe.md.

---

## FINAL VERDICT

**Universe.md aligns 90% with baseline.** Main issue is timeline discrepancy (projection fix in Week 4 instead of Week 1-2). Apply patches and it's production-ready as a high-level project map.

**Critical finding**: Universe.md correctly identifies all features and decisions, but tasks.md is missing ~25 days of implementation tasks (Epics 8, 9, 10, 11). This was already flagged in main review as BLOCKERS 1-4.
