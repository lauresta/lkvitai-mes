# Production deployment

Production is `https://cikada.lauresta.com`: the same path-based layout as the test
stand (`/portal`, `/warehouse`, `/sales`, `/shopfloor`, `/finance`, `/frontline`,
`/scan`, `/mcp`).

| Piece | Where |
| --- | --- |
| Containers | VM `lkvitai-apps`, `docker-compose.prod.yml`, runner label `lkvitai-apps` |
| PostgreSQL | VM `lkvitai-db`, database `lkvitai` (Warehouse, Portal, Shopfloor) |
| Legacy database | SQL Server `lkvitai-mssql` - the same database daily work runs on |
| Routing | Traefik on the VPS, `deploy/traefik/dynamic/cikada.yml` |
| Sign-in | authentik application `cikada` at `sso.lauresta.com`; roles come from `cikada-*` groups |

## How a deployment happens

1. Publish a GitHub release with a version tag (`v0.2.0`). `release-prod.yml` builds
   all images with that tag and calls `deploy-prod.yml`.
2. `deploy-prod.yml` checks the configuration, applies Warehouse and Portal database
   migrations, starts the stack and waits until every container is healthy.

To redeploy or roll back, run **Deploy to Production** by hand with an existing tag:
a release tag or the full commit SHA of a `main` build. Database migrations are not
rolled back by this.

Nothing is deployed to production on push to `main`. The Traefik files are the
exception: they are installed on the VPS on every push that changes them.

## GitHub Environment `prod`

Required - the deploy stops if one is missing:

| Name | Kind | What |
| --- | --- | --- |
| `WAREHOUSE_DB_CONNECTION` | secret | PostgreSQL connection string for `lkvitai` |
| `LKVITAI_MSACCESS_DB_CONNECTION` | secret | SQL Server connection string of the legacy database |
| `CIKADA_OIDC_CLIENT_SECRET` | secret | Client secret of authentik application `cikada` |
| `CIKADA_OIDC_CLIENT_ID` | variable | Client id of authentik application `cikada` |
| `CIKADA_MCP_CLIENT_ID` | variable | Client id of authentik application `cikada-mcp` |
| `LKVITAI_LEGACY_DB_GUID` | variable | `database_guid` of the legacy database (same value as in `test`) |

Optional:

| Name | Kind | What |
| --- | --- | --- |
| `PORTAL_DB_CONNECTION`, `SHOPFLOOR_DB_CONNECTION` | secret | Only if these modules get their own database; default is `WAREHOUSE_DB_CONNECTION` |
| `ITEMIMAGES__ENDPOINT`, `ITEMIMAGES__BUCKET`, `ITEMIMAGES__USESSL` | variable | Object storage for item photos; use a production bucket, not the test one |
| `ITEMIMAGES__ACCESSKEY`, `ITEMIMAGES__SECRETKEY` | secret | Keys for that storage |
| `WAREHOUSE_CODE` | variable | Code of the physical warehouse, default `WH1` |
| `CLARITY_PROJECT_ID` | variable | Microsoft Clarity project for production |
| `AUTH_KEYS_DIR`, `LOGS_DIR`, `CLIP_MODEL_DIR` | variable | Host folders, default under `/opt/lkvitai-mes` |

Switches for writing into the legacy database - all off unless set to `true`:

| Variable | Turns on |
| --- | --- |
| `SALES_ORDER_ENTRY_ENABLED` | Creating and editing orders in Sales |
| `SALES_ORDER_ENTRY_PAYMENTS_ENABLED` | Registering payments in Sales |
| `SALES_PRODUCTION_CONFIRMATION_ENABLED` + `SALES_PRODUCTION_DOCUMENT_PREFIX` | Confirming orders into production |
| `SHOPFLOOR_TASK_WRITE_ENABLED` | Operators changing task state in Shopfloor |
| `CIKADA_MCP_ORDER_ENTRY` | Order entry tools of the assistant (MCP) |

Before turning order entry on, replace the allowed series, payment methods and
employee lists in `docker-compose.prod.yml` (`Sales__OrderEntry__Allowed*`): they are
still the test-only values from the test stand. The Finance allow-list
(`Finance__AllowedLegacyLogins`) is in the same file.

Agnum is not connected on production. `AGNUM__API__BASEURL` and the Agnum key secrets
stay unset until it is.

## On the servers

- `lkvitai-apps`: `/opt/lkvitai-mes/auth-keys` (sign-in key ring, created by the
  deploy; losing it signs everybody out), `/opt/lkvitai-mes/logs`,
  `/opt/lkvitai-mes/models/item-image-model.onnx` (copied by hand, needed for search
  by photo).
- `lkvitai-db`: the `vector` extension must be installed and created in `lkvitai`
  for search by photo.
