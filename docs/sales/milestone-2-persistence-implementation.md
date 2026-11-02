# Legacy order-entry persistence implementation

Implementation: shared SQL Server catalog, Testavimas series only. The API
uses trusted Sales credentials and every mutation checks configured database
GUID, series allowlist and the actual parent series branch7. Ordinary-branch
edit reads remain available but do not authorize writes.

## Deployment

Apply reviewed `scripts/sales/001-command-receipts.sql` explicitly. It adds only
schema `mes_sales` and a technical `CommandReceipt` table, no legacy column,
procedure or trigger modifications. It is guarded by database name and GUID.
It is not an EF/startup migration. The actual receipt key is `(ActorId GUID,
CommandId GUID)`, stricter than the preliminary per-operation proposal: reuse
of an ID for another operation/input conflicts. Stored receipt contains hash,
IDs, operation, revision, timestamp, SQL actor and build. Nullable WarningsJson
stores only server-generated operator warning messages for durable recovery;
these may contain legacy eligibility text. No submitted header/contact payload
is retained in receipts. Reapply the explicit001 script before deploying code
that writes WarningsJson. Existing applications remain compatible with the
additive nullable columns.

Register `SalesOrderEntryOptions`, `ISalesOrderEntryService` →
`SqlSalesOrderEntryService`, and `MapOrderEntryEndpoints` under the protected
Sales group. Bind `Sales:OrderEntry`:

- Enabled defaults false.
- ExpectedDatabaseGuid must explicitly match the live catalog.
- AllowedSeriesIds must explicitly list the permitted Testavimas series.
- BuildVersion identifies the deployed working tree/build.

Default-off/config/actor guards execute before opening a write connection.
Disabling writes leaves exact-ID reads and previously committed orders usable.

## Operation contract

`GET /api/sales/order-entry/orders/{id}` returns exact IDs, typed values,
current SQL totals, separate header/product edit flags, attachments, address,
duties and an aggregate revision. It does not search the first five list rows.
A short transaction reads and range-locks header, products, accessories,
address, duties and payments; SHA-256 covers their actual typed values and
membership. A write compares the revision under the same transaction locks.

POST header / PUT header, POST product / PUT configuration, POST accessory /
DELETE accessory, DELETE product, PUT address, POST duty, DELETE duty,
POST cancel and PUT final-price each accept a command ID. Actor is derived
from the protected credential, never the JSON body. Unknown write fields are
rejected. Composite accessory identity is definition+parent ID.

Mutation, authoritative readback and receipt commit atomically. Same key/input
returns the current order and original IDs without rerunning mutation/triggers.
Changed input/operation conflicts. `GET /order-entry/commands/{id}` resolves an
unknown outcome for that actor. Same-key recovery rechecks current test scope.
SQL1205 receives at most one retry after proven victim rollback. Network or
commit acknowledgement errors remain unknown outcomes; never retry under a
new ID. Client-side pending operation storage must retain immutable input.

## Verified legacy behavior in the adapter

- Header identity captured in INSERT scope with OUTPUT INTO; manual series
  prefix includes hyphens. Canonical current-year grouping is idempotent.
- Header uses verified EUR currency2, series/default user-selected people and
  the legacy insert/default-duty trigger. SQL audit remains SYSTEM_USER;
  receipt supplies trusted Portal actor separately.
- Product create preserves exact legacy INSERT(order,type,Spalva0) semantics
  with correct insert-scope identity. The persisted incomplete line is visible
  to Access immediately, as with the bound legacy form.
- Configuration derives recalculation from actual dependency changes, executes
  server allowlisted formulas and live constants, then applies explicitly
  intended manual PPP slot edits. Manual intent is not persisted as a sticky
  override. The next dimension/quantity change clears/recomputes it.
- Stock is checked before the mutation with current line reservation add-back;
  unchanged notes do not newly trigger a material shortage check. Actions1/2
  allow/warn; action3 rejects/rolls back the attempted save instead of silently
  deleting an existing row. This is an explicit failure-handling difference.
- Fixed unit prices require a verified list lookup, checked on the same SQL
  transaction. Missing/ambiguous prices do not become zero/999. Price remains
  unchanged on ordinary dimension updates unless an explicit lookup is requested.
