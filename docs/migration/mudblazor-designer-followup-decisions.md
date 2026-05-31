# MudBlazor Designer Follow-up Decisions

**Date:** 2026-05-31  
**Context:** Follow-up answers after `mudblazor-designer-handoff-annotated.html`.

## Locked Decisions

- Keep `.lk` globally on the Warehouse `MudLayout`.
- Scope bridge overrides to `.lk .mud-*`, `.lk-grid`, `.lk-dialog`, `.lk-state`, and `.panel`.
- Keep `.lk-bare` as an emergency escape hatch for rare raw-Mud experiments.
- Use one bordered Mud field base style:
  - `.lk-field--filter` for 32px interactive toolbar filters.
  - Filter labels are external mono-caps micro-labels; do not use Mud floating labels in dense list toolbars.
  - `.lk-field--inline` for row actions / inline fields without labels.
- Dialogs:
  - Neutral header by default.
  - Destructive confirmations use `.lk-dialog--danger` on the header only.
  - Destructive body remains neutral.
  - Filled danger button is reserved for the final destructive confirmation.
- Buttons:
  - Dashed disabled state is approved for all Mud buttons.
  - Row/list destructive actions use outlined danger by default.
  - Filled danger is only for final confirmation.
- Icons:
  - Use Material Outlined icons everywhere.
  - Filled icons are allowed only for active navigation and critical status signals.
  - Navigation icons, including collapsed sidebar icons, are 18px.
- Tables:
  - 30px rows everywhere, including admin/catalog and read-heavy reports.
  - For read-heavy reports, use grouping and section spacing instead of taller rows.
- Admin/list density:
  - Keep the intentional split: `--row-h: 30px` for display rows, `--control-h: 32px` for interactive inputs/buttons.
  - Filter toolbar and grid live in one `MudPaper.panel`; do not stack a separate filter card above a separate grid card.
  - Fold `Total` and page indicator into a 34px grid header strip.
  - MudDataGrid pager target is 36px with 28x28 icon buttons.
  - Empty filtered grids use one compact 56-64px block with mono-caps title and optional `Clear filters` action.
  - Non-row chrome under the page title should stay at or below about 168px on admin/list screens.
- State grammar:
  - `.lk-state` is for page/panel context.
  - `.chip` is for a single row, record, cell, or field.

## Golden Screens For Phase 1

- `AvailableStock` — canonical dense operational list: grid, filters, pagination, statuses.
- `Admin/Lots` — canonical CRUD flow: forms, create/edit dialog, destructive action, empty state.
- `StockAdjustments` — canonical workflow: confirmation dialog, state banners, inline chips.

`InboundShipments` should be migrated after those examples exist, because it combines their patterns.
