# Sales order entry — persistence behavior

The approved M1 layout is retained: separate full-width category/type picker,
product editor with parameter grid, order rail and dimension-responsive preview.
For authenticated Portal users the workspace loads live series/people/stores,
types, material codes, accessory definitions and price metadata. Review-key
sessions retain the local M1 model and never call real order-entry APIs.

Creating an order saves the header before opening its stable `/sales/entry/{id}`
route. Browser reload and the list/detail Edit actions reopen that exact saved
order. Closing an editor never deletes previously saved rows.

`Done` saves a changed product and returns only after a confirmed response.
Switching products, opening another picker or adding an accessory first saves
pending parent configuration. Header and address inputs are independent buffers:
other successful operations do not silently replace their unsaved values.
`Save order`, `Save address` and duty Add issue separate operations. Unsaved
navigation prompts protect buffers. The primary product action saves and returns. The toolbar Back action asks
before discarding a dirty product and reloads its saved values; independent
header/address buffers remain. Browser-history exit keeps the save boundary.

Product list quantities/prices are read-only in live mode; the product editor
and accessory popup provide the explicit save path. Product edit permission is
separate from order-header permission. Store/due/mounting/note exceptions follow
the server capability checks; ordinary-branch writes remain disabled at the API.
Financial posting and production-confirmation controls remain disabled when
those command capabilities are not available, rather than recording local
success for a real order.

Live payments use a separate explicit `Post payment` action. Ordinary `Save
order` never posts money. The sheet refreshes saved balances and scoped payment
references, uses the actual business date/currency/approved employee, and renders
only configured methods. PPK, bank-transfer and register fields appear only when
the selected real method requires them. A closed work period or other source
restriction is shown while posting remains disabled; no local payment is added.
Actual legacy method, employee and currency labels are retained in payment
history, including approved employees absent from the active general catalogue.
Method changes clear optional references and downstream receiver/amount/notes.
PPK syntax unlocks the downstream fields; bank/EKA selection applies the actual
source default receiver, which remains editable. Changing bank/EKA within the
same method preserves amount/notes, following the native AfterUpdate handlers.
EKA choices precede receiver selection and span the accessible departments.
Bank choices are searched/paged by actual payer/purpose/date; selected records
show their unrounded raw 2–4 decimal balance. The source bank table stores no
currency, so its balance is not labelled EUR or converted. New payments retain
the approved EUR currency and actual two-decimal storage precision; subcent
balances are explained instead of rounded up into a payment.
Unknown payment outcomes reuse the original command and frozen body; recovering
a posted payment closes the sheet and refreshes paid/debt so it cannot be
submitted again as an apparently unsaved payment.

Production confirmation is wired behind its separate server capability. Its
compact selection sheet starts with no products selected and offers only saved,
manufactured products in entered/stopped states with positive quantity and
stored price/dimensions. The manufactured flag comes from the saved SQL type;
historical types remain eligible even when absent from the active picker.
Current edits must be saved or discarded first. Selection never posts a command;
the final `Confirm selected` action sends one immutable command. Unknown outcomes
recover the same production-document result and close the selection sheet.
The deployment flag remains off until the native production path is verified
and contained; UI wiring does not enable production writes.

Control input preserves the exact stored value and has a ten-character limit;
K/D are suggestions. Designer English chain/motor labels are not persisted.
Width/height/quantity changes request a debounced read-only PPP preview. Late
responses cannot replace newer inputs. Formula-backed manual overrides reset on
the next dimension/quantity change; explicit manual slots are command intent and
are not persisted as a new override policy.

Entering a zero price or clicking From price list requests the verified list
lookup. Resize does not automatically choose a new price. Fixed/manual policy
comes from the selected type and is combined with current line permission.
Receipt and saved-product amounts use authoritative returned amounts. They do
not promote the demo quantity-times-price calculation to live accounting.

A mutation uses one command ID and frozen request payload. After a transport or
unknown server failure, editing is blocked until Retry replays that same command
and recovers its result. A second create is not submitted.
Once an outcome has been unknown, later authentication failures or disabled
feature gates also retain that exact command and body until recovery succeeds.
The operator can renew the session and retry without generating another payment.
The v2 recovery outbox keeps the exact frozen JSON business input, original ID,
actor and order scope in per-tab sessionStorage. It contains no authentication
credentials. Notes/address values in an interrupted request are therefore held
transiently in that browser tab. Input is bounded to 64 active records, 64 KiB per
body and 1 MiB per outbox; unknown/unrecognized data is preserved rather than
expired or silently discarded. If initial storage cannot be read or written, a
new mutation is not sent. Cleanup after proven success is best effort; a stale
entry still replays the same original ID safely.

