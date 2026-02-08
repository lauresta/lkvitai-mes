# UI Requirements: Warehouse Core Phase 1 - UI Slice 0 (Admin MVP)

**Project:** LKvitai.MES Warehouse Management System  
**Version:** 1.0  
**Date:** February 8, 2026  
**Status:** Requirements Document (Governance Approved)

---

## 1. Scope

### In Scope

UI Slice 0 delivers an **admin-style MVP** for warehouse operations monitoring and management with four core pages:

1. **Dashboard** - System health monitoring, stock/reservation summaries, projection health metrics
2. **Available Stock Search** - Query available stock by warehouse/location/SKU with virtual location filtering
3. **Reservations List** - View active reservations (ALLOCATED/PICKING) with StartPicking and Pick actions
4. **Projections Admin** - Rebuild and verify LocationBalance and AvailableStock projections

**Technology Stack:**
- Frontend: Blazor Server (.NET 8) + Bootstrap 5
- Backend: ASP.NET Core 8 Web API + MediatR + Marten

### Out of Scope (Deferred to Phase 2)

- SignalR real-time updates (polling sufficient for Phase 1)
- Chart.js visualizations (count cards only in Phase 1)
- Asynchronous projection rebuild with jobId/status polling/progress bars/log streaming
- Mobile operator workflows
- Full authentication and authorization (placeholder structure only)
- Role-based access control (RBAC)
- Advanced filtering and search capabilities
- Batch operations and bulk actions
- Audit logging UI
- Performance dashboards with charts

---

## 2. Users and Roles

### Target Users (Phase 1 - No Auth)

- **Warehouse Managers**: Monitor stock levels, reservation status, projection health
- **System Administrators**: Rebuild projections, verify data consistency, troubleshoot issues
- **Operations Team**: View active reservations, start picking operations, monitor picks

**Note:** Phase 1 includes placeholder authentication structure only. Full auth implementation deferred to Phase 2.

---

## 3. Functional Requirements

### 3.1 Dashboard Page

**Purpose:** Provide at-a-glance system health monitoring and operational metrics.

**User Actions:**
- View system health status (healthy/degraded/unhealthy)
- View stock summary (total SKUs, quantity, value)
- View reservation summary (counts by status: ALLOCATED, PICKING, CONSUMED)
- View projection health (lag metrics, last rebuild timestamps)
- View recent activity feed (last 10 stock movements)
- Auto-refresh every 30 seconds

**Data Shown:**
- Health status card: Color-coded indicator (green/yellow/red), projection lag in seconds
- Stock summary card: Total SKUs, total quantity, total value
- Reservation summary card: Counts by status with color-coded badges
- Projection health card: LocationBalance and AvailableStock lag + last rebuild timestamps
- Recent activity feed: Scrollable list of last 10 movements (SKU, quantity, from/to location, timestamp)

**States:**
- Loading: Display spinner while fetching data
- Success: Display all cards with data
- Error (503): Display "Backend unavailable" banner
- Error (500): Display "Query failed" with retry button
- Stale data warning: Yellow badge if projection lag > 5s
- Stale data alert: Red badge if projection lag > 30s

**Pagination/Sorting:** Not applicable (fixed dashboard layout)

---

### 3.2 Available Stock Search Page

**Purpose:** Enable warehouse managers to search and view available stock across warehouses and locations.

**User Actions:**
- Filter by warehouse (dropdown selection)
- Filter by location (text input with wildcard support using *)
- Filter by SKU (text input with wildcard support using *)
- Toggle virtual location visibility (default: hidden)
- Navigate pages (Previous/Next)
- Change page size (25/50/100 items per page)
- Export current page to CSV

**Data Shown:**
- Search filters: Warehouse dropdown, Location text input, SKU text input, "Show virtual locations" checkbox
- Data table columns: Warehouse, Location, SKU, Physical Qty, Reserved Qty, Available Qty, Last Updated
- Stale data badge: Yellow if lastUpdated > 5s, red if lastUpdated > 30s
- Pagination controls: Page X of Y, Previous/Next buttons, page size selector

