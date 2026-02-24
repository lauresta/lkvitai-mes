# P-06 — Stock Adjustments & Write-offs

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Applies manual quantity corrections for damage, loss, found-stock, or discrepancies not covered by formal cycle counts.

**Evidence:**
- UI: `StockAdjustments.razor` (`/warehouse/stock/adjustments`)
- Controller: `AdjustmentsController` (`api/warehouse/v1/adjustments`)
- Service: `ReasonCodeService.cs`

---

## Trigger

Manager identifies a physical vs system quantity mismatch, or a damage/scrap event.

## Outcomes

- `StockMoved` (ADJUSTMENT or SCRAP) appended to StockLedger with reason code
- `LocationBalanceView` and `AvailableStockView` updated (≤5 s)

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Manager | Initiates and approves all adjustments |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/stock/adjustments` | `StockAdjustments.razor` | Stock → Adjustments |

## Subprocesses Used

- SP-04 Reason Code Selection
- SP-06 Stock Adjustment Recording

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| POST | `api/warehouse/v1/adjustments` | AdjustmentsController | ManagerOrAdmin |
| GET | `api/warehouse/v1/adjustments/history` | AdjustmentsController | Authenticated |

## Key Domain Objects

`StockMovement` (type=ADJUSTMENT or SCRAP), `ReasonCode`, `LocationBalance`

## Architectural Notes

- Decision 1: Only StockLedger writes stock events — all adjustments go through `RecordStockMovementCommand`
- Negative balance guard: `StockLedger.RecordMovement()` throws `InsufficientBalanceException` if adjustment would result in negative stock

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
