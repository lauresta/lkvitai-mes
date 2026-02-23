# Audit: Full Bidirectional Layer Cross-Check
**Date:** 2026-02-23 08:41
**Scope:** DB ↔ Service ↔ API ↔ WebUI ↔ Nav
**Branch:** frosty-ardinghelli
**Status:** Initial scan — awaiting fixes

---

## TODO (update as fixes land)

| ID | Severity | Status | Description |
|----|----------|--------|-------------|
| **B-01** | 🔴 Bug | `[ ] open` | `GET api/warehouse/v1/dispatch/history` (DispatchController) is dead — UI uses `/reports/dispatch-history`. Remove or merge. |
| **B-02** | 🔴 Bug | `[ ] open` | `QCPanel.razor` at `/warehouse/qc/pending` is a redirect-only dead page — contains no UI, just `NavigationManager.NavigateTo`. Delete or promote to real page. |
| **B-03** | 🔴 Bug | `[ ] open` | `POST api/test/simulate-capacity-alert` (CapacitySimulationController, `[Route("api/test")]`) — test endpoint exposed in production build. Remove from prod or gate behind env flag. |
| **B-04** | 🔴 Bug | `[ ] open` | Route conflict: `AdvancedWarehouseController` and `QCController` both declare `[Route("api/warehouse/v1/qc")]`. Overlapping actions will cause ambiguous route exceptions. Split routes. |
| **G-01** | 🟠 Gap | `[ ] open` | No UI page for API Key management (`api_keys` table, `AdminApiKeysController` — full CRUD exists in API). |
| **G-02** | 🟠 Gap | `[ ] open` | No UI page for GDPR erasure requests (`gdpr_erasure_requests`, `AdminGdprController`). |
| **G-03** | 🟠 Gap | `[ ] open` | No UI page for audit log browsing (`security_audit_logs`, `AdminAuditLogsController GET`). |
| **G-04** | 🟠 Gap | `[ ] open` | No UI page for backup management (`backup_executions`, `AdminBackupsController`). |
| **G-05** | 🟠 Gap | `[ ] open` | No UI page for retention policy management (`retention_policies` / `retention_executions`, `AdminRetentionPoliciesController`). |
| **G-06** | 🟠 Gap | `[ ] open` | No UI page for disaster recovery drills (`dr_drills`, `AdminDisasterRecoveryController`). |
| **G-07** | 🟠 Gap | `[ ] open` | Agnum export history (`agnum_export_history`): API endpoint `GET api/warehouse/v1/agnum/history` exists but no WebUI client method and no UI page. `AgnumClient` missing `GetHistoryAsync`. |
| **G-08** | 🟠 Gap | `[ ] open` | No UI page for serial number management (`serial_numbers`, `AdvancedWarehouseController /serials`). |
| **G-09** | 🟠 Gap | `[ ] open` | No UI page / editor for warehouse layout (`warehouse_layouts`, `GET/PUT api/warehouse/v1/layout`). 3D view works but layout definition is not editable from UI. |
| **G-10** | 🟠 Gap | `[ ] open` | AdminItems / Suppliers / Locations / Categories pages are **read-only** in UI. `MasterDataAdminClient` only calls GET. POST/PUT/DELETE endpoints exist in API but are never called from WebUI. Cannot create or update master data records from UI. |
| **G-11** | 🟡 Gap | `[ ] open` | `GET api/warehouse/v1/stock/location-balance` endpoint (StockController) exists and is wired to `GetLocationBalanceQueryHandler` but no WebUI client calls it. Marten LocationBalance read model unused by UI. |
| **G-12** | 🟡 Gap | `[ ] open` | `ValuationController POST(initialize)` and `POST({itemId}/adjust-cost)` exist but have no UI consumers. `ValuationClient` only calls base `adjust-cost`. |
| **G-13** | 🟡 Gap | `[ ] open` | `AdminComplianceController`: endpoints `POST(sign)`, `GET(signatures/{id})`, `POST(verify-hash-chain)`, `GET(validation-report)`, `POST(export-transactions)`, `GET(exports)` — not called by any WebUI client. Compliance pages only use dashboard, lot-trace, scheduled-reports. |
| **G-14** | 🟡 Gap | `[ ] open` | `BarcodesController GET api/warehouse/v1/barcodes/lookup` — not consumed by any WebUI client. |
| **G-15** | 🟡 Gap | `[ ] open` | `FeatureFlagsController GET api/warehouse/v1/features/{flagKey}` — not consumed by any WebUI client (LaunchDarkly used internally in services only). |
| **G-16** | 🟡 Gap | `[ ] open` | `AdvancedWarehouseController`: QC checklist-templates endpoints (POST/GET) and QC defects (GET all) have no WebUI consumer. |
| **G-17** | 🟡 Gap | `[ ] open` | `AdvancedWarehouseController`: HU split/merge/hierarchy endpoints have no WebUI consumer. |
| **G-18** | 🟡 Gap | `[ ] open` | `lots` table has no dedicated API endpoint and no UI page for lot listing/management. |
| **G-19** | 🟡 Gap | `[ ] open` | `handling_unit_types`, `unit_of_measures`, `item_uom_conversions` tables have no dedicated API or UI. Items reference UoM but no management screen exists. |
| **G-20** | 🟡 Gap | `[ ] open` | `permissions` table exposed via `AdminPermissionsController` (GET list + POST check) but no UI page. Roles page manages role-permission assignments but permission definitions are not manageable from UI. |
| **A-01** | 🔵 Arch | `[ ] open` | ARCH-02 (known): 34 service files in `Api/Services/` contain business logic that belongs in Application layer. Controllers inject `DbContext` and `IDocumentStore` directly, bypassing MediatR pipeline. |
| **A-02** | 🔵 Arch | `[ ] open` | `WarehouseLocationDetail.razor` at `/warehouse/locations/{Id:int}` — not reachable from nav, injected client unverified. Likely navigated from AdminLocations. Needs explicit nav path or deregistration. |

---

## Step 0: Architecture Detection

**DB Schema Source-of-Truth:** EF Core Model Snapshot (`WarehouseDbContextModelSnapshot.cs`). 34 migration files found.
Additionally, Marten document store (event-sourced + projections) for: StockLedger, Reservation, HandlingUnit, ActiveHardLock, AvailableStock, LocationBalance streams.

**Service/Use-Case Conventions:** MediatR pipeline for Application-layer commands/queries (9 handlers). Large ARCH-02 tech debt: 34 service files in `Api/Services/` with direct DB/Marten access (bypass Application layer). Both sets act as service layer.

**API Conventions:** MVC Controllers, route prefix `api/warehouse/v1/` (plus `api/dashboard/`, `api/reservations/`, `api/sales-orders/`, `api/projections/`, `api/admin/`, `api/auth/`, `api/monitoring/`, `api/test/`, `metrics/`). 54 source controllers.

**UI Framework:** Blazor Server. Route declarations via `@page`. HTTP client pattern: typed service classes in `WebUI/Services/` (`*Client.cs`), 22 files.

**Navigation Source:** `NavMenu.razor` — data-driven accordion from static `_sections` array. **Dashboard** item hard-coded at top.

---

## Step 1: Inventories

### A) DB Inventory (EF Core — WarehouseDbContextModelSnapshot)

