# M2 verification ledger

Updated 2026-11-01. The current execution record below supersedes earlier
discovery-only status statements and deployment gates. A SQL/browser pass is
not an Access runtime pass. Only the explicitly listed owned Testavimas fixtures
448/449/450/453/454/455 were mutated; the original21 Testavimas orders and the
owner-created order2148197452 were not edited during these checks.

## Current release — m2-2402c36 (2026-11-01)

Manual working-tree API/WebUI release for `https://cikada-test.lauresta.com/sales/`.
Source archive SHA256:
`2402c362558368122c086c799e6779e38567caf6bffa17972e3905e8299f36b4`.
No commit/push was issued. A later ordinary CI deployment can replace these
manual images; private deployment snapshots retain the rollback configuration.

All four native payment methods are enabled for the configured Testavimas
orders: cash81423987, bank2, PPK1 and EKA834019474. Native lookup/receiver scope
and exact transaction revalidation replace manual bank/register allowlists.
Cancellation and production confirmation remain independently disabled. New
financial postings remain positive/current-date/EUR2; no refund/backdate or
production/cancellation migration was introduced by this follow-up.

| Check | Observed result |
| --- | --- |
| Final compile/unit/HTTP suite | 220 passed, zero skipped/failed. Includes actor isolation, immutable replay bodies, reload after missing receipt, cross-order/account recovery, storage failure before dispatch, wrong-order receipt/result preservation, cash/PPK/bank/EKA cascades and amount precision. |
| Native financial integration | 17 actual HTTP/SQL rejections before DML, with unchanged orders/products/PPP/payments/cash/bank assignments/notes/audits/receipts. Bank page25 of286, exact0.0007 unavailable balance, bank default receiver, all12 EKA registers and hidden-default switching verified. |
| Final full remote browser (v14) | All10 stages passed on actual Testavimas order2148197455 / XXXX-8312888-26: initial New order; keyboard/mouse autocomplete; R25 LP dimensions/control/material/list price/PPP; attached priedai; standalone priedai; service; reload; all four payment cascades; legacy-result isolation; scoped pending isolation; final navigation. Zero observed page/console errors or HTTP failures. |
| Fast navigation | Initial list button waits for a live circuit. Successful header creation no longer renders a transient editor in the old dialog. Route order IDs key the workspace. The fresh-header browser test used normal fast clicks, without a session-proof wait workaround. |
| Recovery in browser | Existing cash receipt ce4ded72… for owned450 could be checked from455 without changing455; an explicit link opened the actual saved order. A synthetic local pending entry for450 did not block455 and did protect450. That synthetic cancellation record was never sent to the server and was removed only by the test harness. |
| Live SQL readback | Owned454 and455 each contain three real lines: R25 LP qty1, Smulkios detalės qty.5, Žaliuzių matavimas qty.5, plus attached priedai qty1.25. StoredTotal29.72, PaidNULL(0), zero payments. Full dimension/material/PPP/amount values were retained as private evidence. |
| Existing records unchanged | Owner452 still has zero products/payments. Owned450 still has its original three products, total28.43, Paid.02 and exactly two retained earlier cash payments. |
| Preview total correction (v15) | Live entry labels the amount Saved product total and shows a dash before a native amount exists, instead of demo Quantity×UnitPrice. A real read-only browser check on453 entered800×1500/qty1/R173 and listquote20.8526 without saving; total stayed—. Saved455 showed23.92 (base16.6821 + attached7.2405). Session proof200, zero browser/HTTP errors, and full SQL snapshots unchanged. |

The v2 recovery journal contains frozen business input in per-tab sessionStorage
bound to the authenticated actor and order; no API credentials/session proofs.
Initial storage read/write failure stops submission. Unknown attempts retain
the original key/body, including after401/403. Successful recovery clears only
the corresponding entry. Unresolved records are not discarded by age. Legacy
bare GUIDs are kept separately as check-only notices, never automatically replayed.

Payment positive verification remains the two earlier real cash postings/replays.
Bank/PPK/EKA lookup, cascade and rejection verification does not claim positive
booking against disposable financial documents: no suitable test bank fixture
was identified and no arbitrary funds or made-up receipts were booked.

