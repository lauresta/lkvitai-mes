# Production Runbook

This runbook defines production operating procedures for LKvitai MES.

## Sections
- `deployment/` - release, rollback, migration, and feature rollout procedures
- `monitoring/` - dashboards, alert response, and SLO checks
- `troubleshooting/` - common symptoms and deterministic diagnosis steps
- `incident-response/` - severity classification, escalation, and communication flow
- `disaster-recovery/` - backup restore and failover operations
- `maintenance/` - recurring operational maintenance

## Procedure Count
- Deployment: 4
- Monitoring: 4
- Troubleshooting: 4
- Incident Response: 4
- Disaster Recovery: 4
- Maintenance: 4
- Total: 24

## Related Docs
- `docs/deployment/blue-green-deployment.md`
- `docs/deployment/canary-releases.md`
- `docs/deployment/database-migrations.md`
- `docs/deployment/feature-flags.md`
- `docs/operations/failover-runbook.md`
- `docs/operations/rollback-runbook.md`
- `docs/operations/migration-runbook.md`

## Validation Notes
- Procedure commands were validated for existence against repository scripts.
- Environment-dependent runtime execution (staging infra, Prometheus, PagerDuty, DB replication) must be run in staging before production go-live.
