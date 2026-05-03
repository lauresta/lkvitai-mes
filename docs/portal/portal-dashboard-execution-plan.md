# Portal Dashboard Execution Plan

This document splits the Portal dashboard work into four implementation issues. The goal is that a maintainer can choose a GitHub issue by number and ask another AI agent to implement that exact slice without rediscovering the product decisions.

## Current State To Read First

Read these files before implementing any issue:

- `src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/wwwroot/index.html` - current static Portal page.
- `src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/wwwroot/script.js` - current mock modules, mock operations data, branch percentages, environment detection, search filtering and news.
- `src/Modules/Portal/LKvitai.MES.Modules.Portal.WebUI/Program.cs` - Portal auth flow, Warehouse redirect, logout mapping, path base and HTTP clients.
- `src/Modules/Portal/LKvitai.MES.Modules.Portal.Api/Program.cs` - current minimal `/api/portal/v1/status` endpoint.
- `src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/Components/PortalModuleShell.razor` - shared shell/header starting point.
- `src/BuildingBlocks/LKvitai.MES.BuildingBlocks.WebUI/wwwroot/css/portal-shell.css` - shared shell styling.
- `src/Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/Sql/SalesOrderStatusMap.cs` - existing Sales mapping for localized order statuses, including `SKIRTINGAI`.
- `src/Modules/Sales/LKvitai.MES.Modules.Sales.Contracts/Orders/OrderStatusCodes.cs` - stable semantic Sales status codes.
- `src/Modules/Sales/LKvitai.MES.Modules.Sales.Api/Endpoints/OrdersEndpoints.cs` - current Sales API endpoint style and auth expectations.
- `docs/ux/sales-orders-codex-rules.md` - Sales UX/status chip rules referenced by the Sales status code docs.

## Product Decisions

- The Portal header should be shared through `BuildingBlocks.WebUI`, not hand-coded separately in the Portal page and each module.
- Logo, `LKvitai.MES`, and the slogan must be one clickable brand link back to the Portal home so users can return from Warehouse, Sales, Frontline, and future modules.
- Global search should behave like an Outlook 365-style universal search entry point. For now it is a placeholder, not real search. It should open a lightweight placeholder state instead of filtering only the module grid.
- Version must not be hardcoded in UI. It should come from release/container metadata such as `APP_VERSION`, `RELEASE_TAG`, `GIT_SHA`, and `BUILD_DATE`, exposed by the Portal API/status model.
- User display data must come from auth/session claims: display name, initials/avatar, roles/permissions and logout behavior. Future migration from internal auth to Authentic must be accounted for, but not implemented in this plan.
- Portal news can come from GitHub Releases, but the browser should not call GitHub directly. Use a Portal API proxy/cache.
- The top hero "Open ..." buttons should eventually become the last three user-opened modules. Store recent module keys locally in cookie/localStorage and validate/render them against the module list from API/config.
- `SKIRTINGAI` means `Mixed`: some order lines are already produced and some are not. It belongs between `In production` and `Produced`, not in `Other`.
- Branches on track should stay simple visually as percentages, but the percentage must have a clear business meaning: of items ready for customer handoff, how many were actually issued to the customer.
- All normal branches receive deliveries on Thursday. Klaipeda is special: it does not use the normal branch-delivery step and normally goes from `Pagamintas` directly to `Išduotas klientui`.

## Issue 1 - Shared Portal Header And Auth-Aware Shell

Implement the shared header/shell from `BuildingBlocks.WebUI` and replace the manually duplicated header behavior.

Scope:

- Update `PortalModuleShell.razor` so the brand block is a single link to the Portal home URL.
- Portal home URL should come from configuration, for example `PortalWebUi:BaseUrl`, with a safe local fallback.
- Keep logo, `LKvitai.MES`, and slogan inside the same clickable link.
- Make the shell accept user data from `ClaimsPrincipal` and avoid static names such as `Lukas`, `RM`, or `Signed in` except as fallback.
- Keep logout as shared/auth-aware behavior. Do not break the current `auth/logout` flow in `Portal.WebUI/Program.cs`.
- Add a global search placeholder in the shell. It should feel like a universal search entry point, not just module filtering. Real search is out of scope.
- Keep environment and version display slots, but prepare them to be fed by runtime/API data instead of hardcoded constants.
- Add comments or small seams only where future Authentic integration needs to plug in. Do not implement Authentic migration yet.

Acceptance:

- Header brand in Portal and module shells links back to Portal home.
- Header can render user name and initials from auth claims.
- Logout still posts to the configured logout endpoint.
- Search placeholder does not pretend to be full search; it opens/shows a simple "search coming soon" state.
- No hardcoded Portal-only header copy remains duplicated if the shared shell can own it.

## Issue 2 - Portal Runtime Data Sources

Replace Portal dashboard hardcoded/static data sources with API/config-backed sources where appropriate.

Scope:

- Extend `src/Modules/Portal/LKvitai.MES.Modules.Portal.Api/Program.cs` beyond the current minimal status endpoint.
- Add a status/build metadata response that includes at least module, status, version, release tag, git SHA, build date, environment/channel where available.
- Read build values from environment/config, for example `APP_VERSION`, `RELEASE_TAG`, `GIT_SHA`, `BUILD_DATE`.
- Provide a modules endpoint or config-backed model for module cards: key, title, category, description, status, URL, planned quarter, and permission requirements if available.
- Move current `modules` array out of `script.js` hardcode once the endpoint/config is available.
- Add a news endpoint that can later proxy/cache GitHub Releases. Initial implementation may use config/stub data if GitHub access is not ready, but shape it around release title, tag, date, excerpt/body, URL.
- Recent "Open ..." buttons should be local-only state: store the last three opened module keys in cookie/localStorage and render them after validating against the module list returned by API/config.

Acceptance:

- Portal UI no longer hardcodes version in multiple places.
- Module cards are rendered from API/config data, not a JS constant.
- News cards are rendered from API/config/release-shaped data, not a JS constant.
- Last-three module shortcuts are local recents, sorted by most recent access, and never show a module key that is not present in the current module list.

## Issue 3 - Operations Summary API

Create a Portal operations summary API/procedure for the `This month` and `Last month` selector.

Scope:

- Find the authoritative orders data source and current status fields used by Sales. Start with `SalesOrderStatusMap.cs`, `OrderStatusCodes.cs`, `IOrdersQueryService`, and the SQL adapter under `src/Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/Sql/`.
- Reuse or align with the existing Sales localized status mapping where possible.
- Add Portal-facing grouped stages:
  - `New / Intake`: `Įvestas į programą`, `Nepriskirtas`
  - `Queued`: `Eilėje`, `Patvirtintas gamybai`
  - `In production`: `Gaminamas`
  - `Mixed`: `SKIRTINGAI`
  - `Blocked`: `Trūksta audinio`, `Trūksta detalių`, `Sustabdytas`, `Garantinis remontas`
  - `Produced`: `Pagamintas`
  - `In branch / delivery`: `Išsiųstas į filialą`
  - `Completed`: `Išduotas klientui`
- Treat `Įvestas į programą` as the lifecycle start and `Išduotas klientui` as the lifecycle final status.
- Add an endpoint such as `GET /api/portal/v1/operations-summary?period=this|last`, or a two-month response if that fits the UI better.
- Return grouped stage counts, raw status counts, created-by-day and completed-by-day arrays.
- Preserve the `This month` and `Last month` UI behavior from the existing static selector.

Suggested response shape:

```json
{
  "period": { "key": "this", "from": "2026-05-01", "to": "2026-05-31" },
  "stages": [
    { "key": "intake", "label": "New / Intake", "count": 248 },
    { "key": "queued", "label": "Queued", "count": 140 },
    { "key": "production", "label": "In production", "count": 480 },
    { "key": "mixed", "label": "Mixed", "count": 22 },
    { "key": "blocked", "label": "Blocked", "count": 37 },
    { "key": "produced", "label": "Produced", "count": 150 },
    { "key": "branch", "label": "In branch / delivery", "count": 56 },
    { "key": "completed", "label": "Completed", "count": 150 }
  ],
  "statuses": [
    { "status": "Gaminamas", "count": 480 },
    { "status": "SKIRTINGAI", "count": 22 }
  ],
  "createdByDay": [
    { "date": "2026-05-01", "count": 8 }
  ],
  "completedByDay": [
    { "date": "2026-05-01", "count": 5 }
  ]
}
```

Acceptance:

- Operations preview stages are real API data, not `script.js` mock data.
- `SKIRTINGAI` is represented as `Mixed`.
- Raw localized statuses are still visible in the API response for debugging and future UI drill-down.
- The daily chart can render both this month and last month from API data.

## Issue 4 - Branches On Track Metric

Replace mock branch percentages with a simple real metric: customer handoff after readiness.

Definition:

```text
onTrackPercent = issued / readyForCustomer * 100
```

For normal branches:

- `readyForCustomer` is based on `Išsiųstas į filialą`.
- `issued` is based on `Išduotas klientui`.
- All normal branches receive deliveries on Thursday. The first version does not need a complex Thursday cutoff UI, but the API should not make Klaipeda follow the same rule.

For Klaipeda:

- `readyForCustomer` is based on `Pagamintas`.
- `issued` is based on `Išduotas klientui`.
- Klaipeda normally goes `Pagamintas` directly to `Išduotas klientui`, so `Išsiųstas į filialą` should not be required for the metric.

Scope:

- Add branch-level summary data to the Portal operations summary response or a sibling endpoint.
- Keep the UI light: show the percentage and a small text line such as `47 issued / 50 ready`.
- Add `readyBasis` to the API response so the UI/debug view can explain what the percentage means.
- Use warning styling for low percentages, for example below 90%, matching the existing simple branch visual language.

Suggested response shape:

```json
{
  "branchesOnTrack": [
    {
      "branch": "Vilnius",
      "readyBasis": "Išsiųstas į filialą",
      "ready": 50,
      "issued": 47,
      "onTrackPercent": 94
    },
    {
      "branch": "Klaipėda",
      "readyBasis": "Pagamintas",
      "ready": 40,
      "issued": 38,
      "onTrackPercent": 95
    }
  ]
}
```

Acceptance:

- The existing branch percentage UI remains visually simple.
- Percentages are computed from real order data.
- Klaipeda uses `Pagamintas` as readiness basis.
- Other branches use `Išsiųstas į filialą` as readiness basis.
- Each branch shows both percent and `issued / ready` counts so the number is explainable.