**States:**
- Initial: Empty table with filters visible
- Loading: Display spinner while searching
- Success: Display data table with results
- Empty result: Display "No stock found matching filters"
- Error (400): Display "Invalid filters" with field-specific errors
- Error (500): Display "Query failed" with retry button

**Validation:**
- At least one filter must be provided (warehouse OR location OR sku)
- Page must be >= 1
- PageSize must be one of [25, 50, 100]

**Virtual Location Filtering:**
- Default behavior: Hide virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM)
- Toggle checkbox: Show virtual locations when checked
- Backend filters WHERE location NOT IN ('SUPPLIER', 'PRODUCTION', 'SCRAP', 'SYSTEM') when includeVirtual=false

**Pagination/Sorting:**
- Default page size: 50 items
- Supported page sizes: 25, 50, 100
- Sorting: Not implemented in Phase 1 (deferred to Phase 2)

---

### 3.3 Reservations List Page

**Purpose:** Enable operations team to view active reservations and execute picking operations.

**User Actions:**
- Filter by status (All, ALLOCATED, PICKING)
- View reservation details (click row to open modal)
- Start picking operation (button visible for ALLOCATED reservations)
- Execute pick (button visible for PICKING reservations, opens modal)
- Navigate pages (Previous/Next)
- Change page size (25/50/100 items per page)

**Data Shown:**
- Status filter: Dropdown with options (All, ALLOCATED, PICKING)
- Data table columns: Reservation ID, Purpose, Priority, Status, Lock Type, Created At, Actions
- Actions column: Conditional buttons based on status
  - "Start Picking" button (if status=ALLOCATED)
  - "Pick" button (if status=PICKING)
- Reservation details modal: Lines with SKU, requested qty, allocated HUs (HU ID, LPN, qty)
- Pick modal: HU selection dropdown, quantity input field

**States:**
- Initial: Empty table with status filter visible
- Loading: Display spinner while fetching reservations
- Success: Display data table with results
- Empty result: Display "No reservations found"
- Error (400): Display "Invalid filters" with retry button
- Error (500): Display "Query failed" with retry button
- Start Picking success: Display "Picking started successfully" toast, refresh table
- Start Picking error (409): Display "HARD lock conflict. Another reservation is picking this stock."
- Start Picking error (422): Display "Insufficient balance. Cannot start picking."
- Pick success: Display "Pick completed successfully" toast, close modal, refresh table
- Pick error (404): Display "Handling unit not found"
- Pick error (422): Display "Quantity exceeds allocated"

**Validation:**
- Pick quantity must be > 0 and <= allocated quantity for selected HU

**Pagination/Sorting:**
- Default page size: 50 items
- Supported page sizes: 25, 50, 100
- Sorting: Not implemented in Phase 1 (deferred to Phase 2)

---

### 3.4 Projections Admin Page

**Purpose:** Enable system administrators to rebuild and verify projection data consistency.

**User Actions:**
- Select projection (radio buttons: LocationBalance, AvailableStock)
- Rebuild projection (button with confirmation dialog)
- Verify projection (button, no confirmation required)

**Data Shown:**
- Projection selection: Radio buttons for LocationBalance and AvailableStock
- Rebuild button: Triggers synchronous rebuild with confirmation
- Verify button: Triggers checksum verification
- Status card: Shows current status (Idle, Running, Completed, Failed)
- Spinner: Displayed during synchronous rebuild (blocks until complete)
- Result detail card: Shows eventsProcessed, duration, productionChecksum, shadowChecksum, checksumMatch, swapped
  - Green card if checksumMatch=true and swapped=true
  - Red card if checksumMatch=false

