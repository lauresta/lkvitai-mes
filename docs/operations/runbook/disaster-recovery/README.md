# Disaster Recovery Runbook

## Procedure 1: Database Restore
1. Identify latest valid backup artifact.
2. Restore using `./scripts/backup/restore_from_backup.sh <backup-file> <target-db>`.
3. Run integrity and schema checks.
4. Reconnect application after validation.

## Procedure 2: Primary/Standby Failover
1. Promote standby: `./scripts/failover/promote-standby.sh`.
2. Redirect traffic to healthy region/instance.
3. Validate write/read flow and projection freshness.
4. Record failover start/end for RTO reporting.

## Procedure 3: Regional Failover DNS Switch
1. Validate secondary stack health.
2. Execute DNS failover: `./scripts/disaster-recovery/switch_dns_failover.sh`.
3. Run verification: `./scripts/disaster-recovery/verify_services.sh`.
4. Notify stakeholders of cutover completion.

## Procedure 4: Failback After Recovery
1. Confirm primary recovery and data consistency.
2. Plan maintenance window and failback sequence.
3. Execute controlled failback and verify service health.
4. Update DR drill evidence and lessons learned.
