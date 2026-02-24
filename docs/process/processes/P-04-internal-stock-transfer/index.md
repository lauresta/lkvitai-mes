# P-04 — Internal Stock Transfer

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Moves physical stock between two warehouse locations (zone-to-zone, bin-to-bin, or logical warehouse).

**Evidence:**
- UI: `Transfers/List.razor`, `Transfers/Create.razor`, `Transfers/Execute.razor`
- Controller: `TransfersController` (`api/warehouse/v1/transfers`)
- Service: `TransferServices.cs` (43 KB) — `CreateTransferCommandHandler`, `ExecuteTransferCommandHandler`, `MartenTransferStockAvailabilityService`

---

## Trigger

Operator or manager initiates a transfer request; or triggered by putaway/replenishment rules.

## Outcomes

- Transfer executed and confirmed
- `StockMoved` (TRANSFER, FROM → TO) appended to StockLedger
- `LocationBalanceView` updated for both locations (≤5 s)

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Operator | Executes approved transfer |
| Warehouse Manager | Creates and approves transfer request |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/transfers` | `Transfers/List.razor` | Operations → Transfers |
| `/warehouse/transfers/create` | `Transfers/Create.razor` | Operations → Transfers → Create |
| `/warehouse/transfers/{Id}/execute` | `Transfers/Execute.razor` | Operations → Transfers → Execute |

## Subprocesses Used

- SP-03 Approval Workflow (optional, configurable)
- SP-05 Handling Unit Lifecycle
- SP-10 Warehouse Location Lookup

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| POST | `api/warehouse/v1/transfers` | TransfersController | ManagerOrAdmin |
| GET | `api/warehouse/v1/transfers` | TransfersController | OperatorOrAbove |
| GET | `api/warehouse/v1/transfers/{id}` | TransfersController | OperatorOrAbove |
| POST | `api/warehouse/v1/transfers/{id}/execute` | TransfersController | OperatorOrAbove |

## Key Domain Objects

`Transfer`, `HandlingUnit`, `WarehouseLocation`, `StockMovement` (type=TRANSFER)

## Architectural Notes

- Decision 3: Offline operation permitted for assigned HUs
- Multi-step approval workflow via `ApprovalRuleService.cs`

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