The old30-second SQL failure remains an explicit underlying legacy issue
[S2-012](milestone-2-issues.md#s2-012--intermittent-legacy-product-create-delay).
Read-only trigger/plan evidence found a wide barcode-cascade scan and a29.395s
maximum wait-sensitive trigger execution; the historical blocker was not captured.
The new successful browser creates do not prove that intermittent wait is fixed.
Failed-statement fingerprints/duration/provider diagnostics are now logged
without SQL text, input values or credentials. No global index/trigger/isolation
change or blocker termination was performed.

Final v15 API/WebUI containers are healthy. Public `/sales/health` returned200
in0.276s. The final read-only SQL snapshot comparison SHA256 is
`c6b2bbc793679f13e74b8f01f4df82e8cc2f55ad049ee2a5d3cfed738b44694d`.
Temporary fixture issuers/cookies were removed after both final browser checks;
the production/shared auth keyring and application configuration were retained.

Private follow-up evidence: `/tmp/sales-m2-browser-ui/v14-result.json`, product/
order/payment screenshots, `/tmp/sales-m2-evidence/final-ui-sql-readback.json`,
`payment-cascade-verification.json`, `v15-readonly-comparison.json`,
`/tmp/sales-m2-browser-ui/v15-total-result.json`, `v15-fixture-cleanup.json`,
and the v13/v14/v15 deployment snapshots.

## Prior deployed baseline — remote build m2-f9367ea

Core entry is deployed at `https://cikada-test.lauresta.com/sales/`.
API/WebUI containers are healthy; public `/sales/health` returned200 in0.34s. This is a
manual working-tree deployment without a commit. Source archive SHA-256:
`f9367eac17ca78c725d44fc38d712762be125e26dd261422b742ebd4755378a2`.
A later ordinary CI deployment may replace these uncommitted images; retain the
reviewed source/configuration before promoting a release. Private rollback
snapshots retain the original containers and the successive Sales deployments.

Writes require the inspected database GUID and series allowlist, branch7,
trusted employee actor and a current order revision. Reads span all branches.
Cash payments are enabled only for approved receiver77249366/method81423987/EUR2.
Cancellation and production confirmation remain independently disabled. The
additive technical `mes_sales.CommandReceipt` table, including nullable
RelatedEntityId and WarningsJson, was explicitly applied. Legacy procedures/triggers were not
altered, and `sp_DeleteBlind` was never invoked.

| Scenario | Actual result and evidence boundary |
| --- | --- |
| Header creation/reopen/idempotency | Actual SQL adapter created2148197448; new service instance same-key replay returned the same ID; changed payload under the same key rejected. New header/default-duty identities read back after triggers. |
| First product/price/PPP | Product2152012295, R25 LP/type−1025240863, material6026/R173, controlK.800×1500,qty1 produced PPP12=.755, unit20.8526, amount16.6821, final16.68. |
| Material rejection | Component2328 free0/action3 rejected configuration. Stored product/revision unchanged; no successful receipt. No global component policy was changed. |
| Customer eligibility | Initial RY-TEST default customer policy returned unsupported−1 and correctly blocked product creation. Only the new test header was changed through the supported workflow to a customer whose verified policy returned2. No customer policy was modified. |
| Dimension/manual PPP | Width820, manual visible PPP01=700 and recomputed PPP12=.775 saved, amount17.0991. Existing unit price dispatched the SQL total trigger without automatic price lookup. |
| Attached priedai | Definition−1804768558 qty1.25 persisted/reopened. Delete and re-add normalized totals despite the two legacy DELETE triggers; final test total24.33. |
| Trigger failure atomicity | Uppercase-note validation raised SQL error; stored configuration and revision unchanged, no committed receipt. |
| Concurrent Sales writers | Same original revision: one success and one STALE_ORDER; both did not silently overwrite. |
| Simulated legacy bound update | Parameterized direct SQL note update guarded to448/series2136330188/branch7 changed the Sales revision. Stale Sales save rejected with no receipt; fresh Sales save restored the original note, and reviewed `sp_frmOrder` read it back. This simulates a bound SQL write, not a running Access client. |
| Duplicate manual number | Actual SQL attempt using the existing448 number returned409 with no new header or committed receipt. |
| Preview side effects/ownership | Four previews of448/product2152012295 returned required.775, free514.945, own reservation.775, available515.720. Product/revision/PPP, its barcode audit, order-note and receipt counts unchanged. Wrong or missing order/product ownership rejected. The available value is point-in-time. |
| Ordinary branch isolation | Actual create attempt for an ordinary-branch series denied before business mutation. |
| Legacy readback | Reviewed SELECT-only `sp_frmOrder` returned all three new orders and matching stored/current totals:448=24.33,449=24.33,450=23.92 at readback. This proves shared SQL visibility, not Access UI execution. |
| Local live browser | Trusted Portal fixture session → real API → header/product/list-price/priedai → reload succeeded. Order2148197449 created and reopened; entered controlD and fractional attached quantity1.25 preserved. Guest review remained local-only. |
| Remote live browser | `cikada-test.lauresta.com`, authenticated fixture session, actual SQL order2148197450: create, R25 LP, R173, list price20.8526, save, attached priedai1.25, close/reload passed with no observed page/console errors. Later v6 actions are recorded below. |
| Remote v4 extra entry actions | Existing own450: standalone Smulkios detalės qty.5 @1.50 and Žaliuzių matavimas qty.5 @10 saved/reloaded; fixed total29.67→28.19 read back exactly; measurer duty add/delete passed; preview showed own-line material availability515.7 and need.755. The subsequent v6 entry checks below supersede the pending navigation status. |
| Remote v6 operator flow | Real Check/Price/Refresh, width810 save through calculation fingerprint, visible manual PPP01=700/reopen, Duplicate including child accessory, safe Delete copy, Done→Back all passed. Actual save cycle585ms; full editor open4.72s. Standalone Smulkios price1.60 with NULLPPP12/material0 saved/reopened. Approved full editor fits the measured1600×1100 screenshot; no page/console errors observed. |
| Remote session longevity | Same browser circuit held139s; real HTTPS proof renewal returned200 at3/63/123s and a later real preview still succeeded. Versioned module URL contains24da0ac-working. Fresh guest context remained sample-only with catalog401 and zero live entry requests. |
| Calculation drift | Actual v5 SQL missing/wrong reference token rejected409 before PPP/data/receipt mutation. Current references produced stable64hex fingerprint, including SQL2014 formula/constants/type/minimums/discount/matrix/period inputs. Stock remains a fresh commit-time check, not part of an availability promise. |
| Duplicate identity/replay | Actual448 duplicate2152012300 got a fresh ID/barcode/state4, retained manual visible PPP/accessories/current price, and replay via restarted service made one copy. Guarded delete restored original total24.33 and product count; original2152012295 unchanged. |
| Durable warnings | Technical-only callback/receipt integration: restart/replay/outcome retained a server warning, other actor could not read it, business revision unchanged. Exact owned synthetic verification receipt removed. |
| Cancellation — FAILED/blocked | Remote450 cancel reached legacy trigger and rolled back: `trg_ChangeOrder` writes BIGINT header ID into `AUDIT_Anuliavimai.OrderID INT`, causing overflow. Fresh read showed State4, Cancelled=false and zero successful cancel receipts. No fresh-key retry, trigger bypass or shared schema change was attempted. Guarded script002 is prepared; the owner declined its application and requested cancellation remain disabled. Current code adds a definitive422 preflight and snapshot reason instead of trying this known overflow. |
| Cancellation preflight | Actual private HTTP harness against active450 returned422 LEGACY_AUDIT_ID_RANGE with the supplied source limitation. Revision, order notes, scoped audit and receipt counts unchanged. This verifies safe denial, not successful cancellation or cancelled-state enforcement. |
| Final v9 read-only browser | Build m2-01b2815 verified truthful shared-production banner;450 active with Cancel disabled and explicit bigint-audit reason; stored R25 type−1025240863,width810,manualPPP01=700/controlD stable; stock refresh did not dirty input; current versioned module, HTTPS proof200, zero browser errors. Historical inactive-type snapshot ID/name fallback has a real HtmlRenderer regression and remains excluded from new choices. |
| Remote auth boundary | Anonymous and forged raw-bearer write requests returned401. Fixture cookie was minted with existing server keyring read-only, bounded expiry, no added public login endpoint; temporary fixture credentials are not release artifacts. |
| Unit/HTTP checks |144 tests passed on the final v12 source; API/WebUI compiled. Includes actor isolation, expired/tampered/wrong-purpose credentials, mixed review sessions, scope/financial gates, formula/function boundaries, material policies, rounding, pending command protocol and cancelled action mapping. These tests alone do not establish SQL/Access parity. |

Controlled records retained for review:

| Order ID | Canonical number | Origin |
| --- | --- | --- |
|2148197448 | RY-TEST-913322-26 | SQL adapter integration; product2152012295 |
|2148197449 | XXXX-926404-26 | Published local browser against actual SQL |
|2148197450 | XXXX-943185-26 | Published remote test browser |

These are operational test records, not fixtures for unrestricted cleanup.
Only verified narrow deletion/cancellation commands may change them.

Remaining M2 acceptance gaps: actual current Access client execution on Windows
(including later unconditional Access overwrites), six ambiguous `Nera` formula
types, contained financial/confirmation fixtures, and full commercial-quote acceptance. Reference fingerprint drift protection
is now implemented/tested; stock and legacy-wide credit atomicity remain
bounded by the shared system. Cancellation additionally requires the identified
shared audit bigint compatibility repair. The legacy VBA uses32-bit `Long` in active refresh/price paths
although current generated IDs exceed2^31. See the narrow proposed compatibility
patch; the actual ADP file has not been modified. Pending features are not passed
by the browser/SQL checks above. Ordinary-branch rollout is not authorized.

## Historical discovery and execution baseline

Earlier entries below describe their original stage. Their old default-off,
no-deployment and no-write statements do not describe the current release.

## Executed discovery

| ID | Check | Evidence / result |
| --- | --- | --- |
| D01 | Test API SQL configuration and network reachability | Read through authorized SSH; configured host192.168.0.3, catalogLKVITAIDBV1SQL |
| D02 | Owner host versus API target | Both endpoints returned TERMINAL, version12.0.6024.0, same database GUID31FC1889-3D3E-4422-A4E3-362899649FF5 |
| D03 | Core tables, indexes, constraints, triggers, procedures | Private read-only metadata export:625 module definitions; allowlisted hashes committed in milestone-2-schema-baseline.json; no procedures executed |
| D04 | Test branch/series | Branch7 TESTAVIMAS, four verified series in contracts;21 existing orders counted, none edited |
| D05 | Read visibility and scope | weblb_Orders_Paged excludes branch7; test-series places are not branch7; branch policy must follow series |
| D06 | Active product references |291 active types versus290 snapshot; first candidate R25 LP -1025240863 exists, active and used in test branch |
| D07 | Recover current VBA | Owner confirmed Desktop ADP;149 current modules, seven differ from historical main source; current form/product/edit-state evidence documents supersede the historical recovery. Runtime acceptance not executed |
| D08 | Safe ID/concurrency baseline | No rowversion in core tables; nested audit identity exists; attached accessory composite key confirmed |
| D09 | Side effects | sp_DeleteBlind globally disables a trigger; payment/discount/financial changes have effects outside isolated test order; activation blocked pending containment |
| D10 | SQL Agent scan | One enabled job step matched catalog/order keyword scan; external schedulers and indirect jobs remain unverified |

## Required execution matrix

All scenarios below are **NOT RUN** unless a later dated/build-specific evidence
row explicitly replaces that status. Reference implementation and local auth
tests will be recorded separately; they do not pass Access/SQL write cases.

| ID | Scenario | Expected evidence / acceptance |
| --- | --- | --- |
| A01 | Valid Portal user, SSR then interactive circuit | Same trusted GUID actor, no raw bearer forwarding, live read success |
| A02 | Tampered/expired/wrong-purpose/no-expiry credential |401, no real data/write |
| A03 | Review+Portal cookies together, DevAuth, raw structured bearer | Review remains sample-only; no credential elevation |
| A04 | Two simultaneous user circuits and factory HttpClient reuse | No actor/token leak, request-scoped header only |
| A05 | Logout/account switch/renewal/reconnect | Bounded expiry, no account rebinding; buffer retained on auth failure |
| R01 | Live series/categories/types/detail, all branches incl7 | Exact IDs/defaults/formulas; no static fallback; existing inactive reference visible |
| R02 | Editor exact ID with number colliding in text search | Correct record independent of first five/page results |
| W01 | RY-TEST header create + reopen in Access | Approved defaults/currency, one header/default duty, canonical number + real identity |
| W02 | Concurrent creators same number | One unique header; clear duplicate conflict |
| W03 | Header response lost then same-key retry/restart | One mutation/receipt, same committed ID |
| W04 | R25 LP create/configure/save | Exact dimensions/control/colour/PPP, price and trigger readback agree with active Access |
| W05 | Unknown outcome after product commit | Same command recovery, no duplicate barcode/product |
| W06 | Access changes while Sales editor dirty | Sales409 with current values and retained buffer |
| W07 | Sales changes while Access editor dirty | Measure actual Access protection; document unsupported reverse guarantee |
| W08 | Child insert/delete race, empty child collection | Revision/SQL-range protection, no lost child or torn snapshot |
| W09 | Parent/child ID spoof, ordinary branch, gates off |403/appropriate denial, zero mutation/receipt disclosure |
| W10 | Trigger raises/returns/rolls back, receipt failure | Business write and receipt atomic; no false success |
| P01 | Every custom formula family and boundary | Active Access values at stored precision, live DIC_Constants |
| P02 | Manual PPP then dimensions/colour/reopen | Event-specific reset/preserve semantics match active Access |
| P03 | Attached and standalone priedai incl fractional units | Correct composite IDs, units, prices and totals |
| P04 | Type change/duplicate/delete | Defined resets/copies; new identities; no global trigger disable |
| C01 | Quote versus commit reference/price/stock change | Operator-review conflict or verified warning/block, no invented reservation |
| C02 | Rounding/min-area/discount/fixed price/VAT | Exact stored amounts and Access comparison, no uncontained mail |
| C03 | Credit/late payment warning/block/confirm restriction | Actual decision-point behavior; no hardcoded displayed credit treated as real |
| O01 | Address/duties/priority/dates/notes | Exact IDs, history/audit/default behavior, no duplicate salary fields |
| F01 | PPK/transfer/cash/EKA individually | Real verified references, contained employee/bank effects, exact paid/debt |
| F02 | Refund/backdating/zero/period errors | Explicit scope, verified trigger behavior, no uncontained mail or cleanup delete |
| S01 | Confirm/cancel/locked state | Verified action path/state map; ordinary edits cannot assign state |
| U01 | Remote authenticated full create-close-reopen-edit | Correct deployed build and Access two-way visibility; keyboard/live-preview intact |
| U02 | Disable writes/app rollback | Access/read/saved data usable, no shared schema rollback/deletion |

Each actual execution adds date/build, tester/client version, Testavimas IDs and
canonical number, inputs, Access result, Sales result, relevant stored values,
side effects and pass/fail/gap. Do not record customer PII in public fixtures.
No routine CI uses the production connection. Financial/unsafe experiments use
an isolated copy unless an actually contained production-test path is proven.

## Current external dependencies

The owner confirmed `/Users/bykovas/Desktop/Uzsakymai.adp` is current. Its149
modules and relevant design streams were recovered and compared; current
source identity is resolved. Numeric design flags and actual Access runtime
comparison remain unverified. Independent auth/read work proceeds;
write gates remain off. Payment/confirmation/delete activation also requires
contained effects and a safe deletion path. Required additive receipt DDL is a
proposal only and has not been deployed.

## Local implementation checks — 2026-10-31 working tree

- Sales Release auth/reference endpoint test suite: **12 passed,0 failed**.
  Includes cryptographic bounded credentials, purpose/key/expiry rejection,
  cookie→proof→API HTTP flow, raw bearer/Portal-cookie-only denial, mixed review
  session denial, cross-site proof denial, parallel per-circuit API clients,
  protected reference routes, negative legacy IDs, query errors and read-only
  method boundary. These are local TestServer/unit tests, not remote UAT.
- Actual `SqlSalesOrderEntryReferences` exercised against the inspected database
  using a private .NET SELECT-only runner: RY-TEST exact ID/scope; out-of-range
  empty page retained total1; literal-percent search returned0;24 active
  categories;277 selectable types with active parent categories; R25 LP all12
  PPP definitions including expected decimal PPP12 formula; unknown type=null.
  The291 global active count includes types under inactive categories.
- No reference cache yet: each adapter call reads fresh SQL; API responses use
  no-store. Explicit Stub hosts do not register live reference endpoints.
- New live-reference routes exist under `/api/sales/order-entry/references/`:
  `series`, `categories`, `types`, `types/{typeId}`. They share trusted Sales
  authentication. The operator's M1 workspace still uses preview references;
  live WebUI binding and editable persisted-order reads remain unfinished.
- No write endpoint, command-receipt DDL, production mutation or remote
  deployment has been introduced by these checks.

Published local browser check also passed: protected Portal fixture cookie →
SSR/circuit → real local API transport/search; proof refresh200; logout
refresh401 displays renewal notice with local draft retained; same-account
login restores refresh200 and retains draft. Guest review search produced zero
actual API calls and denied proof403/API401; raw bearer401. Local isolated
API used Stub, no production database. Temporary browser fixtures/keys/servers
were cleaned up. Remote authenticated UAT remains NOT RUN.

## Current Desktop source audit and lookup checks

Owner confirmed current Desktop ADP. Recovered149 modules, seven changed from
old main ADP. Current evidence maps all inspected entry actions across product,
stock, pricing, edit capabilities, customer eligibility, confirmation,
priedai, payment, duties and address. See milestone-2-current-audit.md for the
source/SQL map and the explicit remaining runtime boundaries.

Working-tree Sales suite after price/reference enrichment: **30 passed,0
failed**. New cases cover price precision20.8526, ambiguity/null/missing/ref
outcomes, exact fixed-price exceptions, NULL→any price mode, metadata versus
line permission, protected GET binding/no-store and rejection of mutation
methods. No runtime Access/SQL-write scenario is marked passed by these tests.

Actual SqlClient SELECT-only lookup checks against the inspected production
catalog passed: R25 LP/component2328,800×1500,quantity1,order date2026-10-31
returned Found20.8526/match1; unknown type returned InvalidReference; unmatched
matrix returned Missing. Exact-type pricing metadata returned fixed mode,
width-metre unit-1789163110, minimum width0.50m,height0.00m,area0.00m² and no
manual-price support for that type. No record was changed.

Live SQL route `/api/sales/order-entry/unit-price` is a read-only unit-list-price
lookup with typed outcomes. It does not calculate a complete commercial quote,
reserve stock or approve a save. Operator UI binding and persisted editor/write
flow remain unfinished; no remote deployment occurred in this audit.


## Shared-audit compatibility: owner deferred

`002-cancellation-audit-bigint.sql` proposes exactly one shared legacy column
widening, `AUDIT_Anuliavimai.OrderID int NOT NULL` → `bigint NOT NULL`.
This is different from the already applied technical receipt DDL. Owner approval
was requested explicitly because its schema effect spans all branches. The owner
then instructed that cancellation stay disabled and this be recorded in issues.
Do not execute the migration or bypass the trigger.6732 original audit rows
were inspected; there is no OrderID index/FK and the only SQL module reference
in the inspected625 modules is the existing `trg_ChangeOrder` writer. The script
checks target identity, column shape/dependencies, lock timeout, row preservation
and rolls back on failure. External/dynamic readers cannot be proven by this
static audit. No Access audit reference was found in the149 current VBA modules.

The three owned test orders are retained active because cancellation is
explicitly deferred. Their R25 material reservations therefore remain present; do not claim
successful cleanup or cancelled-state runtime tests. No original Testavimas
order or original cancellation-audit row was modified. After a future explicit authorization for both repair and activation, run the
unchanged legacy cancellation path on450, verify denied stale/fresh child writes,
and cancel only the two other exact owned fixtures to release test reservations.


Final housekeeping: private fixture cookies, pending browser metadata and local/
remote issuer artifacts were removed; owned browser contexts and temporary local
API/WebUI processes were closed. The production DataProtection key ring was never
exported or modified. Owned verification projects were archived privately and
removed from the repository workspace. Screenshots/summaries and private SQL
source evidence are retained separately; credentials are not repository files.
Only Sales API/WebUI were recreated during deployment; unrelated modules were
not restarted. No commit or push was performed.


Owner decisions after v9: exact Nera token means literal lowercase text “nera”;
`d.bykovas`/Testavimas may be used for test payments; cancellation repair is
not approved and cancellation must stay disabled. Follow
[milestone-2-issues.md](milestone-2-issues.md) for the current issue/decision index.
An independent default-off CancellationEnabled flag now enforces this decision
for every order ID, even if a DBA later widens the audit column separately.


## Follow-up owner-authorized execution — after v9

The owner explicitly deferred audit002/cancellation; it was not applied.
CancellationEnabled is independently default-off and explicitly false on test,
including for old int-sized IDs. Current issues/owner decisions are centralized
in [milestone-2-issues.md](milestone-2-issues.md).

- Bare Nera text semantics were confirmed by the owner. All six actual types
  were created/configured/read/deleted one at a time on owned450, storing typed
  PPP02='nera' and PPP12=.764/.769 with real material/pricing. ProductIDs
  2152012302..2152012307 were individually removed through the guarded safe path;
  original three product snapshots and total28.43 restored after each.
  Live stock units now join dbo.DIC_Units and returnm for6026/5950/5805.
- The owner approved d.bykovas77249366 as receiver even though its historical
  employee record is hidden/branch1. Only the explicit configured receiver is
  selectable on Testavimas orders; no employee was unhidden or moved. New Sales
  payment currency is explicitly EUR2; old LTL1 metadata is untouched.
- Initial real HTTP preflights proved unallowlisted receiver403 and closed-period
  422 before payments/Paid/cash/audit/receipt/revision changes. UI showed actual
  cash/receiver/EUR references and disabled Post with the work-period reason.
- The owner separately approved sa's period through2030 and its normal legacy
  administrative notification. Explicit guarded003 updated only the end date to
  2030-01-31, preserved all other sa fields and all other users, and the unchanged
  source period function becameY. Native work-period audit was retained. Normal
  Database Mail item1121186 queued; first observed delivery statusretrying.
  Reapplying003 skipped UPDATE and created neither a second audit nor notice.
- API/TestServer plus actual SQL cash0.01 EUR: payment2149652454,
  command6173a906-20e8-4503-81a0-165cca91fc38. Restart/same-key replay and outcome
  returned the same ID with no second financial effect. Legacy sp_frmOrder
  saw the committed Paid0.01.
- Remote v10 browser cash0.01 EUR: payment2149652456,
  commandce4ded72-8077-4ecc-b040-ef496dbeaf90. Paid.01→.02, debt28.42→28.41,
  history1→2; reload stable and no browser errors. Exact captured actor/key/body,
  including the original v10 revision, was replayed through the latest adapter:
  Replayed=true/same payment, no payment/cash/Paid/audit/receipt/revision delta.
- Final actual SQL/legacy readback:450/XXXX-943185-26, state4, active, original
  three products, total28.43, exactly two retained EUR payments, Paid0.02/debt28.41;
  combined approved receiver cash delta+0.02. No refunds or payment deletion.
- Confirmation remains off. Actual consumers include confirmed branch7 products
  without prefix isolation; a namespace does not contain eventual production.
  UI is now wired behind the server capability; stored-value/unit/trusted-price/
  PPP/material guards are tested without running real confirmation. Full128-bit
  Base32 document identity now fits downstreamnvarchar32 without truncation.


## Final deployed follow-up — m2-f9367ea

Both Sales containers are healthy; public health200 in0.34s. Deployed archive
matches the current application source exactly. Cash-only receiver/EUR scope,
Testavimas parent allowlist and explicit cancellation/production OFF were
verified from the running containers. Unrelated modules were not restarted.

Final authenticated browser verified exactly two EUR payment rows, Paid0.02 and
debt28.41 after the actual API/WebUI container recreation. The original captured
v10 payment command was replayed through the **public HTTPS ingress** using a
bounded same-actor credential: Replayed=true/same2149652456, still exactly two
payments/Paid0.02. No third money operation occurred.

The browser also verified all six Nera choices, materialunitm, original stored
width810/manualPPP01=700, truthful production-data banner, disabled cancellation
and production controls, HTTPS session proof200 and zero page/console errors.
The unknown-result→401/403→same-key recovery regression retains the immutable
body/key rather than losing an earlier possibly committed payment.

All private browser contexts/cookies/tokens and local/remote issuers were
removed. Temporary verification sources were archived privately and removed
from the workspace. Production keys were never exported or modified. No
commit or push was performed. Test orders and the two authorized payments are
retained; no cancellation, refund, payment deletion or production document was
created.

The standard authorized work-period notification1121186 finally reported
Database Mail statusfailed. Its delivery was not claimed successful and no
manual resend/SMTP reconfiguration was performed. The period update itself
committed and real payments passed. See S2-010 in the issue index.


## 2026-11-01 — product-entry feedback and material picker

Remote build `m2-7e2d621` deployed with API/WebUI healthy. 230 Sales tests pass.
Chromium1600×1100 passed nine operator stages: actual material-row clicking,
Access stock columns, single-Tab select-all navigation, editable PPP, clear
width/native-note validation and field focus, missing-list-price guidance,
Back/discard without notification obstruction, Save/back/reload and restoration.
Zero page/console errors and unexpected HTTP failures. Evidence is private under
`/tmp/sales-m2-browser-ui/v18-*` and `/tmp/sales-m2-evidence/v18-*`.

Owned Testavimas2148197455 was the only edited order; note-only positive writes
were restored through the normal UI. Full SQL comparison shows all business
values restored, only the native product `dtDateUpdate` changed. No money or
production writes. The S2-012 observer was rebound to current API hostname
`d50f1e35a083` with its original2026-11-02T03:02:11.235796Z expiry and previous
capture preserved. Payroll/report optimization remains deferred in issues.


## 2026-11-01 — native Safari login regression

WebUI `m2-0a4bb90`; API remains `m2-7e2d621`. 256 Sales tests pass. Actual Safari
with the owner's already-issued Portal cookie reproduced a login loop before
the selector fix and opened authenticated Sales afterward. Verified Orders,
New order live reference loading/open/cancel and normal reload after temporary
auth diagnostics were removed. No login credentials, cookies, proofs or claim
values were logged. Safe server diagnostics and route audit confirmed valid
native Cookie authentication, successful proof/API reads and zero business
mutation routes. API and existing SQL observer were not restarted/reconfigured.

## 2026-11-01 — Cikada user mapping and legacy SQL context

The owner-approved identity boundary is now explicit: `admin` maps to `sa`;
`bykovas` maps to the exact Windows identity `TERMINAL\d.bykovas`. The new
account has the original admin's exact two roles (`WarehouseAdmin`,
`WarehouseManager`) and uses the owner-specified password, which is not stored
in repository files. Existing admin GUID, roles, status and password behavior
were preserved. Both profiles are stored durably in Warehouse PostgreSQL using
the reviewed additive migration004; no legacy SQL permission/department/principal
or stored-procedure changes were made.

