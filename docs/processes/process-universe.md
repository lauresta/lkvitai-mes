# LKvitai.MES Warehouse — Process Universe

**Document type:** Business Process Map (Phase 1)
**Status:** Repository-grounded discovery; BPMN expansion is Phase 2
**Branch:** `docs/process-universe`
**Date:** 2026-02-24
**Methodology:** Every entry is backed by at least one file path + route or controller reference. Anything not provable from the repo is listed under "Unknown / Not implemented yet".

---

## How to read this document

| Section | Purpose |
|---------|---------|
| [1. Process Map](#1-process-map) | One-glance list of all top-level processes |
| [2. Top-Level Process Descriptions](#2-top-level-process-descriptions) | Per-process summary, actors, UI, APIs, subprocesses |
| [3. Subprocess Library](#3-subprocess-library) | Reusable subprocesses and which top-level processes depend on them |
| [Appendix A – UI Route Index](#appendix-a--ui-route-index) | route → file path |
| [Appendix B – API Route Index](#appendix-b--api-route-index) | HTTP method + route → controller |
| [Appendix C – Nav Menu Index](#appendix-c--nav-menu-index) | Nav group → items → routes |
| [Appendix D – Unknown / Not Implemented](#appendix-d--unknown--not-implemented-yet) | Gaps not provable from repo |

---

## 1. Process Map

| # | Top-Level Process | Nav Group | Core Domain |
|---|---|---|---|
| **P-01** | [Goods Receiving (Inbound)](#p-01-goods-receiving-inbound) | Inbound | Receiving, QC, HU, StockLedger |
| **P-02** | [Putaway & Location Assignment](#p-02-putaway--location-assignment) | Inbound | HU, WarehouseLayout |
| **P-03** | [Outbound Order Fulfillment](#p-03-outbound-order-fulfillment) | Outbound | SalesOrder, Reservation, Picking, HU, StockLedger |
| **P-04** | [Internal Stock Transfer](#p-04-internal-stock-transfer) | Operations | StockLedger, HU, WarehouseLayout |
| **P-05** | [Cycle Count / Stock Reconciliation](#p-05-cycle-count--stock-reconciliation) | Operations | StockLedger, LocationBalance |
| **P-06** | [Stock Adjustments & Write-offs](#p-06-stock-adjustments--write-offs) | Stock | StockLedger |
| **P-07** | [Inventory Valuation & Costing](#p-07-inventory-valuation--costing) | Finance | Valuation, StockLedger |
| **P-08** | [Agnum Integration & Reconciliation](#p-08-agnum-integration--reconciliation) | Finance | Valuation, AgnumExport |
| **P-09** | [Returns / RMA](#p-09-returns--rma) | Outbound | StockLedger, HU, SalesOrder |
| **P-10** | [Cross-Dock Operations](#p-10-cross-dock-operations) | Outbound | StockLedger, HU |
| **P-11** | [Lot & Serial Number Traceability](#p-11-lot--serial-number-traceability) | Reports/Compliance | Lots, Serials, StockLedger |
| **P-12** | [Warehouse Visualization & Location Discovery](#p-12-warehouse-visualization--location-discovery) | Operations | WarehouseLayout |
| **P-13** | [Reporting & Analytics](#p-13-reporting--analytics) | Reports / Analytics | Read models (all) |
| **P-14** | [System Administration & Compliance](#p-14-system-administration--compliance) | Admin | Security, GDPR, Backups, DR |
| **P-15** | [Master Data Management](#p-15-master-data-management) | Admin | Items, Suppliers, Locations, UoM |

---

## 2. Top-Level Process Descriptions

---

### P-01 Goods Receiving (Inbound)

**Purpose:** Accept incoming goods from suppliers, record them into the warehouse stock ledger via Handling Units, and gate them through QC inspection.

**Trigger:** An inbound shipment is expected (manually created or ERP-driven via Kafka `MaterialRequested`).

**Outcomes:**
- Inbound shipment is marked received
- QC inspection passed/rejected
- `StockMoved` events recorded in StockLedger (RECEIPT movement type)
- Handling Unit created and sealed with barcode
- Stock visible in `LocationBalanceView` and `AvailableStockView`

**Primary Actors:** Warehouse Operator, QC Inspector, Warehouse Manager

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Inbound → Inbound Shipments | `/warehouse/inbound/shipments` | `InboundShipments.razor` |
| Inbound → Inbound Shipments → Create | `/warehouse/inbound/shipments/create` | `InboundShipmentCreate.razor` |
| Inbound → Inbound Shipments → Detail | `/warehouse/inbound/shipments/{Id:int}` | `InboundShipmentDetail.razor` |
| Inbound → Receiving QC | `/warehouse/inbound/qc` | `ReceivingQc.razor` |

**Key Subprocesses Used:**
- [SP-01 QC Inspection & Disposition](#sp-01-qc-inspection--disposition)
- [SP-05 Handling Unit Lifecycle](#sp-05-handling-unit-lifecycle)
- [SP-07 Label Printing](#sp-07-label-printing)
- [SP-09 Lot / Batch Assignment](#sp-09-lot--batch-assignment)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/receiving/shipments` | `ReceivingController` | OperatorOrAbove |
| GET | `api/warehouse/v1/receiving/shipments/{id:int}` | `ReceivingController` | OperatorOrAbove |
| POST | `api/warehouse/v1/receiving/shipments` | `ReceivingController` | QcOrManager |
| POST | `api/warehouse/v1/receiving/shipments/{id:int}/receive` | `ReceivingController` | QcOrManager |
| POST | `api/warehouse/v1/receiving/shipments/{id:int}/receive-items` | `ReceivingController` | QcOrManager |
| GET | `api/warehouse/v1/qc/inspections` | `QCController` | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections` | `QCController` | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections/{id:guid}/approve` | `QCController` | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections/{id:guid}/reject` | `QCController` | QcOrManager |

**Key Domain Objects:** InboundShipment, HandlingUnit, StockMovement (RECEIPT), QcInspection, Lot

**Application Commands:**
- `ReceiveGoodsCommand` → `ReceiveGoodsCommandHandler` — creates HU + records StockMovement atomically
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/ReceiveGoodsCommand.cs`

**Notes:**
- Receiving also triggered via ERP/Kafka anti-corruption layer (`MaterialRequested` → `CreateReservation`)
- Integration source: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/`

---

### P-02 Putaway & Location Assignment

**Purpose:** Move received goods from the receiving area to their designated warehouse storage locations.

**Trigger:** Inbound shipment received and QC passed; putaway task generated automatically or manually.

**Outcomes:**
- HU moved from receiving location to storage bin
- `StockMoved` event recorded (TRANSFER movement type, FROM receiving TO storage)
- `LocationBalanceView` updated for target location

**Primary Actors:** Warehouse Operator

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Inbound → Putaway | `/warehouse/putaway` | `Putaway.razor` |

**Key Subprocesses Used:**
- [SP-05 Handling Unit Lifecycle](#sp-05-handling-unit-lifecycle)
- [SP-10 Warehouse Location Lookup](#sp-10-warehouse-location-lookup)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/putaway/tasks` | `PutawayController` | OperatorOrAbove |
| POST | `api/warehouse/v1/putaway/tasks` | `PutawayController` | ManagerOrAdmin |
| POST | `api/warehouse/v1/putaway/tasks/{id:guid}/complete` | `PutawayController` | OperatorOrAbove |
| GET | `api/warehouse/v1/putaway/history` | `PutawayController` | OperatorOrAbove |

**Key Domain Objects:** PutawayTask, HandlingUnit, WarehouseLocation, StockMovement (TRANSFER)

**Notes:** `PutawayClient` typed client used in `Putaway.razor` also injects `MasterDataAdminClient` for location lookups.

---

### P-03 Outbound Order Fulfillment

**Purpose:** Process a customer sales order from creation through stock allocation, picking, packing, and final dispatch.

**Trigger:** Sales order created (manually via UI or from ERP); stock available in `AvailableStockView`.

**Outcomes:**
- Sales order dispatched and shipment confirmed
- HARD lock reservation consumed
- `StockMoved` events recorded (PICK movement type)
- HU updated; stock decremented in all read models

**Primary Actors:** Sales Admin, Warehouse Manager, Picking Operator, Packing Operator, Dispatch Clerk

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Outbound → Sales Orders | `/warehouse/sales/orders` | `SalesOrders.razor` |
| Outbound → Sales Orders → Create | `/warehouse/sales/orders/create` | `SalesOrderCreate.razor` |
| Outbound → Sales Orders → Detail | `/warehouse/sales/orders/{Id:guid}` | `SalesOrderDetail.razor` |
| Outbound → Allocations | `/warehouse/sales/allocations` | `AllocationDashboard.razor` |
| Outbound → Outbound Orders | `/warehouse/outbound/orders` | `OutboundOrders.razor` |
| Outbound → Outbound Orders → Detail | `/warehouse/outbound/orders/{Id:guid}` | `OutboundOrderDetail.razor` |
| Outbound → Picking Tasks | `/warehouse/picking/tasks` | `PickingTasks.razor` |
| Outbound → Wave Picking | `/warehouse/waves` | `WavePicking.razor` |
| Outbound → Packing Station | `/warehouse/outbound/pack/{OrderId:guid}` | `PackingStation.razor` |
| Outbound → Dispatch | `/warehouse/outbound/dispatch` | `OutboundDispatch.razor` |
| Outbound → Labels | `/warehouse/labels` | `Labels.razor` |

**Key Subprocesses Used:**
- [SP-02 Reservation Lifecycle (SOFT → HARD)](#sp-02-reservation-lifecycle-soft--hard)
- [SP-05 Handling Unit Lifecycle](#sp-05-handling-unit-lifecycle)
- [SP-07 Label Printing](#sp-07-label-printing)
- [SP-08 Wave / Batch Picking](#sp-08-wave--batch-picking)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET/POST | `api/warehouse/v1/sales-orders` | `SalesOrdersController` | SalesAdminOrManager |
| GET/PUT | `api/warehouse/v1/sales-orders/{id:int}` | `SalesOrdersController` | SalesAdminOrManager |
| POST | `api/warehouse/v1/sales-orders/{id:int}/reserve` | `SalesOrdersController` | SalesAdminOrManager |
| GET/POST | `api/warehouse/v1/outbound-orders` | `OutboundOrdersController` | SalesAdminOrManager |
| GET | `api/warehouse/v1/reservations` | `ReservationsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/reservations/{id:guid}/start-picking` | `ReservationsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/reservations/{id:guid}/pick` | `ReservationsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/picking/tasks` | `PickingController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/picking/tasks/{id:guid}/locations` | `PickingController` | OperatorOrAbove |
| POST | `api/warehouse/v1/picking/tasks/{id:guid}/complete` | `PickingController` | OperatorOrAbove |
| GET | `api/warehouse/v1/waves` | `AdvancedWarehouseController` (WavesController) | — |
| POST | `api/warehouse/v1/waves` | `AdvancedWarehouseController` (WavesController) | — |
| POST | `api/warehouse/v1/waves/{id:guid}/assign` | `AdvancedWarehouseController` (WavesController) | — |
| POST | `api/warehouse/v1/waves/{id:guid}/start` | `AdvancedWarehouseController` (WavesController) | — |
| GET/POST | `api/warehouse/v1/shipments` | `ShipmentsController` | DispatchClerkOrManager |
| POST | `api/warehouse/v1/shipments/{id:int}/dispatch` | `ShipmentsController` | DispatchClerkOrManager |
| POST | `api/warehouse/v1/labels/print` | `LabelsController` | PackingOperatorOrManager |

**Key Domain Objects:** SalesOrder, Reservation (SOFT/HARD), PickingTask, Wave, HandlingUnit, Shipment, StockMovement (PICK)

**Application Commands:**
- `AllocateReservationCommand` → `AllocateReservationCommandHandler`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/AllocateReservationCommand.cs`
- `StartPickingCommand` → `StartPickingCommandHandler`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/StartPickingCommand.cs`
- `PickStockCommand` → `PickStockCommandHandler`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/PickStockCommand.cs`

**Services:**
- `SalesOrderCommandHandlers.cs` (Api/Services — noted tech debt: belongs in Application)
- `ShipmentCommandHandlers.cs`, `OutboundOrderCommandHandlers.cs`
  - Files: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/`

**Architectural notes (mandatory decisions):**
- Decision 1: Only StockLedger writes stock movements — picking cannot bypass
- Decision 2: StockMovement commits BEFORE HU/Reservation updates (ledger-first)
- Decision 4: HARD locks cannot be bumped; SOFT locks are bumpable

---

### P-04 Internal Stock Transfer

**Purpose:** Move physical stock between two warehouse locations (zone-to-zone, bin-to-bin, or logical warehouse).

**Trigger:** Operator or manager initiates a transfer request; may also be triggered by putaway assignment or replenishment rules.

**Outcomes:**
- Transfer executed and confirmed
- `StockMoved` events recorded (TRANSFER FROM → TO)
- `LocationBalanceView` updated for both source and target locations

**Primary Actors:** Warehouse Operator, Warehouse Manager

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Operations → Transfers | `/warehouse/transfers` | `Transfers/List.razor` |
| Operations → Transfers → Create | `/warehouse/transfers/create` | `Transfers/Create.razor` |
| Operations → Transfers → Execute | `/warehouse/transfers/{Id:guid}/execute` | `Transfers/Execute.razor` |

**Key Subprocesses Used:**
- [SP-05 Handling Unit Lifecycle](#sp-05-handling-unit-lifecycle)
- [SP-10 Warehouse Location Lookup](#sp-10-warehouse-location-lookup)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| POST | `api/warehouse/v1/transfers` | `TransfersController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/transfers` | `TransfersController` | OperatorOrAbove |
| GET | `api/warehouse/v1/transfers/{id:guid}` | `TransfersController` | OperatorOrAbove |
| POST | `api/warehouse/v1/transfers/{id:guid}/execute` | `TransfersController` | OperatorOrAbove |

**Key Domain Objects:** Transfer, HandlingUnit, WarehouseLocation, StockMovement (TRANSFER)

**Application Commands:**
- `RecordStockMovementCommand` (TRANSFER type) → `RecordStockMovementCommandHandler`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/RecordStockMovementCommand.cs`

**Services:**
- `TransferServices.cs` — `CreateTransferCommandHandler`, `SubmitTransferCommandHandler`, `ApproveTransferCommandHandler`, `ExecuteTransferCommandHandler`; includes `MartenTransferStockAvailabilityService`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/TransferServices.cs`

**Architectural note:** Multi-step transfer workflow uses approval steps (see `AdminApprovalRulesController`). Offline operation is permitted for assigned HUs (Decision 3).

---

### P-05 Cycle Count / Stock Reconciliation

**Purpose:** Periodically verify physical stock quantities against system records; record and resolve discrepancies.

**Trigger:** Scheduled cycle count (by manager or Hangfire job) or ad-hoc physical count request.

**Outcomes:**
- Physical count submitted for one or more locations
- Discrepancies identified and reviewed
- Approved discrepancies create `RecordStockMovementCommand` (ADJUSTMENT type) to correct the ledger
- Cycle count completed and archived

**Primary Actors:** Warehouse Operator (counting), Warehouse Manager (scheduling, approval)

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Operations → Cycle Counts | `/warehouse/cycle-counts` | `CycleCounts/List.razor` |
| Operations → Cycle Counts → Schedule | `/warehouse/cycle-counts/schedule` | `CycleCounts/Schedule.razor` |
| Operations → Cycle Counts → Execute | `/warehouse/cycle-counts/{Id:guid}/execute` | `CycleCounts/Execute.razor` |
| Operations → Cycle Counts → Discrepancies | `/warehouse/cycle-counts/{Id:guid}/discrepancies` | `CycleCounts/Discrepancies.razor` |

**Key Subprocesses Used:**
- [SP-06 Stock Adjustment Recording](#sp-06-stock-adjustment-recording)
- [SP-10 Warehouse Location Lookup](#sp-10-warehouse-location-lookup)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/cycle-counts` | `CycleCountsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts` | `CycleCountsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/cycle-counts/{id:guid}` | `CycleCountsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts/{id:guid}/count-items` | `CycleCountsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts/{id:guid}/complete` | `CycleCountsController` | ManagerOrAdmin |

**Key Domain Objects:** CycleCount, CycleCountItem, LocationBalance, StockMovement (ADJUSTMENT)

**Application Commands:**
- `ScheduleCycleCountCommand`, `PerformCycleCountCommand`, `CompleteCycleCountCommand`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/CycleCountCommands.cs`

**Services:**
- `CycleCountServices.cs` — `ScheduleCycleCountCommandHandler`, `CompleteCycleCountCommandHandler`, `MartenCycleCountQuantityResolver`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/CycleCountServices.cs`

---

### P-06 Stock Adjustments & Write-offs

**Purpose:** Apply manual quantity corrections for damage, loss, found-stock, or other discrepancies not covered by cycle counts.

**Trigger:** Operator or manager identifies a physical vs system quantity mismatch outside of a formal cycle count; damage/scrap event.

**Outcomes:**
- `StockMoved` event recorded (ADJUSTMENT or SCRAP movement type)
- Reason code attached to the movement
- Stock balances updated in `LocationBalanceView` and `AvailableStockView`

**Primary Actors:** Warehouse Manager (initiates and approves)

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Stock → Adjustments | `/warehouse/stock/adjustments` | `StockAdjustments.razor` |

**Key Subprocesses Used:**
- [SP-06 Stock Adjustment Recording](#sp-06-stock-adjustment-recording)
- [SP-04 Reason Code Selection](#sp-04-reason-code-selection)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| POST | `api/warehouse/v1/adjustments` | `AdjustmentsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/adjustments/history` | `AdjustmentsController` | (no policy shown — defaults to Authenticated) |

**Key Domain Objects:** StockMovement (ADJUSTMENT/SCRAP), ReasonCode, LocationBalance

**Application Commands:**
- `RecordStockMovementCommand` (ADJUSTMENT type) → `RecordStockMovementCommandHandler`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/RecordStockMovementCommand.cs`

**Services:**
- `ReasonCodeService.cs` — manages reason codes for adjustment classification
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/ReasonCodeService.cs`

---

### P-07 Inventory Valuation & Costing

**Purpose:** Maintain the financial valuation of warehouse stock — revalue items, apply landed costs, record write-downs, and produce valuation summaries.

**Trigger:**
- Goods received (initial valuation event)
- Manual cost adjustment by Inventory Accountant
- Landed cost receipt (freight, duties, etc.)
- Write-down decision (damage, obsolescence)

**Outcomes:**
- `CostAdjusted`, `LandedCostAllocated`, or `StockWrittenDown` events recorded in Valuation aggregate
- `OnHandValueView` updated
- Valuation summary available for Agnum export

**Primary Actors:** Inventory Accountant, CFO

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Finance → Valuation | `/warehouse/valuation/dashboard` | `Valuation/Dashboard.razor` |
| Finance → Valuation → Adjust Cost | `/warehouse/valuation/adjust-cost` | `Valuation/AdjustCost.razor` |
| Finance → Valuation → Apply Landed Cost | `/warehouse/valuation/apply-landed-cost` | `Valuation/ApplyLandedCost.razor` |
| Finance → Valuation → Write Down | `/warehouse/valuation/write-down` | `Valuation/WriteDown.razor` |

**Key Subprocesses Used:** None (self-contained financial process)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/valuation/summary` | `ValuationController` | CfoOrAdmin |
| GET | `api/warehouse/v1/valuation/by-location` | `ValuationController` | CfoOrAdmin |
| POST | `api/warehouse/v1/valuation/revalue` | `ValuationController` | CfoOrAdmin |

**Key Domain Objects:** Valuation (event-sourced), CostAdjustment, LandedCost, WriteDown

**Application Commands:**
- Commands in `ValuationCommands.cs`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/ValuationCommands.cs`

**Services:**
- `ValuationCommandHandlers.cs`, `ValuationLifecycleCommandHandlers.cs` (tech debt: belongs in Application)
- `LandedCostAllocationService.cs`, `CostAdjustmentRules.cs`, `ValuationCostAdjustmentPolicy.cs`, `ValuationWriteDownPolicy.cs`
  - Files: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/`

**ADR reference:** `docs/adr/ADR-002-valuation-event-sourcing.md`

---

### P-08 Agnum Integration & Reconciliation

**Purpose:** Export the daily stock valuation snapshot to the Agnum accounting system and reconcile discrepancies.

**Trigger:** Daily scheduled batch at 23:00 (Hangfire recurring job); or manual trigger via UI.

**Outcomes:**
- CSV file generated from `OnHandValueView`
- Exported to Agnum via API
- Reconciliation report produced and stored
- Any discrepancies flagged for accountant review

**Primary Actors:** Inventory Accountant, Warehouse Manager, System (scheduled)

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Finance → Agnum Config | `/warehouse/agnum/config` | `Agnum/Configuration.razor` |
| Finance → Agnum Reconcile | `/warehouse/agnum/reconcile` | `Agnum/Reconciliation.razor` |

**Key Subprocesses Used:** None (integration process)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/agnum/config` | `AgnumController` | InventoryAccountantOrManager |
| PUT | `api/warehouse/v1/agnum/config` | `AgnumController` | InventoryAccountantOrManager |
| POST | `api/warehouse/v1/agnum/schedule` | `AgnumController` | InventoryAccountantOrManager |
| GET | `api/warehouse/v1/agnum/export-status` | `AgnumController` | InventoryAccountantOrManager |
| POST | `api/warehouse/v1/agnum/reconciliation` | `AgnumController` | InventoryAccountantOrManager |
| GET | `api/warehouse/v1/agnum/reconciliation-report` | `AgnumController` | InventoryAccountantOrManager |
| DELETE | `api/warehouse/v1/agnum/reconciliation-report/{reportId:int}` | `AgnumController` | InventoryAccountantOrManager |

**Key Domain Objects:** AgnumConfig, ExportJob, ReconciliationReport

**Services:**
- `AgnumExportOrchestrator` (in `AgnumExportServices.cs`, ~150 KB) — CSV generation, API call, retry logic
- `AgnumReconciliationServices.cs`
- `AgnumSecretProtector`, `AgnumDataProtector`
  - Files: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/`

**Architectural note (Decision 5):** Financial integration is batch-tier — latency is scheduled daily, not real-time.

---

### P-09 Returns / RMA

**Purpose:** Process customer returns: receive returned goods, inspect condition, restock or scrap as appropriate.

**Trigger:** Customer initiates return; RMA created in system.

**Outcomes:**
- RMA record created and tracked
- Returned goods physically received
- QC inspection performed on returned goods
- Stock reinstated (RECEIPT movement) or written off (SCRAP movement)

**Primary Actors:** Returns/RMA Clerk, QC Inspector, Warehouse Manager

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Outbound → RMAs | `/warehouse/rmas` | `Rmas.razor` |

**Key Subprocesses Used:**
- [SP-01 QC Inspection & Disposition](#sp-01-qc-inspection--disposition)
- [SP-06 Stock Adjustment Recording](#sp-06-stock-adjustment-recording)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| RMA endpoints | `api/warehouse/v1/rma` | `AdvancedWarehouseController` (RmaController) | — |

**Key Domain Objects:** RMA, ReturnedGoods, QcInspection, StockMovement (RECEIPT or SCRAP)

**Services:** `AdvancedWarehouseStore.cs`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/AdvancedWarehouseStore.cs`

**Notes:** RMA-specific QC may use `QcAdvancedController` (`api/warehouse/v1/qc-advanced`) for advanced inspection flows.

---

### P-10 Cross-Dock Operations

**Purpose:** Route incoming goods directly from receiving to outbound dispatch without formal putaway into storage.

**Trigger:** Inbound shipment matches a pending outbound order; cross-dock opportunity identified.

**Outcomes:**
- Cross-dock record created and tracked
- Stock transferred directly from receiving dock to dispatch dock
- Relevant read models updated

**Primary Actors:** Warehouse Manager, Dispatch Clerk

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Outbound → Cross-Dock | `/warehouse/cross-dock` | `CrossDock.razor` |

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| POST | `api/warehouse/v1/cross-dock` | `AdvancedWarehouseController` (CrossDockController) | — |
| GET | `api/warehouse/v1/cross-dock` | `AdvancedWarehouseController` (CrossDockController) | — |
| POST | `api/warehouse/v1/cross-dock/{id:guid}/status` | `AdvancedWarehouseController` (CrossDockController) | — |

**Key Domain Objects:** CrossDockRecord, InboundShipment, OutboundOrder

**Notes:** Backed by `AdvancedWarehouseClient` in `CrossDock.razor`. Cross-dock specifics (matching rules) not yet fully documented.

---

### P-11 Lot & Serial Number Traceability

**Purpose:** Track the full lifecycle of lot/batch numbers and serial numbers across inbound → storage → outbound, and produce traceability reports for compliance and recall scenarios.

**Trigger:**
- Lot created at receiving
- Serial number assigned to HU line
- Traceability query by compliance officer or customer

**Outcomes:**
- Lot/serial records maintained
- Full traceability chain (supplier → receiving → location → order → dispatch) available on demand
- Compliance reports exportable

**Primary Actors:** QC Inspector, Compliance Officer, Warehouse Manager

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Reports → Traceability | `/reports/traceability` | `ReportsTraceability.razor` |
| Reports → Lot Traceability | `/warehouse/compliance/lot-trace` | `ComplianceLotTrace.razor` |
| Reports → Compliance Dashboard | `/warehouse/compliance/dashboard` | `ComplianceDashboard.razor` |
| Admin → Serial Numbers | `/warehouse/admin/serial-numbers` | `Admin/SerialNumbers.razor` |
| Admin → Lots | `/warehouse/admin/lots` | `Admin/Lots.razor` |

**Key Subprocesses Used:**
- [SP-09 Lot / Batch Assignment](#sp-09-lot--batch-assignment)
- [SP-11 Serial Number Assignment](#sp-11-serial-number-assignment)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/lots/item/{itemId:int}` | `LotsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/lots` | `LotsController` | QcOrManager |
| GET | `api/warehouse/v1/lots/{id:int}` | `LotsController` | OperatorOrAbove |
| PUT | `api/warehouse/v1/lots/{id:int}` | `LotsController` | QcOrManager |
| Serial endpoints | `api/warehouse/v1/serials` | `AdvancedWarehouseController` (SerialsController) | — |

**Key Domain Objects:** Lot, SerialNumber, HandlingUnitLine, StockMovement

**Services:**
- `LotTraceabilityService.cs`
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/LotTraceabilityService.cs`

---

### P-12 Warehouse Visualization & Location Discovery

**Purpose:** Provide operators and managers a visual 2D/3D map of the warehouse to find stock, check location status, and understand layout.

**Trigger:** Operator searches for a SKU or browses the warehouse map interactively.

**Outcomes:**
- Location contents visible (current stock, HUs)
- Location clicked → detail view with balance information

**Primary Actors:** Warehouse Operator, Warehouse Manager

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Operations → Warehouse Map (3D) | `/warehouse/visualization/3d` | `Visualization/Warehouse3D.razor` |
| Operations → Warehouse Map (2D) | `/warehouse/visualization/2d` | `Visualization/Warehouse3D.razor` (shared) |
| — | `/warehouse/locations/{Id:int}` | `WarehouseLocationDetail.razor` |

**Key Subprocesses Used:**
- [SP-12 Layout Editor / Zone Setup](#sp-12-layout-editor--zone-setup) (admin subprocess)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/visualization/layout/{layoutId:int}` | `WarehouseVisualizationController` | OperatorOrAbove |
| GET | `api/warehouse/v1/visualization/3d/{layoutId:int}` | `WarehouseVisualizationController` | OperatorOrAbove |
| GET | `api/warehouse/v1/stock/available` | `StockController` | OperatorOrAbove |
| GET | `api/warehouse/v1/stock/location-balance` | `StockController` | OperatorOrAbove |

**Key Domain Objects:** WarehouseLayout, WarehouseLocation, LocationBalance

**Notes:** `VisualizationClient` used in `Warehouse3D.razor`. 2D and 3D share the same Razor file.

---

### P-13 Reporting & Analytics

**Purpose:** Provide historical and current visibility into stock levels, movements, receiving, picking, dispatch, fulfillment KPIs, and quality metrics.

**Trigger:** On-demand query by manager, accountant, or compliance officer; also scheduled via Hangfire.

**Outcomes:** Reports rendered in UI or exported; KPI dashboards refreshed.

**Primary Actors:** Warehouse Manager, Inventory Accountant, CFO, Compliance Officer, Auditor

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Stock → Available Stock | `/available-stock` | `AvailableStock.razor` |
| Stock → Stock Dashboard | `/warehouse/stock/dashboard` | `StockDashboard.razor` |
| Stock → Location Balance | `/warehouse/stock/location-balance` | `StockLocationBalance.razor` |
| Stock → Reservations | `/reservations` | `Reservations.razor` |
| Reports → Stock Level | `/reports/stock-level` | `ReportsStockLevel.razor` |
| Reports → Receiving History | `/reports/receiving-history` | `ReportsReceivingHistory.razor` |
| Reports → Pick History | `/reports/pick-history` | `ReportsPickHistory.razor` |
| Reports → Dispatch History | `/reports/dispatch-history` | `ReportsDispatchHistory.razor` |
| Reports → Stock Movements | `/reports/stock-movements` | `ReportsStockMovements.razor` |
| Reports → Traceability | `/reports/traceability` | `ReportsTraceability.razor` |
| Reports → Compliance Audit | `/reports/compliance-audit` | `ReportsComplianceAudit.razor` |
| Analytics → Fulfillment KPIs | `/analytics/fulfillment` | `AnalyticsFulfillment.razor` |
| Analytics → Quality Analytics | `/analytics/quality` | `AnalyticsQuality.razor` |
| — | `/dashboard` | `Dashboard.razor` |
| — | `/projections` | `Projections.razor` |

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET | `api/warehouse/v1/reports/inventory` | `ReportsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/movements` | `ReportsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/utilization` | `ReportsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/aged-stock` | `ReportsController` | CfoOrAdmin |
| GET | `api/warehouse/v1/stock/available` | `StockController` | OperatorOrAbove |
| GET | `api/warehouse/v1/stock/location-balance` | `StockController` | OperatorOrAbove |
| GET | `api/warehouse/v1/metrics/performance` | `MetricsController` | OperatorOrAbove |
| GET | `api/warehouse/v1/metrics/utilization` | `MetricsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/dashboard/summary` | `DashboardController` | OperatorOrAbove |
| GET | `api/warehouse/v1/dashboard/widget/{widgetId}` | `DashboardController` | OperatorOrAbove |
| GET | `api/warehouse/v1/analytics` | `AdvancedWarehouseController` (AdvancedAnalyticsController) | — |
| GET | `api/warehouse/v1/projections/status` | `ProjectionsController` | AdminOnly |
| GET | `api/warehouse/v1/projections/{name}/lag` | `ProjectionsController` | AdminOnly |
| POST | `api/warehouse/v1/projections/{name}/rebuild` | `ProjectionsController` | AdminOnly |

**Read Models consumed:** `LocationBalanceView`, `AvailableStockView`, `ActiveHardLockView`, `HandlingUnitView`, `OnHandValueView`

**Services:**
- `ReportsClient` typed client (covers all report pages)
- `ScheduledReportsRecurringJob.cs` — Hangfire scheduled report generation
  - File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/ScheduledReportsRecurringJob.cs`

**Application Queries:**
- `GetLocationBalanceQuery`, `GetAvailableStockQuery`, `SearchReservationsQuery`, `VerifyProjectionQuery`
  - Files: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Queries/`

---

### P-14 System Administration & Compliance

**Purpose:** Manage system configuration, user access, API keys, compliance obligations (GDPR, audit), data retention, disaster recovery, and operational health.

**Trigger:** Admin action, scheduled DR drill, regulatory request, or system alert.

**Outcomes:**
- System properly configured; access controlled
- Audit trail maintained
- GDPR erasure requests fulfilled
- Backups verified; DR drills documented

**Primary Actors:** System Administrator, Compliance Officer, Auditor

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Admin → Users | `/admin/users` | `AdminUsers.razor` |
| Admin → Admin Settings | `/warehouse/admin/settings` | `Admin/Settings.razor` |
| Admin → Reason Codes | `/warehouse/admin/reason-codes` | `Admin/ReasonCodes.razor` |
| Admin → Approval Rules | `/warehouse/admin/approval-rules` | `Admin/ApprovalRules.razor` |
| Admin → Roles | `/warehouse/admin/roles` | `Admin/Roles.razor` |
| Admin → API Keys | `/warehouse/admin/api-keys` | `Admin/ApiKeys.razor` |
| Admin → GDPR Erasure | `/warehouse/admin/gdpr-erasure` | `Admin/GdprErasure.razor` |
| Admin → Audit Logs | `/warehouse/admin/audit-logs` | `Admin/AuditLogs.razor` |
| Admin → Backups | `/warehouse/admin/backups` | `Admin/Backups.razor` |
| Admin → Retention Policies | `/warehouse/admin/retention-policies` | `Admin/RetentionPolicies.razor` |
| Admin → DR Drills | `/warehouse/admin/dr-drills` | `Admin/DisasterRecoveryDrills.razor` |

**Primary APIs (admin group):**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET/POST/PUT/DELETE | `api/warehouse/v1/admin/api-keys` | `AdminApiKeysController` | AdminOnly |
| GET/POST/PUT/DELETE | `api/admin/users` | `AdminUsersController` | AdminOnly |
| GET/POST/PUT/DELETE | `api/warehouse/v1/admin/roles` | `AdminRolesController` | AdminOnly |
| GET/POST/PUT/DELETE | `api/warehouse/v1/admin/approval-rules` | `AdminApprovalRulesController` | AdminOnly |
| GET | `api/warehouse/v1/admin/audit-logs` | `AdminAuditLogsController` | AdminOnly |
| GET/PUT | `api/warehouse/v1/admin/settings` | `AdminSettingsController` | AdminOnly |
| GET/POST/PUT/DELETE | `api/warehouse/v1/admin/reason-codes` | `AdminReasonCodesController` | AdminOnly |
| GET/POST/PUT | `api/warehouse/v1/admin/retention-policies` | `AdminRetentionPoliciesController` | AdminOnly |
| GET/POST | `api/warehouse/v1/admin/backups` | `AdminBackupsController` | AdminOnly |
| POST | `api/warehouse/v1/admin/backups/{id:int}/restore` | `AdminBackupsController` | AdminOnly |
| GET/POST | `api/warehouse/v1/admin/disaster-recovery` | `AdminDisasterRecoveryController` | AdminOnly |
| POST | `api/warehouse/v1/admin/gdpr/erasure-request` | `AdminGdprController` | AdminOnly |
| GET | `api/warehouse/v1/admin/gdpr/erasure-status/{requestId}` | `AdminGdprController` | AdminOnly |
| GET | `api/warehouse/v1/admin/compliance/status` | `AdminComplianceController` | AdminOnly |
| POST | `api/warehouse/v1/admin/compliance/audit-export` | `AdminComplianceController` | AdminOnly |
| GET/POST/PUT/DELETE | `api/warehouse/v1/feature-flags` | `FeatureFlagsController` | AdminOnly |
| GET/POST | `api/warehouse/v1/admin/encryption` | `AdminEncryptionController` | AdminOnly |
| GET/POST | `api/auth/oauth` | `OAuthController` | AllowAnonymous/Authenticated |
| POST/GET | `api/auth/mfa` | `MfaController` | Authenticated |

**Key Domain Objects:** User, Role, ApiKey, AuditLog, RetentionPolicy, BackupRecord, GdprRequest

**Services:**
- `GdprErasureService.cs`, `PiiEncryptionService.cs`, `SecurityAuditLogService.cs`, `ComplianceReportService.cs`
- `ApiKeyService.cs`, `RoleManagementService.cs`, `ApprovalRuleService.cs`
- `BackupServices.cs`, `DisasterRecoveryService.cs`
- `SchemaDriftHealthService.cs` (health check)
- `AlertEscalationService.cs`, `SlaMonitoringService.cs`
  - Files: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/`

---

### P-15 Master Data Management

**Purpose:** Create and maintain the reference data that all warehouse processes depend on: items (SKUs), suppliers, locations, categories, units of measure, and supplier-item mappings.

**Trigger:** New product, supplier, or warehouse location onboarded; UoM conversion needed; bulk import.

**Outcomes:**
- Master data records created/updated
- Changes immediately available to all processes

**Primary Actors:** Warehouse Manager, Administrator

**UI Entry Points:**

| Nav path | Route | File |
|---|---|---|
| Admin → Items | `/admin/items` | `AdminItems.razor` |
| Admin → Suppliers | `/admin/suppliers` | `AdminSuppliers.razor` |
| Admin → Supplier Mappings | `/admin/supplier-mappings` | `AdminSupplierMappings.razor` |
| Admin → Locations | `/admin/locations` | `AdminLocations.razor` |
| Admin → Categories | `/admin/categories` | `AdminCategories.razor` |
| Admin → Import Wizard | `/admin/import` | `AdminImport.razor` |
| Admin → Units of Measure | `/warehouse/admin/uom` | `Admin/UnitsOfMeasure.razor` |
| Admin → Layout Editor | `/warehouse/admin/layout-editor` | `Admin/LayoutEditor.razor` |

**Key Subprocesses Used:**
- [SP-12 Layout Editor / Zone Setup](#sp-12-layout-editor--zone-setup)
- [SP-13 UoM Conversion Setup](#sp-13-uom-conversion-setup)

**Primary APIs:**

| Method | Route | Controller | Auth |
|---|---|---|---|
| GET/POST/GET/PUT/DELETE | `api/warehouse/v1/items` | `ItemsController` | OperatorOrAbove / ManagerOrAdmin / AdminOnly |
| GET/POST/GET/PUT | `api/warehouse/v1/suppliers` | `SuppliersController` | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT/DELETE | `api/warehouse/v1/supplier-item-mappings` | `SupplierItemMappingsController` | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/GET/PUT | `api/warehouse/v1/locations` | `LocationsController` | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/categories` | `CategoriesController` | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/unit-of-measures` | `UnitOfMeasuresController` | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/item-uom-conversions/item/{itemId:int}` | `ItemUomConversionsController` | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/handling-unit-types` | `HandlingUnitTypesController` | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/GET/PUT | `api/warehouse/v1/customers` | `CustomersController` | OperatorOrAbove / SalesAdminOrManager |
| POST | `api/warehouse/v1/import/csv-upload` | `ImportController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/import/status/{jobId:guid}` | `ImportController` | ManagerOrAdmin |
| POST | `api/warehouse/v1/barcodes/lookup` | `BarcodesController` | OperatorOrAbove |
| GET | `api/warehouse/v1/barcodes/item/{itemId:int}` | `BarcodesController` | OperatorOrAbove |

**Key Domain Objects:** Item, Supplier, SupplierItemMapping, WarehouseLocation, Category, UnitOfMeasure, ItemUomConversion, HandlingUnitType, Customer

**Docs reference:** `docs/master-data/` directory (master-data-00-overview.md through master-data-05-implementation-plan-and-tests.md)

**Note:** `MasterDataEntities.cs` is a known tech debt god object (~1400 LOC, 50+ entities — ARCH-01 open item).

---

## 3. Subprocess Library

Subprocesses are reusable activities used by multiple top-level processes.

---

### SP-01 QC Inspection & Disposition

**Purpose:** Inspect received (or returned) goods for quality; approve or reject.

**Used by:** P-01 (Goods Receiving), P-09 (Returns/RMA)

**UI entry:** `/warehouse/inbound/qc` → `ReceivingQc.razor`

**API:**
- `GET/POST api/warehouse/v1/qc/inspections` — `QCController`
- `POST api/warehouse/v1/qc/inspections/{id:guid}/approve` — `QCController`
- `POST api/warehouse/v1/qc/inspections/{id:guid}/reject` — `QCController`
- `api/warehouse/v1/qc-advanced` — `AdvancedWarehouseController` (QcAdvancedController) for extended inspection flows

**Actors:** QC Inspector

---

### SP-02 Reservation Lifecycle (SOFT → HARD)

**Purpose:** Manage stock reservations from soft allocation through hard lock and final consumption.

**Used by:** P-03 (Outbound Order Fulfillment)

**Domain states:** `Created → Allocated (SOFT) → PickingStarted (HARD) → Consumed → Cancelled`

**API:**
- `GET api/warehouse/v1/reservations` — `ReservationsController`
- `POST api/warehouse/v1/reservations/{id:guid}/start-picking` — `ReservationsController`
- `POST api/warehouse/v1/reservations/{id:guid}/pick` — `ReservationsController`
- `POST api/warehouse/v1/sales-orders/{id:int}/reserve` — `SalesOrdersController`

**Commands:** `AllocateReservationCommand`, `StartPickingCommand`, `PickStockCommand`
- Files: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/`

**Architectural rule:** HARD locks cannot be bumped (Decision 4). Advisory lock serialization via `pg_advisory_xact_lock` (Package B).

---

### SP-03 Approval Workflow

**Purpose:** Route certain operations (transfers, adjustments) through a manager approval step before execution.

**Used by:** P-04 (Internal Transfer), P-06 (Stock Adjustments) — when approval rules configured

**UI entry:** `/warehouse/admin/approval-rules` → `Admin/ApprovalRules.razor`

**API:**
- `api/warehouse/v1/admin/approval-rules` — `AdminApprovalRulesController`

**Services:** `ApprovalRuleService.cs`, `ElectronicSignatureService.cs`

---

### SP-04 Reason Code Selection

**Purpose:** Tag stock movements with a reason code for audit and reporting purposes.

**Used by:** P-05 (Cycle Count discrepancy write), P-06 (Stock Adjustments)

**UI entry:** `/warehouse/admin/reason-codes` → `Admin/ReasonCodes.razor`

**API:** `api/warehouse/v1/admin/reason-codes` — `AdminReasonCodesController`

**Services:** `ReasonCodeService.cs`

---

### SP-05 Handling Unit Lifecycle

**Purpose:** Create, update, seal, split, merge, and move physical handling units (pallets, boxes, bags).

**Used by:** P-01 (Receiving), P-02 (Putaway), P-03 (Picking/Packing), P-04 (Transfer), P-09 (Returns)

**API:**
- `api/warehouse/v1/handling-units` — `AdvancedWarehouseController` (HandlingUnitsController)

**Domain events:** `HandlingUnitCreated`, `LineAddedToHandlingUnit`, `HandlingUnitSealed`, `HandlingUnitMoved`, `HandlingUnitSplit`, `HandlingUnitMerged`, `HandlingUnitEmptied`
- File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/` (HU aggregate)

**Read model:** `HandlingUnitView`

---

### SP-06 Stock Adjustment Recording

**Purpose:** Issue a `RecordStockMovementCommand` (ADJUSTMENT or SCRAP type) to correct the StockLedger.

**Used by:** P-05 (Cycle Count discrepancies), P-06 (Manual Adjustments), P-09 (RMA scrap)

**Command:** `RecordStockMovementCommand` → `RecordStockMovementCommandHandler`
- File: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/Commands/RecordStockMovementCommand.cs`

**Architectural rule (Decision 1):** Only StockLedger writes StockMovement events. All adjustment flows must go through this command.

---

### SP-07 Label Printing

**Purpose:** Print barcode labels (ZPL over TCP:9100) for HUs, items, or locations.

**Used by:** P-01 (Receiving labels), P-03 (Packing labels)

**UI entry:** `/warehouse/labels` → `Labels.razor`

**API:**
- `POST api/warehouse/v1/labels/print` — `LabelsController`
- `GET api/warehouse/v1/labels/preview/{templateId}` — `LabelsController`
- `GET api/warehouse/v1/labels/history` — `LabelsController`

**Services:** `LabelPrintingServices.cs`, `LabelPrintQueueServices.cs`, `LabelTemplateEngine.cs`
- Files: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Services/`

**Integration:** ZPL print server at TCP:9100; retry 3x, fallback to manual queue.

---

### SP-08 Wave / Batch Picking

**Purpose:** Group multiple pick tasks into a wave for efficient execution by a single operator.

**Used by:** P-03 (Outbound Order Fulfillment)

**UI entry:** `/warehouse/waves` → `WavePicking.razor`

**API:**
- `GET/POST api/warehouse/v1/waves` — `AdvancedWarehouseController`
- `POST api/warehouse/v1/waves/{id:guid}/assign` — assign operator
- `POST api/warehouse/v1/waves/{id:guid}/start` — begin wave
- `POST api/warehouse/v1/waves/{id:guid}/complete-lines` — complete lines

---

### SP-09 Lot / Batch Assignment

**Purpose:** Assign a lot/batch number to incoming stock lines; track lot through all movements.

**Used by:** P-01 (Goods Receiving), P-11 (Traceability)

**UI entry:** `/warehouse/admin/lots` → `Admin/Lots.razor`

**API:**
- `GET api/warehouse/v1/lots/item/{itemId:int}` — `LotsController`
- `POST api/warehouse/v1/lots` — create lot (QcOrManager)
- `GET/PUT api/warehouse/v1/lots/{id:int}` — `LotsController`

---

### SP-10 Warehouse Location Lookup

**Purpose:** Query available locations by zone, capacity, or item type to suggest optimal placement.

**Used by:** P-02 (Putaway), P-04 (Transfer), P-05 (Cycle Count scheduling)

**API:**
- `GET api/warehouse/v1/locations` — `LocationsController`
- `GET api/warehouse/v1/locations/{id:int}` — `LocationsController`
- `GET api/warehouse/v1/visualization/layout/{layoutId:int}` — `WarehouseVisualizationController`

---

### SP-11 Serial Number Assignment

**Purpose:** Assign unique serial numbers to individual items received or produced.

**Used by:** P-01 (Receiving), P-11 (Traceability)

**UI entry:** `/warehouse/admin/serial-numbers` → `Admin/SerialNumbers.razor`

**API:** `api/warehouse/v1/serials` — `AdvancedWarehouseController` (SerialsController)

---

### SP-12 Layout Editor / Zone Setup

**Purpose:** Define or modify the physical warehouse layout — zones, aisles, bins — via the admin layout editor.

**Used by:** P-12 (Visualization setup), P-15 (Master Data / Location setup)

**UI entry:** `/warehouse/admin/layout-editor` → `Admin/LayoutEditor.razor`

**Services:** `LayoutEditorClient` → calls layout editor APIs

---

### SP-13 UoM Conversion Setup

**Purpose:** Configure units of measure and conversion factors for items (e.g., pallet = 48 boxes = 576 units).

**Used by:** P-01 (Receiving), P-03 (Picking), P-07 (Valuation)

**UI entry:** `/warehouse/admin/uom` → `Admin/UnitsOfMeasure.razor`

**API:**
- `GET/POST/PUT api/warehouse/v1/unit-of-measures` — `UnitOfMeasuresController`
- `GET/POST/PUT api/warehouse/v1/item-uom-conversions/item/{itemId:int}` — `ItemUomConversionsController`

---

### SP-14 Barcode Lookup

**Purpose:** Resolve a scanned barcode to its item and/or handling unit for use in receiving, picking, and transfer flows.

**Used by:** P-01, P-02, P-03, P-04 (all scan-driven operations)

**API:**
- `POST api/warehouse/v1/barcodes/lookup` — `BarcodesController`
- `GET api/warehouse/v1/barcodes/item/{itemId:int}` — `BarcodesController`

---

### SP-15 Alert Escalation

**Purpose:** Escalate unresolved system alerts (SLA breaches, stock discrepancies) to the appropriate manager.

**Used by:** P-14 (System Administration), operational monitoring

**API:**
- `GET api/warehouse/v1/alerts/pending` — `AlertEscalationController`
- `POST api/warehouse/v1/alerts/{id:guid}/escalate` — `AlertEscalationController`

**Services:** `AlertEscalationService.cs`, `SlaMonitoringService.cs`

---

## Appendix A — UI Route Index

| Route | Page File | Nav Group | Intent |
|---|---|---|---|
| `/` | `Index.razor` | — | Redirect to /dashboard |
| `/dashboard` | `Dashboard.razor` | — | System health + stock summary |
| `/available-stock` | `AvailableStock.razor` | Stock | View available inventory |
| `/warehouse/stock/dashboard` | `StockDashboard.razor` | Stock | Stock analytics |
| `/warehouse/stock/location-balance` | `StockLocationBalance.razor` | Stock | Balance per location |
| `/warehouse/stock/adjustments` | `StockAdjustments.razor` | Stock | Record adjustments |
| `/reservations` | `Reservations.razor` | Stock | View/manage reservations |
| `/warehouse/inbound/shipments` | `InboundShipments.razor` | Inbound | Inbound shipment list |
| `/warehouse/inbound/shipments/create` | `InboundShipmentCreate.razor` | Inbound | Create shipment |
| `/warehouse/inbound/shipments/{Id:int}` | `InboundShipmentDetail.razor` | Inbound | Shipment detail |
| `/warehouse/inbound/qc` | `ReceivingQc.razor` | Inbound | QC inspection queue |
| `/warehouse/putaway` | `Putaway.razor` | Inbound | Putaway tasks |
| `/warehouse/sales/orders` | `SalesOrders.razor` | Outbound | Sales order list |
| `/warehouse/sales/orders/create` | `SalesOrderCreate.razor` | Outbound | Create sales order |
| `/warehouse/sales/orders/{Id:guid}` | `SalesOrderDetail.razor` | Outbound | Sales order detail |
| `/warehouse/sales/allocations` | `AllocationDashboard.razor` | Outbound | Allocation dashboard |
| `/warehouse/outbound/orders` | `OutboundOrders.razor` | Outbound | Outbound order list |
| `/warehouse/outbound/orders/{Id:guid}` | `OutboundOrderDetail.razor` | Outbound | Outbound order detail |
| `/warehouse/outbound/dispatch` | `OutboundDispatch.razor` | Outbound | Dispatch confirmation |
| `/warehouse/outbound/pack/{OrderId:guid}` | `PackingStation.razor` | Outbound | Packing station |
| `/warehouse/waves` | `WavePicking.razor` | Outbound | Wave picking |
| `/warehouse/picking/tasks` | `PickingTasks.razor` | Outbound | Picking task execution |
| `/warehouse/labels` | `Labels.razor` | Outbound | Label printing |
| `/warehouse/cross-dock` | `CrossDock.razor` | Outbound | Cross-dock |
| `/warehouse/rmas` | `Rmas.razor` | Outbound | Returns/RMA |
| `/warehouse/transfers` | `Transfers/List.razor` | Operations | Transfer list |
| `/warehouse/transfers/create` | `Transfers/Create.razor` | Operations | Create transfer |
| `/warehouse/transfers/{Id:guid}/execute` | `Transfers/Execute.razor` | Operations | Execute transfer |
| `/warehouse/cycle-counts` | `CycleCounts/List.razor` | Operations | Cycle count list |
| `/warehouse/cycle-counts/schedule` | `CycleCounts/Schedule.razor` | Operations | Schedule count |
| `/warehouse/cycle-counts/{Id:guid}/execute` | `CycleCounts/Execute.razor` | Operations | Execute count |
| `/warehouse/cycle-counts/{Id:guid}/discrepancies` | `CycleCounts/Discrepancies.razor` | Operations | Review discrepancies |
| `/warehouse/visualization/3d` | `Visualization/Warehouse3D.razor` | Operations | 3D warehouse map |
| `/warehouse/visualization/2d` | `Visualization/Warehouse3D.razor` | Operations | 2D warehouse map |
| `/projections` | `Projections.razor` | Operations | Projection health |
| `/warehouse/valuation/dashboard` | `Valuation/Dashboard.razor` | Finance | Valuation summary |
| `/warehouse/valuation/adjust-cost` | `Valuation/AdjustCost.razor` | Finance | Adjust cost |
| `/warehouse/valuation/apply-landed-cost` | `Valuation/ApplyLandedCost.razor` | Finance | Apply landed cost |
| `/warehouse/valuation/write-down` | `Valuation/WriteDown.razor` | Finance | Write down |
| `/warehouse/agnum/config` | `Agnum/Configuration.razor` | Finance | Agnum config |
| `/warehouse/agnum/reconcile` | `Agnum/Reconciliation.razor` | Finance | Agnum reconcile |
| `/reports/stock-level` | `ReportsStockLevel.razor` | Reports | Stock level report |
| `/reports/receiving-history` | `ReportsReceivingHistory.razor` | Reports | Receiving history |
| `/reports/pick-history` | `ReportsPickHistory.razor` | Reports | Pick history |
| `/reports/dispatch-history` | `ReportsDispatchHistory.razor` | Reports | Dispatch history |
| `/reports/stock-movements` | `ReportsStockMovements.razor` | Reports | Stock movements |
| `/reports/traceability` | `ReportsTraceability.razor` | Reports | Lot traceability |
| `/reports/compliance-audit` | `ReportsComplianceAudit.razor` | Reports | Compliance audit |
| `/warehouse/compliance/lot-trace` | `ComplianceLotTrace.razor` | Reports | Lot trace detail |
| `/warehouse/compliance/dashboard` | `ComplianceDashboard.razor` | Reports | Compliance dashboard |
| `/analytics/fulfillment` | `AnalyticsFulfillment.razor` | Analytics | Fulfillment KPIs |
| `/analytics/quality` | `AnalyticsQuality.razor` | Analytics | Quality analytics |
| `/warehouse/admin/settings` | `Admin/Settings.razor` | Admin | Warehouse settings |
| `/warehouse/admin/reason-codes` | `Admin/ReasonCodes.razor` | Admin | Reason codes |
| `/warehouse/admin/approval-rules` | `Admin/ApprovalRules.razor` | Admin | Approval rules |
| `/warehouse/admin/roles` | `Admin/Roles.razor` | Admin | Roles |
| `/warehouse/admin/api-keys` | `Admin/ApiKeys.razor` | Admin | API keys |
| `/warehouse/admin/gdpr-erasure` | `Admin/GdprErasure.razor` | Admin | GDPR erasure |
| `/warehouse/admin/audit-logs` | `Admin/AuditLogs.razor` | Admin | Audit logs |
| `/warehouse/admin/backups` | `Admin/Backups.razor` | Admin | Backups |
| `/warehouse/admin/retention-policies` | `Admin/RetentionPolicies.razor` | Admin | Retention policies |
| `/warehouse/admin/dr-drills` | `Admin/DisasterRecoveryDrills.razor` | Admin | DR drills |
| `/warehouse/admin/serial-numbers` | `Admin/SerialNumbers.razor` | Admin | Serial numbers config |
| `/warehouse/admin/lots` | `Admin/Lots.razor` | Admin | Lots config |
| `/warehouse/admin/uom` | `Admin/UnitsOfMeasure.razor` | Admin | UoM config |
| `/warehouse/admin/layout-editor` | `Admin/LayoutEditor.razor` | Admin | Layout editor |
| `/admin/users` | `AdminUsers.razor` | Admin | User management |
| `/admin/items` | `AdminItems.razor` | Admin | Items management |
| `/admin/suppliers` | `AdminSuppliers.razor` | Admin | Suppliers |
| `/admin/supplier-mappings` | `AdminSupplierMappings.razor` | Admin | Supplier mappings |
| `/admin/locations` | `AdminLocations.razor` | Admin | Locations management |
| `/admin/categories` | `AdminCategories.razor` | Admin | Categories |
| `/admin/import` | `AdminImport.razor` | Admin | Bulk import |
| `/warehouse/locations/{Id:int}` | `WarehouseLocationDetail.razor` | — | Location detail |

All Razor files located under:
`src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/`

---

## Appendix B — API Route Index

All controllers located under:
`src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/`

| Method | Route | Controller | Auth Policy |
|---|---|---|---|
| GET | `api/warehouse/v1/health` | `HealthController` | AllowAnonymous |
| GET | `api/warehouse/v1/stock/available` | `StockController` | OperatorOrAbove |
| GET | `api/warehouse/v1/stock/location-balance` | `StockController` | OperatorOrAbove |
| GET | `api/warehouse/v1/picking/tasks/{id:guid}/locations` | `PickingController` | OperatorOrAbove |
| POST | `api/warehouse/v1/picking/tasks` | `PickingController` | ManagerOrAdmin |
| POST | `api/warehouse/v1/picking/tasks/{id:guid}/complete` | `PickingController` | OperatorOrAbove |
| GET | `api/warehouse/v1/picking/history` | `PickingController` | OperatorOrAbove |
| GET | `api/warehouse/v1/receiving/shipments` | `ReceivingController` | OperatorOrAbove |
| GET | `api/warehouse/v1/receiving/shipments/{id:int}` | `ReceivingController` | OperatorOrAbove |
| POST | `api/warehouse/v1/receiving/shipments` | `ReceivingController` | QcOrManager |
| POST | `api/warehouse/v1/receiving/shipments/{id:int}/receive` | `ReceivingController` | QcOrManager |
| POST | `api/warehouse/v1/receiving/shipments/{id:int}/receive-items` | `ReceivingController` | QcOrManager |
| POST | `api/warehouse/v1/adjustments` | `AdjustmentsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/adjustments/history` | `AdjustmentsController` | Authenticated |
| GET | `api/warehouse/v1/reservations` | `ReservationsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/reservations/{id:guid}/start-picking` | `ReservationsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/reservations/{id:guid}/pick` | `ReservationsController` | OperatorOrAbove |
| GET | `api/warehouse/v1/agnum/config` | `AgnumController` | InventoryAccountantOrManager |
| PUT | `api/warehouse/v1/agnum/config` | `AgnumController` | InventoryAccountantOrManager |
| POST | `api/warehouse/v1/agnum/schedule` | `AgnumController` | InventoryAccountantOrManager |
| GET | `api/warehouse/v1/agnum/export-status` | `AgnumController` | InventoryAccountantOrManager |
| POST | `api/warehouse/v1/agnum/reconciliation` | `AgnumController` | InventoryAccountantOrManager |
| GET | `api/warehouse/v1/agnum/reconciliation-report` | `AgnumController` | InventoryAccountantOrManager |
| DELETE | `api/warehouse/v1/agnum/reconciliation-report/{reportId:int}` | `AgnumController` | InventoryAccountantOrManager |
| GET | `api/warehouse/v1/admin/api-keys` | `AdminApiKeysController` | AdminOnly |
| POST | `api/warehouse/v1/admin/api-keys` | `AdminApiKeysController` | AdminOnly |
| PUT | `api/warehouse/v1/admin/api-keys/{id:int}/rotate` | `AdminApiKeysController` | AdminOnly |
| DELETE | `api/warehouse/v1/admin/api-keys/{id:int}` | `AdminApiKeysController` | AdminOnly |
| GET | `api/admin/users` | `AdminUsersController` | AdminOnly |
| POST | `api/admin/users` | `AdminUsersController` | AdminOnly |
| PUT | `api/admin/users/{id:guid}` | `AdminUsersController` | AdminOnly |
| DELETE | `api/admin/users/{id:guid}` | `AdminUsersController` | AdminOnly |
| GET | `api/auth/oauth/login` | `OAuthController` | AllowAnonymous |
| GET | `api/auth/oauth/callback` | `OAuthController` | AllowAnonymous |
| POST | `api/auth/oauth/logout` | `OAuthController` | Authenticated |
| GET | `api/warehouse/v1/waves` | `AdvancedWarehouseController` | — |
| POST | `api/warehouse/v1/waves` | `AdvancedWarehouseController` | — |
| GET | `api/warehouse/v1/waves/{id:guid}` | `AdvancedWarehouseController` | — |
| POST | `api/warehouse/v1/waves/{id:guid}/assign` | `AdvancedWarehouseController` | — |
| POST | `api/warehouse/v1/waves/{id:guid}/start` | `AdvancedWarehouseController` | — |
| POST | `api/warehouse/v1/waves/{id:guid}/complete-lines` | `AdvancedWarehouseController` | — |
| GET | `api/warehouse/v1/cross-dock` | `AdvancedWarehouseController` | — |
| POST | `api/warehouse/v1/cross-dock` | `AdvancedWarehouseController` | — |
| POST | `api/warehouse/v1/cross-dock/{id:guid}/status` | `AdvancedWarehouseController` | — |
| GET | `api/warehouse/v1/rma` | `AdvancedWarehouseController` | — |
| GET | `api/warehouse/v1/serials` | `AdvancedWarehouseController` | — |
| GET | `api/warehouse/v1/handling-units` | `AdvancedWarehouseController` | — |
| GET | `api/warehouse/v1/analytics` | `AdvancedWarehouseController` | — |
| GET | `api/warehouse/v1/admin/audit-logs` | `AdminAuditLogsController` | AdminOnly |
| GET | `api/warehouse/v1/admin/audit-logs/{id:int}` | `AdminAuditLogsController` | AdminOnly |
| GET | `api/warehouse/v1/alerts/pending` | `AlertEscalationController` | OperatorOrAbove |
| POST | `api/warehouse/v1/alerts/{id:guid}/escalate` | `AlertEscalationController` | ManagerOrAdmin |
| POST | `api/warehouse/v1/barcodes/lookup` | `BarcodesController` | OperatorOrAbove |
| GET | `api/warehouse/v1/barcodes/item/{itemId:int}` | `BarcodesController` | OperatorOrAbove |
| GET | `api/warehouse/v1/categories` | `CategoriesController` | OperatorOrAbove |
| POST | `api/warehouse/v1/categories` | `CategoriesController` | ManagerOrAdmin |
| PUT | `api/warehouse/v1/categories/{id:int}` | `CategoriesController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/customers` | `CustomersController` | OperatorOrAbove |
| POST | `api/warehouse/v1/customers` | `CustomersController` | SalesAdminOrManager |
| PUT | `api/warehouse/v1/customers/{id:int}` | `CustomersController` | SalesAdminOrManager |
| GET | `api/warehouse/v1/cycle-counts` | `CycleCountsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts` | `CycleCountsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/cycle-counts/{id:guid}` | `CycleCountsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts/{id:guid}/count-items` | `CycleCountsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/cycle-counts/{id:guid}/complete` | `CycleCountsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/dashboard/summary` | `DashboardController` | OperatorOrAbove |
| GET | `api/warehouse/v1/dashboard/widget/{widgetId}` | `DashboardController` | OperatorOrAbove |
| GET | `api/warehouse/v1/feature-flags` | `FeatureFlagsController` | AdminOnly |
| POST | `api/warehouse/v1/feature-flags` | `FeatureFlagsController` | AdminOnly |
| PUT | `api/warehouse/v1/feature-flags/{flag:alpha}` | `FeatureFlagsController` | AdminOnly |
| DELETE | `api/warehouse/v1/feature-flags/{flag:alpha}` | `FeatureFlagsController` | AdminOnly |
| GET | `api/warehouse/v1/idempotency/{key:guid}` | `IdempotencyController` | OperatorOrAbove |
| POST | `api/warehouse/v1/import/csv-upload` | `ImportController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/import/status/{jobId:guid}` | `ImportController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/items` | `ItemsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/items` | `ItemsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/items/{id:int}` | `ItemsController` | OperatorOrAbove |
| PUT | `api/warehouse/v1/items/{id:int}` | `ItemsController` | ManagerOrAdmin |
| DELETE | `api/warehouse/v1/items/{id:int}` | `ItemsController` | AdminOnly |
| POST | `api/warehouse/v1/labels/print` | `LabelsController` | PackingOperatorOrManager |
| GET | `api/warehouse/v1/labels/preview/{templateId}` | `LabelsController` | PackingOperatorOrManager |
| GET | `api/warehouse/v1/labels/history` | `LabelsController` | PackingOperatorOrManager |
| GET | `api/warehouse/v1/locations` | `LocationsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/locations` | `LocationsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/locations/{id:int}` | `LocationsController` | OperatorOrAbove |
| PUT | `api/warehouse/v1/locations/{id:int}` | `LocationsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/lots/item/{itemId:int}` | `LotsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/lots` | `LotsController` | QcOrManager |
| GET | `api/warehouse/v1/lots/{id:int}` | `LotsController` | OperatorOrAbove |
| PUT | `api/warehouse/v1/lots/{id:int}` | `LotsController` | QcOrManager |
| GET | `api/warehouse/v1/metrics/performance` | `MetricsController` | OperatorOrAbove |
| GET | `api/warehouse/v1/metrics/utilization` | `MetricsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/outbound-orders` | `OutboundOrdersController` | SalesAdminOrManager |
| POST | `api/warehouse/v1/outbound-orders` | `OutboundOrdersController` | SalesAdminOrManager |
| GET | `api/warehouse/v1/outbound-orders/{id:int}` | `OutboundOrdersController` | SalesAdminOrManager |
| PUT | `api/warehouse/v1/outbound-orders/{id:int}` | `OutboundOrdersController` | SalesAdminOrManager |
| GET | `api/warehouse/v1/projections/status` | `ProjectionsController` | AdminOnly |
| POST | `api/warehouse/v1/projections/{name}/rebuild` | `ProjectionsController` | AdminOnly |
| GET | `api/warehouse/v1/projections/{name}/lag` | `ProjectionsController` | AdminOnly |
| GET | `api/warehouse/v1/putaway/tasks` | `PutawayController` | OperatorOrAbove |
| POST | `api/warehouse/v1/putaway/tasks` | `PutawayController` | ManagerOrAdmin |
| POST | `api/warehouse/v1/putaway/tasks/{id:guid}/complete` | `PutawayController` | OperatorOrAbove |
| GET | `api/warehouse/v1/putaway/history` | `PutawayController` | OperatorOrAbove |
| GET | `api/warehouse/v1/qc/inspections` | `QCController` | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections` | `QCController` | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections/{id:guid}/reject` | `QCController` | QcOrManager |
| POST | `api/warehouse/v1/qc/inspections/{id:guid}/approve` | `QCController` | QcOrManager |
| GET | `api/warehouse/v1/reports/inventory` | `ReportsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/movements` | `ReportsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/utilization` | `ReportsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/aged-stock` | `ReportsController` | CfoOrAdmin |
| GET | `api/warehouse/v1/sales-orders` | `SalesOrdersController` | SalesAdminOrManager |
| POST | `api/warehouse/v1/sales-orders` | `SalesOrdersController` | SalesAdminOrManager |
| GET | `api/warehouse/v1/sales-orders/{id:int}` | `SalesOrdersController` | SalesAdminOrManager |
| PUT | `api/warehouse/v1/sales-orders/{id:int}` | `SalesOrdersController` | SalesAdminOrManager |
| POST | `api/warehouse/v1/sales-orders/{id:int}/reserve` | `SalesOrdersController` | SalesAdminOrManager |
| GET | `api/warehouse/v1/shipments` | `ShipmentsController` | DispatchClerkOrManager |
| POST | `api/warehouse/v1/shipments` | `ShipmentsController` | DispatchClerkOrManager |
| GET | `api/warehouse/v1/shipments/{id:int}` | `ShipmentsController` | DispatchClerkOrManager |
| POST | `api/warehouse/v1/shipments/{id:int}/dispatch` | `ShipmentsController` | DispatchClerkOrManager |
| GET | `api/warehouse/v1/suppliers` | `SuppliersController` | OperatorOrAbove |
| POST | `api/warehouse/v1/suppliers` | `SuppliersController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/suppliers/{id:int}` | `SuppliersController` | OperatorOrAbove |
| PUT | `api/warehouse/v1/suppliers/{id:int}` | `SuppliersController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/supplier-item-mappings/supplier/{id:int}` | `SupplierItemMappingsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/supplier-item-mappings` | `SupplierItemMappingsController` | ManagerOrAdmin |
| PUT | `api/warehouse/v1/supplier-item-mappings/{id:int}` | `SupplierItemMappingsController` | ManagerOrAdmin |
| DELETE | `api/warehouse/v1/supplier-item-mappings/{id:int}` | `SupplierItemMappingsController` | ManagerOrAdmin |
| POST | `api/warehouse/v1/transfers` | `TransfersController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/transfers` | `TransfersController` | OperatorOrAbove |
| GET | `api/warehouse/v1/transfers/{id:guid}` | `TransfersController` | OperatorOrAbove |
| POST | `api/warehouse/v1/transfers/{id:guid}/execute` | `TransfersController` | OperatorOrAbove |
| GET | `api/warehouse/v1/unit-of-measures` | `UnitOfMeasuresController` | OperatorOrAbove |
| POST | `api/warehouse/v1/unit-of-measures` | `UnitOfMeasuresController` | ManagerOrAdmin |
| PUT | `api/warehouse/v1/unit-of-measures/{id:int}` | `UnitOfMeasuresController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/item-uom-conversions/item/{itemId:int}` | `ItemUomConversionsController` | OperatorOrAbove |
| POST | `api/warehouse/v1/item-uom-conversions` | `ItemUomConversionsController` | ManagerOrAdmin |
| PUT | `api/warehouse/v1/item-uom-conversions/{id:int}` | `ItemUomConversionsController` | ManagerOrAdmin |
| GET | `api/warehouse/v1/valuation/summary` | `ValuationController` | CfoOrAdmin |
| GET | `api/warehouse/v1/valuation/by-location` | `ValuationController` | CfoOrAdmin |
| POST | `api/warehouse/v1/valuation/revalue` | `ValuationController` | CfoOrAdmin |
| GET | `api/warehouse/v1/visualization/layout/{layoutId:int}` | `WarehouseVisualizationController` | OperatorOrAbove |
| GET | `api/warehouse/v1/visualization/3d/{layoutId:int}` | `WarehouseVisualizationController` | OperatorOrAbove |
| GET | `api/warehouse/v1/admin/approval-rules` | `AdminApprovalRulesController` | AdminOnly |
| POST | `api/warehouse/v1/admin/approval-rules` | `AdminApprovalRulesController` | AdminOnly |
| PUT | `api/warehouse/v1/admin/approval-rules/{id:int}` | `AdminApprovalRulesController` | AdminOnly |
| DELETE | `api/warehouse/v1/admin/approval-rules/{id:int}` | `AdminApprovalRulesController` | AdminOnly |
| GET | `api/warehouse/v1/admin/backups` | `AdminBackupsController` | AdminOnly |
| POST | `api/warehouse/v1/admin/backups/backup-now` | `AdminBackupsController` | AdminOnly |
| POST | `api/warehouse/v1/admin/backups/{id:int}/restore` | `AdminBackupsController` | AdminOnly |
| GET | `api/warehouse/v1/admin/capacity/planning` | `AdminCapacityController` | AdminOnly |
| POST | `api/warehouse/v1/admin/capacity/limits` | `AdminCapacityController` | AdminOnly |
| GET | `api/warehouse/v1/admin/compliance/status` | `AdminComplianceController` | AdminOnly |
| POST | `api/warehouse/v1/admin/compliance/audit-export` | `AdminComplianceController` | AdminOnly |
| GET | `api/warehouse/v1/admin/compliance/transaction-log` | `AdminComplianceController` | AdminOrAuditor |
| GET | `api/warehouse/v1/admin/disaster-recovery/status` | `AdminDisasterRecoveryController` | AdminOnly |
| POST | `api/warehouse/v1/admin/disaster-recovery/drill` | `AdminDisasterRecoveryController` | AdminOnly |
| GET | `api/warehouse/v1/admin/encryption/status` | `AdminEncryptionController` | AdminOnly |
| POST | `api/warehouse/v1/admin/encryption/rotate-keys` | `AdminEncryptionController` | AdminOnly |
| POST | `api/warehouse/v1/admin/gdpr/erasure-request` | `AdminGdprController` | AdminOnly |
| GET | `api/warehouse/v1/admin/gdpr/erasure-status/{requestId}` | `AdminGdprController` | AdminOnly |
| GET | `api/warehouse/v1/admin/gdpr/consent-log` | `AdminGdprController` | AdminOrAuditor |
| GET | `api/warehouse/v1/admin/permissions` | `AdminPermissionsController` | AdminOnly |
| POST | `api/warehouse/v1/admin/permissions/grant` | `AdminPermissionsController` | AdminOnly |
| DELETE | `api/warehouse/v1/admin/permissions/revoke/{id:guid}` | `AdminPermissionsController` | AdminOnly |
| GET | `api/warehouse/v1/admin/reason-codes` | `AdminReasonCodesController` | AdminOnly |
| POST | `api/warehouse/v1/admin/reason-codes` | `AdminReasonCodesController` | AdminOnly |
| PUT | `api/warehouse/v1/admin/reason-codes/{id:int}` | `AdminReasonCodesController` | AdminOnly |
| DELETE | `api/warehouse/v1/admin/reason-codes/{id:int}` | `AdminReasonCodesController` | AdminOnly |
| GET | `api/warehouse/v1/admin/retention-policies` | `AdminRetentionPoliciesController` | AdminOnly |
| POST | `api/warehouse/v1/admin/retention-policies` | `AdminRetentionPoliciesController` | AdminOnly |
| PUT | `api/warehouse/v1/admin/retention-policies/{id:int}` | `AdminRetentionPoliciesController` | AdminOnly |
| GET | `api/warehouse/v1/admin/roles` | `AdminRolesController` | AdminOnly |
| POST | `api/warehouse/v1/admin/roles` | `AdminRolesController` | AdminOnly |
| PUT | `api/warehouse/v1/admin/roles/{id:int}` | `AdminRolesController` | AdminOnly |
| DELETE | `api/warehouse/v1/admin/roles/{id:int}` | `AdminRolesController` | AdminOnly |
| GET | `api/warehouse/v1/admin/settings` | `AdminSettingsController` | AdminOnly |
| PUT | `api/warehouse/v1/admin/settings` | `AdminSettingsController` | AdminOnly |
| GET | `api/warehouse/v1/admin/sla` | `AdminSlaController` | AdminOnly |
| POST | `api/warehouse/v1/admin/sla` | `AdminSlaController` | AdminOnly |
| PUT | `api/warehouse/v1/admin/sla/{id:int}` | `AdminSlaController` | AdminOnly |
| GET | `api/warehouse/v1/admin/sla/metrics` | `AdminSlaController` | ManagerOrAuditor |
| GET | `api/warehouse/v1/handling-unit-types` | `HandlingUnitTypesController` | OperatorOrAbove |
| POST | `api/warehouse/v1/handling-unit-types` | `HandlingUnitTypesController` | ManagerOrAdmin |
| PUT | `api/warehouse/v1/handling-unit-types/{id:int}` | `HandlingUnitTypesController` | ManagerOrAdmin |

---

## Appendix C — Nav Menu Index

Source: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Shared/NavMenu.razor`

| Nav Group | Icon | Menu Item | Route |
|---|---|---|---|
| **Stock** | bi-box-seam | Available Stock | `/available-stock` |
| | | Stock Dashboard | `/warehouse/stock/dashboard` |
| | | Location Balance | `/warehouse/stock/location-balance` |
| | | Adjustments | `/warehouse/stock/adjustments` |
| | | Reservations | `/reservations` |
| **Inbound** | bi-box-arrow-in-down | Inbound Shipments | `/warehouse/inbound/shipments` |
| | | Receiving QC | `/warehouse/inbound/qc` |
| | | Putaway | `/warehouse/putaway` |
| **Outbound** | bi-box-arrow-right | Sales Orders | `/warehouse/sales/orders` |
| | | Allocations | `/warehouse/sales/allocations` |
| | | Outbound Orders | `/warehouse/outbound/orders` |
| | | Dispatch | `/warehouse/outbound/dispatch` |
| | | Wave Picking | `/warehouse/waves` |
| | | Picking Tasks | `/warehouse/picking/tasks` |
| | | Labels | `/warehouse/labels` |
| | | Cross-Dock | `/warehouse/cross-dock` |
| | | RMAs | `/warehouse/rmas` |
| **Operations** | bi-gear | Transfers | `/warehouse/transfers` |
| | | Cycle Counts | `/warehouse/cycle-counts` |
| | | Warehouse Map | `/warehouse/visualization/3d` |
| | | Projections | `/projections` |
| **Finance** | bi-cash-stack | Valuation | `/warehouse/valuation/dashboard` |
| | | Agnum Config | `/warehouse/agnum/config` |
| | | Agnum Reconcile | `/warehouse/agnum/reconcile` |
| **Admin** | bi-shield-lock | Users | `/admin/users` |
| | | Admin Settings | `/warehouse/admin/settings` |
| | | Reason Codes | `/warehouse/admin/reason-codes` |
| | | Approval Rules | `/warehouse/admin/approval-rules` |
| | | Roles | `/warehouse/admin/roles` |
| | | API Keys | `/warehouse/admin/api-keys` |
| | | GDPR Erasure | `/warehouse/admin/gdpr-erasure` |
| | | Audit Logs | `/warehouse/admin/audit-logs` |
| | | Backups | `/warehouse/admin/backups` |
| | | Retention Policies | `/warehouse/admin/retention-policies` |
| | | DR Drills | `/warehouse/admin/dr-drills` |
| | | Serial Numbers | `/warehouse/admin/serial-numbers` |
| | | Lots | `/warehouse/admin/lots` |
| | | Units of Measure | `/warehouse/admin/uom` |
| | | Items | `/admin/items` |
| | | Suppliers | `/admin/suppliers` |
| | | Supplier Mappings | `/admin/supplier-mappings` |
| | | Locations | `/admin/locations` |
| | | Categories | `/admin/categories` |
| | | Import Wizard | `/admin/import` |
| | | Layout Editor | `/warehouse/admin/layout-editor` |
| **Reports** | bi-clipboard-data | Stock Level | `/reports/stock-level` |
| | | Receiving History | `/reports/receiving-history` |
| | | Pick History | `/reports/pick-history` |
| | | Dispatch History | `/reports/dispatch-history` |
| | | Stock Movements | `/reports/stock-movements` |
| | | Traceability | `/reports/traceability` |
| | | Lot Traceability | `/warehouse/compliance/lot-trace` |
| | | Compliance Audit | `/reports/compliance-audit` |
| | | Compliance Dashboard | `/warehouse/compliance/dashboard` |
| **Analytics** | bi-graph-up-arrow | Fulfillment KPIs | `/analytics/fulfillment` |
| | | Quality Analytics | `/analytics/quality` |

---

## Appendix D — Unknown / Not Implemented Yet

The following items have UI or API surface evidence but lack complete implementation detail, or were identified as Phase 2 scope in the architecture docs.

| ID | Item | Evidence | Status |
|---|---|---|---|
| U-01 | **Replenishment rules** — automatic reorder or zone replenishment | No dedicated UI route or controller found for replenishment triggers | Phase 2 / Not implemented |
| U-02 | **AdvancedWarehouseController auth policies** — waves, cross-dock, RMA, serials, HUs endpoints lack explicit auth policy strings in available scan output | `AdvancedWarehouseController.cs` exists; auth policies internal to nested controllers not fully captured | Needs code verification |
| U-03 | **ERP/Kafka inbound integration** — `MaterialRequested` event consumed, `CreateReservation` command issued | Mentioned in CLAUDE.md and docs/04; anti-corruption layer in `Modules.Warehouse.Integration/` | Implemented but no UI; integration-only |
| U-04 | **ReservationTimeoutSaga** — auto-cancel HARD locks after 2-hour policy | Documented as HIGH-02 open item in CLAUDE.md | Phase 2 scope |
| U-05 | **FedEx shipping integration** — `FedExApiService.cs` registered in Program.cs | No UI route or controller endpoint exposes FedEx directly | Integration scaffold only |
| U-06 | **PagerDuty integration** — HTTP client registered in Program.cs | No UI or controller found; alert escalation service mentioned | Integration scaffold only |
| U-07 | **Capacity planning UI** — `AdminCapacityController` exists (`api/warehouse/v1/admin/capacity`) | No Razor page found in UI scan for capacity planning | API exists, UI missing |
| U-08 | **MfaController** endpoints (`api/auth/mfa`) | No dedicated Razor page found for MFA setup in UI scan | Auth flow handled differently |
| U-09 | **Idempotency query endpoint** (`GET api/warehouse/v1/idempotency/{key:guid}`) | `IdempotencyController` exists; no direct UI page | Developer/diagnostic tool only |
| U-10 | **Virtual location phantom stock** (MED-02) — SUPPLIER, PRODUCTION, SCRAP, SYSTEM locations create phantom AvailableStock | Documented open item | Phase 2 fix |
| U-11 | **SLA configuration UI** — `AdminSlaController` exists | No SLA page found in UI scan | API exists, UI unknown |
| U-12 | **Chaos resilience / DR drill detail** — `ChaosResilienceService.cs` registered | DR Drills page exists at `/warehouse/admin/dr-drills`; internal chaos testing detail unclear | Implementation detail unknown |

---

*End of document.*

*Generated: 2026-02-24 | Branch: `docs/process-universe` | Source: repository scan of UI routes, API controllers, Application commands/queries, and docs/*