After reload, recovery checks the authenticated receipt first. If absent, it
can replay the exact stored request with the same ID. Only the affected order
is protected from further edits; other orders remain usable. Actor mismatch,
non-whitelisted routes, inconsistent scope or a result for another order never
replays/applies that input as a different order. Old GUID-only markers have no
body/scope and are quarantined as a nonblocking check notice. A found legacy
result offers an explicit link to its own order instead of replacing the current
editor. A missing legacy receipt is not reported as a rollback.

First-attempt definitive validation/conflict responses retain form input for
correction. Once an outcome was uncertain, a later conflict remains retained for
recovery/operational review; a new ID is not generated to bypass it. Authentication
renewal is handled in another tab while the circuit keeps its edit buffer.

Header customer/store/employee fields use the shared compact autocomplete with
source IDs and saved-reference fallback. A freshly created product receives a
visible unsaved quantity of one; reopening an existing zero-quantity record
preserves its actual source value until an explicit edit is saved.

Standalone priedai starts with a real Priedai category/type product row, then
uses its allowed accessory definitions as children. Definition IDs are never
written as product type IDs. Unknown reference prices require explicit entry.
Unsupported formula types remain visible with a reason and cannot be created.

Live reference choices refresh before opening pickers after 60 seconds. The
product toolbar uses the real preview, list-price, duplicate, reload and delete
operations. Reload asks before discarding unsaved buffers. Production return and
warranty handling remain outside order entry. The browser module URL includes
the deployed build identity so a cached M1 history handler cannot override the
current editor navigation behavior.

The displayed server preview carries a calculation-reference fingerprint.
Business configuration saves send that fingerprint; a note-only edit does not
need one. The editor never replaces an existing displayed fingerprint just
before saving: the API can reject reference drift while retaining the inputs.
If a required preview is still pending or the product/material context changed,
the editor waits, displays the result and asks the operator to review and save
again. Merely reading stock or receiving a fingerprint does not dirty a product.

Local published-browser verification (2026-10-31) used the trusted test actor
`14b59371-0bf0-40cb-81b2-7f2bd06693a2` and the configured real legacy database,
with server writes restricted to branch7. Existing test order2148197448 reopened,
product width810 saved through Done and survived reload. The browser created
`XXXX-926404-26`, order2148197449, added R25 LP with800×1500/quantity1/controlD,
selected R173, looked up20.8526, saved, attached an allowed accessory with1.25
quantity and reopened with both product and accessory present. Guest review
continued to show the local-only entry notice. No browser page/console errors
were observed. This verifies browser/SQL persistence, not Access UI execution.

Remote published-browser verification subsequently created order2148197450,
`XXXX-943185-26`, with R25 LP/R173, actual list price and an attached accessory.
On build `m2-0e96bac`, standalone Smulkios detalės quantity0.5 at1.50 and
Žaliuzių matavimas quantity0.5 at10 saved and survived reload. Fixed final price
29.67→28.19 saved exactly, measurement duty add/remove succeeded, and the stock
preview displayed actual availability for the product without marking the
unchanged product dirty. Navigation after Done succeeded when the current
browser module was used; a stale cached M1 module was identified and the import
is now versioned. Existing street2255 and fictional building TEST-M2, due date
2026-11-15, mounting date2026-11-16, normal priority and a test note saved and
reopened. Urgent priority without the native required markup was correctly
rejected with its legacy message. The later v6 browser pass verified duplicate/fingerprint/toolbar navigation;
cancellation exposed the shared legacy audit INT overflow described below. Header receipt
uses actual discountable/non-discountable/current-final figures. Product total
uses SQL product and accessory final amounts once each.


Build m2-24da0ac verified actual width810, manualPPP01=700, list-price lookup,
calculation-reference save, duplicate with children, safe deletion of the copy,
Refresh with explicit discard and Done→Back. Standalone NULLPPP12/colour0 price
edit to1.60 also saved/reopened. The measured product screen fits1600×1100;
this is a measured viewport, not a claim that every resolution fits without
scrolling. Same-circuit session proof renewal stayed HTTPS200 at3/63/123s and
live preview remained usable after139s. Guest review was still sample-only.

Cancellation is disabled when `CancellationUnavailableReason` is nonnull.
Current legacy audit OrderID is INT, although new order IDs exceed its range.
The API rejects that known limitation before mutation with422; the tooltip
explains the order has not been cancelled. A reviewed optional migration002
would widen the shared audit column after explicit owner approval. The later owner decision keeps cancellation explicitly disabled through its
independent deployment flag. A schema fix plus Refresh alone does not activate
the action; a future explicit activation would also be required. No successful cancelled
state UI/API test is claimed before that migration is applied.

Sales' Test banner explicitly says the legacy production database is shared
and writes are restricted to Testavimas. The shared shell's optional BannerNote
parameter preserves other modules' default copy. A Test label alone must not
claim this database is disposable.


