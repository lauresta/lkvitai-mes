# Warehouse: prices everywhere — status and plan

Updated: 2026-11-07. Owner: Denisas Bykovas. Status: **blueprint ready, handed to Codex**.
Code task: [pricing-implementation-blueprint.md](pricing-implementation-blueprint.md).
Progress: [pricing-codex-handoff.md](pricing-codex-handoff.md).

Goal: every Warehouse screen where an operator sees a stock quantity also shows the price.

## Decisions

- **Both prices** are shown: cost (WAC, else purchase) and selling (base). The client picks after the demo.
- **All roles** see the purchase cost in Warehouse. No role gating.
- **Frontline shows the indicative selling price only.** Changed on 2026-11-07: the module is anonymous on
  test (issue #102), so purchase cost must not reach it. This also drops the fabric ↔ item mapping table.
- The legacy database is read-only, no DDL (it is shared with production, #267).

## Shipped

| PR | What |
|---|---|
| [#203](https://github.com/lauresta/lkvitai-mes/pull/203) | Item base/purchase price, price groups, overrides, history, Customer CRUD, group price in Sales Order, cost columns on Available Stock (table) |
| [#204](https://github.com/lauresta/lkvitai-mes/pull/204) | Stock falls back to purchase price without valuation |
| [#206](https://github.com/lauresta/lkvitai-mes/pull/206) | Unit price at receiving, landed cost into `ItemValuation`, cost history |
| [#223](https://github.com/lauresta/lkvitai-mes/pull/223) | Unified item pricing page, SKU links |
| [#280](https://github.com/lauresta/lkvitai-mes/pull/280) | Warehouse dashboard fix on test: `/api/warehouse/*` routing, Bootstrap 5.3.3 and icons vendored |

## Data findings (2026-11-07)

- **Agnum API is down on test.** The vendor jar has a hard-coded expiry of 2026-08-01; every call returns 500
  `invalid exp config`. It is being replaced by our own .NET service, see `agnum-dotnet-api-blueprint.md`. Balances are frozen at 2026-08-01, and daily import runs report
  `Completed` with 0 products, which hid this for three months. Production was not checked.
- **Agnum exposes selling prices only** (`KN1`–`KN5`). Purchase price is not in the API.
- Test database: 9505 items, 2 priced, `on_hand_value` empty, available-stock projection empty. The only screen
  with real volume is `/agnum/balances`.

So purchase and selling prices for the demo come from an **Excel price import** (stage S2).

## Stages

| Stage | Scope | Estimate |
|---|---|---|
| S1 | One price snapshot service and endpoint, shared WebUI pieces, three cost joins replaced | 1–2 days |
| S2 | Excel price import through Admin Import, with history | 0.5 day |
| S3 | Stock screens, `/agnum/balances` first | 1–2 days |
| S4 | Operational screens | 2 days |
| S5 | Price and stock hint under item pickers | 0.5 day |
| S6 | Frontline indicative selling price | 0.5–1 day |
| S7a | Agnum failures reported as `Failed` | 0.5 day |
| S7b | Agnum selling price sync, only once the API answers | 1 day |

Out of scope: FIFO/lot costing, currency, price export to Agnum, LT translation of Warehouse screens.

## Owner actions

1. Decide which Firebird database `agnum-api-net` should read. The configured `LAURESTA-EUR-TESTAPI.GDB` looks like a
   copy frozen around 2026-05-05. A vendor jar is no longer required.
2. Provide an Excel file: `InternalSKU`, `PurchasePrice`, `BasePrice` (template from stage S2).
3. After S7b, set `Agnum__PriceSync__Enabled=true` on test to write Agnum selling prices. The price levels are set by
   `deploy-net-test.yml`.
