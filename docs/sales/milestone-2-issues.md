# Sales milestone 2 — issues and owner decisions

Updated 2026-11-02. This file is the issue index requested by the owner.
Execution evidence: [verification ledger](milestone-2-verification.md).

## S2-001 — Order cancellation: deliberately disabled

**Status: deferred by owner, not approved for SQL repair.** Owner decision:
“ostavit vykliucennyj i zapisat v fail issues, posle vsego skazat mne gde on”.

New `Uzsakymai.UzsakymasID` values exceed signed32-bit range. The unchanged
`trg_ChangeOrder` cancellation path inserts this bigint ID into
`dbo.AUDIT_Anuliavimai.OrderID int NOT NULL`. Actual cancellation of the owned
Testavimas order2148197450 overflowed; the transaction rolled back. The order
remained active, with no successful cancellation receipt or audit insertion.

The inspected audit table has6732 existing rows. The proposed exact column
widening in [002-cancellation-audit-bigint.sql](../../scripts/sales/002-cancellation-audit-bigint.sql)
would affect the shared production schema for all branches. **It has not been
applied and must not run under the current authorization.** No trigger,
procedure or audit behavior may be bypassed as a substitute.

`Sales:OrderEntry:CancellationEnabled` defaults false and is explicitly false
on test. Both old int-sized IDs and new bigint IDs are denied before SQL;
the editor disables Cancel and displays the owner-deferred reason. A later
schema fix alone must not enable cancellation: separate explicit activation
would be required. Existing order entry/price/PPP/priedai actions remain usable.

Owned test orders2148197448/449/450 and follow-up fixtures453/454/455 are retained active. Their material
reservations remain present until a verified permitted cleanup action is chosen.
No blanket cleanup or original Testavimas-order edits have been performed.

Acceptance for a future cancellation repair: owner authorizes the exact shared
schema migration and activation; preserve all existing audit rows; exercise
legacy cancellation atomically; verify same-key recovery, post-cancel child
write denial, state/readback and release only the owned test reservations.

## S2-002 — Access bigint narrowing and runtime parity

**Status: open; Access binary unchanged.** Current ADP source contains VBA Long
assignments in active product refresh/price paths while SQL IDs exceed2^31.
The narrow proposed source patch is documented in
[legacy-bigint-compatibility.md](legacy-bigint-compatibility.md).
SQL/browser roundtrips do not establish actual Windows Access UI behavior.
An execution with the current Access client remains required for full parity.

## S2-003 — Production confirmation containment

**Status: owner-authorized activation on test for manual verification (2026-11-02); successful production confirmation not yet runtime verified.** The optional server command follows
the actual document/barcode-detail path. Actual SQL consumers
`V_WaitingProductionBlinds` and `sp_SelectBlindsForProductionIntoTPRODBLNDS`
include confirmed Testavimas products without a branch7/prefix exclusion:
normal nonVIP products become eligible the next day, high-priority/VIP products
can enter immediately. Active legacy Access consumers exist. A Sales namespace
does not provide isolation. The implicit GT daily counter also counts all
production documents, even explicitly numbered Sales documents.

The immediate confirmation trigger chain updates document audit, product state,
order aggregate state and that order's salary rows; no immediate mail/print or
stock writeoff was found. This does not remove eventual production exposure.
The owner explicitly requested enabling confirmation on test and will verify it
manually. Enable only the existing Testavimas scope, with document prefix MEST;
ordinary-branch writes and cancellation remain disabled. Automation must not
create confirmation documents as part of activation checks.

Downstream `SP_WriteOffBlindComponentsNew` uses nvarchar32 despite the document
table's nvarchar64. The gated adapter's number format now uses a full128-bit GUID encoded as26 uppercase Base32
characters with a maximum five-character prefix and separator (maximum32 total).
No identity bits are truncated; date remains the separate document field. No actual confirmation
or production-document record was created by these checks.

