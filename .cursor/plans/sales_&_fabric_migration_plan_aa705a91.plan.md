---
name: Sales & Fabric Migration Plan
overview: UI-first migration of Sales Orders and Fabric Availability from the .NET 6 legacy MVC app into the scaffolded Sales and Frontline modules of LKvitai.MES, wrapping legacy SQL Server stored procedures behind an ACL gateway in Infrastructure.
todos:
  - id: S-0
    content: Sales.WebUI design shell with local sample records only; no Sales.Contracts dependency yet
    status: pending
  - id: S-1
    content: Promote sample record shapes into Sales.Contracts DTOs + Application queries/ports + Sales.Api stub endpoints + update WebUI to use HttpClient; MediatR optional — follow existing module pattern
    status: pending
  - id: S-2
    content: Sales.Infrastructure SqlOrdersQueryService — real weblb_* proc calls, server-side search, auth hardening
    status: pending
  - id: S-3
    content: Sales polish — Portal auth on Api, Open in PoS, keyboard shortcuts, empty/error states, export stub
    status: pending
  - id: F-0
    content: Frontline.WebUI design shell with local sample records only; no Frontline.Contracts dependency yet
    status: pending
  - id: F-1
    content: Promote sample record shapes into Frontline.Contracts DTOs + Application queries/ports + Frontline.Api stub endpoints (anonymous in dev only — see auth note) + update WebUI to use HttpClient; MediatR optional — follow existing module pattern
    status: pending
  - id: F-2
    content: Frontline.Infrastructure SqlFabricQueryService — weblb_Fabric_GetMobileCard, 3 result sets, nullable guards, auth
    status: pending
  - id: F-3
    content: Desktop low-stock list real data — confirm proc shape for low-stock list (R-6/R-7), implement SqlFabricQueryService.GetLowStockListAsync
    status: pending
isProject: false
---

# Sales Orders & Fabric Availability — Migration Plan

> **Fabric Cutter is explicitly out of scope.**
> Source of truth for all UI/styling: `docs/ux/sales-orders-codex-rules.md` and `docs/ux/fabric-availability-codex-rules.md` (plus the matching static preview HTML files).

---

## Part 1 — Current State Inventory

### 1a. New MES — Sales module (scaffolded only)

| Project | State |
|---------|-------|
| `Sales.Contracts` | `_Module.cs` marker only |
| `Sales.Application` | `_Placeholder.cs` only |
| `Sales.Infrastructure` | `_Module.cs` marker only |
| `Sales.Api` | `Program.cs` — minimal startup, no endpoints |
| `Sales.WebUI` | `Index.razor` stub, `MainLayout`, `RedirectToLogin`; **MudBlazor 6.20.0 already referenced**; Portal cookie auth wired toward `SalesApi` |

No `Sales.Domain` project exists (none scaffolded; none needed for read-only phase 1).

### 1b. New MES — Frontline module (scaffolded only)

| Project | State |
|---------|-------|
| `Frontline.Contracts` | marker only |
| `Frontline.Application` | placeholder only |
| `Frontline.Infrastructure` | marker only |
| `Frontline.Api` | `GET /api/frontline/ping` only, **no auth wired** |
| `Frontline.WebUI` | `Index.razor` stub; MudBlazor referenced; **no** Portal auth yet |

### 1c. Legacy app (`LKvitai.Web`, .NET 6, ADO.NET, SQL Server)

**Orders — procs called (all via `SqlCommand` with `LKvitaiDb` connection string):**

| Proc | Where called | Parameters | Notable |
|------|-------------|------------|---------|
| `dbo.weblb_Orders` | `Api/OrdersController.Get()` | none | Full table dump; DataTables pages client-side. Column 11 = `ProductsSearch` hidden text field |
| `dbo.weblb_Order` | MVC `OrdersController.Details` | `@OrderId bigint` | Header row |
| `dbo.weblb_Accessories` | same reader chain | `@OrderId` | Accessory lines |
| `dbo.weblb_Items` | same | `@OrderId` | Product lines |
| `dbo.weblb_Employees` | same | `@OrderId` | Employee/duty rows |

