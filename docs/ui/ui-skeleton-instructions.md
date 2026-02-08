# UI Skeleton Implementation Playbook

**Scope:** Package UI-0 — Foundation & Error Model (tasks-ui.md UI-0.1 through UI-0.7)
**Stack:** Blazor Server (.NET 8) + Bootstrap 5
**Phase 1 exclusions:** No SignalR, No Chart.js, No async rebuild/polling, No auth enforcement
**Source of truth:** requirements-ui.md (behavior), spec-ui.md (architecture), tasks-ui.md (tasks)

---

## 1. Project Structure and Naming

### 1.1 Create the Blazor Server project

```
cd src/
dotnet new blazorserver -n LKvitai.MES.WebUI --framework net8.0 --no-https false
dotnet sln LKvitai.MES.sln add LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj
```

The project lives at `src/LKvitai.MES.WebUI/` — same level as `LKvitai.MES.Api/`.
It inherits `Directory.Build.props` (LangVersion=latest, nullable already enabled by template).

Add it to the `src` solution folder in the `.sln` (same GUID `{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}` as other src projects).

**No project reference to Api.** The WebUI talks to the API over HTTP — it is a separate host process. The only shared reference (optional, for DTO reuse) is:

```xml
<ProjectReference Include="..\LKvitai.MES.Contracts\LKvitai.MES.Contracts.csproj" />
```

If you prefer fully-decoupled DTOs on the UI side, skip this reference and duplicate the DTO records in `Models/`. Recommendation: **skip the reference** — keep UI models independent so API contract changes are explicit.

### 1.2 Folder layout

Delete the template's `Data/`, `Pages/Counter.razor`, `Pages/FetchData.razor`, `Shared/SurveyPrompt.razor`. Then create:

```
src/LKvitai.MES.WebUI/
├── Program.cs
├── App.razor
├── _Imports.razor
├── appsettings.json
├── appsettings.Development.json
│
├── Shared/
│   ├── MainLayout.razor
│   ├── MainLayout.razor.css
│   ├── NavMenu.razor
│   └── AuthPlaceholder.razor          # empty file, TODO comment for Phase 2
│
├── Pages/
│   ├── Dashboard.razor                # @page "/dashboard"
│   ├── AvailableStock.razor           # @page "/available-stock"
│   ├── Reservations.razor             # @page "/reservations"
│   └── Projections.razor              # @page "/projections"
│
├── Components/
│   ├── DataTable.razor                # generic, @typeparam TItem
│   ├── Pagination.razor
│   ├── LoadingSpinner.razor
│   ├── ErrorBanner.razor
│   ├── StaleBadge.razor
│   ├── ConfirmDialog.razor
│   └── ToastContainer.razor
│
├── Services/
│   ├── DashboardClient.cs
│   ├── StockClient.cs
│   ├── ReservationsClient.cs
│   ├── ProjectionsClient.cs
│   └── ToastService.cs
│
├── Models/
│   ├── PagedResult.cs                 # generic
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
│   └── VerifyResultDto.cs
│
├── Infrastructure/
│   ├── ProblemDetailsModel.cs
│   ├── ProblemDetailsParser.cs
│   ├── ApiException.cs
│   └── ErrorCodeMessages.cs
│
└── wwwroot/
    └── js/
        └── csvExport.js               # JS interop for client-side CSV download
```

**Naming rules:**
- Razor components: PascalCase, `.razor` extension, no `.razor.cs` code-behind unless >50 lines of logic.
- Services/clients: `{Domain}Client.cs` (not `{Domain}Service.cs`) — these are HTTP clients, not domain services.
- DTOs: `{Name}Dto.cs`, always `public record` with `init` properties.
- Infrastructure: error handling plumbing only — no business logic.

### 1.3 Clean up template files

Remove from `_Host.cshtml` (or `App.razor` in .NET 8 template): any reference to `WeatherForecast`, `Counter`, `FetchData`. Keep the Bootstrap CDN links.

---

## 2. Dependency Injection + Configuration

### 2.1 appsettings.json

File: `src/LKvitai.MES.WebUI/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "WarehouseApi": {
    "BaseUrl": "https://localhost:5001"
  },
  "StalenessThresholds": {
    "WarnSeconds": 5,
    "AlertSeconds": 30
  },
  "RefreshIntervals": {
    "DashboardSeconds": 30
  }
}
```