## S2-004 — Native payment cascades and financial verification

**Status: four native methods implemented; live lookup/validation/browser checks passed.**
Cash81423987, PPK1, bank2 and EKA834019474 use the actual Access lookup and
cascade rules, with server revalidation. No manually supplied bank/register ID
allowlists are required. Receiver selection follows visible accessible native
employees plus the selected EKA's exact default; approved77249366/d.bykovas
remains an explicit exception. His hidden employee record was not changed.
Writes remain restricted to the configured Testavimas parent orders.

Changing method clears irrelevant references/amount/notes/receiver. Bank/EKA
selection applies its actual editable receiver default. Changing the receiver
does not clear the chosen EKA. PPK uses the native ten-character receipt format.
Amounts follow DECIMAL(18,2) payment storage; raw bank balances retain up to four
decimals, with subcent balances unavailable for a positive stored payment. New
postings use owner-approved EUR2 without inventing a bank source currency.

Seventeen real HTTP/SQL negative cases passed before DML with unchanged
payments, Paid, cash, bank assignments/notes, products/PPP, audits and receipts.
Remote browser checks passed all four cascades and input precision. Earlier
positive cash/replay checks retain exactly two0.01EUR payments on owned450.
There is no identified disposable native bank-transfer fixture; arbitrary real
bank funds, made-up PPK receipts and real EKA financial postings were not used
for positive tests. This remains a verification boundary, not a hidden UI gate.
Refunds/backdating remain outside the enabled positive current-date flow.

## S2-005 — PPP02 Nera semantics

**Status: resolved, implemented and SQL-persistence verified.** Owner
confirmed the bare legacy formula token Nera means literal text “nera” for the
six Fakro/Roto types. No legacy SQL formula or ADP rewrite is requested.
All six actual type configurations were created, saved and read back on the
owned Testavimas order, then safely deleted to restore the original lines/total.
The adapter interprets only this exact text token in text PPP slots;
unknown expressions and numeric PPP12 must retain explicit validation.

## S2-006 — Final-price recovery warning text

**Status: minor known limitation.** Direct final-price response and same-key
replay report any legacy rounding difference. The extra warning is derived
after the receipt transaction, so outcome-only recovery after a reload does
not reconstruct that specific warning text. The committed actual total is
always read and displayed; eligibility/material warnings are durable.


## S2-007 — Shared SQL payment work period and notification

**Status: resolved; approved extension and normal notification executed.**
`sa` uses a fixed financial work period2001-01-01..2026-01-01, so current-day
payments are rejected. The owner approved extending its end through2030,
matching the existing `d.bykovas` end2030-01-31. This is a shared SQL-login
setting, not an employee/Portal role. Receiver permission alone cannot replace it.

Guarded script [003-sa-work-period-2030.sql](../../scripts/sales/003-sa-work-period-2030.sql)
changes only that end date and retains the original trigger. A first guarded
attempt aborted before UPDATE because an unreviewed enabled trigger existed;
no setting/payment was changed. Full inspection found the normal trigger writes
`SYS_UsersWorkPeriods_log` and sends three administrative notifications. Explicit
permission for that normal notification was requested separately and granted. The trigger
and recipients must not be disabled, rewritten or bypassed to make tests pass.
The applied script fingerprints the reviewed trigger, honors the legacy database's
recipient-concatenation setting and skips UPDATE on same-state replay, so it
cannot repeat the notification for an already extended period.

The exact authorized update changed only sa.IndividualPeriodLastDate to
2030-01-31; all other sa fields and every other user remained unchanged. The
actual source function now returnsY for the current SQL login/date. A closed
period still disables Post and yields PERIOD_CLOSED before DML in the generic
flow; no impersonation, period-function or trigger alteration was introduced.

## S2-008 — New payment currency: owner decision

