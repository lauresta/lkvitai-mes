# Tasks: Warehouse Core Phase 1 - UI Slice 0

**Project:** LKvitai.MES Warehouse Management System  
**Version:** 1.0  
**Date:** February 8, 2026  
**Status:** Implementation Tasks

---

## Package UI-0: Foundation & Error Model

**Purpose:** Establish Blazor Server project structure, shared components, ProblemDetails error handling, and MainLayout.

**Dependencies:** None (foundation package)

**Estimated Effort:** 3-4 days

---

### Task UI-0.1: Create Blazor Server Project Structure

**Description:** Initialize LKvitai.MES.WebUI project with .NET 8, Bootstrap 5, and folder structure per spec-ui.md.

**Dependencies:** None

**Acceptance Criteria:**
- [ ] Project created with `dotnet new blazorserver -n LKvitai.MES.WebUI`
- [ ] Bootstrap 5.3+ added via CDN in `_Host.cshtml` or `App.razor`
- [ ] Bootstrap Icons 1.11+ added via CDN
- [ ] Folder structure created: `Pages/`, `Components/`, `Services/`, `Models/`, `Infrastructure/`, `Shared/`
- [ ] `_Imports.razor` configured with global usings
- [ ] `appsettings.json` includes `ApiBaseUrl`, `StalenessThresholds`, `RefreshIntervals`
- [ ] Project builds without errors

**Cursor Minimal Context:**
```
Files: Program.cs, _Imports.razor, appsettings.json
Pattern: Blazor Server + Bootstrap 5 + folder structure
Constraint: .NET 8, no SignalR/Chart.js in Phase 1
```

---

### Task UI-0.2a: Implement ProblemDetails Parser

**Description:** Create `ProblemDetailsParser` to parse RFC 7807 responses from API into `ProblemDetailsModel`.

**Dependencies:** UI-0.1

**Acceptance Criteria:**
- [ ] `ProblemDetailsModel.cs` created with properties: `Type`, `Title`, `Status`, `TraceId`, `Errors` (dict), `Detail`
- [ ] `ProblemDetailsParser.cs` created with `ParseAsync(HttpResponseMessage)` method
- [ ] Parser handles `application/problem+json` content type
- [ ] Parser extracts traceId from response body or headers
- [ ] Parser returns null if response is not ProblemDetails format
- [ ] Unit tests verify parsing of valid ProblemDetails JSON
- [ ] Unit tests verify null return for non-ProblemDetails responses

**Cursor Minimal Context:**
```
Files: Infrastructure/ProblemDetailsParser.cs, Infrastructure/ProblemDetailsModel.cs
Pattern: RFC 7807 parser, async JSON deserialization
Constraint: Must handle missing traceId gracefully
```

**BLOCKER:** Requires backend ProblemDetails middleware (API task)

---

### Task UI-0.2b: Implement ErrorCodeMessages Mapper

**Description:** Create static dictionary mapping DomainErrorCode to user-friendly messages per requirements-ui.md Section 5.

**Dependencies:** UI-0.1

**Acceptance Criteria:**
- [ ] `ErrorCodeMessages.cs` created with static `Dictionary<string, string>`
- [ ] All 12 domain error codes from requirements-ui.md mapped: 7 domain-specific codes (INSUFFICIENT_BALANCE, RESERVATION_NOT_ALLOCATED, HARD_LOCK_CONFLICT, INVALID_PROJECTION_NAME, IDEMPOTENCY_IN_PROGRESS, IDEMPOTENCY_ALREADY_PROCESSED, CONCURRENCY_CONFLICT) + 5 generic codes from SharedKernel.DomainErrorCodes (VALIDATION_ERROR, NOT_FOUND, UNAUTHORIZED, FORBIDDEN, INTERNAL_ERROR)
- [ ] Generic fallback message for unknown codes: "An unexpected error occurred. Please try again."
- [ ] HTTP status fallbacks: 500 → "Server error. Please try again later.", 503 → "Backend unavailable. Please check system status."
- [ ] Unit tests verify all required error codes present

**Cursor Minimal Context:**
```
Files: Infrastructure/ErrorCodeMessages.cs
Pattern: Static dictionary, DomainErrorCode → user message
Constraint: Must match requirements-ui.md Section 5 table exactly (7 domain + 5 generic = 12 codes)
```

**BLOCKER:** Requires backend DomainErrorCode enum definition (API task)

---

### Task UI-0.3: Implement ApiException

**Description:** Create custom exception wrapping ProblemDetailsModel for typed error handling in API clients.

**Dependencies:** UI-0.2a, UI-0.2b

**Acceptance Criteria:**
- [ ] `ApiException.cs` created inheriting from `Exception`
- [ ] Properties: `StatusCode`, `ErrorCode`, `TraceId`, `UserMessage`, `ProblemDetails`
- [ ] Constructor accepts `ProblemDetailsModel` and looks up `UserMessage` from `ErrorCodeMessages`
- [ ] Constructor handles null/missing error code with fallback message
- [ ] `ToString()` includes traceId for logging
- [ ] Unit tests verify message lookup and fallback behavior

**Cursor Minimal Context:**
```
Files: Infrastructure/ApiException.cs, Infrastructure/ErrorCodeMessages.cs, Infrastructure/ProblemDetailsModel.cs
Pattern: Custom exception with ProblemDetails wrapper
Constraint: Must surface traceId for support escalation
```

---

### Task UI-0.4: Create Reusable Components (DataTable, Pagination, LoadingSpinner)

**Description:** Implement generic DataTable, Pagination, and LoadingSpinner components per spec-ui.md Section 3.

**Dependencies:** UI-0.1

**Acceptance Criteria:**
- [ ] `DataTable.razor` created with `@typeparam TItem`, `Items`, `TotalCount`, `Columns` (RenderFragment), `EmptyMessage` props
- [ ] `DataTable.razor` emits `OnRowClick(TItem)` event
- [ ] `Pagination.razor` created with `CurrentPage`, `TotalPages`, `PageSize`, `PageSizeOptions` props
- [ ] `Pagination.razor` emits `OnPageChanged(int)`, `OnPageSizeChanged(int)` events
- [ ] `Pagination.razor` displays "Page X of Y" and page size selector (25/50/100)
- [ ] `LoadingSpinner.razor` created with `IsLoading` bool prop, Bootstrap `spinner-border`
- [ ] All components use Bootstrap 5 classes
- [ ] Manual test: DataTable renders with sample data, pagination controls work

