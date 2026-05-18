# 06. Master Data Mapping

This document is the first mapping matrix for Agnum products, warehouses, categories, and suppliers against current Warehouse master data.

## Short Answer

Basic Warehouse fields exist for simple import/export:

- product code/SKU;
- name;
- category;
- base UoM;
- weight;
- active status;
- primary and additional barcodes;
- supplier master;
- supplier item mapping;
- warehouse and location master.

But we do not yet have enough structure for a reliable Agnum read/import and balance foundation:

- no first-class Agnum external product reference `(sndid, ID_PRK)`;
- no first-class Agnum virtual warehouse mapping `(sndid -> MES virtual warehouse context)`;
- no first-class mapping from Agnum virtual warehouse to allowed MES physical warehouses/locations;
- no storage for Agnum product custom fields `group`, `category`, `subgroup`, `direction`, `branch`, `place`, `f1..f20`;
- no virtual warehouse balance import table;
- no distribution workflow from imported virtual balances to physical MES bins;
- no clear supplier import source from Agnum API yet;
- no history/status table for product import/export runs;
- no raw Agnum payload snapshot for troubleshooting.

So: simple mapping can be done now, production-grade mapping needs migrations.

## Agnum Product to Warehouse Item

| Agnum field | Meaning | Current Warehouse target | Exists now? | Recommendation |
| --- | --- | --- | --- | --- |
| `id` | `PRK.ID_PRK`; unique only inside `sndid` context | No safe target. `Item.ProductConfigId` is too weak. | Partial | Add `ItemExternalReference` or `AgnumProductLink` with `ItemId`, `SndId`, `AgnumProductId`, `ApiUserKeyName`, timestamps. |
| `code` | Product code, max 25, required | `Item.InternalSKU` | Yes | Use as primary SKU for import/export unless business confirms different SKU source. |
| `name` | Product name | `Item.Name` | Yes | Direct mapping. |
| `enabled` | Active flag | `Item.Status` | Yes | `true -> Active`, `false -> Discontinued` or `Obsolete`; confirm final status rule. |
| `pcs` | UoM | `Item.BaseUoM` | Yes | Needs UoM normalization table, because Agnum values include lower-case/free-form values like `vnt`, `m`, `m2`, `kompl`. |
| `barcode` / `barcodes` | Product barcodes | `Item.PrimaryBarcode`, `ItemBarcode` | Yes | Import first barcode as primary, all as `ItemBarcode`; importer must accept both field names. |
| `netto` | Net weight | `Item.Weight` | Yes | Direct mapping if unit is kg; confirm. |
| `brutto` | Gross weight | No separate field | No | Add `GrossWeight` or store in custom attributes. Current `Weight` can only hold one weight. |
| `balance` | Agnum current qty for API key's `sndid` | Separate virtual warehouse balance model | No | Do not write to `Item`. Import as `AgnumVirtualWarehouseBalance` and use as virtual balance/opening stock/reconciliation source. |
| `modify_date` | Agnum modified timestamp | No external sync field | No | Store on external reference or sync state. |
| `create_date` | Agnum created timestamp | `Item.CreatedAt` only for MES create time | Partial | Keep as Agnum metadata on external reference, not as MES `CreatedAt`. |
| `place` | Agnum storage/bin place | `Location.Code` or `Location.Bin` | Partial | Use only if business says Agnum `place` is maintained. Needs mapping table, not direct overwrite. |
| `group` | Product group | `ItemCategory` or custom classification | Partial | Could map to category level 1, but current category has only one hierarchy. Confirm whether `group/category/subgroup` should become hierarchy. |
| `category` | Product category | `ItemCategory` | Partial | Same as above. Current `ItemCategory` can support hierarchy but not multiple independent classifier dimensions. |
| `subgroup` | Product subgroup | `ItemCategory` | Partial | Same as above. |
| `direction` | Product direction/classifier | None | No | Add custom attributes/classifiers if needed. |
| `branch` | Product branch/classifier | None | No | Add custom attributes/classifiers if needed. |
| `f1` | Intrastat commodity code | None | No | Add `ItemAttribute` or explicit `IntrastatCode`. |
| `f2` | Supplier-provided product code | `SupplierItemMapping.SupplierSKU` | Partial | Needs supplier identity/source. If no supplier in payload, store as attribute or create pending supplier mapping. |
| `f3..f5` | Custom text fields | None | No | Add flexible attributes if business wants retention. |
| `f6` | Intrastat quantity coefficient | None | No | Add `ItemAttribute` or explicit numeric field. |
| `f7..f10` | Custom numeric fields | None | No | Add flexible attributes if business wants retention. |
| `f11..f15` | Custom text fields | None | No | Add flexible attributes if business wants retention. |
| `f16..f20` | Custom numeric fields | None | No | Add flexible attributes if business wants retention. |