**Fabric — proc called:**

| Proc | Parameters | Result sets |
|------|------------|-------------|
| `dbo.weblb_Fabric_GetMobileCard` | `@Code`, `@DefaultPhotoUrl`, `@LowThreshold int`, `@EnoughThreshold int` | RS1: header (Name, Notes, PhotoUrl, DiscountPercent — nullable); RS2: widths (WidthMm, Status enum, StockMeters nullable, ExpectedDate nullable); RS3: alternatives (Code, PhotoUrl, WidthMm, Status, StockMeters nullable, ExpectedDate nullable) |

No `Supplier` or `LastChecked` fields in the existing proc — the **Desktop low-stock list is a new screen** not backed by the current proc.

---

## Part 2 — Target Architecture

### Sales Orders

```
Sales.Contracts
  OrderSummaryDto        (list row: Id, Number, Date, Price, Debt, Customer, Status, Store, Address, HasDebt, IsVip, HasNote, ProductsSearch)
  OrderDetailsDto        (header fields from weblb_Order)
  OrderItemDto           (from weblb_Items)
  OrderAccessoryDto      (from weblb_Accessories)
  OrderEmployeeDto       (from weblb_Employees)
  OrdersQueryParams      (search, status, store, dateFrom, dateTo, hasDebt, page, pageSize, sortField, sortDir)
  PagedResult<T>

Sales.Application
  Queries/
    GetOrdersQuery        → IOrdersQueryService.GetOrdersAsync(params)
    GetOrderDetailsQuery  → IOrdersQueryService.GetOrderDetailsAsync(id)
  Ports/
    IOrdersQueryService   (interface)

Sales.Infrastructure
  SqlOrdersQueryService   implements IOrdersQueryService
    - opens SqlConnection("LKvitaiDb")  ← SQL Server, separate from PG
    - calls weblb_Orders / weblb_Order / weblb_Accessories / weblb_Items / weblb_Employees
    - maps nullable columns defensively (DBNull guards)
    - server-side paging/search: see Risk R-1 below

Sales.Api
  OrdersController
    GET /api/sales/orders?{params}    → GetOrdersQuery → PagedResult<OrderSummaryDto>
    GET /api/sales/orders/{id}        → GetOrderDetailsQuery → OrderDetailsDto

Sales.WebUI
  Pages/
    Index.razor           (module root — keeps @page "/" / hosts Orders layout or thin redirect; do NOT break /sales/ tile)
    Orders.razor          (if separate page; list above, details below selected row)
  Components/
    OrdersGrid.razor      (MudDataGrid ServerData=@, custom lk-grid/chip CSS)
    OrderDetailPanel.razor
    OrderDetailHeader.razor
    OrderItemsTable.razor
    OrderAmountsGrid.razor
    OrderEmployeesTable.razor
  wwwroot/css/
    tokens.css            (from codex §1)
    orders.css            (layout, lk-table, chips, amounts-grid, mobile-cards)
```

### Fabric Availability → Frontline module