**Status: resolved.** The owner explicitly selected EUR (currencyID2), matching
new order headers. Existing legacy payments retain their original ID1/LTL
metadata; no historic rows or conversion rates are changed. The writer requires
new payment currency to match the actual order currency and performs no invented
conversion. This is a deliberate new Sales persistence convention, rather than
an assertion that the old VBA's hardcodedID1 already storedEUR.


## S2-009 — Orphan active type reference

**Status: existing data issue, no reference edits performed.** Global active
metadata includes type2112842799/Porta with no category. The live category/type
join accounts for290 rows; the global active count is291. The orphan cannot be
offered as a valid new product category/type pair. Historical snapshot labels
and IDs remain readable. Assigning its intended category needs an explicit
business decision; the adapter must not fabricate one to reach a type count.


## S2-010 — Standard period-notification delivery

**Status: failed delivery in existing Database Mail infrastructure.**
The owner explicitly approved the three-recipient administrative notification.
The authorized SA-period update created normal work-period audit and Database
Mail item1121186. Its first observed status was retrying; final status was failed. The database
received/queued the normal notice, but delivery was not successful. Do not manually send a
duplicate notification or change SMTP settings as part of Sales entry work.
The financial period update itself committed and current-date validation isY.


SA work-period operation was also reapplied as an idempotency check: it returned
already-extended without UPDATE, no additional work-period audit and no second
notification. The unchanged legacy notification/audit behavior remains intact.


## S2-011 — Recovery after unknown outcome and later authorization failure

**Status: resolved in final release.** An initial transport/
503 failure can occur after a payment committed. A later401/403 on same-key
retry (expired auth or a disabled deployment gate) does not prove that first
operation rolled back. The client must retain the original key and immutable
body once any attempt had unknown outcome, until successful replay/outcome
recovery. Fresh first-attempt definitive rejections may still release their
pending key. Regression coverage exercises unknown→401/403→same-key success with the
exact original body/key and confirms that first-attempt definitive rejections
still release their key. No SQL posting is performed by those isolated tests.


## S2-012 — Critical intermittent product-create delay

**Status: open; reporting overload confirmed, original Sales wait not recovered.**
The owner's product-create on2148197452 returned503 after30.059 seconds.
Historical system_health and job records identify UpdateSalaryReportsData during
that exact window, with SQL CPU99% and two duplicate heavy completion-date
calculations consuming about11/12 cores for almost33 minutes. The turnover
pass alone read2.633 billion logical pages. The specific failed Sales SPID/wait
is absent, so CPU contention must not be declared the proven sole cause.

Current controlled Linux adapter checks on owned453/454 completed creates in
148–415ms, including parallel writers/reads. All eight temporary products from
the local/Linux diagnostic runs were safely removed. Original product data,
accessories/payments and totals remained;453's derived aggregate NULLs became0
through native deletion, with total unchanged. No owner order was edited.

A review-only set-based calculation took964ms and matched23,858/23,858 shared
persisted salary dates. [SQL proposal](s2-012-completion-dates-proposal.sql) and
[validation review](s2-012-completion-dates-review.md) are **not applied**.
No production procedure/job/schedule/index/isolation setting was changed.

**Owner decision, 2026-11-01: record the optimization here and defer it.**
The follow-up is to replace only the duplicated completion-date `#R` calculation
in `sproc_GenerateSalaryReportData` and `sproc_GenerateTurnOverData`, preserving
the existing order-wide timestamp grouping and all remaining report logic.
Before any shared-production change, prepare exact procedure fingerprints,
a guarded migration and rollback, and repeat result/concurrency verification.
The review-only proposal is not an approved production migration; this product
entry UX work must not apply it or change the reporting job/schedule.

A bounded read-only observer captured slow Sales SQL requests, exact wait
resources and blockers until2026-11-02T03:02:11Z (05:02 Vilnius). It was
validated against an own-fixture row lock and had CPU/memory/log caps. Its
window has expired; the v23 deployment verified the observer absent and did
not restart or extend it. Do not describe it as active monitoring.
See [investigation](milestone-2-product-create-latency.md) and
[observer operations](s2-012-diagnostic-observer.md).

