# Migration Runbook

## Purpose
Operational runbook for PRD-1654 migration validation and rollback safety.

## Preconditions
- Database backup taken before migration.
- Application health endpoint monitored during rollout.
- Rollback target migration identified.

## Forward Migration
```bash
cd src/LKvitai.MES.Infrastructure
dotnet ef database update
```

## Rollback
```bash
cd src/LKvitai.MES.Infrastructure
dotnet ef database update <PreviousMigrationName>
```

## Integrity Checklist
1. Verify schema updated as expected (`\d` checks with psql).
2. Validate foreign keys and unique indexes remain valid.
3. Confirm API health stays green during and after migration.
4. Verify projection rebuild status if schema affects read models.

## Zero-Downtime Guidance
- Use additive changes first (nullable columns, new tables).
- Use backward-compatible deployments before destructive drops.
- For large index creation, prefer `CREATE INDEX CONCURRENTLY` in SQL migration scripts.

## Recovery Decision
- If API error rate rises or migration exceeds SLO, execute rollback immediately.
- Record migration start/end timestamps to track `< 5 minute` target.
