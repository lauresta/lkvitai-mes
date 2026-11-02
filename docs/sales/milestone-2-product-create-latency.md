# Product-create latency investigation

Initial read-only inspection on2026-11-01 against the configured
LKVITAIDBV1SQL SQL2014 target. No product, trigger, index, isolation option or
schema was changed during that initial inspection. Later controlled fixture
operations and their restoration are recorded in the update below. This identifies a concrete expensive/wait-sensitive legacy
path; it does not establish the historical blocking session for order2148197452.

## Follow-up investigation, 2026-11-01

**Confirmed:** a severe, contemporaneous reporting workload. **Still not
reconstructed:** the original Sales session's precise wait/blocker. Keep
S2-012 open; an overlapping CPU-heavy job is not proof of the sole cause of
the one historical timeout.

### Historical job and timing

The retained system_health ring and event files cover the original incident.
SQL local time is UTC+02. The POST ran approximately00:27:48–00:28:18 onNov1.
At00:27:52.650, system diagnostics recorded system CPU100% and SQL CPU99%.
SQL Agent job **UpdateSalaryReportsData** ran00:00:00–00:32:54.

| Phase | Historical elapsed | CPU time | Logical reads |
| --- | ---: | ---: | ---: |
| sproc_GenerateSalaryReportData | 983.345s | 11,006.638s | approximately2.633 billion |
| sproc_GenerateTurnOverData | 987.425s | 10,983.117s | approximately2.633 billion |
| Remaining three reporting procedures | approximately3.1s total | — | — |

Both first procedures execute the same normalized #R completion-date
calculation. The second hot statement ran about00:16:23–00:32:51, overlapping
the failed POST. Together these passes averaged about11 of the server's12
logical CPU cores for almost33 minutes. Its plan repeatedly scans historical
production documents/details beneath correlated nested loops and an index
spool, rather than processing the event history once. The turnover call
returns a result set without persistent business DML; whether that result
is consumed should be reviewed, not assumed.

The trace's EKA missing-statistics event on SPID64 belonged to our
SalesM2ReadOnlyDiscovery observer, not the failed Sales POST. No captured
deadlock, autogrow, worker exhaustion, OOM or long-IO fault establishes an
alternative historical cause. Barcode UPDATE/outer INSERT require no workspace
memory grant; their cached physical-read counters do not support a cold-scan
explanation. Default health lock-wait collection requires waits longer than
30 seconds, so absence of its event cannot rule out a29.4-second lock wait.

### Bounded concurrency checks

The actual Sales adapter was run in the current Linux API image and its
network context, on owned fixtures453/454 only. Normal sequential create took
202ms, create alongside repeated reads148ms, and two concurrent creates280ms
and415ms. Fifteen reads completed without a30-second stall (maximum111ms).
Live sampling captured only short69ms/37ms lock waits during these checks,
including header table intent-lock and product range-update-lock waits.
These show ordinary cross-order contention can occur, not that the historical
blocker is known. A separate local-tunnel run also completed successfully; its
WAN-inflated timings are not substituted for the Linux server measurements.

Eight temporary products across both controlled runs were created and removed
through the adapter's normal trigger/receipt and safe-delete paths. No original
product was deleted. Existing product configurations/accessories/payments and
monetary totals matched prior readback afterward, including owner452 unchanged
and450's original two payments. Native audit/receipt records remain. Owned453's
three derived aggregate columns normalized from NULL to0 on native deletion;
its stored total stayed0. This is recorded rather than claiming byte-identical
headers. No financial posting or production confirmation was issued.

### Tested optimization proposal — NOT APPLIED

[Review and validation](s2-012-completion-dates-review.md) and
[review-only SQL](s2-012-completion-dates-proposal.sql) replace repeated sums
with timestamp-grouped cumulative event balances while preserving existing
order-wide semantics. A single MAXDOP1 temporary-table execution took964ms,
returned24,729 completion dates, and matched all23,858 shared persisted salary
report dates exactly. Four candidate-only orders are documented, not classified
as errors. The original full expensive procedures were not rerun. These are
different historical/current execution conditions, not a controlled speedup
benchmark or future SLA. No production procedure/job/index/configuration was
changed; applying it is a separate production decision.

