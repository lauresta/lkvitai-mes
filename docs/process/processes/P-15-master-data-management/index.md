# P-15 — Master Data Management

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Creates and maintains the reference data that all warehouse processes depend on: items (SKUs), suppliers, locations, categories, units of measure, and supplier-item mappings.

**Evidence:**
- UI: `AdminItems.razor`, `AdminSuppliers.razor`, `AdminLocations.razor`, `AdminCategories.razor`, `AdminImport.razor`, `Admin/UnitsOfMeasure.razor`, `Admin/LayoutEditor.razor`
- Controllers: `ItemsController`, `SuppliersController`, `LocationsController`, `CategoriesController`, `UnitOfMeasuresController`, `ItemUomConversionsController`, `HandlingUnitTypesController`, `ImportController`, `BarcodesController`
- Docs: `docs/master-data/` directory (6 files: overview, domain model, API contracts, events/projections, UI scope, implementation plan)
- Known tech debt: `MasterDataEntities.cs` (~1400 LOC god object — ARCH-01)

---

## Trigger

New product, supplier, or warehouse location onboarded; UoM conversion needed; bulk import.

## Outcomes

- Master data records created/updated
- Changes immediately available to all dependent processes

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Manager | Creates/edits items, locations, categories |
| Administrator | Full CRUD including delete; manages UoM and layout |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/admin/items` | `AdminItems.razor` | Admin → Items |
| `/admin/suppliers` | `AdminSuppliers.razor` | Admin → Suppliers |
| `/admin/supplier-mappings` | `AdminSupplierMappings.razor` | Admin → Supplier Mappings |
| `/admin/locations` | `AdminLocations.razor` | Admin → Locations |
| `/admin/categories` | `AdminCategories.razor` | Admin → Categories |
| `/admin/import` | `AdminImport.razor` | Admin → Import Wizard |
| `/warehouse/admin/uom` | `Admin/UnitsOfMeasure.razor` | Admin → Units of Measure |
| `/warehouse/admin/layout-editor` | `Admin/LayoutEditor.razor` | Admin → Layout Editor |

## Subprocesses Used

- SP-12 Layout Editor / Zone Setup
- SP-13 UoM Conversion Setup

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET/POST/PUT/DELETE | `api/warehouse/v1/items` | ItemsController | OperatorOrAbove → AdminOnly |
| GET/POST/PUT | `api/warehouse/v1/suppliers` | SuppliersController | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT/DELETE | `api/warehouse/v1/supplier-item-mappings` | SupplierItemMappingsController | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/locations` | LocationsController | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/categories` | CategoriesController | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/unit-of-measures` | UnitOfMeasuresController | OperatorOrAbove / ManagerOrAdmin |
| GET/POST/PUT | `api/warehouse/v1/item-uom-conversions` | ItemUomConversionsController | OperatorOrAbove / ManagerOrAdmin |
| POST | `api/warehouse/v1/import/csv-upload` | ImportController | ManagerOrAdmin |
| POST | `api/warehouse/v1/barcodes/lookup` | BarcodesController | OperatorOrAbove |

## Key Domain Objects

`Item`, `Supplier`, `SupplierItemMapping`, `WarehouseLocation`, `Category`, `UnitOfMeasure`, `ItemUomConversion`, `HandlingUnitType`, `Customer`

## Known Tech Debt

- `MasterDataEntities.cs` (~1400 LOC, 50+ entities, 8+ bounded contexts) — must be decomposed before module extraction (ARCH-01)

## Docs Reference

`docs/master-data/master-data-00-overview.md` through `master-data-05-implementation-plan-and-tests.md`

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