The `WarehouseApi:BaseUrl` must match the API project's launch URL. Create a `appsettings.Development.json` override if ports differ locally.

### 2.2 Program.cs — HttpClient registration

File: `src/LKvitai.MES.WebUI/Program.cs`

Key registrations (pseudo-code, do not copy as-is — adapt to actual template):

```
// Named HttpClient for all API calls
builder.Services.AddHttpClient("WarehouseApi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["WarehouseApi:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);  // covers sync rebuild
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// Typed clients — each gets the named HttpClient via factory
builder.Services.AddScoped<DashboardClient>();
builder.Services.AddScoped<StockClient>();
builder.Services.AddScoped<ReservationsClient>();
builder.Services.AddScoped<ProjectionsClient>();

// Toast service (in-memory, scoped to circuit)
builder.Services.AddScoped<ToastService>();
```

**30-second timeout** is mandatory. The sync projection rebuild can take up to 60s under load; if that becomes an issue, increase to 90s — but do not remove the timeout entirely.

Each typed client receives `IHttpClientFactory` via constructor injection and calls `_factory.CreateClient("WarehouseApi")` per request. Do **not** inject `HttpClient` directly — Blazor Server circuits are long-lived and would hold stale connections.

### 2.3 _Imports.razor

File: `src/LKvitai.MES.WebUI/_Imports.razor`

Add:
```
@using LKvitai.MES.WebUI.Components
@using LKvitai.MES.WebUI.Models
@using LKvitai.MES.WebUI.Services
@using LKvitai.MES.WebUI.Infrastructure
@using Microsoft.AspNetCore.Components.Web
```

### 2.4 Bootstrap CDN

In the host page (`_Host.cshtml` or `App.razor`), ensure:
- Bootstrap 5.3+ CSS via CDN
- Bootstrap Icons 1.11+ via CDN
- No jQuery, no Popper.js separate — Bootstrap 5 bundle includes Popper

The `wwwroot/js/csvExport.js` script tag goes at the bottom of the body.

---

## 3. API Client Policy

### 3.1 ProblemDetailsModel

File: `Infrastructure/ProblemDetailsModel.cs`

```
public record ProblemDetailsModel
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}
```

`TraceId` may come from:
1. The JSON body's `traceId` field (preferred).
2. The response header `traceparent` (fallback).
3. `null` if neither present.

### 3.2 ProblemDetailsParser

File: `Infrastructure/ProblemDetailsParser.cs`

Single static method:

```
public static class ProblemDetailsParser
{
    public static async Task<ProblemDetailsModel?> ParseAsync(HttpResponseMessage response)
```

Logic:
1. Check `response.Content.Headers.ContentType?.MediaType` equals `"application/problem+json"`. If not, return `null`.
2. Read body string: `await response.Content.ReadAsStringAsync()`.
3. Deserialize with `System.Text.Json` using `PropertyNameCaseInsensitive = true`.
4. If `TraceId` is null in body, try `response.Headers` → `traceparent` header → extract trace-id segment.
5. Return `ProblemDetailsModel` or `null` on any deserialization failure (never throw).

### 3.3 ErrorCodeMessages

File: `Infrastructure/ErrorCodeMessages.cs`

Static `Dictionary<string, string>` with 12 entries (7 domain + 5 generic), sourced from requirements-ui.md Section 5:

| Key | Message |
|-----|---------|
| `INSUFFICIENT_BALANCE` | Insufficient balance. Cannot complete operation. |
| `RESERVATION_NOT_ALLOCATED` | Reservation is not in ALLOCATED state. Cannot start picking. |
| `HARD_LOCK_CONFLICT` | HARD lock conflict detected. Another reservation is already picking this stock. |
| `INVALID_PROJECTION_NAME` | Invalid projection name. Must be LocationBalance or AvailableStock. |
| `IDEMPOTENCY_IN_PROGRESS` | Request is currently being processed. Please wait. |
| `IDEMPOTENCY_ALREADY_PROCESSED` | Request already processed. Idempotency key conflict. |
| `CONCURRENCY_CONFLICT` | Concurrent modification detected. Please retry. |
| `VALIDATION_ERROR` | One or more validation errors occurred. |
| `NOT_FOUND` | The requested resource was not found. |
| `UNAUTHORIZED` | Authentication required. |
| `FORBIDDEN` | You do not have permission to perform this action. |
| `INTERNAL_ERROR` | Server error. Please try again later. |