All Sales SQL adapters use mandatory per-actor resolution and an unpooled
`EXECUTE AS LOGIN` lease with rollback-before-cookie-REVERT cleanup. No mapping,
inactive profile or unavailable identity service ever falls back to `sa`.
Windows group-authenticated identities are supported without creating new SQL
principals. Native SQL permission denials produce definitive403 responses.

Read-only live verification with existing restricted SQL login `OP046001`
(UKS0,1,6,7,9,11) matched the independent scoped total10012. A branch4 order was
absent from exact search and counts; number/ID/editor, preview, mismatched
product reservation, payment references and bank search all denied disclosure.
Catalog series/default customer/store/employee references and store filters
were scoped. The Windows owner identity could read that order as a positive
control; its existing UKS0–11 were left intact.

Runtime verification on test used actual native Portal password login as
`bykovas`, the resulting Portal cookie, the HTTPS Sales session-proof endpoint
and the normal protected API credential. Orders/counts, filters, live catalog,
owned Testavimas editor and number lookup returned200. Anonymous/unsigned Sales
credentials returned401, an unmapped actor403, forged mapping-lookup proof401,
and unsigned Warehouse administrator headers could not update user mappings
(403). Mapping administration additionally requires a purpose-bound proof and
current active persisted administrator rights; broader pre-existing Warehouse
authentication remains S2-017.

