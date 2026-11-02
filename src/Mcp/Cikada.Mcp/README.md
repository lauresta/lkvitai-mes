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

Order entry (only with `MCP_ENABLE_ORDER_ENTRY=true`; the API is PR #224):

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
| `MCP_ENABLE_ORDER_ENTRY` | no | `false` | `true` offers the order entry tools |
| `MCP_PORT` | no | `8080` | |
| `MCP_OAUTH_ISSUER` | no | `https://sso.lauresta.com/application/o/cikada-mcp/` | Expected `iss` |
| `MCP_OAUTH_AUTHORIZE_URL`, `MCP_OAUTH_TOKEN_URL`, `MCP_OAUTH_JWKS_URL` | no | authentik endpoints | |

## Develop

```bash
cd src/Mcp/Cikada.Mcp
npm ci && npm run build && npm test
MCP_PUBLIC_URL=http://localhost:5070 MCP_PORT=5070 MCP_OAUTH_CLIENT_ID=<client id> \
  SALES_API_URL=http://localhost:5021 WAREHOUSE_API_URL=http://localhost:5001 npm start
docker build -t lkvitai-mes-mcp .
```

Try it with the MCP Inspector: `npx @modelcontextprotocol/inspector`, transport Streamable HTTP, URL `http://localhost:5070/mcp`.