**Cursor Minimal Context:**
```
Files: Components/DataTable.razor, Components/Pagination.razor, Components/LoadingSpinner.razor
Pattern: Generic Blazor components, RenderFragment columns, event callbacks
Constraint: Bootstrap 5 only, no custom CSS beyond layout
```

---

### Task UI-0.5: Create Reusable Components (ErrorBanner, StaleBadge, ConfirmDialog, ToastContainer)

**Description:** Implement ErrorBanner, StaleBadge, ConfirmDialog, and ToastContainer components per spec-ui.md Section 3.

**Dependencies:** UI-0.1, UI-0.3

**Acceptance Criteria:**
- [ ] `ErrorBanner.razor` created with `Message`, `TraceId` props, `OnRetry`, `OnDismiss` events
- [ ] `ErrorBanner.razor` displays "Error ID: {traceId}" below message in alert-danger
- [ ] `StaleBadge.razor` created with `LastUpdated` (DateTime) prop
- [ ] `StaleBadge.razor` computes lag client-side: no badge <5s, yellow 5-30s, red >30s
- [ ] `ConfirmDialog.razor` created with `Title`, `Message`, `ConfirmText`, `IsVisible` props, `OnConfirm`, `OnCancel` events
- [ ] `ConfirmDialog.razor` uses Bootstrap modal
- [ ] `ToastContainer.razor` created, renders stacked toasts with auto-dismiss (5s)
- [ ] `ToastService.cs` created with in-memory event bus for toast notifications (success/error/warning types)
- [ ] Manual test: ErrorBanner shows traceId, StaleBadge changes color, ConfirmDialog opens/closes, Toast auto-dismisses

**Cursor Minimal Context:**
```
Files: Components/ErrorBanner.razor, Components/StaleBadge.razor, Components/ConfirmDialog.razor, Components/ToastContainer.razor, Services/ToastService.cs
Pattern: Bootstrap alerts/modals, client-side time calculations, event-based toast service
Constraint: No SignalR, toasts are client-side only
```

---

### Task UI-0.6: Create MainLayout and NavMenu

**Description:** Implement MainLayout with Bootstrap 5 sidebar and NavMenu with 4 navigation links per spec-ui.md Section 2.

**Dependencies:** UI-0.1

**Acceptance Criteria:**
- [ ] `MainLayout.razor` created with Bootstrap sidebar (collapsed on narrow viewports) + top bar
- [ ] `MainLayout.razor.css` created for sidebar styling
- [ ] Top bar displays "LKvitai.MES Warehouse" left-aligned, placeholder "Login" button right-aligned (disabled)
- [ ] `NavMenu.razor` created with 4 links: Dashboard, Available Stock, Reservations, Projections
- [ ] NavMenu uses Bootstrap Icons: `bi-speedometer2`, `bi-box-seam`, `bi-clipboard-check`, `bi-database-gear`
- [ ] Active link highlighted via Bootstrap `active` class
- [ ] `/` route redirects to `/dashboard` in `App.razor` or `Program.cs`
- [ ] Manual test: Navigation works, active link highlights, sidebar collapses on mobile

**Cursor Minimal Context:**
```
Files: Shared/MainLayout.razor, Shared/MainLayout.razor.css, Shared/NavMenu.razor, App.razor
Pattern: Bootstrap 5 sidebar layout, NavLink active class, route redirect
Constraint: No auth enforcement, placeholder login button only
```

---

### Task UI-0.7: Configure HttpClient DI

**Description:** Register named HttpClient "WarehouseApi" in Program.cs with base address, timeout, and default headers.

**Dependencies:** UI-0.1

**Acceptance Criteria:**
- [ ] `Program.cs` registers `IHttpClientFactory` with named client "WarehouseApi"
- [ ] BaseAddress set from `appsettings.json` "ApiBaseUrl"
- [ ] Timeout set to 30 seconds (covers sync rebuild)
- [ ] DefaultRequestHeaders includes `Accept: application/json`
- [ ] `ToastService` registered as scoped service
- [ ] Manual test: HttpClient can be injected and makes successful request to API health endpoint

**Cursor Minimal Context:**
```
Files: Program.cs, appsettings.json
Pattern: IHttpClientFactory named client, DI registration
Constraint: 30s timeout for sync rebuild operations
```

---

## Package UI-Res-Index: Reservation Projection (BLOCKER for UI-3)

**Purpose:** Create ReservationSummaryView read model and projection to support Reservations list page queries.

**Dependencies:** UI-0 (foundation)

**Estimated Effort:** 2-3 days

**CRITICAL:** This package BLOCKS UI-3 (Reservations page). Must be completed before UI-3 tasks.

---

### Task UI-Res-Index.1: Create ReservationSummaryView Read Model

**Description:** Define Marten document for reservation list queries with indexed fields for filtering and pagination.

**Dependencies:** None (backend task)

**Acceptance Criteria:**
- [ ] `ReservationSummaryView.cs` created in `Contracts/ReadModels/ReservationSummaryView.cs`
- [ ] Properties: `ReservationId`, `Purpose`, `Priority`, `Status`, `LockType`, `CreatedAt`, `LineCount`, `LastUpdated`
- [ ] Marten index on `Status` field for fast filtering
- [ ] Marten index on `CreatedAt` field for sorting (Phase 2 prep)
- [ ] Document stored in `reservation_summary_view` table
- [ ] Unit test verifies document can be stored and queried

**Cursor Minimal Context:**
```
Files: Contracts/ReadModels/ReservationSummaryView.cs, Infrastructure/MartenConfiguration.cs
Pattern: Marten document with indexes, read model projection
Constraint: Status filter must be fast (<100ms for 10k reservations)
```

---

### Task UI-Res-Index.2: Implement ReservationSummaryProjection

**Description:** Create SingleStreamProjection to populate ReservationSummaryView from StockLedger events.

**Dependencies:** UI-Res-Index.1

**Acceptance Criteria:**
- [ ] `ReservationSummaryProjection.cs` created in `Projections/ReservationSummaryProjection.cs`
- [ ] Projection type: Marten `SingleStreamProjection<ReservationSummaryView>` with Async lifecycle
- [ ] Handles `ReservationCreatedEvent`: Create view with initial status
- [ ] Handles `StockAllocatedEvent`: Update status to ALLOCATED
- [ ] Handles `PickingStartedEvent`: Update status to PICKING
- [ ] Handles `ReservationConsumedEvent`: Delete view (or mark inactive)
- [ ] Handles `ReservationCancelledEvent`: Delete view (or mark cancelled)
- [ ] Handles `ReservationBumpedEvent`: Update LastUpdated timestamp
- [ ] Projection registered in Marten configuration as Async
- [ ] Integration test verifies projection updates on events
- [ ] Manual test: Create reservation → view created, Allocate → status updated, StartPicking → status updated

