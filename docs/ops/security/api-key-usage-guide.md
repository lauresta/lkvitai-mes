# API Key Usage Guide

This guide documents API key management and usage for LKvitai.MES (`PRD-1628`).

## Endpoints

Admin-only management endpoints:

- `GET /api/warehouse/v1/admin/api-keys`
- `POST /api/warehouse/v1/admin/api-keys`
- `PUT /api/warehouse/v1/admin/api-keys/{id}/rotate`
- `DELETE /api/warehouse/v1/admin/api-keys/{id}`

## Create API Key

```http
POST /api/warehouse/v1/admin/api-keys
Content-Type: application/json
Authorization: Bearer <admin-token>

{
  "name": "ERP Integration",
  "scopes": ["read:items", "write:orders"],
  "rateLimitPerMinute": 100
}
```

Response includes `plainKey` exactly once.

## Supported Scopes

- `read:items`
- `write:orders`
- `read:stock`

## Use API Key

Send key in header:

```http
X-API-Key: wh_xxx...
```

Examples:

- `GET /api/warehouse/v1/items` requires `read:items`
- `POST /api/warehouse/v1/orders` or `POST /api/warehouse/v1/sales-orders` requires `write:orders`
- `GET /api/warehouse/v1/stock` requires `read:stock`

If scope is missing, API returns `403` with detail:

`Insufficient scope: <scope> required`

## Rotation

Rotate endpoint returns a new `plainKey` and starts a 7-day grace period for the previous key.

## Security Notes

- API keys are generated from 32 random bytes and prefixed with `wh_`.
- Only SHA-256 hash is persisted for active and previous key values.
- Per-key rate limits are enforced by middleware (`RateLimitPerMinute`, default `100`).
- Key usage updates `LastUsedAt` and emits security logs.