Plus fallback logic:
- Unknown code → "An unexpected error occurred. Please try again."
- No code + HTTP 500 → "Server error. Please try again later."
- No code + HTTP 503 → "Backend unavailable. Please check system status."

Expose via: `public static string GetMessage(string? errorCode, int? httpStatus)`.

### 3.4 ApiException

File: `Infrastructure/ApiException.cs`

```
public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }
    public string? TraceId { get; }
    public string UserMessage { get; }
    public ProblemDetailsModel? ProblemDetails { get; }
```

Constructor: accept `ProblemDetailsModel` (nullable) and `int statusCode`.
- If ProblemDetails present: extract `ErrorCode` from `Type` field (strip URI prefix if present, or use raw value), look up `UserMessage` via `ErrorCodeMessages.GetMessage(errorCode, status)`, store `TraceId`.
- If ProblemDetails null: set `UserMessage` from status-only fallback.
- `ToString()` must include `TraceId` for log correlation.

### 3.5 Error handling flow in every typed client method

**CRITICAL RULE — never use `EnsureSuccessStatusCode()` before reading the body.**

Every client method follows this exact pattern:

```
var response = await client.GetAsync(url);       // or PostAsJsonAsync, etc.
var body = await response.Content.ReadAsStringAsync();

if (!response.IsSuccessStatusCode)
{
    var problem = await ProblemDetailsParser.ParseAsync(response);
    throw new ApiException(problem, (int)response.StatusCode);
}

return JsonSerializer.Deserialize<TDto>(body, _jsonOptions)!;
```

Extract this into a shared `SendAndParseAsync<T>` helper method on a base class or extension method to avoid repetition across 4 clients.

### 3.6 Page-level error handling

Every page wraps its data-loading call in `try/catch (ApiException ex)`:

```razor
@if (_error is not null)
{
    <ErrorBanner Message="@_error.UserMessage"
                 TraceId="@_error.TraceId"
                 OnRetry="LoadDataAsync"
                 OnDismiss="() => _error = null" />
}
```

Toast-style errors (for actions like StartPicking, Pick) use `ToastService` instead.

---

## 4. Shared Components

### 4.1 ErrorBanner

File: `Components/ErrorBanner.razor`

| Parameter | Type | Purpose |
|-----------|------|---------|
| `Message` | `string` | User-facing error message |
| `TraceId` | `string?` | Displayed as "Error ID: {traceId}" below message |
| `OnRetry` | `EventCallback` | Retry button click |
| `OnDismiss` | `EventCallback` | X button click |

Markup: Bootstrap `alert alert-danger alert-dismissible`. Retry button as `btn btn-outline-danger btn-sm`. TraceId in `<small class="text-muted d-block">`.

If `TraceId` is null, hide the Error ID line.

### 4.2 StaleBadge

File: `Components/StaleBadge.razor`

| Parameter | Type | Purpose |
|-----------|------|---------|
| `LastUpdated` | `DateTime` | UTC timestamp to compute lag against |

Logic (computed on render, no timer):
- `lag = DateTime.UtcNow - LastUpdated`
- `lag < 5s` → render nothing
- `5s <= lag < 30s` → `<span class="badge bg-warning text-dark">Stale</span>`
- `lag >= 30s` → `<span class="badge bg-danger">Stale</span>`

Read thresholds from `IConfiguration["StalenessThresholds:WarnSeconds"]` and `AlertSeconds` if you want configurability, but hardcoding 5/30 is acceptable for the skeleton.

### 4.3 DataTable<TItem>

File: `Components/DataTable.razor`

```razor
@typeparam TItem
```

