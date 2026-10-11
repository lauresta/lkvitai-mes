# Sales order entry: UI-first milestones

## Milestone 1 — interactive preview

The `+ New order` action on the Sales orders list opens a drawer. The operator
enters a number in one field. The UI validates the series against a bundled
reference snapshot and expands the current-year group:

- `VV-123` → `VV-123-26` in 2026;
- `VV-123-A` → `VV-123-26A`;
- an unknown series or an invalid number is rejected.

The drawer fills customer, manager, store, and consultant from the selected
series snapshot, and allows the operator to edit them. Creating the draft opens
an interactive editor with product/service selection, standalone `priedai`,
and accessories attached to a product. An item opens a configurable product
screen, and the receipt opens a local payment sheet. Product/category
illustrations are schematic SVGs. The editor exposes all 26 category records and 290 type records
with `Naudojamas <> 0` in the snapshot, plus 469 accessory definitions and
1,449 active colour/fabric codes mapped to their categories. The
snapshot was read from the legacy database on 2026-10-11 and is embedded in the
WebUI assembly at `Data/demo-catalog.json`. It is **static** at runtime.

The configurator exposes width, height, quantity, colour/fabric, control type,
price, product notes, and the visible `PPP01–PPP12` fields. PPP captions come
from `Zinynas_rusys.P01–P12`; type formulas come from
`Zinynas_tipai.NPP01–NPP12`. The preview evaluates only an allowlisted
arithmetic subset (`Width`, `Height`, `Quantity`, operators, and `Round`).
Unsupported VBA functions remain visible for manual entry; their exact
results will be supplied through legacy logic in milestone 2. Attached
accessory quantity is suggested from the legacy unit rule for width, height,
pieces, or square metres. All quantities and prices remain editable.
The colour input searches the category's static codes and marks codes outside
the snapshot; stock quantities are not cached in the UI.
The draft editor also includes priority, mounting date, structured address,
responsible employee assignments, order note, preview discount and fixed-price
calculation, and method-specific payment details. These are preview values;
legacy validation and persistence belong to milestone 2.
`Save draft preview` checks required people, dates, line quantities and prices,
product dimensions, and selected colour codes. It displays the result in the
open UI but deliberately persists nothing.

No order-entry action calls Sales.Api or writes SQL in this milestone. The
draft exists only in the open Blazor component and is lost on close or refresh.
The UI labels this explicitly. Prices typed for products are preview values;
no legacy price lookup, stock check, credit gate, production confirmation, or
payment posting runs. The product configurator and payment sheet update only
the local preview model. Existing read-only Sales orders continue to use their
existing API and SQL procedures.

## Milestone 2 — legacy database integration

Start only after the product owner reviews milestone 1 and requests it. Replace
the static snapshot and local draft with API-backed reference lookups and
transactional writes to the same legacy SQL Server tables/procedures used by
Access. Preserve the separate `Priedai` category and `ParduotiPriedai` child
lines, including legacy unit-based quantity calculation. Verify each write
path against a test-branch order and confirm visibility in both clients.

Before enabling writes, address the existing Sales read procedures' branch
filters (`Loc_leidimas()` and the explicit branch-7 exclusion), so the agreed
all-branch scope and two-way visibility hold. Price formulas, credit checks,
stock rules, triggers, audit identity, and concurrency behavior also need
parity tests. Production database writes were not used for milestone 1.

## Design precedence

`docs/ux/lkvitai-mes-ux-handoff.html` and repository `AGENTS.md` define the
visual system. The supplied `Новые экраны модуля Sales.zip` provides layout and
interaction reference. Its eight hardcoded categories and limited accessory
popover are expanded in this milestone according to the legacy catalog and
forms; its separate series/group/sequence fields are replaced by the agreed
single-field number entry.
