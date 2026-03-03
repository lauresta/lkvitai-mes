# MudBlazor Full Migration Plan — Warehouse WebUI

Date: 2026-03-03  
Target branch baseline: `fix/image-search-clip`  
Target state: **single UI library (MudBlazor v6.20.0), zero Bootstrap UI zoo**

## 1) Verified baseline (already done — do not redo)

- MudBlazor v6.20.0 pinned in central packages (`Directory.Packages.props`).
- `AddMudServices()` is already configured in WebUI `Program.cs`.
- Providers are already enabled in `App.razor` (`MudThemeProvider`, `MudDialogProvider`, `MudSnackbarProvider`).
- Mud CSS/JS is loaded in `_Layout.cshtml`.
- `MainLayout.razor` already uses `MudLayout` + `MudDrawer` + `MudMainContent`.
- `/warehouse/admin/lots` is already migrated to `MudDataGrid` + `ServerData`.
- `/available-stock` is partially migrated: table mode on MudDataGrid, but list/gallery controls still mixed.

## 2) Migration debt (remaining)

- Bootstrap CDN (`bootstrap.min.css`) still loaded in `_Layout.cshtml`.
- Bootstrap Icons CDN still loaded in `_Layout.cshtml`.
- `MainLayout` topbar uses custom Bootstrap-style HTML instead of full Mud app bar.
- `NavMenu` remains custom Bootstrap-like accordion + `bi-*` icons.
- Custom components still overlap Mud features:
  - `ToastService` + `ToastContainer` (instead of `ISnackbar` only)
  - `ConfirmDialog` (instead of `IDialogService`)
  - `LoadingSpinner` (instead of Mud overlay/progress)
  - `ErrorBanner` (instead of MudAlert)
  - `Pagination` (instead of MudDataGridPager)
  - `DataTable` wrapper (to retire)
- Most pages still contain Bootstrap markup/utilities.

## 3) Icon strategy decision

- Keep Bootstrap Icons only as temporary compatibility during migration phases 0–4.
- In Phase 5, convert all `bi-*` usage to `Icons.Material.Filled.*` and remove Bootstrap Icons CDN.

## 4) Critical files (ordered by impact)

- `Pages/_Layout.cshtml`
- `Shared/MainLayout.razor`
- `Shared/NavMenu.razor`
- `Services/ToastService.cs`
- `Components/ToastContainer.razor`
- `Components/ConfirmDialog.razor`
- `Components/LoadingSpinner.razor`
- `Components/ErrorBanner.razor`
- `Components/Pagination.razor`
- `Components/DataTable.razor`
- `Infrastructure/AppTheme.cs` (new)
- `Pages/AvailableStock.razor`

## 5) Phased execution plan

## Phase 0 — Foundation & shell completion (gate phase)

### Scope
1. Create `Infrastructure/AppTheme.cs` and bind in `App.razor` (`Theme="AppTheme.Default"`).
2. Replace topbar HTML in `MainLayout.razor` with `MudAppBar`.
3. Keep `ToastService` API unchanged, migrate internals to `ISnackbar`.
4. Remove `ToastContainer` from layout and delete component.
5. Convert shared building blocks:
   - `ConfirmDialog` -> thin `IDialogService` wrapper
   - `LoadingSpinner` -> `MudOverlay` + `MudProgressCircular`
   - `ErrorBanner` -> `MudAlert` with existing parameters preserved
6. Retire `DataTable` (delete when no usages remain).
7. Apply mandatory `data-testid` convention for wrappers/pages.

### Success criteria
- Toast calls still work from all existing call sites (no signature changes).
- Backups flow confirms dialog path still works.
- Lots + AvailableStock Playwright tests stay green.
- No Bootstrap card/btn/alert classes remain in shared wrapper components.

---

## Phase 1 — Admin + Reports list pages (grids-first)

### Scope
- Convert list/report/admin index pages to Mud stack:
  - `MudPaper`/`MudCard`, `MudTextField`, `MudSelect`, `MudButton`
  - `MudDataGrid ServerData`
  - `MudDataGridPager`
