# M2 command persistence and concurrency contract

Status: original proposed protocol, 2026-10-31, subsequently implemented for
core Testavimas commands. Historical no-DDL/no-write/blocker statements below
describe the original discovery stage. Current SQL execution, receipt DDL and
remote deployment evidence are in [verification](milestone-2-verification.md);
final deployed mechanics and intentional differences are in
[persistence implementation](milestone-2-persistence-implementation.md).
The [M2 plan](milestone-2-plan.md) controls remaining scope and acceptance.

Observed target is SQL Server 2014 (`12.0.6024.0`), server `TERMINAL`, database
`LKVITAIDBV1SQL`. Both the configured target `192.168.0.3` and supplied host
`10.11.12.103` report that server/catalog/version and matching database GUID
`31FC1889-3D3E-4422-A4E3-362899649FF5`; active Access client comparison remains
a separate evidence requirement. The three
core order/product/accessory tables have no timestamp/rowversion column.
`Uzsakymai.UzsakymasID` and
`ParduotiGaminiai.ParduotoGaminioID` are bigint identities.
`ParduotiPriedai` instead has the composite PK
`(PrduotoPriedoID int, ParduotoGaminioID bigint)` with no generated row identity.
Accessory commands and receipt RelatedIds must represent that composite key;
do not invent `accessoryRowId` or treat an accessory-definition ID as unique.

Live scope discovery identifies Testavimas as series branch `FilialoID=7`,
with series IDs `2136330188`, `2136330191`, `551738835`, `768439496`.
Existing web reads explicitly exclude branch 7. These series may point to
ordinary store locations: enforce scope through the order's actual series
and that series' `FilialoID`, not `Uzsakymai.VietaID` or the displayed store.
The configured allowlist must retain this relationship check at execution;
IDs in this document alone are not enablement of writes.

## Existing architecture and proposed first slice

Sales currently has `IOrdersQueryService`, read-only `OrdersEndpoints`, and
`SqlOrdersQueryService` using Microsoft.Data.SqlClient. Application has ports
and CQRS abstraction references; Infrastructure already references Application
and Contracts. Display DTOs are not writable editor contracts: products and
accessories have no stable IDs or revision, and employee data is display text.

Add typed editor/command DTOs in Sales.Contracts, an order-entry application
service with small read/write ports, and a SqlClient adapter using a supplied
connection/transaction for each operation. Reuse ordinary dependency injection.
Do not introduce a generic workflow engine, generic entity patch endpoint,
Warehouse persistence or a second transaction coordinator. The existing
Warehouse idempotency behavior is not evidence of SQL Server atomicity here.

First command set, after complete rules for the selected type are known:
`header.create.v1`, `header.update.v1`, `product.create.v1`,
`product.configure.v1`. Product create must use the actual permitted incomplete
state/defaults. Do not invent a valid configuration solely to fit the UI.
Later commands for accessories, address/duties, duplicate/delete, payment and
confirmation follow the same protocol but have distinct allowed inputs/rules.

Every request includes `commandId` (UUID), operation/contract version, exact
legacy target IDs, original `expectedRevision`, and allowed edited fields.
`actorId` comes from trusted API authentication. Reject unknown fields rather
than silently treating arbitrary status, calculated amount or audit input as
an authorized edit. Validate child→parent→order ownership in SQL.

## One technical table in the legacy database

Choose `mes_sales.CommandReceipt` in the **same database and transaction** as
legacy changes. It is also the durable successful-command audit. No separate
audit table is necessary for M2 unless retention/access rules require one.
Failed/denied attempts go to structured application logs with correlation ID;
a rolled-back database operation must not leave a committed success receipt.

The following DDL is a proposal, not a deployment script. Verify available
collation, object-name collision and actor ID bounds first. It uses SQL Server
2014-compatible types; no SQL JSON functions or newer CREATE OR ALTER syntax.
Bigint below is a technical envelope, not permission to change legacy key types.

