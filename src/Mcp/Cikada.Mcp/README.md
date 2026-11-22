# Cikada MCP (`@cikada/mcp`)

Remote MCP server that lets a Sales operator use Cikada.MES from ChatGPT or Claude: search orders, open an order, look up customers, items with prices and available stock, and (when enabled) enter and change orders.

- Endpoint (test): `https://cikada-test.lauresta.com/mcp` (Streamable HTTP, stateless, JSON responses)
- Sign-in: the operator's own `sso.lauresta.com` account (authentik application `cikada-mcp`, public client with PKCE; no secret anywhere)
- Every tool call runs as the signed-in user: the access token is forwarded unchanged to the Sales and Warehouse APIs, which validate it and apply the user's Cikada roles.

## Connect from ChatGPT

1. ChatGPT → Settings → Apps & Connectors → Advanced settings → turn on **Developer mode** (needed for custom MCP servers on Plus / Pro / Business; on Enterprise / Edu an admin publishes the connector).
2. Settings → Apps & Connectors → **Create** (or "Add custom connector").
3. Name `Cikada`, MCP server URL `https://cikada-test.lauresta.com/mcp`, Authentication **OAuth**. Leave client id / secret empty: ChatGPT registers itself through `/register` and gets the public client id.
4. Click Create, sign in on `sso.lauresta.com` when the popup asks, and approve.
5. In a chat, pick the Cikada connector (Developer mode tools menu) and ask, for example: "Show today's orders in Vilnius with debt".

## Connect from Claude

- claude.ai / Claude Desktop: Settings → Connectors → **Add custom connector**, URL `https://cikada-test.lauresta.com/mcp`, then **Connect** and sign in on `sso.lauresta.com`.
- Claude Code: `claude mcp add --transport http cikada https://cikada-test.lauresta.com/mcp`, then `/mcp` → cikada → Authenticate.

## Tools

Read (always on):

| Tool | API call(s) |
|---|---|
| `orders_search` | Sales `GET /api/sales/orders` |
| `order_get` | Sales `GET /api/sales/orders/{number}` |
| `orders_filters` | Sales `GET /api/sales/orders/filters` |
| `customers_search` | Warehouse `GET /api/warehouse/v1/customers` |
| `customer_get` | Warehouse `GET /api/warehouse/v1/customers/{id}` |
| `price_groups_list` | Warehouse `GET /api/warehouse/v1/price-groups` |
| `items_search` | Warehouse `GET /api/warehouse/v1/items`, `GET /api/warehouse/v1/stock/available`, `GET /api/warehouse/v1/price-groups/{id}/item-prices` |
| `stock_get` | Warehouse `GET /api/warehouse/v1/stock/available` |
| `whoami` | none: the signed-in user and roles from the token (explains a 403) |
| `bank_payments_list` | Finance `GET /api/finance/bank-payments`: individual live bank rows with NR, date, payer, full purpose and amount; LT/LV, inclusive date range, text search, sort and pages of 100. Read all pages for a complete list. |
| `bank_payments_preview` | Finance `POST /api/finance/bank-payments/preview`: validate extracted bank rows against the live legacy table, report exact and possible duplicates with matching NR and payer, errors, latest NR and projected NR range; no writes |

Bank statement import: attach an Excel, CSV, PDF, screenshot, MT940, CAMT.053 or other bank statement in ChatGPT and ask Cikada to preview it. ChatGPT reads the attachment with its available file or vision capabilities and passes the extracted rows to `bank_payments_preview`; the MCP server does not receive or decode the original binary file. Review the complete preview and approve the selected lines explicitly. `bank_payments_import` then writes only rows held in that server-side preview. Import fails if the preview expires (15 minutes) or another entry changes the latest NR. Unreadable fields stay blank and are marked as errors. Both tools require the operator's Finance access and legacy SQL mapping.

| Write tool | API call |
|---|---|
| `bank_payments_import` | Finance `POST /api/finance/bank-payments`: import explicitly approved preview lines after rechecking the current NR |