**Cursor Minimal Context:**
```
Files: Projections/ReservationSummaryProjection.cs, Infrastructure/MartenConfiguration.cs
Pattern: Marten SingleStreamProjection<T>, Async lifecycle, event handlers
Constraint: Must handle out-of-order events gracefully
```

---

### Task UI-Res-Index.3: Implement SearchReservationsQuery

**Description:** Create MediatR query handler to search ReservationSummaryView with status filter and pagination.

**Dependencies:** UI-Res-Index.2

**Acceptance Criteria:**
- [ ] `SearchReservationsQuery.cs` created with `Status` (optional), `Page`, `PageSize` params
- [ ] `SearchReservationsQueryHandler.cs` queries Marten `reservation_summary_view`
- [ ] Handler filters by status if provided (ALLOCATED, PICKING)
- [ ] Handler paginates results (default page=1, pageSize=50)
- [ ] Handler returns `PagedResult<ReservationSummaryView>` with TotalCount
- [ ] Unit test verifies status filtering works
- [ ] Unit test verifies pagination calculates TotalCount correctly
- [ ] Integration test queries actual Marten database

**Cursor Minimal Context:**
```
Files: Application/Queries/SearchReservationsQuery.cs, Application/Queries/SearchReservationsQueryHandler.cs
Pattern: MediatR query, Marten LINQ query, pagination
Constraint: Must support null status (return all ALLOCATED+PICKING)
```

---

### Task UI-Res-Index.4: Update SearchReservationsQuery to Include Full Details

**Description:** Enhance SearchReservationsQuery to return full reservation details including lines and allocated HUs in the list response.

**Dependencies:** UI-Res-Index.2

**Acceptance Criteria:**
- [ ] `SearchReservationsQuery.cs` updated to return full `ReservationDetailDto` items (not summary)
- [ ] `SearchReservationsQueryHandler.cs` queries ActiveReservations projection
- [ ] Handler joins with HandlingUnit projection to get LPN for each allocated HU
- [ ] Handler returns `PagedResult<ReservationDetailDto>` with Lines and AllocatedHUs
- [ ] Unit test verifies HU join works correctly
- [ ] Integration test fetches reservations with multiple lines and HUs

**Cursor Minimal Context:**
```
Files: Application/Queries/SearchReservationsQuery.cs, Application/Queries/SearchReservationsQueryHandler.cs
Pattern: MediatR query, Marten join, DTO mapping with full details
Constraint: Must include LPN from HandlingUnit projection in list response
```

---

## Package UI-1: Dashboard Page

**Purpose:** Implement Dashboard page with 5 summary cards and auto-refresh.

**Dependencies:** UI-0 (foundation)

**Estimated Effort:** 3-4 days

---

### Task UI-1.1: Create Dashboard DTOs

**Description:** Define DTOs for 5 dashboard API responses per spec-ui.md Section 6.

**Dependencies:** None

**Acceptance Criteria:**
- [ ] `HealthStatusDto.cs` created with `Status`, `ProjectionLag`, `LastCheck`
- [ ] `StockSummaryDto.cs` created with `TotalSKUs`, `TotalQuantity`, `TotalValue`
- [ ] `ReservationSummaryDto.cs` created with `Allocated`, `Picking`, `Consumed`
- [ ] `ProjectionHealthDto.cs` created with `LocationBalanceLag`, `AvailableStockLag`, `LastRebuildLB`, `LastRebuildAS`
- [ ] `RecentMovementDto.cs` created with `MovementId`, `SKU`, `Quantity`, `FromLocation`, `ToLocation`, `Timestamp`
- [ ] All DTOs in `Models/` folder
- [ ] Unit test verifies JSON serialization/deserialization

**Cursor Minimal Context:**
```
Files: Models/HealthStatusDto.cs, Models/StockSummaryDto.cs, Models/ReservationSummaryDto.cs, Models/ProjectionHealthDto.cs, Models/RecentMovementDto.cs
Pattern: Simple DTOs, JSON serialization
Constraint: Must match API contract exactly
```

---

### Task UI-1.2: Implement DashboardClient

**Description:** Create typed HttpClient wrapper for 5 dashboard API endpoints with error handling.

**Dependencies:** UI-0.3 (ApiException), UI-0.7 (HttpClient DI), UI-1.1

**Acceptance Criteria:**
- [ ] `DashboardClient.cs` created in `Services/` folder
- [ ] Method: `GetHealthAsync()` calls GET `/api/dashboard/health`
- [ ] Method: `GetStockSummaryAsync()` calls GET `/api/dashboard/stock-summary`
- [ ] Method: `GetReservationSummaryAsync()` calls GET `/api/dashboard/reservation-summary`
- [ ] Method: `GetProjectionHealthAsync()` calls GET `/api/dashboard/projection-health`
- [ ] Method: `GetRecentActivityAsync(limit)` calls GET `/api/dashboard/recent-activity?limit={limit}`
- [ ] All methods use `IHttpClientFactory.CreateClient("WarehouseApi")`
- [ ] All methods parse ProblemDetails on error and throw `ApiException`
- [ ] All methods include traceId in exception
- [ ] Client registered as scoped service in `Program.cs`
- [ ] Unit test verifies error parsing with mock HttpClient
- [ ] Integration test calls real API endpoints

**Cursor Minimal Context:**
```
Files: Services/DashboardClient.cs, Infrastructure/ApiException.cs, Infrastructure/ProblemDetailsParser.cs
Pattern: Typed HttpClient, async/await, ProblemDetails error handling
Constraint: Must surface traceId for all errors
```

---

### Task UI-1.3: Create Dashboard Card Components

**Description:** Implement 5 dashboard card components per spec-ui.md Section 3.

**Dependencies:** UI-0.4 (LoadingSpinner), UI-0.5 (StaleBadge), UI-1.1

**Acceptance Criteria:**
- [ ] `HealthStatusCard.razor` created with color-coded badge (green/yellow/red) based on ProjectionLag
- [ ] `StockSummaryCard.razor` created displaying TotalSKUs, TotalQuantity, TotalValue
- [ ] `ReservationSummaryCard.razor` created with counts by status (ALLOCATED/PICKING/CONSUMED) and color-coded badges
- [ ] `ProjectionHealthCard.razor` created with LB/AS lag + last rebuild timestamps + StaleBadge
- [ ] `RecentActivityFeed.razor` created with scrollable list (max-height: 300px) of last 10 movements
- [ ] All cards use Bootstrap card component
- [ ] All cards show LoadingSpinner during fetch
- [ ] Manual test: Cards render with sample data, colors match spec

