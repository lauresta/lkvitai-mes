# UI-API Gaps

Only `GAP_NO_UI` endpoints are listed.

| Endpoint | Why it is needed | Suggested UI page/action | Sprint/Epic |
|---|---|---|---|
| `GET /api/warehouse/v1/adjustments` | Create/list stock adjustments with audit trace. | /warehouse/stock/adjustments (list/create) | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/adjustments` | Create/list stock adjustments with audit trace. | /warehouse/stock/adjustments (list/create) | Sprint S3 / Epic: Operational UI Completion |
| `GET /api/warehouse/v1/admin/compliance/exports` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `POST /api/warehouse/v1/admin/compliance/export-transactions` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `GET /api/warehouse/v1/admin/compliance/lot-trace/{traceId}` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `GET /api/warehouse/v1/admin/compliance/scheduled-reports/history` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `POST /api/warehouse/v1/admin/compliance/sign` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `GET /api/warehouse/v1/admin/compliance/signatures/{resourceId}` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `GET /api/warehouse/v1/admin/compliance/validation-report` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `POST /api/warehouse/v1/admin/compliance/verify-hash-chain` | Compliance signatures, exports, and validation artifacts. | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `POST /api/warehouse/v1/admin/projections/cleanup-shadows` | Administrative/operational control endpoint. | /projections switch calls to /api/warehouse/v1/admin/projections/* | Sprint S1 / Epic: Projection Reliability |
| `POST /api/warehouse/v1/admin/projections/rebuild` | Administrative/operational control endpoint. | /projections switch calls to /api/warehouse/v1/admin/projections/* | Sprint S1 / Epic: Projection Reliability |
| `GET /api/warehouse/v1/admin/projections/rebuild-status` | Administrative/operational control endpoint. | /projections switch calls to /api/warehouse/v1/admin/projections/* | Sprint S1 / Epic: Projection Reliability |
| `POST /api/warehouse/v1/admin/projections/verify` | Administrative/operational control endpoint. | /projections switch calls to /api/warehouse/v1/admin/projections/* | Sprint S1 / Epic: Projection Reliability |
| `POST /api/warehouse/v1/agnum/export` | Accounting export/reconciliation integration with Agnum. | /warehouse/agnum/reconcile add export-history panel | Sprint S2 / Epic: Finance & Valuation |
| `GET /api/warehouse/v1/agnum/history` | Accounting export/reconciliation integration with Agnum. | /warehouse/agnum/reconcile add export-history panel | Sprint S2 / Epic: Finance & Valuation |
| `GET /api/warehouse/v1/agnum/history/{exportId}` | Accounting export/reconciliation integration with Agnum. | /warehouse/agnum/reconcile add export-history panel | Sprint S2 / Epic: Finance & Valuation |
| `GET /api/warehouse/v1/barcodes/lookup` | Barcodes LookupAsync | /admin/items barcode management actions | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/cycle-counts/{id}/apply-adjustment` | Cycle-count execution and discrepancy adjustment. | /warehouse/cycle-counts/{id}/execute + discrepancies actions | Sprint S4 / Epic: Counting & Labeling |
| `GET /api/warehouse/v1/cycle-counts/{id}/lines` | Cycle-count execution and discrepancy adjustment. | /warehouse/cycle-counts/{id}/execute + discrepancies actions | Sprint S4 / Epic: Counting & Labeling |
| `GET /api/warehouse/v1/customers/{id}` | Customers GetByIdAsync | /warehouse/sales/orders/{id} customer detail drawer | Sprint S8 / Epic: Outbound/Sales UX hardening |
| `GET /api/warehouse/v1/dispatch/history` | Dispatch GetHistoryAsync | /reports/dispatch-history canonical endpoint usage | Sprint S8 / Epic: Outbound/Sales UX hardening |
| `GET /api/warehouse/v1/handling-units/{huId}/hierarchy` | HandlingUnits Hierarchy | /warehouse/rmas or /warehouse/serials advanced ops page | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/handling-units/{parentHuId}/merge` | HandlingUnits Merge | /warehouse/rmas or /warehouse/serials advanced ops page | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/handling-units/{parentHuId}/split` | HandlingUnits Split | /warehouse/rmas or /warehouse/serials advanced ops page | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/inbound-shipments/{id}/receive` | Receiving ReceiveGoodsAsync | /dashboard (new action/page needed) | Backlog / Epic to be assigned |
| `GET /api/warehouse/v1/items/{id}/barcodes` | Items GetBarcodesAsync | /admin/items barcode management actions | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/items/{id}/barcodes` | Items AddBarcodeAsync | /admin/items barcode management actions | Sprint S3 / Epic: Operational UI Completion |
| `GET /api/warehouse/v1/labels/pdf/{fileName}` | Generate/preview/queue/retry warehouse labels. | /warehouse/labels (preview/print/queue) | Sprint S4 / Epic: Counting & Labeling |
| `GET /api/warehouse/v1/labels/preview` | Generate/preview/queue/retry warehouse labels. | /warehouse/labels (preview/print/queue) | Sprint S4 / Epic: Counting & Labeling |
| `POST /api/warehouse/v1/labels/preview` | Generate/preview/queue/retry warehouse labels. | /warehouse/labels (preview/print/queue) | Sprint S4 / Epic: Counting & Labeling |
| `POST /api/warehouse/v1/labels/print` | Generate/preview/queue/retry warehouse labels. | /warehouse/labels (preview/print/queue) | Sprint S4 / Epic: Counting & Labeling |
| `GET /api/warehouse/v1/labels/queue` | Generate/preview/queue/retry warehouse labels. | /warehouse/labels (preview/print/queue) | Sprint S4 / Epic: Counting & Labeling |
| `POST /api/warehouse/v1/labels/queue/{id}/retry` | Generate/preview/queue/retry warehouse labels. | /warehouse/labels (preview/print/queue) | Sprint S4 / Epic: Counting & Labeling |
| `GET /api/warehouse/v1/labels/templates` | Generate/preview/queue/retry warehouse labels. | /warehouse/labels (preview/print/queue) | Sprint S4 / Epic: Counting & Labeling |
| `GET /api/warehouse/v1/layout` | WarehouseVisualization GetLayoutAsync | /warehouse/visualization/3d layout editor | Sprint S6 / Epic: Warehouse Visualization |
| `PUT /api/warehouse/v1/layout` | WarehouseVisualization PutLayoutAsync | /warehouse/visualization/3d layout editor | Sprint S6 / Epic: Warehouse Visualization |
| `POST /api/warehouse/v1/locations/bulk-coordinates` | Locations BulkCoordinatesAsync | /warehouse/visualization/3d layout editor | Sprint S6 / Epic: Warehouse Visualization |
| `POST /api/warehouse/v1/picking/tasks` | Create and execute picking tasks. | /warehouse/picking/tasks (create/execute) | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/picking/tasks/{id}/complete` | Create and execute picking tasks. | /warehouse/picking/tasks (create/execute) | Sprint S3 / Epic: Operational UI Completion |
| `GET /api/warehouse/v1/picking/tasks/{id}/locations` | Create and execute picking tasks. | /warehouse/picking/tasks (create/execute) | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/putaway` | List and execute putaway tasks from receiving. | /warehouse/putaway (tasks + execute) | Sprint S3 / Epic: Operational UI Completion |
| `GET /api/warehouse/v1/putaway/tasks` | List and execute putaway tasks from receiving. | /warehouse/putaway (tasks + execute) | Sprint S3 / Epic: Operational UI Completion |
| `GET /api/warehouse/v1/qc/checklist-templates` | Quality control templates, defects, and attachments. | /warehouse/inbound/qc advanced tab | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/qc/checklist-templates` | Quality control templates, defects, and attachments. | /warehouse/inbound/qc advanced tab | Sprint S7 / Epic: Advanced Warehouse Operations |
| `GET /api/warehouse/v1/qc/defects` | Quality control templates, defects, and attachments. | /warehouse/inbound/qc advanced tab | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/qc/defects` | Quality control templates, defects, and attachments. | /warehouse/inbound/qc advanced tab | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/qc/defects/{defectId}/attachments` | Quality control templates, defects, and attachments. | /warehouse/inbound/qc advanced tab | Sprint S7 / Epic: Advanced Warehouse Operations |
| `GET /api/warehouse/v1/reservations` | Reservation lifecycle and pick execution APIs. | /reservations migrate to /api/warehouse/v1/reservations | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/reservations/{id}/pick` | Reservation lifecycle and pick execution APIs. | /reservations migrate to /api/warehouse/v1/reservations | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/reservations/{id}/start-picking` | Reservation lifecycle and pick execution APIs. | /reservations migrate to /api/warehouse/v1/reservations | Sprint S3 / Epic: Operational UI Completion |
| `GET /api/warehouse/v1/serials` | Serials Search | /warehouse/rmas or /warehouse/serials advanced ops page | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/serials` | Serials Register | /warehouse/rmas or /warehouse/serials advanced ops page | Sprint S7 / Epic: Advanced Warehouse Operations |
| `POST /api/warehouse/v1/serials/{id}/status` | Serials Transition | /warehouse/rmas or /warehouse/serials advanced ops page | Sprint S7 / Epic: Advanced Warehouse Operations |
| `GET /api/warehouse/v1/stock/location-balance` | Stock GetLocationBalanceAsync | /warehouse/stock/dashboard capacity tab | Sprint S3 / Epic: Operational UI Completion |
| `POST /api/warehouse/v1/valuation/{itemId}/adjust-cost` | Inventory valuation and cost adjustment operations. | /warehouse/valuation/dashboard + adjust/initialize actions | Sprint S2 / Epic: Finance & Valuation |
| `POST /api/warehouse/v1/valuation/initialize` | Inventory valuation and cost adjustment operations. | /warehouse/valuation/dashboard + adjust/initialize actions | Sprint S2 / Epic: Finance & Valuation |
| `GET /api/warehouse/v1/waves/{id}` | Waves Get | /warehouse/waves detail route | Sprint S7 / Epic: Advanced Warehouse Operations |
