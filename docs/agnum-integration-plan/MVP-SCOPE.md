# Agnum Integration — MVP Scope

Created: 2026-05-18

This file is the authoritative scope gate for the Agnum integration workstream.
It governs what Codex implements in each slice.

## Business goal

The first deliverable is read-only visibility and a distribution bridge:

1. MES shows the full Agnum nomenclature (product list) from each configured Agnum warehouse.
2. MES shows how much stock exists per Agnum warehouse, with `sndid` as a virtual warehouse context.
3. Product identity is `(sndid, agnumProductId)`. Agnum `code` is the SKU candidate.
4. Agnum balance is never stored on `Item`. It lives in a separate virtual warehouse balance model.
5. Users distribute those virtual balances into real MES physical warehouses and locations.
6. After distribution, users can search products and see physical location in the existing 2D/3D viewer.
7. Agnum document export is not part of this deliverable.

## Phase status

| Phase | Description | Status |
| --- | --- | --- |
| 0 | Business confirmations (sndid scope, UoM rules, category mapping) | **Done** — defaults in this file |
| 1 | Agnum read connector (`IAgnumApiClient`, `AgnumApiClient`, DTOs) | **Done** — PR #159, shipping on main |
| 2 | Product/nomenclature import (`AgnumProductLink`, `ItemExternalAttribute`, conflict detection, apply, supplier sync, UoM auto-create, category hierarchy) | **Done** — PR #159 + fixes #162–169 |
| 3 | Virtual balance import (`AgnumVirtualWarehouseBalance`, `AgnumBalanceImportRun`), Hangfire daily job, `Balances.razor` read-only UI | **Done** — PR #160 + #161 + #169 |
| 4 | Distribution from virtual balance to physical MES location | **SLICE 2** — next |
| 5 | Product search + 2D/3D location visibility after distribution | **SLICE 3** — after Slice 2 |
| 6 | Real warehouse operations | **NOT NEEDED** — packages A–F already implement all MES warehouse operations; no changes required |
| 7 | Agnum document export | **BACKLOG** — governed by ADR-006; do not start until phases 1–5 are stable |

## Phase 0 defaults (resolved for Slice 1)

These are the working defaults for Codex. Do not re-open them unless business confirms a different rule.

| Decision | Default |
| --- | --- |
| Active sndid for import | `493` (Centrinis sandėlys) — main WMS warehouse. `496` (Pagaminta produkcija-pardavimai) — secondary. `498` (Gamyba) — confirm with business. Exclude all others: 502 Kuras, 507 Ilgalaikis, 509 Visi, 1498, 12503 Paslaugos, 142026 PVZ, 142029 Parduotuvė, 144513. |
| Product identity | `(sndid, agnumProductId)` — not `agnumProductId` alone |
| SKU source | Agnum `code` → `Item.InternalSKU` |
| Balance storage | Never on `Item`. Import as `AgnumVirtualWarehouseBalance` only. |
| UoM on unknown `pcs` value | Block import of that product, surface as a conflict row. Known mappings: `vnt`, `m`, `m2`, `kg`. |
| Category mapping | `group` → `ItemCategory` level 1, `category` → level 2, `subgroup` → level 3. `direction`/`branch` → `ItemExternalAttribute`. |
| Pagination | No `limit`/`offset` (jar 1.39 bug). Load all products per sndid in one call. |
| Agnum base URL config key | `Agnum:BaseUrl`, default `http://agnum-api:8181` |
| API key config pattern | `Agnum:Warehouses:{name}:SndId` and `Agnum:Warehouses:{name}:ApiKey` |
| POST/PUT to Agnum | **Never** in Slices 1–3 |

## Files to freeze — do not modify in Slice 1

These files implement the old export-first pattern. They compile and must keep compiling.
Do not extend, refactor, or fix them in Slice 1.

| File | Location | Why frozen |
| --- | --- | --- |
| `AgnumController.cs` | `Api/Api/Controllers/` | Old export routes; leave intact |
| `AgnumExportServices.cs` | `Api/Services/` | Old export + `IAgnumExportOrchestrator` + `IAgnumSecretProtector` |
| `AgnumReconciliationServices.cs` | `Api/Services/` | Old reconciliation; leave intact |
| `AgnumExportSaga.cs` | `Sagas/` | Old MassTransit export state machine |
| `IAgnumExportService.cs` | `Integration/Agnum/` | Old export interface; new `IAgnumApiClient` is separate |
| `AgnumExportConfig` entity | `Domain/Entities/MasterDataEntities.cs` | Old export config entity |
| `AgnumMapping` entity | `Domain/Entities/MasterDataEntities.cs` | Old mapping entity |
| `AgnumExportHistory` entity | `Domain/Entities/MasterDataEntities.cs` | Old history entity |
| Migration `20260211070343_AddAgnumExportTables` | `Infrastructure/Persistence/Migrations/` | Already applied; do not touch |

## What Slice 1 must NOT include

- Blazor UI pages
- Physical distribution command or workflow
- 3D/2D product search
- Agnum document export of any kind
- Refactor of the old Agnum export code
- MassTransit sagas for import flows
- Any POST or PUT to the Agnum API