## S2-013 — Interrupted-save recovery and fast navigation

**Status: deployed and remote-browser verified.**
The previous tab-wide bare GUID and circuit-wide pending operation could block
unrelated orders. Reload also lost the original request body. The replacement
actor/order-scoped v2 session outbox freezes and preserves the exact request
before dispatch, so reload can replay the same key/body after a missing receipt.
Initial storage read/write failure prevents dispatch; unknown outcomes remain
preserved after later authorization failures. Other orders remain usable, and
wrong-order receipts/results cannot replace or clear the current input.

This temporary per-tab browser record contains the operation's business input,
not credentials/session proofs. It is cleared after confirmed recovery or a
definitive first-attempt rejection; unresolved records are not deleted by age.
Legacy bare GUIDs are retained as a separate check-only notice. Their lookup
never replaces another order's editor; a found result has an explicit order link.

The editor is keyed by actual route order ID. New-order submission navigates
directly to the saved order without rendering a transient old-dialog editor.
The list's New order button stays disabled during prerender until its live
circuit can handle clicks. Busy and recovery notices use operator-facing text.


## S2-014 — New-product total used the demo fallback before first save

**Status: deployed and remote-browser verified, with unchanged SQL snapshots.**
A fresh live product has no stored amount yet. The shared draft model's fallback
Quantity×UnitPrice could then show20.85 for an R25 whose actual legacy price
unit produces16.68 after saving. Do not present this demo arithmetic as a live
confirmed total. Live product entry now labels the total as saved and shows a
dash until the server amount exists. Existing products show the actual saved
base/accessory total; editing dimensions does not invent a new confirmed amount.
Demo-mode arithmetic and native SQL pricing/totals are unchanged.


## S2-015 — Access order-note display parity

**Status: implemented and locally tested (321 Sales tests), deployment verification pending.**
Current editable note and read-only historical entries are now separate on
order details/editor. History retains every native NoteID/text/author/time in
NoteID order, including duplicate entries; the privacy marker is translated
only in presentation and its stored text is unchanged.
The Access frmNewOrder note action updates Uzsakymai.sNotes (128 characters);
trg_ChangeOrder appends changes to UzsakymuPastabos. Its sp_frmOrder read model
builds sOrderNotes from current sNotes plus the order's historical Note rows.
Sales reads the current sNotes and the same UzsakymuPastabos history used by
Access. Unlike the legacy unbounded-order string concatenation, entries have
a deterministic NoteID order and separately displayed audit metadata. Windows
Access runtime acceptance remains S2-002.

Exact stored text `Kliento prasymu anuliuotas` is also consumed as a business
condition by legacy production selections and salary calculations. It must
remain distinct from generic cancellation state or a translated display label.
Existing SQL consumers were not rewritten, and sp_CancellOrder only sets
Sugadintas; it does not automatically assign this note. Cancellation stays
disabled under S2-001; confirmation activation for manual test is recorded in S2-003.

Remaining verification: compare native history with deployed API/UI reads.
History is read-only, parent-scoped, and excluded from editable command inputs.
No history/price repair, cancellation migration or automated confirmation is
part of this change.


## S2-016 — Invalid design-review cookie caused native Portal login loop

**Status: resolved; deployed and verified in actual Safari, 2026-11-01.**
Native Portal login succeeded and its root cookie decrypted correctly, but the
presence-only Sales review selector sent requests to an invalid Sales-path
review cookie. The default context stayed anonymous even though explicit
`AuthenticateAsync("Cookies")` succeeded; Sales returned to the login page.
This was not a rejected password or failed cookie storage.

`SalesReviewSelectorHandler` now validates review authentication and falls back
to Portal cookie authentication if review fails. Valid review sessions retain
sample-only priority and cannot acquire a real API credential through a second
Portal cookie. No identities are merged; expired/tampered Portal tickets remain
invalid. Eighteen cookie-combination regressions and the existing API/proof
isolation tests pass (256 total Sales tests).

