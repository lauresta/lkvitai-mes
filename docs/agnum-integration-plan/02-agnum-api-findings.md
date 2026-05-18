# 02. Agnum API Findings

Source repository: `https://github.com/lauresta/agnum-api-deploy`

Checked commit: `c059aa3` on 2026-05-18 09:47:35 +0300.

## Deployment Shape

The repo wraps `AgnumDBApi-1.39.jar`.

Test VM layout documented there:

- Runtime config: `/opt/agnum/application.properties`
- Logs: `/opt/agnum/logs/`
- Container: `agnum-api`
- Image: `ghcr.io/lauresta/agnum-api`
- Local health: `http://localhost:8181/doc/health`
- Local Swagger: `http://localhost:8181/swagger-ui/index.html`
- Local OpenAPI: `http://localhost:8181/v3/api-docs`

The compose stack joins `lkvitai-mes_default`, so Warehouse API can likely call it by container/service DNS if deployed on the same network.

## Authentication and Warehouse Context

Agnum API uses header:

```text
X-API-KEY: <key>
```

Important behavior:

- API key maps to an API user in `application.properties`.
- Each API user has fixed `sndid`.
- `sndid` is the Agnum warehouse/store context.
- Product reads/writes use that context.
- There is no confirmed request parameter to switch warehouse per call.
- Multi-warehouse integration means multiple API users/keys.

Example config from `application.properties.example`:

```properties
cfg.apiusers.sandelys.apiKey=CHANGE_ME_493
cfg.apiusers.sandelys.dbalias=agnum
cfg.apiusers.sandelys.sndid=493
cfg.apiusers.sandelys.rights=READ_ALL,SAVE_ALL

cfg.apiusers.gamyba.apiKey=CHANGE_ME_498
cfg.apiusers.gamyba.dbalias=agnum
cfg.apiusers.gamyba.sndid=498
cfg.apiusers.gamyba.rights=READ_ALL,SAVE_ALL
```

Known test DB `sndid` values include:

| sndid | Name | Description |
| --- | --- | --- |
| 493 | Sandelys | Centrinis sandelys |
| 496 | Pardavimai | Pagaminta produkcija-pardavimai |
| 498 | Gamyba | Gamybos sandelys |
| 500 | Mazavertis | Mazavertis inventorius |
| 502 | Kuras | Transportas ir kuras |
| 507 | Ilgalaikis | Ilgalaikis turtas |
| 509 | Visi | Visi sandeliai |
| 1498 | Nebaigta s | Nebaigta statyba |
| 12503 | Paslaugos | Paslaugos |
| 142026 | PVZ | Pavyzdziai |
| 142029 | Parduotuve | Internetine parduotuve |

## Product/Nomenclature API

Endpoints documented:

- `GET /api/products/{id}`
- `GET /api/products/search`
- `GET /api/products/qty/search`
- `GET /api/products/price/search`
- `POST /api/products`
- `PUT /api/products`

Product create requires at least `code`; practical minimum:

```json
{
  "code": "SKU-001",
  "name": "Product name",
  "pcs": "vnt",
  "enabled": true,
  "netto": 1,
  "brutto": 1
}
```

Key mapping facts:

- `id` maps to `PRK.ID_PRK`, but it is not globally unique across warehouses. Treat product identity as `(sndid, id)` or `(warehouseKey, id)`.
- `code` maps to `PRK.KOD` and should be treated as the primary SKU/code for integration unless we store Agnum references explicitly.
- `balance` maps to `PRK.KIEKIS` for the API user's warehouse.
- `pcs` maps to unit of measure.
- `barcode`/`barcodes` may differ between API responses; importer should handle both defensively.
- `f1` is likely Intrastat commodity code.
- `f2` is supplier product code.
- `f6` is likely Intrastat quantity coefficient.

Known issue:

- Product `limit`/`offset` is buggy in jar `1.39`, generating invalid SQL such as `ORDER BY P.ID_PRKROWS 5`.
- Do not rely on pagination until jar is fixed.

## Document APIs

Sales/invoice-like document:

- `POST /api/orders`
- Jar maps this to `DokType VSK = pardavimas`.
- Required top-level fields: `date`, `doc_no`, `sum`, `vat_sum`, `total`, `client`.
- Treat `products[]` as required in practice.
- Required line fields: `code`, `qty`, `price`, `vat`, `vat_proc`.
- Optional line fields: `id`, `bk`, `mark`, `vat_code`, `memo`, `pcs`.

Purchase receipt candidate:

- `POST /api/receipts`
- `GET /api/receipts/{docNo}`
- Jar maps this to `DokType VKS = pajamavimas`.
- This is not in current OpenAPI output and must be validated carefully before production use.

Customer return:

- `POST /api/customer-returns`
- `GET /api/customer-returns/{docNo}`
- `GET /api/customer-returns/search`
- Jar maps this to `DokType GKS = grazinimas`.

## Consequences for MES

The integration should not be a single stock snapshot POST.

It needs a small anti-corruption layer with:

- Per-warehouse Agnum credentials and `sndid` mapping.
- Product import/export/sync.
- Document export for business events.
- Idempotency and export history by document/movement.
- Read-side diagnostics against Agnum products and balances.
- Explicit business confirmation for each document type and direction.