- Only intended changed columns are included in UPDATE. Width/height/type edits
  additionally include the existing Kaina value to explicitly dispatch legacy
  chargeable-measure calculation; this does not fetch a new list price or
  blanket-update unrelated audit/mail columns. SQL trigger amounts and source
  SQL final-price expression are authoritative, including money division and
  rounding. Stored final total is synchronized only when changed, following
  the legacy save/close total operation.
- Accessory edits check actual type applicability. Their derived sums are
  normalized after both enabled DELETE triggers, using the current aggregate
  and discount function. This intentionally fixes double-subtraction risk
  without changing shared triggers.
- Product deletion is narrower than unsafe sp_DeleteBlind: only entered/stopped
  products with **no production-output history**. Child deletes run one row at
  a time, no trigger is globally disabled. Empty remaining order totals are
  explicitly zeroed because the historical aggregate INNER JOIN misses them.
- Address writes use real street IDs; duties retain actual SQL uniqueness,
  payroll-cutoff and protected-manager constraints. No acquisition amount is
  accepted from the client. Persisted number/series are immutable in this editor.
- Cancellation uses the real cancellation procedure after an explicit state4/7
  check, independent of privileged SQL-login exceptions.
- Fixed final price uses actual discountable/non-discountable groups and the
  source CCur percentage rounding. Discount changes>=20 remain unavailable
  in this initial shared-production path because they send real subscription mail.

## Remaining independently gated scope

Payment and production-confirmation mutation require additional explicit flags,
contained employee/register/transfer test references and verified production
history behavior. A disabled action is not reported as delivered. Existing
source defects (e.g. unsupported customer action−1/fail-open errors) are surfaced
as explicit validation/dependency failures rather than bypassed.

The current Testavimas RY-TEST default customer has unsupported action−1;
XXXX/XXX1 default customer policy returns2. Do not modify customer policy or
pretend failure is approval to make a fixture pass. An operator may select a
verified customer using the ordinary legacy header-change workflow.

Automated boundary tests do not establish Access runtime parity. Real SQL
execution results/test IDs and remote browser verification belong in the
verification ledger maintained by the parent task.

## Optional financial and confirmation adapters

Implemented typed routes are independently default-off:

- POST orders/{id}/payments with explicit method/date/receiver/reference input.
- POST orders/{id}/confirm with distinct exact product IDs from that order.

`PaymentsEnabled` and `PaymentCurrencyId` are explicit activation options. Native
methods and bank/register membership now come from current Access data sources,
without manual ID arrays. `AllowedPaymentEmployeeIds` adds explicit historical
receiver exceptions to the native visible/allowed-department choices. The owner explicitly
approved the historical d.bykovas receiver77249366 for Testavimas orders, even
though that employee belongs to branch1 and is hidden. Receiver access now uses
the source employee predicate plus explicit exceptions; no employee is unhidden
or moved. Parent-order writes remain restricted to branch7. EKA uses
Loc_leidimas.UKS department scope and applies its actual default without requiring
receiver equality; only the selected EKA's hidden default gains that narrow
exception. Bank choices are LT with positive raw remaining. Feature activation
and currency are not enabled by code.
Initial test posting accepts current SQL date and positive two-decimal amounts
only; backdating/refunds remain unavailable because of real mail/cash effects.
SQL posting period, cash revision, bank assignment and Paid triggers still run.
No payment is edited/deleted through generic CRUD.

GET orders/{id}/payment-references returns actual source methods, receivers,
currency, accessible registers and selected bank details, together with SQL
business date, revision and availability reason. GET payment-bank-transfers
provides bounded search/pages and exact raw selected-reference details.
No fabricated PPK, transfer or register is supplied. A closed actual SYSTEM_USER
period returns definitive422 PERIOD_CLOSED before the first DML and no receipt.
The owner chose EUR currency2 for new Sales payments; it must match the order's
currency. Historical rows remain unchanged: current VBA and the latest1000
observed legacy payments use currency1 against EUR orders, with raw amounts
added to Paid/cash and no exchange conversion. Actual history labels are shown.

Payment identity is captured with OUTPUT INTO; the successful payment ID is
stored atomically in the new nullable technical receipt `RelatedEntityId`,
separate from ProductId. The explicit001 deployment script adds that column
idempotently to an existing receipt table. Reapply it before enabling either
optional adapter. Core order/product operations do not require the new column.
Replay/outcome DTOs return typed PaymentId or ProductionDocumentId as appropriate.

