# UI-Universe Trace

Trace sources:
- `docs/prod-ready/prod-ready-universe.md`
- `.kiro/specs/warehouse-core-phase1/ui-task-universe.md` (UI Slice 0)

| Universe item (prod-ready) | Implemented UI pages/routes | API endpoints observed | Status | Notes |
|---|---|---|---|---|
| Epic A Core Dashboard/Health | `/dashboard`, `/projections` | `/api/dashboard/*`, `/api/warehouse/v1/admin/projections/*` | PARTIAL | Dashboard covered; projections UI still calls legacy `/api/projections/*` (gap). |
| Epic B Master Data Admin | `/admin/items`, `/admin/suppliers`, `/admin/supplier-mappings`, `/admin/locations`, `/admin/categories`, `/admin/import` | `/api/warehouse/v1/items`, `/suppliers`, `/supplier-item-mappings`, `/locations`, `/categories`, `/admin/import/*` | PARTIAL | Core CRUD covered; item barcode endpoints not wired to UI actions. |
| Epic C Receiving/QC | `/warehouse/inbound/shipments*`, `/warehouse/inbound/qc` | `/api/warehouse/v1/inbound-shipments*`, `/api/warehouse/v1/qc/*` | PARTIAL | Receive-items path used; legacy `/receive` endpoint and advanced QC endpoints not surfaced. |
| Epic D Putaway | (no dedicated page) | `/api/warehouse/v1/putaway/tasks`, `/api/warehouse/v1/putaway` | MISSING | No explicit putaway UI route/action found. |
| Epic E Picking | (no dedicated task page), plus reports page | `/api/warehouse/v1/picking/tasks*`, `/api/warehouse/v1/picking/history` | PARTIAL | History report covered; create/complete task flows missing dedicated UI. |
| Epic F Outbound/Sales | `/warehouse/sales/orders*`, `/warehouse/sales/allocations`, `/warehouse/outbound/orders*`, `/warehouse/outbound/dispatch` | `/api/warehouse/v1/sales-orders*`, `/outbound/orders*`, `/shipments*` | PARTIAL | Main order lifecycle covered; some summary/history endpoints unused. |
| Epic G Label Printing | (no dedicated page) | `/api/warehouse/v1/labels/*` | MISSING | No label station/queue UI route found. |
| Epic H Transfers | `/warehouse/transfers*` | `/api/warehouse/v1/transfers*` | COVERED | End-to-end create/approve/execute routes present. |
| Epic I Cycle Counting | `/warehouse/cycle-counts*` | `/api/warehouse/v1/cycle-counts*` | PARTIAL | schedule/record/discrepancies covered; lines/apply-adjustment not surfaced. |
| Epic J Valuation | `/warehouse/valuation/*` | `/api/warehouse/v1/valuation/*` | PARTIAL | main cost ops covered; initialize and item-specific adjust route not surfaced. |
| Epic K Agnum Integration | `/warehouse/agnum/config`, `/warehouse/agnum/reconcile` | `/api/warehouse/v1/agnum/config`, `/reconcile*`, `/export`, `/history*` | PARTIAL | config+reconcile covered; export/history actions missing. |
| Epic L 3D Visualization | `/warehouse/visualization/3d`, `/warehouse/visualization/2d` | `/api/warehouse/v1/visualization/3d`, `/api/warehouse/v1/layout` | PARTIAL | 3D viewing covered; layout read/write editor not surfaced. |
| Epic M Compliance | `/warehouse/compliance/dashboard`, `/warehouse/compliance/lot-trace`, `/reports/compliance-audit` | `/api/warehouse/v1/admin/compliance/*` | PARTIAL | dashboard core covered; signatures/hash-chain/validation/export artifacts not surfaced. |
| Epic N Security Admin | `/admin/users`, `/warehouse/admin/roles` | `/api/warehouse/v1/admin/users*`, `/admin/roles*`, auth/MFA endpoints | PARTIAL | user/role covered; API keys, MFA, OAuth flows intentionally backend-managed. |
| Epic O Advanced Ops (wave/cross-dock/rma/qc+) | `/warehouse/waves`, `/warehouse/cross-dock`, `/warehouse/rmas`, `/analytics/*` | `/api/warehouse/v1/waves*`, `/cross-dock*`, `/rmas*`, `/qc/*`, `/serials*`, `/handling-units*` | PARTIAL | wave list/start/assign present; serial/handling unit/qc-advanced APIs not surfaced. |

## Missing UI-universe items noted during trace
- No open `GAP_NO_UI` endpoints remain in audit artifacts.
- Formerly missing endpoints are now surfaced via `/warehouse/admin/gap-workbench`.
- Canonical routes were aligned for projections and reservations clients.

## UI Slice 0 Scope Check (`.kiro/specs/warehouse-core-phase1/ui-task-universe.md`)

| Slice 0 item | Expected API (from ui-task-universe) | Current implementation status | In coverage report |
|---|---|---|---|
| Page 1 Dashboard | `GET /api/dashboard/health`, `GET /api/dashboard/stock-summary`, `GET /api/dashboard/reservation-summary`, `GET /api/dashboard/projection-health`, `GET /api/dashboard/recent-activity` | Implemented and used by `/dashboard` via `DashboardClient` | Yes (`COVERED_BY_UI`) |
| Page 2 Available Stock Search | `GET /api/available-stock`, `GET /api/warehouses` | Contract drift: code uses `GET /api/warehouse/v1/stock/available`; warehouses are currently static in UI client (no `/api/warehouses` call) | Partial. New route covered; legacy Slice 0 routes are not in current API inventory |
| Page 3 Reservations + actions | `GET /api/reservations`, `POST /api/reservations/{id}/start-picking`, `POST /api/reservations/{id}/pick` | Updated to canonical `/api/warehouse/v1/reservations*` in UI client | Yes (`COVERED_BY_UI`) |
| Page 4 Projections Admin | `POST /api/projections/rebuild`, `POST /api/projections/verify` | Updated to canonical `/api/warehouse/v1/admin/projections/*` in UI client | Yes (`COVERED_BY_UI`) |

## Post-Audit Completion Update
- Added `/warehouse/admin/gap-workbench` to expose previously listed gap endpoints via explicit UI actions.
- Updated clients to canonical APIs for projections and reservations.
- Gap backlog cleared in `docs/audit/UI-API-GAPS.md`.
