# P-13 — Reporting & Analytics

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Provides historical and current visibility into stock levels, movements, receiving, picking, dispatch, fulfillment KPIs, and quality metrics.

**Evidence:**
- UI: 15+ report and analytics routes — `ReportsStockLevel.razor`, `ReportsPickHistory.razor`, `ReportsDispatchHistory.razor`, `AnalyticsFulfillment.razor`, `AnalyticsQuality.razor`, etc.
- Controllers: `ReportsController`, `DashboardController`, `MetricsController`, `AdvancedWarehouseController` (AdvancedAnalyticsController)
- Typed client: `ReportsClient` (covers all report pages)
- Service: `ScheduledReportsRecurringJob.cs` (Hangfire)

---

## Trigger

On-demand query (manager, accountant, compliance officer), or scheduled Hangfire job.

## Outcomes

Reports rendered in UI or exported; KPI dashboards refreshed.

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Manager | Reads operational reports and KPIs |
| Inventory Accountant | Reads stock level and valuation reports |
| CFO | Reads aged-stock and financial reports |
| Compliance Officer / Auditor | Reads compliance audit and traceability reports |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/stock/dashboard` | `StockDashboard.razor` | Stock → Stock Dashboard |
| `/available-stock` | `AvailableStock.razor` | Stock → Available Stock |
| `/reservations` | `Reservations.razor` | Stock → Reservations |
| `/reports/stock-level` | `ReportsStockLevel.razor` | Reports → Stock Level |
| `/reports/receiving-history` | `ReportsReceivingHistory.razor` | Reports → Receiving History |
| `/reports/pick-history` | `ReportsPickHistory.razor` | Reports → Pick History |
| `/reports/dispatch-history` | `ReportsDispatchHistory.razor` | Reports → Dispatch History |
| `/reports/stock-movements` | `ReportsStockMovements.razor` | Reports → Stock Movements |
| `/reports/compliance-audit` | `ReportsComplianceAudit.razor` | Reports → Compliance Audit |
| `/analytics/fulfillment` | `AnalyticsFulfillment.razor` | Analytics → Fulfillment KPIs |
| `/analytics/quality` | `AnalyticsQuality.razor` | Analytics → Quality Analytics |
| `/dashboard` | `Dashboard.razor` | — (main dashboard) |
| `/projections` | `Projections.razor` | Operations → Projections |

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/reports/inventory` | ReportsController | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/movements` | ReportsController | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/utilization` | ReportsController | ManagerOrAdmin |
| GET | `api/warehouse/v1/reports/aged-stock` | ReportsController | CfoOrAdmin |
| GET | `api/warehouse/v1/dashboard/summary` | DashboardController | OperatorOrAbove |
| GET | `api/warehouse/v1/metrics/performance` | MetricsController | OperatorOrAbove |
| GET | `api/warehouse/v1/projections/status` | ProjectionsController | AdminOnly |
| POST | `api/warehouse/v1/projections/{name}/rebuild` | ProjectionsController | AdminOnly |

## Read Models Consumed

`LocationBalanceView` (≤5 s), `AvailableStockView` (≤5 s), `ActiveHardLockView` (instant), `HandlingUnitView` (async), `OnHandValueView`

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