**Cursor Minimal Context:**
```
Files: Components/Dashboard/*.razor, Models/*Dto.cs
Pattern: Blazor components, Bootstrap cards, conditional rendering
Constraint: Color thresholds: green <5s, yellow 5-30s, red >30s
```

---

### Task UI-1.4: Implement Dashboard Page with Auto-Refresh

**Description:** Create Dashboard.razor page with 5 cards and 30-second auto-refresh timer.

**Dependencies:** UI-1.2, UI-1.3

**Acceptance Criteria:**
- [ ] `Dashboard.razor` created at route `/dashboard`
- [ ] Page calls all 5 DashboardClient methods on load
- [ ] Page uses `System.Threading.Timer` with 30s interval for auto-refresh
- [ ] Timer disposed on page dispose (`IDisposable` implementation)
- [ ] Each card handles errors independently (partial failure support)
- [ ] Failed cards show inline ErrorBanner, successful cards display data
- [ ] Full error (503) shows page-level ErrorBanner with retry button
- [ ] Manual test: Dashboard loads, auto-refreshes every 30s, handles API errors gracefully

**Cursor Minimal Context:**
```
Files: Pages/Dashboard.razor, Services/DashboardClient.cs, Components/Dashboard/*.razor
Pattern: Blazor page, async data loading, timer-based refresh, IDisposable
Constraint: No SignalR, polling only, timer must be disposed
```

---

### Task UI-1.5: Implement Dashboard Backend Queries

**Description:** Create MediatR query handlers for 5 dashboard endpoints (health, stock summary, reservation summary, projection health, recent activity).

**Dependencies:** None (backend task)

**Acceptance Criteria:**
- [ ] `GetHealthStatusQuery.cs` + handler created, queries projection lag from Marten metadata
- [ ] `GetStockSummaryQuery.cs` + handler created, aggregates from LocationBalance projection
- [ ] `GetReservationSummaryQuery.cs` + handler created, counts from ReservationSummaryView by status
- [ ] `GetProjectionHealthQuery.cs` + handler created, queries Marten projection metadata for lag + last rebuild
- [ ] `GetRecentActivityQuery.cs` + handler created, queries last N StockMovement events from stream
- [ ] All handlers return appropriate DTOs
- [ ] Unit tests verify aggregation logic
- [ ] Integration tests query actual Marten database

**Cursor Minimal Context:**
```
Files: Application/Queries/Get*Query.cs, Application/Queries/Get*QueryHandler.cs
Pattern: MediatR queries, Marten aggregation, projection metadata queries
Constraint: Health status: green <5s, yellow 5-30s, red >30s lag
```

---

### Task UI-1.6: Create Dashboard API Controller

**Description:** Implement DashboardController with 5 GET endpoints per requirements-ui.md Section 4.1.

**Dependencies:** UI-1.5

**Acceptance Criteria:**
- [ ] `DashboardController.cs` created in `LKvitai.MES.Api/Controllers/`
- [ ] GET `/api/dashboard/health` endpoint
- [ ] GET `/api/dashboard/stock-summary` endpoint
- [ ] GET `/api/dashboard/reservation-summary` endpoint
- [ ] GET `/api/dashboard/projection-health` endpoint
- [ ] GET `/api/dashboard/recent-activity` endpoint with `limit` query param (default: 10)
- [ ] All endpoints use MediatR to dispatch queries
- [ ] All endpoints return ProblemDetails on errors (500, 503)
- [ ] Swagger documentation generated
- [ ] Integration test verifies all endpoints return valid data
- [ ] Manual test: Postman/curl returns expected JSON

**Cursor Minimal Context:**
```
Files: Api/Controllers/DashboardController.cs, Application/Queries/Get*Query.cs
Pattern: ASP.NET Core controller, MediatR dispatch, ProblemDetails errors
Constraint: Must handle backend unavailable (503) gracefully
```

---

## Package UI-2: Available Stock Search Page

**Purpose:** Implement Available Stock search page with filters, pagination, and virtual location toggle.

**Dependencies:** UI-0 (foundation)

**Estimated Effort:** 2-3 days

---

### Task UI-2.1: Create Available Stock DTOs

**Description:** Define DTOs for available stock search per spec-ui.md Section 6.

**Dependencies:** None

**Acceptance Criteria:**
- [ ] `AvailableStockItemDto.cs` created with `WarehouseId`, `Location`, `SKU`, `PhysicalQty`, `ReservedQty`, `AvailableQty`, `LastUpdated`
- [ ] `WarehouseDto.cs` created with `Id`, `Code`, `Name`
- [ ] `PagedResult<T>.cs` created with `Items`, `TotalCount`, `Page`, `PageSize`
- [ ] All DTOs in `Models/` folder
- [ ] Unit test verifies JSON serialization/deserialization

**Cursor Minimal Context:**
```
Files: Models/AvailableStockItemDto.cs, Models/WarehouseDto.cs, Models/PagedResult.cs
Pattern: Simple DTOs, generic paged result
Constraint: LastUpdated used for staleness calculation
```

---

### Task UI-2.2: Implement StockClient

**Description:** Create typed HttpClient wrapper for available stock search and warehouse list endpoints.

**Dependencies:** UI-0.3 (ApiException), UI-0.7 (HttpClient DI), UI-2.1

**Acceptance Criteria:**
- [ ] `StockClient.cs` created in `Services/` folder
- [ ] Method: `SearchAvailableStockAsync(warehouse, location, sku, includeVirtual, page, pageSize)` calls GET `/api/available-stock` with query params
- [ ] Method: `GetWarehousesAsync()` calls GET `/api/warehouses`
- [ ] `includeVirtual` parameter defaults to `false` (hides SUPPLIER, PRODUCTION, SCRAP, SYSTEM locations)
- [ ] Both methods use `IHttpClientFactory.CreateClient("WarehouseApi")`
- [ ] Both methods parse ProblemDetails on error and throw `ApiException`
- [ ] Client registered as scoped service in `Program.cs`
- [ ] Unit test verifies query string building with wildcards and includeVirtual parameter
- [ ] Integration test calls real API endpoints and verifies virtual location filtering

**Cursor Minimal Context:**
```
Files: Services/StockClient.cs, Infrastructure/ApiException.cs, Models/AvailableStockItemDto.cs, Models/PagedResult.cs
Pattern: Typed HttpClient, query string parameters, wildcard support, includeVirtual default false
Constraint: At least one filter (warehouse/location/sku) required, virtual locations hidden by default
```

---

### Task UI-2.3: Create Stock Filter and Table Components

