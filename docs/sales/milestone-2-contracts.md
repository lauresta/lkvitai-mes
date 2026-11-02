# M2 gate 0 — verified SQL and recovered Access contracts

Discovery baseline: 2026-10-31, originally metadata/reference SELECTs only.
Core write prerequisites were subsequently implemented and exercised on three
new Testavimas orders, with explicit receipt DDL and remote deployment. Current
execution status superseding the historical baseline statements below is in
[milestone-2-verification.md](milestone-2-verification.md) and
[milestone-2-persistence-implementation.md](milestone-2-persistence-implementation.md).
Financial/confirmation containment and Access runtime acceptance remain open.

Supporting contracts:
[authentication](milestone-2-auth-contract.md),
[commands/concurrency](milestone-2-command-protocol.md),
[recovered VBA](milestone-2-vba-evidence.md),
[verification](milestone-2-verification.md).

The owner has now confirmed the Desktop ADP is the current client source.
Its SHA-256 is `84185a33c8390abf327cac0bf469b0240dc135d650acdc5d8d4c60fef8cabaf5`;
149 modules recovered, seven differ from the historical Dropbox copy. Current
evidence superseding older assumptions:
[products/pricing/stock](milestone-2-current-product-rules.md),
[edit/state/eligibility](milestone-2-current-edit-state-rules.md),
[form bindings/defaults](milestone-2-current-form-contracts.md).

## Target and scope proven by current SQL

Test API's configured host is `192.168.0.3`; owner's supplied endpoint is
`10.11.12.103`. Both returned server `TERMINAL`, catalog `LKVITAIDBV1SQL`,
SQL Server version `12.0.6024.0` (2014) and database GUID
`31FC1889-3D3E-4422-A4E3-362899649FF5`. They identify the same database in this
inspection. The owner-confirmed current Desktop ADP PropData contains that
host and catalog too. Current source is identified; no Access runtime session
has been executed by this agent.

Production database's test branch is `Zinynas_filialai.FilialoID=7`, name
`TESTAVIMAS`. The existing `weblb_Orders_Paged` explicitly excludes branch 7;
using its result as proof of a test order's existence would therefore fail.

| Series | Series ID | Branch | Default place ID | Default customer ID | Default manager ID | Consultant |
| --- | --- | --- | --- | --- | --- | --- |
| RY-TEST | 2136330188 | 7 | 2071714218 | 2089646683 | 2138337273 | NULL |
| RY01 | 2136330191 | 7 | 2071714218 | 2089646683 | 2138337273 | NULL |
| XXX1 | 551738835 | 7 | -637977188 | 227095126 | NULL | NULL |
| XXXX | 768439496 | 7 | -637977188 | 227095126 | 77249366 | NULL |

These four series were not hidden at inspection. There are 21 existing orders
under them; none has been modified. No `Zinynas_vieta` row has `Filialas=7`:
test-series default places belong elsewhere. Scope uses **series.FilialoID**,
not place/employee/default customer, display name or a submitted branch field.
Keep the exact IDs in explicit initial write configuration and verify the
relationships again at command time. Existing test records are not disposable.

`RY-TEST` contains a hyphen: the M1 regex cannot parse this real test series.
Server normalization must first match a complete known series prefix (longest
match), then parse sequence/group. Test `RY-TEST-123` → `RY-TEST-123-26`,
canonical round trip and collisions with shorter similar codes. Do not invent
an alternative series to accommodate the demo parser.

## Storage facts affecting commands

- Header ID is bigint identity with unique `UzsakymasID`; the primary composite
  key is `(UzsakymoSerija, UzsakymoNumeris, UzsakymoGrupe)`. Sequence is int;
  group is nvarchar(4), so year plus at most two suffix characters.
- Product ID is bigint identity. Quantity decimal(18,3), dimensions int mm,
  `Valdymas` nvarchar(10), colour component ID int. Prices/costs are money
  (four decimal places); do not round storage blindly to two decimals.
- PPP01–PPP11 are nvarchar(15), PPP12 is decimal(18,3); PPP12NEW also exists.
  An array of twelve identical string fields is not the storage contract.
