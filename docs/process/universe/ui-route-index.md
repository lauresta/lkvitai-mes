# UI Route Index

**Source:** Blazor `@page` directives in `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Pages/**/*.razor`
**Status:** Extracted 2026-02-24 — see authoritative full table in `docs/processes/process-universe.md` §Appendix A

---

## Route Table (72 routes)

> For the full evidence-backed table (route → file path → nav group → intent) see:
> [`docs/processes/process-universe.md#appendix-a--ui-route-index`](../../processes/process-universe.md)

### Summary by Nav Group

| Nav Group | Route Count | Route Prefix |
|-----------|-------------|-------------|
| Stock | 5 | `/available-stock`, `/warehouse/stock/*`, `/reservations` |
| Inbound | 5 | `/warehouse/inbound/*`, `/warehouse/putaway` |
| Outbound | 11 | `/warehouse/sales/*`, `/warehouse/outbound/*`, `/warehouse/waves`, `/warehouse/picking/*`, `/warehouse/labels`, `/warehouse/cross-dock`, `/warehouse/rmas` |
| Operations | 9 | `/warehouse/transfers/*`, `/warehouse/cycle-counts/*`, `/warehouse/visualization/*`, `/projections` |
| Finance | 5 | `/warehouse/valuation/*`, `/warehouse/agnum/*` |
| Admin | 22 | `/warehouse/admin/*`, `/admin/*` |
| Reports | 9 | `/reports/*`, `/warehouse/compliance/*` |
| Analytics | 2 | `/analytics/*` |
| Root/Misc | 4 | `/`, `/dashboard`, `/warehouse/locations/{Id:int}`, `/available-stock` |

### TODO

- [ ] Expand this file with the full route → file path mapping from Appendix A
- [ ] Add "Last verified" date after each major Blazor page change
- [ ] Add process ID column (P-XX) cross-reference
