# Current Desktop Access — edit, eligibility and transition contracts

Inspected 2026-10-31 from the owner-confirmed current
`/Users/bykovas/Desktop/Uzsakymai.adp` (SHA-256
`84185a33c8390abf327cac0bf469b0240dc135d650acdc5d8d4c60fef8cabaf5`).
149 VBA modules were extracted read-only to
`/tmp/sales-vba-source/Desktop-current/`. Line references below count the
Attribute lines in those extracted files. SQL definitions were read from the
parent's current metadata export, reference rows from
`/tmp/sales-m2-evidence/current-lookups.json`. No Access code, SQL procedure,
mutation or transition was executed by this analysis.

This replaces assumptions based on the older Dropbox copy for these actions.
Static inspection establishes the executable branches; actual Access behavior,
control bindings/design defaults, error handling and concurrent use still need
runtime acceptance evidence. The user has confirmed the Desktop source is
current; do not keep asking whether the older Dropbox file is current.

## Two separate edit permissions

`Form_frmNewOrder.Form_Current` obtains `bIsUnlockedFlag` from
`sp_frmOrder`, which projects `Zinynas_busenos.bAllowEditOrder` by the order's
`Uzsakymai.StateID`. It toggles individual header buttons, **not** a universal
read-only mode for the entire order.

`Form_frmNewBlind.Form_Current` (line184) looks up
`Zinynas_busenos.bAllowEditBlindFlag` using its `iStateID` control, then sets
`Me.AllowEdits=Nz(flag,0)` and lock icons accordingly. Product editing uses the
product state, represented in storage by `ParduotiGaminiai.BusenosID`, not the
order's aggregate `StateID`. The bound-control source should be included in
form-metadata verification. Separately, `Me.AllowDeletions` comes from
`GalimaRedaguotiGamini`; the two flags are not interchangeable.

Current live reference rows:

| State ID | Name | Order-edit flag | Product-edit flag | Access form deletion flag |
| --- | --- | --- | --- | --- |
| 0 | SKIRTINGAI | true | false | false |
| 1 | Gaminamas | false | false | false |
| 2 | Išduotas klientui | false | false | false |
| 4 | Įvestas į programą | true | true | true |
| 5 | Trūksta audinio | NULL | NULL | true |
| 6 | Trūksta detalių | NULL | NULL | true |
| 7 | Sustabdytas | true | true | true |
| 8 | Eilėje | NULL | NULL | false |
| 1173102373 | Nepriskirtas | NULL | NULL | true |
| 1173102374 | Pagamintas | false | false | false |
| 1173102375 | Patvirtintas gamybai | false | false | false |
| 1173102376 | Išsiųstas į filialą | false | false | false |
| 1173102377 | Garantinis remontas | false | false | false |

States5/6/8/unassigned are hidden by `bShowInApplicationFlag=false` in the
reference data. Product edit NULL is explicitly denied by VBA `Nz`. Header
NULL is assigned to button properties without `Nz` under an error handler;
that is not evidence that editing is allowed, and may leave a partially
updated UI. Sales must deny unknown capability and report the reference
problem rather than carry flags from a previously selected row.

State0 is important: an order containing mixed line states can allow header
changes while a particular line remains locked. A single `order.editable`
Boolean must not unlock every product or priedai.

## Field/action map: UI gates versus database enforcement