- Attached accessory key is `(PrduotoPriedoID int, ParduotoGaminioID bigint)`;
  there is no accessory-row identity. Quantity decimal(18,3), price money.
- Address primary key is `iOrderID`; its street ID is
  required, building/room nvarchar(12), phone nvarchar(64), notes nvarchar(512).
  Use DIC_Addresses hierarchy; do not create arbitrary reference addresses on save.
- Duty primary key is `(iOrderID,iDutyID,iEmployeeID)` with required smalldatetime date;
  acquisition/payroll columns are server results, not operator inputs.
- None of these collected core tables has rowversion. Use the explicit
  transactional content-revision contract, not dtDateUpdate as a complete version.
- Header currency defaults to ID 2/EUR; all21 Testavimas headers use2. Current
  payment VBA explicitly inserts1/LTL; reference currency defaultflag is also1,
  with both rates1. This is verified inconsistent historical metadata, not
  permission to silently change payment currency during porting.
- Current active type count is **291**, compared with M1 snapshot's 290.

## Action map and enabled boundary

All mutations use trusted Portal actor plus command ID in the proposed receipt,
short transaction and revision check. Existing SYSTEM_USER audit stays intact.
The following is an implementation contract, **not** a record of passed writes.

| UI operation | Verified entry point / storage | Rules and effects | Current boundary |
| --- | --- | --- | --- |
| Resolve series/defaults | SELECT Zinynas_UzsakymoSerijos by stable ID/code | Customer/manager/place/consultant defaults; nullable manager/consultant exist; known series may contain hyphens | Safe reference read; revalidate hidden status/branch at create |
| Create header | Bound Access insert into Uzsakymai; trg_NewOrder | Identity, SYSTEM_USER/date, default duty3 inserted from TBD_DefaultOrderPersons; current header defaults need form verification | No writes until trusted auth, receipt DDL, actual defaults and effects verified |
| Edit header | Targeted UPDATE Uzsakymai | Date change checks work period; sNotes inserts note history; Nuolaida invokes email check; stored final/Paid/State invoke salary/finish logic | Allow only independently verified fields; no blanket SET |
| Open/reopen | Exact UzsakymasID reads; sp_frmOrder definition | Joins manager/customer/place/state; missing nullable manager can hide record in inner joins; current sp_frmOrder credit display is hardcoded150000 | Editor reads must not infer nonexistence or real available credit from this projection |
| Add product | sp_CreateNewItem: INSERT(order,type,Spalva=0), then unsafe @@IDENTITY | trg_NewBlind sets barcode/actor/time/order state and inserts AuditUserActions | Choose Sales exact equivalent INSERT with OUTPUT INTO at insert scope plus post-trigger SELECT; preserve shared Access procedure, verify parity on isolated fixture before activation |
| Edit dimensions/quantity/PPP | UPDATE ParduotiGaminiai; recovered Form_frmNewBlind events | Width/height/quantity recalculate all nonnull NPP formulas in order, save, then stock check. Colour checks stock only. Formula-backed manual PPP overwritten on next calculation | No M1 sticky-manual semantics; use server allowlist/current DIC_Constants |
| Calculate price/total | VBA FindPrice + trg_ChangeBlind + discount functions | SQL minimum pricing dimensions/area, money precision and type/category customer discounts; trigger quantity/price/accessory-final changes recompute amounts | Read-only quote and commit revalidation; TOP1 price query tie semantics unresolved |
| Attached priedai | ParduotiPriedai composite key; sp_ListAccessoriesByType SELECT | Parent saved first, dimensions passed in metres; four source unit rules verified | Other units/defaults require form evidence; no invented accessory identity |
| Standalone priedai/service | Actual Zinynas_tipai rows persisted through ParduotiGaminiai | Name/category does not establish storage semantics; Be gaminių currently Gamininamas=true | Use type metadata rather than translating labels into business kind |
| Type change | Bound-form behavior plus current type definitions | PPP/control/colour/accessory reset behavior requires active form metadata/event verification | Disabled until complete target-type configuration contract |
| Duplicate | Recovered cmd_CopyRecord_Click is a message only | No executable legacy duplicate workflow | Sales composition must be explicitly specified/tested; not claimed as an existing proc |
| Check parameters | Recovered button is success MsgBox only | No actual validation routine in this handler | Real server validation can report its checks, not claim legacy button parity |
| Delete product | sp_DeleteBlind (state4/7) | **Globally disables a production-output trigger**, deletes output details/product, re-enables trigger inside nested transaction; no TRY/CATCH restoration | Do not execute this procedure for testing or expose it blindly; design a contained compatible deletion path first |
| Stock shortage | Recovered CheckComponents | available-required>0, equality counts shortage; action3 deletes product, optional NOTIFY_UserComponents write | Preview is read-only; destructive shortage branch needs explicit safe transaction/UX contract |
| Address | TCR_OrderAddresses | Structured required street ID and hierarchy; legacy unload may prompt for missing address | Validated targeted operation, pending bound defaults/locks |
| Employees | TCR_OrdersDutiesEmployees | Default manager duty insert already in trigger; salary updates acquisition columns | Do not duplicate default row or accept calculated acquisition fields |
| Discount/fixed final | Targeted Uzsakymai updates | Nuolaida>=20 can send mail; final-price save recalculates salary, cross-database reads include MTTools | No Testavimas exemption observed; contain effects before test activation |
| Payments PPK/transfer/cash/EKA | Recovered INSERT Mokejimai; trg_NewPayments | Nonzero signed amount; required method documents; transfer assigns actual BankTransfersTable record; employee cash/Paid updated, period checks, refunds/backdating may send mail | Each method independently blocked until safe employee/register/transfer records and effects established; no fabricated bank document |
| Payment update/delete | trg_ProhibitUpdatesOfPayments / trg_ProhibitDeletionOfPayments | Deletion rolls back; updates restricted by trigger | No generic CRUD or cleanup delete |
| Confirm/transition | Current Form_frmOrdersConfirmation creates production-output document/details per barcode | AssignBlindState trigger changes product state; customer action1 and narrow stock-view guard; historical loop may partially confirm | Path traced; remains disabled pending safe atomic command/numbering/effects tests; no direct StateID/Patvirtintas patch |
| Cancel order | sp_CancellOrder, state4/7 rules plus SQL-login exceptions | Updates Sugadintas, triggers cancellation audit; doesn't delete header | Separate verified transition; shared sa is not a Portal identity mapping |

