# Milestone 2 — Sales order entry on the shared legacy database

Status: implementation and controlled Testavimas verification are in progress
on 2026-10-31. Core header/product/priedai entry is connected to the shared SQL
database and deployed to the remote test stand; the current evidence and gaps
are in [milestone-2-verification.md](milestone-2-verification.md). Cash payments are now verified for the owner-approved d.bykovas/EUR scope;
production confirmation remains disabled because shared legacy selectors include
Testavimas products;
cancellation is explicitly deferred by the owner; do not apply the shared audit bigint fix;
full M2 acceptance has not been claimed. This is the controlling M2 plan; the
earlier discovery sequence is historical.

Current Desktop source is now confirmed and audited. The controlling rule map
for implementation is [milestone-2-current-audit.md](milestone-2-current-audit.md)
and its detailed product/edit/form evidence. Do not assume universal header
locks, universal stock policy, sticky PPP overrides or price lookup on resize.

## Fixed boundaries

- Preserve the approved M1 operator screens: full-width product entry, compact
  parameters/PPP/priedai, keyboard flow and dimension-responsive SVG preview.
  The preview fits the drawing to its panel; it is not a calculation engine.
- Access and Sales use the same production SQL Server database. A committed
  change appears in the other client after refresh. No second order store,
  synchronization worker or Warehouse SalesOrder integration.
- Exercise writes only on verified `Testavimas` orders and its real series.
  Enforce this in the API for **every** command, including child IDs, payments,
  deletion and state changes. Resolve parent/order/branch in SQL; never trust
  a submitted branch. Moving an order into/out of the scope is not a bypass.
- Eventual business access is all branches. Read scope and the temporary write
  restriction are separate policies. Ordinary-branch writes require a later
  explicit rollout decision from the owner.
- Manual numbering: in 2026 `VV-123` → `VV-123-26`, `VV-123-A` → `VV-123-26A`.
  Validate live series and the SQL unique key. Do not auto-allocate.
- Procedures/triggers remain authoritative where legacy uses them. Reproduce
  VBA-only rules on the server with parity evidence. No arbitrary Eval,
  browser-supplied executable formulas, disabled triggers or skipped checks.
- The design-review link remains sample-only, with no real lookups/orders or
  write access. A real SQL failure must not silently fall back to demo data.
- Scope includes creation **and reopening/editing** saved orders where legacy
  state permits it. Production return and warranty repair remain outside M2.

## Review findings affecting implementation

These are the original review baseline, before implementation. Authentication,
tests and source recovery have since advanced; current status is in the audit
and verification documents. These findings are not passed write/parity tests.
Paths below are relative to `src/Modules/Sales/` unless stated otherwise.

| Finding | Required implementation consequence |
| --- | --- |
| `LKvitai.MES.Modules.Sales.Api/Program.cs` accepts `PortalStructuredBearer`; the parser/handler in `src/BuildingBlocks/LKvitai.MES.BuildingBlocks.PortalAuth/` accepts text claims without a signature and permits absent expiry. The checked-in test proxy exposes `/api/sales`. | Trusted authentication is a prerequisite to writes, not a final smoke test. Do not inherit the current default policy for commands. |
| Existing details/item DTOs carry display labels, lack product/accessory IDs, editable foreign keys, PPP and revisions. `DemoOrderDraft` uses local objects/names. | Add dedicated ID-based edit/command contracts. Do not post display DTOs or map the demo draft directly to SQL. |
| `SqlOrdersQueryService` obtains a detail summary through text search limited to five results and substitutes zero/empty for absent fields. | Exact ID-based editor reads with strict mapping; search collisions must not hide records, missing contract columns must fail visibly. |
| M1 assumes 200 mm minimum, 800×1500 defaults, English control values, string PPPs and demo rounding/discount rules. | These are scaffolding, not verified business rules. Derive per-type constraints, codes, null handling and precision from legacy. |
| Snapshot: 26 categories/290 types, eight `CarrierQnt*`/`CorrectorsQnt*` function variants plus `Round`; no series has a store label identifying Testavimas. | Counts are a baseline, not a hardcoded coverage limit or write allowlist. Resolve current Testavimas IDs and full function coverage from SQL/VBA. |
| Original `Desktop/uzsakymai.zip` is absent; main/LT ADP VBA has now been recovered read-only. | See recovered-source evidence; confirm active deployed version and runtime form metadata before claiming parity. |
| Sales CI builds images, but has no dedicated Sales behavior tests. | Add targeted tests/CI as slices are implemented. Warehouse SalesOrder tests do not cover this bridge. |