| # | Table | Source |
|---|-------|--------|
| 1 | adjustment_reason_codes | Snapshot |
| 2 | agnum_export_configs | Snapshot |
| 3 | agnum_export_history | Snapshot |
| 4 | agnum_mappings | Snapshot |
| 5 | api_keys | Snapshot |
| 6 | approval_rules | Snapshot |
| 7 | audit_logs_archive | Snapshot |
| 8 | backup_executions | Snapshot |
| 9 | customers | Snapshot |
| 10 | cycle_counts | Snapshot |
| 11 | cycle_count_lines | Snapshot |
| 12 | dispatch_history | Snapshot |
| 13 | dr_drills | Snapshot |
| 14 | electronic_signatures | Snapshot |
| 15 | event_processing_checkpoints | Snapshot |
| 16 | events_archive | Snapshot |
| 17 | gdpr_erasure_requests | Snapshot |
| 18 | generated_report_history | Snapshot |
| 19 | handling_unit_lines | Snapshot (owned type) |
| 20 | handling_units | Snapshot (Aggregate) |
| 21 | handling_unit_types | Snapshot |
| 22 | inbound_shipment_lines | Snapshot |
| 23 | inbound_shipments | Snapshot |
| 24 | item_barcodes | Snapshot |
| 25 | item_categories | Snapshot |
| 26 | item_uom_conversions | Snapshot |
| 27 | items | Snapshot |
| 28 | locations | Snapshot |
| 29 | lots | Snapshot |
| 30 | on_hand_value | Snapshot |
| 31 | outbound_order_lines | Snapshot |
| 32 | outbound_orders | Snapshot |
| 33 | outbound_order_summary | Snapshot |
| 34 | permissions | Snapshot |
| 35 | pick_tasks | Snapshot |
| 36 | pii_encryption_keys | Snapshot |
| 37 | retention_executions | Snapshot |
| 38 | retention_policies | Snapshot |
| 39 | role_permissions | Snapshot |
| 40 | roles | Snapshot |
| 41 | sales_order_lines | Snapshot |
| 42 | sales_orders | Snapshot |
| 43 | scheduled_reports | Snapshot |
| 44 | security_audit_logs | Snapshot |
| 45 | serial_numbers | Snapshot |
| 46 | shipment_lines | Snapshot |
| 47 | shipments | Snapshot |
| 48 | shipment_summary | Snapshot |
| 49 | sku_sequences | Snapshot |
| 50 | supplier_item_mappings | Snapshot |
| 51 | suppliers | Snapshot |
| 52 | transaction_exports | Snapshot |
| 53 | transfer_lines | Snapshot |
| 54 | transfers | Snapshot |
| 55 | unit_of_measures | Snapshot |
| 56 | user_mfa | Snapshot |
| 57 | user_role_assignments | Snapshot |
| 58 | warehouse_layouts | Snapshot |
| 59 | warehouse_settings | Snapshot |
| 60 | warehouses | Snapshot |
| 61 | zone_definitions | Snapshot |
| M1 | StockLedger (event stream) | MartenStockLedgerRepository.cs |
| M2 | Reservation (event stream) | MartenReservationRepository.cs |
| M3 | LocationBalanceView (Marten doc) | MartenLocationBalanceRepository.cs |
| M4 | AvailableStockView (Marten doc) | MartenAvailableStockRepository.cs |
| M5 | ActiveHardLockView (Marten doc) | MartenActiveHardLocksRepository.cs |
| M6 | HandlingUnitView (Marten doc) | Projections |

**Total: 61 EF tables + 6 Marten = 67 DB objects**

### B) Services Inventory

**Application Layer (MediatR):**

| Handler | File |
|---------|------|
| AllocateReservationCommandHandler | Application/Commands/ |
| PickStockCommandHandler | Application/Commands/ |
| RebuildProjectionCommandHandler | Application/Commands/ |
| ReceiveGoodsCommandHandler | Application/Commands/ |
| RecordStockMovementCommandHandler | Application/Commands/ |
| StartPickingCommandHandler | Application/Commands/ |
| GetAvailableStockQueryHandler | Application/Queries/ |
| GetLocationBalanceQueryHandler | Application/Queries/ |
| SearchReservationsQueryHandler | Application/Queries/ |

**Infrastructure/Api Services (ARCH-02 tech debt — business logic in wrong layer):**

AgnumExportServices, AgnumReconciliationServices, AlertEscalationService, ApiKeyService, ApprovalRuleService, BackupServices, BusinessTelemetryService, CapacityPlanningService, CycleCountServices, DisasterRecoveryService, FeatureFlagService, GdprErasureService, LandedCostAllocationService, LabelPrintingServices, LotTraceabilityService, MfaService, OutboundOrderCommandHandlers, PiiEncryptionService, ReasonCodeService, RetentionPolicyService, RoleManagementService, SalesOrderCommandHandlers, SecurityAuditLogService, ShipmentCommandHandlers, SlaMonitoringService, TransactionExportService, TransferServices, ValuationCommandHandlers, ValuationLifecycleCommandHandlers, WarehouseSettingsService, IdempotencyCleanupService, ProjectionHealthService, ProjectionRebuildService, MasterDataImportService, SkuGenerationService, RedisCacheService

### C) API Inventory (~130 endpoints across 54 controllers)

```
Controller                       Route Prefix                                    Endpoints
---------------------------------|-----------------------------------------------|--------------------------------------------------
AdjustmentsController            api/warehouse/v1/adjustments                   POST, GET
AdminApiKeysController           api/warehouse/v1/admin/api-keys                GET, POST, PUT({id}/rotate), DELETE({id})
AdminApprovalRulesController     api/warehouse/v1/admin/approval-rules          GET, POST, PUT({id}), DELETE({id}), POST(evaluate)
AdminAuditLogsController         api/warehouse/v1/admin/audit-logs              GET
AdminBackupsController           api/warehouse/v1/admin/backups                 POST(trigger), GET, POST(restore)
AdminCapacityController          api/admin/capacity                             GET(report)
AdminComplianceController        api/warehouse/v1/admin/compliance              GET(dashboard), GET/POST/PUT/DELETE(scheduled-reports*),
                                                                                POST(sign), GET(signatures/{id}),
                                                                                POST(verify-hash-chain), GET(validation-report),
                                                                                POST(export-transactions), GET(exports),
                                                                                POST/GET(lot-trace)
AdminDisasterRecoveryController  api/warehouse/v1/admin/dr                      POST(drill), GET(drills)
AdminEncryptionController        api/warehouse/v1/admin/encryption              POST(rotate-key)
AdminGdprController              api/warehouse/v1/admin/gdpr                    POST(erasure-request), GET, PUT(approve), PUT(reject)
AdminPermissionsController       api/warehouse/v1/admin                         GET(permissions), POST(permissions/check)
AdminReasonCodesController       api/warehouse/v1/admin/reason-codes            GET, POST, PUT({id}), DELETE({id})
AdminRetentionPoliciesController api/warehouse/v1/admin/retention-policies      GET, POST, PUT({id}), DELETE({id}), POST(execute),
                                                                                PUT(legal-hold/{id})
AdminRolesController             api/warehouse/v1/admin                         GET/POST/PUT/DELETE(roles), POST(users/{id}/roles)
AdminSettingsController          api/warehouse/v1/admin/settings                GET, PUT
AdminSlaController               api/admin/sla                                  POST(report)
AdminUsersController             api/admin/users + api/warehouse/v1/admin/users GET, POST, PUT({id})
AdvancedWarehouseController      api/warehouse/v1/waves                         POST, GET, GET({id}), POST(assign/start/complete-lines)
                                 api/warehouse/v1/cross-dock                    POST, GET, POST({id}/status)
                                 api/warehouse/v1/qc [⚠ CONFLICT]              POST(checklist-templates), GET, POST(defects), GET,
                                                                                POST(defects/{id}/attachments)
                                 api/warehouse/v1/rmas                          POST, GET, POST(receive), POST(inspect)
                                 api/warehouse/v1/handling-units                POST(split), POST(merge), GET(hierarchy)
                                 api/warehouse/v1/serials                       POST, POST(status), GET
                                 api/warehouse/v1/analytics                     GET(fulfillment-kpis), GET(qc-late-shipments)
AgnumController                  api/warehouse/v1/agnum                         GET(config), PUT(config), POST(test-connection),
                                                                                POST(export), GET(history), GET(history/{id}),
                                                                                POST(reconcile), GET(reconcile/{id})
AlertEscalationController        api/monitoring/v1/alerts                       POST(escalation)
BarcodesController               api/warehouse/v1/barcodes                      GET(lookup)
CapacitySimulationController     api/test [⚠ PROD EXPOSED]                     POST(simulate-capacity-alert)
CategoriesController             api/warehouse/v1/categories                    GET, POST, PUT({id}), DELETE({id})
CustomersController              api/warehouse/v1/customers                     GET, GET({id}), POST
CycleCountsController            api/warehouse/v1/cycle-counts                  GET, GET({id}), POST(schedule/record-count),
                                                                                GET(lines/discrepancies), POST(approve-adjustment)
DashboardController              api/dashboard                                  GET(health/projection-health/stock-summary/
                                                                                reservation-summary/recent-activity)
DispatchController               api/warehouse/v1/dispatch                      GET(history) [⚠ DEAD — duplicates /reports/dispatch-history]
FeatureFlagsController           api/warehouse/v1/features                      GET({flagKey})
HealthController                 api/warehouse/v1/health + /health              GET, GET
IdempotencyController            api/admin/idempotency +                        POST(cleanup)
                                 api/warehouse/v1/admin/idempotency
ImportController                 api/warehouse/v1/admin/import                  GET(template), POST({entityType}), POST(error-report)
ItemsController                  api/warehouse/v1/items                         GET, POST, GET({id}), PUT({id}), POST(deactivate),
                                                                                GET(barcodes), POST(barcodes)
LabelsController                 api/warehouse/v1/labels                        POST(print/preview), GET(templates/preview/queue/pdf),
                                                                                POST(queue/{id}/retry)
LocationsController              api/warehouse/v1/locations                     GET, POST, PUT({id}), PUT({code:regex}),
                                                                                POST(bulk-coordinates)
MetricsController                metrics/                                        GET (Prometheus)
MfaController                    api/auth/mfa                                   POST(enroll/verify-enrollment/verify/reset),
                                                                                GET(backup-codes)
OAuthController                  api/auth/oauth                                 GET(login/callback), POST(logout)
OutboundOrdersController         api/warehouse/v1/outbound/orders               POST({id}/pack), GET(summary), GET({id})
PickingController                api/warehouse/v1/picking                       POST(tasks), GET(tasks/{id}/locations),
                                                                                POST(tasks/{id}/complete), GET(history)
ProjectionsController            api/projections +                              POST(rebuild/verify/cleanup-shadows),
                                 api/warehouse/v1/admin/projections             GET(rebuild-status)
PutawayController                api/warehouse/v1/putaway                       GET(tasks), POST
QCController                     api/warehouse/v1/qc [⚠ CONFLICT]              GET(pending), POST(pass), POST(fail)
ReceivingController              api/warehouse/v1/receiving/shipments +         GET, GET({id}), POST, POST(receive/receive-items)
                                 api/warehouse/v1/inbound-shipments
ReportsController                api/warehouse/v1/reports                       GET(dispatch-history/stock-movements/
                                                                                traceability/compliance-audit)
ReservationsController           api/reservations +                             GET, POST({id}/start-picking), POST({id}/pick)
                                 api/warehouse/v1/reservations
SalesOrdersController            api/sales-orders +                             POST, POST(submit/approve/allocate/release/cancel),
                                 api/warehouse/v1/sales-orders                  GET, GET({id})
ShipmentsController              api/warehouse/v1/shipments                     POST({id}/dispatch), GET(summary)
StockController                  api/warehouse/v1/stock                         GET(available), GET(location-balance)
SupplierItemMappingsController   api/warehouse/v1/supplier-item-mappings        GET, POST, PUT({id})
SuppliersController              api/warehouse/v1/suppliers                     GET, POST, PUT({id})
TransfersController              api/warehouse/v1/transfers                     GET, GET({id}), POST, POST(submit/approve/execute)
ValuationController              api/warehouse/v1/valuation                     POST(initialize/adjust-cost/apply-landed-cost/write-down),
                                                                                POST({itemId}/adjust-cost),
                                                                                GET(on-hand-value/cost-history)
WarehouseVisualizationController api/warehouse/v1                               GET(layout), PUT(layout), GET(visualization/3d)
```