Management reports (live legacy Mango workbook reports; roles SalesAdmin or Reports, plus the caller's legacy SQL permissions):

| Tool | API call |
|---|---|
| `report_production` | Sales `GET /api/sales/reports/production` (products by state / type / series, per day, month or year) |
| `report_work` | Sales `GET /api/sales/reports/work` (accruals per duty / employee / branch) |
| `report_payments` | Sales `GET /api/sales/reports/payments` (money received by method / branch) |
| `report_dimensions` | Sales `GET /api/sales/reports/{report}/dimensions` |
| `production_queue` | Sales `GET /api/sales/reports/lists/pipeline` (current state × type, days in state) |
| `materials_popularity` | Sales `GET /api/sales/reports/lists/materials` |
| `stopped_products` | Sales `GET /api/sales/reports/lists/stopped` |
| `unpaid_orders` | Sales `GET /api/sales/reports/lists/unpaid-orders` |
| `made_not_delivered` | Sales `GET /api/sales/reports/lists/not-delivered` |
| `customer_debt` | Sales `GET /api/sales/reports/lists/customer-debt` |
| `orders_find` | Sales `GET /api/sales/reports/lists/order-search` |

User manuals (always on, no backend call, any signed-in user; the manuals are Lithuanian, see "User manuals index" below):

| Tool | Source |
|---|---|
| `docs_search` | In-process BM25 search over the manual chunks (diacritics folded, title / heading boost, light prefix match for Lithuanian endings); filter by `module`, `limit` 1-15 (default 6). Returns id, module, page title, heading path, status, url and a snippet per hit, plus the total |
| `docs_get` | The full text of one chunk (`id`) or of every chunk of a page in order (`page`), with module, status (`laikina` = stub), routes, roles, url and `images` (the screenshots the chunk shows) |
| `docs_image` | One manual screenshot (`img/<module>/<name>.png`) as MCP image content; with `marks` (up to 6 frames in percent of the image size) it draws a coloured frame per mark: red, blue, green, orange, purple, cyan. The screenshots are copied into the image at build time (`data/docs-img`) |

Warehouse (reads always on):

| Tool | Source |
|---|---|
| `warehouses_list` | Warehouse `GET /api/warehouse/v1/warehouses` |
| `locations_search` | Warehouse `GET /locations` |
| `location_stock` | Warehouse `GET /stock/available` by `locationId` / `location` |
| `item_cost` | Warehouse `GET /valuation/on-hand-value` |
| `suppliers_search` | Warehouse `GET /suppliers` |
| `inbound_shipments_list`, `inbound_shipment_get` | Warehouse `GET /inbound-shipments[/{id}]` |
| `transfers_list`, `transfer_get` | Warehouse `GET /transfers[/{id}]` |

Warehouse entry (only with `MCP_ENABLE_WAREHOUSE_ENTRY=true`; GitHub variable `CIKADA_MCP_WAREHOUSE_ENTRY`):

| Tool | Source |
|---|---|
| `location_create` | Warehouse `POST /locations` |
| `inbound_shipment_create` | Warehouse `POST /inbound-shipments` (a supplier invoice with its lines) |
| `inbound_shipment_receive` | Warehouse `POST /inbound-shipments/{id}/receive` |
| `inbound_shipment_post` | Warehouse `POST /inbound-shipments/{id}/post` (destructive: accounting post) |
| `putaway_move` | Warehouse `POST /putaway` (RECEIVING to a storage location) |
| `transfer_create`, `transfer_advance` | Warehouse `POST /transfers`, `POST /transfers/{id}/submit|approve|execute` |

Prompts: `production_status`, `debt_review`, `management_report`, `daily_orders_review`, `take_new_order`.

Order entry (only with `MCP_ENABLE_ORDER_ENTRY=true`; on test via the `CIKADA_MCP_ORDER_ENTRY` environment variable):

| Tool | API call |
|---|---|
| `order_entry_catalog` | `GET /api/sales/order-entry/catalog` |
| `order_entry_references` | `GET /api/sales/order-entry/references/{series,categories,types,types/{id}}` |
| `order_entry_addresses` | `GET /api/sales/order-entry/addresses` |
| `order_entry_unit_price` | `GET /api/sales/order-entry/unit-price` |
| `order_entry_product_preview` | `POST /api/sales/order-entry/product-preview` (calculation only) |
| `order_entry_get` | `GET /api/sales/order-entry/orders/{id}` |
| `order_create` | `POST /api/sales/order-entry/orders` |
| `order_update` | `PUT /api/sales/order-entry/orders/{id}` (merges with the current header) |
| `order_product_add` | `POST /api/sales/order-entry/orders/{id}/products` |
| `order_product_update` | `PUT /api/sales/order-entry/orders/{id}/products/{productId}` (merges with the current line) |
| `order_product_duplicate` | `POST /api/sales/order-entry/orders/{id}/products/{productId}/duplicate` |
| `order_accessory_set` | `POST /api/sales/order-entry/orders/{id}/products/{productId}/accessories` |
| `order_accessory_remove` | `DELETE /api/sales/order-entry/orders/{id}/products/{productId}/accessories/{accessoryId}` |
| `order_final_price_set` | `PUT /api/sales/order-entry/orders/{id}/final-price` |
| `order_product_remove` | `DELETE /api/sales/order-entry/orders/{id}/products/{productId}` (destructive) |
| `order_address_set` | `PUT /api/sales/order-entry/orders/{id}/address` |
| `order_confirm` | `POST /api/sales/order-entry/orders/{id}/confirm` (destructive: sends to production) |
| `order_cancel` | `POST /api/sales/order-entry/orders/{id}/cancel` (destructive) |

Writes take the order `expectedRevision` (from `order_entry_get` or the previous write) and an optional `commandId` (idempotency; a new one is generated when absent and returned for retries). A 404 from a Sales API without order entry answers "Order entry is not deployed on this environment yet."

Prompts: `daily_orders_review`, `take_new_order` (order entry only).

## OAuth endpoints (served by this process)

| Path | What |
|---|---|
| `/.well-known/oauth-protected-resource[/mcp]` | RFC 9728; `authorization_servers: [MCP_PUBLIC_URL]` |
| `/.well-known/oauth-authorization-server`, `/.well-known/openid-configuration` | RFC 8414 metadata; PKCE S256, `token_endpoint_auth_methods_supported: [none]` |
| `/register` | RFC 7591 without a registry: always the static public client id |
| `/authorize` | 302 to `https://sso.lauresta.com/application/o/authorize/` (drops `resource`, adds the `cikada` scope) |
| `/token` | relays to `https://sso.lauresta.com/application/o/token/` (drops `resource`) |

`/mcp` without a valid bearer answers 401 with `WWW-Authenticate: Bearer resource_metadata="<base>/.well-known/oauth-protected-resource/mcp"`. The bearer is checked for shape, expiry, issuer and audience only; signature and roles are checked by the APIs.

## Configuration

| Variable | Required | Default | |
|---|---|---|---|
| `MCP_PUBLIC_URL` | yes | | Public origin, e.g. `https://cikada-test.lauresta.com` |
| `MCP_OAUTH_CLIENT_ID` | yes | | Public client id of authentik app `cikada-mcp` (GitHub var `CIKADA_MCP_CLIENT_ID`) |
| `SALES_API_URL` | yes | | e.g. `http://lkvitai-mes-sales-api:8080` |
| `WAREHOUSE_API_URL` | yes | | e.g. `http://lkvitai-mes-warehouse-api:8080` |
| `FINANCE_API_URL` | yes | | e.g. `http://lkvitai-mes-finance-api:8080` |
| `MCP_ENABLE_ORDER_ENTRY` | no | `false` | `true` offers the order entry tools |
| `MCP_ENABLE_WAREHOUSE_ENTRY` | no | `false` | `true` offers the warehouse write tools |
| `DOCS_IMAGE_DIR` | no | `data/docs-img` next to `dist/` | Folder with the manual screenshots for `docs_image` |
| `MANUALS_BASE_URL` | no | | Where the MkDocs manuals site is served; with it `docs_search` / `docs_get` return absolute links, without it the site path (`procesai/P-01-.../#anchor`) |
| `DOCS_INDEX_PATH` | no | `data/docs-index.json` next to `dist/` | Override the generated index file |
| `MCP_PORT` | no | `8080` | |
| `MCP_OAUTH_ISSUER` | no | `https://sso.lauresta.com/application/o/cikada-mcp/` | Expected `iss` |
| `MCP_OAUTH_AUTHORIZE_URL`, `MCP_OAUTH_TOKEN_URL`, `MCP_OAUTH_JWKS_URL` | no | authentik endpoints | |

## User manuals index

The manuals (`docs/manuals/docs/**/*.md`, MkDocs source, format in `docs/manuals/AUTHORING.md`) are chunked by `src/docs-chunker.ts`: optional YAML front matter (`module`, `title`, `routes`, `roles`, `status`), one chunk per `##` section (a `##` over about 1800 characters is split at `###`; text before the first `##` is its own `#intro` chunk), images reduced to their alt text, `(UI: ...)` labels kept. Chunk id is `<path without .md>#<heading slugs>`; the module comes from front matter, else from the folder (`procesai`, `roles`, `trikciu-salinimas` are `warehouse`).

The index is **generated, not committed**: `npm run docs:index` (`src/build-docs-index.ts`, after `npm run build`) writes `data/docs-index.json` and `data/docs-manifest.json` (page count, chunk count, generated-at, git sha), both git-ignored. The Dockerfile runs it in the build stage and copies `data/` into the image, so the **Docker build context is the repository root** (filtered by `Dockerfile.dockerignore` to this package and `docs/manuals/docs/**/*.md`). `mcp-ci` and `build-and-push` also run when `docs/manuals/docs/**` changes, so a manual edit publishes a new MCP image. Without the file the two tools answer `DOCS_UNAVAILABLE`.

## Develop

```bash
cd src/Mcp/Cikada.Mcp
npm ci && npm run build && npm test
npm run docs:index   # data/docs-index.json from ../../../docs/manuals/docs
MCP_PUBLIC_URL=http://localhost:5070 MCP_PORT=5070 MCP_OAUTH_CLIENT_ID=<client id> \
  SALES_API_URL=http://localhost:5021 WAREHOUSE_API_URL=http://localhost:5001 FINANCE_API_URL=http://localhost:5031 npm start
docker build -f src/Mcp/Cikada.Mcp/Dockerfile -t lkvitai-mes-mcp .   # run from the repository root
```

Try it with the MCP Inspector: `npx @modelcontextprotocol/inspector`, transport Streamable HTTP, URL `http://localhost:5070/mcp`.
