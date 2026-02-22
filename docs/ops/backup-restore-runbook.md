# Backup and Restore Runbook (PRD-1639)

## Targets
- RTO: < 2 hours
- RPO: < 1 hour

## Daily backup
- Hangfire job: `backup-daily-2am` (02:00 UTC)
- Manual API: `POST /api/warehouse/v1/admin/backups/trigger`
- Script: `scripts/backup/pg_dump_backup.sh`

## Backup retention policy
- Daily backups: 90 days
- Weekly backups: 365 days
- Retention enforcement should run in object storage lifecycle rules or scheduled cleanup job.

## Restore
- Manual API: `POST /api/warehouse/v1/admin/backups/restore`
- Script: `scripts/backup/restore_from_backup.sh <backup.sql.gz>`
- Evidence log artifacts are written to `artifacts/restores/`.

## Monthly restore test
- Hangfire job: `backup-monthly-restore-test` (1st day of month, 03:00 UTC)
- Flow: restore latest completed backup into test target, emit audit events, retain restore evidence logs.

## WAL archiving
- Baseline config in `scripts/backup/wal-archiving.conf`
- Apply equivalent settings in PostgreSQL `postgresql.conf` and ensure archive destination durability.
