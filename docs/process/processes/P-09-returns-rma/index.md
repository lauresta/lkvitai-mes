# P-09 — Returns / RMA

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Phase 1 scaffolded

---

## Summary

Processes customer returns: receives returned goods, inspects condition via QC, then restocks or scraps as appropriate.

**Evidence:**
- UI: `Rmas.razor` (`/warehouse/rmas`)
- Controller: `AdvancedWarehouseController` (RmaController) — `api/warehouse/v1/rma`
- Typed client: `AdvancedWarehouseClient`
- QC advanced: `api/warehouse/v1/qc-advanced`

---

## Trigger

Customer initiates return; RMA record created in system.

## Outcomes

- RMA record created and tracked
- Returned goods physically received
- QC inspection performed
- Stock reinstated (`StockMoved` RECEIPT) or written off (`StockMoved` SCRAP)

## Actors

| Role | Responsibility |
|------|---------------|
| Returns/RMA Clerk | Creates RMA, receives goods |
| QC Inspector | Inspects returned goods |
| Warehouse Manager | Approves restock or scrap decision |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/rmas` | `Rmas.razor` | Outbound → RMAs |

## Subprocesses Used

- SP-01 QC Inspection & Disposition
- SP-06 Stock Adjustment Recording (for scrap)

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET/POST | `api/warehouse/v1/rma` | AdvancedWarehouseController | — (auth TBD — U-02) |
| POST | `api/warehouse/v1/qc-advanced` | AdvancedWarehouseController | — |

## Key Domain Objects

`RMA`, `ReturnedGoods`, `QcInspection`, `StockMovement` (type=RECEIPT or SCRAP)

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