| Action | Current Access gate | Database entry/guard and Sales implication |
| --- | --- | --- |
| Consultant, customer, manager, priority | Buttons enabled iff order `bIsUnlockedFlag` (`Form_frmNewOrder:251–258`) | Dialogs issue targeted header UPDATEs; they contain no second state check. Sales must check the live order capability inside the command transaction. |
| Discount and fixed final price | Same order flag by default | `imgLocked_DblClick:352` allows **price-only** unlock for three explicit SQL logins; it does not change order state or other capabilities. Do not grant this override to all Portal users because Sales uses a shared SQL login. A trusted Portal override mapping is an explicit unresolved authorization contract. |
| Store/place | `cmd_ChangePlace.Enabled=True` even for locked order | `dlgPlace` targets `VietaID`; no local state check. This exception is real and is distinct from moving a test order to another series/branch. Retain the server's Testavimas series scope independently. |
| Due date | `cmd_OrderTerm_Click:163` opens `dlgOrderDueDate`; no state test in this handler or Form_Current button assignments | Targeted due-date dialog UPDATE. Bound/design control properties still matter. Do not infer a universal due-date lock from header flag alone. |
| Address | `cmd_Address_Click:12` opens a filtered bound `TCR_OrderAddresses` form with default order ID; no order-state branch in this handler | Validate street/address hierarchy and target order. Source does not prove all design-time controls are always enabled. |
| Duties/employees | `cmd_OrderDuties_Click:141` and persons double-click open bound table filtered to order, no header-state branch | INSERT/DELETE triggers enforce role multiplicity, allowed installer states and payroll cutoff; see below. Do not reuse simple header permission. |
| Open product editor | `lst_Items_DblClick:373` applies customer checks below before opening the form, including existing products | Opening is not a mutation, but failing eligibility prevents entry even to existing lines in this Access handler. Product-specific edit permission is evaluated separately in the product form. |
| Edit product parameters | `AllowEdits=Nz(bAllowEditBlindFlag,0)` | `trg_ChangeBlind` recalculates amounts/audit and note validation; inspected trigger does **not** implement a universal `bAllowEditBlindFlag` denial. Sales must enforce it, not rely on trigger alone. |
| Attached priedai | `Form_frmNewBlind.lslAccessories_DblClick:350` saves parent first, opens popup normally if unlock icon, `acFormReadOnly` if lock icon | `trg_NewOrChangedAccessory`/`trg_DeleteAccessory` recalculate product amounts, but no state lock is established by these triggers. Child commands must check parent product capability inside the transaction. |
| Delete product | Access form deletion permission reads `GalimaRedaguotiGamini`; explicit button also asks confirmation then calls `sp_DeleteBlind` | Procedure permits **only state4 or7**, despite other reference rows having deletion flag=true; it globally disables a production-output trigger. Do not expose this hazardous procedure as a generic deletion endpoint. A safe compatible path needs separate review. |
| Read-only order view | Order-list double-click explicitly opens `frmViewOrder` as `acFormReadOnly` | This is a view route, distinct from toolbar Edit, which opens `frmNewOrder`. `frmViewBlind` always displays a lock icon but is not always opened with `acFormReadOnly`; actual form design properties must be checked before equating its icon with database enforcement. |
| Payments | `cmd_Payments_Click:168` opens payment dialog without a parent edit-flag test; can first update stored final price if current total differs | Payment insertion is not governed by `bAllowEditOrder`. Apply its own real period/method/employee/bank rules, and contain non-order effects before enabling. |
| Cancel order | Order-list delete asks confirmation and calls `sp_CancellOrder` (`Form_frmOrderList:746`) | SQL permits state4/7 or two privileged SQL-login exceptions; sets `Sugadintas=-1`, does not delete header. Shared sa does not imply those employee exceptions. |
| Cancel production confirmation | Product button calls `sp_CancellBlindConfirmation` after confirmation (`Form_frmNewBlind:156`) | SQL permits only current product state1173102375; creates an output document assigned state4 and a barcode detail. Its parameter is int despite product ID bigint, and `@@IDENTITY` is unsafe; do not silently narrow IDs. This is a transition workflow, not a direct state patch. |

The cancellation flag is shown by the header, and confirmation-list SQL filters
`u.Sugadintas=0`; the inspected header/product edit-enable code does not combine
that cancellation flag with all edit permissions. Any new blanket
“cancelled means all fields locked” rule would require an explicit decision,
not be represented as proven source parity.

## Exact customer eligibility sequence

