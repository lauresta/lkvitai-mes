# Monitoring Runbook

## Procedure 1: Dashboard Health Check
1. Open API latency/error dashboard.
2. Confirm p95 latency < 500ms and 5xx rate < 1%.
3. Confirm projection lag and queue depth are within thresholds.
4. Save a timestamped dashboard snapshot for release evidence.

## Procedure 2: Alert Triage
1. Acknowledge alert in monitoring tool.
2. Correlate with deployment timeline and recent changes.
3. Determine severity using `incident-response/README.md`.
4. Escalate if unresolved by SLA timer.

## Procedure 3: Capacity Monitoring
1. Review DB growth and event throughput trends.
2. Check storage and connection pool headroom.
3. Compare forecast against warning thresholds.
4. Create capacity action item if warning threshold is breached.

## Procedure 4: SLO Monthly Review
1. Export SLO metrics for the last 30 days.
2. Validate uptime and API latency targets.
3. Identify recurring breach patterns and owners.
4. Publish monthly SLO summary to operations channel.
