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

Writing into the legacy database. Production works on the same database as Access,
in every branch the signed-in user may use there; the test stand is limited to the
TESTAVIMAS branch (series XXXX, XXX1, RY-TEST, RY01) by default in code. Each kind of
write is on unless its variable is set to `false`:

| Variable | Default | Controls |
| --- | --- | --- |
| `SALES_ORDER_ENTRY_ENABLED` | `true` | Creating and editing orders in Sales |
| `SALES_ORDER_ENTRY_PAYMENTS_ENABLED` | `true` | Registering payments in Sales |
| `SALES_PRODUCTION_CONFIRMATION_ENABLED` | `true` | Confirming orders into production |
| `SALES_PRODUCTION_DOCUMENT_PREFIX` | `MES` | Prefix of production documents created by Cikada (test stand: `MEST`) |
| `SHOPFLOOR_TASK_WRITE_ENABLED` | `true` | Issuing items into production tasks (GU) in Shopfloor |
| `SALES_ORDER_ENTRY_ALL_BRANCHES`, `SHOPFLOOR_TASK_WRITE_ALL_BRANCHES` | `true` | `false` narrows production to the TESTAVIMAS branch, as on the test stand |
| `CIKADA_MCP_ORDER_ENTRY` | off | Order entry tools of the assistant (MCP) |

Bank payment import in Finance is open to the logins listed in
`Finance__AllowedLegacyLogins` in `docker-compose.prod.yml`.

Agnum is not connected on production. The Agnum key secrets stay unset until it is.
Warehouse API does not start without an Agnum address, so `AGNUM__API__BASEURL`
defaults to a placeholder that never resolves (`http://agnum-not-connected.invalid:8181`).

The deploy job runs on the repository runner `lkvitai-apps-01` (label `lkvitai-apps`,
`/opt/actions-runner` on `lkvitai-apps`). If a deploy stays queued, check that the
runner is still listed under repository Settings - Actions - Runners; GitHub deletes
the registration of a runner that was offline for a long time.

## On the servers

- `lkvitai-apps`: `/opt/lkvitai-mes/auth-keys` (sign-in key ring, created by the
  deploy; losing it signs everybody out), `/opt/lkvitai-mes/logs`,
  `/opt/lkvitai-mes/models/item-image-model.onnx` (copied by hand, needed for search
  by photo).
- `lkvitai-db`: the `vector` extension must be installed and created in `lkvitai`
  for search by photo.
