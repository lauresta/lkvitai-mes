# spec-ui.md — Blazor Server UI Architecture (Phase 1)

**Project:** LKvitai.MES Warehouse — UI Slice 0
**Stack:** Blazor Server (.NET 8) + Bootstrap 5
**Source:** requirements-ui.md + governance review
**Phase 1 exclusions:** No SignalR, No Chart.js, No async rebuild/polling, No auth enforcement

---

## 1. Project Structure

```
src/LKvitai.MES.WebUI/
├── Program.cs                          # Host builder, HttpClient DI, service registration
├── appsettings.json                    # ApiBaseUrl, StalenessThresholds, RefreshIntervals
├── _Imports.razor                      # Global usings
│
├── Shared/
│   ├── MainLayout.razor                # Bootstrap 5 sidebar + top bar, nav links
│   ├── MainLayout.razor.css
│   ├── NavMenu.razor                   # Dashboard, Available Stock, Reservations, Projections
│   └── AuthPlaceholder.razor           # TODO: Phase 2 — commented-out <AuthorizeView>
│
├── Pages/
│   ├── Dashboard.razor                 # /dashboard (default redirect from /)
│   ├── AvailableStock.razor            # /available-stock
│   ├── Reservations.razor              # /reservations
│   └── Projections.razor               # /projections
│
├── Components/
│   ├── DataTable.razor                 # Generic @typeparam TItem, columns via RenderFragment
│   ├── Pagination.razor                # Prev/Next, page size selector (25/50/100)
│   ├── LoadingSpinner.razor            # Bootstrap spinner, shown during async calls
│   ├── ErrorBanner.razor               # Alert-danger + retry button + dismiss + traceId display
│   ├── StaleBadge.razor                # Yellow (>5s) / Red (>30s) based on LastUpdated prop
│   ├── ConfirmDialog.razor             # Bootstrap modal, Title + Message + Confirm/Cancel
│   ├── ToastContainer.razor            # Stacked toast host, auto-dismiss after 5s
│   │
│   ├── Dashboard/
│   │   ├── HealthStatusCard.razor      # Green/Yellow/Red badge + lag seconds
│   │   ├── StockSummaryCard.razor      # Total SKUs, qty, value
│   │   ├── ReservationSummaryCard.razor # ALLOCATED/PICKING/CONSUMED counts
│   │   ├── ProjectionHealthCard.razor  # LB + AS lag + last rebuild timestamps
│   │   └── RecentActivityFeed.razor    # Scrollable list, last 10 movements
│   │
│   ├── Stock/
│   │   ├── StockFilters.razor          # Warehouse dropdown, Location/SKU inputs, virtual toggle
│   │   └── StockTable.razor            # 7-column table with StaleBadge in LastUpdated
│   │
│   ├── Reservations/
│   │   ├── ReservationStatusFilter.razor  # Dropdown: All / ALLOCATED / PICKING
│   │   ├── ReservationsTable.razor        # 7-col table with conditional action buttons
│   │   ├── ReservationDetailModal.razor   # Lines with allocated HUs (read-only)
│   │   └── PickModal.razor                # HU dropdown + quantity input + validation
│   │
│   └── Projections/
│       ├── ProjectionSelector.razor    # Radio: LocationBalance / AvailableStock
│       ├── RebuildStatusCard.razor     # Idle/Running/Completed/Failed + spinner
│       └── RebuildResultDetail.razor   # Green/Red card with all metrics
│
├── Services/
│   ├── DashboardClient.cs             # 5 GET endpoints, auto-refresh timer helper
│   ├── StockClient.cs                 # GET /available-stock, GET /warehouses
│   ├── ReservationsClient.cs          # GET /reservations, POST start-picking, POST pick
│   ├── ProjectionsClient.cs           # POST rebuild (sync), POST verify
│   └── ToastService.cs               # In-memory event bus for toast notifications
│
├── Models/
│   ├── HealthStatusDto.cs
│   ├── StockSummaryDto.cs
│   ├── ReservationSummaryDto.cs
│   ├── ProjectionHealthDto.cs
│   ├── RecentMovementDto.cs
│   ├── AvailableStockItemDto.cs
│   ├── WarehouseDto.cs
│   ├── ReservationDto.cs
│   ├── ReservationLineDto.cs
│   ├── AllocatedHUDto.cs
│   ├── StartPickingResponseDto.cs
│   ├── PickResponseDto.cs
│   ├── RebuildResultDto.cs
│   ├── VerifyResultDto.cs
│   └── PagedResult.cs                 # Generic: Items<T>, TotalCount, Page, PageSize
│
└── Infrastructure/
    ├── ProblemDetailsParser.cs         # Parse HttpResponseMessage → ProblemDetailsModel
    ├── ProblemDetailsModel.cs          # Type, Title, Status, TraceId, Errors dict, Detail
    ├── ApiException.cs                 # Wraps ProblemDetailsModel, thrown by clients
    └── ErrorCodeMessages.cs           # Static Dictionary<string, string> for UI messages
```