WebUI-only build `m2-0a4bb90` was verified with the existing real Safari/admin
session, without re-login or cookie deletion: Orders, live reference loading,
New order open/cancel and final reload work. Safe diagnostics confirmed one
Cookies identity and HTTP200 session proof/API reads. Temporary diagnostics
were switched off and the final reload still worked. API container/config and
S2-012 observer were unchanged; no business mutation requests were issued.


## S2-017 — Existing Warehouse header authentication outside user management

**Status: open; outside the approved SQL mapping work.**
The existing Warehouse API accepts unsigned structured bearer/user-role headers
on general routes. This task must not use those headers as authority to edit
SQL impersonation mappings or create/elevate Cikada accounts. User administration
is now additionally protected by a short-lived, purpose-bound server proof tied
to the actual Portal subject and the current active persisted administrator.
Sales mapping lookup has its own separate proof purpose and does not trust the
Warehouse header format. A platform-wide Warehouse authentication migration
remains a separate security task; general Warehouse routes were not rewritten.

## S2-018 — Existing dependency advisory warnings

**Status: open; dependency upgrades deferred outside this task.**
Warehouse builds report NU1904 for Marten7.0.0 and NU1902 for
OpenTelemetry.Exporter.Jaeger1.5.1 and SixLabors.ImageSharp3.1.8. Review advisories
GHSA-vmw2-qwm8-x84c, GHSA-38h3-2333-qx47 and GHSA-rxmq-m78w-7wmc in a separate
upgrade/compatibility task. These pre-existing packages were not upgraded as
part of legacy SQL identity mapping.

## S2-019 — Cancelled orders missing from status selection and presentation

**Status: resolved on test, 2026-11-02; build `m2-33fa509`.**
The owner's test screenshot shows no `Anuliuotas` choice. Filter options read
only `Zinynas_busenos.Busena`; legacy cancellation is the separate
`Uzsakymai.Sugadintas` flag, not a replacement StateID. SalesOrdersPaged.sql and
SalesOrderHeader.sql return that flag as IsCancelled, but the list DTO mapper
explicitly ignores it and derives the displayed status only from Busena.
Consequently an already-cancelled order can retain its previous status label
in Sales, and cancellation cannot be selected as a filter. The status map also
recognizes `Atšauktas` rather than the requested `Anuliuotas` label.

Fixed: cancellation precedence is applied consistently to list/details, and
`Anuliuotas` is selected and filtered from the actual flag before paging/counting.
SQL-login department guards are retained. Current adapter/native SQL counts
match for two identities; browser and actual authenticated HTTP checks passed.
The owner-approved privacy notice is shown only for the exact legacy note
`Kliento prasymu anuliuotas`. Customer/products/native amounts stay visible and
unchanged; ordinary cancellation has no privacy overlay. Full Sales suite318
passed; test browser verified the filter and details/order/product screens.
See the verification ledger and `docs/ux/sales-customer-privacy-notice.md`.
This display/filter fix does not enable cancellation, apply migration002,
delete data or alter legacy prices/procedures/department permissions.

## S2-020 — Some native workflow labels fall back to the neutral chip

**Status: open; outside the cancelled-status correction, 2026-11-02.**
`SalesOrderStatusMap` contains shortened labels such as `Patvirtintas`,
`Filialui` and `Atiduotas`, while the native status reference also returns
`Patvirtintas gamybai`, `Išsiųstas į filialą` and `Išduotas klientui`.
Those full labels retain their displayed text but fall back to the neutral
Entered semantic code instead of the corresponding workflow chip family.
The S2-019 fix maps cancellation explicitly; it does not silently broaden all
other legacy status mappings. Follow-up should verify the actual reference
labels and add the matching existing semantic families without changing
stored StateID or introducing new statuses.
