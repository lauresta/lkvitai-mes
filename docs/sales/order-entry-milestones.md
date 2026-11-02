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
an interactive editor with standalone `priedai` and accessories attached to a
product. `+ Add product / service` opens a full-width category and type page.
Selecting a type immediately adds a draft line and opens its separate product
editor. The editor keeps the order-product rail, width/height/quantity, control,
colour, price, PPP fields, accessories, and a dimension-responsive live
schematic together on a desktop screen. `Done` returns to the order; the
receipt opens a local payment sheet. Product/category
illustrations are schematic SVGs. The editor exposes all 26 category records and 290 type records
with `Naudojamas <> 0` in the snapshot, plus 469 accessory definitions and
1,449 active colour/fabric codes mapped to their categories. The
snapshot was read from the legacy database on 2026-10-11 and is embedded in the
WebUI assembly at `Data/demo-catalog.json`. It is **static** at runtime.
One legacy accessory has no name; the preview displays its ID as a fallback.

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

The product owner requested a reviewed milestone 2 plan on 2026-10-31.
[milestone-2-plan.md](milestone-2-plan.md) now defines the reviewed implementation
sequence, command protocol and acceptance gates. The owner subsequently
authorized implementation; live core entry is now deployed on the remote test
stand with branch7-only writes and verified new Testavimas orders. Current
execution evidence and remaining M2 gaps are in
[milestone-2-verification.md](milestone-2-verification.md).
The actual database is production; initial write verification is restricted to
its verified Testavimas branch/series. Start with read-only contract discovery.

Start only after the product owner reviews milestone 1 and requests it. Replace
the static snapshot and local draft with API-backed reference lookups and
transactional writes to the same legacy SQL Server tables/procedures used by
Access. Preserve the separate `Priedai` category and `ParduotiPriedai` child
lines, including legacy unit-based quantity calculation. Verify each write
path against a test-branch order and confirm visibility in both clients.

Before enabling writes, establish trusted Sales authentication, exact ID-based
editor reads, branch scope, audit identity, concurrency and durable retry
handling. The first product slice already requires all applicable PPP, pricing,
credit and stock rules; later gates expand coverage rather than waive these
checks. Verify actual Access behavior as well as stored SQL values. Production
database writes were not used for milestone 1.

## Design precedence

`docs/ux/lkvitai-mes-ux-handoff.html` and repository `AGENTS.md` define the
visual system. The supplied `Новые экраны модуля Sales.zip` provides layout and
interaction reference. Its eight hardcoded categories and limited accessory
popover are expanded in this milestone according to the legacy catalog and
forms; its separate series/group/sequence fields are replaced by the agreed
single-field number entry.