## 0. Confirm contracts and prerequisites for writes

Refresh **read-only** SQL metadata and relevant VBA inspection. Record source
version/date and definition hashes without credentials/customer data. Reading
procedure definitions is discovery; executing Check/Calculate/Preview procedures
is not presumed read-only.

Current source is the owner-confirmed `/Users/bykovas/Desktop/Uzsakymai.adp`.
It has been extracted read-only and compared to the historical Dropbox files;
current source/design takes precedence. See the current audit for hashes,
traceable rules and remaining runtime/numeric-property evidence boundaries.

Deliver `docs/sales/milestone-2-contracts.md` with one row per UI action:
SQL entry point/tables/keys; input types/nulls/units/defaults; VBA event and rule
order; allowed states; triggers/side effects; transaction; returned IDs;
audit actor; retry semantics; verification scenario. Include header, product,
PPP, type change, duplicate/delete, both priedai forms, duties/address,
pricing/discounts, payments and confirmation.

Resolve before enabling the first write:

- **Target/scope:** prove API and Access target the same database; identify
  Testavimas branch/series IDs and default people/customer. A branch name alone
  does not prove containment: trace mail, payroll, material, financial and
  scheduled processing effects. Record the particular test records used.
- **Safe identity:** trace `sp_CreateNewItem` and nested triggers. Do not use
  `@@IDENTITY`, `MAX(Id)` or `IDENT_CURRENT`. `SCOPE_IDENTITY()` in the caller
  after `EXEC` does not capture the insert in the procedure's scope. Select a
  proven return/output contract at the insert, preserving legacy behavior.
  Any wrapper/procedure change needs an exact script and Access compatibility
  evidence. Re-read final values after triggers.
- **Trusted actor:** choose Sales-specific cryptographically validated
  WebUI→API authentication derived from a valid Portal session; prefer existing
  DataProtection infrastructure, bounded expiry and Sales audience/purpose.
  Commands must reject raw structured bearer, review/DevAuth identities and
  request-supplied actors. If exposing cookie-authenticated commands, require
  antiforgery protection. Move real Sales reads to the trusted path too;
  preserve Portal login. A platform-wide authentication rewrite is out of scope.
  Explicitly select the trusted scheme in API policies; do not sign or promote
  claims originating from the old unverified bearer or a merged guest identity.
- **Concurrency/retries/audit:** settle the actual SQL/storage approach for the
  protocol below. Record trusted Portal user ID, command ID, legacy IDs, time
  and outcome alongside legacy `SYSTEM_USER` audit. Do not impersonate SQL
  users or overwrite trigger audit fields.
- **DDL:** inventory required additive command-receipt/audit storage or wrappers.
  Technical receipts are allowed, another order store is not. Keep scripts
  explicit, versioned and backward compatible, separate from Warehouse/Portal
  EF migrations. No schema patching on app startup or automatic CI execution
  against production. Record deployment and application-rollback compatibility.

Create `docs/sales/milestone-2-verification.md` with coverage, safe scenarios,
known gaps and evidence. Initially mark tests **not run**, not passed. Actual
Access comparison requires Access execution; SQL reads cannot establish VBA
behavior. Identify how to run the correct Access client before claiming parity.

**Gate 0:** concrete decisions above, recovered sources or precisely blocked
areas, verified scope and the chosen first product type. No dependency of that
first slice may be bypassed. Report these artifacts before implementing it;
blocked payment/type work does not prevent independent read work.

## 1. Live references and exact editor reads

Implement and test the trusted Sales authentication path selected in gate 0
before exposing the new live endpoints; remove raw bearer acceptance from
Sales real-data endpoints while preserving the existing Portal user journey.

Add series/default IDs, branch, customer, employee/duty, category/type, control,
PPP, colour and applicable-accessory lookups. Search/page large sets and load
per selected type. Set a bounded reference TTL (initial target ≤60 seconds)
and explicit refresh; revalidate references on save. Stock/credit cannot use
cached values as authority. Exclude inactive references from new selections,
but preserve them on historical orders and unrelated edits.

Read the editor by `UzsakymasID`, including product/accessory/assignment IDs,
editable/calculated fields, original revisions and allowed actions. Missing
contract columns are errors, not defaults. Resolve `Loc_leidimas()` and the
branch-7 exclusion in Sales so list/detail/filter/editor scope agrees, without
globally changing Access permissions. Never reopen via a paged text search.