```
Frontline.Contracts
  FabricCardDto          (Name, PhotoUrl, Notes, DiscountPercent, Widths[], Alternatives[])
  WidthStockDto          (WidthMm, Status enum, StockMeters, ExpectedDate, IncomingMeters, IncomingDate)
  FabricAlternativeDto   (Code, PhotoUrl, WidthMm, Status, StockMeters, ExpectedDate)
  FabricLowStockDto      (desktop list row — requires new/extended proc; see R-6)
  FabricLookupParams     (Code, Width, DefaultPhotoUrl, LowThreshold, EnoughThreshold)

Frontline.Application
  Queries/
    GetFabricCardQuery    → IFabricQueryService.GetMobileCardAsync(params)
    GetLowStockListQuery  → IFabricQueryService.GetLowStockListAsync(…) ← new screen
  Ports/
    IFabricQueryService

Frontline.Infrastructure
  SqlFabricQueryService  implements IFabricQueryService
    - calls weblb_Fabric_GetMobileCard (3 result sets via NextResult())
    - maps nullable columns with DBNull guards
    - low-stock list: stub until new proc exists (see R-6)

Frontline.Api
  FabricAvailabilityController
    GET /api/frontline/fabric/{code}?width=&lowThreshold=  → FabricCardDto
    GET /api/frontline/fabric/low-stock?{params}           → PagedResult<FabricLowStockDto>  (stub initially)

Frontline.WebUI
  Pages/
    FabricLookup.razor    (mobile search + result)
    FabricLowStock.razor  (desktop low-stock list)
  Components/
    FabricSearchBar.razor
    FabricResultCard.razor
    WidthSelector.razor
    StatusBlock.razor
    AlternativesStrip.razor
    FabricLowStockGrid.razor
  wwwroot/css/
    fabric.css            (mobile status-block, width chips, alternatives strip, desktop table)
```

---

## Part 3 — Sales Orders Migration — Phased Checklist

### Milestone S-0 — Design shell (no backend)

*Goal: `Sales.WebUI` runs locally, shows the full Orders screen with sample data hardcoded in Razor. **No dependency on `Sales.Contracts`** — use `private record` local view models inside WebUI components.*

- [ ] **S-0.1** Create `wwwroot/css/tokens.css` in `Sales.WebUI` — paste token block verbatim from codex §1.
- [ ] **S-0.2** Create `wwwroot/css/orders.css` — layout, `.lk-table`, `.chip` grammar, `.orders-list`, `.panel.details`, `.mobile-cards`, `.amounts-grid`, `.emp-avatar`.
- [ ] **S-0.3** Update `_Layout.cshtml` — link both CSS files; add test-strip div; dark topbar.
- [ ] **S-0.4** `Index.razor` keeps `@page "/"` so the `/sales/` portal tile still works. Either host the full Orders layout directly in `Index.razor`, or make it a thin wrapper that renders `<OrdersPage />`. Do not delete or reroute the module root.
- [ ] **S-0.5** Implement the Orders list section using `MudDataGrid Dense="true" FixedHeader="true"` with **5–10 hardcoded local sample records** (a `private record OrderRow(...)` defined in the component). Apply all column definitions, chip rendering, debt tiering, flags column. No API call, no `Sales.Contracts` reference.
- [ ] **S-0.6** Below the grid: `OrderDetailPanel` — **list above, details below selected row**. Shows `OrderDetailHeader`, `OrderItemsTable` (group-rows + acc-rows), `OrderAmountsGrid` (6-card), `OrderEmployeesTable`. Same local sample records, no Contracts dep.
- [ ] **S-0.7** Wire row selection: clicking a grid row sets `SelectedOrderId`, drives the detail panel below. Teal inset on selected row.
- [ ] **S-0.8** Visual QA against `docs/ux/orders-static-preview.html` — typography, chip colors, amounts grid, mobile breakpoint.

**PR scope for S-0:** `Sales.WebUI` changes only. No backend projects touched. Reviewable as a pure UI diff.

---

### Milestone S-1 — Contracts + API stub (no SQL yet)

*Goal: WebUI talks to `Sales.Api` over HTTP; API returns the same sample data. Promote local sample record shapes into proper DTOs in `Sales.Contracts`.*

