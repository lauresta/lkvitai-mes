# Alerting Runbook

## Alert Classes

- API downtime (`/health` non-200)
- Elevated API errors (`5xx` spike)
- Projection lag unhealthy
- Repeated background job failures

## Triage Flow

1. Confirm incident window and affected environment.
2. Check `/health` and `/api/warehouse/v1/health`.
3. Inspect latest trace IDs and correlated logs.
4. Validate DB connectivity and queue status.

## Escalation

- P1: API down for all users -> page on-call immediately.
- P2: Single workflow degraded -> create incident, escalate within 30 minutes.
- P3: Intermittent issues -> backlog and monitor trend.

## Recovery Steps

- Restart impacted service if safe.
- Re-run failed batch jobs manually when idempotent.
- Validate key routes:
  - `/warehouse/outbound/orders`
  - `/warehouse/waves`
  - `/analytics/fulfillment`

## Post-Incident

- Record root cause, impacted workflows, and preventive action.