**Gate 1:** live employee lookups and exact Testavimas editor read; inactive
historical IDs/values remain present in the edit buffer; review users remain sample-only and
are denied real API access. Writes stay off until their slice rules and command
protocol are implemented.

## 2. One complete persistent order/product slice

Implement header create/read/update and **one verified product type**. Its
applicable PPP, eligibility, pricing and stock rules from gates 3–4 are required
**now**. Those gates expand coverage; they are not permission to save an
incomplete first product. Unsupported actions/types stay visibly disabled.

- Create persists the header before lines, returns ID/canonical number and
  reads back trigger results. Do not duplicate default duties inserted by SQL.
- Type selection follows the verified legacy creation path. Distinguish an
  intentionally incomplete persisted line, if legacy permits it, from invalid
  data. Never persist invented M1 dimensions, control codes or zero prices.
- Keystrokes update an edit buffer; calculation previews do not save. `Done`
  saves the product operation and returns only on success; order Save saves
  header changes. Before switching lines/types or Back/close, save or offer
  discard/stay for dirty inputs. Closing never silently deletes saved records.
  Resolve pending parent edits before an accessory/duplicate command whose
  calculation depends on them; do not mix unsaved dimensions with saved PPP.
- Show unsaved/saving/saved/validation/conflict/result-unknown states. Serialize
  saves per editor; discard stale preview responses, preserving newer typing
  and focus. Retain the buffer on 401, reconnect or timeout; resolve outcome.
- Add an Edit/reopen action for permitted saved orders, including Access-created
  orders. A persisted ID-based route survives reload/new session; the current
  `#sales-product` history fragment alone is insufficient.

**Gate 2:** Sales save → Access reopen and Access create/edit → Sales reopen;
IDs, fields and amounts match. Demonstrate repeated create after a lost reply,
duplicate-number conflict, stale Access/Sales edit, close/reopen and continued
editing. An unrelated save preserves inactive historical references. Nothing
is called saved solely because a local model changed.

## 3. Complete product configuration and priedai

Build a live category/type capability matrix: inputs, validation and all rule
dependencies for each enabled type. Include services, `Be gaminių`, standalone
`Priedai` and attached `ParduotiPriedai` with verified parent/FK contracts.
Never derive persistence kind from a translated label, or require dimensions
on non-dimensional entries because M1 did so.

Port each distinct VBA function/formula family using an allowlisted server
implementation. Verify order/dependencies, conditional PPPs, precision,
rounding, null/empty/zero and invalid-input behavior. PPP may be numeric, text
or options: derive the actual contract. Compare boundary cases for every
`CarrierQnt*` and `CorrectorsQnt*` variant, not just normal-size examples.
Formulas stay server-controlled.

Define legacy override behavior on dimension/colour/type change and reopen.
Do not persist demo `ParameterManual` flags as a new business rule without a
verified representation. Verify accessory quantities/applicability/overrides
for `vnt`, `kvm`, `m`, `1m pločio`, `1m aukščio`, `kmpl`, fractional rounding,
and the unknown-unit/unnamed entry. Type changes must reset/preserve the exact
legacy fields. Duplicate produces new IDs/barcodes and appropriate configuration,
not copied audit/payments/production status. Delete uses the verified legacy
path, checking state and child ownership. Current `sp_DeleteBlind` globally
disables a production trigger; do not call it blindly. Establish a contained
compatible path before exposing deletion.

**Gate 3:** every active category/type accounted for; tests cover each distinct
rule and stored field shape, manual overrides, type changes, duplicate/delete
and fractional priedai. Disabled unsupported types are incomplete M2 scope
until implemented or explicitly deferred by the owner.

## 4. Complete pricing, stock and eligibility coverage

Complete price groups/periods/customer selection, VAT meaning, unit/line/order
rounding, accessory prices, discount eligibility, fixed price and debt. Use
verified SQL precision/scale and legacy rounding; do not promote demo totals
or `AwayFromZero` to authoritative accounting. Preserve manual/list/fixed-price
semantics across reopen and recalculation.

Trace material need, free/reserved stock and shortage actions from
`TBD_Components.iActionIDWhenNotInStock`. Trace credit/late-payment behavior,
including `sproc_CheckIfNewOrdersAllowedByCustID_forVBA`, at the real decision
points: creation, product add, customer change and confirmation as applicable.
Do not defer an applicable check to confirmation for convenience.

Preview must be read-only. Revalidate on commit; changed authoritative
price/configuration returns an operator-review conflict instead of silently
saving a different commercial value. Carry a server-issued calculation/quote
revision so reference or price-list changes can be detected as well as order
edits. Record stock/credit consistency limits;
never promise reservations or atomic global credit enforcement unsupported by
legacy. A preview must not invent stock movements.