**Description:** Implement StockFilters and StockTable components per spec-ui.md Section 3.

**Dependencies:** UI-0.4 (DataTable, Pagination), UI-0.5 (StaleBadge), UI-2.1

**Acceptance Criteria:**
- [ ] `StockFilters.razor` created with warehouse dropdown, location/SKU text inputs, "Show virtual locations" checkbox
- [ ] `StockFilters.razor` emits `OnSearch` event with filter values
- [ ] `StockFilters.razor` validates at least one filter provided before emitting event
- [ ] `StockTable.razor` created with 7 columns: Warehouse, Location, SKU, Physical Qty, Reserved Qty, Available Qty, Last Updated
- [ ] `StockTable.razor` uses `DataTable<AvailableStockItemDto>` component
- [ ] `StockTable.razor` displays `StaleBadge` in Last Updated column
- [ ] Manual test: Filters validate, table renders with sample data, StaleBadge shows correct color

**Cursor Minimal Context:**
```
Files: Components/Stock/StockFilters.razor, Components/Stock/StockTable.razor, Components/DataTable.razor, Components/StaleBadge.razor
Pattern: Blazor components, form validation, event callbacks
Constraint: At least one filter required, virtual locations hidden by default
```

---

### Task UI-2.4: Implement Available Stock Page

**Description:** Create AvailableStock.razor page with search filters, data table, and pagination.

**Dependencies:** UI-2.2, UI-2.3

**Acceptance Criteria:**
- [ ] `AvailableStock.razor` created at route `/available-stock`
- [ ] Page loads warehouse list on init for dropdown
- [ ] Page calls `StockClient.SearchAvailableStockAsync()` on search submit
- [ ] Page handles validation error (no filters) without API call
- [ ] Page displays LoadingSpinner during search
- [ ] Page displays "No stock found matching filters" on empty result
- [ ] Page displays ErrorBanner on API errors (400, 500) with retry button
- [ ] Page supports pagination with page size selector (25/50/100)
- [ ] Manual test: Search works, pagination works, virtual location toggle filters correctly

**Cursor Minimal Context:**
```
Files: Pages/AvailableStock.razor, Services/StockClient.cs, Components/Stock/*.razor
Pattern: Blazor page, async search, pagination state management
Constraint: No auto-refresh, user-triggered search only
```

---

### Task UI-2.5: Implement SearchAvailableStockQuery Backend

**Description:** Create MediatR query handler to search AvailableStock projection with filters, pagination, and virtual location filtering.

**Dependencies:** None (backend task)

**Acceptance Criteria:**
- [ ] `SearchAvailableStockQuery.cs` created with `Warehouse`, `Location`, `SKU`, `IncludeVirtual`, `Page`, `PageSize` params
- [ ] `IncludeVirtual` parameter defaults to `false`
- [ ] `SearchAvailableStockQueryHandler.cs` queries Marten `available_stock` projection
- [ ] Handler filters by warehouse if provided
- [ ] Handler filters by location with wildcard support (LIKE '%pattern%')
- [ ] Handler filters by SKU with wildcard support (LIKE '%pattern%')
- [ ] Handler excludes virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM) when `IncludeVirtual=false` (default)
- [ ] Handler validates at least one filter provided, throws `ValidationException` if none
- [ ] Handler paginates results and returns `PagedResult<AvailableStockItemDto>`
- [ ] Unit test verifies wildcard filtering works
- [ ] Unit test verifies virtual location exclusion when `IncludeVirtual=false`
- [ ] Integration test queries actual Marten database

**Cursor Minimal Context:**
```
Files: Application/Queries/SearchAvailableStockQuery.cs, Application/Queries/SearchAvailableStockQueryHandler.cs
Pattern: MediatR query, Marten LINQ with LIKE, pagination, virtual location filter
Constraint: Virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM) hidden by default
```

---

### Task UI-2.6: Create Available Stock API Controller

**Description:** Implement AvailableStockController with GET /api/available-stock and GET /api/warehouses endpoints.

**Dependencies:** UI-2.5

**Acceptance Criteria:**
- [ ] `AvailableStockController.cs` created in `LKvitai.MES.Api/Controllers/`
- [ ] GET `/api/available-stock` endpoint with query params: `warehouse`, `location`, `sku`, `includeVirtual` (default: false), `page` (default: 1), `pageSize` (default: 50)
- [ ] GET `/api/warehouses` endpoint returns list of warehouses from configuration
- [ ] Controller uses MediatR to dispatch queries
- [ ] Controller returns ProblemDetails on errors (400 validation, 500 server error)
- [ ] Swagger documentation generated with parameter descriptions including includeVirtual default
- [ ] Integration test verifies wildcard filtering and virtual location toggle (includeVirtual=false excludes SUPPLIER, PRODUCTION, SCRAP, SYSTEM)
- [ ] Manual test: Postman/curl returns paginated stock with filters, virtual locations hidden by default

**Cursor Minimal Context:**
```
Files: Api/Controllers/AvailableStockController.cs, Application/Queries/SearchAvailableStockQuery.cs
Pattern: ASP.NET Core controller, MediatR dispatch, query string binding, includeVirtual default false
Constraint: Must validate at least one filter provided (400 error), virtual locations hidden by default
```

---

### Task UI-2.7: Implement CSV Export for Available Stock

**Description:** Add CSV export functionality to Available Stock page for current page items via JS interop.

**Dependencies:** UI-2.4

**Acceptance Criteria:**
- [ ] Add "Export CSV" button to Available Stock page
- [ ] Button exports current page items only (not full dataset)
- [ ] Use JS interop to generate and download CSV file client-side
- [ ] CSV includes all 7 columns: Warehouse, Location, SKU, Physical Qty, Reserved Qty, Available Qty, Last Updated
- [ ] CSV filename format: `available-stock-{timestamp}.csv`
- [ ] Button disabled when no results displayed
- [ ] Manual test: Export works, CSV opens in Excel/spreadsheet app, data matches table

**Cursor Minimal Context:**
```
Files: Pages/AvailableStock.razor, wwwroot/js/csvExport.js
Pattern: Blazor JS interop, client-side CSV generation, current page export
Constraint: Export current page only (≤100 rows), no server-side streaming in Phase 1
```

---

## Package UI-3: Reservations List Page

**Purpose:** Implement Reservations list page with status filter, detail modal, pick modal, and StartPicking/Pick actions.

**Dependencies:** UI-0 (foundation), UI-Res-Index (BLOCKER - reservation projection)

**Estimated Effort:** 3-4 days

**CRITICAL:** This package is BLOCKED by UI-Res-Index. Cannot start until UI-Res-Index is complete.