---

## 2. Navigation and Layout

**Layout:** Bootstrap 5 sidebar (collapsed on narrow viewports) + top bar with project name.

| Sidebar Link | Route | Icon (Bootstrap Icons) |
|-------------|-------|----------------------|
| Dashboard | `/dashboard` | `bi-speedometer2` |
| Available Stock | `/available-stock` | `bi-box-seam` |
| Reservations | `/reservations` | `bi-clipboard-check` |
| Projections | `/projections` | `bi-database-gear` |

- `/` redirects to `/dashboard`.
- Active link highlighted via Bootstrap `active` class.
- Top bar: project name "LKvitai.MES Warehouse" left-aligned, placeholder "Login" button right-aligned (disabled, Phase 2).
- No breadcrumbs in Phase 1 (4 flat pages, no nesting).

---

## 3. Reusable Components

| Component | Props | Events | Notes |
|-----------|-------|--------|-------|
| `DataTable<TItem>` | `Items`, `TotalCount`, `Columns` (RenderFragment), `EmptyMessage` | `OnRowClick(TItem)` | No built-in sorting (Phase 2). CSV export via JS interop on current page items. |
| `Pagination` | `CurrentPage`, `TotalPages`, `PageSize`, `PageSizeOptions` (25/50/100) | `OnPageChanged(int)`, `OnPageSizeChanged(int)` | Displays "Page X of Y". |
| `LoadingSpinner` | `IsLoading` (bool) | — | Bootstrap `spinner-border`. Overlays content area. |
| `ErrorBanner` | `Message`, `TraceId` | `OnRetry`, `OnDismiss` | Alert-danger. Shows "Error ID: {traceId}" below message. |
| `StaleBadge` | `LastUpdated` (DateTime) | — | Computes lag client-side. No badge <5s, `badge-warning` 5-30s, `badge-danger` >30s. |
| `ConfirmDialog` | `Title`, `Message`, `ConfirmText`, `IsVisible` | `OnConfirm`, `OnCancel` | Bootstrap modal. Used by Projections rebuild. |
| `ToastContainer` | — | — | Renders stacked toasts from `ToastService`. Auto-dismiss 5s. Types: success, error, warning. |

---

## 4. API Client Design

### Base Configuration (Program.cs)

```
Register named HttpClient "WarehouseApi":
  - BaseAddress from appsettings "ApiBaseUrl"
  - Timeout: 30s (covers sync rebuild)
  - DefaultRequestHeaders: Accept application/json
```

### Typed Clients

Each client is a scoped service injected with `IHttpClientFactory.CreateClient("WarehouseApi")`.

| Client | Endpoints | Return Types |
|--------|-----------|-------------|
| `DashboardClient` | GET `/api/dashboard/health`, GET `/api/dashboard/stock-summary`, GET `/api/dashboard/reservation-summary`, GET `/api/dashboard/projection-health`, GET `/api/dashboard/recent-activity` | `HealthStatusDto`, `StockSummaryDto`, `ReservationSummaryDto`, `ProjectionHealthDto`, `List<RecentMovementDto>` |
| `StockClient` | GET `/api/available-stock?warehouse={w}&location={l}&sku={s}&includeVirtual={bool}&page={p}&pageSize={ps}` (includeVirtual defaults to false, excludes SUPPLIER/PRODUCTION/SCRAP/SYSTEM when false), GET `/api/warehouses` | `PagedResult<AvailableStockItemDto>`, `List<WarehouseDto>` |
| `ReservationsClient` | GET `/api/reservations`, POST `/api/reservations/{id}/start-picking`, POST `/api/reservations/{id}/pick` | `PagedResult<ReservationDto>`, `StartPickingResponseDto`, `PickResponseDto` |
| `ProjectionsClient` | POST `/api/projections/rebuild`, POST `/api/projections/verify` | `RebuildResultDto`, `VerifyResultDto` |

### Error Handling Policy (all clients)

1. Always read response content first (await `response.Content.ReadAsStringAsync()`).
2. If `!response.IsSuccessStatusCode`: Parse response body. If Content-Type is `application/problem+json`, parse into `ProblemDetailsModel`. Throw `ApiException(problemDetails)` with `StatusCode`, `ErrorCode` (from `type` extension), `TraceId`, `UserMessage` (looked up from `ErrorCodeMessages`).
3. If response is not ProblemDetails (e.g., plain 500), throw `ApiException` with generic message + status code.
4. If `response.IsSuccessStatusCode`: Deserialize response content to expected DTO type.
5. Page catches `ApiException`, sets error state, renders `ErrorBanner` with message + traceId.