**Gate 4:** prices/overrides, PPP, priedai, discounts, VAT, final/paid/debt match
Access at stored precision. Test period boundaries, halfway rounding, warning/
block cases and stock/customer changes between preview and save.

## 5. Complete order details, payments and transitions

Connect remaining address, duties, priority, mounting/due dates and notes.
Verify series/customer/branch changes and state-dependent editing; do not
silently reset overrides when defaults refresh. Payment operations stay
separate from ordinary header saves.

Trace PPK/transfer/cash/EKA separately: does each action create a payment, link
an existing document or invoke another workflow? Preserve document numbering,
receiver/register, posting period, audit and totals. No fabricated financial
documents or retry under a new command ID. The legacy parameter-check button
is a message-only stub; define the real validation checks explicitly. Use the
verified confirmation document/detail path; generic updates must not assign arbitrary status codes.
Document state/action rules including deletion/cancellation. Draft cancellation,
if needed, uses its real legacy path, not a destructive cleanup job.

For external/financial effects, establish an actually contained test path.
Use an isolated copy for unsafe experiments. If unavailable, continue other
work and record the action as disabled/unverified, not completed. Never disable
production triggers/jobs for testing or assume rollback reverses external mail.

**Gate 5:** a complete Testavimas order with applicable payment methods and
confirmation agrees in both clients. Failed multi-row operations are atomic;
evidence includes records and side effects, not merely a success message.

## Command protocol — required from the first write

| Concern | Required contract and verification |
| --- | --- |
| Layers | Typed IDs/commands/results in `Sales.Contracts`; orchestration/ports in Application; parameterized legacy adapter in Infrastructure.Sql; API auth/result mapping; WebUI buffer. Keep existing query DTOs compatible. No generic repository/workflow engine/Warehouse persistence. |
| Command | Separate header, product/configuration, accessory, address/duty, payment and transition operations. Include command ID, target IDs, expected revision and editable inputs; return committed IDs, authoritative values, next revision, warnings/errors and allowed actions. Never accept audit/arbitrary state/calculated totals from clients. |
| Transaction | Short transaction, one SQL connection per operation: current-state checks, writes, receipt and transactional audit commit together. Enlist every command; verify legacy transaction/error handling and lock order. No UI-think-time transaction or network call inside it. |
| Triggers | Update only intended changed columns in the verified event sequence. Unchanged columns in `SET` can still satisfy `UPDATE(column)` inside a trigger. No blanket aggregate DTO update. Read affected fields after triggers and return success only after commit. |
| Identity | Capture at inserting scope/proven return contract, test nested trigger inserts; never guess largest/newest ID. `OUTPUT` values precede AFTER-trigger changes and are neither final readback nor proof of commit. |
| Concurrency | Prefer existing rowversions covering touched rows; otherwise a server-issued version of original typed values/child membership, atomically compared with writes using SQL locks/predicates. Include parent state and changed children. Parent-only timestamps, in-memory locks or Sales-only application locks do not detect Access edits. No check-then-write gap or `NOLOCK`. Finalize actual SQL in gate 0. |
| Conflict | Stale writes return 409/current revision+values; keep local buffer for reload/compare/reapply. Test actual Access bound-form protection: Sales cannot prevent a later unconditional Access overwrite without changing Access. Document this limit rather than promising an unverified guarantee. |
| Idempotency | Durable unique receipt keyed by actor/operation/command ID with canonical input hash, IDs/result, in the same DB transaction as mutation. Same key+payload replays without rerunning triggers; changed payload conflicts. Must survive restart/multiple instances. No memory cache or second DB as an atomicity substitute. Specify retention covering the supported retry window; expired keys must not be blindly resubmitted. |
| Unknown outcome | Timeout/disconnect/cancel after submission does not prove rollback. Resolve the existing ID via authenticated outcome lookup or same-key replay. Never retry create/payment under a new ID. Revalidate scope/actor before returning receipts. After recovery, read current entity state: a receipt's original result may precede later Access edits. Retry only proven rolled-back transient failures with a bounded policy. |
| Number | Server uses Europe/Vilnius current year at creation; stored group then stays fixed. Already canonical current-year input is idempotent; old orders never renormalize on reopen. Verify SQL range/length/collation, leading zeros and prior-year input before accepting ambiguity. Prechecks help UX; SQL composite uniqueness arbitrates simultaneous creators. |
| Errors | Field-addressed validation 422, invalid shape 400, auth 401/403, missing 404, duplicate/stale/changed quote 409, dependency unavailable 503. Stable codes for known legacy failures, correlation/command ID, no SQL/connection-string leaks. Distinguish unknown outcome from confirmed failure. |
| Write gates | Server default off; explicit DB/test scope and supported command/type set. Deny invalid scope, guests, synthetic identities and cross-order children. Payments/confirmation independently gated. Environment name Test is not permission to write any production row. |

