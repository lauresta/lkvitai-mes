# Deployment Guide

## Scope

Deployment baseline for LKvitai.MES API + Blazor WebUI in Phase 1.5.

## Pre-Deployment Checklist

- `dotnet build src/LKvitai.MES.sln` passes.
- `dotnet test src/LKvitai.MES.sln` passes.
- Connection strings are set for API and background jobs.
- `ASPNETCORE_ENVIRONMENT=Production` in production.
- `/api/auth/dev-token` is unreachable in production.
- Database migrations are applied.

## API Deployment

```bash
cd src
dotnet publish LKvitai.MES.Api/LKvitai.MES.Api.csproj -c Release -o ../artifacts/api
```

Run with:

```bash
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__WarehouseDb="<connection-string>" \
DOTNET_URLS="http://0.0.0.0:5000" \
dotnet LKvitai.MES.Api.dll
```

## WebUI Deployment

```bash
cd src
dotnet publish LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj -c Release -o ../artifacts/webui
```

Run with:

```bash
ASPNETCORE_ENVIRONMENT=Production \
WarehouseApi__BaseUrl="https://api-host" \
dotnet LKvitai.MES.WebUI.dll
```

## Post-Deployment Smoke Checks

- `GET /health` returns `200`.
- `GET /api/warehouse/v1/health` returns `200` with auth.
- `GET /api/warehouse/v1/waves` returns `200/401/403` (expected by auth context).
- Blazor routes load:
  - `/warehouse/waves`
  - `/warehouse/cross-dock`
  - `/warehouse/rmas`
  - `/analytics/fulfillment`
  - `/analytics/quality`

## Rollback

- Stop current process.
- Start previous known-good artifact set.
- Verify `/health` and outbound order list route.
