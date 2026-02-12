# Phase 1.5 UI Gap Audit (2026-02-12)

## Method

Compared all `PRD-1521..PRD-1600` tasks marked as `UI` or `UI/Backend` in:
- `docs/prod-ready/prod-ready-tasks-PHASE15-S3.md`
- `docs/prod-ready/prod-ready-tasks-PHASE15-S4.md`
- `docs/prod-ready/prod-ready-tasks-PHASE15-S5.md`
- `docs/prod-ready/prod-ready-tasks-PHASE15-S6.md`

against implemented Blazor routes under `src/LKvitai.MES.WebUI/Pages`.

Status legend:
- `IMPLEMENTED`: dedicated route/page exists and core workflow is wired.
- `PARTIAL`: route exists but significant acceptance scope is simplified or absent.
- `MISSING`: no corresponding Blazor page/workflow found.

## Results

| TaskId | Task | Status | Evidence |
|---|---|---|---|
| PRD-1523 | Receiving Invoice Entry UI | MISSING | No inbound create page route found (e.g. `/warehouse/inbound/shipments/create`) in `src/LKvitai.MES.WebUI/Pages` |
| PRD-1524 | Receiving Scan & QC Workflow UI | MISSING | No inbound receiving scan/QC execution page route found in `src/LKvitai.MES.WebUI/Pages` |
| PRD-1525 | Stock Visibility Dashboard UI | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/AvailableStock.razor` (`@page "/available-stock"`) |
| PRD-1526 | Stock Movement/Transfer UI | MISSING | No transfer UI route found (e.g. `/warehouse/transfers` or `/warehouse/transfers/create`) |
| PRD-1527 | Create Sales Order UI | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/SalesOrderCreate.razor` (`@page "/warehouse/sales/orders/create"`) |
| PRD-1528 | Sales Order List & Detail UI | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/SalesOrders.razor`, `src/LKvitai.MES.WebUI/Pages/SalesOrderDetail.razor` |
| PRD-1529 | Allocation & Release UI | IMPLEMENTED | Action buttons wired in `src/LKvitai.MES.WebUI/Pages/SalesOrderDetail.razor` (`Allocate`, `Release`) |
| PRD-1530 | Picking Workflow UI Enhancements | PARTIAL | Picking exists via reservations flow in `src/LKvitai.MES.WebUI/Pages/Reservations.razor`, but no dedicated order-based picking UI route |
| PRD-1531 | Packing Station UI Enhancements | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/PackingStation.razor` (`@page "/warehouse/outbound/pack/{OrderId:guid}"`) |
| PRD-1532 | Dispatch UI Enhancements | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/OutboundDispatch.razor` (`@page "/warehouse/outbound/dispatch"`) |
| PRD-1533 | Receiving History Report UI | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/ReportsReceivingHistory.razor` |
| PRD-1534 | Dispatch History Report UI | MISSING | No dispatch history report page in WebUI; only API history endpoint exists in `src/LKvitai.MES.Api/Api/Controllers/DispatchController.cs` |
| PRD-1548 | Admin User Management UI | MISSING | No `/admin/users` or equivalent page in `src/LKvitai.MES.WebUI/Pages` |
| PRD-1549 | Stock Movement History Report | MISSING | No stock movement history report page route found |
| PRD-1551 | Traceability Report (Lot -> Order) | MISSING | No traceability report page route found |
| PRD-1552 | Compliance Audit Report | MISSING | No compliance audit report page route found |
| PRD-1575 | Empty State & Error Handling UI | PARTIAL | `ErrorBanner`/`EmptyState` used in many pages, but coverage is not uniform across all workflow pages |
| PRD-1576 | Bulk Operations (Multi-Select) | IMPLEMENTED | Multi-select + bulk cancel/export in `src/LKvitai.MES.WebUI/Pages/OutboundOrders.razor` |
| PRD-1577 | Advanced Search & Filters | PARTIAL | Advanced filters implemented in selected pages (`OutboundOrders`, `AvailableStock`), not consistently across all targeted pages |
| PRD-1583 | Wave Picking UI & Execution | PARTIAL | Implemented page exists (`src/LKvitai.MES.WebUI/Pages/WavePicking.razor`) but relies on manual GUID entry and simplified execution UX |
| PRD-1585 | Cross-Dock UI & Tracking | PARTIAL | Implemented page exists (`src/LKvitai.MES.WebUI/Pages/CrossDock.razor`) but flow is simplified with manual IDs |
| PRD-1591 | RMA UI & Customer Portal | PARTIAL | RMA operator UI exists (`src/LKvitai.MES.WebUI/Pages/Rmas.razor`), customer portal slice not present |
| PRD-1596 | Fulfillment KPIs Dashboard | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/AnalyticsFulfillment.razor` |
| PRD-1597 | QC Defects & Late Shipments Analytics | IMPLEMENTED | `src/LKvitai.MES.WebUI/Pages/AnalyticsQuality.razor` |

## Summary

- Newly added now: `PRD-1527` and `PRD-1528` UI routes/pages, plus `PRD-1529` actions in sales order detail.
- Most visible remaining UI misses are inbound/transfer/report/admin-user slices (`PRD-1523`, `PRD-1524`, `PRD-1526`, `PRD-1534`, `PRD-1548`, `PRD-1549`, `PRD-1551`, `PRD-1552`).
