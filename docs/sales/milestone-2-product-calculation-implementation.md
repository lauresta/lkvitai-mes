# M2 server product calculations and live catalog

Working implementation, 2026-10-31. Source is the current Desktop ADP identified
in milestone-2-current-audit.md. This code does not execute Access Eval and does
not open transactions that survive user interaction.

## Calculator contract

LegacyProductCalculator.Calculate accepts the stored server type's twelve
formulas, entered values, numeric input and live DIC_Constants. Its arithmetic
uses Double and midpoint-to-even Round, matching the source's Variant/Double
function family. All eight CarrierQnt/CorrectorsQnt variants are allowlisted;
unknown tokens/functions fail a field-addressed LegacyProductRuleException.

Recalculation replaces every non-null formula slot, including hidden captions.
Null formula slots preserve entered values. Preserve mode does not recalculate
on reopen or colour-only change. The mutation service must derive whether
width/height/quantity/type changed from the stored row; the client cannot use
RecalculateParameters=false to bypass dependency recalculation. PPP01–11 keep
nullable nvarchar(15) values, PPP12 converts to decimal(18,3) with explicit
precision/scale validation. Formula conversion uses invariant transport text;
no browser-supplied expression or SQL executes.

The initial coverage run using all291 current SQL type formula sets at
800×1500,quantity1 and inspected constants passed285 types and identified six
unquoted Nera values in PPP02. The owner subsequently confirmed that Nera is
literal text, with canonical output `nera`. The server now recognizes exactly
that standalone token (case-insensitive, surrounding whitespace ignored) only
in text slots PPP01–PPP11. This does not enable arbitrary identifiers, function
calls such as Nera(...), compound Nera expressions or text in numeric PPP12.
Legacy SQL formulas and the Access application are not modified.

All six affected current SQL formula sets were then previewed read-only:
2112842751,2112842752,2112842763,2112842764,2112842775,2112842776.
Each returned PPP02=`nera`, material requirement0.764 or0.769 at800×1500,
and a valid calculation-reference revision. Fresh catalog capability checks
therefore enable those six types without hardcoded ID exceptions. This
resolves the earlier six-formula limitation; orphan/inactive category data
remains a separate selection-contract concern. Golden source-function cases
at width1000 passed all8 variants; this is not an executed Access comparison
or proof of every floating-point boundary.

Contained persistence verification then exercised all six types sequentially
on owned Testavimas order2148197450 with current applicable materials6026,
5950 and5805, fresh calculation revisions and unique actual list prices.
Created product IDs2152012302–2152012307 returned typed SQL PPP02=`nera`
(nvarchar), PPP12=0.764/0.769 (decimal), and native barcodes. Each new product
was removed through the guarded Sales delete command immediately after its
readback. The original three product snapshots and stored/calculated totals
28.43 were verified restored after every case; no original line or payment
was changed. The reviewed legacy sp_frmOrder projection could read the order
throughout. Actual Access UI execution remains separate acceptance evidence.

Accessory quantity suggestions implement the four verified source units
(width metres,height metres,pieces,area). Other units retain operator-entered
quantity; the helper's fallback is only a UI suggestion, not an authoritative
new unit formula or permission to overwrite an existing accessory quantity.

## Atomic material validation

LegacyProductRuleSql reads constants, type formulas and stock on the caller's
SqlConnection/SqlTransaction. Stock validation runs **before mutation**:
warehouse1+production2 minus reservations of uncancelled state4/1173102375
products, then adds back only the existing current line's same-component
reservation. Colour changes do not add back reservation from another material.
This avoids subtracting a saved line's PPP12 twice.

Component UPDLOCK/HOLDLOCK serializes Sales for the material; stock and
reservation HOLDLOCK reads remain in the short business transaction to protect
against concurrent Access changes. Existing Spalva-leading product indexes
provide a candidate targeted range path; actual plans/blocking must still be
verified. No global trigger is disabled and no stock ledger is mutated here.

Strict available-for-line minus new requirement >0 is retained: equality is
shortage. Action1 allows,2 returns warning,3 rejects and rolls back the attempted
configuration. This intentionally preserves the preexisting saved line on a
failed edit rather than reproducing Access's destructive bound-record deletion.
Numeric actions outside1/2/3 retain the source Select Case no-op, including0
and negative integers; no new operator warning is introduced. A null action
when shortage exists is an explicit error. Sufficient stock returns before
policy validation, so a null action does not block an abundant material.
Invalid negative material requirement remains an explicit data error. Notification requests
are a separate authenticated mutation and never run from a preview.

## Live read foundation

ILiveOrderEntryCatalog reads current series/default IDs and labels, categories,
types/formulas/pricing metadata, mapped colours, applicable accessories,
and customer/employee/store/duty/priority labels. It selects no customer web
credentials/contact data and applies no SQL-login branch filter, matching the
agreed Sales all-branch read policy. Mutation authorization independently
rechecks Testavimas series ownership. Null accessory price stays null; it does
not become a valid zero. Catalog reads have no permanent static cache.