Local validation: all297 Sales tests and58 affected Warehouse tests passed;
Warehouse WebUI build and `git diff --check` passed. Pre-existing dependency
advisories are recorded in S2-018 rather than upgraded in this task. Remote
final-image/restart and note-only save results are recorded below after completion.

Actual note-only write verification on owned Testavimas order2148197455 passed
through native `bykovas` login and the normal API. Both save and restore receipts
contain the new Cikada actor GUID and `SqlLogin=TERMINAL\d.bykovas`; the native
product `sUserNameUpdate` also records that identity. Six groups of before/after
SQL business data matched exactly after restoration. Only native product audit
fields `dtDateUpdate` and `sUserNameUpdate` changed. No financial, production,
cancellation or other business mutations were performed in this verification.

Warehouse API was actually restarted on the same `m2-5823935` image at
2026-11-01T21:17:58.989Z. Image, environment and canonicalized mounts were
unchanged. Real native password login for both `admin` and `bykovas` passed
afterward; strongly authenticated user administration returned identical
before/after GUIDs, roles, active status and mappings. No reseed, password reset
or additional schema change was needed.

Final deployment: Sales API `m2-d81206f`, Warehouse WebUI `m2-2d0e8be`;
Warehouse API and Sales WebUI remain `m2-5823935`. All four services are healthy.
Portal containers/config/start times were unchanged. Existing connection,
financial receiver/Testavimas limits and cancellation/production OFF flags were
preserved. The S2-012 observer was rebound to the current API host with its
original2026-11-02T03:02:11.235796Z deadline; no monitoring window extension.

