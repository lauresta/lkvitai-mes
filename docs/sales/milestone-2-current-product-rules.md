# Current Access product rules — stock, price and PPP

Inspected 2026-10-31 from the owner's confirmed current file
`/Users/bykovas/Desktop/Uzsakymai.adp`, SHA-256
`84185a33c8390abf327cac0bf469b0240dc135d650acdc5d8d4c60fef8cabaf5`.
This supersedes the old Dropbox source as the product-code baseline. The
current ADP has 149 extracted modules. No Access/macros, SQL procedures,
stock actions, payments or business writes were executed by this inspection.

Private extracted source directory: `/tmp/sales-vba-source/Desktop-current/`.
Line numbers below count Attribute lines in those exports. SQL definitions
come from the parent's current read-only metadata inspection in
`/tmp/sales-m2-evidence/complete.json`, not from old commented-out SQL in VBA.
Form design properties and runtime event behavior remain distinct evidence.

## Changes from the earlier source

Compared with the old Dropbox `Form_frmNewBlind`, current source changes the
refresh ID variable from Double to Long, makes MakeLstSource public, changes
its invocation to Call, and changes exception handling: **CheckComponents has
its On Error handler commented out**, while **FindPrice enables CError handling**.
The calculation, stock actions, price query and manual-price logic otherwise
match. The parameter-check message is reformatted; it still does no validation.
The current refresh Long is a 32-bit VBA value while SQL product IDs are bigint;
Sales must retain long/bigint IDs rather than reproducing this client limitation.

## Events and persistence order

| Trigger/event | Exact current behavior | Server/UI consequence |
| --- | --- | --- |
| New category, `Form_frmNewBlind:13–34` | Clears type selection; queries types with matching Rusis and Naudojamas<>0, ordered by TipoTrPavadinimas | Type eligibility comes from reference IDs/status, not illustrated family name |
| New type, `:36–66` | Executes sp_CreateNewItem(order,type), captures field0 into VBA Long, clears selectors, requeries, goes to last row, focuses control type | Persisted product exists before dimensions. Sales requires a proven exact ID, not last-row ordering or unsafe @@IDENTITY |
| Current record, `:184–224` | Loads edit flag from state, sets AllowEdits/AllowDeletions and lock icon, shows PPPs, reloads colour source and history/accessory list | Reference data changes per record; cannot permanently infer edit permission from creation state |
| Quantity, `:180–182`; height `:330–332`; width `:334–337` | Each calls CalculateAdditionalFields | These events replace formula-backed PPP values; do not preserve M1 sticky manual flags |
| CalculateAdditionalFields, `:285–315` | Reads NPP01–NPP12; evaluates every nonnull formula in slot order and writes corresponding bound control; then Me.Dirty=False; then CheckComponents(colour,Nz(PPP12,0)) | Parent is saved before stock behavior. A read-only preview must simulate/calculates without invoking these mutations |
| Colour, `:317–319` | CheckComponents using current PPP12; no call to CalculateAdditionalFields | Do not recalculate all PPP automatically on every colour change without proving the intended rule |
| Reopen/current-record event | Calls ShowAdditionalFields and loads metadata; does not call CalculateAdditionalFields | Stored manually edited values survive reopening until a calculation-triggering event |
| Open priedai, `:352–380` | Saves parent, focuses notes; edit/read-only popup follows lock icon. Passes width/height divided by1000 and current quantity; filters by product ID | Resolve dirty parent before child operation; accessory formula input units are metres |
| Price enter, `:552–567` | If current price=0, calls FindPrice; then locks/unlocks price according to category rule/type exceptions | Automatic lookup is on price focus when zero, not every dimension/colour change |
| Price-list button, `:133–141` | Calls FindPrice even when current price is nonzero | An explicit operator action can replace manual/list price |
| Manual price AfterUpdate, `:543–550` | If old price differs from new, clears module flag and stored trusted-price checkbox | Manual/list source matters; do not leave trusted=true after manual changes |