**CRITICAL**: Never use `EnsureSuccessStatusCode()` before reading response body. Always read content first to ensure ProblemDetails can be parsed from error responses.

---

## 5. UI State Patterns Per Page

### Dashboard (`/dashboard`)

| State | Trigger | Display |
|-------|---------|---------|
| Loading | Initial load, every 30s refresh | `LoadingSpinner` overlay on each card independently |
| Success | All 5 API calls return OK | All cards populated |
| Partial Error | Some calls fail | Failed cards show inline error, successful cards display data |
| Full Error (503) | Backend unreachable | `ErrorBanner` "Backend unavailable" + retry |
| Stale Warning | ProjectionLag > 5s | Yellow badge on Projection Health card |
| Stale Alert | ProjectionLag > 30s | Red badge on Projection Health card |

**Refresh:** `System.Threading.Timer` with 30s interval. Calls all 5 endpoints. No SignalR. Timer disposed on page dispose.

### Available Stock (`/available-stock`)

| State | Trigger | Display |
|-------|---------|---------|
| Initial | Page load | Empty table, filters visible, "Enter search criteria" prompt |
| Loading | Search submitted | `LoadingSpinner` over table area |
| Success | Results returned | `StockTable` with data, `Pagination` controls |
| Empty | 0 items returned | "No stock found matching filters" |
| Validation Error | No filters provided | Inline error below filters, no API call |
| Error (400) | Backend validation fail | `ErrorBanner` with field-specific messages |
| Error (500) | Server error | `ErrorBanner` with retry |

**No auto-refresh.** User triggers search explicitly.

### Reservations (`/reservations`)

| State | Trigger | Display |
|-------|---------|---------|
| Initial | Page load | Auto-search with status=All |
| Loading | Filter change, page change | `LoadingSpinner` over table |
| Success | Results returned | `ReservationsTable` with action buttons |
| Empty | 0 items returned | "No reservations found" |
| Action Success | StartPicking/Pick returns OK | Toast "Picking started" / "Pick completed", table auto-refreshes |
| Action Error (409) | HARD lock conflict | Toast error "HARD lock conflict..." |
| Action Error (422) | Insufficient balance / qty exceeds | Toast error with specific message |
| Error (500) | Server error | `ErrorBanner` with retry |

**Detail modal:** Opens on row click. Displays lines and allocated HUs from list response (no separate API call).
**Pick modal:** Opens from "Pick" button. HU dropdown populated from reservation lines in list response.

### Projections (`/projections`)

| State | Trigger | Display |
|-------|---------|---------|
| Idle | Page load | Projection selector visible, buttons enabled, no result |
| Rebuild Confirm | Rebuild button clicked | `ConfirmDialog` "Are you sure?" |
| Rebuild Running | User confirms | `LoadingSpinner`, buttons disabled. **Sync — blocks until response.** |
| Rebuild Success | Checksums match + swapped | Green `RebuildResultDetail` card |
| Rebuild Fail | Checksums mismatch | Red `RebuildResultDetail` card |
| Rebuild Error (409) | Already running | Toast error "Rebuild already running" |
| Verify Success | Checksums match | Green alert |
| Verify Mismatch | Checksums differ | Red alert |
| Verify Error (404) | No shadow table | Toast error "Run rebuild first" |

**No polling, no progress, no logs.** Single synchronous request. HttpClient timeout must accommodate rebuild duration (30s configured, extendable).

---

## 6. DTOs (UI-side only)

