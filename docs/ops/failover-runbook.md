# Failover Runbook

## Purpose
Operational runbook for PRD-1653 failover validation across database and API layers.

## Preconditions
- Primary and standby PostgreSQL containers are running.
- API instances are behind a load balancer.
- Auth token available for protected API checks.

## Automated Database Failover
```bash
scripts/failover/promote-standby.sh
```

Environment variables:
- `PRIMARY_CONTAINER` (default `postgres-primary`)
- `STANDBY_CONTAINER` (default `postgres-standby`)
- `API_HEALTH_URL` (default `http://localhost:5000/health`)
- `TIMEOUT_SECONDS` (default `240`)

## Manual Validation Steps
1. Stop primary DB (`docker-compose stop postgres-primary`).
2. Promote standby (`docker exec postgres-standby pg_ctl promote`).
3. Verify API recovers (`curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items`).
4. Measure RTO from DB stop to first successful API 200 response.
5. Verify RPO by checking no unexpected row loss in critical tables.
6. Stop one API node (`docker-compose stop api-1`) and verify continued success through LB.
7. Run integrity checks (foreign keys, projection checksums, event-store consistency).

## Recovery Targets
- RTO: `< 4 hours` (target `< 2 minutes`)
- RPO: `< 1 hour` (target `0` under synchronous replication)

## Rollback
1. Restart original primary DB node.
2. Reconfigure replication topology (new primary/new standby).
3. Confirm API connection strings and health checks.
