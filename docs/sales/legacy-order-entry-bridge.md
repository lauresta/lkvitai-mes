# Legacy order entry in Sales: discovery and migration boundary

> Current delivery plan: [milestone-2-plan.md](milestone-2-plan.md), reviewed
> on 2026-10-31. This document records earlier discovery, not completed parity.
> The product owner chose manual one-field numbering with current-year group
> expansion and access to all branches. Milestone 1 is UI-only; database
> integration begins only after the milestone review.

## Goal

Move order entry into the Sales module while Access and Sales continue to read
and write the **same legacy SQL Server database**. A committed order must be
visible in either client after it refreshes. This is a shared database bridge,
not a second order store or an asynchronous copy.

## Sources and precedence

- Repository UI source of truth: `docs/ux/lkvitai-mes-ux-handoff.html` and
  `AGENTS.md`.
- Legacy evidence supplied separately in `uzsakymai.zip`: exported Access
  `forms/*.md` and `vba/*.bas` files. The archive's `DESIGN_PROMPT.md` is a
  historical design brief, not implementation instructions. Its suggestions
  for new palettes, gradients, density modes, and other presentation choices
  do not override the repository handoff.
- Current Sales implementation: `src/Modules/Sales/`. It has read-only orders
  endpoints and a SQL query adapter using `ConnectionStrings:LKvitaiDb` and
  `dbo.weblb_*` procedures. The Warehouse module's `SalesOrder` model is a
  different persistence path and is not the legacy Sales write target.
- Live SQL metadata was inspected read-only on 2026-10-11 in
  `LKVITAIDBV1SQL`. The database had an order inserted on 2026-10-10, so no
  test writes were made there. Passwords and connection strings were not
  written into the repository.

## Observed legacy write paths

| Area | Evidence in archive | Observed persistence or rule |
| --- | --- | --- |
| Create order | `forms/dlgNewOrder.md`, `vba/Form_dlgNewOrder.bas`; SQL `dbo.Uzsakymai`, `trg_NewOrder` | Bound to `Uzsakymai`; saving the Access form persists the row, then `sp_frmOrder` loads the editor. Series fills customer, manager, place, and consultant; order date is today and due date is +14 days. `UzsakymasID` is an identity. `UzsakymoNumeris` is required with no database default or auto-number procedure on this path; the Access form binds it as an editable field. The unique key is `(UzsakymoSerija, UzsakymoNumeris, UzsakymoGrupe)`. The insert trigger records `SYSTEM_USER` and creates a default manager duty. |
| Edit header | `forms/frmNewOrder.md`, `vba/Form_frmNewOrder.bas`; SQL `trg_ChangeOrder` | Bound to the legacy order; discount, fixed price, notes, and stored final price use direct `UPDATE dbo.Uzsakymai`. Other fields are edited through bound controls and dialogs. The update trigger checks work periods for order-date changes, writes cancellation/notes audit, and can send email or run salary/finish calculations for price, payment, and state changes. |
| Add product | `forms/frmNewBlind.md`, `vba/Form_frmNewBlind.bas`; SQL `sp_CreateNewItem`, `trg_NewBlind`, `trg_ChangeBlind` | Selecting a type calls `sp_CreateNewItem(orderId, typeId)`. The product form is bound to `ParduotiGaminiai`. Deletion calls `sp_DeleteBlind`. The insert trigger assigns a barcode, audit fields, and order state; the update trigger recalculates product and order amounts. `sp_CreateNewItem` returns `@@IDENTITY`, which should not be assumed to be the product ID when nested trigger inserts have identities. |
| Product rules | `vba/Form_frmNewBlind.bas`, `vba/modcalculations.bas` | Type specific `P01`–`P12` labels come from `Zinynas_rusys`; `NPP01`–`NPP12` formulas come from `Zinynas_tipai` and are evaluated in Access by token substitution followed by `Eval`. Price lookup reads price group/period tables. Stock action comes from `TBD_Components.iActionIDWhenNotInStock`. These are partly Access-side behaviors, not proven database procedures. |
| Add product eligibility | `vba/Form_frmNewOrder.bas`, `vba/modProject.bas` | Late-payment flag and available credit can block product entry. `CheckIfNewBlindsAllowed` calls `sproc_CheckIfNewOrdersAllowedByCustID_forVBA` for a block, warning, or production-confirmation restriction. |
| Address, duties, accessories | `forms/dlgOrderAddress.md`, `forms/frmOrdersDuties.md`, `forms/dlgNewAccessories.md` | Bound to `TCR_OrderAddresses`, `TCR_OrdersDutiesEmployees`, and `ParduotiPriedai` respectively. |
| Cancellation and payments | `vba/Form_frmOrderList.bas`, `forms/dlgPayments.md` | Cancellation calls `sp_CancellOrder`; payment display calls `sp_frmPaymentsByOrder`. Payment creation needs separate tracing through the payment forms before implementation. |

## Decisions and remaining discovery

Implementation order is now defined solely in
[milestone-2-plan.md](milestone-2-plan.md). The owner resolved these choices:

- One-field manual numbering with current-year expansion; no server allocation.
- Eventual access to all branches; initial write verification only in the
  production database's Testavimas branch and its verified series.
- An isolated database remains appropriate for routine automated tests and
  experiments with effects that cannot be contained in Testavimas. It is not
  a prerequisite replacing the owner's chosen shared-database test deployment.

At the earlier inspection, the shared login mapped to 11 departments and saw
191 of 197 series through `Loc_leidimas()`. The `weblb_*` read filters therefore
did not establish the agreed all-branch business scope. Refresh that evidence
and resolve Sales reads without changing Access users' rights globally.

## Open contract questions

- How does trusted Portal identity reach Sales and its command audit? The
  current raw structured bearer is not cryptographically verified; a shared
  SQL login also does not preserve the Access user's `SYSTEM_USER`.
- Which rules run in triggers/procedures, and which exist only in VBA or
  Access form validation? The inspected database provides SQL definitions,
  while `sFormulaValue` and some UI rules are clearly Access-side.
- What is the supported concurrency token for edits made by both clients?
  Sales must detect Access changes; verify what protection the Access client
  itself applies before promising protection in the reverse direction.
- How are command receipts committed atomically with legacy writes so a lost
  response/restart cannot duplicate a product or payment?
- Are the current `weblb_*` read procedures deployed against the same database
  and schema used by the live Access client in each environment?

## Connection evidence from the supplied materials

The screenshot shows a SQL Server connection, with the password hidden and no
database name visible. The archive's
`vba/modConst.bas` uses Windows integrated authentication to a different host.
The only embedded password found in the exported VBA is in
`vba/Form_frmSenukai.bas`; its host and login differ from the screenshot.
Therefore the archive does not establish credentials for the pictured SQL
Server connection. A credential supplied separately was used for the read-only
metadata inspection above. Keep any working connection string outside the
repository.