### D) UI Pages Inventory (63 razor files with `@page`)

| Route | File | Client Injected |
|-------|------|----------------|
| / | Index.razor | NavigationManager (redirect → /dashboard) |
| /dashboard | Dashboard.razor | DashboardClient |
| /available-stock | AvailableStock.razor | StockClient |
| /warehouse/stock/dashboard | StockDashboard.razor | ReportsClient |
| /warehouse/stock/adjustments | StockAdjustments.razor | AdjustmentsClient |
| /reservations | Reservations.razor | ReservationsClient |
| /warehouse/inbound/shipments | InboundShipments.razor | ReceivingClient |
| /warehouse/inbound/shipments/create | InboundShipmentCreate.razor | ReceivingClient, MasterDataAdminClient |
| /warehouse/inbound/shipments/{Id:int} | InboundShipmentDetail.razor | ReceivingClient |
| /warehouse/inbound/qc | ReceivingQc.razor | ReceivingClient |
| /warehouse/putaway | Putaway.razor | PutawayClient, MasterDataAdminClient |
| /warehouse/sales/orders | SalesOrders.razor | SalesOrdersClient |
| /warehouse/sales/orders/create | SalesOrderCreate.razor | SalesOrdersClient |
| /warehouse/sales/orders/{Id:guid} | SalesOrderDetail.razor | SalesOrdersClient |
| /warehouse/sales/allocations | AllocationDashboard.razor | SalesOrdersClient |
| /warehouse/outbound/orders | OutboundOrders.razor | OutboundClient |
| /warehouse/outbound/orders/{Id:guid} | OutboundOrderDetail.razor | OutboundClient |
| /warehouse/outbound/dispatch | OutboundDispatch.razor | OutboundClient |
| /warehouse/outbound/pack/{OrderId:guid} | PackingStation.razor | OutboundClient |
| /warehouse/waves | WavePicking.razor | AdvancedWarehouseClient |
| /warehouse/picking/tasks | PickingTasks.razor | PickingTasksClient, MasterDataAdminClient |
| /warehouse/labels | Labels.razor | LabelsClient |
| /warehouse/cross-dock | CrossDock.razor | AdvancedWarehouseClient |
| /warehouse/rmas | Rmas.razor | AdvancedWarehouseClient |
| /warehouse/transfers | Transfers/List.razor | TransfersClient |
| /warehouse/transfers/create | Transfers/Create.razor | TransfersClient |
| /warehouse/transfers/{Id:guid}/execute | Transfers/Execute.razor | TransfersClient |
| /warehouse/cycle-counts | CycleCounts/List.razor | CycleCountsClient |
| /warehouse/cycle-counts/schedule | CycleCounts/Schedule.razor | CycleCountsClient, MasterDataAdminClient |
| /warehouse/cycle-counts/{Id:guid}/execute | CycleCounts/Execute.razor | CycleCountsClient |
| /warehouse/cycle-counts/{Id:guid}/discrepancies | CycleCounts/Discrepancies.razor | CycleCountsClient |
| /warehouse/visualization/3d + /2d | Visualization/Warehouse3D.razor | VisualizationClient |
| /projections | Projections.razor | ProjectionsClient |
| /warehouse/valuation/dashboard | Valuation/Dashboard.razor | ValuationClient |
| /warehouse/valuation/adjust-cost | Valuation/AdjustCost.razor | ValuationClient, MasterDataAdminClient |
| /warehouse/valuation/apply-landed-cost | Valuation/ApplyLandedCost.razor | ValuationClient, OutboundClient |
| /warehouse/valuation/write-down | Valuation/WriteDown.razor | ValuationClient, MasterDataAdminClient |
| /warehouse/agnum/config | Agnum/Configuration.razor | AgnumClient, MasterDataAdminClient |
| /warehouse/agnum/reconcile | Agnum/Reconciliation.razor | AgnumClient |
| /admin/users | AdminUsers.razor | AdminUsersClient |
| /warehouse/admin/settings | Admin/Settings.razor | AdminConfigurationClient |
| /warehouse/admin/reason-codes | Admin/ReasonCodes.razor | AdminConfigurationClient |
| /warehouse/admin/approval-rules | Admin/ApprovalRules.razor | AdminConfigurationClient |
| /warehouse/admin/roles | Admin/Roles.razor | AdminConfigurationClient |
| /admin/items | AdminItems.razor | MasterDataAdminClient |
| /admin/suppliers | AdminSuppliers.razor | MasterDataAdminClient |
| /admin/supplier-mappings | AdminSupplierMappings.razor | MasterDataAdminClient |
| /admin/locations | AdminLocations.razor | MasterDataAdminClient |
| /admin/categories | AdminCategories.razor | MasterDataAdminClient |
| /admin/import | AdminImport.razor | MasterDataAdminClient |
| /reports/stock-level | ReportsStockLevel.razor | ReportsClient |
| /reports/receiving-history | ReportsReceivingHistory.razor | ReportsClient |
| /reports/pick-history | ReportsPickHistory.razor | ReportsClient |
| /reports/dispatch-history | ReportsDispatchHistory.razor | ReportsClient |
| /reports/stock-movements | ReportsStockMovements.razor | ReportsClient |
| /reports/traceability | ReportsTraceability.razor | ReportsClient |
| /reports/compliance-audit | ReportsComplianceAudit.razor | ReportsClient |
| /warehouse/compliance/lot-trace | ComplianceLotTrace.razor | ReportsClient |
| /warehouse/compliance/dashboard | ComplianceDashboard.razor | ReportsClient |
| /analytics/fulfillment | AnalyticsFulfillment.razor | AdvancedWarehouseClient |
| /analytics/quality | AnalyticsQuality.razor | AdvancedWarehouseClient |
| /warehouse/locations/{Id:int} | WarehouseLocationDetail.razor | Unknown (needs verification) |
| /warehouse/qc/pending | QCPanel.razor | NavigationManager redirect only ⚠ |

