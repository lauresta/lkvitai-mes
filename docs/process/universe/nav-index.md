# Navigation Menu Index

**Source:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/Shared/NavMenu.razor`
**Status:** Extracted 2026-02-24 — see authoritative full table in `docs/processes/process-universe.md` §Appendix C

---

## Nav Structure (8 groups, 55+ items)

> For the full evidence-backed table (group → item → route) see:
> [`docs/processes/process-universe.md#appendix-c--nav-menu-index`](../../processes/process-universe.md)

| Group | Icon | Item Count | Maps to Process |
|-------|------|-----------|----------------|
| **Stock** | `bi-box-seam` | 5 | P-06, P-13 |
| **Inbound** | `bi-box-arrow-in-down` | 3 | P-01, P-02 |
| **Outbound** | `bi-box-arrow-right` | 9 | P-03, P-09, P-10 |
| **Operations** | `bi-gear` | 4 | P-04, P-05, P-12 |
| **Finance** | `bi-cash-stack` | 3 | P-07, P-08 |
| **Admin** | `bi-shield-lock` | 22 | P-14, P-15 |
| **Reports** | `bi-clipboard-data` | 9 | P-11, P-13 |
| **Analytics** | `bi-graph-up-arrow` | 2 | P-13 |

### Groups → Key Routes

```
Stock
  /available-stock
  /warehouse/stock/dashboard
  /warehouse/stock/location-balance
  /warehouse/stock/adjustments
  /reservations

Inbound
  /warehouse/inbound/shipments
  /warehouse/inbound/qc
  /warehouse/putaway

Outbound
  /warehouse/sales/orders
  /warehouse/sales/allocations
  /warehouse/outbound/orders
  /warehouse/outbound/dispatch
  /warehouse/waves
  /warehouse/picking/tasks
  /warehouse/labels
  /warehouse/cross-dock
  /warehouse/rmas

Operations
  /warehouse/transfers
  /warehouse/cycle-counts
  /warehouse/visualization/3d
  /projections

Finance
  /warehouse/valuation/dashboard
  /warehouse/agnum/config
  /warehouse/agnum/reconcile

Admin
  /admin/users
  /warehouse/admin/settings
  /warehouse/admin/reason-codes
  /warehouse/admin/approval-rules
  /warehouse/admin/roles
  /warehouse/admin/api-keys
  /warehouse/admin/gdpr-erasure
  /warehouse/admin/audit-logs
  /warehouse/admin/backups
  /warehouse/admin/retention-policies
  /warehouse/admin/dr-drills
  /warehouse/admin/serial-numbers
  /warehouse/admin/lots
  /warehouse/admin/uom
  /admin/items
  /admin/suppliers
  /admin/supplier-mappings
  /admin/locations
  /admin/categories
  /admin/import
  /warehouse/admin/layout-editor

Reports
  /reports/stock-level
  /reports/receiving-history
  /reports/pick-history
  /reports/dispatch-history
  /reports/stock-movements
  /reports/traceability
  /warehouse/compliance/lot-trace
  /reports/compliance-audit
  /warehouse/compliance/dashboard

Analytics
  /analytics/fulfillment
  /analytics/quality
```

### TODO

- [ ] Expand with role visibility rules (which nav items visible per role)
- [ ] Add Lithuanian menu labels for cross-reference with user manuals