The actual field edits in Access may trigger additional automatic saves because
they are bound controls. The table above records explicit code operations;
it does not claim all Access runtime saves have been reproduced on macOS.

## PPP definition and evaluation contract

`ShowAdditionalFields (:246–283)` joins type to category P01–P12. Each label and
control is visible when its caption is **not null**, including an empty caption.
The PPP value itself does not determine visibility. Hidden slots may still have
nonnull formulas and therefore still be recalculated by CalculateAdditionalFields.

`modcalculations.bas:5–27` substitutes Width, Height and Quantity, then Colour
if not null, then ControlType if not null, and evaluates with Access Eval.
A null Colour jumps straight to Eval, also skipping ControlType substitution.
No silent zero/null replacement appears inside this evaluator. Its required
width/height Long and quantity Double arguments can fail on incompatible input.
Sales must use a server allowlist/function implementation, never execute VBA
or client-supplied expressions. Formula errors need field-level results; do not
save approximate values or silently turn errors into zero.

`modTechno.bas:31–143` contains all eight named calculation functions. Their
live values come from `DIC_Constants.dConstantValue` keyed by:
`MaxStep127`, `MaxStep89`, `EdgeFix127_AB`, `EdgeFix89_AB`,
`EdgeFix127_CDE`, `EdgeFix89_CDE`. Declared module constants are not the values
actually read by these functions. The parent's live SELECT on 2026-10-31
confirmed MaxStep127=114, MaxStep89=79, EdgeFix127_AB=139, EdgeFix89_AB=101,
EdgeFix127_CDE=124 and EdgeFix89_CDE=86. These are inspected values, not
constants to hardcode permanently.

For width w, selected edge e and step s:

- Base carriers = Access Round(((w−e)/s+1)+0.5,0).
- AB variants return base carriers.
- CDE variants add1 only when VBA `(base Mod 2)=1`. Do not simplify to a
  different odd-number rule for negative/out-of-range inputs without evidence.
- Correctors return0 only when carrier count=0; otherwise Access
  Round(((s−(w−e)/(carriers−1))*carriers)/6,0).
- Count1 has a division-by-zero edge. Missing/zero constants and negative
  widths need explicit failure behavior, not invented fallback values.

Access functions use Variant/Double and VBA Round; midpoint/floating-point
boundaries require comparison before assuming decimal arithmetic is identical.
SQL PPP01–11 storage is nvarchar(15), PPP12 decimal(18,3); preserve actual
conversion, scale and null semantics in the server adapter.

## Material availability and shortage behavior

### Source of displayed available quantity

Current record sets colour RowSource to `sp_cboSpalva(typeId)` (`:215`). The
**active** SQL query joins `view_Remains`, `TBD_Components`,
`TCR_CategoriesColours` and `Zinynas_tipai` by type/category. It returns columns:
component ID, internal code, Laisva, warehouse, production, combined, reserved;
column2 is the third column **Laisva**, which CheckComponents reads (`:395`).
It also unions a synthetic component0/codeKlaida/zero-quantity row. This is an
error/fallback entry, not a real stocked colour to accept by default.

`view_Remains` defines:

- warehouse = summed `TBD_BlindComponentsRemains.dQuantity` for storehouse1;
- production = summed quantity for storehouse2;
- combined = warehouse+production;
- reserved = summed `PPP12 * -1` from products in state4 or1173102375 whose
  order has Sugadintas=0;
- **Laisva = warehouse+production+reserved** (reserved is already negative).

Reservation is therefore partly implicit in saved product/state data. A Sales
save can change visible free stock without a separate stock-reservation command.
Do not subtract the already-negative reservation a second time.

### Entry-time shortage policy

`CheckComponents (:386–454)` takes component ID and required PPP12. It reads
available from the colour combo's current column2, then tests:

`enough = available − required > 0`

Exact equality counts as shortage, including zero available/zero required.
It reads `TBD_Components.iActionIDWhenNotInStock` for the selected component:

| Action | Current Access behavior |
| --- | --- |
| 1 | Returns silently, even when shortage exists |
| 2 | Shows an information warning, leaves product in place |
| 3 | Shows blocking message, deletes bound product through Access menu commands, then optionally offers notification |
| Other/unmatched | No action branch; procedure exits |

The action3 path does **not** call the explicit delete button/sp_DeleteBlind;
it uses Access bound-record menu deletion. Directly replacing it with that
procedure would introduce its global trigger-disable behavior. Do not do so.
If operator accepts notification, it inserts `NOTIFY_UserComponents`
(SYSTEM_USER,component,order-number note,required quantity). This is another
mutation and cannot run from preview. Actor behavior needs Portal attribution
in addition to existing SQL-login storage.

Current CheckComponents has no active `On Error GoTo` statement. It does not
handle null available/action/component locally; do not claim an unknown
component or read failure is equivalent to action1/allow.

### Freshness and self-reservation ambiguity to resolve

The form loads the colour combo on Form_Current. Dimension recalculation saves
PPP before checking, but the procedure does not explicitly requery that combo.
Thus the source does not establish whether its available number is pre-save,
post-save or a cached earlier value in every interaction. A new server that
freshly reads Laisva after saving PPP and then subtracts required again can
count the current line twice. Conversely a pre-save read may include an old
reservation for an edited line. Before save activation, establish the exact
comparison boundary, own-line reservation adjustment and freshness in Access
and SQL, including new line versus editing existing line and colour switch.
This is an unresolved runtime contract, not permission to blindly duplicate a
mathematically incorrect check or silently change the business policy.

### Confirmation policy is separate

SQL `view_CanConfirmBlinds` uses component
`bDenyOrderConfirmationWhenNotInStock<>0`, product state4, and Laisva<0. Its
sAllow CASE compares Laisva with PPP12 using < and > with no equality branch.
It is not the same threshold/flag as entry-time CheckComponents. Trace the
actual confirmation command's use of this view; presence of a view alone does
not prove the confirmation operation consults it.

## Price lookup and trusted/manual state

`FindPrice (:456–541)` requires nonnull/nonempty type, width, height, colour,
quantity. It takes order date from the open order form. The active SELECT joins:

`TCR_TypeColourPriceGroup g → DIC_PriceList l → TCR_PriceGroupPeriods p`
plus `TBD_ComponentsPriceGroups cg → TBD_ComponentsPriceGroupRows cgr`.

Predicates: price-list width≥product width, height≥product height, exact type,
period start≤order date≤period end, exact component ID. Quantity is required
by the form but is not used in the unit-price lookup predicates. There is
**TOP(1) with no ORDER BY**. SQL does not guarantee nearest dimensions, cheapest
price, latest period or any other tie-break. Need live matching-row/cardinality
inspection for each enabled price contract. Do not invent ORDER BY silently.
The parent's live SELECT sample for R25 LP (type−1025240863),800×1500 at
the current SQL date returned20 component groups (IDs2309–2328), each with
one matching price and one distinct price20.8526. This supports those samples,
not global determinism for every dimension/date/type or an Access runtime pass.

Period-boundary overlap is plausible by definition: SQL trigger
`UpdateValidationPeriodInPriceGroupPeriods` sets the previous maximum period's
end to the incoming period's start, while lookup uses inclusive bounds on both.
The parent's live period-boundary SELECT found actual shared boundaries:
group197 periods1150/3757 at2024-03-01, and group399 periods2217 with3701/2346
at2013-06-25. Distinct prices at these boundaries are not yet checked. Verify
matching prices and operator-visible effect; no DDL was run.

| Lookup outcome | Current Access result |
| --- | --- |
| Found | Assigns money/Currency price, refreshes current record, then marks price trusted |
| Not found, category KainosIvestis='fixed' | Warns, writes sentinel999, refreshes, then marks trusted |
| Not found, other category | Sets trusted=false; leaves price otherwise as it was |
| Lookup error | Calls CError and exits; not equivalent to no price row |