### E) Nav Inventory (NavMenu.razor — 45 items)

| Group | Nav Label | Target Href | Page Exists? |
|-------|-----------|-------------|-------------|
| (top) | Dashboard | /dashboard | ✅ |
| stock | Available Stock | /available-stock | ✅ |
| stock | Stock Dashboard | /warehouse/stock/dashboard | ✅ |
| stock | Adjustments | /warehouse/stock/adjustments | ✅ |
| stock | Reservations | /reservations | ✅ |
| inbound | Inbound Shipments | /warehouse/inbound/shipments | ✅ |
| inbound | Receiving QC | /warehouse/inbound/qc | ✅ |
| inbound | Putaway | /warehouse/putaway | ✅ |
| outbound | Sales Orders | /warehouse/sales/orders | ✅ |
| outbound | Allocations | /warehouse/sales/allocations | ✅ |
| outbound | Outbound Orders | /warehouse/outbound/orders | ✅ |
| outbound | Dispatch | /warehouse/outbound/dispatch | ✅ |
| outbound | Wave Picking | /warehouse/waves | ✅ |
| outbound | Picking Tasks | /warehouse/picking/tasks | ✅ |
| outbound | Labels | /warehouse/labels | ✅ |
| outbound | Cross-Dock | /warehouse/cross-dock | ✅ |
| outbound | RMAs | /warehouse/rmas | ✅ |
| operations | Transfers | /warehouse/transfers | ✅ |
| operations | Cycle Counts | /warehouse/cycle-counts | ✅ |
| operations | Warehouse Map | /warehouse/visualization/3d | ✅ |
| operations | Projections | /projections | ✅ |
| finance | Valuation | /warehouse/valuation/dashboard | ✅ |
| finance | Agnum Config | /warehouse/agnum/config | ✅ |
| finance | Agnum Reconcile | /warehouse/agnum/reconcile | ✅ |
| admin | Users | /admin/users | ✅ |
| admin | Admin Settings | /warehouse/admin/settings | ✅ |
| admin | Reason Codes | /warehouse/admin/reason-codes | ✅ |
| admin | Approval Rules | /warehouse/admin/approval-rules | ✅ |
| admin | Roles | /warehouse/admin/roles | ✅ |
| admin | Items | /admin/items | ✅ |
| admin | Suppliers | /admin/suppliers | ✅ |
| admin | Supplier Mappings | /admin/supplier-mappings | ✅ |
| admin | Locations | /admin/locations | ✅ |
| admin | Categories | /admin/categories | ✅ |
| admin | Import Wizard | /admin/import | ✅ |
| reports | Stock Level | /reports/stock-level | ✅ |
| reports | Receiving History | /reports/receiving-history | ✅ |
| reports | Pick History | /reports/pick-history | ✅ |
| reports | Dispatch History | /reports/dispatch-history | ✅ |
| reports | Stock Movements | /reports/stock-movements | ✅ |
| reports | Traceability | /reports/traceability | ✅ |
| reports | Lot Traceability | /warehouse/compliance/lot-trace | ✅ |
| reports | Compliance Audit | /reports/compliance-audit | ✅ |
| reports | Compliance Dashboard | /warehouse/compliance/dashboard | ✅ |
| analytics | Fulfillment KPIs | /analytics/fulfillment | ✅ |
| analytics | Quality Analytics | /analytics/quality | ✅ |

**Nav dead links: 0**

---

## Step 2: Trace Graphs

### Service → API (MediatR Application-layer — properly layered)

| Application Handler | Controller | Route |
|--------------------|-----------|-------|
| AllocateReservationCommandHandler | SalesOrdersController | POST /api/warehouse/v1/sales-orders/{id}/allocate |
| StartPickingCommandHandler | ReservationsController | POST /api/warehouse/v1/reservations/{id}/start-picking |
| PickStockCommandHandler | ReservationsController | POST /api/warehouse/v1/reservations/{id}/pick |
| ReceiveGoodsCommandHandler | ReceivingController | POST /api/warehouse/v1/inbound-shipments/{id}/receive-items |
| RecordStockMovementCommandHandler | AdjustmentsController | POST /api/warehouse/v1/adjustments |
| RebuildProjectionCommandHandler | ProjectionsController | POST /api/warehouse/v1/admin/projections/rebuild |
| GetAvailableStockQueryHandler | StockController | GET /api/warehouse/v1/stock/available |
| GetLocationBalanceQueryHandler | StockController | GET /api/warehouse/v1/stock/location-balance |
| SearchReservationsQueryHandler | ReservationsController | GET /api/warehouse/v1/reservations |

All remaining controllers (AgnumController, CycleCountsController, TransfersController, ValuationController, etc.) call directly into `Api/Services/` or inject EF `DbContext` / `IDocumentStore` — **ARCH-02 bypass**.

---

## Step 3: Reports

### 1) Counts

```
DB OBJECTS
  EF Core tables:            61
  Marten streams/documents:   6
  Total:                     67
  Confirmed DB orphans:       0  (all have some service reference)

APPLICATION HANDLERS (MediatR)
  Total:                      9
  Referenced by API:          9
  Orphan (no API):            0

API ENDPOINTS
  Total controllers:         54
  Total endpoints:         ~130
  Used by UI clients:      ~100
  Unused by UI:             ~30  (see Gap section)
  Missing service behind:     0  (all have service or direct DB)

UI PAGES (@page)
  Total:                     63
  With API client injected:  60
  Without API (redirects):    2  (Index.razor, QCPanel.razor)
  Client unknown:             1  (WarehouseLocationDetail.razor)
  In nav (direct target):    45
  Not in nav (sub/detail):   18

NAV ITEMS
  Total:                     45
  Valid targets:             45
  Dead links:                 0
```

### 2) Orphans / Gaps

#### A) DB Orphans — Tables with no UI access path

| Table | Gap Description |
|-------|----------------|
| `lots` | No dedicated Lots endpoint. No UI page. `lots` created via receiving, never listed. |
| `handling_unit_types` | No dedicated endpoint or UI. |
| `unit_of_measures` | No UoM endpoint or UI management screen. |
| `item_uom_conversions` | No dedicated endpoint or UI. |
| `dr_drills` | `GET(drills)` exists in `AdminDisasterRecoveryController` but no UI page. |
| `backup_executions` | API exists (`/admin/backups`) but no UI page or nav entry. |
| `retention_executions` | No UI for retention execution history. |
| `transaction_exports` | `POST(export-transactions)` in AdminComplianceController — no UI consumer. |
| `serial_numbers` | API exists (`/serials`) but no UI page. |
| `permissions` | `AdminPermissionsController` (GET list) exists but no UI page. |
| `user_mfa` | `MfaController` exists but no Blazor UI (auth flow only). |
| `gdpr_erasure_requests` | `AdminGdprController` exists but no UI page. |
| `pii_encryption_keys` | `POST(rotate-key)` exists but no UI page. |
| `events_archive`, `audit_logs_archive` | Internal archival — intentionally opaque. |
| `event_processing_checkpoints`, `sku_sequences` | Internal only. No UI needed. |
| `agnum_export_history` | API endpoint exists but no UI client method or page (see G-07). |

#### B) Service Orphans

| Service | Gap |
|---------|-----|
| `AlertEscalationService` → `AlertEscalationController` | No UI consumer. Internal monitoring integration. |
| `AdminSlaController` (`api/admin/sla/report`) | No UI or nav entry. |
| `CapacitySimulationController` (`api/test/`) | **Test endpoint exposed in production.** |
| `AdminCapacityController` (`api/admin/capacity/report`) | No UI or nav entry. |
| `BarcodesController` | No WebUI client calls this. |
| `FeatureFlagsController` | Not consumed by any WebUI client. |
| `OAuthController`, `MfaController` | Server-side auth flows — no Blazor consumption expected. |
| `IdempotencyController` | Admin background operation — no UI. |
| `DispatchController GET(history)` | **Dead duplicate** — UI uses `/reports/dispatch-history`. |

