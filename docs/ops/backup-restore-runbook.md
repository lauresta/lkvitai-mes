# Backup and Restore Runbook (PRD-1639)

## Targets
- RTO: < 2 hours
- RPO: < 1 hour

## Daily backup (what actually runs in production)
- `pg_dump` of database `lkvitai` on the database host (lkvitai-db): `/usr/local/sbin/lkvitai-pg-dump.sh`
  from `/etc/cron.d/lkvitai-pg-dump`, every day 20:30 Europe/Vilnius, into `/var/backups/postgresql`, kept 14 days.
- Proxmox Backup Server snapshot of all VMs every night at 21:00, kept 5 days.
- The Warehouse API endpoints below only write placeholder files inside the container; they are not backups.
  Their Hangfire schedules (`backup-daily-2am`, `backup-monthly-restore-test`, `dr-quarterly-drill`) were
  removed because they failed every night on the read-only container filesystem.
- Manual API (placeholder): `POST /api/warehouse/v1/admin/backups/trigger`
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
- No scheduled job (the `backup-monthly-restore-test` placeholder schedule was removed).
- Do it by hand: restore the latest `/var/backups/postgresql/lkvitai_*.dump` with `pg_restore` into a scratch database.

## WAL archiving
- Baseline config in `scripts/backup/wal-archiving.conf`
- Apply equivalent settings in PostgreSQL `postgresql.conf` and ensure archive destination durability.