```sql
CREATE TABLE mes_sales.CommandReceipt
(
    ActorId             nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    Operation           varchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CommandId           uniqueidentifier NOT NULL,
    RequestHash         binary(32) NOT NULL,
    ContractVersion     smallint NOT NULL,
    OrderId             bigint NULL,
    EntityKind          varchar(32) NOT NULL,
    EntityId            bigint NULL,
    RelatedIds          nvarchar(max) NULL,
    ResultCode          varchar(64) NOT NULL,
    ResultRevision      binary(32) NULL,
    StartedAtUtc        datetime2(3) NOT NULL,
    CommittedAtUtc      datetime2(3) NOT NULL,
    SqlLogin            nvarchar(128) NOT NULL,
    BuildVersion        varchar(64) NOT NULL,
    CorrelationId       uniqueidentifier NOT NULL,
    CONSTRAINT PK_SalesCommandReceipt
        PRIMARY KEY CLUSTERED (ActorId, Operation, CommandId)
);
```

Creation of the schema itself belongs in an explicit reviewed deployment
script. The table has **no identity**, trigger or FK/cascade to business rows.
Receipt insertion must not change legacy `@@IDENTITY` behavior, and deleting
a business entity must not erase the audit. Do not add triggers or columns to
legacy tables just for this proposal. Add a nonclustered `(OrderId,
CommittedAtUtc)` index only if required for actual support queries.

`RelatedIds` is application-serialized IDs only, using a versioned structure;
it does not contain customer names, address, raw input or a second order
snapshot. `ResultCode` is a successful typed outcome such as `created`,
`updated` or `deleted`. `SqlLogin` is diagnostic actual SQL identity, separate
from trusted Portal ActorId. Existing legacy audit remains untouched.

Retain committed receipts without automated pruning for initial M2. This
keeps all issued IDs resolvable and avoids a silently forgotten retry becoming
another payment. Retention/archival can be specified later with a bounded
submission-token protocol and persistent tombstones before any deletion is
introduced. UI automatic retry/recovery window is 24 hours initially; older
commands use outcome lookup/support rather than automatic mutation retry.

## Canonical input and atomic execution

RequestHash is SHA-256 over a deterministic typed serialization with explicit
operation/contract version, target IDs, expected revision and editable input.
Specify field order, decimal representation and UTC/date-only representation;
distinguish absent, null, empty and zero where the actual legacy contract does.
Do not hash arbitrary incoming JSON text, labels, HTTP headers, UI timestamps
or fresh current-time defaults. Keep the original request immutable for retry.
The hash must be stable across instances and deployments of the same version.
Creation-date/year defaults are fixed in the original creation command, then
validated server-side; a midnight/year rollover must not change its identity.

Per attempt:

1. Authenticate with the trusted Sales scheme. Check write gates, supported
   operation/type, trusted actor and configured database identity/scope. Reject
   invalid request shape before opening the transaction.
2. Open SqlConnection and short SqlTransaction. Resolve the unique receipt key
   through its PK using `UPDLOCK, HOLDLOCK`, including the absent-key range.
   A second identical request waits for the first transaction to finish; there
   is no committed pending row requiring a lease/background recovery process.
3. If receipt exists, compare RequestHash. A mismatch is
   `409 COMMAND_ID_REUSED`; no mutation. Before returning even a matching
   receipt, revalidate caller access to its recorded order/current parent, or
   verified recorded scope for a deleted entity. Never disclose another
   actor's outcome. Return committed IDs/outcome with `replayed=true`.
4. If absent, lock/read the parent and relevant rows/collections in the defined
   order below. Validate ownership, allowed state, Testavimas scope, revision,
   live references, eligibility and calculation/price expectations.
5. Run only the verified business operation, enlisting **every** command in the
   same transaction. Update only intended changed columns in the legacy event
   sequence. Re-read trigger-computed values; build revision and typed result.
6. Insert the completed receipt with actor/IDs/hash and diagnostics. Commit.
   Only then return `committed=true` and the result. Any failure before commit
   rolls back both changes and receipt; do not emit a success from `OUTPUT`.

No network request, Access invocation or UI think time is inside this
transaction. Verify stored procedures neither commit the ambient transaction
unexpectedly nor swallow errors. A SQL exception/RAISERROR is translated only
after rollback is established. Trace trigger mail/external operations separately:
transaction rollback does not establish their rollback or retry safety.

A receipt represents the original committed operation, not today's entity
snapshot. After replay/recovery, load the latest editor state separately so an
old response cannot overwrite changes made later by Access or another operator.
A no-op save returns unchanged state and records the completed command without
issuing redundant business-table UPDATEs that could fire side effects.

## Concrete concurrency strategy without new legacy columns

