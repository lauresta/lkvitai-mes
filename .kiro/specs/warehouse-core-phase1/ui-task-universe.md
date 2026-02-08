# UI Task Universe: Warehouse Core Phase 1 - UI Slice 0

**Project:** LKvitai.MES Warehouse Management System  
**Spec:** warehouse-core-phase1-ui  
**Version:** 1.0  
**Date:** February 2026  
**Status:** UI Implementation Plan

---

## Document Purpose

This document provides the complete UI implementation task universe for **UI Slice 0** (Admin-style MVP) of Warehouse Core Phase 1. It maps required API endpoints, DTOs, validation, error handling, and generates tasks suitable for implementation in small packages (10-20 tasks per package).

**CRITICAL CONSTRAINTS:**
- Do NOT redesign backend domain
- Prefer filtering virtual locations in API or UI, not changing projections in Phase 1
- Keep auth out (Phase 2), but add placeholder TODO and non-invasive structure
- Total tasks: ~50-80 (not 150+)
- Focus Phase 1 only

**Baseline Status:**
- Backend provides: LocationBalance, AvailableStock, Reservations (ALLOCATED/PICKING), StartPicking, Pick, Projection rebuild (LocationBalance+AvailableStock) + status
- Error model: ProblemDetails + SharedKernel.DomainErrorCodes
- Known non-blocking items: orphan hard locks by design, virtual locations pollution (filter in UI)

---

## Table of Contents

