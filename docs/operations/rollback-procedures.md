# Rollback Procedures

## Version Pinning
- API image tags are controlled with `IMAGE_TAG` in `docker-compose.test.yml`.
- Rollback scripts require explicit version or migration arguments.
- Full rollback combines API and database rollbacks with smoke checks.

## Commands
```bash
./scripts/rollback/rollback-api.sh <api-version-tag>
./scripts/rollback/rollback-database.sh <target-migration-name>
./scripts/rollback/rollback-full.sh <api-version-tag> <target-migration-name>
```

## Timing Targets
- API rollback: < 5 minutes
- Database rollback: < 2 minutes
- Full rollback: < 10 minutes

## Post-Rollback Validation
1. Health endpoint returns `200`.
2. Smoke tests pass.
3. No migration/version mismatch in logs.
4. No integrity violations detected.