```
PagedResult<T>
  Items: List<T>, TotalCount: int, Page: int, PageSize: int

HealthStatusDto
  Status: string ("healthy"|"degraded"|"unhealthy"), ProjectionLag: double, LastCheck: DateTime

StockSummaryDto
  TotalSKUs: int, TotalQuantity: decimal, TotalValue: decimal

ReservationSummaryDto
  Allocated: int, Picking: int, Consumed: int

ProjectionHealthDto
  LocationBalanceLag: double, AvailableStockLag: double, LastRebuildLB: DateTime?, LastRebuildAS: DateTime?

RecentMovementDto
  MovementId: Guid, SKU: string, Quantity: decimal, FromLocation: string, ToLocation: string, Timestamp: DateTime

AvailableStockItemDto
  WarehouseId: string, Location: string, SKU: string, PhysicalQty: decimal, ReservedQty: decimal,
  AvailableQty: decimal, LastUpdated: DateTime

WarehouseDto
  Id: string, Code: string, Name: string

ReservationDto
  ReservationId: Guid, Purpose: string, Priority: int, Status: string, LockType: string,
  CreatedAt: DateTime, Lines: List<ReservationLineDto>

ReservationLineDto
  SKU: string, RequestedQty: decimal, AllocatedQty: decimal, Location: string, WarehouseId: string,
  AllocatedHUs: List<AllocatedHUDto>

AllocatedHUDto
  HuId: Guid, LPN: string, Qty: decimal

StartPickingResponseDto
  Success: bool, Message: string

PickResponseDto
  Success: bool, Message: string

RebuildResultDto
  ProjectionName: string, EventsProcessed: int, ProductionChecksum: string, ShadowChecksum: string,
  ChecksumMatch: bool, Swapped: bool, Duration: TimeSpan

VerifyResultDto
  ChecksumMatch: bool, ProductionChecksum: string, ShadowChecksum: string,
  ProductionRowCount: int, ShadowRowCount: int
```

---

## 7. DomainErrorCode → User Message Mapping

Defined in `Infrastructure/ErrorCodeMessages.cs` as `static Dictionary<string, string>`:

**Domain-Specific Codes** (from domain logic):
| ErrorCode | HTTP | User Message |
|-----------|------|-------------|
| `INSUFFICIENT_BALANCE` | 422 | Insufficient balance. Cannot complete operation. |
| `RESERVATION_NOT_ALLOCATED` | 400 | Reservation is not in ALLOCATED state. Cannot start picking. |
| `HARD_LOCK_CONFLICT` | 409 | HARD lock conflict detected. Another reservation is already picking this stock. |
| `INVALID_PROJECTION_NAME` | 400 | Invalid projection name. Must be LocationBalance or AvailableStock. |
| `IDEMPOTENCY_IN_PROGRESS` | 409 | Request is currently being processed. Please wait. |
| `IDEMPOTENCY_ALREADY_PROCESSED` | 409 | Request already processed. Idempotency key conflict. |
| `CONCURRENCY_CONFLICT` | 409 | Concurrent modification detected. Please retry. |

**Generic Codes** (from `SharedKernel.DomainErrorCodes`, used by middleware for non-domain failures):
| ErrorCode | HTTP | User Message |
|-----------|------|-------------|
| `VALIDATION_ERROR` | 400 | One or more validation errors occurred. |
| `NOT_FOUND` | 404 | The requested resource was not found. |
| `UNAUTHORIZED` | 401 | Authentication required. |
| `FORBIDDEN` | 403 | You do not have permission to perform this action. |
| `INTERNAL_ERROR` | 500 | Server error. Please try again later. |

**Fallback Messages** (when no error code present):
| Scenario | HTTP | User Message |
|----------|------|-------------|
| *(unknown code)* | * | An unexpected error occurred. Please try again. |
| *(no code, HTTP 500)* | 500 | Server error. Please try again later. |
| *(no code, HTTP 503)* | 503 | Backend unavailable. Please check system status. |

TraceId is always surfaced as: `"Error ID: {traceId}"` appended below the user message in `ErrorBanner`.

---

## 8. Deferred to Phase 2

| Feature | Reason | Expected Phase 2 Approach |
|---------|--------|--------------------------|
| **SignalR** | Polling sufficient for admin UI with 3-5 concurrent users | Replace 30s timer with SignalR hub for dashboard push |
| **Chart.js** | Dashboard uses count cards only, no time-series data yet | Add trend charts for stock movements, reservation throughput |
| **Auth / RBAC** | Placeholder structure only in Phase 1 | JWT auth, 3 roles (Admin, Manager, Operator), `[Authorize]` on controllers |
| **Column sorting** | Read-only admin view, filter is sufficient | Add `OrderBy` param to queries, sortable DataTable headers |
| **Server-side CSV streaming** | Current-page client-side export is sufficient for ≤100 rows | `text/csv` streaming endpoint with `IAsyncEnumerable` for full dataset export |
| **Async projection rebuild** | Sync rebuild adequate for Phase 1 data volumes (<60s) | `BackgroundService` with jobId, `GET /status` polling, progress bar, log streaming |
| **Mobile operator workflows** | Desktop admin-only in Phase 1 | Responsive layout, barcode scanner integration, offline PWA |
| **Audit logging UI** | No audit trail display needed yet | Event stream viewer, user action log, export |
| **Advanced search** | Basic wildcard filters sufficient | Full-text search, date range filters, saved searches |
| **Batch operations** | Single-item actions sufficient | Multi-select in tables, bulk StartPicking, bulk allocation |

---

**End of spec-ui.md**