---

### Task UI-3.1: Create Reservations API Controller

**Description:** Implement ReservationsController with GET /api/reservations endpoint that returns full details.

**Dependencies:** UI-Res-Index.3, UI-Res-Index.4

**Acceptance Criteria:**
- [ ] `ReservationsController.cs` created in `LKvitai.MES.Api/Controllers/`
- [ ] GET `/api/reservations` endpoint with query params: `status`, `page`, `pageSize`
- [ ] Endpoint returns full reservation details including lines and allocated HUs in paginated response
- [ ] Controller uses MediatR to dispatch SearchReservationsQuery
- [ ] Controller returns ProblemDetails on errors (404, 500)
- [ ] Swagger documentation generated for endpoint
- [ ] Integration test verifies pagination, status filtering, and full details in response
- [ ] Manual test: Postman/curl returns paginated reservations with lines and HUs

**Cursor Minimal Context:**
```
Files: Api/Controllers/ReservationsController.cs, Application/Queries/SearchReservationsQuery.cs
Pattern: ASP.NET Core controller, MediatR dispatch, ProblemDetails errors
Constraint: Must return RFC 7807 ProblemDetails on all errors, full details in list response
```

---

### Task UI-3.2: Create Reservation DTOs

**Description:** Define DTOs for reservation list (with full details) per spec-ui.md Section 6.

**Dependencies:** None

**Acceptance Criteria:**
- [ ] `ReservationDto.cs` created with `ReservationId`, `Purpose`, `Priority`, `Status`, `LockType`, `CreatedAt`, `Lines`
- [ ] `ReservationLineDto.cs` created with `SKU`, `RequestedQty`, `AllocatedQty`, `Location`, `WarehouseId`, `AllocatedHUs`
- [ ] `AllocatedHUDto.cs` created with `HuId`, `LPN`, `Qty`
- [ ] `StartPickingResponseDto.cs` created with `Success`, `Message`
- [ ] `PickResponseDto.cs` created with `Success`, `Message`
- [ ] All DTOs in `Models/` folder
- [ ] Unit test verifies JSON serialization/deserialization

**Cursor Minimal Context:**
```
Files: Models/ReservationDto.cs, Models/ReservationLineDto.cs, Models/AllocatedHUDto.cs, Models/*ResponseDto.cs
Pattern: Nested DTOs with full details, response wrappers
Constraint: Must match API contract exactly, single DTO for list and detail
```

---

### Task UI-3.3: Implement ReservationsClient

**Description:** Create typed HttpClient wrapper for reservations list, StartPicking, and Pick endpoints.

**Dependencies:** UI-0.3 (ApiException), UI-0.7 (HttpClient DI), UI-3.2

**Acceptance Criteria:**
- [ ] `ReservationsClient.cs` created in `Services/` folder
- [ ] Method: `SearchReservationsAsync(status, page, pageSize)` calls GET `/api/reservations` with query params, returns full details including lines
- [ ] Method: `StartPickingAsync(reservationId)` calls POST `/api/reservations/{id}/start-picking`
- [ ] Method: `PickAsync(reservationId, huId, sku, quantity)` calls POST `/api/reservations/{id}/pick`
- [ ] All methods use `IHttpClientFactory.CreateClient("WarehouseApi")`
- [ ] All methods parse ProblemDetails on error and throw `ApiException`
- [ ] Client registered as scoped service in `Program.cs`
- [ ] Unit test verifies error parsing for 409 (HARD_LOCK_CONFLICT), 422 (INSUFFICIENT_BALANCE)
- [ ] Integration test calls real API endpoints

**Cursor Minimal Context:**
```
Files: Services/ReservationsClient.cs, Infrastructure/ApiException.cs, Models/Reservation*.cs
Pattern: Typed HttpClient, GET list with full details, POST actions, error handling
Constraint: Must surface specific error codes (409, 422) for user messages
```

---

### Task UI-3.4: Create Reservation Components (Filter, Table, Detail Modal)

**Description:** Implement ReservationStatusFilter, ReservationsTable, and ReservationDetailModal components.

**Dependencies:** UI-0.4 (DataTable, Pagination), UI-3.2

**Acceptance Criteria:**
- [ ] `ReservationStatusFilter.razor` created with dropdown: All, ALLOCATED, PICKING
- [ ] `ReservationStatusFilter.razor` emits `OnFilterChanged(string)` event
- [ ] `ReservationsTable.razor` created with 7 columns: Reservation ID, Purpose, Priority, Status, Lock Type, Created At, Actions
- [ ] `ReservationsTable.razor` uses `DataTable<ReservationDto>` component
- [ ] `ReservationsTable.razor` displays conditional action buttons: "Start Picking" (if ALLOCATED), "Pick" (if PICKING)
- [ ] `ReservationsTable.razor` emits `OnStartPicking(Guid)`, `OnPick(Guid)`, `OnRowClick(Guid)` events
- [ ] `ReservationDetailModal.razor` created with Bootstrap modal, displays lines with allocated HUs (read-only)
- [ ] Manual test: Filter dropdown works, table renders with conditional buttons, detail modal opens/closes

**Cursor Minimal Context:**
```
Files: Components/Reservations/ReservationStatusFilter.razor, Components/Reservations/ReservationsTable.razor, Components/Reservations/ReservationDetailModal.razor
Pattern: Blazor components, conditional rendering, Bootstrap modal
Constraint: Action buttons conditional on status (ALLOCATED → Start Picking, PICKING → Pick)
```

---

### Task UI-3.5: Create Pick Modal Component

**Description:** Implement PickModal component with HU selection dropdown and quantity input with validation.

**Dependencies:** UI-3.2

**Acceptance Criteria:**
- [ ] `PickModal.razor` created with Bootstrap modal
- [ ] Modal displays HU dropdown populated from `AllocatedHUs` list (shows LPN + allocated qty)
- [ ] Modal displays quantity input field with validation (> 0 and <= allocated qty for selected HU)
- [ ] Modal displays validation error messages inline
- [ ] Modal emits `OnConfirm(huId, quantity)` event on submit
- [ ] Modal emits `OnCancel` event on cancel/close
- [ ] Manual test: Modal opens, HU dropdown populates, quantity validation works, submit/cancel work

**Cursor Minimal Context:**
```
Files: Components/Reservations/PickModal.razor, Models/AllocatedHUDto.cs
Pattern: Blazor modal, form validation, event callbacks
Constraint: Quantity must be > 0 and <= allocated qty for selected HU
```

---

### Task UI-3.6: Implement Reservations Page