Final real browser under `bykovas` verified Admin Users shows both approved
mappings and identical roles, with empty create-user mapping/password fields
(the browser no longer mistakes the mapping for the login field). Sales loaded
live orders after the deployment, New order loaded live references, and Cancel
closed the form without creating an order. No Sales console errors were seen;
the old Warehouse circuit disconnected as expected during its UI replacement
and recovered after reload. Screenshot evidence is retained privately as
`/tmp/sales-m2-evidence/cikada-users-mapping-final.png`.

The complete native-login readonly runtime suite passed again on the final
images, including all401/403 negative identity boundaries. The final SQL
snapshot of the owned test order exactly matched the post-restoration snapshot,
including audit fields; the final smoke checks made no business writes.
Temporary QA credentials, cookies, tokens and proof text files were deleted.
Verification harness source was archived privately and removed from `.scratch`;
the task SQL tunnel was closed. Shared Data Protection keys were untouched.
No commit/push was performed. Remaining findings are in the issue index.

## 2026-11-02 — cancelled-status read model and privacy notice (S2-019)

Cancellation is read from the actual `Sugadintas` flag, overrides the old
workflow label as `Anuliuotas`, and is filtered before paging/counting. The
filter reference includes that choice. The existing SQL-login department
checks still protect list/counts, number lookup, exact header and children.
The privacy notice uses only the exact normalized legacy order note
`Kliento prasymu anuliuotas`, exposed additively as `IsPrivacyRestricted` on
details and evaluated by the same shared helper in the live editor.