- [ ] **S-1.1** Add DTOs to `Sales.Contracts`: `OrderSummaryDto`, `OrderDetailsDto`, `OrderItemDto`, `OrderAccessoryDto`, `OrderEmployeeDto`, `OrdersQueryParams`, `PagedResult<T>`. Shapes come from S-0 local records — promote, don't invent.
- [ ] **S-1.2** Add `IOrdersQueryService` interface to `Sales.Application/Ports/`.
- [ ] **S-1.3** Add query/handler classes in `Sales.Application/Queries/`. **MediatR optional**: if MediatR is already wired in the module's DI registration, use `IRequest`/`IRequestHandler`; otherwise implement as plain application service methods on `IOrdersQueryService` called directly from the controller. Follow whichever pattern already exists in the scaffolded module.
- [ ] **S-1.4** Register a `StubOrdersQueryService` in `Sales.Infrastructure` (returns hardcoded sample data matching the S-0 records).
- [ ] **S-1.5** Add `OrdersController` to `Sales.Api` with `GET /api/sales/orders` and `GET /api/sales/orders/{id}` — dispatch via MediatR or direct service call per S-1.3 decision.
- [ ] **S-1.6** Update `Sales.WebUI` — replace local sample records with `HttpClient` calls to `Sales.Api`; keep same visual. Components now reference `Sales.Contracts` DTOs.

---

### Milestone S-2 — Infrastructure SQL gateway (real data)

*Goal: Replace stub with real legacy stored procedure calls.*

- [ ] **S-2.1** Add `ConnectionStrings:LKvitaiDb` (SQL Server) to `appsettings.Development.json` in `Sales.Api`.
- [ ] **S-2.2** Implement `SqlOrdersQueryService` in `Sales.Infrastructure`:
  - Open `SqlConnection(LKvitaiDb)`.
  - `GetOrdersAsync`: call `dbo.weblb_Orders` (or new paging wrapper — see R-1); map all columns including index 11 `ProductsSearch`; apply search/filter/page **at application layer** if proc returns full set.
  - `GetOrderDetailsAsync`: sequential `SqlCommand` for `weblb_Order` → `weblb_Accessories` → `weblb_Items` → `weblb_Employees` on shared `@OrderId`; handle all DBNull columns.
- [ ] **S-2.3** Replace `StubOrdersQueryService` registration with `SqlOrdersQueryService`.
- [ ] **S-2.4** Wire server-side search: `OrdersQueryParams.Search` filters across `Number`, `Customer`, `Address`, `ProductsSearch` in the returned set.
- [ ] **S-2.5** Wire `MudDataGrid ServerData` in `OrdersGrid.razor` to call API with page/sort/filter params; `debounce ~200ms` on search field.
- [ ] **S-2.6** Smoke test: orders list loads, detail loads, search filters, pagination works.

---

### Milestone S-3 — Polish and auth hardening

- [ ] **S-3.1** Wire Portal cookie auth on `Sales.Api` (inheriting `PortalAuth` building block pattern from `Sales.WebUI` → `Sales.Api`).
- [ ] **S-3.2** Confirm/implement "Open in PoS" action — see R-5.
- [ ] **S-3.3** Implement keyboard shortcuts: `/` → focus search, `↑/↓` selection, `Enter` open, `Esc` clear.
- [ ] **S-3.4** Implement empty/error/loading states per codex §7.
- [ ] **S-3.5** Export action stub (placeholder button, no impl).

---

## Part 4 — Fabric Availability Migration — Phased Checklist

### Milestone F-0 — Mobile lookup shell (no backend)

*Goal: `Frontline.WebUI` shows the mobile fabric lookup and desktop low-stock list with sample data hardcoded in Razor. **No dependency on `Frontline.Contracts`** — use `private record` local view models inside components.*

- [ ] **F-0.1** Copy `tokens.css` from `Sales.WebUI/wwwroot/css/` into `Frontline.WebUI/wwwroot/css/` for now (file duplication is acceptable at this stage; track as tech debt for future shared web asset consolidation — do not use a symlink, which is unreliable on Windows/CI). Create `fabric.css` — status-block classes, width chips, incoming chip, alternatives strip, recent checks.
- [ ] **F-0.2** Create `Pages/FabricLookup.razor` (`@page "/fabric"` or similar) — dark header with `FA` mark, search bar, empty state, recent-checks list. Hardcoded local sample records for recent checks. No `Frontline.Contracts` reference.
- [ ] **F-0.3** Implement `FabricResultCard.razor` — photo hero, campaign ribbon (no bright yellow), name/code, `WidthSelector` chips with quantity pills (ok/low/out coloring), `StatusBlock` markup (`status-block--ok/low/out/disc`), notes (sand left-border), incoming chip, alternatives horizontal scroll. All driven by local sample `private record WidthRow(...)` etc.
- [ ] **F-0.4** Wire width selector to switch displayed width row from hardcoded local sample array.
- [ ] **F-0.5** Visual QA against `docs/ux/fabric-availability-static-preview.html` — mobile panels.