At the end it stores the trusted flag in `chk_bIsTrustedPriceFlag`. Category
price-input mode is `ISNULL(Zinynas_rusys.KainosIvestis,'any')` in the form's
record source. On price focus, fixed mode locks manual price except type IDs
428417548 and2112842479; any mode unlocks it. These IDs must remain stable
business exceptions until verified otherwise, not a new generic permission.
The live291-type coverage export has259 types in fixed categories and32 with
null mode (thus any); this is the dominant catalog behavior, not a rare UI
exception. A sentinel999 is historical exceptional behavior, not a safe default price
or a successful quote. Surface unresolved/missing price explicitly until the
business-preserving handling has been specified and tested.

Do not replace this with `fn_GetBlindPriceByBlindId` merely because the SQL
function exists: it accepts int product ID and returns **int** despite selecting
money, losing fractional price semantics. `fnRyga_GetBlindPriceByBlindId` returns
money but additionally applies a Riga-specific discount. Neither is the
function called by this current Access FindPrice path.

## Calculated amounts are not simply quantity × unit price

Active SQL `trg_ChangeBlind` recalculates costs when Kiekis, Kaina or
mBlindAccessoryFinalCost is in UPDATE's SET list. Width/height alone are not
listed triggers for the cost-recalculation branch; this matters when an existing
nonzero price remains after dimensions change. Verify Access event saves and
expected displayed/stored cost rather than broadening the SET list casually.

Its price unit branches use pieces, height metre, width metre or square metres.
Width/height/area charging uses type MinKainosPlotis/MinKainosAukstis/
MinKainosPlotas floors. Minimum **charged** dimensions are not proof of minimum
allowed manufacturing dimensions. Prices and intermediate amounts use SQL
money/decimal precision; do not force two-decimal rounding before SQL.

`fn_DiscountByCustomerAndType` and accessory-category counterpart inspect the
order customer's wholesaler flag. Non-wholesalers return multiplier1. For
wholesalers they read configured discounts using reference customer2089646703,
then return `(100-discount)/100` as decimal(18,3). This is a multiplier, not a
percentage to subtract again. Order sums group discountable/non-discountable
product final costs according to category TeikiamaNuolaida and accessory final
costs. Existing query DTO assumptions of Qty×Price are insufficient to edit.

On note updates the trigger rejects more than3 ASCII uppercase characters or
more than1 exclamation mark within its cast to2048-character text; its RAISERROR
returns without an explicit rollback. CountUpperCase uses ASCII65–90, not all
Unicode uppercase. Sales must roll back its failed operation and report the
actual rule instead of broadening it to a different text policy.

## Dimensions/control limits and remaining evidence

No VBA minimum200mm, default800×1500, maximum dimension, universal control
choices or active technical validation routine was found in these current
modules. `cmd_CheckParameters_Click (:93–97)` is a message only. The inspected
core-table CHECK-constraint export is empty. This does **not** prove arbitrary
values are valid: form design validation/input masks, bound defaults, lookup
rows and other paths still require inspection. Category/type charging floors
must not be mistaken for validation bounds. Control values are not the English
M1 labels by default; SQL Valdymas is nvarchar(10).

The supplied current source confirms code provenance, but an Access runtime
comparison has not been executed. Required open checks: control bindings/
validation/defaults; exact stock freshness/self-line adjustment; constant null/zero failure behavior;
price matrix multiplicity outside the inspected R25 samples/current periods; manual price/PPP event behavior;
reference state flags; confirmation caller; safe action3 delete/notification
containment. This document does not enable a write path.

## Port acceptance scenarios (all not run in Access yet)

1. Width/height/quantity with formula and null-formula slots, including hidden
   captions; manual PPP then dimension change; reopen without dimension change.
2. Every custom function variant with live constants, midpoint, zero/one carrier,
   missing/zero step and negative/out-of-range width; explicit errors preserved.
3. New and edited line, same versus switched colour, Laisva below/equal/above
   required; quantity0/PPP12=0; action1/2/3/unknown; notification yes/no; null
   component/read failure never treated as enough.