1. [UI Requirements](#ui-requirements)
2. [Page Specifications](#page-specifications)
3. [Task Packages](#task-packages)
4. [Traceability Matrix](#traceability-matrix)

---

## UI Requirements

### Overview

UI Slice 0 is an **admin-style MVP** focused on warehouse operations monitoring and management. It provides essential visibility into stock movements, reservations, and projection health without full operator workflows (those come in Phase 2).


### Target Users

- **Warehouse Managers**: Monitor stock levels, reservation status, projection health
- **System Administrators**: Rebuild projections, view system health, troubleshoot issues
- **Operations Team**: View active reservations, start picking operations, monitor picks

### Core Principles

1. **Read-Heavy**: UI Slice 0 is primarily for monitoring and querying (80% reads, 20% writes)
2. **Admin-First**: Desktop-optimized, data tables, filters, bulk operations
3. **No Mobile**: Mobile operator workflows deferred to Phase 2
4. **No Auth**: Placeholder structure only, full auth in Phase 2
5. **Error Transparency**: Show ProblemDetails errors clearly with corrective actions
6. **Virtual Location Filtering**: Hide virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM) in UI by default

### Non-Functional Requirements

- **Response Time**: < 2s for queries, < 5s for commands
- **Projection Lag Indicator**: Show stale data warning if lag > 5s
- **Error Handling**: Display ProblemDetails with error codes and user-friendly messages
- **Pagination**: All lists support pagination (default 50 items per page)
- **Export**: All data tables support CSV export
- **Accessibility**: WCAG 2.1 AA compliance (keyboard navigation, screen reader support)

---

## Page Specifications

### Page 1: Dashboard (Health + Counts + Last Rebuilds)

**User Story:** As a warehouse manager, I want to see system health at a glance, so that I can quickly identify issues.


#### UI Components

- **Health Status Card**: Green/Yellow/Red indicator for overall system health
- **Stock Summary Card**: Total SKUs, total quantity, total value
- **Reservation Summary Card**: Active reservations by status (ALLOCATED, PICKING)
- **Projection Health Card**: Projection lag metrics, last rebuild timestamps
- **Recent Activity Feed**: Last 10 stock movements (scrollable list)

#### Data Sources (API Calls)

**GET /api/dashboard/health**
- Response: `{ status: "healthy" | "degraded" | "unhealthy", projectionLag: number, lastCheck: DateTime }`
- Error: 503 Service Unavailable if backend down

**GET /api/dashboard/stock-summary**
- Response: `{ totalSKUs: number, totalQuantity: number, totalValue: number }`
- Error: 500 Internal Server Error on query failure

**GET /api/dashboard/reservation-summary**
- Response: `{ allocated: number, picking: number, consumed: number }`
- Error: 500 Internal Server Error on query failure

**GET /api/dashboard/projection-health**
- Response: `{ locationBalanceLag: number, availableStockLag: number, lastRebuildLB: DateTime, lastRebuildAS: DateTime }`
- Error: 500 Internal Server Error on query failure

**GET /api/dashboard/recent-activity?limit=10**
- Response: `{ movements: [{ movementId, sku, quantity, fromLocation, toLocation, timestamp }] }`
- Error: 500 Internal Server Error on query failure

#### Validation Rules

- No user input validation (read-only page)
- Refresh interval: 30 seconds (auto-refresh)

#### Error States (ProblemDetails Code Mapping)

- **503 Service Unavailable**: Display "Backend unavailable" banner
- **500 Internal Server Error**: Display "Query failed" with retry button
- **Projection Lag > 5s**: Display yellow warning "Data may be stale"
- **Projection Lag > 30s**: Display red alert "Data is stale, consider rebuild"


#### Acceptance Criteria

- [ ] Dashboard displays health status card with color-coded indicator
- [ ] Dashboard displays stock summary card with totals
- [ ] Dashboard displays reservation summary card with counts by status
- [ ] Dashboard displays projection health card with lag metrics
- [ ] Dashboard displays recent activity feed with last 10 movements
- [ ] Dashboard auto-refreshes every 30 seconds
- [ ] Dashboard displays stale data warning if projection lag > 5s
- [ ] Dashboard displays stale data alert if projection lag > 30s
- [ ] Dashboard displays error banner if backend unavailable
- [ ] Dashboard displays retry button on query failure

---

### Page 2: Available Stock Search (Warehouse/Location/SKU, Hide Virtual Locations)

**User Story:** As a warehouse manager, I want to search available stock by warehouse, location, and SKU, so that I can find inventory quickly.

#### UI Components

- **Search Filters**: Warehouse dropdown, Location text input, SKU text input
- **Virtual Location Toggle**: Checkbox to show/hide virtual locations (default: hidden)
- **Data Table**: Columns: Warehouse, Location, SKU, Physical Qty, Reserved Qty, Available Qty, Last Updated
- **Pagination Controls**: Previous, Next, Page size selector (25/50/100)
- **Export Button**: Export to CSV
- **Stale Data Indicator**: Yellow badge if last updated > 5s ago

#### Data Sources (API Calls)

**GET /api/available-stock?warehouse={id}&location={pattern}&sku={pattern}&includeVirtual={bool}&page={n}&pageSize={size}**
- Response: `{ items: [{ warehouseId, location, sku, physicalQty, reservedQty, availableQty, lastUpdated }], totalCount: number, page: number, pageSize: number }`
- Error: 400 Bad Request if invalid filters
- Error: 500 Internal Server Error on query failure


**GET /api/warehouses**
- Response: `{ warehouses: [{ id, code, name }] }`
- Error: 500 Internal Server Error on query failure

#### Validation Rules

- Warehouse: Optional, dropdown selection from /api/warehouses
- Location: Optional, text input (wildcard search with *)
- SKU: Optional, text input (wildcard search with *)
- At least one filter must be provided (warehouse OR location OR sku)
- Page: Integer >= 1
- PageSize: Integer in [25, 50, 100]

#### Error States (ProblemDetails Code Mapping)

- **400 Bad Request**: Display "Invalid filters" with field-specific errors
- **500 Internal Server Error**: Display "Query failed" with retry button
- **Empty Result**: Display "No stock found matching filters"
- **Stale Data (lastUpdated > 5s)**: Display yellow badge on row

#### Acceptance Criteria

- [ ] Search page displays warehouse dropdown populated from /api/warehouses
- [ ] Search page displays location text input with wildcard support
- [ ] Search page displays SKU text input with wildcard support
- [ ] Search page displays virtual location toggle (default: hidden)
- [ ] Search page validates at least one filter is provided
- [ ] Search page displays data table with all columns
- [ ] Search page displays pagination controls
- [ ] Search page displays export to CSV button
- [ ] Search page displays stale data badge if lastUpdated > 5s
- [ ] Search page displays error message on 400 Bad Request
- [ ] Search page displays retry button on 500 Internal Server Error
- [ ] Search page displays "No stock found" on empty result

---

### Page 3: Reservations List (ALLOCATED/PICKING) + Actions StartPicking/Pick

**User Story:** As a warehouse operator, I want to view active reservations and start picking operations, so that I can fulfill orders.


#### UI Components

- **Status Filter**: Dropdown (All, ALLOCATED, PICKING)
- **Data Table**: Columns: Reservation ID, Purpose, Priority, Status, Lock Type, Created At, Actions
- **Actions Column**: 
  - "Start Picking" button (visible if status=ALLOCATED)
  - "Pick" button (visible if status=PICKING)
- **Pagination Controls**: Previous, Next, Page size selector (25/50/100)
- **Reservation Details Modal**: Shows lines (SKU, requested qty, allocated HUs)

#### Data Sources (API Calls)

**GET /api/reservations?status={status}&page={n}&pageSize={size}**
- Response: `{ items: [{ reservationId, purpose, priority, status, lockType, createdAt, lines: [{ sku, requestedQty, allocatedHUs: [{ huId, lpn, qty }] }] }], totalCount: number, page: number, pageSize: number }`
- Error: 400 Bad Request if invalid filters
- Error: 500 Internal Server Error on query failure

**POST /api/reservations/{id}/start-picking**
- Request: `{ reservationId: Guid }`
- Response: `{ success: true, message: "Picking started" }`
- Error: 400 Bad Request if reservation not ALLOCATED
- Error: 409 Conflict if HARD lock conflict detected
- Error: 422 Unprocessable Entity if insufficient balance
- Error: 500 Internal Server Error on command failure

**POST /api/reservations/{id}/pick**
- Request: `{ reservationId: Guid, huId: Guid, sku: string, quantity: number }`
- Response: `{ success: true, message: "Pick completed" }`
- Error: 400 Bad Request if reservation not PICKING
- Error: 404 Not Found if HU not found
- Error: 422 Unprocessable Entity if quantity exceeds allocated
- Error: 500 Internal Server Error on command failure


#### Validation Rules

- Status: Optional, dropdown selection (All, ALLOCATED, PICKING)
- Page: Integer >= 1
- PageSize: Integer in [25, 50, 100]
- Start Picking: No user input (button click)
- Pick: Quantity must be > 0 and <= allocated quantity

#### Error States (ProblemDetails Code Mapping)

- **400 Bad Request**: Display "Invalid request" with field-specific errors
- **404 Not Found**: Display "Handling unit not found"
- **409 Conflict**: Display "HARD lock conflict detected. Another reservation is already picking this stock."
- **422 Unprocessable Entity**: Display "Insufficient balance" or "Quantity exceeds allocated"
- **500 Internal Server Error**: Display "Command failed" with retry button

#### Acceptance Criteria

- [ ] Reservations page displays status filter dropdown
- [ ] Reservations page displays data table with all columns
- [ ] Reservations page displays "Start Picking" button for ALLOCATED reservations
- [ ] Reservations page displays "Pick" button for PICKING reservations
- [ ] Reservations page displays pagination controls
- [ ] Reservations page displays reservation details modal on row click
- [ ] Start Picking button calls POST /api/reservations/{id}/start-picking
- [ ] Start Picking displays success message on 200 OK
- [ ] Start Picking displays error message on 409 Conflict (HARD lock conflict)
- [ ] Start Picking displays error message on 422 Unprocessable Entity (insufficient balance)
- [ ] Pick button opens modal with HU selection and quantity input
- [ ] Pick modal validates quantity > 0 and <= allocated
- [ ] Pick modal calls POST /api/reservations/{id}/pick
- [ ] Pick displays success message on 200 OK
- [ ] Pick displays error message on 404 Not Found (HU not found)
- [ ] Pick displays error message on 422 Unprocessable Entity (quantity exceeds allocated)

---

### Page 4: Projections Admin (Rebuild LB/AS with Verify, Show Result)

**User Story:** As a system administrator, I want to rebuild projections and verify results, so that I can fix data inconsistencies.


#### UI Components

- **Projection Selection**: Radio buttons (LocationBalance, AvailableStock)
- **Rebuild Button**: Triggers rebuild with confirmation dialog
- **Verify Button**: Triggers verification (checksum comparison)
- **Status Card**: Shows rebuild status (Idle, Running, Completed, Failed)
- **Spinner**: Displayed during synchronous rebuild call (replaces progress bar — no async tracking in Phase 1)
- **Result Table**: Shows verification results (checksum match/mismatch, row count)

#### Data Sources (API Calls)

**POST /api/projections/rebuild**
- Request: `{ projectionName: "LocationBalance" | "AvailableStock", verify: bool }`
- Response: `{ projectionName, eventsProcessed, productionChecksum, shadowChecksum, checksumMatch, swapped, duration }`
- Error: 400 Bad Request if invalid projection name (INVALID_PROJECTION_NAME code)
- Error: 500 Internal Server Error on rebuild failure
- Note: Synchronous. Returns when rebuild completes. Show spinner in UI.

**POST /api/projections/verify**
- Request: `{ projectionName: "LocationBalance" | "AvailableStock" }`
- Response: `{ checksumMatch: bool, productionChecksum: string, shadowChecksum: string, productionRowCount: number, shadowRowCount: number }`
- Error: 400 Bad Request if invalid projection name
- Error: 404 Not Found if shadow table not found
- Error: 500 Internal Server Error on query failure


#### Validation Rules

- Projection Name: Required, radio button selection (LocationBalance, AvailableStock)
- Rebuild: Confirmation dialog required ("Are you sure? This will rebuild the projection from scratch.")
- Verify: No confirmation required (read-only operation)

#### Error States (ProblemDetails Code Mapping)

- **400 Bad Request**: Display "Invalid projection name"
- **404 Not Found**: Display "Shadow table not found. Run rebuild first."
- **409 Conflict**: Display "Rebuild already running. Please wait."
- **500 Internal Server Error**: Display "Command failed" with retry button
- **Checksum Mismatch**: Display red alert "Verification failed. Checksums do not match. Do not swap to production."
- **Checksum Match**: Display green success "Verification passed. Safe to swap to production."

#### Acceptance Criteria

- [ ] Projections page displays projection selection radio buttons
- [ ] Projections page displays rebuild button
- [ ] Projections page displays verify button
- [ ] Projections page displays status card
- [ ] Projections page displays spinner during rebuild
- [ ] Projections page displays result table when rebuild completes
- [ ] Rebuild button shows confirmation dialog
- [ ] Rebuild button calls POST /api/projections/rebuild
- [ ] Rebuild displays success message on 200 OK
- [ ] Rebuild displays error message on 409 Conflict (already running)
- [ ] Verify button calls POST /api/projections/verify
- [ ] Verify displays green success on checksum match
- [ ] Verify displays red alert on checksum mismatch
- [ ] Verify displays error message on 404 Not Found (shadow table not found)

---

## Task Packages


### Package UI-0: Foundation & Shared Components (8 tasks)

**Duration:** 3 days  
**Dependencies:** None

#### Tasks

**UI-0.1: Create Blazor Server Project**
- Create new Blazor Server project: `LKvitai.MES.WebUI`
- Configure appsettings.json with API base URL
- Add HttpClient with base address
- Add Serilog logging
- **Tests**: None (project setup)
- **Cursor Minimal Context:**
```
Files: LKvitai.MES.WebUI.csproj (new), Program.cs (new), appsettings.json (new)
Template: dotnet new blazorserver -n LKvitai.MES.WebUI
HttpClient: builder.Services.AddHttpClient with BaseAddress from config
Serilog: Add Serilog.AspNetCore package, configure in Program.cs
Config: appsettings.json with "ApiBaseUrl": "https://localhost:5001"
```

**UI-0.2: Implement ProblemDetails Error Handler**
- Create `ProblemDetailsException` class
- Create `ErrorHandler` service to parse ProblemDetails responses
- Map error codes to user-friendly messages
- **Tests**: Unit test error code mapping
- **Cursor Minimal Context:**
```
Files: WebUI/Services/ErrorHandler.cs (new), WebUI/Exceptions/ProblemDetailsException.cs (new)
Pattern: Parse HttpResponseMessage with ProblemDetails JSON body
Mapping: Dictionary<string, string> for error code → user message
Usage: Catch HttpRequestException, parse response, throw ProblemDetailsException
DI: Register as singleton service
```

**UI-0.2a: Create ResultToProblemDetails Middleware**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Add ASP.NET Core middleware that maps Result.Fail(errorCode) → RFC 7807 ProblemDetails
  - [ ] Map error codes to HTTP statuses: 409 (concurrency/idempotency), 422 (domain violations), 400 (validation)
  - [ ] Register middleware in Program.cs pipeline
  - [ ] Unit test: verify each DomainErrorCode maps to correct (status, type, title)
- **Cursor Minimal Context:**
```
Files: Api/Middleware/ResultToProblemDetailsMiddleware.cs, Api/Program.cs
Read: SharedKernel/Result.cs, DomainErrorCodes.cs
Pattern: ASP.NET Core ExceptionHandler middleware
Constraint: All Result.Fail → ProblemDetails with errorCode in "type" extension field
Register: app.UseMiddleware<ResultToProblemDetailsMiddleware>() before UseRouting
```

**UI-0.2b: Add Missing DomainErrorCodes**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Add to DomainErrorCodes.cs: INSUFFICIENT_BALANCE, RESERVATION_NOT_ALLOCATED, HARD_LOCK_CONFLICT, INVALID_PROJECTION_NAME
  - [ ] Update StockLedger.RecordMovement() to use INSUFFICIENT_BALANCE in DomainException
  - [ ] Update Reservation.ValidateCanStartPicking() to use RESERVATION_NOT_ALLOCATED
  - [ ] Update MartenStartPickingOrchestration HARD lock conflict path to use HARD_LOCK_CONFLICT
  - [ ] Unit test: verify each code appears in correct failure path
- **Cursor Minimal Context:**
```
Files: SharedKernel/DomainErrorCodes.cs, Domain/Aggregates/StockLedger.cs, Domain/Aggregates/Reservation.cs
Add: public const string INSUFFICIENT_BALANCE = "INSUFFICIENT_BALANCE";
Add: public const string RESERVATION_NOT_ALLOCATED = "RESERVATION_NOT_ALLOCATED";
Add: public const string HARD_LOCK_CONFLICT = "HARD_LOCK_CONFLICT";
Add: public const string INVALID_PROJECTION_NAME = "INVALID_PROJECTION_NAME";
```

**UI-0.3: Create Shared Layout Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `MainLayout.razor` with navigation menu
  - [ ] Add navigation links: Dashboard, Available Stock, Reservations, Projections
  - [ ] Add placeholder for auth (TODO: Phase 2)
- **Cursor Minimal Context:**
```
Files: WebUI/Shared/MainLayout.razor
Pattern: Blazor Server default layout template
Components: NavMenu with Bootstrap 5 nav classes
Auth: Add commented-out <AuthorizeView> with TODO: Phase 2
```
- **Tests**: None (UI component)

**UI-0.4: Create Data Table Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `DataTable.razor` generic component
  - [ ] Support columns, pagination, sorting
  - [ ] Support row click event
  - [ ] Support export to CSV (client-side, current page only)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/DataTable.razor
Pattern: Bootstrap 5 table with <thead>, <tbody>, pagination controls
Generic: @typeparam TItem, RenderFragment for columns
CSV: Blazor JS interop to download CSV from current page data
```
- **Tests**: Unit test pagination logic

**UI-0.5: Create Pagination Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `Pagination.razor` component
  - [ ] Support Previous, Next, Page size selector
  - [ ] Emit page change events
- **Cursor Minimal Context:**
```
Files: WebUI/Components/Pagination.razor
Pattern: Bootstrap 5 pagination component
Events: EventCallback<int> OnPageChanged, EventCallback<int> OnPageSizeChanged
Props: CurrentPage, TotalPages, PageSize, PageSizeOptions
```
- **Tests**: Unit test page navigation

**UI-0.6: Create Loading Indicator Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `LoadingIndicator.razor` component
  - [ ] Support spinner, progress bar, skeleton loader
- **Cursor Minimal Context:**
```
Files: WebUI/Components/LoadingIndicator.razor
Pattern: Bootstrap 5 spinner component
Variants: Spinner (default), ProgressBar (optional), Skeleton (optional)
Usage: <LoadingIndicator Type="Spinner" />
```
- **Tests**: None (UI component)


**UI-0.7: Create Error Banner Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `ErrorBanner.razor` component
  - [ ] Support error message, retry button, dismiss button
- **Cursor Minimal Context:**
```
Files: WebUI/Components/ErrorBanner.razor
Pattern: Bootstrap 5 alert component with alert-danger class
Props: ErrorMessage (string), OnRetry (EventCallback), OnDismiss (EventCallback)
Display: Show/hide based on ErrorMessage != null
```
- **Tests**: None (UI component)

**UI-0.8: Create Stale Data Badge Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `StaleDataBadge.razor` component
  - [ ] Display yellow badge if data age > 5s
  - [ ] Display red badge if data age > 30s
- **Cursor Minimal Context:**
```
Files: WebUI/Components/StaleDataBadge.razor
Pattern: Bootstrap 5 badge component
Logic: TimeSpan lag = DateTime.UtcNow - lastUpdated; badge-warning if lag > 5s, badge-danger if lag > 30s
Props: LastUpdated (DateTime)
```
- **Tests**: Unit test badge color logic

---

### Package UI-Res-Index: Reservation Summary Read Model (BLOCKER for UI-3)

**Duration:** 2 days  
**Dependencies:** UI-0  
**Blocker for:** UI-3

**Rationale:** Current `GetReservationsInStateAsync()` hydrates all reservation event streams sequentially (O(N×M) where N=reservations, M=events per stream). Will not scale beyond ~100 reservations. This package implements a lightweight read model for the reservations list page.

#### Tasks

**UI-Res-Idx.1: Create ReservationSummaryView Read Model**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Add ReservationSummaryView to Contracts/ReadModels
  - [ ] Fields: ReservationId, Purpose, Priority, Status, LockType, CreatedAt, PickingStartedAt, LineCount
  - [ ] Unit test: DTO serialization
- **Cursor Minimal Context:**
```
Files: Contracts/ReadModels/ReservationSummaryView.cs
Pattern: Existing AvailableStockView.cs as reference
Schema: Flat document for Marten projection
Key: ReservationId (string, stream key)
```

**UI-Res-Idx.2: Create ReservationSummaryProjection**
- **Dependencies:** UI-Res-Idx.1
- **Acceptance Criteria:**
  - [ ] Implement Marten SingleStreamProjection<ReservationSummaryView> with Async lifecycle
  - [ ] Subscribe to: ReservationCreatedEvent, StockAllocatedEvent, PickingStartedEvent, ReservationConsumedEvent, ReservationCancelledEvent, ReservationBumpedEvent
  - [ ] Register in ProjectionRegistration.cs
  - [ ] Unit test: Apply methods via aggregation helper (mirror existing pattern)
- **Cursor Minimal Context:**
```
Files: Projections/ReservationSummaryProjection.cs, Projections/ProjectionRegistration.cs
Pattern: Existing LocationBalanceProjection.cs as reference
Lifecycle: ProjectionLifecycle.Async
Events: ReservationCreated → PENDING, StockAllocated → ALLOCATED, PickingStarted → PICKING
```

**UI-Res-Idx.3: Create SearchReservationsQuery + Handler**
- **Dependencies:** UI-Res-Idx.2
- **Acceptance Criteria:**
  - [ ] Query params: status (optional filter), page, pageSize
  - [ ] Handler queries mt_doc_reservationsummaryview via Marten LINQ with pagination
  - [ ] Returns PagedResult<ReservationSummaryDto>
  - [ ] Integration test: query with filters + pagination
- **Cursor Minimal Context:**
```
Files: Application/Queries/SearchReservationsQuery.cs, Application/Queries/SearchReservationsQueryHandler.cs
Pattern: Existing GetAvailableStockQuery.cs as reference
Query: session.Query<ReservationSummaryView>().Where(r => status == null || r.Status == status)
Pagination: Skip((page-1)*pageSize).Take(pageSize)
```

**UI-Res-Idx.4: Create GetReservationDetailQuery + Handler**
- **Dependencies:** UI-Res-Idx.2
- **Acceptance Criteria:**
  - [ ] Loads single reservation aggregate via IReservationRepository.LoadAsync() for detail modal
  - [ ] Returns ReservationDetailDto with lines (SKU, requestedQty, allocatedQty, location, warehouseId, allocatedHUs)
  - [ ] Unit test: handler logic
- **Cursor Minimal Context:**
```
Files: Application/Queries/GetReservationDetailQuery.cs, Application/Queries/GetReservationDetailQueryHandler.cs
Pattern: Load aggregate from event stream, map to DTO
Repository: IReservationRepository.LoadAsync(reservationId)
DTO: Include full line details for modal display
```

---

### Package UI-1: Dashboard Page (10 tasks)

**Duration:** 4 days  
**Dependencies:** UI-0

#### Tasks

**UI-1.1: Create Dashboard API Endpoints**
- Implement `GET /api/dashboard/health`
- Implement `GET /api/dashboard/stock-summary`
- Implement `GET /api/dashboard/reservation-summary`
- Implement `GET /api/dashboard/projection-health`
- Implement `GET /api/dashboard/recent-activity`
- **Tests**: Integration test each endpoint
- **Cursor Minimal Context:**
```
Files: Api/Controllers/DashboardController.cs (new)
Queries: Create 5 new query handlers in Application/Queries/Dashboard/
Pattern: MediatR IRequestHandler<Query, Dto>
Health: Query Marten projection metadata for lag
Stock: Aggregate LocationBalance projection
Reservations: Count Reservation streams by status
Recent: Query last 10 StockMovement events
```

**UI-1.2: Create Dashboard DTOs**
- Create `HealthStatusDto`
- Create `StockSummaryDto`
- Create `ReservationSummaryDto`
- Create `ProjectionHealthDto`
- Create `RecentActivityDto`
- **Tests**: None (DTOs)
- **Cursor Minimal Context:**
```
Files: Contracts/DTOs/DashboardDtos.cs (new)
Pattern: Simple record types with properties
HealthStatusDto: status, projectionLag, lastCheck
StockSummaryDto: totalSKUs, totalQuantity, totalValue
ReservationSummaryDto: allocated, picking, consumed
ProjectionHealthDto: locationBalanceLag, availableStockLag, lastRebuildLB, lastRebuildAS
RecentActivityDto: movements array
```

**UI-1.3: Create Dashboard Service**
- Create `DashboardService` to call API endpoints
- Implement error handling with ProblemDetails
- **Tests**: Unit test API calls with mocked HttpClient
- **Cursor Minimal Context:**
```
Files: WebUI/Services/DashboardService.cs (new)
Pattern: HttpClient wrapper with async methods
Methods: GetHealthAsync(), GetStockSummaryAsync(), GetReservationSummaryAsync(), GetProjectionHealthAsync(), GetRecentActivityAsync()
Error: Catch HttpRequestException, parse ProblemDetails from response body
DI: Register as scoped service in Program.cs
```

**UI-1.4: Create Health Status Card Component**
- Create `HealthStatusCard.razor`
- Display green/yellow/red indicator
- Display projection lag metrics
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/HealthStatusCard.razor (new)
Pattern: Bootstrap 5 card with badge
Props: HealthStatusDto
Logic: Green if lag < 5s, Yellow if 5-30s, Red if > 30s
Display: Status badge + lag value in seconds
```


**UI-1.5: Create Stock Summary Card Component**
- Create `StockSummaryCard.razor`
- Display total SKUs, quantity, value
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/StockSummaryCard.razor (new)
Pattern: Bootstrap 5 card with list group
Props: StockSummaryDto
Display: 3 rows - Total SKUs, Total Quantity, Total Value
Format: Number formatting for quantity/value
```

**UI-1.6: Create Reservation Summary Card Component**
- Create `ReservationSummaryCard.razor`
- Display counts by status (ALLOCATED, PICKING)
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/ReservationSummaryCard.razor (new)
Pattern: Bootstrap 5 card with list group
Props: ReservationSummaryDto
Display: 3 rows - Allocated, Picking, Consumed
Badge: Color-coded badges for each status
```

**UI-1.7: Create Projection Health Card Component**
- Create `ProjectionHealthCard.razor`
- Display lag metrics, last rebuild timestamps
- Display stale data warning if lag > 5s
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/ProjectionHealthCard.razor (new)
Pattern: Bootstrap 5 card with table
Props: ProjectionHealthDto
Display: 2 rows (LocationBalance, AvailableStock) with lag + last rebuild timestamp
Warning: Yellow badge if lag > 5s, red if > 30s
```

**UI-1.8: Create Recent Activity Feed Component**
- Create `RecentActivityFeed.razor`
- Display last 10 stock movements
- Scrollable list
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/RecentActivityFeed.razor (new)
Pattern: Bootstrap 5 list group with scrollable container
Props: RecentActivityDto (array of movements)
Display: Each movement shows SKU, quantity, from/to location, timestamp
Scroll: max-height: 400px; overflow-y: auto
```

**UI-1.9: Create Dashboard Page**
- Create `Dashboard.razor` page
- Wire up all card components
- Implement auto-refresh every 30 seconds
- **Tests**: E2E test dashboard loads
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Dashboard.razor (new)
Pattern: Blazor page with @page "/dashboard"
Components: HealthStatusCard, StockSummaryCard, ReservationSummaryCard, ProjectionHealthCard, RecentActivityFeed
Service: Inject DashboardService
Refresh: Timer with 30s interval, call LoadDataAsync() on tick
Layout: Bootstrap grid with cards
```

**UI-1.10: Implement Dashboard Error Handling**
- Display error banner if backend unavailable (503)
- Display retry button on query failure (500)
- **Tests**: E2E test error states
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Dashboard.razor (update)
Component: ErrorBanner from UI-0.7
Logic: Catch exceptions in LoadDataAsync(), set errorMessage state
Display: Show ErrorBanner if errorMessage != null
Retry: OnRetry callback calls LoadDataAsync() again
```

---

### Package UI-2: Available Stock Search Page (12 tasks)

**Duration:** 5 days  
**Dependencies:** UI-0

#### Tasks

**UI-2.1: Create Available Stock API Endpoints**
- Implement `GET /api/available-stock`
- Support filters: warehouse, location, sku, includeVirtual
- Support pagination: page, pageSize
- Filter out virtual locations by default (includeVirtual=false)
- **Tests**: Integration test with filters
- **Cursor Minimal Context:**
```
Files: Api/Controllers/AvailableStockController.cs (new)
Query: GetAvailableStockQuery.cs (exists), add includeVirtual parameter
Filter: WHERE location NOT IN ('SUPPLIER', 'PRODUCTION', 'SCRAP', 'SYSTEM') if includeVirtual=false
Pagination: Skip((page-1)*pageSize).Take(pageSize)
Response: PagedResult<AvailableStockDto>
```


**UI-2.2: Create Warehouses API Endpoint**
- Implement `GET /api/warehouses`
- Return list of warehouses (id, code, name)
- **Tests**: Integration test
- **Cursor Minimal Context:**
```
Files: Api/Controllers/WarehousesController.cs (new)
Query: GetWarehousesQuery.cs (new) in Application/Queries
Handler: Query Warehouse configuration (EF Core or hardcoded list for Phase 1)
Response: List<WarehouseDto> with id, code, name
Pattern: Simple query handler, no pagination needed
```

**UI-2.3: Create Available Stock DTOs**
- Create `AvailableStockDto`
- Create `WarehouseDto`
- Create `PagedResultDto<T>`
- **Tests**: None (DTOs)
- **Cursor Minimal Context:**
```
Files: Contracts/DTOs/AvailableStockDtos.cs (new)
AvailableStockDto: warehouseId, location, sku, physicalQty, reservedQty, availableQty, lastUpdated
WarehouseDto: id, code, name
PagedResultDto<T>: items (List<T>), totalCount, page, pageSize
Pattern: Simple record types
```

**UI-2.4: Create Available Stock Service**
- Create `AvailableStockService` to call API endpoints
- Implement error handling with ProblemDetails
- **Tests**: Unit test API calls with mocked HttpClient
- **Cursor Minimal Context:**
```
Files: WebUI/Services/AvailableStockService.cs (new)
Pattern: HttpClient wrapper with async methods
Methods: SearchAsync(warehouse, location, sku, includeVirtual, page, pageSize), GetWarehousesAsync()
Error: Catch HttpRequestException, parse ProblemDetails
DI: Register as scoped service in Program.cs
```

**UI-2.5: Create Search Filters Component**
- Create `AvailableStockFilters.razor`
- Warehouse dropdown, Location text input, SKU text input
- Virtual location toggle (default: hidden)
- Validate at least one filter provided
- **Tests**: Unit test validation logic
- **Cursor Minimal Context:**
```
Files: WebUI/Components/AvailableStockFilters.razor (new)
Pattern: Bootstrap 5 form with dropdowns and text inputs
Props: Warehouses (List<WarehouseDto>), OnSearch (EventCallback)
Validation: At least one of warehouse/location/sku must be non-empty
Toggle: Checkbox for includeVirtual (default: false)
```

**UI-2.6: Create Available Stock Data Table**
- Create `AvailableStockTable.razor`
- Columns: Warehouse, Location, SKU, Physical Qty, Reserved Qty, Available Qty, Last Updated
- Support pagination
- Support export to CSV
- Display stale data badge if lastUpdated > 5s
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/AvailableStockTable.razor (new)
Pattern: Bootstrap 5 table with DataTable component from UI-0.4
Props: Items (List<AvailableStockDto>), TotalCount, Page, PageSize
Columns: 7 columns as specified
Badge: StaleDataBadge component from UI-0.8 in Last Updated column
Export: CSV button calls DataTable export method
```

**UI-2.7: Create Available Stock Page**
- Create `AvailableStock.razor` page
- Wire up filters and data table
- Implement search on filter change
- **Tests**: E2E test search with filters
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/AvailableStock.razor (new)
Pattern: Blazor page with @page "/available-stock"
Components: AvailableStockFilters, AvailableStockTable, Pagination
Service: Inject AvailableStockService
Logic: OnSearch callback calls service.SearchAsync(), updates table data
State: searchResults, currentPage, pageSize
```

**UI-2.8: Implement Virtual Location Filtering**
- Filter out SUPPLIER, PRODUCTION, SCRAP, SYSTEM by default
- Allow toggle to show virtual locations
- **Tests**: E2E test virtual location toggle
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/AvailableStock.razor (update), Api/Controllers/AvailableStockController.cs (update)
Backend: Add includeVirtual query parameter to GetAvailableStockQuery
Backend: Filter WHERE location NOT IN (...) if includeVirtual=false
Frontend: Wire includeVirtual checkbox to API call
Test: Verify virtual locations hidden by default, shown when toggled
```


**UI-2.9: Implement Pagination**
- Wire up pagination component
- Update query on page change
- **Tests**: E2E test pagination
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/AvailableStock.razor (update)
Component: Pagination from UI-0.5
Logic: OnPageChanged callback updates currentPage state, calls SearchAsync() again
State: currentPage, pageSize, totalCount (from API response)
Display: Show "Page X of Y" and Previous/Next buttons
```

**UI-2.10: Implement Export to CSV**
- Export current page to CSV
- Include all columns
- **Tests**: Unit test CSV generation
- **Cursor Minimal Context:**
```
Files: WebUI/Components/AvailableStockTable.razor (update)
Pattern: Use DataTable CSV export from UI-0.4
Logic: Convert current page items to CSV string, trigger browser download
Columns: All 7 columns (Warehouse, Location, SKU, Physical, Reserved, Available, LastUpdated)
Download: Blazor JS interop to create blob and download
```

**UI-2.11: Implement Error Handling**
- Display error message on 400 Bad Request (invalid filters)
- Display retry button on 500 Internal Server Error
- Display "No stock found" on empty result
- **Tests**: E2E test error states
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/AvailableStock.razor (update)
Component: ErrorBanner from UI-0.7
Logic: Catch exceptions in SearchAsync(), parse ProblemDetails, set errorMessage
Display: Show ErrorBanner if errorMessage != null
Empty: Show "No stock found" message if items.Count == 0
Retry: OnRetry callback calls SearchAsync() again
```

**UI-2.12: Implement Stale Data Indicator**
- Display yellow badge if lastUpdated > 5s
- Display red badge if lastUpdated > 30s
- **Tests**: Unit test badge logic
- **Cursor Minimal Context:**
```
Files: WebUI/Components/AvailableStockTable.razor (update)
Component: StaleDataBadge from UI-0.8
Logic: For each row, pass lastUpdated to StaleDataBadge component
Display: Badge appears in Last Updated column next to timestamp
Colors: Yellow (warning) if lag 5-30s, Red (danger) if lag > 30s
```

---

### Package UI-3: Reservations List Page (14 tasks)

**Duration:** 6 days  
**Dependencies:** UI-0, UI-Res-Index (BLOCKER)

**Note:** UI-3 is BLOCKED until UI-Res-Index is complete. The reservation list page requires the ReservationSummaryProjection for scalability.

#### Tasks

**UI-3.1: Create Reservations List API Endpoint**
- **Dependencies:** UI-Res-Idx.3
- **Acceptance Criteria:**
  - [ ] Implement GET /api/reservations controller
  - [ ] Dispatch SearchReservationsQuery via MediatR (created in UI-Res-Index)
  - [ ] Support filter: status (All, ALLOCATED, PICKING)
  - [ ] Support pagination: page, pageSize
  - [ ] Integration test: endpoint with filters + pagination
- **Cursor Minimal Context:**
```
Files: Api/Controllers/ReservationsController.cs
Query: SearchReservationsQuery.cs (from UI-Res-Index), ReservationSummaryView.cs
Route: [HttpGet] /api/reservations
Params: [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50
```

**UI-3.2: Create Start Picking API Endpoint**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Implement `POST /api/reservations/{id}/start-picking`
  - [ ] Call `StartPickingCommand` via MediatR
  - [ ] Return 200 OK on success
  - [ ] Return 409 Conflict on HARD lock conflict (HARD_LOCK_CONFLICT code)
  - [ ] Return 422 Unprocessable Entity on insufficient balance (INSUFFICIENT_BALANCE code)
  - [ ] Integration test: happy path and error cases
- **Cursor Minimal Context:**
```
Files: Api/Controllers/ReservationsController.cs
Command: StartPickingCommand.cs (exists), StartPickingCommandHandler.cs
Route: [HttpPost] /api/reservations/{id}/start-picking
Error mapping: Use ResultToProblemDetailsMiddleware from UI-0.2a
```


**UI-3.3: Create Pick API Endpoint**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Implement `POST /api/reservations/{id}/pick`
  - [ ] Call `PickStockCommand` via MediatR
  - [ ] Return 200 OK on success
  - [ ] Return 404 Not Found if HU not found
  - [ ] Return 422 Unprocessable Entity if quantity exceeds allocated
  - [ ] Integration test: happy path and error cases
- **Cursor Minimal Context:**
```
Files: Api/Controllers/ReservationsController.cs
Command: PickStockCommand.cs (exists), PickStockCommandHandler.cs
Route: [HttpPost] /api/reservations/{id}/pick
Request: { reservationId, huId, sku, quantity }
```

**UI-3.4: Create Reservations DTOs**
- Create `ReservationDto`
- Create `ReservationLineDto`
- Create `AllocatedHUDto`
- Create `StartPickingRequestDto`
- Create `PickRequestDto`
- **Tests**: None (DTOs)
- **Cursor Minimal Context:**
```
Files: Contracts/DTOs/ReservationDtos.cs (new)
ReservationDto: reservationId, purpose, priority, status, lockType, createdAt, lines
ReservationLineDto: sku, requestedQty, allocatedHUs (list)
AllocatedHUDto: huId, lpn, qty
StartPickingRequestDto: reservationId
PickRequestDto: reservationId, huId, sku, quantity
```

**UI-3.5: Create Reservations Service**
- Create `ReservationsService` to call API endpoints
- Implement error handling with ProblemDetails
- **Tests**: Unit test API calls with mocked HttpClient
- **Cursor Minimal Context:**
```
Files: WebUI/Services/ReservationsService.cs (new)
Pattern: HttpClient wrapper with async methods
Methods: SearchAsync(status, page, pageSize), GetDetailAsync(reservationId), StartPickingAsync(reservationId), PickAsync(reservationId, huId, sku, quantity)
Error: Catch HttpRequestException, parse ProblemDetails
DI: Register as scoped service in Program.cs
```

**UI-3.6: Create Status Filter Component**
- Create `ReservationStatusFilter.razor`
- Dropdown: All, ALLOCATED, PICKING
- Emit filter change event
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/ReservationStatusFilter.razor (new)
Pattern: Bootstrap 5 dropdown/select
Options: "All" (null), "ALLOCATED", "PICKING"
Event: EventCallback<string?> OnStatusChanged
Default: "All" selected
```

**UI-3.7: Create Reservations Data Table**
- Create `ReservationsTable.razor`
- Columns: Reservation ID, Purpose, Priority, Status, Lock Type, Created At, Actions
- Actions: "Start Picking" button (if status=ALLOCATED), "Pick" button (if status=PICKING)
- Support pagination
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/ReservationsTable.razor (new)
Pattern: Bootstrap 5 table with DataTable component from UI-0.4
Props: Items (List<ReservationDto>), OnStartPicking, OnPick, OnRowClick
Columns: 7 columns as specified
Actions: Conditional buttons based on status, emit events on click
Pagination: Use Pagination component from UI-0.5
```

**UI-3.8: Create Reservation Details Modal**
- Create `ReservationDetailsModal.razor`
- Display lines (SKU, requested qty, allocated HUs)
- Open on row click
- **Tests**: None (UI component)
- **Cursor Minimal Context:**
```
Files: WebUI/Components/ReservationDetailsModal.razor (new)
Pattern: Bootstrap 5 modal dialog
Props: ReservationDto (full detail with lines), IsOpen, OnClose
Display: Table with lines, each line shows SKU, requestedQty, allocatedHUs (nested list)
Trigger: OnRowClick event from ReservationsTable
```


**UI-3.9: Create Pick Modal**
- Create `PickModal.razor`
- HU selection dropdown, Quantity input
- Validate quantity > 0 and <= allocated
- Call POST /api/reservations/{id}/pick
- **Tests**: Unit test validation logic
- **Cursor Minimal Context:**
```
Files: WebUI/Components/PickModal.razor (new)
Pattern: Bootstrap 5 modal with form
Props: ReservationLineDto (with allocatedHUs), IsOpen, OnPick, OnClose
Inputs: HU dropdown (from allocatedHUs), Quantity number input
Validation: quantity > 0 AND quantity <= selected HU allocated qty
Submit: OnPick callback with huId, sku, quantity
```

**UI-3.10: Create Reservations Page**
- Create `Reservations.razor` page
- Wire up status filter and data table
- Implement search on filter change
- **Tests**: E2E test reservations list
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Reservations.razor (new)
Pattern: Blazor page with @page "/reservations"
Components: ReservationStatusFilter, ReservationsTable, Pagination, ReservationDetailsModal, PickModal
Service: Inject ReservationsService
Logic: OnStatusChanged calls SearchAsync(), OnRowClick opens details modal
State: searchResults, currentPage, pageSize, selectedReservation
```

**UI-3.11: Implement Start Picking Action**
- Wire up "Start Picking" button
- Call POST /api/reservations/{id}/start-picking
- Display success message on 200 OK
- Display error message on 409 Conflict (HARD lock conflict)
- Display error message on 422 Unprocessable Entity (insufficient balance)
- **Tests**: E2E test start picking happy path and error cases
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Reservations.razor (update)
Action: OnStartPicking callback calls service.StartPickingAsync(reservationId)
Success: Show toast/alert "Picking started successfully", refresh table
Error 409: Show "HARD lock conflict. Another reservation is picking this stock."
Error 422: Show "Insufficient balance. Cannot start picking."
Component: Use ErrorBanner from UI-0.7 for errors
```

**UI-3.12: Implement Pick Action**
- Wire up "Pick" button
- Open pick modal
- Call POST /api/reservations/{id}/pick
- Display success message on 200 OK
- Display error message on 404 Not Found (HU not found)
- Display error message on 422 Unprocessable Entity (quantity exceeds allocated)
- **Tests**: E2E test pick happy path and error cases
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Reservations.razor (update)
Action: OnPick button opens PickModal, OnPick callback calls service.PickAsync(reservationId, huId, sku, quantity)
Success: Show toast "Pick completed successfully", close modal, refresh table
Error 404: Show "Handling unit not found"
Error 422: Show "Quantity exceeds allocated"
Component: Use ErrorBanner in modal for errors
```

**UI-3.13: Implement Pagination**
- Wire up pagination component
- Update query on page change
- **Tests**: E2E test pagination
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Reservations.razor (update)
Component: Pagination from UI-0.5
Logic: OnPageChanged callback updates currentPage state, calls SearchAsync() again
State: currentPage, pageSize, totalCount (from API response)
Display: Show "Page X of Y" and Previous/Next buttons
```

**UI-3.14: Implement Error Handling**
- Display error message on 400 Bad Request (invalid filters)
- Display retry button on 500 Internal Server Error
- Display "No reservations found" on empty result
- **Tests**: E2E test error states
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Reservations.razor (update)
Component: ErrorBanner from UI-0.7
Logic: Catch exceptions in SearchAsync(), parse ProblemDetails, set errorMessage
Display: Show ErrorBanner if errorMessage != null
Empty: Show "No reservations found" message if items.Count == 0
Retry: OnRetry callback calls SearchAsync() again
```

---

### Package UI-4: Projections Admin Page (11 tasks)

**Duration:** 5 days  
**Dependencies:** UI-0


#### Tasks

**UI-4.1: Update Rebuild Projection API Endpoint (Synchronous)**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Update `POST /api/projections/rebuild` to return synchronous result
  - [ ] Call `RebuildProjectionCommand` via MediatR (synchronous execution)
  - [ ] Return 200 OK with ProjectionRebuildReport (checksumMatch, rowCounts, duration) immediately
  - [ ] Return 409 Conflict if rebuild already running
  - [ ] Return 400 Bad Request if invalid projection name (INVALID_PROJECTION_NAME code)
  - [ ] Integration test: synchronous rebuild completes
- **Cursor Minimal Context:**
```
Files: Api/Controllers/ProjectionsController.cs
Command: RebuildProjectionCommand.cs (exists), returns ProjectionRebuildReport
Route: [HttpPost] /api/projections/rebuild
Response: Sync - blocks until rebuild complete, no jobId
Error: Use INVALID_PROJECTION_NAME from UI-0.2b
```

**UI-4.2: Create Verify Projection API Endpoint**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Implement `POST /api/projections/verify`
  - [ ] Call projection rebuild service to compute checksums
  - [ ] Return checksum match result
  - [ ] Return 404 Not Found if shadow table not found
  - [ ] Integration test: happy path and not found
- **Cursor Minimal Context:**
```
Files: Api/Controllers/ProjectionsController.cs
Service: IProjectionRebuildService.VerifyProjection(projectionName)
Route: [HttpPost] /api/projections/verify
Response: { checksumMatch, productionChecksum, shadowChecksum, rowCounts }
```

**UI-4.3: Create Projections DTOs**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `RebuildProjectionRequestDto`
  - [ ] Create `RebuildProjectionResponseDto`
  - [ ] Create `VerifyProjectionRequestDto`
  - [ ] Create `VerifyProjectionResponseDto`
- **Cursor Minimal Context:**
```
Files: Contracts/DTOs/ProjectionDtos.cs
Pattern: Simple DTOs matching API request/response shapes
RebuildResponse: projectionName, eventsProcessed, checksumMatch, swapped, duration
VerifyResponse: checksumMatch, productionChecksum, shadowChecksum, rowCounts
```
- **Tests**: None (DTOs)

**UI-4.4: Create Projections Service**
- **Dependencies:** UI-4.3
- **Acceptance Criteria:**
  - [ ] Create `ProjectionsService` to call API endpoints
  - [ ] Implement error handling with ProblemDetails
- **Cursor Minimal Context:**
```
Files: WebUI/Services/ProjectionsService.cs
Methods: RebuildAsync(projectionName), VerifyAsync(projectionName)
Pattern: HttpClient wrapper with error handling
Error: Parse ProblemDetails from response
```
- **Tests**: Unit test API calls with mocked HttpClient

**UI-4.5: Create Projection Selection Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `ProjectionSelection.razor`
  - [ ] Radio buttons: LocationBalance, AvailableStock
  - [ ] Emit selection change event
- **Cursor Minimal Context:**
```
Files: WebUI/Components/ProjectionSelection.razor
Pattern: Bootstrap 5 radio button group
Event: EventCallback<string> OnSelectionChanged
Options: "LocationBalance", "AvailableStock"
```
- **Tests**: None (UI component)

**UI-4.6: Create Status Card Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Create `RebuildStatusCard.razor`
  - [ ] Display status: Idle, Running, Completed, Failed
  - [ ] Display spinner during rebuild
- **Cursor Minimal Context:**
```
Files: WebUI/Components/RebuildStatusCard.razor
Pattern: Bootstrap 5 card with status badge
States: Idle (default), Running (spinner), Completed (success), Failed (danger)
Props: Status (enum), IsLoading (bool)
```
- **Tests**: None (UI component)

**UI-4.7: Create Rebuild Result Detail Component**
- **Dependencies:** None
- **Acceptance Criteria:**
  - [ ] Display: eventsProcessed, duration, productionChecksum, shadowChecksum, checksumMatch, swapped
  - [ ] Green card if checksumMatch=true + swapped=true
  - [ ] Red card if checksumMatch=false
- **Cursor Minimal Context:**
```
Files: WebUI/Components/RebuildResultDetail.razor
Pattern: Bootstrap 5 card with alert-success or alert-danger
Props: RebuildResultDto
Display: Table with key-value pairs for all result fields
```
- **Tests**: None (UI component)

**UI-4.8: Create Projections Page**
- **Dependencies:** UI-4.4, UI-4.5, UI-4.6, UI-4.7
- **Acceptance Criteria:**
  - [ ] Create `Projections.razor` page
  - [ ] Wire up projection selection, rebuild button, verify button
  - [ ] Implement rebuild with confirmation dialog
  - [ ] Implement verify
  - [ ] Display spinner during synchronous rebuild
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Projections.razor
Components: ProjectionSelection, RebuildStatusCard, RebuildResultDetail
Actions: RebuildAsync (shows spinner, awaits sync response), VerifyAsync
No polling: Synchronous rebuild returns full result immediately
```
- **Tests**: E2E test rebuild and verify

**UI-4.9: Implement Rebuild Action (Synchronous)**
- **Dependencies:** UI-4.8
- **Acceptance Criteria:**
  - [ ] Wire up "Rebuild" button
  - [ ] Show confirmation dialog
  - [ ] Call POST /api/projections/rebuild (synchronous, blocks until complete)
  - [ ] Display spinner during rebuild
  - [ ] Display result detail component immediately after response
  - [ ] No polling required (synchronous response)
  - [ ] E2E test: rebuild completes and displays result
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Projections.razor
Action: await ProjectionsService.RebuildAsync(projectionName) // blocks
Loading: Show spinner during await, hide on completion
Result: Display RebuildResultDetail with response DTO
No polling: Synchronous response contains full result
```

**UI-4.10: Implement Verify Action**
- **Dependencies:** UI-4.8
- **Acceptance Criteria:**
  - [ ] Wire up "Verify" button
  - [ ] Call POST /api/projections/verify
  - [ ] Display green success on checksum match
  - [ ] Display red alert on checksum mismatch
  - [ ] Display error message on 404 Not Found (shadow table not found)
  - [ ] E2E test: verify happy path and error cases
- **Cursor Minimal Context:**
```
Files: WebUI/Pages/Projections.razor
Action: await ProjectionsService.VerifyAsync(projectionName)
Display: Green alert if checksumMatch=true, red alert if false
Error: Show error banner on 404 (shadow table not found)
```

---

## Traceability Matrix


### Pages to Packages

| Page | Packages | Total Tasks |
|------|----------|-------------|
| Dashboard | UI-0, UI-1 | 20 |
| Available Stock Search | UI-0, UI-2 | 22 |
| Reservations List | UI-0, UI-Res-Index, UI-3 | 26 |
| Projections Admin | UI-0, UI-4 | 21 |
| **Total** | **6 packages** | **61 tasks** |

### Packages to API Endpoints

| Package | API Endpoints | Backend Module |
|---------|---------------|----------------|
| UI-0 | None (shared components + error model) | SharedKernel, Api/Middleware |
| UI-Res-Index | GET /api/reservations | LKvitai.MES.Api (new controller), Projections (new projection) |
| UI-1 | GET /api/dashboard/health<br>GET /api/dashboard/stock-summary<br>GET /api/dashboard/reservation-summary<br>GET /api/dashboard/projection-health<br>GET /api/dashboard/recent-activity | LKvitai.MES.Api (new controllers) |
| UI-2 | GET /api/available-stock<br>GET /api/warehouses | LKvitai.MES.Api (new controllers) |
| UI-3 | POST /api/reservations/{id}/start-picking<br>POST /api/reservations/{id}/pick | LKvitai.MES.Api (new controller routes) |
| UI-4 | POST /api/projections/rebuild (sync)<br>POST /api/projections/verify | LKvitai.MES.Api (updated controller) |

### API Endpoints to Backend Commands/Queries

| API Endpoint | Backend Command/Query | Module |
|--------------|----------------------|--------|
### API Endpoints to Backend Commands/Queries

| API Endpoint | Backend Command/Query | Module |
|--------------|----------------------|--------|
| GET /api/dashboard/health | Query projection lag metrics | LKvitai.MES.Application.Queries |
| GET /api/dashboard/stock-summary | Query LocationBalance aggregates | LKvitai.MES.Application.Queries |
| GET /api/dashboard/reservation-summary | Query Reservation counts | LKvitai.MES.Application.Queries |
| GET /api/dashboard/projection-health | Query projection metadata | LKvitai.MES.Application.Queries |
| GET /api/dashboard/recent-activity | Query StockMovement events | LKvitai.MES.Application.Queries |
| GET /api/available-stock | Query AvailableStock projection | LKvitai.MES.Application.Queries (exists) |
| GET /api/warehouses | Query Warehouse configuration | LKvitai.MES.Application.Queries |
| GET /api/reservations | SearchReservationsQuery | LKvitai.MES.Application.Queries (UI-Res-Index) |
| POST /api/reservations/{id}/start-picking | StartPickingCommand | LKvitai.MES.Application.Commands (exists) |
| POST /api/reservations/{id}/pick | PickStockCommand | LKvitai.MES.Application.Commands (exists) |
| POST /api/projections/rebuild | RebuildProjectionCommand (sync) | LKvitai.MES.Application.Commands (exists) |
| POST /api/projections/verify | Verify projection checksums | LKvitai.MES.Application.Projections (exists) |


### Backend Readiness Assessment

| Backend Feature | Status | Notes |
|----------------|--------|-------|
| LocationBalance projection | ✅ Done | Package C |
| AvailableStock projection | ✅ Done | Package D |
| ActiveHardLocks projection | ✅ Done | Package D |
| HandlingUnit projection | ✅ Done | Package E |
| StartPickingCommand | ✅ Done | Package D |
| PickStockCommand | ✅ Done | Package D |
| RebuildProjectionCommand | ✅ Done | Package C (needs sync update) |
| Projection rebuild service | ✅ Done | Package C |
| **ReservationSummaryProjection** | ❌ **BLOCKER** | **UI-Res-Index package (new)** |
| **DomainErrorCodes (4 missing)** | ❌ **BLOCKER** | **UI-0.2b (new)** |
| **ProblemDetails mapper** | ❌ **BLOCKER** | **UI-0.2a (new)** |
| Reservation queries | ❌ BLOCKED | Requires ReservationSummaryProjection (UI-Res-Index) |
| Dashboard queries | ❌ Not started | Need query handlers (UI-1) |
| Warehouse configuration | ❌ Not started | Need state-based aggregate (UI-2) |

**Backend Gaps (to be filled in UI packages):**
1. **CRITICAL**: ProblemDetails mapper middleware (UI-0.2a) - blocks all API work
2. **CRITICAL**: Missing DomainErrorCodes (UI-0.2b) - blocks error handling
3. **CRITICAL**: ReservationSummaryProjection (UI-Res-Index) - blocks UI-3
4. Dashboard query handlers (UI-1.1)
5. Reservation query handlers (UI-Res-Idx.3, UI-Res-Idx.4)
6. Warehouse configuration query handlers (UI-2.2)
7. Rebuild synchronous update (UI-4.1)

---

## Implementation Notes

### Technology Stack

**Frontend:**
- Blazor Server (ASP.NET Core 8)
- Bootstrap 5 for styling
- (Phase 2) Chart.js — deferred, dashboard uses count cards only in Phase 1
- (Phase 2) SignalR — deferred, polling is sufficient for Phase 1

**Backend:**
- ASP.NET Core 8 Web API
- MediatR for CQRS
- Marten for event store and projections
- EF Core for state-based aggregates
- MassTransit for saga orchestration

### Error Handling Strategy

All API endpoints return ProblemDetails on error:

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

UI maps error codes to user-friendly messages:


- **400 Bad Request**: "Invalid input. Please check your filters."
- **404 Not Found**: "Resource not found."
- **409 Conflict**: "Operation conflict. Another operation is in progress."
- **422 Unprocessable Entity**: "Operation cannot be completed. Check business rules."
- **500 Internal Server Error**: "Server error. Please try again later."
- **503 Service Unavailable**: "Backend unavailable. Please check system status."

### Virtual Location Filtering

Virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM) are filtered out by default in the Available Stock search page. Users can toggle to show virtual locations if needed.

**Implementation:**
- Backend: Add `includeVirtual` query parameter (default: false)
- Backend: Filter WHERE location NOT IN ('SUPPLIER', 'PRODUCTION', 'SCRAP', 'SYSTEM') if includeVirtual=false
- Frontend: Add checkbox "Show virtual locations" (default: unchecked)

### Projection Lag Indicator

Projection lag is computed as: `DateTime.UtcNow - projection.LastUpdated`

**Thresholds:**
- < 5s: No indicator (data is fresh)
- 5s - 30s: Yellow badge "Data may be stale"
- > 30s: Red badge "Data is stale, consider rebuild"

**Implementation:**
- Backend: Include `lastUpdated` timestamp in all projection DTOs
- Frontend: Compute lag on client side
- Frontend: Display badge based on thresholds

### Authentication Placeholder

Phase 1 does not include authentication. Add placeholder structure for Phase 2:

**Backend:**
- Add `[Authorize]` attribute to controllers (commented out)
- Add TODO comments: "// TODO: Phase 2 - Enable authentication"

**Frontend:**
- Add placeholder login page (not functional)
- Add TODO comments: "// TODO: Phase 2 - Implement authentication"


### Testing Strategy

**Unit Tests:**
- Test validation logic (filters, quantity checks)
- Test error code mapping
- Test pagination logic
- Test CSV export logic

**Integration Tests:**
- Test API endpoints with real database
- Test query handlers with Marten projections
- Test command handlers with MediatR

**E2E Tests:**
- Test complete user workflows (search, start picking, pick, rebuild)
- Test error states (invalid input, backend unavailable)
- Test pagination and filtering

**Test Coverage Target:** 80% for backend, 60% for frontend

---

## Execution Plan

### Wave 1: Foundation (Week 1)
- Package UI-0: Foundation & Shared Components (3 days)
- Setup Blazor Server project, shared components, error handling

### Wave 2: Dashboard (Week 2)
- Package UI-1: Dashboard Page (4 days)
- Implement dashboard API endpoints, DTOs, services, components

### Wave 3: Available Stock (Week 3)
- Package UI-2: Available Stock Search Page (5 days)
- Implement available stock API endpoints, DTOs, services, components

### Wave 4: Reservations (Week 4)
- Package UI-3: Reservations List Page (6 days)
- Implement reservations API endpoints, DTOs, services, components

### Wave 5: Projections (Week 5)
- Package UI-4: Projections Admin Page (5 days)
- Implement projections API endpoints, DTOs, services, components

**Total Duration:** 5 weeks (23 days)

---

## Summary

This UI task universe provides a complete implementation plan for UI Slice 0 (Admin-style MVP) with:

- **4 pages**: Dashboard, Available Stock Search, Reservations List, Projections Admin
- **6 packages**: UI-0 (Foundation + Error Model), UI-Res-Index (Reservation Projection - BLOCKER), UI-1 (Dashboard), UI-2 (Available Stock), UI-3 (Reservations), UI-4 (Projections)
- **61 tasks**: Small, implementable tasks with Cursor Minimal Context
- **12 API endpoints**: Mapped to backend commands/queries
- **Clear traceability**: Pages → Packages → Endpoints → Backend

**Technology Stack (Governance Approved):**
- ✅ Blazor Server + Bootstrap 5
- ❌ NO SignalR (polling sufficient for Phase 1, deferred to Phase 2)
- ❌ NO Chart.js (counts only, no charts in Phase 1, deferred to Phase 2)

**Error Model (Governance Approved):**
- ✅ ProblemDetails mapper middleware (UI-0.2a)
- ✅ 10 standardized DomainErrorCodes with HTTP status mapping
- ✅ 4 new codes added: INSUFFICIENT_BALANCE, RESERVATION_NOT_ALLOCATED, HARD_LOCK_CONFLICT, INVALID_PROJECTION_NAME (UI-0.2b)

**Scalability (Governance Approved):**
- ✅ ReservationSummaryProjection added (UI-Res-Index package)
- ✅ Solves O(N×M) reservation list query problem
- ✅ Marked as BLOCKER for UI-3

**Projections Admin (Governance Approved):**
- ✅ Synchronous rebuild approach for Phase 1
- ✅ No jobId/status polling (simpler implementation)
- ✅ No progress bar, no log streaming (spinner + result only)

**Backend Readiness:**
- ✅ Core projections done (LocationBalance, AvailableStock, ActiveHardLocks, HandlingUnit)
- ✅ Core commands done (StartPicking, PickStock, RebuildProjection)
- ❌ **3 BLOCKERS**: Error model (UI-0.2a/b), Reservation projection (UI-Res-Index)
- ❌ 7 backend gaps (all added as explicit tasks)

**Next Steps:**
1. **CRITICAL**: Implement UI-0.2a + UI-0.2b (Error model) - blocks all API work
2. **CRITICAL**: Implement UI-Res-Index (Reservation projection) - blocks UI-3
3. Start UI-0 (Foundation + shared components)
4. Implement packages sequentially: UI-1 → UI-2 → UI-3 (after UI-Res-Index) → UI-4
5. Fill backend query gaps as each package progresses

**Execution Order:**
- Week 1: UI-0 (foundation + error model)
- Week 2: UI-Res-Index (reservation read model) + UI-1 (dashboard) — parallel
- Week 3: UI-2 (available stock)
- Week 4: UI-3 (reservations, unblocked by UI-Res-Index)
- Week 5: UI-4 (projections admin, sync)

---

**End of UI Task Universe Document**