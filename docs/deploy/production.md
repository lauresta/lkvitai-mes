# Production deployment

Production is `https://cikada.lauresta.com`: the same path-based layout as the test
stand (`/portal`, `/warehouse`, `/sales`, `/shopfloor`, `/finance`, `/frontline`,
`/scan`, `/mcp`, `/manuals`).

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

## User manuals (`/manuals/`)

The MkDocs manuals (`docs/manuals`) are served on both stands under the app host:
`https://cikada.lauresta.com/manuals/` and `https://cikada-test.lauresta.com/manuals/`.

- The static site is an nginx container (`lkvitai-mes-manuals`, image `lkvitai-mes-manuals`) with no
  Traefik route and no public port. The Portal WebUI streams it under `/manuals/{**path}` after checking
  the shared portal cookie (`Manuals__BaseUrl`), because the pages contain screenshots of internal
  screens. Not signed in: redirected to the sign-in page; the search index answers with that redirect.
- Traefik: router `cikada-manuals` (prod) / `mes-test-manuals` (test) sends `PathPrefix(/manuals)` to the
  Portal WebUI. The route is installed on the VPS when the Traefik file is merged; until the Portal image
  with the endpoint is deployed it leads to the portal sign-in, not to an error page.
- Prod: the image is built by `release-prod.yml` with the release tag and started by `deploy-prod.yml`
  with the rest of the stack. Test: `build-and-push-manuals.yml` rebuilds it when `docs/manuals` changes
  and `deploy-test-manuals.yml` replaces the container (tag `MANUALS_IMAGE_TAG`, default `latest`).
- The MCP server returns links from `MANUALS_BASE_URL` (`https://<host>/manuals`) and the portal search
  box reads `/manuals/search/search_index.json` in the browser (group "Manuals" / "Instrukcijos").
- `mkdocs.yml` has no `site_url`: the theme emits relative links, so one image serves both hosts.

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
| `SENTRY_DSN` | variable | DSN of Sentry project `cikada-prod` (public, it reaches the browser); empty = Sentry off |
| `SENTRY_REPLAY_SESSION_SAMPLE_RATE` | variable | Share of browser sessions recorded by Session Replay, default `0.1` |
| `SENTRY_REPLAY_ON_ERROR_SAMPLE_RATE` | variable | Share of sessions with an error whose replay is kept, default `1.0` |
| `SENTRY_TRACES_SAMPLE_RATE` | variable | Performance tracing (server and browser), default `0` = off |
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
| `CIKADA_MCP_WAREHOUSE_ENTRY` | off | Warehouse write tools of the assistant (MCP): locations, purchase documents, receiving, putaway, transfers |

Bank payment import in Finance is open to the logins listed in
`Finance__AllowedLegacyLogins` in `docker-compose.prod.yml`. Import and deletion work
only where `Finance__BankPayments__WritesEnabled` is `true` (set in
`docker-compose.prod.yml`). The test stand leaves it off: these writes go to the live
legacy database and cannot be limited to the test branch. Parsing and preview work everywhere.

Agnum is not connected on production. The Agnum key secrets stay unset until it is.
Warehouse API does not start without an Agnum address, so `AGNUM__API__BASEURL`
defaults to a placeholder that never resolves (`http://agnum-not-connected.invalid:8181`).

The deploy job runs on the repository runner `lkvitai-apps-01` (label `lkvitai-apps`,
`/opt/actions-runner` on `lkvitai-apps`). If a deploy stays queued, check that the
runner is still listed under repository Settings - Actions - Runners; GitHub deletes
the registration of a runner that was offline for a long time.

## First-time setup of an empty database

Nothing below is created by the deploy.

1. **First administrator.** Signing in through SSO does not create an account. Sales,
   Finance, the live Shopfloor screens and user administration need an active row in
   `public.local_user_accounts` whose `Username` equals the authentik username; its
   `LegacySqlLogin` is the SQL Server login the user works under in Access. The first
   row (role `WarehouseAdmin`) has to be inserted in SQL; after that, accounts are
   managed in Warehouse - `/warehouse/admin/users`.
2. **Finance.** Bank payment import is additionally limited to the logins in
   `Finance__AllowedLegacyLogins` (`docker-compose.prod.yml`).
3. **Warehouse.** Create the physical warehouse whose code equals `WAREHOUSE_CODE`
   (default `WH1`), active and not virtual, in `/warehouse/admin/warehouses`. Until
   then Warehouse API logs a warning at startup and stock operations find no master data.
4. **Shopfloor.** Press *Sync legacy* on `/shopfloor/map/product-types` once; work
   centers, stations and workflows are entered in the Shopfloor reference screens.
   The live queue and task screens read the legacy database and need none of this.
5. **Agnum.** Not connected: import is switched off for both rows of
   `public.agnum_warehouse_mappings` (`IsImportEnabled = false`). Switch it back on
   when Agnum is connected. Items, suppliers and price groups are entered in the
   Warehouse admin screens or imported from Excel until then.

## On the servers

- `lkvitai-apps`: `/opt/lkvitai-mes/auth-keys` (sign-in key ring, created by the
  deploy; losing it signs everybody out), `/opt/lkvitai-mes/logs`,
  `/opt/lkvitai-mes/models/item-image-model.onnx` (copied by hand, needed for search
  by photo).
- `lkvitai-db`: the `vector` extension must be installed and created in `lkvitai`
  for search by photo. The application signs in as `lkvitai_app`, which owns the
  database and is not a superuser. `/etc/cron.d/lkvitai-pg-dump` writes a daily dump
  to `/var/backups/postgresql` (kept 14 days), in addition to the nightly VM backup.