#### C) API Endpoint Orphans (not called by any WebUI client)

| Endpoint | Controller | Issue |
|----------|-----------|-------|
| GET api/warehouse/v1/dispatch/history | DispatchController | **Dead — UI uses `/reports/dispatch-history`** |
| POST api/test/simulate-capacity-alert | CapacitySimulationController | Test endpoint in production |
| GET api/admin/capacity/report | AdminCapacityController | No UI consumer |
| POST api/admin/sla/report | AdminSlaController | No UI consumer |
| POST api/monitoring/v1/alerts/escalation | AlertEscalationController | Internal only |
| GET api/warehouse/v1/features/{key} | FeatureFlagsController | Not consumed by WebUI |
| GET api/warehouse/v1/barcodes/lookup | BarcodesController | Not consumed by WebUI |
| GET api/warehouse/v1/stock/location-balance | StockController | Not consumed by any WebUI client |
| GET/PUT api/warehouse/v1/layout | WarehouseVisualizationController | Not consumed by WebUI |
| GET api/warehouse/v1/agnum/history + history/{id} | AgnumController | AgnumClient has no history method |
| GET api/warehouse/v1/items/{id} | ItemsController | Not called from UI |
| POST api/warehouse/v1/items + PUT({id}) + POST(deactivate) | ItemsController | AdminItems page is read-only |
| POST/GET api/warehouse/v1/items/{id}/barcodes | ItemsController | No UI consumer |
| GET api/warehouse/v1/customers/{id} | CustomersController | No UI consumer |
| POST api/warehouse/v1/customers | CustomersController | No UI for customer creation |
| PUT api/warehouse/v1/supplier-item-mappings/{id} | SupplierItemMappingsController | MasterDataAdminClient GET-only |
| PUT api/warehouse/v1/suppliers/{id} | SuppliersController | MasterDataAdminClient GET-only |
| POST api/warehouse/v1/locations/bulk-coordinates | LocationsController | No UI consumer |
| POST api/warehouse/v1/categories + PUT({id}) | CategoriesController | AdminCategories GET+DELETE only |
| POST api/warehouse/v1/valuation/initialize | ValuationController | No UI consumer |
| POST api/warehouse/v1/valuation/{itemId}/adjust-cost | ValuationController | UI uses base adjust-cost |
| GET api/warehouse/v1/admin/audit-logs | AdminAuditLogsController | No UI page |
| POST/GET api/warehouse/v1/admin/backups/* | AdminBackupsController | No UI page |
| POST/GET api/warehouse/v1/admin/dr/* | AdminDisasterRecoveryController | No UI page |
| POST api/warehouse/v1/admin/encryption/rotate-key | AdminEncryptionController | No UI page |
| POST/GET api/warehouse/v1/admin/gdpr/* | AdminGdprController | No UI page |
| GET/POST/PUT/DELETE api/warehouse/v1/admin/retention-policies/* | AdminRetentionPoliciesController | No UI page |
| GET/POST api/warehouse/v1/admin/permissions + check | AdminPermissionsController | No UI page |
| POST/GET api/warehouse/v1/admin/compliance/sign + signatures + verify-hash-chain + validation-report + export-transactions + exports | AdminComplianceController | Not called by any WebUI client |
| POST/GET api/warehouse/v1/qc/checklist-templates | AdvancedWarehouseController | No UI consumer |
| POST/GET api/warehouse/v1/qc/defects (list/create) | AdvancedWarehouseController | No UI consumer |
| POST/GET api/warehouse/v1/serials/* | AdvancedWarehouseController | No UI page |
| POST/GET api/warehouse/v1/handling-units/{id}/split,merge,hierarchy | AdvancedWarehouseController | No UI page |
| POST api/warehouse/v1/admin/import/error-report | ImportController | Not called by UI |
| POST api/admin/idempotency/cleanup | IdempotencyController | Background job only |

#### D) UI Orphans

| UI Page | Route | Issue |
|---------|-------|-------|
| QCPanel.razor | /warehouse/qc/pending | **Redirect-only** — immediately `NavigationManager.NavigateTo("/warehouse/inbound/qc")`. No content. Dead code. |
| Index.razor | / | Redirect to /dashboard. Acceptable. |
| WarehouseLocationDetail.razor | /warehouse/locations/{Id:int} | Not in nav. Client injection unverified. Needs check. |

#### E) Nav Orphans

```
NAV DEAD LINKS: 0
All 45 nav items point to existing @page routes.
```

**Partial backend chains (nav → page works, but API capability limited):**

| Nav → Page | Issue |
|-----------|-------|
| /admin/items → AdminItems.razor | Read-only in UI. POST/PUT/DELETE exist in API, never called from UI. |
| /admin/suppliers → AdminSuppliers.razor | Read-only in UI. PUT exists in API, never called. |
| /admin/locations → AdminLocations.razor | Read-only in UI. POST/PUT exist in API, never called. |
| /admin/categories → AdminCategories.razor | GET + DELETE only in UI. POST/PUT exist in API, never called. |
| /admin/supplier-mappings → AdminSupplierMappings.razor | Read-only in UI. PUT exists in API, never called. |
| /reports/receiving-history → ReportsReceivingHistory.razor | Calls `/receiving/shipments` — reuses inbound-shipments list endpoint, not a dedicated history endpoint. |

---

## Step 4: Trace Tables

### T1: DB_OBJECT → SERVICE → API → UI → NAV

```
DB_OBJECT                | SERVICE/USECASE                   | API_ENDPOINT                                              | UI_PAGE/ROUTE                      | NAV_ITEM
-------------------------|-----------------------------------|-----------------------------------------------------------|------------------------------------|------------------
inbound_shipments        | ReceivingController (direct EF)   | GET/POST api/warehouse/v1/inbound-shipments               | /warehouse/inbound/shipments       | Inbound Shipments
inbound_shipment_lines   | ReceivingController               | POST .../receive-items                                    | /warehouse/inbound/shipments/{id}  | (sub-page)
stock (M1 StockLedger)   | RecordStockMovementCmdHandler     | POST api/warehouse/v1/adjustments                         | /warehouse/stock/adjustments       | Adjustments
stock (M4 AvailStock)    | GetAvailableStockQueryHandler     | GET api/warehouse/v1/stock/available                      | /available-stock                   | Available Stock
stock (M4 AvailStock)    | GetAvailableStockQueryHandler     | GET api/warehouse/v1/stock/available                      | /warehouse/stock/dashboard         | Stock Dashboard
reservation (M2)         | SearchReservationsQueryHandler    | GET api/warehouse/v1/reservations                         | /reservations                      | Reservations
reservation (M2)         | StartPickingCmdHandler            | POST /reservations/{id}/start-picking                     | /reservations                      | Reservations
reservation (M2)         | PickStockCmdHandler               | POST /reservations/{id}/pick                              | /reservations                      | Reservations
pick_tasks               | PickingController (direct EF)     | POST/GET api/warehouse/v1/picking/tasks                   | /warehouse/picking/tasks           | Picking Tasks
cycle_counts             | CycleCountServices (Api/Svc)      | GET/POST api/warehouse/v1/cycle-counts                    | /warehouse/cycle-counts            | Cycle Counts
cycle_count_lines        | CycleCountServices                | GET .../lines, .../discrepancies                          | /warehouse/cycle-counts/{id}/*     | (sub-pages)
transfers                | TransferServices (Api/Svc)        | GET/POST api/warehouse/v1/transfers                       | /warehouse/transfers               | Transfers
transfer_lines           | TransferServices                  | Embedded in transfers endpoints                           | /warehouse/transfers               | Transfers
sales_orders             | SalesOrderCommandHandlers (Api)   | GET/POST api/warehouse/v1/sales-orders                    | /warehouse/sales/orders            | Sales Orders
sales_order_lines        | SalesOrderCommandHandlers         | Embedded                                                  | /warehouse/sales/orders/{id}       | (sub-page)
outbound_orders          | OutboundOrderCommandHandlers      | GET api/warehouse/v1/outbound/orders                      | /warehouse/outbound/orders         | Outbound Orders
outbound_order_lines     | OutboundOrderCommandHandlers      | Embedded                                                  | /warehouse/outbound/orders/{id}    | (sub-page)
shipments                | ShipmentCommandHandlers (Api)     | POST api/warehouse/v1/shipments/{id}/dispatch             | /warehouse/outbound/dispatch       | Dispatch
dispatch_history         | DispatchController (direct EF)    | GET api/warehouse/v1/reports/dispatch-history             | /reports/dispatch-history          | Dispatch History
handling_units (EF+M6)   | ReceiveGoodsCommandHandler        | POST /inbound-shipments/{id}/receive-items                | /warehouse/inbound/shipments/{id}  | (sub-page)
agnum_export_configs     | AgnumExportServices (Api/Svc)     | GET/PUT api/warehouse/v1/agnum/config                     | /warehouse/agnum/config            | Agnum Config
agnum_export_history     | AgnumExportServices               | GET api/warehouse/v1/agnum/history                        | NONE ⚠                            | NONE ⚠
agnum_mappings           | AgnumExportServices               | GET/PUT api/warehouse/v1/agnum/config                     | /warehouse/agnum/config            | Agnum Config
adjustment_reason_codes  | ReasonCodeService (Api/Svc)       | GET api/warehouse/v1/admin/reason-codes                   | /warehouse/admin/reason-codes      | Reason Codes
approval_rules           | ApprovalRuleService (Api/Svc)     | GET api/warehouse/v1/admin/approval-rules                 | /warehouse/admin/approval-rules    | Approval Rules
roles                    | RoleManagementService (Api/Svc)   | GET api/warehouse/v1/admin/roles                          | /warehouse/admin/roles             | Roles
role_permissions         | RoleManagementService             | Embedded in roles                                         | /warehouse/admin/roles             | Roles
permissions              | AdminPermissionsController        | GET api/warehouse/v1/admin/permissions                    | NONE ⚠                            | NONE ⚠
user_role_assignments    | RoleManagementService             | POST /admin/users/{id}/roles                              | /warehouse/admin/roles             | Roles
items                    | ItemsController (direct EF)       | GET api/warehouse/v1/items                                | /admin/items (read-only ⚠)        | Items
item_barcodes            | ItemsController                   | GET/POST .../barcodes                                     | NONE ⚠                            | NONE ⚠
item_categories          | CategoriesController              | GET api/warehouse/v1/categories                           | /admin/categories                  | Categories
item_uom_conversions     | NONE ⚠                           | NONE ⚠                                                   | NONE ⚠                            | NONE ⚠
suppliers                | SuppliersController               | GET api/warehouse/v1/suppliers                            | /admin/suppliers (read-only ⚠)    | Suppliers
supplier_item_mappings   | SupplierItemMappingsController    | GET api/warehouse/v1/supplier-item-mappings               | /admin/supplier-mappings (r/o ⚠)  | Supplier Mappings
locations                | LocationsController (direct EF)   | GET api/warehouse/v1/locations                            | /admin/locations (read-only ⚠)    | Locations
customers                | CustomersController (direct EF)   | GET api/warehouse/v1/customers                            | /warehouse/sales/orders/create     | (sub-page)
warehouse_settings       | WarehouseSettingsService (Api)    | GET/PUT api/warehouse/v1/admin/settings                   | /warehouse/admin/settings          | Admin Settings
warehouse_layouts        | WarehouseVisualizationController  | GET/PUT api/warehouse/v1/layout                           | NONE ⚠                            | NONE ⚠
warehouses               | WarehouseVisualizationController  | GET api/warehouse/v1/visualization/3d                     | /warehouse/visualization/3d        | Warehouse Map
zone_definitions         | WarehouseVisualizationController  | Embedded in layout                                        | NONE ⚠                            | NONE ⚠
on_hand_value            | ValuationController (direct EF)   | GET api/warehouse/v1/valuation/on-hand-value              | /warehouse/valuation/dashboard     | Valuation
security_audit_logs      | SecurityAuditLogService (Api)     | GET api/warehouse/v1/admin/audit-logs                     | NONE ⚠                            | NONE ⚠
api_keys                 | ApiKeyService (Api/Svc)           | GET/POST api/warehouse/v1/admin/api-keys                  | NONE ⚠                            | NONE ⚠
backup_executions        | BackupServices (Api/Svc)          | POST api/warehouse/v1/admin/backups/trigger               | NONE ⚠                            | NONE ⚠
dr_drills                | DisasterRecoveryService (Api/Svc) | GET api/warehouse/v1/admin/dr/drills                      | NONE ⚠                            | NONE ⚠
electronic_signatures    | AdminComplianceController         | POST api/warehouse/v1/admin/compliance/sign               | (via compliance dashboard only)    | Compliance Dashboard
gdpr_erasure_requests    | GdprErasureService (Api/Svc)      | GET/POST api/warehouse/v1/admin/gdpr/*                    | NONE ⚠                            | NONE ⚠
pii_encryption_keys      | PiiEncryptionService (Api/Svc)    | POST api/warehouse/v1/admin/encryption/rotate-key         | NONE ⚠                            | NONE ⚠
retention_policies       | RetentionPolicyService (Api/Svc)  | GET api/warehouse/v1/admin/retention-policies             | NONE ⚠                            | NONE ⚠
retention_executions     | RetentionPolicyService            | Embedded in retention POST(execute)                       | NONE ⚠                            | NONE ⚠
scheduled_reports        | AdminComplianceController         | GET api/warehouse/v1/admin/compliance/scheduled-reports   | /warehouse/compliance/dashboard    | Compliance Dashboard
generated_report_history | AdminComplianceController         | GET .../scheduled-reports/history                         | /warehouse/compliance/dashboard    | Compliance Dashboard
transaction_exports      | TransactionExportService (Api)    | POST api/warehouse/v1/admin/compliance/export-transactions | NONE ⚠                           | NONE ⚠
serial_numbers           | AdvancedWarehouseController       | POST/GET api/warehouse/v1/serials                         | NONE ⚠                            | NONE ⚠
lots                     | ReceivingController (ref only)    | NONE ⚠                                                   | NONE ⚠                            | NONE ⚠
unit_of_measures         | ItemsController (ref only)        | NONE ⚠                                                   | NONE ⚠                            | NONE ⚠
handling_unit_types      | NONE ⚠                           | NONE ⚠                                                   | NONE ⚠                            | NONE ⚠
user_mfa                 | MfaService (Api/Svc)              | POST api/auth/mfa/*                                       | NONE (auth flow only)              | NONE
sku_sequences            | SkuGenerationService              | NONE (internal)                                           | NONE                               | NONE
events_archive           | Internal archival                 | NONE                                                      | NONE                               | NONE
event_processing_checkpoints | Internal                      | NONE                                                      | NONE                               | NONE
outbound_order_summary   | OutboundOrdersController          | GET api/warehouse/v1/outbound/orders/summary              | /warehouse/outbound/orders         | Outbound Orders
shipment_summary         | ShipmentsController               | GET api/warehouse/v1/shipments/summary                    | /warehouse/outbound/dispatch       | Dispatch
```

---

### T2: NAV_ITEM → UI_PAGE → UI_CALLS → API_ENDPOINT_EXISTS

```
NAV_ITEM               | UI_PAGE/ROUTE                      | UI_CALLS (Client)                          | API_ENDPOINT_EXISTS
-----------------------|------------------------------------|--------------------------------------------|-----------
Dashboard              | /dashboard                         | DashboardClient.*                          | Y
Available Stock        | /available-stock                   | StockClient → /stock/available             | Y
Stock Dashboard        | /warehouse/stock/dashboard         | ReportsClient → /stock/available           | Y
Adjustments            | /warehouse/stock/adjustments       | AdjustmentsClient.*                        | Y
Reservations           | /reservations                      | ReservationsClient.*                       | Y
Inbound Shipments      | /warehouse/inbound/shipments       | ReceivingClient.*                          | Y
Receiving QC           | /warehouse/inbound/qc              | ReceivingClient → /qc/pending,pass,fail    | Y
Putaway                | /warehouse/putaway                 | PutawayClient.*                            | Y
Sales Orders           | /warehouse/sales/orders            | SalesOrdersClient.*                        | Y
Allocations            | /warehouse/sales/allocations       | SalesOrdersClient.*                        | Y
Outbound Orders        | /warehouse/outbound/orders         | OutboundClient.*                           | Y
Dispatch               | /warehouse/outbound/dispatch       | OutboundClient.*                           | Y
Wave Picking           | /warehouse/waves                   | AdvancedWarehouseClient.*                  | Y
Picking Tasks          | /warehouse/picking/tasks           | PickingTasksClient.*                       | Y
Labels                 | /warehouse/labels                  | LabelsClient.*                             | Y
Cross-Dock             | /warehouse/cross-dock              | AdvancedWarehouseClient.*                  | Y
RMAs                   | /warehouse/rmas                    | AdvancedWarehouseClient.*                  | Y
Transfers              | /warehouse/transfers               | TransfersClient.*                          | Y
Cycle Counts           | /warehouse/cycle-counts            | CycleCountsClient.*                        | Y
Warehouse Map          | /warehouse/visualization/3d        | VisualizationClient.*                      | Y
Projections            | /projections                       | ProjectionsClient.*                        | Y
Valuation              | /warehouse/valuation/dashboard     | ValuationClient.*                          | Y
Agnum Config           | /warehouse/agnum/config            | AgnumClient.*                              | Y
Agnum Reconcile        | /warehouse/agnum/reconcile         | AgnumClient.*                              | Y
Users                  | /admin/users                       | AdminUsersClient.*                         | Y
Admin Settings         | /warehouse/admin/settings          | AdminConfigurationClient.*                 | Y
Reason Codes           | /warehouse/admin/reason-codes      | AdminConfigurationClient.*                 | Y
Approval Rules         | /warehouse/admin/approval-rules    | AdminConfigurationClient.*                 | Y
Roles                  | /warehouse/admin/roles             | AdminConfigurationClient.*                 | Y
Items                  | /admin/items                       | MasterDataAdminClient → GET /items only    | Y (partial ⚠)
Suppliers              | /admin/suppliers                   | MasterDataAdminClient → GET only           | Y (partial ⚠)
Supplier Mappings      | /admin/supplier-mappings           | MasterDataAdminClient → GET only           | Y (partial ⚠)
Locations              | /admin/locations                   | MasterDataAdminClient → GET only           | Y (partial ⚠)
Categories             | /admin/categories                  | MasterDataAdminClient → GET + DELETE       | Y (partial ⚠)
Import Wizard          | /admin/import                      | MasterDataAdminClient.*                    | Y
Stock Level            | /reports/stock-level               | ReportsClient → /stock/available           | Y
Receiving History      | /reports/receiving-history         | ReportsClient → /receiving/shipments ⚠    | Y (reuses inbound-shipments)
Pick History           | /reports/pick-history              | ReportsClient → /picking/history           | Y
Dispatch History       | /reports/dispatch-history          | ReportsClient → /reports/dispatch-history  | Y
Stock Movements        | /reports/stock-movements           | ReportsClient.*                            | Y
Traceability           | /reports/traceability              | ReportsClient.*                            | Y
Lot Traceability       | /warehouse/compliance/lot-trace    | ReportsClient.*                            | Y
Compliance Audit       | /reports/compliance-audit          | ReportsClient.*                            | Y
Compliance Dashboard   | /warehouse/compliance/dashboard    | ReportsClient.*                            | Y
Fulfillment KPIs       | /analytics/fulfillment             | AdvancedWarehouseClient.*                  | Y
Quality Analytics      | /analytics/quality                 | AdvancedWarehouseClient.*                  | Y
```

---

### T3: API_ENDPOINT → SERVICE_CALLED → DB_TOUCHES → UI_USES → NAV_VISIBLE

```
API_ENDPOINT                                            | SERVICE                             | DB  | UI  | NAV
--------------------------------------------------------|-------------------------------------|-----|-----|-----
GET  /stock/available                                   | GetAvailableStockQueryHandler       | M   | Y   | Y
GET  /stock/location-balance                            | GetLocationBalanceQueryHandler      | M   | N ⚠ | N ⚠
POST /adjustments                                       | RecordStockMovementCommandHandler   | M   | Y   | Y
GET  /adjustments                                       | AdjustmentsController direct EF     | EF  | Y   | Y
GET  /reservations                                      | SearchReservationsQueryHandler      | M   | Y   | Y
POST /reservations/{id}/start-picking                   | StartPickingCommandHandler          | M   | Y   | Y
POST /reservations/{id}/pick                            | PickStockCommandHandler             | M   | Y   | Y
POST /inbound-shipments                                 | ReceivingController (EF+Marten)     | EF  | Y   | Y
GET  /inbound-shipments                                 | ReceivingController (EF)            | EF  | Y   | Y
POST /inbound-shipments/{id}/receive-items              | ReceiveGoodsCommandHandler          | EF+M| Y   | Y
GET  /qc/pending                                        | QCController (Marten)               | M   | Y   | Y
POST /qc/pass + /qc/fail                                | QCController (EF+Marten)            | EF+M| Y   | Y
POST /picking/tasks                                     | PickingController (direct)          | EF+M| Y   | Y
GET  /picking/tasks/{id}/locations                      | PickingController (direct)          | EF+M| Y   | Y
POST /picking/tasks/{id}/complete                       | PickingController (direct)          | EF+M| Y   | Y
GET  /picking/history                                   | PickingController (EF)              | EF  | Y   | Y
POST /sales-orders                                      | SalesOrderCommandHandlers           | EF  | Y   | Y
GET  /sales-orders                                      | SalesOrderCommandHandlers           | EF  | Y   | Y
POST /sales-orders/{id}/allocate                        | AllocateReservationCommandHandler   | M   | Y   | Y
POST /sales-orders/{id}/release + cancel                | SalesOrderCommandHandlers           | EF  | Y   | Y
GET  /outbound/orders/summary                           | OutboundOrderCommandHandlers        | EF  | Y   | Y
GET  /outbound/orders/{id}                              | OutboundOrderCommandHandlers        | EF  | Y   | Y
POST /outbound/orders/{id}/pack                         | OutboundOrderCommandHandlers        | EF+M| Y   | Y
GET  /shipments/summary                                 | ShipmentCommandHandlers             | EF  | Y   | Y
POST /shipments/{id}/dispatch                           | ShipmentCommandHandlers             | EF+M| Y   | Y
GET  /transfers                                         | TransferServices                    | EF  | Y   | Y
POST /transfers + /submit + /approve + /execute         | TransferServices                    | EF+M| Y   | Y
GET  /cycle-counts                                      | CycleCountServices                  | EF  | Y   | Y
POST /cycle-counts/schedule + record-count              | CycleCountServices                  | EF  | Y   | Y
GET  /cycle-counts/{id}/discrepancies                   | CycleCountServices                  | EF+M| Y   | Y
POST /cycle-counts/{id}/approve-adjustment              | CycleCountsController (MediatR+EF)  | EF+M| Y   | Y
POST /valuation/adjust-cost + apply-landed-cost + write-down | ValuationCommandHandlers       | M   | Y   | Y
GET  /valuation/on-hand-value + cost-history            | ValuationController (EF)            | EF  | Y   | Y
POST /valuation/initialize                              | ValuationController                 | M   | N ⚠ | N
POST /valuation/{itemId}/adjust-cost                    | ValuationController                 | M   | N ⚠ | N
GET  /agnum/config                                      | AgnumExportServices                 | EF  | Y   | Y
PUT  /agnum/config                                      | AgnumExportServices                 | EF  | Y   | Y
POST /agnum/reconcile + GET(reconcile/{id})             | AgnumReconciliationServices         | EF  | Y   | Y
GET  /agnum/history + history/{id}                      | AgnumExportServices                 | EF  | N ⚠ | N ⚠
POST /admin/projections/rebuild                         | RebuildProjectionCommandHandler     | M   | Y   | Y
GET  /admin/projections/rebuild-status                  | ProjectionsController (Marten)      | M   | Y   | Y
GET  /layout                                            | WarehouseVisualizationController    | EF  | N ⚠ | N ⚠
PUT  /layout                                            | WarehouseVisualizationController    | EF  | N ⚠ | N ⚠
GET  /visualization/3d                                  | WarehouseVisualizationController    | M   | Y   | Y
GET  /dispatch/history (DispatchController)             | DispatchController (EF)             | EF  | N ⚠ | N ⚠  ← DEAD DUPLICATE
GET  /reports/dispatch-history                          | ReportsController (EF)              | EF  | Y   | Y
POST api/test/simulate-capacity-alert                   | CapacitySimulationController        | N   | N ⚠ | N ⚠  ← TEST IN PROD
GET  api/admin/capacity/report                          | CapacityPlanningService             | EF  | N ⚠ | N ⚠
GET  api/warehouse/v1/admin/audit-logs                  | SecurityAuditLogService             | EF  | N ⚠ | N ⚠
GET  api/dashboard/health + stock-summary + reservation-summary + projection-health + recent-activity | DashboardController | EF+M | Y | Y
```

---

### T4: UI_PAGE/ROUTE → NAV_VISIBLE → API_CALLS → BACKEND_CHAIN_OK

```
UI_PAGE/ROUTE                                  | NAV | API_CALLS                                  | OK?
-----------------------------------------------|-----|--------------------------------------------|------
/dashboard                                     | Y   | DashboardClient.*                          | ✅
/available-stock                               | Y   | StockClient → /stock/available             | ✅
/warehouse/stock/dashboard                     | Y   | ReportsClient → /stock/available           | ✅
/warehouse/stock/adjustments                   | Y   | AdjustmentsClient → /adjustments           | ✅
/reservations                                  | Y   | ReservationsClient → /reservations         | ✅
/warehouse/inbound/shipments                   | Y   | ReceivingClient → /inbound-shipments       | ✅
/warehouse/inbound/shipments/create            | N   | ReceivingClient → POST /inbound-shipments  | ✅
/warehouse/inbound/shipments/{Id:int}          | N   | ReceivingClient → GET /inbound-shipments/{id} | ✅
/warehouse/inbound/qc                          | Y   | ReceivingClient → /qc/*                    | ✅
/warehouse/putaway                             | Y   | PutawayClient → /putaway                   | ✅
/warehouse/sales/orders                        | Y   | SalesOrdersClient → /sales-orders         | ✅
/warehouse/sales/orders/create                 | N   | SalesOrdersClient → POST                  | ✅
/warehouse/sales/orders/{Id:guid}              | N   | SalesOrdersClient → GET/{id}              | ✅
/warehouse/sales/allocations                   | Y   | SalesOrdersClient → allocate              | ✅
/warehouse/outbound/orders                     | Y   | OutboundClient → /outbound/orders/summary | ✅
/warehouse/outbound/orders/{Id:guid}           | N   | OutboundClient → GET/{id}                 | ✅
/warehouse/outbound/dispatch                   | Y   | OutboundClient → /shipments/summary       | ✅
/warehouse/outbound/pack/{OrderId:guid}        | N   | OutboundClient → POST .../pack            | ✅
/warehouse/waves                               | Y   | AdvancedWarehouseClient → /waves          | ✅
/warehouse/picking/tasks                       | Y   | PickingTasksClient → /picking/tasks       | ✅
/warehouse/labels                              | Y   | LabelsClient → /labels/*                  | ✅
/warehouse/cross-dock                          | Y   | AdvancedWarehouseClient → /cross-dock     | ✅
/warehouse/rmas                                | Y   | AdvancedWarehouseClient → /rmas           | ✅
/warehouse/transfers                           | Y   | TransfersClient → /transfers              | ✅
/warehouse/transfers/create                    | N   | TransfersClient → POST                    | ✅
/warehouse/transfers/{Id:guid}/execute         | N   | TransfersClient → POST .../execute        | ✅
/warehouse/cycle-counts                        | Y   | CycleCountsClient → /cycle-counts         | ✅
/warehouse/cycle-counts/schedule               | N   | CycleCountsClient → POST .../schedule     | ✅
/warehouse/cycle-counts/{Id:guid}/execute      | N   | CycleCountsClient → POST .../record-count | ✅
/warehouse/cycle-counts/{Id:guid}/discrepancies| N   | CycleCountsClient → GET .../discrepancies | ✅
/warehouse/visualization/3d                    | Y   | VisualizationClient → /visualization/3d   | ✅
/projections                                   | Y   | ProjectionsClient → /admin/projections/*  | ✅
/warehouse/valuation/dashboard                 | Y   | ValuationClient → /valuation/on-hand-value | ✅
/warehouse/valuation/adjust-cost               | N   | ValuationClient → POST /valuation/adjust-cost | ✅
/warehouse/valuation/apply-landed-cost         | N   | ValuationClient → POST .../apply-landed-cost | ✅
/warehouse/valuation/write-down                | N   | ValuationClient → POST .../write-down     | ✅
/warehouse/agnum/config                        | Y   | AgnumClient → /agnum/config               | ✅
/warehouse/agnum/reconcile                     | Y   | AgnumClient → /agnum/reconcile            | ✅
/admin/users                                   | Y   | AdminUsersClient → /admin/users           | ✅
/warehouse/admin/settings                      | Y   | AdminConfigurationClient → /admin/settings | ✅
/warehouse/admin/reason-codes                  | Y   | AdminConfigurationClient → /admin/reason-codes | ✅
/warehouse/admin/approval-rules                | Y   | AdminConfigurationClient → /admin/approval-rules | ✅
/warehouse/admin/roles                         | Y   | AdminConfigurationClient → /admin/roles   | ✅
/admin/items                                   | Y   | MasterDataAdminClient → GET /items        | ⚠ PARTIAL (read-only)
/admin/suppliers                               | Y   | MasterDataAdminClient → GET /suppliers    | ⚠ PARTIAL (read-only)
/admin/supplier-mappings                       | Y   | MasterDataAdminClient → GET              | ⚠ PARTIAL (read-only)
/admin/locations                               | Y   | MasterDataAdminClient → GET /locations    | ⚠ PARTIAL (read-only)
/admin/categories                              | Y   | MasterDataAdminClient → GET+DELETE /categories | ⚠ PARTIAL
/admin/import                                  | Y   | MasterDataAdminClient → /admin/import/*   | ✅
/reports/stock-level                           | Y   | ReportsClient → /stock/available          | ✅
/reports/receiving-history                     | Y   | ReportsClient → /receiving/shipments      | ⚠ (reuses inbound list)
/reports/pick-history                          | Y   | ReportsClient → /picking/history          | ✅
/reports/dispatch-history                      | Y   | ReportsClient → /reports/dispatch-history | ✅
/reports/stock-movements                       | Y   | ReportsClient → /reports/stock-movements  | ✅
/reports/traceability                          | Y   | ReportsClient → /reports/traceability     | ✅
/reports/compliance-audit                      | Y   | ReportsClient → /reports/compliance-audit | ✅
/warehouse/compliance/lot-trace                | Y   | ReportsClient → /admin/compliance/lot-trace | ✅
/warehouse/compliance/dashboard                | Y   | ReportsClient → /admin/compliance/dashboard | ✅
/analytics/fulfillment                         | Y   | AdvancedWarehouseClient → /analytics/fulfillment-kpis | ✅
/analytics/quality                             | Y   | AdvancedWarehouseClient → /analytics/qc-late-shipments | ✅
/warehouse/locations/{Id:int}                  | N   | Unknown client ⚠                          | ? NEEDS VERIFICATION
/warehouse/qc/pending                          | N   | NavigationManager redirect only ⚠         | ❌ DEAD PAGE
/                                              | N   | NavigationManager redirect only           | ✅ (intentional)
```

---

## Step 5: Evidence & Commands Used

| Discovery | Tool / Pattern | Key File |
|-----------|---------------|----------|
| Navigation menu items | Read `NavMenu.razor` | `src/.../WebUI/Shared/NavMenu.razor` |
| UI routes | Grep `@page` in *.razor | `src/.../WebUI/Pages/**/*.razor` line 1 |
| API routes | Grep `\[Route\(` + `\[HttpGet\|Post\|...` in *Controller.cs | `src/.../Api/Controllers/*.cs` |
| DB tables | Grep `modelBuilder\.Entity\|\.ToTable\(` in Snapshot | `src/.../Persistence/Migrations/WarehouseDbContextModelSnapshot.cs` |
| Application handlers | Glob `*Handler*.cs` in Application/ | `src/.../Application/Commands/*.cs`, `Application/Queries/*.cs` |
| UI-to-API mapping | Grep `GetAsync\|PostAsync\|PutAsync\|DeleteAsync` + `"api/` in WebUI/Services/*.cs | `src/.../WebUI/Services/*Client.cs` |
| Page-to-client wiring | Grep `@inject` in Pages/*.razor | `src/.../WebUI/Pages/**/*.razor` |
| MediatR-wired controllers | Grep `_mediator\|ISender\|Send\(` in Controllers | `src/.../Api/Controllers/*.cs` |
| Marten config | Grep `AddMarten\|StoreOptions\|MartenRegistry` | `src/.../Infrastructure/Persistence/MartenConfiguration.cs` |
