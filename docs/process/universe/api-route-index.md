# API Route Index

**Source:** ASP.NET Core `[Route]` + `[Http*]` attributes in
`src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Api/Controllers/**/*.cs`
**Status:** Extracted 2026-02-24 — see authoritative full table in `docs/processes/process-universe.md` §Appendix B

---

## Base Path Convention

```
Health/Auth : api/warehouse/v1/health  |  api/auth/oauth  |  api/auth/mfa
Core Ops    : api/warehouse/v1/{resource}
Admin       : api/warehouse/v1/admin/{function}
Legacy      : api/reservations  (alias for api/warehouse/v1/reservations)
```

## Controller Summary (56 controllers / nested classes)

> For the full evidence-backed table (method + route → controller → auth policy) see:
> [`docs/processes/process-universe.md#appendix-b--api-route-index`](../../processes/process-universe.md)

| Controller | Base Route | Auth Tier |
|------------|------------|-----------|
| HealthController | `api/warehouse/v1/health` | AllowAnonymous |
| StockController | `api/warehouse/v1/stock` | OperatorOrAbove |
| PickingController | `api/warehouse/v1/picking` | OperatorOrAbove / ManagerOrAdmin |
| ReceivingController | `api/warehouse/v1/receiving/shipments` | QcOrManager |
| AdjustmentsController | `api/warehouse/v1/adjustments` | ManagerOrAdmin |
| ReservationsController | `api/warehouse/v1/reservations` | OperatorOrAbove |
| AgnumController | `api/warehouse/v1/agnum` | InventoryAccountantOrManager |
| QCController | `api/warehouse/v1/qc` | QcOrManager |
| PutawayController | `api/warehouse/v1/putaway` | OperatorOrAbove / ManagerOrAdmin |
| TransfersController | `api/warehouse/v1/transfers` | OperatorOrAbove / ManagerOrAdmin |
| CycleCountsController | `api/warehouse/v1/cycle-counts` | OperatorOrAbove / ManagerOrAdmin |
| ShipmentsController | `api/warehouse/v1/shipments` | DispatchClerkOrManager |
| SalesOrdersController | `api/warehouse/v1/sales-orders` | SalesAdminOrManager |
| OutboundOrdersController | `api/warehouse/v1/outbound-orders` | SalesAdminOrManager |
| ValuationController | `api/warehouse/v1/valuation` | CfoOrAdmin |
| LotsController | `api/warehouse/v1/lots` | OperatorOrAbove / QcOrManager |
| LabelsController | `api/warehouse/v1/labels` | PackingOperatorOrManager |
| WarehouseVisualizationController | `api/warehouse/v1/visualization` | OperatorOrAbove |
| ReportsController | `api/warehouse/v1/reports` | ManagerOrAdmin / CfoOrAdmin |
| DashboardController | `api/warehouse/v1/dashboard` | OperatorOrAbove |
| MetricsController | `api/warehouse/v1/metrics` | OperatorOrAbove / ManagerOrAdmin |
| ProjectionsController | `api/warehouse/v1/projections` | AdminOnly |
| ItemsController | `api/warehouse/v1/items` | OperatorOrAbove → AdminOnly |
| SuppliersController | `api/warehouse/v1/suppliers` | OperatorOrAbove / ManagerOrAdmin |
| LocationsController | `api/warehouse/v1/locations` | OperatorOrAbove / ManagerOrAdmin |
| CategoriesController | `api/warehouse/v1/categories` | OperatorOrAbove / ManagerOrAdmin |
| UnitOfMeasuresController | `api/warehouse/v1/unit-of-measures` | OperatorOrAbove / ManagerOrAdmin |
| ItemUomConversionsController | `api/warehouse/v1/item-uom-conversions` | OperatorOrAbove / ManagerOrAdmin |
| HandlingUnitTypesController | `api/warehouse/v1/handling-unit-types` | OperatorOrAbove / ManagerOrAdmin |
| ImportController | `api/warehouse/v1/import` | ManagerOrAdmin |
| BarcodesController | `api/warehouse/v1/barcodes` | OperatorOrAbove |
| AlertEscalationController | `api/warehouse/v1/alerts` | OperatorOrAbove / ManagerOrAdmin |
| AdvancedWarehouseController | `api/warehouse/v1/waves` + `cross-dock` + `rma` + `serials` + `handling-units` + `qc-advanced` + `analytics` | varies |
| Admin* (12 controllers) | `api/warehouse/v1/admin/*` | AdminOnly / AdminOrAuditor |
| OAuthController | `api/auth/oauth` | AllowAnonymous / Authenticated |
| MfaController | `api/auth/mfa` | Authenticated |

### Authorization Policies

```
WarehousePolicies.AdminOnly                   → Admin
WarehousePolicies.ManagerOrAdmin              → Manager, Admin
WarehousePolicies.QcOrManager                 → QC Inspector, Manager, Admin
WarehousePolicies.OperatorOrAbove             → Operator, QC, Manager, Admin
WarehousePolicies.SalesAdminOrManager         → Sales Admin, Manager, Admin
WarehousePolicies.PackingOperatorOrManager    → Packing Operator, Manager, Admin
WarehousePolicies.DispatchClerkOrManager      → Dispatch Clerk, Manager, Admin
WarehousePolicies.InventoryAccountantOrManager→ Inventory Accountant, Manager, Admin
WarehousePolicies.CfoOrAdmin                  → CFO, Admin
WarehousePolicies.AdminOrAuditor              → Admin, Auditor
WarehousePolicies.ManagerOrAuditor            → Manager, Auditor
```

### TODO

- [ ] Expand with full endpoint-level table from Appendix B
- [ ] Add process ID cross-reference column (P-XX)
- [ ] Flag endpoints with missing auth policies (AdvancedWarehouseController nested classes)