The collected metadata has no rowversion in the three main tables. Select a
**server-computed aggregate content revision**, checked while rows/ranges are
locked. This requires no Access client/schema modification.

For the first slice, hash a versioned deterministic snapshot of the order's
state/branch/series/customer and editable business values, product rows and
applicable attached accessory rows, including stable IDs and all inputs or
calculated fields the command could overwrite/depend on. Include collection
membership sorted by PK. Expand the snapshot for address/duty/payment actions
before enabling them. Include the composite accessory key noted above. Do not
include current clock time or display labels.
Nulls, SQL money/decimal scale, datetime resolution and binary values require
explicit typed serialization; SQL CHECKSUM/string concatenation is unsuitable.
Compute SHA-256 in .NET; SQL Server 2014 HASHBYTES has an input-length limit
that makes concatenated order/notes hashing unsuitable for this snapshot.
The observed product PPP01–PPP11 are nullable nvarchar; PPP12 and PPP12NEW
are nullable decimal. Width/height are nullable int, quantity decimal and
price/amount money. Header notes/address and product notes include ntext;
read these values into the typed application hash, not a SQL equality predicate
that assumes ntext can be compared like nvarchar. Preserve nulls on unrelated
edits. Exact max lengths/precision/scale come from the contract metadata map.

Initial conservative strategy: every Sales command on an order takes locks on
the parent, then products ordered by PK, then accessories ordered by parent/PK,
then other required collections in one documented order. Use `UPDLOCK,
HOLDLOCK` targeted reads and indexed parent-ID predicates to hold row/range
locks through compare/write/commit, including empty collections. Verify actual
plans/indexes first: a missing child-FK index can turn this into broad blocking.
All commands and editor-read snapshots use a consistent footprint. A short
transactional editor read must assemble a coherent snapshot/revision; it must
not combine torn header/child reads with an apparently authoritative token.

Observed supporting indexes: header has unique `SecondaryKey(UzsakymasID)`;
product has PK on `ParduotoGaminioID`, several `UzsakymoID`-leading indexes
including `(UzsakymoID,ParduotoGaminioID,...)`; accessory has a
`ParduotoGaminioID`-leading index plus its composite PK. Thus targeted parent
and collection predicates have existing candidate index paths. Inspect actual
plans/locks with a fixture before activation; do not add an index speculatively.

Under the write locks, recompute the current content revision and compare it
to expectedRevision **before** any mutation. A difference returns
`409 STALE_ORDER` plus a newly read authorized comparison view; retain the
operator's original buffer. Never auto-merge financial/state/product changes.
Do not release locks between version comparison and writes. Targeted SQL
conditional updates still check affected-row count. Parent-only app locks or
an in-process mutex cannot protect against Access and are not the concurrency
mechanism. A rowversion-based optimization is optional only after proof that
it covers the same write/read dependency footprint.

This can conservatively conflict on another line's edit; safe conflicts are
preferable for the first slice. Later narrowing requires demonstrated tests,
not removing child checks to improve apparent success rate. Changing a value
and changing it back is invisible to a content fingerprint; document that
limitation. If detecting every intervening change is a business requirement,
existing complete version tracking or a separately reviewed schema/client change
is required.

Access writes will respect SQL locks while a Sales command commits. They may
follow a different lock order, causing deadlocks. Only a proven deadlock-victim
rollback may receive a bounded retry with the **same** ID/payload. Network or
commit-acknowledgement failure is an unknown outcome, not a rollback. Keep
server retry bounds small (initially one rolled-back deadlock retry), log them,
and validate the actual Access workflow under concurrent use.

Sales can detect changes Access committed before its comparison. It cannot
prevent a later unconditional stale Access UPDATE. Verify Access bound-form
optimistic concurrency and report the actual reverse-direction protection.

## ID retrieval selected from the observed procedure

For header direct INSERT, capture the inserted ID in the same statement/scope
using `OUTPUT INSERTED.<PK> INTO @ids` if applicable, followed by authoritative
SELECT after triggers. An INSERT into a trigger-enabled table must use an
appropriate OUTPUT INTO form; inspect INSTEAD OF triggers before assuming IDs.

Observed `sp_CreateNewItem(@iOrderID bigint,@iTypeID int)` contains only
`INSERT dbo.ParduotiGaminiai(UzsakymoID,ParduotoGaminioTipoID,Spalva)
VALUES(@iOrderID,@iTypeID,0)` and `SELECT @@IDENTITY`. Select a Sales-only
parameterized adapter batch preserving that exact INSERT/default/trigger
behavior and capturing the true ID through OUTPUT INTO:

