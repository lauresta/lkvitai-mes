# P-03 — Outbound Order Fulfillment

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Processes a customer sales order from creation through stock allocation (SOFT lock), picking (HARD lock), packing, and final dispatch.

**Evidence:**
- UI: 11 routes — `SalesOrders.razor`, `AllocationDashboard.razor`, `PickingTasks.razor`, `WavePicking.razor`, `PackingStation.razor`, `OutboundDispatch.razor`, `Labels.razor`, etc.
- Controllers: `SalesOrdersController`, `ReservationsController`, `PickingController`, `ShipmentsController`, `AdvancedWarehouseController` (waves)
- Commands: `AllocateReservationCommand`, `StartPickingCommand`, `PickStockCommand`
- Services: `SalesOrderCommandHandlers.cs`, `ShipmentCommandHandlers.cs`, `OutboundOrderCommandHandlers.cs`

---

## Trigger

Sales order created manually in UI or from ERP.

## Outcomes

- Sales order dispatched; shipment confirmed
- HARD reservation consumed; `StockMoved` (PICK) events recorded
- HU lines updated; stock decremented in all read models

## Actors

| Role | Responsibility |
|------|---------------|
| Sales Admin | Creates sales orders |
| Warehouse Manager | Releases orders for picking; approves allocation |
| Picking Operator | Executes picking tasks |
| Packing Operator | Packs and labels outbound HUs |
| Dispatch Clerk | Confirms final dispatch |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/sales/orders` | `SalesOrders.razor` | Outbound → Sales Orders |
| `/warehouse/sales/allocations` | `AllocationDashboard.razor` | Outbound → Allocations |
| `/warehouse/waves` | `WavePicking.razor` | Outbound → Wave Picking |
| `/warehouse/picking/tasks` | `PickingTasks.razor` | Outbound → Picking Tasks |
| `/warehouse/outbound/pack/{OrderId}` | `PackingStation.razor` | Outbound → (from order detail) |
| `/warehouse/outbound/dispatch` | `OutboundDispatch.razor` | Outbound → Dispatch |
| `/warehouse/labels` | `Labels.razor` | Outbound → Labels |

## Subprocesses Used

- SP-02 Reservation Lifecycle (SOFT → HARD)
- SP-05 Handling Unit Lifecycle
- SP-07 Label Printing
- SP-08 Wave / Batch Picking

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET/POST | `api/warehouse/v1/sales-orders` | SalesOrdersController | SalesAdminOrManager |
| POST | `api/warehouse/v1/sales-orders/{id}/reserve` | SalesOrdersController | SalesAdminOrManager |
| GET | `api/warehouse/v1/reservations` | ReservationsController | OperatorOrAbove |
| POST | `api/warehouse/v1/reservations/{id}/start-picking` | ReservationsController | OperatorOrAbove |
| POST | `api/warehouse/v1/reservations/{id}/pick` | ReservationsController | OperatorOrAbove |
| GET/POST | `api/warehouse/v1/waves` | AdvancedWarehouseController | — |
| POST | `api/warehouse/v1/picking/tasks/{id}/complete` | PickingController | OperatorOrAbove |
| POST | `api/warehouse/v1/shipments/{id}/dispatch` | ShipmentsController | DispatchClerkOrManager |

## Key Domain Objects

`SalesOrder`, `Reservation` (SOFT/HARD), `PickingTask`, `Wave`, `HandlingUnit`, `Shipment`, `StockMovement` (type=PICK)

## Architectural Notes

- Decision 1: Only StockLedger writes `StockMoved` events
- Decision 2: StockMovement commits BEFORE HU/Reservation updates (ledger-first)
- Decision 4: HARD locks cannot be bumped

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