- [ ] **F-0.6** Create `Pages/FabricLowStock.razor` — desktop low-stock list: metrics strip (4 KPI columns), toolbar (search + threshold highlight + status + width + supplier + refresh), **plain `MudDataGrid` or semantic HTML table over local sample records** (no `ServerData` yet — `ServerData` is wired in F-1/F-3 once contracts exist), all 11 columns (Photo, Code, Name, Width, Available+progress, Status, ETA, Incoming, Alternatives, LastChecked, Actions).
- [ ] **F-0.7** Visual QA against desktop panel in static preview.

**PR scope for F-0:** `Frontline.WebUI` only.

---

### Milestone F-1 — Contracts + API stub

> **Auth note:** stub endpoints in `Frontline.Api` may be `[AllowAnonymous]` **in dev only**. Before any non-local deploy, Portal auth must be enabled (see F-2.4 and R-10). Do not merge to a shared environment with open endpoints.

- [ ] **F-1.1** Add DTOs to `Frontline.Contracts`: `FabricCardDto`, `WidthStockDto`, `FabricAlternativeDto`, `FabricLowStockDto` (shape TBD — see R-6/R-7), `FabricLookupParams`. Promote from F-0 local records.
- [ ] **F-1.2** Add `IFabricQueryService` interface in `Frontline.Application/Ports/`.
- [ ] **F-1.3** Add query/handler classes in `Frontline.Application/Queries/`. **MediatR optional** — same rule as S-1.3: follow whichever pattern is already wired in the scaffolded module.
- [ ] **F-1.4** `StubFabricQueryService` in `Frontline.Infrastructure` — returns hardcoded card + empty low-stock list.
- [ ] **F-1.5** `FabricAvailabilityController` in `Frontline.Api` — `GET /api/frontline/fabric/{code}` + `GET /api/frontline/fabric/low-stock`. Mark `[AllowAnonymous]` in dev; add TODO comment for auth gate.
- [ ] **F-1.6** Update `Frontline.WebUI` — replace local sample records with `HttpClient` calls; switch `MudDataGrid` to `ServerData`. Components now reference `Frontline.Contracts` DTOs.

---

### Milestone F-2 — Infrastructure SQL gateway (mobile lookup real data)

- [ ] **F-2.1** Add `ConnectionStrings:LKvitaiDb` to `Frontline.Api` `appsettings.Development.json`.
- [ ] **F-2.2** Implement `SqlFabricQueryService.GetMobileCardAsync`:
  - `CommandType.StoredProcedure`, `@Code`, `@DefaultPhotoUrl`, `@LowThreshold=10`, `@EnoughThreshold=25`.
  - Read 3 result sets via `NextResult()`.
  - Map nullable columns (`DiscountPercent`, `Notes`, `Name`, `StockMeters`, `ExpectedDate`) with DBNull guards.
  - Map `Status int` to `FabricAvailabilityStatus` enum.
- [ ] **F-2.3** Replace stub; smoke test mobile lookup.
- [ ] **F-2.4** Wire `Frontline.Api` auth — add `PortalAuth` building block (parity with Sales; frontline currently has no auth).

---

### Milestone F-3 — Desktop low-stock list (new screen)

- [ ] **F-3.1** Determine data source — `weblb_Fabric_GetMobileCard` covers single-fabric lookup only. Desktop list needs either:
  - (a) a new SQL Server proc `weblb_Fabric_GetLowStockList(@ThresholdMeters, @Status, @WidthMm, @Supplier, @Page, @PageSize)`, or
  - (b) a query against a different table/view that returns all fabrics with stock metrics.
  - **Decision required from product/DB team before implementation.**