Final build m2-01b2815 read-only browser pass confirmed the saved fields, current
module, clean stock refresh, truthful banner and disabled cancellation reason.
The saved-type dropdown now retains a missing/inactive historical snapshot ID
and name via an explicit saved option; only active references provide new
choices. A renderer regression covers this without modifying a legacy record.


Final follow-up m2-f9367ea verifies actual two0.01EUR payments, retained across
redeploy (Paid0.02/debt28.41). The original captured command replays through
public HTTPS with the same PaymentId and no third payment. The client keeps an
unknown command's exact key/body even if later auth or write-gate attempts fail.
Cash scope is the explicitly approved d.bykovas receiver; existing LTL payment
metadata is not relabelled. Confirmation selection/recovery UI is connected
behind its disabled server capability, with stored manufactured/unit/price facts
for historical lines; no real production confirmation was executed.


## Operator feedback and keyboard correction (2026-11-01)

Product material selection uses a rich searchable combobox instead of the browser
`datalist`: code/name, optional existing photo, and the Access stock projection
(free/warehouse/production/total/reserved, with actual units). Photo discovery is
optional, bounded and same-origin; missing image metadata never blocks selection.
Stock is a catalog snapshot and the product preview/write recheck the native rule.

Errors remain in a prominent top-right notice until explicitly dismissed or
replaced by the next operation. A failed product save keeps the editor/inputs,
shows an inline error, scrolls/focuses the affected input and briefly highlights
it (motion is disabled for reduced-motion preferences). Error text names the
failed action and how to correct it; required/available stock values and fixed
price-list requirements are preserved. The save button says `Save & back to
order` while changed and returns only after confirmed saving. Toolbar `Back to order`
asks before discarding unsaved product inputs and reloads saved values; it does
not retry a failing save or leave unsaved edits in the order summary. A pending/refreshed
calculation review explicitly explains that another save is required.

One Tab moves from width to height to quantity without visiting +/- buttons;
text and numeric values select fully on keyboard focus. Mouse focus preserves
normal caret placement. Calculated PPP values are directly keyboard-editable;
only a changed value becomes an override. Background photo/price requests never
move keyboard focus. Fresh material checks clear obsolete stock figures while
pending, including manual PPP12 changes.


Remote verification, 2026-11-01, build `m2-7e2d621`: all nine browser stages
passed at 1600×1100 with zero console/page errors and zero unexpected HTTP
failures. The second material result was visibly hit-tested and clicked (not
merely present in DOM); material Tab advanced to the fully selected price.
Width→height→quantity and PPP selection worked. Invalid width and the native
uppercase-note constraint retained input, exposed `aria-invalid=true`, focused
the correct field and showed persistent feedback. A missing list price showed
its exact dimensions/material/saved date; toolbar Back remained clickable
without dismissing the notice. An explicit discard restored saved input.
Valid lowercase note changes saved, survived reload and were restored on owned
Testavimas order2148197455. Full SQL comparison found only the expected native
`dtDateUpdate` change; header, product business values, accessories and payments
were restored. No financial or production operations were issued.

On desktop the notice sits below the toolbar within the illustration column,
so it cannot cover the toolbar, type selector, main inputs or primary Save
button. A corrected fieldset-aware panel selector keeps material suggestions
and inline errors from being clipped or flex-shrunk. Existing photo metadata
currently has no real thumbnails, so live verification covers the neutral
fallback; bounded image lookup/rendering has a separate module smoke check.

## Cancelled order and legacy privacy notice (2026-11-02)

Cancellation is a separate `Uzsakymai.Sugadintas` flag. Sales presents nonzero
values as `Anuliuotas` in the list and details, and filters that effective
status before paging/counting; existing SQL-login department scope still applies.

Only the exact legacy order note `Kliento prasymu anuliuotas` (case-insensitive,
outer whitespace ignored) activates the owner-approved notice. Ordinary
cancellation does not imply this notice. Live details expose an additive
`IsPrivacyRestricted` boolean; the live editor uses the same shared marker rule.

The notice is a visible translucent watermark over the order/product content,
with an accessible English notice and a Back to orders action. Customer, product,
accessory and amount values remain visible and are read from the existing
source; no client-side zeroing, deletion, anonymization or GDPR workflow is
introduced. Existing legacy zero values are displayed as stored. The raw marker
is replaced by the approved English notice in the editor's note presentation.

Approved copy:
- Personal data restricted — GDPR
- This order was cancelled at the customer’s request. Certain order information has been removed for privacy reasons.
- ← Back to orders

The business wording is owner-specified; the presentation flag is not evidence
that Sales performed data erasure. Cancellation writes remain independently
controlled by the existing deferred gate.
