# 04. Implementation Plan

> **Current MVP scope:** Phases 1–3 are active (Slice 1). Phase 4 is Slice 2.
> Phase 5 is Slice 3. **Phase 6 is not an implementation task** — packages A–F already implement
> all MES warehouse operations; no changes are needed there.
> **Phase 7 is backlog** — do not start until Phases 1–5 are stable.
> See `MVP-SCOPE.md` for the authoritative scope gate and frozen file list.

## Revised Business Priority

The first critical goal is not Agnum document export.

The first critical goal is to import and see the full Agnum nomenclature and stock balances in MES:

- MES shows the full product/nomenclature list from Agnum.
- MES shows how much stock exists in each Agnum warehouse.
- Agnum warehouse/`sndid` is treated as a MES virtual warehouse context.
- Users can correct/distribute those virtual balances into real MES physical warehouses and locations.
- Users can search products and see where they are located in 2D/3D.
- Only after this foundation works do we implement receiving new goods and Agnum accounting documents.

## Guardrails

- Agnum `sndid` = MES virtual warehouse context.
- Physical MES locations are not the same as Agnum warehouses.
- Agnum balance is not stored on `Item`.
- Agnum balance is imported as virtual warehouse balance, opening stock source, and reconciliation source.
- Product identity from Agnum is `(sndid, agnumProductId)`, not `agnumProductId` alone.
- Agnum `code` is the SKU candidate unless business confirms otherwise.
- Do not export raw `StockMovedEvent` to Agnum.
- Do not start with UI-only screens; start with data model, import, mapping, conflict detection, and balance visibility.
- Do not post anything back to Agnum in phases 0-5.

## Phase 0 - Confirm Agnum Warehouse/Sndid Mapping and Product Identity Rules

Deliverables:

- Confirm Agnum test URL reachable from Warehouse API/container network.
- Confirm API keys for each `sndid`.
- Confirm which Agnum `sndid` values are in Warehouse scope.
- Confirm whether each `sndid` should become a MES virtual warehouse.
- Confirm product identity rule: `(sndid, agnumProductId)` plus Agnum `code` as SKU candidate.
- Confirm how to handle the same Agnum `code` appearing in multiple `sndid` contexts.
- Confirm UoM mapping rules for Agnum `pcs`.
- Confirm whether Agnum `group/category/subgroup` should become MES category hierarchy.

Output:

- Seedable `AgnumWarehouseMapping` list.
- Product identity decision.
- UoM mapping decision.
- Category mapping decision.
- Clear exclusion list for non-WMS Agnum warehouses such as services/fixed assets if applicable.

## Phase 1 - Build Agnum Read Connector for Products and Balances

Implement:

- `IAgnumApiClient` with `X-API-KEY` auth per `sndid`.
- Product read DTOs for `/api/products/search`, `/api/products/{id}`, and balance-bearing product responses.
- Defensive JSON handling for both `barcode` and `barcodes`.
- Read-only connector methods:
  - search products by code/name;
  - load all visible products for a configured `sndid`;
  - load product by Agnum ID;
  - load balances per `sndid`.
- Timeout, retry, logging, redacted diagnostics.
- Integration tests with mocked Agnum responses.

Avoid:

- Product pagination until Agnum jar `1.39` `limit/offset` bug is fixed.
- Any POST/PUT back to Agnum.
- Any UI-only implementation before connector/data model exists.

## Phase 2 - Import Agnum Products into MES Item Master

Implement data model first:

- `AgnumProductLink`
  - `ItemId`
  - `SndId`
  - `AgnumProductId`
  - `AgnumCode`
  - `AgnumEnabled`
  - Agnum create/modify timestamps
  - import timestamps/source hash
- `ItemExternalAttribute` for Agnum `group/category/subgroup/direction/branch/place/f1..f20`.
- `AgnumUomMapping` for Agnum `pcs -> UnitOfMeasure.Code`.

Implement import workflow:

- Read products from each enabled `sndid`.
- Build import preview before applying changes.
- Detect conflicts:
  - missing SKU;
  - duplicate SKU in same `sndid`;
  - same Agnum `code` across multiple `sndid` contexts;
  - UoM not mapped;
  - category/classifier not mapped;
  - active/inactive status mismatch;
  - existing MES item linked to different Agnum product.
- Apply approved import into `Item`, `ItemBarcode`, `ItemCategory`, `AgnumProductLink`, and external attributes.

Important:

- Import product master only; do not store Agnum balance on `Item`.
- Do not create/update Agnum products from MES in this phase.

## Phase 3 - Import/Display Agnum Balances as MES Virtual Warehouse Balances

Implement data model:

- `AgnumVirtualWarehouseBalance`
  - `SndId`
  - `AgnumProductId`
  - `ItemId`
  - `Sku`
  - `Quantity`
  - `Uom`
  - `ImportedAt`
  - `SourceHash`
  - `ImportRunId`
- `AgnumBalanceImportRun`
  - status, counts, started/finished timestamps, errors.

Behavior:

- Import balances per configured `sndid`.
- Show balances grouped by Agnum virtual warehouse.
- Keep imported balances separate from physical MES stock/location balances.
- Use imported balances as:
  - initial stock source;
  - virtual warehouse visibility;
  - reconciliation baseline.

Do not:

- Write Agnum balances to `Item`.
- Treat Agnum `sndid` as a physical bin/location.
- Post any stock movement or correction to Agnum.

## Phase 4 - Distribute Imported Virtual Balances into Physical MES Locations/Bins

Implement:

- Distribution workflow from `AgnumVirtualWarehouseBalance` to physical MES warehouse/location/bin.
- Draft distribution document:
  - source `sndid` virtual warehouse;
  - target MES physical warehouse/location;
  - item/SKU;
  - quantity;
  - operator and reason;
  - validation and approval state.
- Apply distribution by creating MES physical stock/opening balance through controlled commands.
- Keep traceability from physical stock to Agnum import run/source balance.
- Support partial distribution and remaining virtual balance.
- Prevent over-distribution beyond imported virtual balance unless explicitly approved as correction.

Business meaning:

- This is the bridge from Agnum accounting/store balance to actual MES physical reality.
- Physical MES locations are created/managed separately from Agnum `sndid`.

## Phase 5 - Product Search and 2D/3D Location Visibility

Implement after physical distribution exists:

- Search products by Agnum code/MES SKU/name/barcode.
- Show virtual balance by `sndid`.
- Show physical quantity by MES warehouse/location/bin.
- Show where the product is located in existing 2D/3D visualization.
- Support discrepancy view:
  - imported virtual balance;
  - distributed physical quantity;
  - remaining undistributed virtual quantity;
  - MES operational physical quantity.

UI follows Warehouse UX rules and must be backed by real imported/distributed data, not static screens.

## Phase 6 - Real MES Warehouse Operations

Implement or adapt operational workflows after initial master/balance/location foundation:

- receiving new goods;
- internal movement;
- correction/adjustment;
- inventory/cycle count;
- reservation;
- picking.

Rules:

- MES `StockLedger` remains operational stock truth for physical MES stock.
- Operations work on physical MES warehouses/locations, not directly on Agnum `sndid`.
- Agnum imported balances remain reconciliation/opening-source data.

## Phase 7 - Agnum Document Export for Confirmed Flows

Only after phases 0-6 are working, implement exports back to Agnum.

Confirmed export candidates:

- purchase receipts;
- sales documents;
- customer returns;
- write-offs/corrections.

Implement:

- `AgnumDocumentExport` outbox.
- Mapped Agnum endpoint/document type.
- Idempotency key.
- Payload snapshot.
- Export status.
- Retry/review workflow.
- Operator-facing failed export review.

Rules:

- Do not export raw `StockMovedEvent`.
- Build Agnum documents from confirmed business processes/documents.
- Preserve ADR-006 as the architectural guardrail.