- [ ] **F-3.2** Once proc shape is confirmed, add `FabricLowStockDto` with: code, name, thumbnail, width, available meters, threshold, status, ETA, incoming meters, supplier, last checked, action flags.
- [ ] **F-3.3** Implement `SqlFabricQueryService.GetLowStockListAsync` with server-side search/filter/sort/page.
- [ ] **F-3.4** Wire `FabricLowStockGrid` `ServerData` to real API.
- [ ] **F-3.5** Implement 4 KPI metrics in `FabricLowStock.razor` — query summary counts from API or derive from first result page.

---

## Part 5 — Risks & Open Questions

| ID | Area | Risk / Question |
|----|------|-----------------|
| **R-1** | Orders | `dbo.weblb_Orders` returns the **full table** — no server-side paging/search params. Interim: fetch all + filter/page in `SqlOrdersQueryService`. Long-term: request a new proc or wrapper with `@Search`, `@Page`, `@PageSize`. |
| **R-2** | Orders | `Api/OrdersController.Get()` in legacy does **not set `CommandType.StoredProcedure`** — likely executes as a T-SQL text batch. Verify in SQL Server Profiler before cutting over. |
| **R-3** | Both | **SQL Server connection string** (`LKvitaiDb`) must be provisioned in new MES app settings. The MES currently only has PostgreSQL configured (Warehouse module). Do not mix; register a named `SqlClient` factory in Sales/Frontline DI. |
| **R-4** | Both | **Auth propagation**: Legacy has no auth. New Sales.Api must enforce Portal cookie auth. If the SQL procs use `CURRENT_USER` or row-level security, the DB user running `SqlCommand` must be confirmed. |
| **R-5** | Orders | **"Open in PoS"** appears in the codex detail actions. It's unclear what the PoS target URL is. Confirm before S-3. |
| **R-6** | Fabric | **Desktop low-stock list** has no legacy proc. `weblb_Fabric_GetMobileCard` is single-code only. A new proc (or table scan + paging query) must be written or commissioned. Do not block F-0/F-1/F-2 on this — implement F-3 separately once proc shape is decided. |
| **R-7** | Fabric | **Incoming meters / Supplier / Last checked** fields are required by the codex desktop spec but absent from `weblb_Fabric_GetMobileCard` result sets. Confirm these columns exist elsewhere (fabric master table?) before designing `FabricLowStockDto`. |
| **R-8** | Orders | **ProductsSearch (column 11)** in `weblb_Orders` is a hidden server-side text field for searching by product. The codex §7 requires searching across it. As long as the full result set is returned, the new infrastructure layer can filter on this field in memory for now. |
| **R-9** | Performance | `MudDataGrid` with `ServerData` is fine if the API is paged. Risk only appears if app-layer paging on a large orders table is too slow — monitor and push paging to the proc if needed (R-1 path). |
| **R-10** | Frontline | `Frontline.Api` currently has **no auth** by design (scaffold comment). Adding Portal auth before F-2 is required before ship but must be coordinated — frontline devices (scanners) may need a different auth flow than cookie-based Portal. |

---

## Recommended First PR Scope

**PR #1 — `S-0`: Sales.WebUI design shell**
- Files: `Sales.WebUI` only (tokens.css, orders.css, Orders.razor, all component files, _Layout update)
- No backend changes, no new projects
- Reviewer can open the Blazor app and see the full Orders screen with sample rows
- Validates the entire design/token/component system before any plumbing starts

**PR #2 — `F-0`: Frontline.WebUI design shell**
- Files: `Frontline.WebUI` only (fabric.css, FabricLookup.razor, FabricLowStock.razor, components)
- Same no-backend principle

After both design shells are approved, S-1/F-1 (contracts + stubs) and S-2/F-2 (real SQL) can be executed as a single PR each since they are backend-only and the UI is already locked.