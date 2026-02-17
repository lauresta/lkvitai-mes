# Disaster Recovery Runbook (PRD-1640)

## Objectives
- RTO target: `< 4 hours`
- RPO target: `< 1 hour`

## Disaster Scenarios
- Data center outage
- Database corruption
- Ransomware event

## Roles and Access
- DR actions require Warehouse Admin API role (`Warehouse.AdminOnly` policy).
- Operationally, DevOps is expected to execute scripts and infrastructure cutover.

## Drill API
- Trigger drill: `POST /api/warehouse/v1/admin/dr/drill`
- Drill history: `GET /api/warehouse/v1/admin/dr/drills`
- Quarterly schedule: Hangfire job `dr-quarterly-drill` with cron `0 4 1 1,4,7,10 *` (UTC)

## Step-by-Step Failover
1. Restore latest valid backup:
   - Script: `scripts/disaster-recovery/restore_failover.sh <backup.sql.gz> [target_db]`
2. Switch DNS to DR endpoint:
   - Script: `scripts/disaster-recovery/switch_dns_failover.sh [primary_record] [dr_record]`
3. Verify application and API services:
   - Script: `scripts/disaster-recovery/verify_services.sh [base_url]`

## Communication Plan
- Use template: `docs/prod-ready/disaster-recovery-communication-template.md`
- Notify: engineering leadership, operations, support, and impacted stakeholders.
- Update cadence during active event: every 30 minutes or at each milestone.

## Evidence and Artifacts
- Drill artifacts: `artifacts/dr-drills/<drillId>/`
- Per-step logs:
  - `step1-restore.log`
  - `step2-dns-switch.log`
  - `step3-verify-services.log`
- Notification snapshot:
  - `devops-notification.txt`

## Expected Drill Outcome
- All three steps executed.
- Actual RTO measured and persisted on `dr_drills`.
- Issues captured (for example: `DNS switch automation failed`).
- Follow-up actions tracked after drill completion.