**States:**
- Initial: Idle status, no results displayed
- Rebuild confirmation: Display "Are you sure? This will rebuild the projection from scratch."
- Rebuild running: Display spinner, disable buttons
- Rebuild success: Display green result card with metrics
- Rebuild error (400): Display "Invalid projection name"
- Rebuild error (409): Display "Rebuild already running. Please wait."
- Rebuild error (500): Display "Command failed" with retry button
- Verify success (checksum match): Display green alert "Verification passed. Safe to swap to production."
- Verify success (checksum mismatch): Display red alert "Verification failed. Checksums do not match. Do not swap to production."
- Verify error (404): Display "Shadow table not found. Run rebuild first."
- Verify error (500): Display "Query failed" with retry button

**Pagination/Sorting:** Not applicable (single projection operation at a time)

**Phase 1 Constraint:** Rebuild is synchronous. No jobId, no status polling, no progress bar, no log streaming. UI displays spinner and blocks until rebuild completes.

---

## 4. API Requirements

### 4.1 Dashboard APIs

**GET /api/dashboard/health**
- Query params: None
- Response: `{ status: "healthy" | "degraded" | "unhealthy", projectionLag: number, lastCheck: DateTime }`
- Errors: 503 (backend unavailable), 500 (query failure)

**GET /api/dashboard/stock-summary**
- Query params: None
- Response: `{ totalSKUs: number, totalQuantity: number, totalValue: number }`
- Errors: 500 (query failure)

**GET /api/dashboard/reservation-summary**
- Query params: None
- Response: `{ allocated: number, picking: number, consumed: number }`
- Errors: 500 (query failure)

**GET /api/dashboard/projection-health**
- Query params: None
- Response: `{ locationBalanceLag: number, availableStockLag: number, lastRebuildLB: DateTime, lastRebuildAS: DateTime }`
- Errors: 500 (query failure)

**GET /api/dashboard/recent-activity**
- Query params: `limit` (default: 10)
- Response: `{ movements: [{ movementId, sku, quantity, fromLocation, toLocation, timestamp }] }`
- Errors: 500 (query failure)

---

### 4.2 Available Stock APIs

**GET /api/available-stock**
- Query params: `warehouse` (optional), `location` (optional, wildcard with *), `sku` (optional, wildcard with *), `includeVirtual` (bool, default: false), `page` (int, default: 1), `pageSize` (int, default: 50)
- Response: `{ items: [{ warehouseId, location, sku, physicalQty, reservedQty, availableQty, lastUpdated }], totalCount: number, page: number, pageSize: number }`
- Validation: At least one of warehouse/location/sku must be provided
- Errors: 400 (invalid filters), 500 (query failure)

**GET /api/warehouses**
- Query params: None
- Response: `{ warehouses: [{ id, code, name }] }`
- Errors: 500 (query failure)

---

### 4.3 Reservations APIs

**GET /api/reservations**
- Query params: `status` (optional: "ALLOCATED" | "PICKING"), `page` (int, default: 1), `pageSize` (int, default: 50)
- Response: `{ items: [{ reservationId, purpose, priority, status, lockType, createdAt, lines: [{ sku, requestedQty, allocatedHUs: [{ huId, lpn, qty }] }] }], totalCount: number, page: number, pageSize: number }`
- Errors: 400 (invalid filters), 500 (query failure)

**POST /api/reservations/{id}/start-picking**
- Request body: `{ reservationId: Guid }`
- Response: `{ success: true, message: "Picking started" }`
- Errors: 400 (reservation not ALLOCATED), 409 (HARD lock conflict), 422 (insufficient balance), 500 (command failure)

**POST /api/reservations/{id}/pick**
- Request body: `{ reservationId: Guid, huId: Guid, sku: string, quantity: number }`
- Response: `{ success: true, message: "Pick completed" }`
- Errors: 400 (reservation not PICKING), 404 (HU not found), 422 (quantity exceeds allocated), 500 (command failure)

---

### 4.4 Projections APIs

