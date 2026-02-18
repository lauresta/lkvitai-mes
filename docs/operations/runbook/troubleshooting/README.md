# Troubleshooting Runbook

## Procedure 1: High API Response Time
1. Verify p95/p99 latency and identify impacted endpoints.
2. Check database query latency and connection pool pressure.
3. Check cache hit rate and downstream service latency.
4. Mitigate by scaling, cache warm-up, or rollback as needed.

## Procedure 2: Elevated 5xx Error Rate
1. Inspect application logs for exception spikes.
2. Correlate error signatures with recent deploy/version.
3. Validate dependencies (DB, Redis, RabbitMQ, external APIs).
4. Trigger rollback/canary rollback if breach thresholds persist.

## Procedure 3: Queue Backlog or Stuck Processing
1. Check queue depth and consumer health.
2. Restart failed consumers/services if safe.
3. Reprocess dead-letter messages with idempotent handlers.
4. Verify backlog drain and projection freshness.

## Procedure 4: Feature Regression in New Capability
1. Disable affected feature using feature flag.
2. Confirm API behavior returns to expected baseline.
3. Capture impacted users and request samples.
4. Escalate to engineering with rollback recommendation.