`Form_frmNewOrder.lst_Items_DblClick` executes, in this order:

1. `AllowEnterNewOrder(customerId)` (`:491`) allows if the customer's
   `NeleistiPardavinetiJeiVeluojaMoketi="N"`, or `Nz(Agnum_KLID,"")=""`, or
   `Nz(VeluotuNeapmoketuSaskaituKiekis,0)=0`; otherwise blocks the editor.
2. `modProject.CheckIfNewBlindsAllowed` executes
   `sproc_CheckIfNewOrdersAllowedByCustID_forVBA`. First character action0
   blocks; action1 warns that production confirmation will be prohibited but
   still opens; action3 warns but opens; action2 proceeds.
3. `mCustomerAvailableCredit<0` blocks, otherwise opens. Zero is not blocked
   here even though the displayed credit warning treats nonpositive as low.
   **Current sp_frmOrder hardcodes this displayed credit to150000** and comments
   out `fn_AvailableCreditByCustomer`. Do not replace this silently with a new
   calculated block and call it parity; procedure-based limits still run.

The current `modProject` catches an eligibility-procedure error, calls CError,
and returns action2 (fail open). Sales must surface dependency failure rather
than interpreting “could not evaluate” as proven eligibility; record this
intentional safety difference explicitly.

Current `sproc_CheckIfNewOrdersAllowedByCustID` precedence is:

- Sum all noncancelled order debt; if general limit minus debt **<=0**, return
  configured `ActionKaiBendras` when it is not2.
- Sum debt of released-to-client state2 orders; if released-debt limit minus
  that sum **<0**, return configured `ActionKaiAtsiimtu` when not2.
- Sum positive overdue debt using customer Terminas and
  `dtFirstReleasementDate`; if sum **>0**, return configured `ActionKaiSkola`
  when not2.
- Otherwise action2/empty message. The first applicable non2 action wins;
  it is not a combined “most restrictive” reduction. Preserve null semantics
  by using the current SQL rule where verified read-only.

`CheckIfNewBlindsAllowed` returns a concatenated string, but Sales should expose
an explicit action/message contract, not parse human warning text in the UI.

## Actual production confirmation path

The executable entry is `Form_frmOrdersConfirmation` opened by
`modMeniu.frmOrdersConfirmation`. It is separate from order entry, and is
**not** an UPDATE of `Uzsakymai.Patvirtintas`.

1. `FillTvw:14` reads `sp_ListBlindsForConfirmationOfProduction(includeStopped)`.
   Candidates are manufactured types, noncancelled orders and allowed series
   branches via `Loc_leidimas()`, product state4; includeStopped also includes7.
2. `NewTask:217` inserts `TAB_ProductionOutputDocuments` with generated
   `PGyyMMddNN` number, current time and assigned status1173102375. Number uses
   count+1 and identity uses `@@IDENTITY`; neither is an acceptable new Sales
   concurrency/id-return implementation.
3. `ConfirmBlinds:251` requires operator confirmation and an existing task ID.
   For each selected barcode it reads its customer, then denies only when
   `view_CanConfirmBlinds.sAllow="N"` or customer action1. It repeats the
   customer action1 check a second time. Actions0/3 are **not** denied by this
   confirmation handler; do not collapse the entry and confirmation policies.
4. Allowed barcodes are inserted individually into
   `TAB_ProductionOutputDocumentDetails(iProductionOutputDocumentID,iBarCode)`.
   Disallowed barcodes are skipped; the historical loop can partially confirm.
   It is not an atomic all-items transaction or a tested server command.
5. `T_TAB_ProductionOutputDocumentDetails_AssignBlindState` stores description,
   old/new states, SQL actor/time on the detail and sets product `BusenosID`
   from the document's assigned state. `trg_ChangeBlind` then updates aggregate
   order state, leading to order trigger salary/finish effects.