**POST /api/projections/rebuild**
- Request body: `{ projectionName: "LocationBalance" | "AvailableStock", verify: bool }`
- Response (synchronous): `{ projectionName, eventsProcessed, productionChecksum, shadowChecksum, checksumMatch, swapped, duration }`
- Errors: 400 (invalid projection name, INVALID_PROJECTION_NAME code), 409 (rebuild already running), 500 (rebuild failure)
- **Note:** Synchronous operation. Blocks until rebuild completes. No jobId or polling.

**POST /api/projections/verify**
- Request body: `{ projectionName: "LocationBalance" | "AvailableStock" }`
- Response: `{ checksumMatch: bool, productionChecksum: string, shadowChecksum: string, productionRowCount: number, shadowRowCount: number }`
- Errors: 400 (invalid projection name), 404 (shadow table not found), 500 (query failure)

---

## 5. Error Model (Governance Approved)

### ProblemDetails Contract

All API errors return RFC 7807 ProblemDetails format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "quantity": ["Quantity must be greater than 0"]
  },
  "traceId": "00-abc123-def456-00"
}
```

**Required Fields:**
- `type`: URI reference identifying the problem type
- `title`: Human-readable summary
- `status`: HTTP status code
- `traceId`: Correlation ID for support and observability

**Optional Fields:**
- `errors`: Dictionary of field-specific validation errors (for 400 Bad Request)
- `detail`: Additional human-readable explanation

---

### DomainErrorCode to HTTP Status Mapping

| DomainErrorCode | HTTP Status | Title | User-Friendly Message |
|-----------------|-------------|-------|----------------------|
| INSUFFICIENT_BALANCE | 422 Unprocessable Entity | Insufficient Balance | Insufficient balance. Cannot complete operation. |
| RESERVATION_NOT_ALLOCATED | 400 Bad Request | Invalid Reservation State | Reservation is not in ALLOCATED state. Cannot start picking. |
| HARD_LOCK_CONFLICT | 409 Conflict | Lock Conflict | HARD lock conflict detected. Another reservation is already picking this stock. |
| INVALID_PROJECTION_NAME | 400 Bad Request | Invalid Projection Name | Invalid projection name. Must be LocationBalance or AvailableStock. |
| IDEMPOTENCY_IN_PROGRESS | 409 Conflict | Request In Progress | Request is currently being processed. Please wait. |
| IDEMPOTENCY_ALREADY_PROCESSED | 409 Conflict | Duplicate Request | Request already processed. Idempotency key conflict. |
| CONCURRENCY_CONFLICT | 409 Conflict | Concurrent Modification | Concurrent modification detected. Please retry. |
| VALIDATION_ERROR | 400 Bad Request | Validation Failed | One or more validation errors occurred. |
| NOT_FOUND | 404 Not Found | Resource Not Found | The requested resource was not found. |
| UNAUTHORIZED | 401 Unauthorized | Unauthorized | Authentication required. |
| FORBIDDEN | 403 Forbidden | Forbidden | You do not have permission to perform this action. |

**Generic HTTP Status Messages:**
- **500 Internal Server Error**: "Server error. Please try again later."
- **503 Service Unavailable**: "Backend unavailable. Please check system status."

---

## 6. Non-Functional Requirements

### Performance Targets

- **Query Response Time**: < 2 seconds for all read operations (dashboard, search, list)
- **Command Response Time**: < 5 seconds for write operations (StartPicking, Pick)
- **Projection Rebuild**: < 60 seconds for LocationBalance and AvailableStock (admin-grade, not user-facing)
- **Auto-Refresh Interval**: 30 seconds for dashboard

### Pagination Defaults

- **Default Page Size**: 50 items
- **Supported Page Sizes**: 25, 50, 100
- **Max Page Size**: 100 (enforced by backend)

### Projection Lag Thresholds

- **Fresh Data**: < 5 seconds (no indicator)
- **Stale Warning**: 5-30 seconds (yellow badge)
- **Stale Alert**: > 30 seconds (red badge)

### Observability

- **TraceId Surfacing**: All error messages must display traceId for support escalation
- **Error Logging**: All errors logged with traceId, user context, and request details
- **Performance Monitoring**: Track query response times and projection lag metrics

### Security (Phase 1 Placeholder)

- **Authentication**: Placeholder structure only. No actual auth enforcement in Phase 1.
- **Authorization**: No RBAC design in Phase 1. All users have full access.
- **HTTPS**: Required for all API communication (enforced by infrastructure)
- **Input Validation**: All user inputs validated on backend (SQL injection prevention, XSS prevention)

**Phase 2 Security Requirements (Deferred):**
- Full authentication with JWT tokens
- Role-based access control (Admin, Manager, Operator)
- Audit logging for all write operations
- Session management and timeout

---

## 7. Acceptance Criteria

1. Dashboard displays system health status with color-coded indicator (green/yellow/red) based on projection lag.
2. Dashboard auto-refreshes every 30 seconds without user interaction.
3. Available Stock search requires at least one filter (warehouse OR location OR sku) and displays validation error if none provided.
4. Available Stock search hides virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM) by default and shows them only when "Show virtual locations" is checked.
5. Available Stock search displays stale data badge (yellow if > 5s, red if > 30s) in Last Updated column.
6. Reservations list displays "Start Picking" button only for ALLOCATED reservations and "Pick" button only for PICKING reservations.
7. Start Picking action displays specific error message "HARD lock conflict. Another reservation is picking this stock." on 409 Conflict error.
8. Pick action validates quantity > 0 and <= allocated quantity before submission and displays field-specific error if validation fails.
9. Projections rebuild displays confirmation dialog "Are you sure? This will rebuild the projection from scratch." before execution.
10. Projections rebuild displays spinner during synchronous operation and blocks UI until completion (no polling or progress bar).
11. Projections verify displays green success alert "Verification passed. Safe to swap to production." on checksum match.
12. All error messages display traceId for support escalation (format: "Error ID: 00-abc123-def456-00").

---

## 8. Traceability Notes

### Package Coverage

- **UI-0 (Foundation)**: Shared components (DataTable, Pagination, ErrorBanner, LoadingIndicator, StaleDataBadge), ProblemDetails error handler, MainLayout
- **UI-Res-Index (Reservation Projection)**: ReservationSummaryView read model, ReservationSummaryProjection, SearchReservationsQuery, GetReservationDetailQuery (BLOCKER for UI-3)
- **UI-1 (Dashboard)**: Dashboard page, health/stock/reservation/projection summary APIs, dashboard DTOs and services
- **UI-2 (Available Stock)**: Available Stock search page, search filters, data table, virtual location filtering, pagination, CSV export
- **UI-3 (Reservations)**: Reservations list page, status filter, reservation details modal, pick modal, StartPicking and Pick actions
- **UI-4 (Projections)**: Projections admin page, projection selection, rebuild action (synchronous), verify action, result display

### Backend Dependencies

- **Existing (Done)**: LocationBalance projection, AvailableStock projection, ActiveHardLocks projection, HandlingUnit projection, StartPickingCommand, PickStockCommand, RebuildProjectionCommand, Projection rebuild service
- **New (Required)**: ProblemDetails mapper middleware (UI-0.2a), Missing DomainErrorCodes (UI-0.2b), ReservationSummaryProjection (UI-Res-Index), Dashboard query handlers (UI-1), Reservation query handlers (UI-Res-Index), Warehouse configuration query (UI-2), Rebuild synchronous update (UI-4)

### Technology Constraints

- **UI Stack**: Blazor Server (.NET 8) + Bootstrap 5 ONLY
- **Phase 1 Exclusions**: NO SignalR, NO Chart.js, NO async rebuild jobs with polling
- **Projection Rebuild**: Synchronous operation (blocks until complete, no jobId/status/progress/logs)
- **Virtual Locations**: Filtered in API/UI, NOT changed in projections
- **Auth**: Placeholder structure only, no enforcement in Phase 1

---

**End of Requirements Document**
