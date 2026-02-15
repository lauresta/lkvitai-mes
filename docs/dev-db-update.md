# Dev Database Update Procedure

## Overview

The project uses two DB mechanisms:
1. **EF Core migrations** — state-based tables in `public` schema
2. **Marten auto-schema** — event store tables in `warehouse_events` schema (created automatically on first app startup)

## Connection Strings

| Environment | Host | Database | Config file |
|-------------|------|----------|-------------|
| Development (local dotnet run) | `lkvitai-test.vpn.lauresta.com:5432` | `lkvitai_warehouse_dev` | `appsettings.Development.json` |
| Test (Docker on lkvitai-test) | `pg:5432` (docker service name) | `lkvitai_warehouse_test` | `appsettings.json` (base) |

## Apply EF Core Migrations to DEV

```bash
ASPNETCORE_ENVIRONMENT=Development \
  dotnet ef database update \
  --project src/LKvitai.MES.Infrastructure \
  --startup-project src/LKvitai.MES.Api
```

This reads the connection string from `appsettings.Development.json` and applies all pending EF Core migrations to `lkvitai_warehouse_dev`.

## Verify Migration State

```bash
# List applied migrations
docker run --rm -e PGPASSWORD=postgres postgres:16-alpine \
  psql -h lkvitai-test.vpn.lauresta.com -U postgres -d lkvitai_warehouse_dev \
  -c 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'

# List tables
docker run --rm -e PGPASSWORD=postgres postgres:16-alpine \
  psql -h lkvitai-test.vpn.lauresta.com -U postgres -d lkvitai_warehouse_dev \
  -c '\dt public.*'
```

## Marten Schema

Marten `warehouse_events` schema objects are created automatically when the API starts. No manual step needed — just run the app with `ASPNETCORE_ENVIRONMENT=Development`.

## Initial Setup (2025-02-15)

- 25 EF Core migrations applied to `lkvitai_warehouse_dev`
- 54 tables created in `public` schema
- `lkvitai_warehouse_test` was NOT modified (verified empty)