## Proposed Product Extension

Minimum production-safe model:

```text
AgnumProductLink
- Id
- ItemId
- SndId
- AgnumProductId
- AgnumCode
- AgnumEnabled
- AgnumModifiedAt
- LastImportedAt
- LastExportedAt
- SourceApiUserKeyName
- RawHash
- IsPrimary
Unique: (SndId, AgnumProductId)
Unique: (SndId, AgnumCode)
```

For custom fields, prefer a generic table first:

```text
ItemExternalAttribute
- Id
- ItemId
- SourceSystem = AGNUM
- SourceContext = sndid or api user
- Key = group/category/subgroup/direction/branch/place/f1...
- ValueText
- ValueNumber
- ValueDate
Unique: (ItemId, SourceSystem, SourceContext, Key)
```

Later, promote heavily used fields to explicit columns only after business confirms they are operationally important.

## Agnum Warehouses to MES Virtual Warehouses

Agnum warehouse/store context comes from API user `sndid`, not from a request field.

Rule:

- Agnum `sndid` becomes a MES virtual warehouse context.
- Physical MES locations are not the same as Agnum warehouses.
- A virtual balance can later be distributed into one or more real MES physical warehouses/locations/bins.

Known `sndid` values from test DB:

| Agnum sndid | Agnum name | Likely target in MES | Exists now? | Recommendation |
| --- | --- | --- | --- | --- |
| 493 | Sandelys / Centrinis sandelys | MES virtual warehouse, later distributable to real physical locations | Not configured | Add virtual mapping. |
| 496 | Pardavimai / Pagaminta produkcija-pardavimai | MES virtual warehouse, later distributable to real physical locations | Not configured | Add virtual mapping. |
| 498 | Gamyba / Gamybos sandelys | MES virtual warehouse, later distributable to real physical locations | Not configured | Add virtual mapping. |
| 500 | Mazavertis | Low-value inventory | Unknown | Business confirmation needed. |
| 502 | Kuras | Fuel/transport | Unknown | Probably not core WMS stock; confirm. |
| 507 | Ilgalaikis | Fixed assets | Unknown | Probably not core WMS stock; confirm. |
| 509 | Visi | All warehouses | No direct MES warehouse | Do not map as operational warehouse unless Agnum confirms semantics. |
| 1498 | Nebaigta statyba | WIP/construction | Unknown | Confirm. |
| 12503 | Paslaugos | Services | Not stock warehouse | Likely exclude from WMS item import. |
| 142026 | PVZ | Samples | Unknown | Confirm. |
| 142029 | Parduotuve | Online shop/store | Unknown | Confirm. |

Needed model:

```text
AgnumWarehouseMapping
- Id
- SndId
- AgnumName
- AgnumDescription
- MesVirtualWarehouseCode
- MesVirtualWarehouseName
- DefaultTargetPhysicalWarehouseId (optional)
- DefaultTargetLocationId (optional)
- AllowedPhysicalWarehouseIds or separate child table
- ApiKeySecretRef
- IsImportEnabled
- IsDistributionEnabled
- IsExportEnabled (later)
- IsBalanceReconciliationEnabled
- Notes
Unique: SndId
```

Current `Location.WarehouseId` exists and `WarehouseLayoutAggregate`/warehouse directory exists, but there is no Agnum `sndid` virtual mapping table.

## Agnum Balances to Virtual Warehouse Balances

Agnum balance belongs to the virtual warehouse context, not to `Item`.

Needed model:

```text
AgnumBalanceImportRun
- Id
- StartedAt
- FinishedAt
- Status
- SndId
- ProductCount
- BalanceCount
- ErrorCount
- SourceHash

AgnumVirtualWarehouseBalance
- Id
- ImportRunId
- SndId
- AgnumProductId
- ItemId
- Sku
- Quantity
- Uom
- ImportedAt
- SourceHash
Unique: (ImportRunId, SndId, AgnumProductId)
```

Physical distribution should consume from these records through a separate workflow/document, not by rewriting imported Agnum balances.

## Agnum Categories to Warehouse Categories

Agnum product classification exposes multiple dimensions:

- `group`
- `category`
- `subgroup`
- `direction`
- `branch`

Warehouse currently has one hierarchical `ItemCategory`:

- `Code`
- `Name`
- `ParentCategoryId`

Mapping options:

1. Simple hierarchy: `group -> category -> subgroup`.
2. Keep `direction` and `branch` as external attributes.
3. Build a separate classifier model if these dimensions are operationally important.

Recommended MVP:

- Import `group/category/subgroup` into `ItemCategory` hierarchy.
- Generate stable category codes from normalized Agnum values plus hash when needed.
- Store original Agnum classifier values in `ItemExternalAttribute`.
- Do not force `direction`/`branch` into `ItemCategory` until confirmed.

Potential gaps:

- Current category code max/normalization rules may need Lithuanian-safe slug generation.
- Need source context so MES-created categories and Agnum-imported categories do not collide.

## Agnum Suppliers to Warehouse Suppliers

Current Warehouse supplier model:

- `Supplier.Code`
- `Supplier.Name`
- `Supplier.ContactInfo`

Current supplier-item mapping:

- `SupplierItemMapping.SupplierId`
- `SupplierItemMapping.SupplierSKU`
- `SupplierItemMapping.ItemId`
- `LeadTimeDays`
- `MinOrderQty`
- `PricePerUnit`

Agnum product field `f2` is supplier-provided product code, but product payload does not identify the supplier.

Therefore:

- `f2 -> SupplierItemMapping.SupplierSKU` is only possible if we can identify supplier by another Agnum source.
- Without supplier identity, store `f2` as `ItemExternalAttribute(key=f2)` and mark supplier mapping as unresolved.

Need to inspect/confirm Agnum API supplier endpoints. Current reviewed docs mention client lookup, products, documents, but not a clear supplier/vendor master endpoint.

## Import/Export Field Availability

Current Excel master-data import supports:

- Items: `InternalSKU`, `Name`, `Description`, `CategoryCode`, `BaseUoM`, `Weight`, `Volume`, `RequiresLotTracking`, `RequiresQC`, `Status`, `PrimaryBarcode`, `ProductConfigId`
- Suppliers: `Code`, `Name`, `ContactInfo`
- Supplier mappings: `SupplierCode`, `SupplierSKU`, `ItemSKU`, `LeadTimeDays`, `MinOrderQty`, `PricePerUnit`
- Barcodes: `ItemSKU`, `Barcode`, `BarcodeType`, `IsPrimary`
- Locations: `Code`, `Barcode`, `Type`, `ParentCode`, `IsVirtual`, `MaxWeight`, `MaxVolume`, `Status`, `ZoneType`

For Agnum import this is not enough because it lacks:

- `SndId`;
- `AgnumProductId`;
- Agnum `enabled` source state;
- Agnum timestamps;
- product classifier dimensions;
- custom fields `f1..f20`;
- virtual balance quantity by `sndid`;
- physical distribution status/remaining quantity;
- raw payload hash/source metadata.

## Recommended Next Implementation Tasks

1. Add migrations/entities for `AgnumWarehouseMapping`, `AgnumProductLink`, `ItemExternalAttribute`, `AgnumBalanceImportRun`, and `AgnumVirtualWarehouseBalance`.
2. Add DTO mapping document/tests for `AgnumProductDto -> ItemImportCandidate`.
3. Add read-only product import preview from Agnum by configured `sndid`.
4. Add conflict report before writing any MES master data.
5. Add UoM mapping table for Agnum `pcs -> UnitOfMeasure.Code`.
6. Add category import strategy and category-code generator.
7. Add virtual balance import/display before any export back to Agnum.
8. Add distribution workflow from virtual balance into physical MES locations.
9. Decide supplier source; keep `f2` as unresolved supplier SKU until supplier identity is available.
