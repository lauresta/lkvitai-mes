# Explicit Sales database operations

These are manually reviewed scripts, not automatic application or CI migrations.
Do not execute this directory as a sequence: the owner deliberately deferred002.

| Script | Current authorization |
| --- | --- |
|001-command-receipts.sql | Applied technical receipt schema, including RelatedEntityId/WarningsJson; no legacy business table changes |
|002-cancellation-audit-bigint.sql | **Deferred by owner. Do not execute.** Cancellation remains independently disabled |
|003-sa-work-period-2030.sql | Applied after owner approved both sa end date2030-01-31 and its normal administrative notice. Only the end date changed; original trigger/audit retained. The initial unreviewed-trigger guard had stopped before UPDATE |
|004-durable-local-users.postgresql.sql | Applied to Cikada Warehouse PostgreSQL for the authorized user mapping. Adds only durable local-user storage; existing admin identity/roles/password behavior were privately preserved before restart, then the approved bykovas account was created. This is not a legacy SQL Server script |

Current decisions and remaining blockers are tracked in
[the Sales issue index](../../docs/sales/milestone-2-issues.md).