**Description:** Create Reservations.razor page with status filter, data table, detail modal, pick modal, and action handlers.

**Dependencies:** UI-3.3, UI-3.4, UI-3.5

**Acceptance Criteria:**
- [ ] `Reservations.razor` created at route `/reservations`
- [ ] Page auto-searches with status=All on load
- [ ] Page calls `ReservationsClient.SearchReservationsAsync()` on filter change, receives full details including lines
- [ ] Page opens detail modal on row click, displays lines and allocated HUs from list response (no separate API call)
- [ ] Page opens pick modal on "Pick" button click, pre-populates HU dropdown from reservation lines
- [ ] Page calls `StartPickingAsync()` on "Start Picking" button click
- [ ] Page calls `PickAsync()` on pick modal submit
- [ ] Page displays success toast on action success, refreshes table
- [ ] Page displays error toast on action failure (409, 422) with specific message
- [ ] Page supports pagination with page size selector (25/50/100)
- [ ] Manual test: All actions work, toasts display, table refreshes after actions, detail modal shows data from list

**Cursor Minimal Context:**
```
Files: Pages/Reservations.razor, Services/ReservationsClient.cs, Components/Reservations/*.razor, Services/ToastService.cs
Pattern: Blazor page, modal state management, action handlers, toast notifications, detail from list
Constraint: Must refresh table after successful action, display specific error messages, no separate detail API call
```

---

### Task UI-3.7: Create Reservations Action API Endpoints

**Description:** Implement POST /api/reservations/{id}/start-picking and POST /api/reservations/{id}/pick endpoints.

**Dependencies:** None (backend task, commands already exist)

**Acceptance Criteria:**
- [ ] `ReservationsController.cs` updated with POST `/api/reservations/{id}/start-picking` endpoint
- [ ] Endpoint dispatches `StartPickingCommand` via MediatR
- [ ] Endpoint returns ProblemDetails on errors: 400 (not ALLOCATED), 409 (HARD_LOCK_CONFLICT), 422 (INSUFFICIENT_BALANCE), 500
- [ ] POST `/api/reservations/{id}/pick` endpoint added
- [ ] Endpoint dispatches `PickStockCommand` via MediatR
- [ ] Endpoint returns ProblemDetails on errors: 400 (not PICKING), 404 (HU not found), 422 (qty exceeds allocated), 500
- [ ] Swagger documentation generated
- [ ] Integration test verifies StartPicking and Pick commands execute correctly
- [ ] Manual test: Postman/curl triggers actions, returns appropriate errors

**Cursor Minimal Context:**
```
Files: Api/Controllers/ReservationsController.cs, Application/Commands/StartPickingCommand.cs, Application/Commands/PickStockCommand.cs
Pattern: ASP.NET Core controller, MediatR command dispatch, ProblemDetails errors
Constraint: Must map DomainErrorCodes to HTTP status per requirements-ui.md Section 5
```

---

## Package UI-4: Projections Admin Page

**Purpose:** Implement Projections admin page with synchronous rebuild and verify actions.

**Dependencies:** UI-0 (foundation)

**Estimated Effort:** 2-3 days

---

### Task UI-4.1: Create Projection DTOs

**Description:** Define DTOs for projection rebuild and verify per spec-ui.md Section 6.

**Dependencies:** None

**Acceptance Criteria:**
- [ ] `RebuildResultDto.cs` created with `ProjectionName`, `EventsProcessed`, `ProductionChecksum`, `ShadowChecksum`, `ChecksumMatch`, `Swapped`, `Duration`
- [ ] `VerifyResultDto.cs` created with `ChecksumMatch`, `ProductionChecksum`, `ShadowChecksum`, `ProductionRowCount`, `ShadowRowCount`
- [ ] All DTOs in `Models/` folder
- [ ] Unit test verifies JSON serialization/deserialization

**Cursor Minimal Context:**
```
Files: Models/RebuildResultDto.cs, Models/VerifyResultDto.cs
Pattern: Simple DTOs, TimeSpan serialization
Constraint: Must match API contract exactly
```

---

### Task UI-4.2: Implement ProjectionsClient

**Description:** Create typed HttpClient wrapper for projection rebuild and verify endpoints.

**Dependencies:** UI-0.3 (ApiException), UI-0.7 (HttpClient DI), UI-4.1

**Acceptance Criteria:**
- [ ] `ProjectionsClient.cs` created in `Services/` folder
- [ ] Method: `RebuildAsync(projectionName, verify)` - synchronous, blocks until complete
- [ ] Method: `VerifyAsync(projectionName)`
- [ ] Both methods use `IHttpClientFactory.CreateClient("WarehouseApi")`
- [ ] Both methods parse ProblemDetails on error and throw `ApiException`
- [ ] Client registered as scoped service in `Program.cs`
- [ ] Unit test verifies error parsing for 400 (INVALID_PROJECTION_NAME), 409 (already running), 404 (no shadow table)
- [ ] Integration test calls real API endpoints (with test data)

**Cursor Minimal Context:**
```
Files: Services/ProjectionsClient.cs, Infrastructure/ApiException.cs, Models/RebuildResultDto.cs, Models/VerifyResultDto.cs
Pattern: Typed HttpClient, synchronous POST, long timeout (30s)
Constraint: Rebuild is synchronous, no polling, blocks until complete
```

---

### Task UI-4.3: Create Projection Components (Selector, Status Card, Result Detail)

**Description:** Implement ProjectionSelector, RebuildStatusCard, and RebuildResultDetail components.

**Dependencies:** UI-0.4 (LoadingSpinner), UI-4.1

**Acceptance Criteria:**
- [ ] `ProjectionSelector.razor` created with radio buttons: LocationBalance, AvailableStock
- [ ] `ProjectionSelector.razor` emits `OnSelectionChanged(string)` event
- [ ] `RebuildStatusCard.razor` created displaying status: Idle, Running, Completed, Failed
- [ ] `RebuildStatusCard.razor` shows LoadingSpinner when status=Running
- [ ] `RebuildResultDetail.razor` created with green/red card based on `ChecksumMatch` and `Swapped`
- [ ] `RebuildResultDetail.razor` displays all metrics: EventsProcessed, Duration, Checksums, ChecksumMatch, Swapped
- [ ] Manual test: Selector works, status card shows spinner, result card colors match spec

**Cursor Minimal Context:**
```
Files: Components/Projections/ProjectionSelector.razor, Components/Projections/RebuildStatusCard.razor, Components/Projections/RebuildResultDetail.razor
Pattern: Blazor components, conditional rendering, Bootstrap cards
Constraint: Green card only if ChecksumMatch=true AND Swapped=true
```

