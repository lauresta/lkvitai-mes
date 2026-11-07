# Warehouse: prices everywhere — status and plan

Updated: 2026-11-07. Owner: Denisas Bykovas. Status: **handed to Codex** — blueprint and stages A–E per
`docs/warehouse/pricing-codex-handoff.md`; Codex works strictly inside that blueprint.

Goal (restated 2026-11-07): every Warehouse screen where an operator sees a stock quantity
also shows the price. Decisions taken:

- **Both prices** are shown — cost (purchase / WAC) and selling (base, group override). Which
  one stays after the demo is the client's call.
- **All roles** see the purchase cost; no role gating.
- Frontline fabric card gets a price too (see section 5).

## 1. Shipped (July 2026, all in `main`)

| PR | What |
|---|---|
| [#203](https://github.com/lauresta/lkvitai-mes/pull/203) | `Item.BasePrice` / `PurchasePrice`, `PriceGroup` + `ItemPriceOverride` + `ItemPriceHistory`, Customer CRUD, group price pre-filled into Sales Order lines, Unit Cost / Value columns on Available Stock (table view), dead `Valuation` fork removed (ADR-002 superseded) |
| [#204](https://github.com/lauresta/lkvitai-mes/pull/204) | Stock screens fall back to `Item.PurchasePrice` when no `OnHandValue` row exists |
| [#206](https://github.com/lauresta/lkvitai-mes/pull/206) | Unit price required at receiving; freight / duty / insurance / other costs allocated across lines and folded into `ItemValuation` (initialize or weighted-average adjust); purchase / cost history on the item page |
| [#223](https://github.com/lauresta/lkvitai-mes/pull/223) | Unified item pricing page (`/admin/items/{id}`: all prices + current on-hand value); SKU links from Available Stock and the Valuation dashboard |

## 2. Screen audit (2026-11-07)

| Screen | Qty | Price |
|---|---|---|
| Available Stock — table view | yes | yes (cost, value) |
| Available Stock — list and gallery views | yes | **no** |
| Stock Dashboard (3 tables), Stock Level report + CSV | yes | **no** |
| Location Balance, Location Detail | yes | **no** (weight / volume only) |
| Stock Movements, Stock Adjustments | yes | **no** (movement / delta value missing) |
| Reservations, Picking Tasks, Putaway, Packing Station, Outbound Order | yes | **no** |
| Inbound Shipments list, Receiving QC, Cross-dock, RMA | yes | **no** (shipment detail has it) |
| Item pickers (adjustment, pick task, inbound, sales order) | — | **no** price next to the item |
| Dashboard | yes | total stock value only |
| Frontline Fabric lookup / low stock | yes | **no** |
| Admin: Items, Item detail, Price groups, Customers, Supplier mappings | — | yes |

## 3. Data finding — the real blocker

Test DB (`lkvitai_warehouse_test`, 2026-11-07): **9505 items, 2 priced**, `on_hand_value` empty,
available-stock projection empty, 4 inbound shipment lines (2 priced). Even where the columns
exist the operator sees "–".

Where prices can come from:

- **Agnum** — 9502 of 9505 items are Agnum products (`agnum_product_links`). The importer
  (`AgnumProductDto`) brings code, name, UoM, netto/brutto, barcode, supplier — **no price** —
  although the Agnum API lists `GET /api/products/price/search`
  (`docs/agnum-integration-plan/02-agnum-api-findings.md`). Response shape is undocumented;
  verify against the live `agnum-api` first.
- **Receiving** (#206) — correct but slow to fill; only lines received through Cikada get a cost.
- **Supplier mappings** — `SupplierItemMapping.PricePerUnit` exists; 0 rows on test.
- **Legacy LKvitaiDb is not a source for warehouse items**: `DIC_PriceList` / `DIC_PriceGroups`
  are blinds selling prices by width × height × period, `Zinynas_priedai.Kaina` accessories,
  `senukai.Nomenklatura` a retailer catalogue. Fabrics there (`TBD_Components`, codes like
  `RPT-12`) do not share a key with the Agnum fabric items (`2103170012`); matching is by
  name / supplier code only.

The cost join (`OnHandValue.UnitCost` → fallback `Item.PurchasePrice`) is duplicated in
`StockController`, `DashboardController` and `CycleCountsController`.

## 4. Plan

| Stage | Scope | Estimate |
|---|---|---|
| **A0. Demo data on test** | Run the existing Agnum imports (`POST /api/warehouse/v1/agnum/import/products`, `.../import/balances`) on cikada-test so Available Stock and `on_hand_value` are not empty; re-run the price sync from stage A afterwards. Ops step, no code. | 0.5 day |
| **A. Price source** | Check what `products/price/search` returns. Pull purchase price on Agnum product import and on re-sync, writing `ItemPriceHistory` on change. Supplier mapping price as fallback. Backfill on test so screens show real numbers for the demo. | 2–3 days |
| **B. One price snapshot** | Replace the three copies of the cost join with one `ItemPriceSnapshot` (purchase, WAC unit cost, base selling, effective group price, value = qty × cost) behind one Application service and one endpoint; all screens read it. | 1–2 days |
| **C. Stock screens** | Add Unit cost / Value (and base selling price) to every "no" row in section 2: Stock Dashboard, Stock Level report + CSV, Location Balance (value per location), Location Detail, Available Stock list + gallery, Stock Movements (movement value), Stock Adjustments (delta value), Reservations, Picking Tasks suggestions, Putaway, Packing Station, Outbound Order, Inbound Shipments list (shipment value), Receiving QC, Cross-dock, RMA. | 3–4 days |
| **D. Item pickers** | Show price + available qty next to the item in autocompletes (`AdminItemDto` already carries `BasePrice` / `PurchasePrice`). | 1 day |
| **E. Frontline** | Fabric card and low-stock list exactly as section 5 (both prices; `frontline_component_items` mapping seeded by name + supplier code, admin correction screen; reference size 1000 × 1000 mm). Separate PR after A–D. | 2–3 days |
| **F. Warehouse dashboard fix** | Done in [#280](https://github.com/lauresta/lkvitai-mes/pull/280): Traefik passed `/api/warehouse/*` through (stripPrefix gave 404 on every public call, incl. photo proxy URLs), Bootstrap 5.3.3 + icons vendored into wwwroot. | done |
| **G. Deferred** | FIFO / lot costing and currency (ADR-002), price export to Agnum (deferred in #206), LT translation of Warehouse screens (module not translated at all — separate task). | — |

Order: A0 → A → B → C → D → E, one PR per stage from `main` (branches `codex/warehouse-prices-<stage>`),
autopilot delivery (commit → PR → admin merge → deploy to test → check on cikada-test).

## 5. Frontline fabric price — proposal

The fabric card is fed by legacy `dbo.TBD_Components` + `dbo.V_Remains` through
`mes_Fabric_GetMobileCard`. Legacy has no per-metre fabric price: a fabric belongs to a
components price group per blind type (`TCR_TypeColourPriceGroup`), and the selling price is
looked up by blind width × height × period in `DIC_PriceList`.

Proposal (both prices, consistent with the decision above):

1. **Cost per metre** from Warehouse: the fabric's Agnum item (`PurchasePrice` / WAC). Needs a
   `TBD_Components ↔ Item` mapping — new table `frontline_component_items`
   (`iComponentID`, `ItemId`), seeded automatically by name + supplier code match and corrected
   by hand in an admin screen; unmatched fabrics show "–".
2. **Indicative selling price** from legacy: the fabric's price group per blind type and the
   `DIC_PriceList` price for a reference size (default 1000 × 1000 mm, current period), e.g.
   "Roletai: group 3 · 1000×1000 from 48.00 €". Read-only, through a new `mes_Fabric_*` proc so
   the Frontline adapter stays proc-only.

Alternative if the mapping is not wanted now: show only item 2 (price group + reference price)
and link to the Sales calculator for exact prices.

## 6. Open points

- Response shape of Agnum `products/price/search` (is it purchase or selling price, per
  warehouse?).
- Reference size for the Frontline indicative price (1000 × 1000 mm proposed).
