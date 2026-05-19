# Agnum Nomenclature Import Fix Blueprint

## Goal

Fix the Agnum nomenclature import so it can:
- fully import product fields from Agnum,
- create and maintain the local FK reference data needed for import,
- import suppliers from Agnum as a supplier reference catalog,
- create missing UoM records during import,
- build category hierarchy without committing halfway through a live import,
- preserve a clean import path for a wiped inventory/catalog database.

## Scope

This blueprint covers changes to:
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Agnum/AgnumNomenclatureImportService.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/Agnum/IAgnumApiClient.cs`
- unit tests in `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/`

It also includes the required cleanup SQL for a fully clean import database.

## Background

Current import behavior is partially broken:
- Agnum API fallback was fixed, so product retrieval works.
- `ItemExternalAttribute` FK is now linked correctly.
- Import still rejects or breaks on missing UoM and does not import suppliers.
- `GetOrCreateCategoryAsync()` still `SaveChangesAsync()` inside helper logic, which can fail when previous uncaptured items are present.

## Implementation Plan

### 1. Extend Agnum DTO

Update `AgnumProductDto` in `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/Agnum/IAgnumApiClient.cs` to include:
- Supplier-related fields: `SupplierCode`, `SupplierName`, or the actual Agnum payload names.
- Any additional Agnum metadata that must be imported, including UoM type if available.

### 2. Stop rejecting unknown UoM in preview

In `AgnumNomenclatureImportService.PreviewAsync()`:
- remove `UnknownUoM` as an import blocker if the service can create UoM records.
- preserve conflict detection for duplicate SKU and linked-to-different-item.

### 3. Add UoM creation helper

Add a helper method in `AgnumNomenclatureImportService`:
- `Task<UnitOfMeasure> EnsureUnitOfMeasureAsync(string code, string? type, CancellationToken ct)`
- Normalize codes to the same format used by the DB.
- If the code exists, return the existing record.
- If it does not exist, create a new `UnitOfMeasure` record and attach it to the context.
- Default `Type` sensibly when Agnum does not provide it: `Piece` for typical piece units, otherwise infer from common codes.

### 4. Add supplier creation helper

Add a supplier helper:
- `Task<Supplier> EnsureSupplierAsync(string code, string? name, CancellationToken ct)`
- If the supplier exists, return it.
- If not, create a new `Supplier` record.
- Optionally create `SupplierItemMapping` for `Item` + supplier SKU if that mapping is needed by downstream flows.

### 5. Refactor category creation

Change `GetOrCreateCategoryAsync()` to:
- return `ItemCategory` instead of `int`
- remove `await _dbContext.SaveChangesAsync(ct)` from inside the helper
- add the category to the DbContext and return it
- continue to normalize category codes consistently

Change `EnsureCategoryHierarchyAsync()` so it builds a chain of `ItemCategory` objects and returns the leaf category.

### 6. Update `CreateProductAsync()` to use navigation properties

Refactor `CreateProductAsync()` so it:
1. loads/creates category via `EnsureCategoryHierarchyAsync()`
2. loads/creates UoM via `EnsureUnitOfMeasureAsync(product.Pcs, maybeType, ct)`
3. loads/creates supplier via `EnsureSupplierAsync(...)`
4. creates `Item` and sets:
   - `InternalSKU`
   - `Name`
   - `Status`
   - `Weight` / `Volume`
   - `BaseUoM = product.Pcs`
   - `BaseUnit = unitOfMeasure`
   - `Category = category`
   - `Supplier = supplier` or `SupplierId` if needed
5. adds the item to `_dbContext.Items`
6. adds item barcodes
7. adds `AgnumProductLink` with `Item = item`
8. adds external attributes with `attribute.Item = item`

### 7. Keep save timing controlled

Ensure the import does not call `SaveChangesAsync()` inside `GetOrCreateCategoryAsync()` or other helper methods that are used while previous `Item` entities are still pending.

Preferred pattern:
- accumulate changes for one product in the current DbContext transaction,
- optionally `SaveChangesAsync()` after each product if batch isolation is desired,
- or save once per import run if the code path is safe.

### 8. Add missing supplier import into the Agnum product flow

If the Agnum payload includes supplier information for a product, import it and link it to the created item.

Potential mapping logic:
- `product.SupplierCode` → `Supplier.Code`
- `product.SupplierName` → `Supplier.Name`
- `SupplierItemMapping.SupplierId` + `SupplierSKU` if Agnum provides a supplier SKU for the item.

### 9. Validation and tests

Add or update tests for:
- UoM creation when `product.Pcs` is missing from `unit_of_measures`
- supplier creation from Agnum fields
- `CreateProductAsync()` creating category, UoM, supplier, item, barcodes, and link records
- ensuring `GetOrCreateCategoryAsync()` does not commit mid-import
- ensuring external attributes are linked with `attribute.Item = item`

### 10. Clean database before import

Add a SQL cleanup snippet in the doc or run manually before import.

Full wipe for import domain:

```sql
BEGIN;

TRUNCATE TABLE public.item_uom_conversions RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.agnum_product_links RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.item_external_attributes RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.item_barcodes RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.item_photos RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.on_hand_value RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.items RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.item_categories RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.supplier_item_mappings RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.suppliers RESTART IDENTITY CASCADE;
TRUNCATE TABLE public.unit_of_measures RESTART IDENTITY CASCADE;

COMMIT;
```

If you need to keep a base UoM catalog, remove the `unit_of_measures` truncate and reinsert base rows separately.

## Implementation prompt for another AI

Use this prompt if you want another AI to make the code changes directly:

> Update the Agnum nomenclature import service in `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/Agnum/AgnumNomenclatureImportService.cs` and the Agnum DTO in `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/Agnum/IAgnumApiClient.cs` with the following behavior:
>
> 1. Extend `AgnumProductDto` with supplier fields and, if available from Agnum, UoM type metadata.
> 2. In `PreviewAsync()`, stop blocking import for UoM codes that are not present in `unit_of_measures` if the service can create them.
> 3. Add `EnsureUnitOfMeasureAsync(string code, string? type, CancellationToken ct)` to create missing UoM records during import.
> 4. Add `EnsureSupplierAsync(string code, string? name, CancellationToken ct)` to create missing suppliers and return the supplier entity.
> 5. Refactor category creation so `GetOrCreateCategoryAsync()` returns `ItemCategory`, does not call `SaveChangesAsync()` inside the helper, and builds category hierarchy safely.
> 6. In `CreateProductAsync()`, create/find category, UoM, and supplier, then build the `Item` with navigation properties: `Category`, `BaseUnit`, `Supplier` or supplier mapping. Attach `ItemBarcode`, `AgnumProductLink`, and `ItemExternalAttribute` correctly.
> 7. Ensure external attributes are linked via `attribute.Item = item` before adding them to the DbContext.
> 8. Add tests for UoM creation, supplier creation, safe category creation, and the full import flow.
>
> Keep the solution consistent with existing EF Core model conventions and avoid committing the DbContext mid-import.

---

File path: `docs/agnum-integration-plan/07-agnum-import-service-fix-blueprint.md`