Owner-approved English copy is a translucent watermark and accessible notice
in details and the active order/product workspace. Existing customer/products
and all native prices remain visible; no money is zeroed client-side and no
GDPR erasure process or cancellation command is run by this presentation.
Ordinary cancellation does not activate the privacy notice. The raw marker
is replaced by that notice in the editor's note presentation.

Local full Sales suite318/318 passed, including nine real-component rendering
cases and twelve cancellation-label/marker/contract cases. Actual current SQL
adapter/factory read-only verification passed for `OP046001` and the exact
Windows `TERMINAL\d.bykovas`: cancellation totals1876/3133 and Gaminamas
totals190/446 match independent scoped native SQL; pages of100 contain only
the matching effective state, with native price/debt equality. Existing
AB-11499-26 is cancelled+privacy with native/projected zero prices;
AB-11236-26 is cancelled without privacy. Normal orders do not acquire a
notice. OP046001 cannot read denied SC-3419-26 or find it through the
cancelled filter. No business DML, legacy schema/procedure/grant or gate changes.
Readonly harness source was archived privately and its SQL tunnel closed.
Deployment/browser/runtime evidence follows below.

Final v23 deploy `m2-33fa509` replaced only Sales API/WebUI; both healthy,
Warehouse and Portal IDs/config unchanged, existing connection and independent
financial/cancellation/production gates preserved. No legacy business/schema
writes. The expired SQL observer was absent and was not restarted/extended.