| Parameter | Type | Purpose |
|-----------|------|---------|
| `Items` | `IReadOnlyList<TItem>` | Current page items |
| `TotalCount` | `int` | Total across all pages (for Pagination) |
| `Columns` | `RenderFragment` | `<th>` header cells — caller defines |
| `RowTemplate` | `RenderFragment<TItem>` | `<td>` cells per row — caller defines |
| `EmptyMessage` | `string` | Shown when `Items` is empty |
| `OnRowClick` | `EventCallback<TItem>` | Row click handler |

Renders a `<table class="table table-striped table-hover">`. Each `<tr>` gets `@onclick` bound to `OnRowClick`. No built-in sorting (Phase 2). No built-in pagination — that's the `Pagination` component, composed externally.

### 4.4 Pagination

File: `Components/Pagination.razor`

| Parameter | Type | Purpose |
|-----------|------|---------|
| `CurrentPage` | `int` | 1-based |
| `TotalPages` | `int` | Computed by caller: `(int)Math.Ceiling((double)totalCount / pageSize)` |
| `PageSize` | `int` | Current page size |
| `PageSizeOptions` | `int[]` | Default: `[25, 50, 100]` |
| `OnPageChanged` | `EventCallback<int>` | New page number |
| `OnPageSizeChanged` | `EventCallback<int>` | New page size (resets to page 1) |

Markup: Bootstrap `pagination` component. Show "Page {CurrentPage} of {TotalPages}". Prev/Next buttons disabled at boundaries. Page size as `<select class="form-select form-select-sm">`.

### 4.5 LoadingSpinner

File: `Components/LoadingSpinner.razor`

| Parameter | Type | Purpose |
|-----------|------|---------|
| `IsLoading` | `bool` | Show/hide |

Markup: `<div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div>` wrapped in a centered overlay `div` with semi-transparent backdrop. Only renders when `IsLoading == true`.

### 4.6 ConfirmDialog

File: `Components/ConfirmDialog.razor`

| Parameter | Type | Purpose |
|-----------|------|---------|
| `Title` | `string` | Modal title |
| `Message` | `string` | Modal body |
| `ConfirmText` | `string` | Confirm button label (default: "Confirm") |
| `IsVisible` | `bool` | Show/hide modal |
| `OnConfirm` | `EventCallback` | Confirm action |
| `OnCancel` | `EventCallback` | Cancel/close action |

Markup: Bootstrap modal with `modal-dialog-centered`. JS interop not needed — control visibility with `@if (IsVisible)` and CSS class toggling.

### 4.7 ToastContainer + ToastService

**ToastService** (`Services/ToastService.cs`):
- `event Action<ToastMessage>? OnShow`
- `void ShowSuccess(string message)`
- `void ShowError(string message)`
- `void ShowWarning(string message)`
- `record ToastMessage(string Message, ToastType Type, DateTime CreatedAt)`
- `enum ToastType { Success, Error, Warning }`

**ToastContainer** (`Components/ToastContainer.razor`):
- Placed once in `MainLayout.razor`.
- Subscribes to `ToastService.OnShow` in `OnInitialized`, unsubscribes in `Dispose`.
- Maintains `List<ToastMessage>`. On new message: add to list, call `StateHasChanged()`, start a 5-second `Task.Delay` to remove.
- Renders Bootstrap toast stack in bottom-right corner: `position-fixed bottom-0 end-0 p-3`.
- Toast background: `bg-success` / `bg-danger` / `bg-warning` per type.

---

## 5. Base Layout

### 5.1 MainLayout.razor

File: `Shared/MainLayout.razor`

Structure:
```
<div class="d-flex">
    <!-- Sidebar -->
    <nav class="sidebar bg-dark text-white" style="width: 250px; min-height: 100vh;">
        <NavMenu />
    </nav>

    <!-- Main content -->
    <div class="flex-grow-1">
        <!-- Top bar -->
        <header class="navbar navbar-light bg-light border-bottom px-3">
            <span class="navbar-brand mb-0 h1">LKvitai.MES Warehouse</span>
            <span class="text-muted small">@_environment</span>
            <button class="btn btn-outline-secondary btn-sm" disabled>Login</button>
        </header>

        <!-- Page body -->
        <main class="container-fluid p-4">
            <ToastContainer />
            @Body
        </main>
    </div>
</div>
```

`_environment`: read from `IWebHostEnvironment.EnvironmentName` — shows "Development" / "Staging" / "Production" in the top bar.

