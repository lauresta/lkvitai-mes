# Sales legacy SQL authorization

Sales resolves each authenticated Cikada actor through a server-owned, active
mapping to an exact legacy SQL identity. The browser cannot choose that identity.
Every live Sales SQL adapter requires `ILegacySqlConnectionFactory`; there is no
constructor fallback to the shared connection's privileges.

The factory verifies the physical service connection's database identity before
impersonation, switches to the mapped login, verifies the effective SQL identity,
and returns an unpooled lease. Transactions belong to that lease. Disposal rolls
back unfinished work before cookie-based REVERT with an independent cleanup
budget; the physical connection is closed even if cleanup fails. Connection
pooling is disabled so a security context cannot reach a subsequent request.

## Department rules

`dbo.Loc_leidimas()` selects `SYS_SQLLoginsDepartments` for `SYSTEM_USER`.
Order-entry department rights use its `UKS` column, matching the inspected Access
order list, customer selector and payment employee/register selectors. SQL
sysadmin/db_owner membership and Cikada roles do not substitute for this filter.
An explicitly configured service-login administrator still receives only that
login's current `UKS` rows.

| Resource | Server-side scope |
| --- | --- |
| Order list and total | Series `FilialoID` must be in acting `UKS` before search, row numbering and counting; full projection repeats the guard. |
| Order number / details / editor ID | Both lookup and header reads require the same series department; denied and absent orders return the same not-found result. Child detail procedures run only after this guard. |
| Preview with order/product IDs | Parent must be visible and product must belong to that exact parent before reservation add-back or calculation-reference reads. Denied/mismatched IDs return404. |
| Series references and counts | Series department must be permitted. Cross-department default customer/store/employee references are omitted. Hidden/inactive flags remain separate reference-selection rules. |
| Customer references | `PriskirtasFilialui` in acting `UKS`. |
| Store references and list filters | `Filialas` in acting `UKS`. |
| Employee references / new duty assignments | `FilialasID` in acting `UKS`. |
| Command receipts | Trusted actor plus command ID, followed by current parent visibility. Requested parent access is checked before command-conflict responses. |
| Payment references / bank search | Current parent authorization precedes financial lookups. EKA departments use acting `UKS`; approved receiver exceptions also require the receiver department. An accessible selected register's native default-receiver rule remains intact. |
| Address references | `DIC_Addresses` is a global geographic hierarchy without a department column. Stored order address reads/writes require the authorized parent. |

Legacy taxonomy, material quantities, pricing and bank-transfer selection retain
their inspected SQL source rules under the mapped identity. No department relation
is invented for tables that have none. In particular, the legacy bank selector
uses available Lithuania transfers; access to its Sales endpoint still requires
the authorized parent and the deployment's financial gates.

## Mutation boundaries

All mutations resolve the permitted parent inside their transaction, then apply
revision, state and deployment checks before business effects. New headers require
a visible permitted series before insertion. Newly assigned customer/store/people
references are checked independently in that transaction; unchanged historical
references on an otherwise permitted order are preserved.

The shared-production test restriction remains additional: only the configured
Testavimas series may be written. It is not a read-authorization grant. Cancellation
and production-confirmation deployment switches remain independent and disabled.
Native SQL permissions, financial periods and triggers execute as the mapped
identity. Existing Access product/order state flags remain separate edit rules;
SQL db_owner membership does not prove equivalence to every Access UI control rule.

## Verified identities and tests

The exact Windows identity `TERMINAL\d.bykovas` is distinct from the SQL-login
alias `d.bykovas`. Current read-only inspection found the Windows identity's
`UKS` values0–11; the SQL alias has none. Domain names must not be stripped or
silently mapped to another login. No department rows, SQL permissions, users or
roles were changed for this verification.

## Cikada identity boundary

Sales resolves the authenticated Portal subject's stable GUID against the active
persisted Cikada user on each API request. The internal lookup uses a separate
short-lived Data Protection proof; usernames, browser parameters and unsigned
Warehouse role headers cannot select a SQL identity. Missing/inactive mappings
are denied before opening SQL. Mapping-service failure returns unavailable;
there is no fallback to the service account.

Admin Users exposes an explicit nullable `LegacySqlLogin` field. Administration
uses its own proof purpose plus current persisted administrator rights. The
owner-approved accounts on test are `admin` → `sa` and `bykovas` →
`TERMINAL\d.bykovas`; both retain the same Cikada roles as the original admin.
Their GUIDs, password hashes, roles and mappings are durable PostgreSQL data,
not deployment seeds. See [durable-local-users.md](durable-local-users.md).

The checked-in test compose mounts the shared proof keyring read-only in
Warehouse API and configures Sales' internal Cikada user service address.
Warehouse API disables automatic key generation; it consumes the existing
Portal keyring without changing key ownership or Portal login behavior.

A read-only adapter run as existing `OP046001` used its actual
`UKS` values0,1,6,7,9,11. It verified permitted reads, independent scoped count,
denied-order search/count, number lookup, editor, preview, cross-order reservation
add-back, payment references, bank search, reference lists and default-reference
masking. The production database was not mutated. Sixteen isolated adapter tests
also prove that a missing actor mapping fails before any physical SQL access.
