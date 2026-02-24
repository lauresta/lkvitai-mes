# P-05 — Cycle Count / Stock Reconciliation

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Periodically verifies physical stock quantities against system records; records and resolves discrepancies.

**Evidence:**
- UI: `CycleCounts/List.razor`, `CycleCounts/Schedule.razor`, `CycleCounts/Execute.razor`, `CycleCounts/Discrepancies.razor`
- Controller: `CycleCountsController` (`api/warehouse/v1/cycle-counts`)
- Commands: `ScheduleCycleCountCommand`, `PerformCycleCountCommand`, `CompleteCycleCountCommand`
- Service: `CycleCountServices.cs` — `MartenCycleCountQuantityResolver`

---

## Trigger

Scheduled cycle count (by manager or Hangfire job) or ad-hoc physical count request.

## Outcomes

- Physical count submitted for one or more locations
- Discrepancies identified and reviewed
- Approved discrepancies generate `RecordStockMovementCommand` (ADJUSTMENT) to correct the ledger
- Cycle count completed and archived

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Operator | Executes physical count (scans locations) |
| Warehouse Manager | Schedules counts, approves discrepancy corrections |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/cycle-counts` | `CycleCounts/List.razor` | Operations → Cycle Counts |
| `/warehouse/cycle-counts/schedule` | `CycleCounts/Schedule.razor` | Operations → Cycle Counts → Schedule |
| `/warehouse/cycle-counts/{Id}/execute` | `CycleCounts/Execute.razor` | Operations → Cycle Counts → Execute |
| `/warehouse/cycle-counts/{Id}/discrepancies` | `CycleCounts/Discrepancies.razor` | Operations → Cycle Counts → Discrepancies |

## Subprocesses Used

- SP-04 Reason Code Selection (for adjustment reason)
- SP-06 Stock Adjustment Recording (for approved discrepancies)
- SP-10 Warehouse Location Lookup

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/cycle-counts` | CycleCountsController | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts` | CycleCountsController | ManagerOrAdmin |
| GET | `api/warehouse/v1/cycle-counts/{id}` | CycleCountsController | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts/{id}/count-items` | CycleCountsController | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts/{id}/complete` | CycleCountsController | ManagerOrAdmin |

## Key Domain Objects

`CycleCount`, `CycleCountItem`, `LocationBalance`, `StockMovement` (type=ADJUSTMENT)

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
