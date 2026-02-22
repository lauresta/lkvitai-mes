# API Documentation

## Swagger

- Development URL: `/swagger`
- OpenAPI metadata is generated from ASP.NET controllers.

## New Phase 1.5 Endpoints

- `POST /api/auth/dev-token` (development only)
- `GET|POST /api/warehouse/v1/waves`
- `POST /api/warehouse/v1/waves/{id}/assign`
- `POST /api/warehouse/v1/waves/{id}/start`
- `POST /api/warehouse/v1/waves/{id}/complete-lines`
- `GET|POST /api/warehouse/v1/cross-dock`
- `POST /api/warehouse/v1/cross-dock/{id}/status`
- `POST|GET /api/warehouse/v1/rmas`
- `POST /api/warehouse/v1/rmas/{id}/receive`
- `POST /api/warehouse/v1/rmas/{id}/inspect`
- `POST /api/warehouse/v1/handling-units/{parentHuId}/split`
- `POST /api/warehouse/v1/handling-units/{parentHuId}/merge`
- `GET /api/warehouse/v1/handling-units/{huId}/hierarchy`
- `POST|GET /api/warehouse/v1/serials`
- `POST /api/warehouse/v1/serials/{id}/status`
- `GET /api/warehouse/v1/analytics/fulfillment-kpis`
- `GET /api/warehouse/v1/analytics/qc-late-shipments`

## Auth Notes

- Warehouse auth uses header or bearer compatibility token.
- In development, generate token via `docs/dev-auth-guide.md`.