`MainLayout.razor.css`: sidebar collapse on narrow viewports (`@media (max-width: 768px) { .sidebar { display: none; } }`). Full responsive sidebar is Phase 2.

### 5.2 NavMenu.razor

File: `Shared/NavMenu.razor`

Four `<NavLink>` items:

| Label | href | Icon class | Match |
|-------|------|-----------|-------|
| Dashboard | `/dashboard` | `bi bi-speedometer2` | `NavLinkMatch.All` |
| Available Stock | `/available-stock` | `bi bi-box-seam` | `NavLinkMatch.Prefix` |
| Reservations | `/reservations` | `bi bi-clipboard-check` | `NavLinkMatch.Prefix` |
| Projections | `/projections` | `bi bi-database-gear` | `NavLinkMatch.Prefix` |

Each link: `<NavLink class="nav-link text-white" href="..." Match="..."><i class="bi bi-xxx me-2"></i>Label</NavLink>` inside a `<ul class="nav flex-column p-3">`.

Active state: Blazor's `NavLink` auto-adds `active` CSS class.

### 5.3 Route redirect

In `App.razor` (or a `Pages/Index.razor` with `@page "/"`), redirect `/` to `/dashboard`:

```razor
@page "/"
@inject NavigationManager Nav

@code {
    protected override void OnInitialized() => Nav.NavigateTo("/dashboard", replace: true);
}
```

### 5.4 Stub pages

Create 4 page files with `@page` directive, page title, and placeholder content:

```razor
@page "/dashboard"
<PageTitle>Dashboard - LKvitai.MES</PageTitle>
<h3>Dashboard</h3>
<p class="text-muted">Coming soon: health cards, stock summary, reservation summary, projection health, recent activity.</p>
```

Same pattern for `/available-stock`, `/reservations`, `/projections`. These stubs prove routing works and will be replaced by actual implementations in packages UI-1 through UI-4.

---

## 6. JS Interop (CSV Export)

File: `wwwroot/js/csvExport.js`

Single function:

```javascript
window.csvExport = {
    download: function (filename, csvContent) {
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
    }
};
```

Called from Blazor via `IJSRuntime.InvokeVoidAsync("csvExport.download", filename, csvString)`.

The CSV string is built in C# (in the page code, not the component) from `Items` on the current page only. Never export the full dataset — that's Phase 2 (server-side streaming).

Add `<script src="js/csvExport.js"></script>` to the host page, after the Blazor framework script.

---

## 7. Testing + Gates

### 7.1 Build gate

```
cd src/
dotnet build LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj --no-restore
```

Must exit 0 with zero warnings treated as errors (per `Directory.Build.props` — currently `TreatWarningsAsErrors=false`, so warnings are OK but errors are not).

### 7.2 Unit tests (optional but recommended for skeleton)

Create `tests/LKvitai.MES.Tests.WebUI/` with xUnit. Recommended skeleton tests:

| Test class | What to test |
|------------|-------------|
| `ProblemDetailsParserTests` | Parse valid ProblemDetails JSON → model. Parse non-ProblemDetails → null. Parse body with missing traceId → model with null TraceId. |
| `ErrorCodeMessagesTests` | All 12 codes return non-empty string. Unknown code returns fallback. Status-only fallback (500, 503). |
| `ApiExceptionTests` | Constructor with ProblemDetails sets all properties. Constructor with null ProblemDetails uses fallback. ToString includes TraceId. |
| `StaleBadgeLogicTests` | Test threshold boundaries: 4s → no badge, 5s → warning, 29s → warning, 30s → danger. |

### 7.3 What to mock

- `HttpMessageHandler` (via `MockHttpMessageHandler`) for typed client tests — return canned `HttpResponseMessage` with ProblemDetails JSON body.
- `IConfiguration` for threshold values.
- `IJSRuntime` for CSV export tests (verify `InvokeVoidAsync` called with correct args).
- Do **not** mock `IHttpClientFactory` directly — mock the handler inside it.

### 7.4 Integration test (optional for skeleton)

If the API project is running locally, a smoke test can verify the `WarehouseApi` HttpClient resolves and can hit `GET /api/dashboard/health`. This is not required for the skeleton gate.