Actual native Portal login as `bykovas` passed the deployed GET-only runtime
suite: filter includesAnuliuotas, count3133,25 returned rows all cancelled,
Gaminamas446; special/ordinary/normal fixture flags and native zero prices
match. Exact search within the cancelled filter includes cancelled and excludes
active fixtures. All tested data API operations were GETs.

Real browser under `bykovas` verified100/100 cancelled rows and total3133,
ordinary cancelled AB-11236-26 with no privacy notice, and AB-11499-26 with the
approved notice in details, order editor and the actual VEGA product editor.
Exactly one active notice, retained products/customer/zero prices, no raw marker
text, working notice Back to orders from details and product, zero console errors.
No form values were changed or saved. Screenshot evidence is private under
`/tmp/sales-m2-evidence/v23/`: `privacy-details-final.png`,
`cancelled-filter-final.png`, `privacy-editor-final.png`, `privacy-product-final.png`.

Final application source hashes match the deployed v23 archive with zero drift.
Public Sales health200 (~0.194s); both Sales services healthy. Private QA
credentials and executable helpers were removed and no QA containers remain.
Shared auth keys were untouched. No commit/push was performed.

## 2026-11-02 — owner activation of confirmation and note history (S2-003/015)

The owner explicitly requested enabling production confirmation on test for
manual testing. The existing Testavimas scope remains mandatory; prefix MEST
fits the verified downstream32-character document contract. Cancellation stays
off. Activation verification uses GETs only and does not create any production
output document. Earlier confirmation-OFF instructions are historical.