SQL semantics follow Microsoft's documentation for
[SCOPE_IDENTITY](https://learn.microsoft.com/en-us/sql/t-sql/functions/scope-identity-transact-sql),
[OUTPUT](https://learn.microsoft.com/en-us/sql/t-sql/queries/output-clause-transact-sql) and
[UPDATE in triggers](https://learn.microsoft.com/en-us/sql/t-sql/functions/update-trigger-functions-transact-sql).

## 6. Verification and release on the remote test stand

Maintain the verification matrix **throughout each slice**, not only at the end:

- Unit/contract: numbering/year rollover, formula/unit/rounding boundaries,
  allowed states, mappings and error translation.
- SQL integration: real trigger/procedure behavior, IDs, rollback, duplicate/
  timeout-after-commit replay, changed child collections, concurrent editors
  and restarts. Routine CI uses isolated fixtures/copy, never the production
  connection. Testavimas production verification is separately controlled.
- Auth/scope: valid Portal, expired/tampered credential, raw structured bearer,
  DevAuth, review guest, wrong branch, forged parent/child IDs, gates off;
  denied requests cause no writes or real-data exposure.
- Remote browser plus Access: saved header → product/PPP → both priedai forms
  → amounts → details → payment/confirmation → close/reopen/edit. Check keyboard
  flow, compact layout and live preview. No SQL writes per keystroke, unbounded
  lookups or late-response focus/value resets. Record load/save timings and
  investigate timeouts instead of only increasing proxy time limits.

Each evidence row: date/build, scenario, test IDs/number, inputs, Access result,
Sales result, stored values, pass/fail/gap. Compare both directions including
changes while the other editor is open. Protect customer data in fixtures and
screenshots. Record retention/cancellation of test records through verified
legacy operations, never blanket deletion of Testavimas.

Deploy slices behind gates to `cikada-test.lauresta.com`; verify actual build
and authenticated path, not just guest demo. Verify disabling writes and app
rollback leaves reads, Access and saved orders usable. Do not undo shared
schema or delete orders as application rollback.

**M2 done:** all in-scope active types/actions have parity evidence or explicit
owner-approved deferral; full remote Testavimas flow and command/auth checks
pass; gaps stated. Owner reviews before ordinary-branch writes are activated.
Successful test deployment does not implicitly authorize production rollout.

## Handoff to Sol

First assignment: finish gate 0's contract/verification documents and report
chosen auth, identity, concurrency, retry/audit and test-scope contracts.
Start read-only; no production writes/DDL while those decisions are unverified.
Then implement the smallest complete slice in sequence. A blocked dependency
blocks that action, not unrelated work; disabled/unverified is not completed.

After each gate report implemented behavior, deployed build if any, test order
references, comparison evidence, gaps and next gate. Scope commits/deploys to
the slice and follow existing authorization/repository rules. No UI redesign,
background sync, new planning, schema cleanup or platform-wide auth rewrite.


## Owner-authorized extension — per-user SQL identity (2026-11-01)

The owner now explicitly requests mapping authenticated Cikada users to legacy
SQL logins and `EXECUTE AS LOGIN`. This supersedes the earlier temporary
all-branch read policy and prohibition on impersonation. Admin maps explicitly
to `sa`; new Cikada user `bykovas` receives the same Cikada roles/permissions as
admin and maps to exact Windows login `TERMINAL\d.bykovas` (not the distinct
SQL alias `d.bykovas`). No passwords belong in source or documentation.

All live Sales reads and writes use a trusted active-user lookup and an isolated
SQL lease; branches follow actual `Loc_leidimas()` including direct-ID/number,
children, preview, references and command recovery paths. Existing Testavimas
write containment, production confirmation/cancellation flags and owner-deferred
SQL fixes remain in force. Do not modify legacy branch assignments or grants
to make tests pass. Persist Cikada identities/mappings across restart, preserve
existing admin identity, verify cross-branch denial and deploy to remote test.
Unrelated findings go to issues; no broad platform rewrite or dependency upgrade.
