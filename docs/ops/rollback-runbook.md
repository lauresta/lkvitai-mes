# Rollback Runbook

## Rollback Criteria
Trigger rollback when any of the following applies:
- Error rate exceeds 5% for 5 minutes
- Critical functional bug in production workflow
- Data corruption or failed integrity checks

## API Rollback
```bash
./scripts/rollback/rollback-api.sh v1.2.3
```

Validation:
1. `curl -I http://localhost:5001/health`
2. Confirm API health is `200`.
3. Run `./scripts/smoke-tests.sh`.

## Database Rollback
```bash
./scripts/rollback/rollback-database.sh 20260211_PreviousMigration
```

Validation:
1. `dotnet ef migrations list` from infrastructure project.
2. Confirm target migration is current.
3. Run integrity checks (FK/unique constraints).

## Full Rollback
```bash
./scripts/rollback/rollback-full.sh v1.2.3 20260211_PreviousMigration
```

Validation:
1. API health endpoint is healthy.
2. Smoke tests pass.
3. Error rate < 1% for post-rollback observation window.
