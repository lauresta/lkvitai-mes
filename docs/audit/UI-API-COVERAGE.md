# UI-API Coverage Audit (Post GapWorkbench Removal)

## Coverage method
- Canonical API endpoints from `src/LKvitai.MES.Api/Api/Controllers`.
- Blazor UI surfaces from `src/LKvitai.MES.WebUI/Pages` and typed clients in `src/LKvitai.MES.WebUI/Services`.
- No endpoint-caller/workbench surfaces are counted.

## Newly covered with dedicated UI
| Endpoint(s) | UI surface |
|---|---|
| `GET/POST /api/warehouse/v1/adjustments` | `/warehouse/stock/adjustments` |
| `GET /api/warehouse/v1/putaway/tasks`, `POST /api/warehouse/v1/putaway` | `/warehouse/putaway` |
| `POST /api/warehouse/v1/picking/tasks`, `GET /api/warehouse/v1/picking/tasks/{id}/locations`, `POST /api/warehouse/v1/picking/tasks/{id}/complete` | `/warehouse/picking/tasks` |
| `POST /api/warehouse/v1/labels/print`, `POST /api/warehouse/v1/labels/preview`, `GET /api/warehouse/v1/labels/templates`, `GET /api/warehouse/v1/labels/queue`, `POST /api/warehouse/v1/labels/queue/{id}/retry`, `GET /api/warehouse/v1/labels/pdf/{fileName}` | `/warehouse/labels` |
| `GET/POST /api/warehouse/v1/admin/projections/{rebuild,verify}` | `/projections` |
| `GET /api/warehouse/v1/reservations`, `POST /api/warehouse/v1/reservations/{id}/start-picking`, `POST /api/warehouse/v1/reservations/{id}/pick` | `/reservations` |

## INTENTIONAL_NO_UI (Phase 1.5)
These endpoints remain intentionally without direct UI in Phase 1.5 and are excluded from `GAP_NO_UI`:

| Endpoint | Justification |
|---|---|
| `GET /api/warehouse/v1/admin/compliance/exports` | Auditor artifact export endpoint; not part of operator flow. |
| `POST /api/warehouse/v1/admin/compliance/export-transactions` | Auditor/batch export operation; offline compliance workflow. |
| `GET /api/warehouse/v1/admin/compliance/scheduled-reports/history` | Back-office audit history, not required for floor operators. |
| `POST /api/warehouse/v1/admin/compliance/sign` | Cryptographic signing control path, restricted governance operation. |
| `GET /api/warehouse/v1/admin/compliance/signatures/{resourceId}` | Forensic verification endpoint; admin-only workflow. |
| `GET /api/warehouse/v1/admin/compliance/validation-report` | Compliance validation artifact endpoint; audit-office usage. |
| `POST /api/warehouse/v1/admin/compliance/verify-hash-chain` | Forensic chain verification; scheduled/office process. |
| `POST /api/warehouse/v1/admin/projections/cleanup-shadows` | Projection maintenance operation, not operator UI. |
| `GET /api/warehouse/v1/admin/projections/rebuild-status` | Maintenance telemetry endpoint for admin reliability ops. |
| `POST /api/warehouse/v1/agnum/export` | Finance batch export handled outside warehouse operator UI. |
| `GET /api/warehouse/v1/agnum/history` | Finance export archive; outside Phase 1.5 floor workflow. |
| `GET /api/warehouse/v1/agnum/history/{exportId}` | Finance archive detail; same rationale as above. |
| `GET /api/warehouse/v1/barcodes/lookup` | Scanner-integration helper endpoint, not mandatory desktop flow. |
| `POST /api/warehouse/v1/cycle-counts/{id}/apply-adjustment` | High-risk financial action deferred; approval flow uses existing discrepancy screen. |
| `GET /api/warehouse/v1/customers/{id}` | Sales order aggregates already provide required customer context. |
| `GET /api/warehouse/v1/dispatch/history` | Legacy alias; canonical dispatch history is covered via reports endpoint. |
| `GET /api/warehouse/v1/handling-units/{huId}/hierarchy` | Advanced handling-unit tooling deferred beyond Phase 1.5. |
| `POST /api/warehouse/v1/handling-units/{parentHuId}/merge` | Advanced handling-unit operation deferred beyond Phase 1.5. |
| `POST /api/warehouse/v1/handling-units/{parentHuId}/split` | Advanced handling-unit operation deferred beyond Phase 1.5. |
| `POST /api/warehouse/v1/inbound-shipments/{id}/receive` | Legacy alias; active UI uses canonical `/receive-items`. |
| `GET /api/warehouse/v1/items/{id}/barcodes` | Specialized master-data extension deferred from core operations slice. |
| `POST /api/warehouse/v1/items/{id}/barcodes` | Specialized master-data extension deferred from core operations slice. |
| `GET /api/warehouse/v1/layout` | Layout editor path deferred; 3D viewer remains covered. |
| `PUT /api/warehouse/v1/layout` | Layout editor path deferred; 3D viewer remains covered. |
| `POST /api/warehouse/v1/locations/bulk-coordinates` | Bulk coordinate admin tool deferred; not needed for operator flows. |
| `GET /api/warehouse/v1/qc/checklist-templates` | Advanced QC template management deferred beyond base receiving/QC flow. |
| `POST /api/warehouse/v1/qc/checklist-templates` | Advanced QC template management deferred beyond base receiving/QC flow. |
| `GET /api/warehouse/v1/qc/defects` | Advanced QC defect registry deferred beyond base receiving/QC flow. |
| `POST /api/warehouse/v1/qc/defects` | Advanced QC defect registry deferred beyond base receiving/QC flow. |
| `GET /api/warehouse/v1/serials` | Serial registry advanced operations deferred beyond Phase 1.5. |
| `POST /api/warehouse/v1/serials` | Serial registry advanced operations deferred beyond Phase 1.5. |
| `POST /api/warehouse/v1/serials/{id}/status` | Serial lifecycle advanced operations deferred beyond Phase 1.5. |
| `GET /api/warehouse/v1/stock/location-balance` | Capacity analytics endpoint deferred; current stock dashboard covers primary visibility needs. |
| `POST /api/warehouse/v1/valuation/{itemId}/adjust-cost` | Item-scoped finance adjustment endpoint deferred; main valuation actions remain covered. |
| `POST /api/warehouse/v1/valuation/initialize` | One-time/bootstrap finance operation; not operator workflow. |
| `GET /api/warehouse/v1/waves/{id}` | Detail endpoint deferred; core wave list/start/assign actions are covered. |

Security/auth/observability and deep admin maintenance endpoints remain `INTENTIONAL_NO_UI` as in previous audit baselines.
