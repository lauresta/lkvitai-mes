# 01. As-Is Warehouse

## High-Level Shape

Warehouse is a modular monolith under `src/Modules/Warehouse`:

- `Domain`: master data entities and core aggregates.
- `Application`: commands, query handlers, orchestration ports.
- `Infrastructure`: EF Core, Marten/event-store repositories, projections, imports.
- `Contracts`: events, messages, read-model DTOs.
- `Api`: ASP.NET controllers, command handlers, integration services, Hangfire jobs.
- `WebUI`: Blazor Server UI.
- `Sagas`: MassTransit state machines.
- `Integration`: external integration ports.

## Core Business Concepts Already Present

Master data:

- `Item` with `InternalSKU`, `Name`, `Description`, `CategoryId`, `BaseUoM`, `Weight`, `Volume`, lot/QC flags, `PrimaryBarcode`, `ProductConfigId`.
- `ItemCategory`, `UnitOfMeasure`, `ItemUoMConversion`, `ItemBarcode`, `Supplier`, `SupplierItemMapping`.
- `Location` with code, barcode, type, virtual flag, coordinates, rack/bin attributes, role, warehouse ownership.
- `WarehouseLayout`/warehouse directory style data.

Inventory ledger:

- `StockLedger` is event-sourced and append-only.
- One stream per `(warehouseId, location, sku)` via `StockLedgerStreamId`.
- `StockMovedEvent` contains `MovementId`, `SKU`, `Quantity`, `FromLocation`, `ToLocation`, `MovementType`, `OperatorId`, optional HU/reason.
- Negative balance protection lives in `StockLedger.RecordMovement`.

Operational flows:

- Receiving goods records stock movement from `SUPPLIER` to location.
- Picking records stock movement before reservation consumption.
- Transfers have entity workflow: Draft, PendingApproval, Approved, InTransit, Completed, Cancelled.
- Cycle counts and adjustments exist.
- Valuation exists through `OnHandValue`, with item/category, quantity, unit cost, total value.

## Current Agnum Implementation

Current Agnum code is mostly a financial snapshot export/reconciliation feature.

API routes in `AgnumController`:

- `GET /api/warehouse/v1/agnum/config`
- `PUT /api/warehouse/v1/agnum/config`
- `POST /api/warehouse/v1/agnum/test-connection`
- `POST /api/warehouse/v1/agnum/export`
- `GET /api/warehouse/v1/agnum/history`
- `GET /api/warehouse/v1/agnum/history/{exportId}`
- `POST /api/warehouse/v1/agnum/reconcile`
- `GET /api/warehouse/v1/agnum/reconcile/{reportId}`

Configuration:

- Export scope: `ByWarehouse`, `ByCategory`, `ByLogicalWh`, `TotalOnly`.
- Export format: `Csv` or `JsonApi`.
- API endpoint and API key stored in `AgnumExportConfig`.
- Mapping table maps source type/value to Agnum account code.
- Schedule is stored as cron and registered in Hangfire as `agnum-daily-export`.

Export:

- `AgnumExportOrchestrator` loads `OnHandValue` and, when available, `AvailableStockView`.
- Builds CSV with columns: `ExportDate`, `AccountCode`, `SKU`, `ItemName`, `Quantity`, `UnitCost`, `OnHandValue`.
- If configured as `JsonApi`, posts grouped payloads to `api/v1/inventory/import`.
- Sends `Authorization: Bearer <apiKey>` and `X-Export-ID`.
- Maintains `AgnumExportHistory`.

Reconciliation:

- Reads latest successful export CSV for a date.
- Uploads Agnum balance CSV.
- Compares warehouse value against Agnum balance by SKU.
- Stores reconciliation report in memory.

Saga/events:

- `AgnumExportStartedEvent`, `AgnumExportCompletedEvent`, `AgnumExportFailedEvent`.
- `StartAgnumExport`, `AgnumExportSucceeded`, `AgnumExportFailed`.
- `AgnumExportSaga` only tracks state; it does not own the actual HTTP workflow.

## Main As-Is Gap

The current Agnum integration assumes a batch stock valuation export to Agnum. It does not yet model Agnum as the operational accounting system API for:

- Product/nomenclature synchronization.
- Per-Agnum-warehouse product visibility and balances.
- Sales/order document export.
- Purchase receipt or inbound document export.
- Customer returns.
- Mapping MES stock movements to Agnum document types.
- One API key per `sndid` warehouse context.

The existing `JsonApi` endpoint path `api/v1/inventory/import` does not match the real API findings from `agnum-api-deploy`.

