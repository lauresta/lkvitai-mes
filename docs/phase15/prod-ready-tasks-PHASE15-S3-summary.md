# Phase 1.5 Sprint 3 - Task Summary

**Sprint Goal:** Complete operational UI gaps and fix critical validation/auth issues to enable end-to-end operator workflows.

**Total Tasks:** 20
**Estimated Effort:** 17 days
**Status:** Ready for Execution

---

## Task Overview

| TaskId | Title | Epic | Est | Owner | Critical? |
|--------|-------|------|-----|-------|-----------|
| PRD-1521 | Local Dev Auth & 403 Fix | Foundation | M | Infra | ⭐ CRITICAL |
| PRD-1522 | Data Model Type Alignment (Guid→int) | Foundation | M | Backend | ⭐ CRITICAL |
| PRD-1523 | Receiving Invoice Entry UI | Inbound | L | UI | ⭐ CRITICAL |
| PRD-1524 | Receiving Scan & QC Workflow UI | Inbound | L | UI | ⭐ CRITICAL |
| PRD-1525 | Stock Visibility Dashboard UI | Stock | M | UI | High |
| PRD-1526 | Stock Movement/Transfer UI | Stock | M | UI | High |
| PRD-1527 | Create Sales Order UI | Sales Orders | L | UI | ⭐ CRITICAL |
| PRD-1528 | Sales Order List & Detail UI | Sales Orders | M | UI | ⭐ CRITICAL |
| PRD-1529 | Allocation & Release UI | Sales Orders | M | UI | ⭐ CRITICAL |
| PRD-1530 | Picking Workflow UI Enhancements | Picking | M | UI | High |
| PRD-1531 | Packing Station UI Enhancements | Packing | M | UI | High |
| PRD-1532 | Dispatch UI Enhancements | Dispatch | S | UI | High |
| PRD-1533 | Receiving History Report UI | Reports | S | UI | Medium |
| PRD-1534 | Dispatch History Report UI | Reports | S | UI | Medium |
| PRD-1535 | Stock Allocation Validation | Validation | M | Backend | ⭐ CRITICAL |
| PRD-1536 | Optimistic Locking for Sales Orders | Validation | S | Backend | High |
| PRD-1537 | Barcode Lookup Enhancement | Validation | S | Backend | High |
| PRD-1538 | FedEx API Integration (Real) | Integration | M | Integration | High |
| PRD-1539 | End-to-End Correlation Tracing | Observability | M | Infra | High |
| PRD-1540 | Smoke E2E Integration Tests | Testing | L | QA | High |

---

## Critical Path

**Week 1 (Days 1-5):**
- PRD-1521: Auth fix (Day 1) — BLOCKS all manual validation
- PRD-1522: Data model alignment (Day 1) — BLOCKS all FK operations
- PRD-1523: Receiving invoice UI (Days 2-3)
- PRD-1527: Create Sales Order UI (Days 2-3, parallel)
- PRD-1535: Stock allocation validation (Day 4)

**Week 2 (Days 6-10):**
- PRD-1524: Receiving scan & QC UI (Days 6-7)
- PRD-1528: Sales Order list/detail UI (Day 6)
- PRD-1529: Allocation & release UI (Day 7)
- PRD-1530-1532: Picking/Packing/Dispatch UI (Days 8-9)
- PRD-1540: E2E integration tests (Days 9-10)

**Deferred to Week 3 (if overflow):**
- PRD-1525-1526: Stock UI (can run in parallel with Week 2)
- PRD-1533-1534: Reports UI
- PRD-1536-1539: Non-critical enhancements

---

## Dependencies

**External Dependencies:**
- None (all work self-contained)

**Internal Dependencies:**
- PRD-1523 → PRD-1524 (Receiving UI → Scan/QC UI)
- PRD-1527 → PRD-1528,1529 (Create SO → List/Detail/Allocation)
- PRD-1521 → All manual validation steps (auth required)
- PRD-1522 → All FK-dependent operations (data model fix)

**Blocking Issues:**
- None known (all PRD-1501 to PRD-1520 from Sprint 1+2 complete)

---

## Key Deliverables