```sql
DECLARE @ids table (ParduotoGaminioID bigint NOT NULL);
INSERT dbo.ParduotiGaminiai (UzsakymoID, ParduotoGaminioTipoID, Spalva)
OUTPUT INSERTED.ParduotoGaminioID INTO @ids
VALUES (@iOrderID, @iTypeID, 0);
SELECT ParduotoGaminioID FROM @ids;
```

This is the selected implementation contract, not executed SQL. The existing
shared procedure/result shape stays untouched for Access. Follow with typed
SELECT of that exact ID after triggers and assert one row plus correct order/
type ownership. Revalidate captured INSERT semantics against current definition
before enabling this capability, with schema/procedure drift detection recorded
in the deployment evidence. If local policy requires all inserts through a
procedure, put this exact batch in a Sales-specific additive wrapper rather
than modifying the shared legacy contract. It is not a wrapper that executes
the old procedure then asks SCOPE_IDENTITY. Never use MAX/latest/IDENT_CURRENT
or infer an inserted ID from collection ordering.

Observed enabled triggers include product insert/update/delete, accessory
insert/update/delete and order insert/update/delete. Product insert invokes
legacy audit and an email routine; order update can invoke email, salary and
finish processing, and some order triggers explicitly ROLLBACK. Capturing an
ID correctly does not establish that these effects are safe in Testavimas.

Further current-definition blockers reported from the complete metadata:

- `sp_DeleteBlind` globally disables a trigger on
  `TAB_ProductionOutputDocumentDetails`, then deletes/re-enables it, with
  nested transaction handling and no TRY/CATCH. Do **not** invoke this path
  under the plan's no-global-trigger-disable boundary. Deletion remains off
  until a narrow, legacy-compatible safe alternative is reviewed.
- `trg_ChangeOrder` can email on `Nuolaida >= 20` without a Testavimas exclusion;
  stored-final-amount, Paid and State changes can invoke payroll effects.
  Discount/price/payment/state mutation cannot be considered contained merely
  because the order's series belongs to branch 7.
- Payment insertion can mutate employee cash, bank-transfer assignment and
  send email irrespective of branch. Keep payment commands disabled pending
  actual workflow/containment evidence; rollback-only tests are insufficient.

## Recovery, testing and blockers

UI stores the pending command ID/operation and target reference in per-tab
sessionStorage before submitting; no credentials/customer payload. Keep the
immutable payload in the edit buffer. On reconnect retain the same ID; after
reload, outcome lookup can find a completed create even if its order ID was
never received. An absent outcome after a timeout does not establish failure:
the old transaction may still be running. Same-key replay waits/resolves it;
when the payload is unavailable, outcome lookup remains read-only and the UI
must not recreate it under a new ID automatically.

Tests required before first write activation (all currently **not run**):

- Same key/payload simultaneously, after restart, after lost commit response:
  one business mutation/one receipt and the same IDs; differing payload is 409.
- Rollback after trigger execution, before receipt insert and during commit:
  no false success; unknown outcome resolves through the same key.
- Access/Sales and Sales/Sales edits, insert/delete a child while another editor
  is open, empty child-collection phantom, stale state/branch, stale price quote.
- Nested identity trigger returns true parent ID; no-op update produces no
  duplicate mail/audit/financial side effects.
- Unauthorized/guest/dev actor, wrong branch and cross-order child: no writes,
  receipt disclosure or new technical success audit.
- Old Access API signatures still work; app rollback leaves committed orders,
  receipts and Access usable.

Still unresolved: deployed target comparison and available collation; complete
trigger/procedure transaction and side-effect containment; exact snapshot
field inventory and query-plan/lock evidence; correct Access
client concurrency mode; first product's full rules; safe Testavimas IDs/series;
permission to deploy required additive SQL once reviewed. These are explicit
gate-0 evidence requirements, not assumed completed implementation.

Technical semantics are based on Microsoft documentation for
[locking hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table),
[identity scope](https://learn.microsoft.com/en-us/sql/t-sql/functions/scope-identity-transact-sql)
and [OUTPUT](https://learn.microsoft.com/en-us/sql/t-sql/queries/output-clause-transact-sql).