### Exact wait capture now enabled

A separate bounded **read-only** observer watches slow requests from the actual
Sales API host. It records SQL connection/session IDs, current wait/resource,
blocker identity/module, query hashes, CPU/reads and scheduler runnable counts.
It hashes batch text instead of writing SQL text/parameters/credentials. It was
validated with a2.4-second read-only row lock on owned453:12 observations correctly
identified the waiting request and exact blocker. No business DML was used for
that observer validation.

The diagnostic container has a0.1-CPU/256MB resource cap,64MB log cap and automatic
18-hour stop: started2026-11-01T09:02:11Z, expires2026-11-02T03:02:11Z
(05:02 Vilnius time). It uses the stack's named network, not a dependency on
the API container's network namespace; it does not prevent API replacement.
Its target host is pinned to the current API container: restart/rebind the
observer if the API is replaced before expiry. No public endpoint or restart
policy was added. Details and stop/retrieval instructions are in
[s2-012-diagnostic-observer.md](s2-012-diagnostic-observer.md).

Private evidence: /tmp/sales-m2-evidence/s2012 and historical s2012-* artifacts
in its parent directory. Raw plans/health XML, proposal validations, exact
command journals and restoration comparisons are retained with restricted
permissions. The initial cache/scan evidence below remains historical context.

## Observed request and transaction behavior

The existing API log records product-create for order2148197452 returning503
with SQL provider number0 after30058.652ms at2026-10-31T22:28:18.269Z. An
independent GET for the same order took10769.2365ms and completed immediately
afterward; the following GET took34.0949ms. The order had no product or product
command receipt afterward. An isolated read-only customer eligibility call
was approximately0.14s in the parent investigation. These facts do not locate
the failing statement or prove the absence of a blocker during the request.
Provider number0 is not evidence of a SQL1205 deadlock victim; the existing
same-command recovery protocol must remain intact.

ExecuteAttempt, its initial/final ReadAsync calls, eligibility/type validation,
product INSERT and receipt INSERT share one SqlConnection and SqlTransaction.
Each Rows call consumes and disposes its reader before the next statement.
No application-created second connection inside this product mutation was
found. GET and outcome reads also use Serializable transactions and take
UPDLOCK,HOLDLOCK on the order and children. An overlapping GET can consequently
wait for the command's order lock; that explains a possible follower wait,
not the cause of the original long command. Changing isolation based solely
on this correlation is not justified.

## Live trigger/statement statistics

At SQL local2026-11-01T00:41:26.670 (UTC2026-10-31T22:41:26.670):

| Cached operation | Executions | Latest execution, SQL local | Latest elapsed | Maximum elapsed | Latest logical reads |
| --- | ---: | --- | ---: | ---: | ---: |
| trg_NewBlind as a whole | 15 | 00:39:35.617 | 136.718ms | 29394.531ms | 25765 |
| First SELECT assigning new barcode from inserted ID | 15 | 00:39:35.617 | 0ms | 0ms | 2 |
| Barcode/insert-metadata UPDATE | 14 | 00:39:35.617 | 92.773ms | 160.156ms | 23905 |
| Aggregate order-state UPDATE | 14 | 00:39:35.710 | 43.945ms | 59.570ms | 1849 |

The trigger's maximum worker time was219.726ms, far below its maximum elapsed
29.394531s. Its15 executions versus14 completed barcode UPDATEs/subsequent
statements are consistent with one invocation being interrupted before that
UPDATE completed. Completed-statement counters omit interrupted work. This is
supporting evidence of a wait-sensitive barcode UPDATE path, not attribution
of that historical maximum to order452 or proof of a particular lock/I/O wait.
These are plan-cache lifetime counters: no timestamp for the maximum-duration
invocation is provided. The latest00:39:35.617 timestamp can be correlated with
the parent/browser453 request log, but cannot retroactively timestamp the
29.394531s maximum.

## Cached barcode UPDATE and confirmed cascading foreign key

The active trigger computes its barcode from the inserted product ID, then:

```sql
UPDATE dbo.ParduotiGaminiai
SET iBarCode = @sNewBarCode,
    sUserNameInsert = SYSTEM_USER,
    dtDateInsert = GETDATE()
WHERE ParduotoGaminioID IN
      (SELECT i.ParduotoGaminioID FROM inserted AS i);
```

Cached SQL query hash: `0x8D406EF44158C4E0`.
Cached SQL query-plan hash: `0x5F8A1A1C746926A0`.
SHA256 of the exact cached trg_NewBlind XML text encoded as UTF-8:
`181267e4973b4519d454d1498ce20bbb0144a5d9d42df54c40978d7d8f8b0700`.
The hashes identify this captured artifact, not every future plan.

The plan seeks the inserted product, but also contains a secondary clustered
UPDATE of TAB_ProductionOutputDocumentDetails and a clustered scan of its
PK_TAB_ProductionOutputDocumentDetails, estimated370748 rows. Live metadata
confirms the enabled foreign key:

`FK_TAB_ProductionOutputDocumentDetails_ParduotiGaminiai`:
TAB_ProductionOutputDocumentDetails.iBarCode → ParduotiGaminiai.iBarCode,
ON UPDATE CASCADE.

That child table currently has only its unique clustered primary key ordered
(iProductionOutputDocumentID,iBarCode); no barcode-leading index exists. This
confirms that the plan's document-detail work is referential cascade handling,
even for the parent trigger's one-product UPDATE. Estimated rows and cached
operators are not an actual-row-count profile of the failed request. No
matching child record has to be changed for a scan to incur reads/waits.
The inserted barcode column is nullable with no default in the inspected
schema. The cascade plan filters whether the old barcode differs from the new
value, then joins the child scan on the old barcode; it does not have a
barcode-leading seek available even when no matching child should exist.

The next trigger statement, query hash0x63CCB0A655D3DFCD / plan hash
0xC131CA91D606DF40, aggregates manufactured product states. Its captured plan
scans ParduotiGaminiai_UzsakymoIDParduotoGaminioTipoIDBusenosID, estimated113048
rows, rather than seeking just the inserted order. This is a second confirmed
wide read under the enclosing transaction. Barcode generation's older MAX/
COUNT implementation is commented out and must not be cited as an active
sequence-contention cause.

The audited insert path then updates the order state, invokes its scoped
finish/salary logic and appends AuditUserActions. No second database connection,
linked-server call or active email call was found in this create path. Salary
reads MTTools via a same-instance three-part name. Trigger ordering and plan
breadth do not prove which resource blocked the historical request.

## Next diagnostics using existing objects

1. Keep the parameterized-statement fingerprint, duration, provider-error and
   native-inner-code logging added to the application. Correlate the next
   naturally requested product create with its command ID and exact request
   timestamps; never repeat an unknown command with a new GUID.
2. During a real wait, sample sys.dm_exec_requests/waiting_tasks and the blocker
   transaction/lock resources. Map a captured KEY/PAGE resource to the existing
   document-detail primary key or other object. After-the-fact absence of active
   blockers is insufficient. No artificial blocking transaction is needed.
3. Compare current trigger/statement counters before and after that request,
   recording SQL local and UTC clocks. Reuse the query hashes above; preserve
   their current cached plans before any plan change or cache eviction.
4. A separate SELECT-only plan comparison can test explicit per-order state
   aggregation against existing order-leading product indexes. The document-
   detail FK scan cannot be assumed seekable by iBarCode using the current
   document-leading key. Any trigger rewrite or new supporting index would be
   a global production change requiring concrete evidence and separate review;
   none is proposed as an automatic fix here.

Serializable scope and long application transactions can amplify the duration
of shared reads/locks, but these observations do not establish that isolation
caused this failure. Do not change database-wide isolation, disable a trigger,
clear the plan cache or add an index as a diagnostic side effect.

Private evidence: productcreate-timeout-readonly.json (plans/stats),
productcreate-timeout-indexes-readonly.json (FK/index/database metadata), and
productcreate-timeout-correlate-readonly.json (clock and statement counters)
under /tmp/sales-m2-evidence. These contain no newly issued business writes.