`ProductionConfirmationEnabled` and `ProductionDocumentPrefix` are separately
required. This adapter retains the actual document+individual barcode detail
path, source customer action1 and source narrow stock-view guard; unknown
customer actions block explicitly. It rejects cancelled orders/incomplete or
nonmanufactured lines and confirms selected rows atomically rather than the
historical partial loop. No generic state patch or trigger disable is used.
Stored-line eligibility requires an actual matching type/category, nonnull
price/dimensions/quantity, positive measures required by the pricing unit, and
the existing fixed-price trust policy. It reuses entry's numeric/storage rules
and validates stored PPP12/material without recomputing formulas, repricing or
overwriting manual parameters. Unrelated historical note length does not block
confirmation. Persisted IsManufactured/PriceUnitId facts come from SQL, so an
inactive type is not lost merely because the new-selection picker excludes it.
The explicitly configured Sales document number uses a prefix and the complete
128-bit GUID encoded as26 uppercase Base32 characters, not the unsafe Access
COUNT+1 sequence. The date remains in the separate SQL date column. Its
operational number-format compatibility must be verified before the flag is
enabled; a distinct namespace does not isolate shared production selectors.

A branch7 receiver with a large existing cash balance is not a disposable test
fixture. Source implementation and default-off tests do not authorize use of
that receiver, real bank assignments or confirmation processing. Until an
explicitly contained fixture/namespace is configured, UI capability flags remain
off and these actions are **unverified**, not reported as delivered to test.

Final-price responses preserve the current CCur percentage formula and SQL
money rounding. If the committed/read-back total differs from the operator's
requested fixed amount, the result includes an explicit warning naming both
amounts; it does not silently alter discount/accounting rules or claim the
requested amount was stored. The warning is reconstructed on receipt replay.

Payment history reads retain every original Mokejimai field for revision and
left-join Zinynas_valiutos for actual CurrencyCode. Currency1 renders LTL,
not a fabricated EUR symbol; unknown reference labels remain an explicit
currency-ID fallback. This read-only display is independent of posting flags.

The actual production-document number column is nvarchar(64)
(sys.columns.max_length128 bytes), but downstream
`SP_WriteOffBlindComponentsNew` accepts nvarchar(32). The gated number helper
therefore allows an ASCII alphanumeric prefix of one through five characters,
a hyphen and all128 GUID bits encoded in26 uppercase Base32 characters:
at most32 characters, with no GUID truncation. The command also checks current
COL_LENGTH before inserting. Tests recover every GUID bit and verify uniqueness
for all128 single-bit positions. Confirmation remains separately default-off:
the real production selector includes branch7 and its GT counter counts all
documents. No number prefix can establish operational containment on its own.

Standalone Priedai material selection is source-specific: Smulkios detalės
(type2112842477) uses the piece pricing unit, manual/any price mode and twelve
NULL formulas. The legacy insert default Spalva0 remains valid when no material
is consumed and no list lookup is requested. A category having possible colour
mappings is not a mandatory-material rule. Positive PPP12 consumption or an
explicit list lookup still requires a selection; negative material demand is
rejected, and every changed nonzero selection retains SQL applicability/stock
validation. No dummy fabric or category-name bypass is introduced.


## Warning and retry compatibility

Commit stores server-generated warning messages in the same technical receipt
transaction. Same-key replay returns the current business snapshot plus the
committed command's warning messages; outcome lookup exposes those messages
only to the original actor after the order's scope is rechecked. UI reload
recovery merges and displays them instead of overwriting them with a generic
recovery notice. Legacy receipt rows with NULL/missing warning metadata return
an empty list; original lost warnings from those older builds cannot be
reconstructed. Fixed-final rounding warnings are additionally derived from the
actual current returned total in the direct final-price response and same-key
final-price replay. This derived rounding warning is added after the receipt
transaction; command-outcome lookup alone does not recover its text. The actual
committed total remains available in the returned order snapshot and UI.

The newly optional CalculationRevision property is omitted from canonical
serialization when NULL. Reading an older pending configuration body must not
inject a null field into the original receipt hash. A regression case compares
the exact pre-property serialized input with the new decoder/encoder.

A controlled technical-only integration test exercised warning insertion,
new-service-instance replay, authenticated outcome lookup and other-actor
isolation. Its mutation callback wrote no business row; the business revision
was unchanged and its one synthetic verification receipt was removed with
exact actor/command/operation/order/series/branch predicates afterward.
