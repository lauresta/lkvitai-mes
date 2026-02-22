# Deployment Runbook

## Procedure 1: Blue-Green Deploy
1. Deploy green stack: `./scripts/blue-green/deploy-green.sh <version>`.
2. Run smoke checks against green: `./scripts/smoke-tests.sh http://localhost:5001`.
3. Switch traffic: `./scripts/blue-green/switch-to-green.sh`.
4. Re-run smoke checks on active endpoint.

## Procedure 2: Blue-Green Rollback
1. Trigger rollback: `./scripts/blue-green/rollback-to-blue.sh`.
2. Verify active version on blue endpoint.
3. Run smoke checks.
4. Open incident ticket with root cause and timestamps.

## Procedure 3: Canary Release
1. Start canary rollout: `./scripts/canary/deploy-canary.sh <version> 10`.
2. Monitor canary metrics for at least 5 minutes.
3. Progress rollout: `./scripts/canary/progress-canary.sh 50`, then `100` if healthy.
4. If unhealthy, run `./scripts/canary/rollback-canary.sh` immediately.

## Procedure 4: Database Migration During Release
1. Review migration impact and rollback step in `docs/operations/migration-runbook.md`.
2. Execute migration using deployment pipeline.
3. Validate key endpoints and health checks.
4. Record migration duration and lock observations in release notes.