`view_CanConfirmBlinds` is narrower than a normal stock comparison. It joins
`view_Remains`, requires component `bDenyOrderConfirmationWhenNotInStock<>0`,
product state4 and **free stock Laisva<0**. Its CASE returns N when free stock
is below PPP12, Y when above, and NULL on equality. Absence/NULL does not enter
the VBA `="N"` branch. Stopped state7 and positive-but-insufficient free stock
are absent from this view. Do not implement confirmation as an imagined
universal available>=required guard and claim exact legacy parity. Test these
edge cases and decide explicitly if correcting the legacy gap is desired.

Cancel confirmation uses a new state4 output document/detail; it preserves
history through the same trigger. Details DELETE restores the recorded prior
state but is blocked when later production documents exist; details UPDATE is
restricted. These are production-history effects requiring Testavimas
containment, exact IDs and short transactions, not toolbar-only status labels.

## Duty and payment constraints beyond header lock

`trg_OrderEmployeesCheckassignedDuties` (AFTER INSERT) rejects multirow insert;
allows at most one consultant1, manager3, measurer2 and transporter5. Installer4
requires order state1173102376 (sent) or2 (released), plus a mounting-service
line type−1551576315. Consultant/measurer and installer changes are blocked
after the seventh day of the month following the acquisition date (literal
SQL `DATEADD(day,6, first day next month)` including its time component).
Dates before2000-01-05 use the no-acquisition path. It invokes finish/payroll
calculations. DELETE trigger rejects deleting manager3, enforces analogous
acquisition cutoff for duties1/2/4, then invokes finish/payroll. Do not replace
these with a free employee collection overwrite or assume the form's lack of
UI checks means no database constraints.

`NaujasMokejimas` requires a nonzero signed amount/date/employee/order and
method-specific PPK/transfer/register reference. Negative values are refunds,
not invalid numeric input. Transfer selection sets a specific historical
employee ID; EKA selection uses register column2 for employee. Do not map Portal
actor directly to receiver by guessing. Method changes clear/disable tagged
fields; raw record fields from another method must not leak into a new save.

`trg_NewPayments` verifies bank-reference method consistency, work periods
(payment and linked transfer date), future dates, nonzero amount and employee
cash-revision cutoff. It changes employee cash, order Paid and real bank
assignment totals, with backdate/refund email paths. `trg_ProhibitDeletionOfPayments`
rolls back deletion. The UPDATE trigger's actual guard returns if
`UPDATE(Pastabos) OR UPDATE(sUser)`, even if other fields are also in SET; its
comment/message does not establish a robust per-field whitelist. Sales must
expose only intended payment operations and may not exploit that trigger gap.

## Acceptance cases before enabling commands

- Mixed-state order0 with entered and locked lines: editable header versus
  separately locked product and attached priedai.
- Each visible state4/7 versus confirmed/manufacturing/made/sent/released;
  hidden/unknown/NULL reference rows denied explicitly, not inherited UI flags.
- Locked order's place/due/address/duties/payments exceptions verified against
  actual form design; privileged price override absent without trusted mapping.
- Eligibility boundary: general debt exactly at limit, released debt exactly
  at limit, overdue debt, actions0/1/2/3, SQL error and credit zero versus negative.
- Confirmation state4/state7, stock negative/zero/positive-insufficient/equal,
  missing view row, customer actions0/1/3, task creation and individual history.
- Cancellation/deletion state mismatch and bigint IDs outside int range;
  do not call global-trigger-disable deletion during verification.
- Duty uniqueness, installer prerequisites, acquisition cutoff boundaries,
  protected manager deletion and transaction rollback after trigger failure.
- Payment locked-parent behavior, valid method references, method switching,
  signed amount, work-period/cash cutoff and non-order side effects.

No acceptance case above has been executed by this analysis. A supported Sales
command must re-read state/capability/dependencies under its transaction and
return explicit allowed actions/reasons to the UI; disabling controls alone
cannot enforce these source-derived rules.
