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

## Block 4 - Risks and Mitigation

### 1) Identified migration risks

1. Grid behavior parity risk (high)
- Context: many list/report pages still depend on handcrafted table/paging/sort/filter behavior.
- Risk: `MudDataGrid` may not match every existing interaction (especially server-side filtering edge cases and paging semantics expected by users/tests).

2. JSInterop-sensitive page risk (high)
- Context: visualization and file/export flows use JS interop (`warehouseVisualization.js`, `csvExport.js`, file upload flows).
- Risk: refactoring surrounding UI shell can break lifecycle timing or DOM assumptions.

3. Bootstrap coexistence conflict risk (high)
- Context: Bootstrap CSS and Mud CSS are both active, with heavy bootstrap class usage remaining.
- Risk: style bleed, spacing/overlay z-index collisions, responsive inconsistencies.

4. Selector fragility risk in E2E (medium-high)
- Context: Mud renders portals/overlays and dynamic internal DOM structure.
- Risk: tests coupled to non-stable classes/structure become flaky during migration.

5. Shared wrapper removal blast radius (medium-high)
- Context: `ErrorBanner`/`LoadingSpinner`/`ToastService` are used across most pages.
- Risk: a breaking change can impact dozens of routes simultaneously.

6. Performance/regression risk on large datasets (medium)
- Context: server-data grids and live filters on stock/orders/reports.
- Risk: perceived latency regressions after template/component switch.

### 2) Mitigation strategy

- Grid capability matrix before mass migration:
  - Validate required features (server sort/filter/page, column templates, actions, dense rows, export hooks) on representative pages.
  - Use fallback page-specific wrappers where MudDataGrid alone is insufficient.

- Keep JS logic unchanged; migrate only UI chrome around it:
  - No rewrite of JS functions during UI component swap.
  - Re-verify end-to-end for visualization/image upload/export after each relevant phase.

- Controlled Bootstrap decommission:
  - Keep Bootstrap Icons temporary until phase 5.
  - Remove Bootstrap CSS only after shared wrappers and high-traffic pages are migrated.
  - Add grep-based guard checks to detect reintroduced bootstrap classes/links.

- Selector policy enforcement:
  - Prefer `getByRole` first, `data-testid` for non-semantic controls.
  - Do not target Mud internal classes.
  - Ensure each migrated page has stable root/grid/error/submit test ids.

- Wrapper-by-wrapper replacement with compatibility shims:
  - Preserve method signatures and parameters while swapping internals to Mud implementations.
  - Delete legacy components only when references are zero and smoke tests stay green.

- Regression control:
  - Run smoke suite at least after each sub-phase commit.
  - If interaction latency regression exceeds acceptable threshold, stop and profile before continuing.
