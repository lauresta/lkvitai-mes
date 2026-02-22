# Dev Auth Guide

This guide applies to local development only.

## Prerequisites

- Run API with `ASPNETCORE_ENVIRONMENT=Development`.
- Endpoint: `POST /api/auth/dev-token`.
- Seeded credentials from `src/LKvitai.MES.Api/appsettings.Development.json`:
  - `username`: `admin`
  - `password`: `Admin123!`

## Get Token

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

echo "$TOKEN"
```

Response schema:

```json
{
  "token": "admin-dev|Operator,QCInspector,...|<unix-expiry>",
  "expiresAt": "2026-02-13T12:34:56Z"
}
```

## Use Token

```bash
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/warehouse/v1/items
```

## Invalid Credentials

```bash
curl -i -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrong"}'
```

Expected: `401 Unauthorized`.

## Production Safety

- In `Production`, `/api/auth/dev-token` is not mapped.
- Startup warning appears in development logs:
  - `Dev auth enabled - DO NOT USE IN PRODUCTION`
