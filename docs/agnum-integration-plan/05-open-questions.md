# 05. Open Questions

## Resolved 2026-05-18

| Question | Answer |
| --- | --- |
| Which sndid are in WMS scope? | **493** (Centrinis sandėlys) — main. **496** (Pagaminta produkcija-pardavimai) — secondary. **498** (Gamyba) — pending business confirmation. |
| Which sndid to exclude? | 500 Mažavertis, 502 Kuras, 507 Ilgalaikis, 509 Visi, 1498 Nebaigta statyba, 12503 Paslaugos, 142026 PVZ, 142029 Parduotuvė, 144513 (null). |
| Is sndid 498 Gamyba used? | Pending — Vytautas to confirm. |

## Business Process

1. Which Agnum `sndid` values are in scope for MES import?
2. Should every in-scope `sndid` become a MES virtual warehouse?
3. Which Agnum warehouses should be excluded from WMS operations, for example services/fixed assets/fuel?
4. Who is allowed to approve distribution from virtual Agnum balances into physical MES bins?
5. What is the business rule for over-distribution or under-distribution versus Agnum balance?
6. Which exact Warehouse flows must create Agnum documents later, after import/distribution foundation is working?
7. Which moment later triggers sales export: order creation, shipping, delivery, invoice approval, or accounting approval?
8. Which moment later triggers purchase receipt export: receiving, QC acceptance, supplier invoice, or accountant approval?

## Agnum API

1. What is the test stand URL reachable from MES containers?
2. Which API keys exist on the stand, and which `sndid` does each represent?
3. Which endpoint is best for full product/balance import: `/api/products/search`, `/api/products/qty/search`, or another endpoint?
4. Does `/api/products/search` balance represent current balance for the API key's `sndid`?
5. Is there any supported way to pass warehouse/sndid per request instead of per API key?
6. Will jar `1.39` product `limit`/`offset` bug be fixed soon?
7. Later: is `/api/receipts` officially supported for purchase receipt/pajamavimas in production?
8. Later: is `/api/orders` the final sales invoice/document endpoint, or only an order-like document?

## Data Mapping

1. Which Agnum `sndid` maps to each MES virtual warehouse?
2. Should MES SKU equal Agnum `code` for all products?
3. Do we need to preserve Agnum `(sndid, id)` on MES item records?
4. How should duplicate product codes across warehouses be handled?
5. Which UoM values are allowed in Agnum and MES?
6. Are Agnum `f1`, `f2`, `f6` required in MES item details?
7. Are Agnum product balances authoritative as opening virtual balances, or only reconciliation source?
8. Should Agnum `group/category/subgroup` become one MES category hierarchy?
9. Should Agnum `direction` and `branch` become separate classifiers, or only stored as imported attributes?
10. Which Agnum source should be used for supplier master import? Product `f2` only gives supplier product code, not supplier identity.
11. Which physical MES warehouses/locations can receive distribution from each Agnum virtual warehouse?

## Operations

1. Who owns failed product/balance import review?
2. How long should Agnum import raw payload/hash snapshots be retained?
3. How often should Agnum products and balances be refreshed?
4. What is the fallback when Agnum API is down during import?
5. Which users can configure API keys and trigger manual import?
6. Later: who owns failed document export retry/review?