Details/editor now expose current note separately from read-only native
UzsakymuPastabos entries, retaining NoteID, exact text, author, timestamp and
duplicates in deterministic NoteID order. The history query follows authorized
parent resolution. Editable command contracts reject submitted history. The
privacy marker is translated only in rendering. No history records or prices
are changed. Full local Sales suite321/321 and diff checks passed.

Final release `m2-686afeb`: confirmation=true with prefix MEST on test;
Testavimas455 exposes enabled confirmation, ordinary branch remains disabled.
Native Portal login/session proof/API credentials and SQL actor work after
moving the unchanged credential codec into shared PortalAuth. WebUI references
only Contracts/BuildingBlocks; shared payment formats and sample data retain
existing behavior. Full321 Sales tests and29 architecture tests passed.

Native history equality passed for details/editor (IDs, exact text, author,
timestamps and ordering), including owned450/XXXX-943185-26 with one historical
entry. Browser verified current note/history separation and enabled Confirm
for production on455 without invoking it. Screenshot:
`/tmp/sales-m2-evidence/v25/note-history-final.png`. No confirmation documents,
legacy business writes, history edits or other branch writes were executed.
Warehouse/Portal/config gates preserved, cancellation off; expired observer
untouched. QA credentials/helpers removed; deployed application source drift0.
All implementation and follow-ups are in one PR: https://github.com/lauresta/lkvitai-mes/pull/224.