---

### Task UI-4.4: Implement Projections Page

**Description:** Create Projections.razor page with projection selector, rebuild/verify actions, and result display.

**Dependencies:** UI-0.5 (ConfirmDialog, ToastContainer), UI-4.2, UI-4.3

**Acceptance Criteria:**
- [ ] `Projections.razor` created at route `/projections`
- [ ] Page displays projection selector (default: LocationBalance)
- [ ] Page displays "Rebuild" button, opens ConfirmDialog on click
- [ ] Page displays "Verify" button, no confirmation required
- [ ] Page calls `ProjectionsClient.RebuildAsync()` on confirm, displays LoadingSpinner (blocks UI)
- [ ] Page calls `ProjectionsClient.VerifyAsync()` on verify button click
- [ ] Page displays RebuildResultDetail card on rebuild success
- [ ] Page displays success/error toast on verify result
- [ ] Page displays error toast on action failure (400, 409, 404, 500) with specific message
- [ ] Manual test: Rebuild blocks UI until complete, verify shows toast, errors display correctly

**Cursor Minimal Context:**
```
Files: Pages/Projections.razor, Services/ProjectionsClient.cs, Components/Projections/*.razor, Components/ConfirmDialog.razor, Services/ToastService.cs
Pattern: Blazor page, synchronous action with spinner, confirmation dialog, toast notifications
Constraint: Rebuild is synchronous (no polling), must block UI until complete
```

---

### Task UI-4.5: Update RebuildProjectionCommand to Return Result

**Description:** Modify existing RebuildProjectionCommand handler to return synchronous result with all metrics.

**Dependencies:** None (backend task)

**Acceptance Criteria:**
- [ ] `RebuildProjectionCommandHandler.cs` updated to return `RebuildResultDto`
- [ ] Handler includes: `ProjectionName`, `EventsProcessed`, `ProductionChecksum`, `ShadowChecksum`, `ChecksumMatch`, `Swapped`, `Duration`
- [ ] Handler calculates checksums after rebuild completes
- [ ] Handler swaps shadow to production if `verify=true` and checksums match
- [ ] Handler throws `DomainException` with `INVALID_PROJECTION_NAME` code if projection name invalid
- [ ] Handler throws `DomainException` with `CONCURRENCY_CONFLICT` code if rebuild already running
- [ ] Unit test verifies result includes all metrics
- [ ] Integration test verifies rebuild completes synchronously

**Cursor Minimal Context:**
```
Files: Application/Commands/RebuildProjectionCommandHandler.cs, Domain/Exceptions/DomainException.cs
Pattern: MediatR command handler, synchronous execution, checksum calculation
Constraint: Must complete synchronously, no background job
```

---

### Task UI-4.6: Implement VerifyProjectionQuery Backend

**Description:** Create MediatR query handler to verify projection checksums without rebuilding.

**Dependencies:** None (backend task)

**Acceptance Criteria:**
- [ ] `VerifyProjectionQuery.cs` created with `ProjectionName` param
- [ ] `VerifyProjectionQueryHandler.cs` calculates checksums for production and shadow tables
- [ ] Handler returns `VerifyResultDto` with `ChecksumMatch`, `ProductionChecksum`, `ShadowChecksum`, `ProductionRowCount`, `ShadowRowCount`
- [ ] Handler throws `NotFoundException` if shadow table does not exist
- [ ] Handler throws `ValidationException` if projection name invalid
- [ ] Unit test verifies checksum calculation logic
- [ ] Integration test queries actual Marten database

**Cursor Minimal Context:**
```
Files: Application/Queries/VerifyProjectionQuery.cs, Application/Queries/VerifyProjectionQueryHandler.cs
Pattern: MediatR query, checksum calculation, row count comparison
Constraint: Must check shadow table exists before calculating checksums
```

---

### Task UI-4.7: Create Projections API Controller

**Description:** Implement ProjectionsController with POST /api/projections/rebuild and POST /api/projections/verify endpoints.

**Dependencies:** UI-4.5, UI-4.6

**Acceptance Criteria:**
- [ ] `ProjectionsController.cs` created in `LKvitai.MES.Api/Controllers/`
- [ ] POST `/api/projections/rebuild` endpoint with body: `{ projectionName, verify }`
- [ ] Endpoint dispatches `RebuildProjectionCommand` via MediatR, returns `RebuildResultDto` synchronously
- [ ] Endpoint returns ProblemDetails on errors: 400 (INVALID_PROJECTION_NAME), 409 (already running), 500
- [ ] POST `/api/projections/verify` endpoint with body: `{ projectionName }`
- [ ] Endpoint dispatches `VerifyProjectionQuery` via MediatR, returns `VerifyResultDto`
- [ ] Endpoint returns ProblemDetails on errors: 400 (invalid name), 404 (no shadow table), 500
- [ ] Swagger documentation generated
- [ ] Integration test verifies rebuild returns result synchronously
- [ ] Manual test: Postman/curl triggers rebuild, blocks until complete, returns result

**Cursor Minimal Context:**
```
Files: Api/Controllers/ProjectionsController.cs, Application/Commands/RebuildProjectionCommand.cs, Application/Queries/VerifyProjectionQuery.cs
Pattern: ASP.NET Core controller, MediatR dispatch, synchronous response
Constraint: Rebuild must block until complete, no async job/polling
```

---

## Summary

**Total Packages:** 6 (UI-0, UI-Res-Index, UI-1, UI-2, UI-3, UI-4)

**Total Tasks:** 38

**Package Breakdown:**
- UI-0 (Foundation): 7 tasks
- UI-Res-Index (Reservation Projection): 4 tasks
- UI-1 (Dashboard): 6 tasks
- UI-2 (Available Stock): 7 tasks
- UI-3 (Reservations): 7 tasks
- UI-4 (Projections): 7 tasks

**Critical Dependencies:**
- UI-Res-Index BLOCKS UI-3 (Reservations page requires ReservationSummaryView projection)
- UI-0.2a and UI-0.2b BLOCK all API error handling (ProblemDetails parser and error code mapper)
- All packages depend on UI-0 (foundation)

**Phase 1 Exclusions (Confirmed):**
- No SignalR real-time updates
- No Chart.js visualizations
- No async projection rebuild with jobId/status polling/progress bars/log streaming
- No authentication enforcement (placeholder structure only)

**Technology Stack:**
- Frontend: Blazor Server (.NET 8) + Bootstrap 5
- Backend: ASP.NET Core 8 Web API + MediatR + Marten
- No additional libraries (no SignalR, no Chart.js)

---

**End of Tasks Document**
