# Current legacy order-entry audit

2026-10-31: the owner identified `/Users/bykovas/Desktop/Uzsakymai.adp` as
current. SHA-256 `84185a33c8390abf327cac0bf469b0240dc135d650acdc5d8d4c60fef8cabaf5`.
149 modules recovered; seven differ from historical Dropbox source. All
following mappings use the current desktop source plus actual SQL definitions
and reference SELECTs. The audit itself executed no Access code or business
write. Subsequent controlled SQL/browser verification and deployment are
recorded separately in [verification](milestone-2-verification.md); Access
runtime acceptance remains open.

## Evidence map

| Area | Current source and SQL traced | Detailed evidence |
| --- | --- | --- |
| Product dimensions, control, quantity, PPP and priedai | Events, bound fields, typed PPP slots, formulas/functions, unit conversions, SQL total triggers | [Product rules](milestone-2-current-product-rules.md) |
| Material shortage | Source of available quantity, reservation states, strict comparison, configured action1/2/3 and notification | [Product rules](milestone-2-current-product-rules.md) |
| List/manual/fixed prices | Lookup events, current joins, effective periods, minimum charge measures, trusted flag and exceptions | [Product rules](milestone-2-current-product-rules.md) |
| Editing and customer checks | Separate order/product/delete flags, actual live state table, field exceptions, SQL-login privilege boundary, late debt/credit precedence | [Edit/state rules](milestone-2-current-edit-state-rules.md) |
| Confirmation/cancellation | Actual production document/detail commands, barcode transitions, shortage/credit checks, partial historical loop | [Edit/state rules](milestone-2-current-edit-state-rules.md) |
| Form bindings/defaults | Current design streams, year group/default people/dates, lookup sources; undecoded scalar properties explicitly identified | [Form contracts](milestone-2-current-form-contracts.md) |
| Address/duties/payments | Hierarchy/employee defaults, duty triggers, payment method dependencies/date/refund fields, posting effects | [Form contracts](milestone-2-current-form-contracts.md), [edit/state rules](milestone-2-current-edit-state-rules.md) |
| Identity, concurrency, retries and side effects | Actual SQL schema/keys/triggers, shared test scope, receipt design and unsafe operations | [Contracts](milestone-2-contracts.md), [command protocol](milestone-2-command-protocol.md) |

## Implementation decisions established by this inspection

- Check capabilities per action/field and per product state. A mixed-state order
  can have editable header and locked products. Store, address, duties and
  payments are not uniformly governed by the header edit flag. Do not turn one
  boolean into universal permission. SQL-login price exceptions do not become
  Portal rights simply because Sales connects as sa.
- Preserve the verified event sequence. Price lookup runs on entering a zero
  price or the price button; it is not automatically repeated on every resize.
  Formula PPP values are replaced on dimension/quantity recalculation. Colour
  checks stock without that full PPP recomputation. Parent is saved before
  opening its accessory popup.
- Available material is not a plain stock-master number: view_Remains includes
  warehouse/production balances minus PPP12 reservation of relevant existing
  lines. Define pre/post-save snapshot and current-line reservation treatment
  before the first material mutation. A fresh lookup followed by another
  subtraction can double-count the current product. Equality is shortage in
  entry code; confirmation uses a different, narrower stock view.
- Unit-price lookup keeps money precision and exact legacy date/type/component
  joins. Missing price, multiple distinct matching prices and invalid reference
  are separate outcomes. No implicit zero,999, integer-return price function or
  invented nearest-ceiling sort. Historical999 behavior is recorded but must
  not silently appear as a successful new Sales quote.
- The inclusive-period overlap is present in current data: a targeted SELECT
  found20 sample boundary pairs, including group197 periods1150/3757 at
  2024-03-01. Different prices on those boundaries are not yet demonstrated;
  ambiguity must be handled if they occur.
- Current291 active types have259 fixed-price category modes and32 NULL/any.
  Do not use the demo's universally editable price or hardcoded200mm limit.
  Current six calculation constants were read from DIC_Constants, not copied
  from stale declared VBA constants. R25 LP sample at800×1500 had unique price
  20.8526 for the first20 inspected matching component groups on SQL today's date;
  that is a sample proof, not a statement about every type/period/component.
- Confirmation creates a production-output document and barcode details; it
  does not set an arbitrary header state. Entry and confirmation have different
  customer/stock conditions. A new atomic confirmation must explicitly account
  for the old partial-item loop and all downstream effects.
- Never expose current sp_DeleteBlind blindly: it disables a global trigger.
  Payment/currency, employee cash, bank-transfer assignment, salary and email
  effects remain separately gated and cannot be cleaned up by generic deletes.
- Keep edits and report failure when legacy catches an error and closes anyway.
  A failed SQL eligibility lookup is not successful eligibility. These are
  explicit failure-handling differences, not claims of identical legacy errors.

## Remaining proof boundaries

This audit traces current executable source, form-design text, SQL definitions
and live reference data. It does not claim that Access was launched or that
create/edit/payment/confirmation comparisons passed. Numeric design properties
such as RecordLocks/AllowAdditions were not decoded reliably. Material combo
freshness/self-reservation and dimension-only stored-cost behavior need
controlled runtime/SQL-write evidence before activation. Source knowledge is
now sufficient to implement the verified read/validation pieces; do not keep
blocking independent development on identifying the already-confirmed ADP.

Acceptance scenarios and results stay in
[milestone-2-verification.md](milestone-2-verification.md). Existing SQL order
records and procedures were not modified by this audit. No deployment or
commit was performed.