Triggers use scalar inserted/deleted assumptions. Initial commands target one
header/product at a time; do not introduce multi-row batch DML without proving
trigger compatibility. RAISERROR is not proof of rollback: e.g. product note
validation returns without explicit rollback, and priority logic can update
back to normal after raising. The API transaction must roll back a failed
operation; never report error while accidentally committing changed rows.

## First slice decision and remaining gates

Selected first **candidate**: `R25 LP`, type ID `-1025240863`, category
`Roletai Textile paprasti` ID `1172751775`. It is active/manufactured, already
used by two Testavimas lines. Formula evidence includes Width-45, Width-40,
Width-60 and PPP12 `((Width-45)/1000)*Quantity`; price unit is width metre.
This is representative dimension/colour/material entry with simple PPP,
not a proof its price/stock contract is already safe. Candidate test series
`RY-TEST`; verify nullable consultant, actual manager/default currency and a
valid component/price-period/stock selection before any create.

Unresolved conditions for first write: remaining opaque bound-form scalar
defaults/locking; runtime comparison path; exact first-type price and
component selection; permitted incomplete/default states; additive receipt
schema deployment; coherent-lock plans/snapshot inventory; trigger side-effect
containment. The SQL Agent job search by catalog/order keywords found one
customer-information refresh job, **not** proof all external schedulers or jobs
are contained. No write or Access parity gate has passed.

Independent next work is trusted Sales authentication and live reference reads.
See the verification matrix for exact completed discovery versus unrun cases.