ILegacyProductPreview computes PPP from server-owned formulas and live constants
inside a short read transaction. It does not perform shortage deletion,
notification, reservation or persistence. UI must debounce/generation-check
responses and save operations must still recalculate/revalidate inside their
own transaction. A preview is not a save approval.

Colour eligibility follows current `sp_cboSpalva`: join `view_Remains` to
`TBD_Components` and `TCR_CategoriesColours`, then match the product category.
The active procedure does not filter `bInUse`; the catalog must not invent a
`bInUse='Y'` condition. Component0 is excluded as the synthetic Klaida fallback.
The stock helper also requires the component-category master row, matching the
view's inner join rather than accepting orphan component-category references.


The final stock policy replaces the earlier stricter unknown-action proposal.
Tests cover numeric0/−1 shortages, null-action shortage rejection, abundant stock
with null action, strict equality and adding back the current reservation. The
current source text performs DLookup before its shortage branch; the server's
sufficient-stock fast path is a chosen behavior and does not claim an Access
runtime observation of missing policy data.

## Read-only stock panel

Product preview accepts optional OrderId/ProductId. A product ID requires a
provided order and SQL verifies exact product ownership before any own-reserve
add-back. Employee preview reads retain the agreed all-branch read scope and
do not grant mutation permission. No product ID means no own-line add-back.
The stock aggregation itself credits an old line only when its saved colour
matches the selected component and its state/order meet the reservation rules.

The response includes AvailableMaterialForLine, FreeMaterial, ActionId,
ShortageDisposition, IsShort and the material's actual reference unit. A
selected component must pass the same category/applicability check as a save;
null/0 colour has no stock result. A shortage is described, not executed:
enough/allow/warn/block/unknown-policy/not-calculated/invalid-requirement.
No notification, deletion or reservation is performed by a preview.

Preview reads use short-transaction HOLDLOCK reads without UPDLOCK; save keeps
its existing update-intent component lock and authoritative comparison in its
own transaction. Preview availability is a point-in-time display, not a save
approval or a promise that stock remains unchanged before the next command.

## Calculation/reference revision

Preview now returns a server-issued CalculationRevision. It is a SHA-256 over
versioned typed binary reference values and context IDs, not a credential or
an authority to override validation. Its SQL2014-compatible SELECT-only helper
reads type/category formula/visibility/pricing metadata, six live constants,
current order customer/discount/currency and wholesaler flag, the actual fixed
reference-customer type/category discount rows, selected component policy/unit,
category/component applicability, applicable price-group mappings, and their
matrix/period rows. Row/table boundaries and numeric types are explicit; no
SQL JSON, concatenated text hash or locale-dependent decimal encoding is used.
Stock quantities are deliberately excluded: they are independently checked
fresh at commit rather than forcing a new reference revision on every reserve.

ProductConfigurationInput carries the displayed preview's revision. The writer
compares it with current references in its existing transaction before changing
calculation/pricing inputs; missing/stale revisions return409. Note-only/no-op
edits retain existing stored PPP and do not require a new calculation token.
The unit-price amount comparison remains separate. The token itself must not
make an otherwise unchanged line dirty, and UI must not silently replace a
displayed old revision with a newly fetched one immediately before saving.
Bump the fingerprint contract version when changing calculation semantics or
the encoded reference inventory across application releases.

Actual helper SELECTs were exercised against the inspected SQL2014 database:
repeat reads produced the same64hex revision; selected-component context
changes produced another revision. The first helper read was243ms over the
existing SSH tunnel. No production reference row was changed for this check.
Unit tests cover formula/policy/constant/price/period changes, context binding,
null/zero/text type separation, row membership and culture independence. This
is optimistic reference-drift protection, not a complete precommit commercial
amount quotation or proof of every Access runtime calculation boundary.

Material display units are resolved from `dbo.DIC_Units(iUnitID,sName)` using
`TBD_Components.iUnitID`; actual material unit ID2 resolves to `m`. The separate
legacy accessory/price units remain in `Zinynas_matai`; their IDs must not be
used to resolve component units. `rpt.DIC_Units` is a different reporting table
and is not the lookup source for this contract.

## Product-entry validation feedback (2026-11-01)

The save rejection for insufficient material now reports the fresh required and
available-for-this-line quantities in the component's unit, plus a concrete next
action. Equality still counts as shortage; the message explains this boundary
when the two amounts are equal. Action 2 remains a warning and action 3 remains
a rejection. Neither the stock policy nor the own-reservation add-back changed.
Preview and writer already use the same stock reader and policy mapping; a
preview is not evidence that later stock/policy reads must have the same values.
Tests explicitly compare preview dispositions with write validation at shortage,
negative-free-stock, own-reservation and exact-equality boundaries.

Missing list-price errors identify the selected dimensions and saved order date.
The fixed-price message directs the operator to From price list and explains what
to check when no price is found. The adapter continues to reject an untrusted
manual price for fixed-price types; no sentinel 999 or zero-price fallback was
introduced. Correcting missing price-list data is a separate business decision.