4. Existing line's old reservation versus new reservation, combo refresh timing,
   another operator consuming/reserving material between preview and save.
5. Price entry with zero/nonzero/manual value, explicit list-price button,
   fixed/any modes, both exceptional type IDs, no row, multiple rows, inclusive
   period boundary and matrix width/height boundary; quantity changes must not
   unexpectedly change which unit-price predicates apply.
6. Minimum charged width/height/area, wholesaler/non-wholesaler discount,
   no discount row, accessory final amount change, and width-only edits retaining
   nonzero price; compare stored costs and order totals at SQL precision.
7. Locked versus unlocked product, editable versus deletable state flags, priedai
   popup edit/read-only, note-limit errors, and rollback of a raised trigger error.
8. Confirmation shortage rule separately from entry shortage rule, including
   state4 scope, component confirmation flag and current negative-free filter.

## Evidence hashes

| Source | SHA-256 |
| --- | --- |
| Current Form_frmNewBlind.vba | d479ad442be18d08b86d2b5cd1cb2450f1669f8b4ab2b161471c94f46bbb4ec0 |
| Current modcalculations.bas | 4bfe92a50998954f894a7f06a5a71355432ac3171afc9ea1c6c2cc61b391f131 |
| Current modTechno.bas | 9710ad1fec97638ffdf0f163ef1cf71bc791a94308c1c9269bbdceb50236c70c |
| Current Form_dlgNewAccessories.vba | 8945a425efb3f8cb15823a73919355222d1a57d4b7ba362b96d2bf0adad7f328 |
| SQL sp_cboSpalva | 82e1a4b0806cfb4826a392a2f66fb9b741c6575b855a4d19988c397c2460d997 |
| SQL view_Remains | d89831cbf3551f369ecc922d0a47658a149ee6a2c360fc18ff8e96339c1c2e10 |
| SQL trg_ChangeBlind | 6ec14ac49168d4e7a361ef1290db30f26d4bc6f1a415344f07652ab1a22b6d3f |
| SQL view_CanConfirmBlinds | fbf6dcf55e5d6c7ffffc1b79f234e0acbcf63d26deb7fd2ee36161ea15d4d5c9 |
| SQL fn_DiscountByCustomerAndType | c071f6d4a54d2dd526e09df88dea385dbf4cc9d192625abb8290fbf040a019e2 |
| SQL fn_DiscountByCustomerAndAccessoryCategory | 200384f695477ca696a1f666051597026880848de493760e97681965b30976a9 |

Export hashes use UTF-8 bytes of extracted files. SQL hashes use UTF-8 of
sys.sql_modules.definition preserving the inspected line endings.


## Implemented read-only unit-price foundation

`ISalesUnitPriceLookup.GetUnitPriceAsync(SalesUnitPriceQuery,...)` now reads the
same live joins/predicates with typed SQL parameters. It returns explicit
Found/Missing/Ambiguous/InvalidReference/InvalidPriceData outcomes and match
counts/money min/max. One distinct nonnull price is Found, including repeated
identical prices; conflicting prices are Ambiguous and expose no selected
UnitPrice. Any matched null price is InvalidPriceData, since the legacy
unordered query could choose it. There is no sentinel999 fallback or stored
trusted-price change. Price0 in an actual matrix is not confused with no match.

Signed legacy IDs are accepted. Required dimensions/quantity must be positive;
there is no demo200mm floor. OrderDate is a date-only input, validated against
SQL datetime range, and remains the pricing date rather than lookup time.
Quantity is validated but intentionally absent from SQL price predicates,
matching the source. This guard is not approval of manufacturing dimensions
or permission to persist the configuration. Existing inactive references are
not implicitly replaced; nonexistent type/component yields InvalidReference.

This foundation does not calculate totals, discounts, VAT, PPP, stock,
reservation, edit authorization or a commit quote/revision. It does not write
orders/prices, grant save approval, or establish Access runtime parity. Parent
owns endpoint/auth/DI integration and live SELECT verification.