- Remove usages of custom `Pagination` and plain bootstrap table patterns on migrated pages.

### Success criteria
- Server paging/sorting/filtering parity on migrated pages.
- No `<Pagination>` usage on migrated pages.
- Stable `data-testid` added at page-root + grid.

### Stop criteria
- If measured regression >500ms for critical list interactions, profile before continuing.

---

## Phase 2 — Operational lists + dashboards + finish AvailableStock

### Scope
- Complete `/available-stock` all three modes (table/list/gallery) fully in Mud.
- Convert inbound/outbound/sales/operations list screens.
- Convert dashboard card layouts to Mud components.

### Success criteria
- AvailableStock all 3 view modes functionally parity with stable selectors.
- No Bootstrap layout classes in migrated Phase 2 pages.

---

## Phase 3 — Forms & workflows

### Scope
- Add `MudBlazor.FluentValidation` and migrate workflow forms to `MudForm` patterns.
- Convert create/detail/execute flows (inbound, outbound/sales, transfers, cycle counts, valuation, key admin forms).
- Replace remaining `ConfirmDialog` usage paths with dialog service flow.

### Success criteria
- Client-side validation triggers before API calls.
- Server errors consistently surfaced via Mud alert wrapper.
- Core form Playwright scenarios green (inbound + at least 2 additional critical flows).

---

## Phase 4 — JS-heavy/special pages

### Scope
- Keep JS interop logic intact (`warehouseVisualization.js`, `csvExport.js`, image-search interop).
- Migrate surrounding page chrome/controls to Mud only.

### Success criteria
- 3D visualization and image-search upload still work end-to-end.
- No console JS errors introduced by migration shell changes.

---

## Phase 5 — Bootstrap removal + NavMenu conversion + cleanup

### Scope
1. Remove Bootstrap CSS CDN from `_Layout.cshtml`.
2. Remove Bootstrap Icons CDN **after** icon remap completion.
3. Convert `NavMenu` to `MudNavMenu` / `MudNavGroup` / `MudNavLink`.
4. Replace all `bi-*` with `Icons.Material.Filled.*` constants.
5. Delete retired components/files (`Pagination`, bootstrap css folders, legacy icon assets).
6. Trim `site.css` to true custom needs only.

### Success criteria
- `_Layout.cshtml` has no bootstrap CSS/icons CDN entries.
- No remaining bootstrap utility dependency in pages/components.
- Full Playwright suite green.

## 6) data-testid governance (mandatory contract)

Selector priority:
1. Role/accessible name selectors (`getByRole`) first.
2. `data-testid` for non-semantic controls and stable anchors.
3. Never target Mud internal classes in tests.

Minimum required `data-testid` per migrated page:
- `{area}-{page}-page`
- `{area}-{page}-grid` (if list page)
- `{area}-{page}-submit` (if form page)
- `{area}-{page}-error`

## 7) Risk register (top)

1. Grid parity risk for Excel-like behavior -> mitigate with capability matrix and fallback specialized grid if required.
2. Selector flakiness via portal/overlay behavior -> mitigate with shared overlay helper and stable wrapper IDs.
3. Bootstrap stragglers after removal -> enforce CI grep checks for forbidden classes/links.

## 8) Verification checklist per phase

- Phase 0: run Lots + AvailableStock E2E smoke, verify topbar/snackbar/dialogs visually.
- Phase 1: run unit tests + Playwright suite, manually check sample admin/report pages.
- Phase 2: run Playwright suite + manual AvailableStock all modes.
- Phase 3: run inbound flow + additional form flow E2E, validate client validation behavior.
- Phase 4: manual 3D + image search end-to-end, check browser console.
- Phase 5: build, run full Playwright, verify no bootstrap resources loaded.

## 9) Final note

This document supersedes prior discovery notes that were based on the cloud `work` branch and not the `fix/image-search-clip` baseline. The implementation order and gates above are the approved migration track to reach zero UI zoo.