---

## 8. "Done" Definition — Skeleton Checklist

The skeleton is complete when every item below is checked. Use this as the self-validation gate for Codex/Cursor.

### Project structure
- [ ] `src/LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj` exists, targets `net8.0`
- [ ] Project added to `LKvitai.MES.sln` in the `src` solution folder
- [ ] `dotnet build src/LKvitai.MES.WebUI/` exits 0
- [ ] Folder structure matches Section 1.2 (Pages, Components, Services, Models, Infrastructure, Shared, wwwroot/js)

### Configuration
- [ ] `appsettings.json` contains `WarehouseApi:BaseUrl`, `StalenessThresholds`, `RefreshIntervals`
- [ ] `Program.cs` registers named HttpClient `"WarehouseApi"` with BaseAddress, 30s timeout, Accept header
- [ ] `Program.cs` registers 4 typed clients (DashboardClient, StockClient, ReservationsClient, ProjectionsClient) as scoped
- [ ] `Program.cs` registers `ToastService` as scoped

### Infrastructure (error handling)
- [ ] `ProblemDetailsModel.cs` exists with Type, Title, Status, Detail, TraceId, Errors properties
- [ ] `ProblemDetailsParser.cs` exists with `ParseAsync(HttpResponseMessage)` — returns null for non-ProblemDetails
- [ ] `ErrorCodeMessages.cs` exists with 12 mapped codes (7 domain + 5 generic) + fallback logic
- [ ] `ApiException.cs` exists with StatusCode, ErrorCode, TraceId, UserMessage, ProblemDetails properties

### Shared components
- [ ] `ErrorBanner.razor` renders alert-danger with Message, TraceId ("Error ID: ..."), retry, dismiss
- [ ] `StaleBadge.razor` renders nothing <5s, yellow 5-30s, red >=30s
- [ ] `DataTable.razor` is generic (`@typeparam TItem`) with Items, Columns (RenderFragment), RowTemplate, EmptyMessage, OnRowClick
- [ ] `Pagination.razor` renders Prev/Next + "Page X of Y" + page size selector (25/50/100)
- [ ] `LoadingSpinner.razor` renders Bootstrap spinner when IsLoading=true
- [ ] `ConfirmDialog.razor` renders Bootstrap modal with Title, Message, OnConfirm, OnCancel
- [ ] `ToastContainer.razor` renders stacked toasts, auto-dismiss 5s
- [ ] `ToastService.cs` exposes ShowSuccess/ShowError/ShowWarning + OnShow event

### Layout and navigation
- [ ] `MainLayout.razor` renders sidebar + top bar ("LKvitai.MES Warehouse" + environment name + disabled Login button)
- [ ] `NavMenu.razor` renders 4 nav links with correct routes and Bootstrap Icons
- [ ] `/` redirects to `/dashboard`
- [ ] All 4 stub pages exist and render at their routes (`/dashboard`, `/available-stock`, `/reservations`, `/projections`)
- [ ] `ToastContainer` is placed once in MainLayout

### DTOs (all in Models/)
- [ ] `PagedResult<T>.cs` — generic record with Items, TotalCount, Page, PageSize
- [ ] `HealthStatusDto.cs` — Status, ProjectionLag, LastCheck
- [ ] `StockSummaryDto.cs` — TotalSKUs, TotalQuantity, TotalValue
- [ ] `ReservationSummaryDto.cs` — Allocated, Picking, Consumed
- [ ] `ProjectionHealthDto.cs` — LocationBalanceLag, AvailableStockLag, LastRebuildLB, LastRebuildAS
- [ ] `RecentMovementDto.cs` — MovementId, SKU, Quantity, FromLocation, ToLocation, Timestamp
- [ ] `AvailableStockItemDto.cs` — WarehouseId, Location, SKU, PhysicalQty, ReservedQty, AvailableQty, LastUpdated
- [ ] `WarehouseDto.cs` — Id, Code, Name
- [ ] `ReservationDto.cs` — ReservationId, Purpose, Priority, Status, LockType, CreatedAt, Lines
- [ ] `ReservationLineDto.cs` — SKU, RequestedQty, AllocatedQty, Location, WarehouseId, AllocatedHUs
- [ ] `AllocatedHUDto.cs` — HuId, LPN, Qty
- [ ] `StartPickingResponseDto.cs` — Success, Message
- [ ] `PickResponseDto.cs` — Success, Message
- [ ] `RebuildResultDto.cs` — ProjectionName, EventsProcessed, ProductionChecksum, ShadowChecksum, ChecksumMatch, Swapped, Duration
- [ ] `VerifyResultDto.cs` — ChecksumMatch, ProductionChecksum, ShadowChecksum, ProductionRowCount, ShadowRowCount

