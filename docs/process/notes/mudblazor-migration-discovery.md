# MudBlazor Migration Discovery

Date: 2026-03-03  
Repo path (actual session): `/Users/bykovas/CodeRepos/clients/lauresta/LKvitai.MES`  
Branch (actual session): `main`

## Block 1 - Baseline

### 1) What the MudBlazor spike already changed (from `feature/ui-lib-spike-mudblazor` lineage)

Verified spike commits and follow-up hardening in git history:
- `708d34f` - add MudBlazor and register services (spike)
- `8b25ff3` - add Mud providers and minimal layout support (spike)
- `d7d9de5` - migrate Lots page to `MudDataGrid` + `ServerData` (spike)
- `dc9e183` - migrate Available Stock page to `MudDataGrid` (spike)
- `823f401` - fix conflicting `MudTextField` attrs on Lots
- `db44226` - stabilize Lots page-size selector against Mud DOM
- `07ae700` - close Mud overlay before Lots grid actions in UI tests
- `40233a4` - pin MudBlazor version for deterministic restore

Merged via PR chain:
- Merge `4cf3fdb` (PR #15, spike infra + first Mud migration work)
- Merge `c7288e5` (PR #16, Mud package/runtime stabilization)
- Merge `91aadcc` (PR #17, continuation on spike branch)

Current verified baseline in code:
- MudBlazor package referenced in WebUI (`MudBlazor`) and services wired in `Program.cs`.
- Providers enabled in `App.razor`: `MudThemeProvider`, `MudDialogProvider`, `MudSnackbarProvider`.
- Mud assets loaded in `_Layout.cshtml`.
- `/warehouse/admin/lots` and `/available-stock` have Mud grid-based flows covered by E2E tests.

### 2) data-testid and Playwright E2E presence

Current footprint:
- `data-testid` occurrences in WebUI Razor files: **34** (from ripgrep count).
- Dedicated UI E2E project present: `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E`.
- Mud smoke-style full-flow tests present:
  - `MudBlazorGridFullFlowTests.Lots_FullFlow`
  - `MudBlazorGridFullFlowTests.AvailableStock_FullFlow`

Baseline conclusion:
- MudBlazor foundation is real and test-backed, but migration is partial and still mixed with Bootstrap + legacy wrappers.

## Block 2 - Route Inventory

Source of truth: `Shared/NavMenu.razor`.

### 1) Navigation inventory by area

- Standalone top item:
  - `/dashboard`
- Stock (6):
  - `/available-stock`, `/search-by-image`, `/warehouse/stock/dashboard`, `/warehouse/stock/location-balance`, `/warehouse/stock/adjustments`, `/reservations`
- Inbound (3):
  - `/warehouse/inbound/shipments`, `/warehouse/inbound/qc`, `/warehouse/putaway`
- Outbound (9):
  - `/warehouse/sales/orders`, `/warehouse/sales/allocations`, `/warehouse/outbound/orders`, `/warehouse/outbound/dispatch`, `/warehouse/waves`, `/warehouse/picking/tasks`, `/warehouse/labels`, `/warehouse/cross-dock`, `/warehouse/rmas`
- Operations (4):
  - `/warehouse/transfers`, `/warehouse/cycle-counts`, `/warehouse/visualization/3d`, `/projections`
- Finance (3):
  - `/warehouse/valuation/dashboard`, `/warehouse/agnum/config`, `/warehouse/agnum/reconcile`
- Admin (22):
  - `/admin/users`, `/warehouse/admin/settings`, `/warehouse/admin/reason-codes`, `/warehouse/admin/approval-rules`, `/warehouse/admin/roles`, `/warehouse/admin/api-keys`, `/warehouse/admin/gdpr-erasure`, `/warehouse/admin/audit-logs`, `/warehouse/admin/backups`, `/warehouse/admin/retention-policies`, `/warehouse/admin/dr-drills`, `/warehouse/admin/serial-numbers`, `/warehouse/admin/lots`, `/warehouse/admin/uom`, `/admin/items`, `/admin/suppliers`, `/admin/supplier-mappings`, `/admin/locations`, `/admin/warehouses`, `/admin/categories`, `/admin/import`, `/warehouse/admin/layout-editor`
- Reports (9):
  - `/reports/stock-level`, `/reports/receiving-history`, `/reports/pick-history`, `/reports/dispatch-history`, `/reports/stock-movements`, `/reports/traceability`, `/warehouse/compliance/lot-trace`, `/reports/compliance-audit`, `/warehouse/compliance/dashboard`
- Analytics (2):
  - `/analytics/fulfillment`, `/analytics/quality`

Inventory total:
- Section items in `_sections`: **58**
- With standalone `/dashboard`: **59** navigation links

### 2) Page type classification (migration lens)

- Grids / list-heavy pages:
  - Already Mud grid based: `/available-stock`, `/warehouse/admin/lots`
  - Remaining major list pages: inbound/outbound/sales order lists, reports lists, admin catalogs (`items`, `suppliers`, `locations`, `warehouses`, `categories`, `uom`, etc.), transfers list, cycle-count list, audit logs.
- Forms / workflow pages:
  - Create/detail/execute flows: inbound create/detail, sales create/detail, outbound detail/pack, transfers create/execute, putaway, stock adjustments, valuation action pages, admin forms (roles/api keys/reason codes/settings/retention/gdpr/etc.).
- Dashboards / KPI cards:
  - `/dashboard`, `/warehouse/stock/dashboard`, `/warehouse/sales/allocations`, `/warehouse/compliance/dashboard`, `/warehouse/valuation/dashboard`, analytics pages.
- JS-heavy / interop sensitive pages:
  - `/warehouse/visualization/3d` (`warehouseVisualization.js`)
  - CSV export-dependent pages (`csvExport.js`) across reports, valuation, labels, agnum reconciliation.
  - File upload/image flows: `/search-by-image`, `/admin/items/{id}`, `/admin/import`, `/warehouse/agnum/reconcile`.
- Shared shell pages/components touching all routes:
  - `MainLayout`, `NavMenu`, shared status/feedback components, and wrappers used across route groups.

Route inventory conclusion:
- The route surface is broad and admin/report heavy; migration should continue with phased list-first conversion and shared wrapper retirement to avoid regression spread.

## Block 3 - Shared Components (Legacy Overlap)

### 1) Inventory of custom components overlapping MudBlazor

Legacy/shared wrappers still used across pages:
- `ErrorBanner` usages: **74**
- `LoadingSpinner` usages: **71**
- `Pagination` usages: **17**
- `ConfirmDialog` usages: **6**
- `DataTable` usages: **2**
- `ToastContainer` usages: **1** (mounted in layout)

Toast service footprint:
- `ToastService` registered in DI (`Program.cs`)
- `@inject ToastService ToastService` in pages/components: **70** usages
- Current rendering path is custom `ToastContainer` (Bootstrap toast markup)

### 2) Overlap details

- `ToastService` + `ToastContainer`
  - Overlaps Mud `ISnackbar`.
  - Constraint: preserve `ToastService.ShowSuccess/ShowError/ShowWarning` call sites during migration.
- `ConfirmDialog`
  - Bootstrap modal markup + visibility flags.
  - Overlaps Mud `IDialogService` and `MudDialog` patterns.
- `LoadingSpinner`
  - Bootstrap spinner overlay; widely used and often stacked in page containers.
  - Overlaps Mud `MudOverlay` + `MudProgressCircular`.
- `ErrorBanner`
  - Bootstrap alert with retry/dismiss + trace id.
  - Overlaps Mud `MudAlert` with action buttons and severity.
- `Pagination`
  - Bootstrap pager + page size selector.
  - Overlaps `MudDataGridPager` / `MudTablePager` depending on page migration target.
- `DataTable`
  - Bootstrap table wrapper.
  - Overlaps `MudDataGrid` / `MudTable`.

### 3) Replacement strategy (safe sequence)

- Step A: Toast bridge first
  - Keep `ToastService` public API unchanged.
  - Replace internals to call `ISnackbar`.
  - Remove `<ToastContainer />` from layout only after snackbar bridge is verified.
- Step B: Dialog bridge
  - Replace `ConfirmDialog` call paths with a thin `IDialogService` wrapper retaining current inputs (`Title`, `Message`, `ConfirmText`).
- Step C: Status wrappers
  - Convert `LoadingSpinner` to Mud overlay implementation without changing external parameters.
  - Convert `ErrorBanner` to Mud alert while preserving retry inference behavior and trace id display.
- Step D: Data wrappers
  - Migrate pages from `DataTable`/`Pagination` to `MudDataGrid` + pager; then remove wrappers when `rg` confirms zero references.

Exit condition for deleting each legacy component:
1. No references in code (`rg` clean).
2. Functional replacement in Mud exists on all prior call paths.
3. Build + UI smoke tests are green.