### 1. Operator Workflow UI (CRITICAL)
- **Inbound:** Invoice entry + receiving + QC panels
- **Stock:** Dashboard + movement/transfer
- **Sales Orders:** Create + List/Detail + Allocation/Release
- **Picking/Packing/Dispatch:** Enhanced with barcode scanning

### 2. Auth & Validation Fixes (CRITICAL)
- **Auth:** Dev token endpoint, documented curl workflow
- **Data Model:** All Guid→int inconsistencies resolved
- **Validation:** Stock allocation checks, optimistic locking, barcode lookup

### 3. Integration & Observability
- **FedEx API:** Real integration (not stub)
- **Tracing:** Correlation IDs flow through all requests
- **Tests:** Smoke E2E integration tests

### 4. Reports
- **Receiving History:** Report UI with CSV export
- **Dispatch History:** Report UI with CSV export

---

## Success Criteria (Sprint 3)

At the end of this sprint, an operator MUST be able to:

1. ✅ Obtain dev auth token and execute all documented API commands without 403 errors
2. ✅ Create inbound shipment via UI (invoice-like entry form)
3. ✅ Receive goods via UI with barcode scan + QC gate
4. ✅ View stock dashboard with on-hand balances by location
5. ✅ Move stock between locations via transfer UI
6. ✅ Create Sales Order via UI with customer/items
7. ✅ Allocate stock to sales order (auto or manual)
8. ✅ Release sales order to picking via UI
9. ✅ Pick, pack, dispatch orders via UI
10. ✅ View receiving and dispatch history reports

---

## Risks & Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| UI development slower than estimated | Sprint incomplete | Medium | Parallelize UI tasks across 2 devs |
| Auth fix breaks production security | Critical security issue | Low | Strict environment check, code review mandatory |
| Data migration loses data | Data loss | Low | Test on dev DB first, backup prod |
| FedEx API credentials unavailable | Integration blocked | Medium | Use FedEx test env, fallback to manual |
| Barcode scanner hardware unavailable | Cannot test scanning | Medium | Use keyboard fallback, document shortcuts |

---

## Team Allocation (Recommended)

**If 1 Developer:**
- Week 1: Focus on critical path (PRD-1521, 1522, 1523, 1527, 1535)
- Week 2: UI completeness (PRD-1524, 1528-1532)
- Week 3 (overflow): Reports + enhancements (PRD-1533-1540)

**If 2 Developers:**
- Dev 1: UI tasks (PRD-1523, 1524, 1527-1534)
- Dev 2: Backend/Infra tasks (PRD-1521, 1522, 1535-1540)
- Result: Complete in 2 weeks (no overflow)

---

## Testing Strategy

**Manual Testing Priority:**
1. Auth flow (PRD-1521) — verify dev token works
2. Inbound UI (PRD-1523, 1524) — create shipment → receive → QC
3. Sales Order UI (PRD-1527-1529) — create → allocate → release
4. Picking/Packing/Dispatch (PRD-1530-1532) — full outbound flow

**Integration Testing:**
- PRD-1540: Smoke E2E tests covering critical workflows

**Validation:**
- All tasks include validation/checks section with exact curl commands
- All tasks require manual test pass before completion

---

## Notes

1. **UI Technology:** All UI tasks use Blazor Server (`src/LKvitai.MES.WebUI`), NOT React
2. **Auth:** Dev token required for all manual validation steps (PRD-1521)
3. **Data Types:** All master data FKs use `int` after PRD-1522
4. **Barcode Scanning:** Keyboard wedge pattern (focus trap + Enter)
5. **Error Handling:** Inline validation + toast notifications mandatory
6. **Audit:** All write operations log user, timestamp, correlation ID
7. **Responsive:** All UI pages must work on tablet (768px+)
8. **Documentation:** Update `docs/dev-auth-guide.md` with all new endpoints

---

## Files Created

- `prod-ready-tasks-PHASE15-S3.md` — Full task details (all 20 tasks)
- `prod-ready-tasks-PHASE15-S3-summary.md` — This summary file

---

**Next Action:** Begin Sprint 3 execution with PRD-1521 (Auth fix)