### Typed clients (stub methods — can throw NotImplementedException for non-skeleton packages)
- [ ] `DashboardClient.cs` — 5 GET methods, uses factory, error handling via ProblemDetailsParser
- [ ] `StockClient.cs` — SearchAvailableStockAsync + GetWarehousesAsync, uses factory
- [ ] `ReservationsClient.cs` — SearchReservationsAsync + StartPickingAsync + PickAsync, uses factory
- [ ] `ProjectionsClient.cs` — RebuildAsync + VerifyAsync, uses factory

### JS interop
- [ ] `wwwroot/js/csvExport.js` exists with `window.csvExport.download(filename, csvContent)` function
- [ ] Script tag added to host page

### Build + tests
- [ ] `dotnet build` passes for WebUI project
- [ ] (Optional) `ProblemDetailsParserTests` pass
- [ ] (Optional) `ErrorCodeMessagesTests` pass
- [ ] (Optional) `ApiExceptionTests` pass

---

## Appendix A: What This Skeleton Does NOT Include

These are handled by subsequent packages (UI-1 through UI-4, UI-Res-Index):

- Dashboard card components (HealthStatusCard, StockSummaryCard, etc.) — **UI-1**
- Stock filter/table components — **UI-2**
- Reservation filter/table/modal components — **UI-3**
- Projection selector/status/result components — **UI-4**
- ReservationSummaryProjection backend — **UI-Res-Index**
- Backend API controllers and query handlers — **UI-1 through UI-4**
- Actual page logic (data loading, auto-refresh, actions) — **UI-1 through UI-4**

The skeleton provides the **shell** that all subsequent packages plug into.

## Appendix B: Existing Backend Error Codes

The current `DomainErrorCodes.cs` in `SharedKernel` has these constants:

```
IdempotencyInProgress, IdempotencyAlreadyProcessed,
HandlingUnitSealed, ConcurrencyConflict, ReceiveGoodsFailed,
ReservationNotFound, ReservationNotPending, InsufficientAvailableStock, AllocationFailed,
ReservationNotPicking, PickStockMovementFailed, PickStockConsumptionFailed,
PickStockConsumptionDeferred, PickStockFailedPermanently,
StuckReservationDetected, OrphanHardLockDetected
```

The UI-side `ErrorCodeMessages.cs` maps a **subset** of these plus 4 new codes that UI-0.2b will add to the backend (`INSUFFICIENT_BALANCE`, `RESERVATION_NOT_ALLOCATED`, `HARD_LOCK_CONFLICT`, `INVALID_PROJECTION_NAME`) and 5 generic codes (`VALIDATION_ERROR`, `NOT_FOUND`, `UNAUTHORIZED`, `FORBIDDEN`, `INTERNAL_ERROR`). The UI mapper does not need to know about backend-only codes (e.g., `PickStockConsumptionDeferred`).

## Appendix C: Key Constraints Reminder

| Constraint | Rule |
|-----------|------|
| Error handling | Read response body FIRST, then check `IsSuccessStatusCode`. NEVER `EnsureSuccessStatusCode()`. |
| HttpClient lifetime | Use `IHttpClientFactory`, never inject `HttpClient` directly in Blazor Server. |
| Timeout | 30s minimum (sync rebuild). |
| Pagination | Default 50, options [25, 50, 100], max 100. |
| CSV export | Current page only, client-side JS interop. |
| Staleness | 5s warn (yellow), 30s alert (red). |
| Phase 1 bans | No SignalR, No Chart.js, No async rebuild/polling, No auth enforcement. |
| DTOs | `public record` with `init` properties. Match API contract exactly. |

---

**End of UI Skeleton Playbook**
