# 03. Target Solution Architecture

## Goal

Rework Warehouse Agnum integration from a file-like financial snapshot into an API-based read/import foundation first.

The first business-critical outcome is:

- MES shows the full Agnum product/nomenclature list.
- MES shows stock balances in each Agnum warehouse.
- Each Agnum warehouse/`sndid` is represented as a MES virtual warehouse context.
- Users can distribute imported virtual balances into real MES physical warehouses/locations/bins.
- Users can search products and see where they physically are in 2D/3D.
- Agnum document export is implemented only after this foundation works.

Architectural guardrail: [ADR-006](../adr/006-warehouse-agnum-document-based-integration.md) states that Warehouse-Agnum integration is document-based, not raw `StockMovedEvent` export.

Additional guardrail: phases 0-5 are read/import/distribution only. They must not POST or PUT anything back to Agnum.

## Proposed Components

### 1. Agnum Connector

New connector in the Warehouse integration layer:

- `IAgnumApiClient`
- `AgnumApiClient`
- Typed request/response DTOs for products and balances first.
- Document DTOs later, after the physical MES foundation is working.
- Uses `X-API-KEY`, not Bearer auth.
- Resolves the correct API key by Agnum `sndid`/virtual warehouse context.
- Handles retry, timeout, logging, response parsing, and redacted diagnostics.

The current `IAgnumExportService` is too narrow. Keep it temporarily, but introduce a broader connector rather than stretching "export snapshot" to cover all Agnum behavior.

### 2. Agnum Mapping Model

Add persistent configuration for:

- Agnum `sndid` to MES virtual warehouse context.
- Agnum `sndid` to API key reference.
- Virtual warehouse to allowed target MES physical warehouses/locations.
- Agnum product reference: `(sndid, agnumProductId)` stored against MES `Item`.
- SKU/code mapping and conflict status.
- Unit mapping between MES UoM and Agnum `pcs`.
- Agnum category/classifier import mapping: `group`, `category`, `subgroup`, `direction`, `branch`.
- Agnum custom product attributes: `place`, `f1..f20`.
- Document type mapping later: MES business process to Agnum endpoint and document type fields.

Do not store raw API keys in normal tables. Reuse the existing protected secret approach or move to a secrets provider later.

### 3. Product/Nomenclature Sync

The first implementation direction is Agnum -> MES import:

- Import Agnum products into MES master data per configured `sndid`.
- Treat Agnum `code` as SKU candidate.
- Store Agnum `(sndid, id)` as an external reference.
- Detect duplicates, disabled products, UoM conflicts, and code collisions.
- Do not create/update Agnum products from MES in the import phases.

The current `Item.ProductConfigId` is not enough for this. It is a string and does not capture per-warehouse Agnum identity.

### 4. Virtual Warehouse Balances

Agnum balance is not an `Item` field.

Import Agnum balances into a separate virtual warehouse balance model:

- Agnum `sndid` virtual warehouse.
- Agnum product identity `(sndid, agnumProductId)`.
- MES item/SKU after product import/linking.
- Quantity and UoM.
- Import run/source hash/timestamps.

These balances are used as:

- stock visibility by Agnum warehouse;
- opening stock source;
- reconciliation source;
- source for physical distribution into MES locations.

### 5. Physical Distribution from Virtual Balance

Users need a workflow to distribute imported Agnum virtual balances into real MES physical warehouses/locations/bins.

The distribution workflow should:

- start from an imported virtual balance;
- create a draft distribution document;
- select target physical warehouse/location/bin;
- support partial distribution;
- prevent over-distribution unless explicitly approved as a correction;
- keep traceability to Agnum import run and source balance;
- update MES physical stock through controlled commands.

Physical MES locations are not the same thing as Agnum `sndid`.

### 6. Search and 2D/3D Location Visibility

After physical distribution exists, product search should show:

- Agnum virtual balance by `sndid`;
- distributed MES physical quantity by warehouse/location/bin;
- remaining undistributed virtual balance;
- 2D/3D location visibility using existing Warehouse visualization capabilities.

### 7. Document Export Outbox

Use an explicit outbox table/entity for Agnum exports:

- `AgnumDocumentExport`
- `AgnumDocumentExportLine`
- Status: Pending, Sending, Sent, Failed, Cancelled, NeedsReview.
- Idempotency key: MES document/movement ID plus type.
- Request payload snapshot.
- Agnum response snapshot.
- Retry count and last error.

This is safer than calling Agnum directly from UI/controller command handlers.

This component belongs after product import, virtual balances, physical distribution, and core MES operations are working.

### 8. Flow Handlers

Map MES flows to export candidates:

- Sales order shipment/invoice candidate -> `/api/orders`.
- Purchase receipt/inbound candidate -> `/api/receipts` after validation.
- Customer return -> `/api/customer-returns`.
- Stock adjustment/write-off/transfer -> needs business confirmation; may require Agnum document types not yet confirmed in the API docs.

Do not export every `StockMovedEvent` blindly. The ledger is a technical truth of stock, but Agnum expects accounting/business documents.

### 9. Reconciliation

Keep reconciliation, but expand it:

- Product sync report: MES item vs Agnum product per warehouse.
- Balance comparison: MES available/on-hand vs Agnum `balance` per API key/sndid.
- Virtual-vs-physical distribution report: imported virtual balance, distributed quantity, remaining virtual quantity.
- Document export report: Pending/failed/sent Agnum document exports.
- Accounting value reconciliation remains, but should align with agreed Agnum endpoints and document semantics.

## Suggested Runtime Configuration

Warehouse API config should reference Agnum service with internal URL:

```json
{
  "Agnum": {
    "BaseUrl": "http://agnum-api:8181",
    "TimeoutSeconds": 15
  }
}
```

Per-warehouse API keys should be configured through UI/admin config or secrets:

| MES virtual warehouse context | Agnum sndid | API key secret |
| --- | --- | --- |
| AGNUM-SND-493 | 493 | protected secret |
| AGNUM-SND-498 | 498 | protected secret |

The final mapping names need business confirmation.

## Migration Strategy

Keep current export/reconciliation screens operational while building the new connector. Then:

1. Add new Agnum read connector and mapping tables.
2. Import Agnum products with preview/conflict detection/apply.
3. Import/display Agnum balances as virtual warehouse balances.
4. Add physical distribution from virtual balance into MES warehouses/locations/bins.
5. Add product search and 2D/3D location visibility from physical distribution.
6. Adapt real MES warehouse operations.
7. Add document export outbox and confirmed Agnum document flows.
8. Replace old `JsonApi` snapshot endpoint with real Agnum document endpoints or deprecate it.
