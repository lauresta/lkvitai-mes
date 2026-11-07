# Sales milestone 2 — current issues

Updated 2026-11-07. Only unresolved, deferred or not yet verified items are listed here. Resolved history remains in Git before this revision. Execution evidence: [verification ledger](milestone-2-verification.md).

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

**Status: enabled on test in `m2-686afeb` for owner manual verification (2026-11-02); successful production confirmation is not yet runtime verified.** The optional server command follows
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

## S2-006 — Final-price recovery warning text

**Status: minor known limitation.** Direct final-price response and same-key
replay report any legacy rounding difference. The extra warning is derived
after the receipt transaction, so outcome-only recovery after a reload does
not reconstruct that specific warning text. The committed actual total is
always read and displayed; eligibility/material warnings are durable.

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

## S2-023 — Native browser session and fresh SSO session differ on current main

**Status: observation requiring separate investigation, 2026-11-02.**
During translation acceptance on a12aa41, the existing native IAB `bykovas`
cookie session initially displayed API-unavailable and, after hydration, an
empty list. The fresh Safari SSO session for Denisas Bykovas loaded the real
order list successfully and remained authenticated after reload. This does
not establish whether the difference is expired credentials, roles or intended
SQL/data scoping. Recheck fresh native authentication and effective identity
before changing behavior. No authentication, mapping or data permission changes
were made as part of localization. The M2 identity/recovery bridge is still in
unmerged PR #224; its integration must retain the new SSO behavior.

## S2-024 — Missing-DB-container fallback omits the existing persistent bind

**Status: existing infrastructure edge; deferred outside Sales integration, 2026-11-02.**
The running test PostgreSQL uses `/opt/postgres-test/pg/data` and retains the
Cikada user mappings across normal app deployments. The deploy workflow's
fallback for a missing database container does not mount that existing bind.
Recreating a deleted DB container through that fallback would not reuse the
existing data directory. The current healthy DB container is retained by this
release; no database container, volume or data directory was recreated. Repair
the missing-container provisioning path separately, preserving the existing
cluster and credentials rather than silently initializing another database.

## S2-025 — Decolux material formula uses millimetres as metres

**Status: root cause confirmed; shared legacy data correction deferred by owner, 2026-11-03.**
The screenshot's Decolux product is 1200 mm wide and quantity 1. Active legacy
type 1170058243 (`Decolux`) and -593367416 (`Decolux laminuotas`) both store
`NPP12=Width*Quantity`. The fabric component R20 is measured in metres and has
59.086 m free, so the legacy formula incorrectly asks for 1200 m. Comparable
active `Roletai`/`Audinys` types use `(Width/1000)*Quantity`, which asks for
1.2 m. Sales evaluates the stored formula directly, as Access does; its stock
comparison is acting on the incorrect requirement rather than missing stock.

Read-only inspection found no stored nonnull PPP12 consumption for either
Decolux type and no enabled trigger on `Zinynas_tipai`. Guarded correction
[005-decolux-material-metres.sql](../../scripts/sales/005-decolux-material-metres.sql)
is prepared but **not applied**. The owner explicitly chose not to change the
shared database for now. The script changes type master data for all branches
and both clients; it must not run without new approval. If later approved,
recheck current rows before execution, then verify 1.2 m preview, save on the
owned Testavimas order and Access calculation.
