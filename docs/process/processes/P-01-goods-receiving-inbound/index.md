# P-01 — Goods Receiving (Inbound)

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Accepts incoming goods from suppliers, records them into the warehouse stock ledger via Handling Units, and gates them through QC inspection.

**Evidence:**
- UI: `InboundShipments.razor`, `InboundShipmentCreate.razor`, `InboundShipmentDetail.razor`, `ReceivingQc.razor`
- Controller: `ReceivingController` (`api/warehouse/v1/receiving/shipments`), `QCController` (`api/warehouse/v1/qc`)
- Command: `ReceiveGoodsCommand` → `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/ReceiveGoodsCommand.cs`

---

## Trigger

- Inbound shipment manually created in UI (`/warehouse/inbound/shipments/create`)
- OR ERP/Kafka `MaterialRequested` event received (anti-corruption layer in `Modules.Warehouse.Integration/`)

## Outcomes

- Inbound shipment status = Received
- QC inspection passed / rejected
- `StockMoved` event (RECEIPT) recorded in StockLedger
- Handling Unit created, sealed, with barcode
- Stock visible in `LocationBalanceView` and `AvailableStockView` (≤5 s lag)

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Operator | Scans goods, creates HU |
| QC Inspector | Inspects, approves/rejects |
| Warehouse Manager | Approves shipment creation |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/inbound/shipments` | `InboundShipments.razor` | Inbound → Inbound Shipments |
| `/warehouse/inbound/shipments/create` | `InboundShipmentCreate.razor` | Inbound → Inbound Shipments → Create |
| `/warehouse/inbound/shipments/{Id:int}` | `InboundShipmentDetail.razor` | Inbound → Detail |
| `/warehouse/inbound/qc` | `ReceivingQc.razor` | Inbound → Receiving QC |

## Subprocesses Used

- SP-01 QC Inspection & Disposition
- SP-05 Handling Unit Lifecycle
- SP-07 Label Printing
- SP-09 Lot / Batch Assignment
- SP-14 Barcode Lookup

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/receiving/shipments` | ReceivingController | OperatorOrAbove |
| POST | `api/warehouse/v1/receiving/shipments` | ReceivingController | QcOrManager |
| POST | `api/warehouse/v1/receiving/shipments/{id}/receive` | ReceivingController | QcOrManager |
| POST | `api/warehouse/v1/receiving/shipments/{id}/receive-items` | ReceivingController | QcOrManager |
| GET | `api/warehouse/v1/qc/inspections` | QCController | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections/{id}/approve` | QCController | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections/{id}/reject` | QCController | QcOrManager |

## Key Domain Objects

`InboundShipment`, `HandlingUnit`, `StockMovement` (type=RECEIPT), `QcInspection`, `Lot`

## Architectural Notes

- Decision 1: Only StockLedger writes `StockMoved` events — ReceiveGoods must go through `ReceiveGoodsCommand`
- HU sealed atomically with stock movement (Package E)

## Files

- [`bpmn.md`](bpmn.md) — Process flow diagram (TODO)
- [`scenarios.md`](scenarios.md) — Happy path + edge cases (TODO)
- [`test-data.md`](test-data.md) — Test fixtures (TODO)
- [`traceability.md`](traceability.md) — Requirement traceability (TODO)
