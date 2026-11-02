# Durable Cikada users and explicit legacy login mapping

Warehouse API now uses scoped `DatabaseAdminUserStore` with the existing Warehouse
PostgreSQL connection. The new `public.local_user_accounts` table stores stable
identity GUIDs, normalized unique usernames, email, Identity password hashes,
roles, status, optional `LegacySqlLogin`, and creation/update timestamps. The
in-memory implementation remains a test fixture; application restart does not
create a new default admin identity. Password hashes never appear in user DTOs.

An administrator can explicitly set or clear Legacy SQL login in Admin Users.
No username-to-SQL-login inference is performed. Empty mapping denies legacy
access. `TryGetLegacySqlLogin` returns a mapping only for an active account.
The internal authenticated resolver is separate from user administration; the
store does not add a public lookup endpoint. User administration requires both
the existing Warehouse AdminOnly policy and a short-lived, purpose-bound server
proof matching the actual Portal subject and its current persisted active
administrator role. Unsigned legacy Warehouse role headers alone are rejected.

Updates include `UpdateLegacySqlLogin=true` when the administrator is editing
this field. Omitting that intent preserves the mapping during unrelated role,
status or email updates. With intent set, null/empty removes the mapping. The
field accepts explicit login names up to128 characters with letters/digits,
backslash, dot, dash, underscore, @ or $. SQL impersonation additionally requires
server-side principal validation; assigning a display value is not authentication.

## Deployment order

1. Privately capture all current in-memory users before restarting Warehouse API.
   Preserve every existing GUID, roles, status, email and creation timestamp.
2. Review/apply `scripts/sales/004-durable-local-users.postgresql.sql` to Warehouse
   PostgreSQL. It requires the expected EF baseline, adds one empty table and
   matching EF migration-history entry, and refuses an untracked existing table.
3. Privately import the captured identities and compatible Identity password
   hashes. Preserve original passwords; never put passwords/hashes into source,
   logs or migration files. Normalize usernames with .NET ToUpperInvariant.
4. Assign only the owner-approved mappings and create the authorized additional
   account privately. No passwords or live identity GUIDs are seeded in this repo.
5. Deploy/restart Warehouse API and verify the same account IDs, roles, password
   validation and mapping after another restart. Existing Test/Development
   startup EF migration behavior is unchanged; preapplying the reviewed migration
   avoids schema changes at restart.

The database must be backed up with the rest of Warehouse PostgreSQL. Application
rollback must retain this table and imported identities; do not drop it merely
to revert application code. The EF Down migration is a destructive explicit
schema rollback, not a routine deployment action.

Focused tests cover a new context/store retaining identity/hash/mapping,
compatibility with pre-existing hashes, no automatic default-admin reseed,
case-insensitive username uniqueness, active-only resolution, explicit mapping
edit/clear, invalid login rejection and unchanged AdminOnly controller policy.
