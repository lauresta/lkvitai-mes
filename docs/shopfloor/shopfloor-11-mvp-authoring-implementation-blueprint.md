# LKvitai.MES - Shopfloor Module - MVP Authoring Implementation Blueprint

**Module:** Shopfloor (ShopfloorPilot)  
**Version:** 0.1 - MVP authoring implementation blueprint  
**Date:** 2026-06-16  
**Status:** Draft - handoff to implementation agent  
**Parent:** [`shopfloor-10-mvp-authoring-scope.md`](./shopfloor-10-mvp-authoring-scope.md)

> This blueprint is for an implementation agent working in this repository.
> It implements the strict authoring slice only: production families, reference
> data, legacy product-type sync, mapping coverage, and workflow graph authoring.

---

## 1. Summary

Implement `docs/shopfloor/shopfloor-10-mvp-authoring-scope.md` as the strict
authoring slice.

Confirmed decisions:

- Legacy product types are **SQL only**.
- The existing workflow editor remains an **iframe bridge**.
- Scope stays **strict AUTH**: no TaskTypes table, formulas, materials, runtime
  orders, queues, stock integration, Marten, sagas, or projections.

Current repo state:

- `src/Modules/Shopfloor/LKvitai.MES.Modules.Shopfloor.WebUI` exists.
- The rest of the Shopfloor backend stack does not exist yet.
- The workflow editor is a static prototype embedded by `WorkflowEditor.razor`.

---

## 2. Backend Shape

Add projects under `src/Modules/Shopfloor` and include them in
`src/LKvitai.MES.sln`:

- `LKvitai.MES.Modules.Shopfloor.Contracts`
- `LKvitai.MES.Modules.Shopfloor.Domain`
- `LKvitai.MES.Modules.Shopfloor.Application`
- `LKvitai.MES.Modules.Shopfloor.Infrastructure`
- `LKvitai.MES.Modules.Shopfloor.Api`

Project references:

- `Contracts`: zero dependencies.
- `Domain`: `SharedKernel` only.
- `Application`: `Domain`, `Contracts`, `Cqrs.Abstractions`, `SharedKernel`,
  `FluentValidation`. Use `BuildingBlocks.Cqrs.Abstractions`
  (`ICommand`/`IQuery`/`ICommandHandler`/`IQueryHandler`), like `Sales`/`Frontline`.
  **Note:** `Cqrs.Abstractions` already pulls in the `MediatR` package and its interfaces
  derive from `IRequest`/`IRequestHandler` — so MediatR *types* are present transitively.
  Do **not** add a direct `MediatR` package reference or wire the MediatR pipeline unless
  you intentionally want the dispatcher; for this CRUD slice, inject and call the
  handlers/application services directly (no behaviors pipeline needed).
- `Infrastructure`: `Application`, `Domain`, `Contracts`, EF Core PostgreSQL,
  `Microsoft.Data.SqlClient`.
- `Api`: `Application`, `Contracts`, `Infrastructure`, `ModuleStartup`,
  `PortalAuth`.
- `WebUI`: add a reference to `Contracts` only.

Add or extend architecture tests so Shopfloor follows these rules:

- Contracts has no project/package dependencies.
- Domain does not reference Infrastructure, EF Core, Marten, or MassTransit.
- Application does not reference Infrastructure, Api, WebUI, EF Core, Marten, or
  MassTransit.
- Infrastructure does not reference Api or WebUI.
- WebUI references only same-module Contracts and building blocks.
- No cross-module references.

---

## 3. Data Model

Use EF Core with PostgreSQL schema `shopfloor`.

Tables:

### `legacy_product_types`

- `code` text primary key
- `kind_name` text required
- `name` text required
- `last_synced_at` timestamp with time zone required
- `removed_at` timestamp with time zone nullable

### `workflow_templates`

- `id` uuid primary key
- `code` text required unique
- `name` text required
- `description` text nullable
- `status` text required, values: `Draft`, `Published` (`Archived` reserved — persisted
  shape allows it, but the MVP UI exposes only Draft/Published)
- `graph_json` jsonb required — on create, seed a valid minimal flow:
  `{ "nodes": [start, finish], "edges": [{ "from": "<startId>", "to": "<finishId>" }] }`
  (one start + one finish joined by an edge; passes §7 so a new draft is immediately valid)
- `created_at` timestamp with time zone required
- `updated_at` timestamp with time zone nullable

### `product_type_workflow_maps`

- `legacy_product_type_code` text primary key, FK to `legacy_product_types.code`
- `workflow_template_id` uuid required, FK to `workflow_templates.id`
- `created_at` timestamp with time zone required
- `updated_at` timestamp with time zone nullable

Keep this as a separate table so legacy re-sync never drops mappings.

### `work_centers`

- `id` uuid primary key
- `code` text required unique
- `name` text required

### `work_stations`

- `id` uuid primary key
- `code` text required unique
- `name` text required
- `work_center_id` uuid required, FK to `work_centers.id`
- `wip_limit` integer nullable
- `is_active` boolean required

Create an initial EF migration for these tables.

---

## 4. Legacy Product-Type Sync

Use this SQL query as the read-only source:

```sql
select
    t.TipoID,
    zr.RusiesPavadinimas,
    t.TipoTrPavadinimas
from dbo.Zinynas_tipai t
join dbo.Zinynas_rusys zr on zr.RusiesID = t.Rusis
where zr.Naudojamas <> 0
  and t.Naudojamas <> 0
  and t.Gamininamas <> 0
  and t.TipoID <> 504437610
```

Column mapping:

- `legacy_product_types.code` = `TipoID.ToString(CultureInfo.InvariantCulture)`
- `legacy_product_types.kind_name` = `RusiesPavadinimas`
- `legacy_product_types.name` = `TipoTrPavadinimas`

Configuration:

- Require `ConnectionStrings:LKvitaiDb`.
- Add `Shopfloor:LegacyProductTypes:DataSource = "Sql"`.
- Add `Shopfloor:LegacyProductTypes:CommandTimeoutSeconds`, default `30`.

Sync behavior:

- Query legacy SQL Server with `Microsoft.Data.SqlClient`.
- Upsert returned rows.
- Set `last_synced_at` to the sync timestamp.
- Clear `removed_at` for returned rows.
- Mark cached rows absent from the latest result with `removed_at = sync timestamp`.
- Never delete `legacy_product_types`.
- Never delete or rewrite `product_type_workflow_maps` except through mapping APIs.
- Fail startup/configuration clearly if SQL config is missing or invalid.

---

## 5. Contracts

Add DTOs in `Shopfloor.Contracts`:

- `WorkflowStatus`
- `WorkflowTemplateSummaryDto`
- `WorkflowTemplateDto`
- `WorkflowGraphDto`
- `WorkflowNodeDto`
- `WorkflowEdgeDto`
- `WorkCenterDto`
- `WorkStationDto`
- `LegacyProductTypeDto` — includes the **per-row mapping** so the list can render the
  family chip without a second call: `Code`, `KindName`, `Name`, `Removed`,
  `MappedWorkflowTemplateId` (nullable), `MappedWorkflowCode` (nullable),
  `MappedWorkflowName` (nullable). The `GET /legacy-product-types` query LEFT-JOINs
  `product_type_workflow_maps` + `workflow_templates` to populate these.
- `ProductTypeMappingDto`
- `CoverageSummaryDto`

Add request DTOs for:

- create/update work center
- create/update work station
- create/update workflow template
- save workflow graph
- clone workflow template
- bulk assign legacy product types to one workflow template

Graph contract:

```csharp
public sealed record WorkflowGraphDto(
    IReadOnlyList<WorkflowNodeDto> Nodes,
    IReadOnlyList<WorkflowEdgeDto> Edges);

public sealed record WorkflowNodeDto(
    string Id,
    string Kind,
    string Name,
    WorkflowNodePositionDto Position,
    Guid? WorkStationId,
    int? DurationSec,
    string? TaskTypeCode);

public sealed record WorkflowEdgeDto(string From, string To);

public sealed record WorkflowNodePositionDto(decimal X, decimal Y);
```

Allowed `Kind` values:

- `start`
- `task`
- `finish`

Only `task` nodes use `WorkStationId`, `DurationSec`, and optional
`TaskTypeCode`.

---

## 6. API

Create `Shopfloor.Api` using `UseScaffoldSerilog("shopfloor-api")` and
`AddScaffoldApiCore()`.

Protect the API with the same Portal auth pattern used by Sales:

- Portal cookie.
- Portal structured bearer.
- default policy requires authenticated user.

Map routes under `/api/shopfloor`.

### Work Centers

- `GET /api/shopfloor/work-centers`
- `GET /api/shopfloor/work-centers/{id}`
- `POST /api/shopfloor/work-centers`
- `PUT /api/shopfloor/work-centers/{id}`
- `DELETE /api/shopfloor/work-centers/{id}`

### Work Stations

- `GET /api/shopfloor/work-stations`
- `GET /api/shopfloor/work-stations/{id}`
- `POST /api/shopfloor/work-stations`
- `PUT /api/shopfloor/work-stations/{id}`
- `DELETE /api/shopfloor/work-stations/{id}`

### Workflows / Production Families

- `GET /api/shopfloor/workflows`
- `GET /api/shopfloor/workflows/{id}`
- `POST /api/shopfloor/workflows` — body is `code` + `name` + optional `description`; the
  server **generates the default graph** (`{ nodes:[start, finish], edges:[start→finish] }`),
  status `Draft`. The client does not send a graph on create.
- `PUT /api/shopfloor/workflows/{id}`
- `DELETE /api/shopfloor/workflows/{id}`
- `PUT /api/shopfloor/workflows/{id}/graph` — saves a (possibly in-progress) draft;
  **lenient** checks only: valid JSON, known node kinds, edge endpoints exist, no cycles.
- `POST /api/shopfloor/workflows/{id}/publish` — runs the **full** §7 validation; rejects
  with the error list if not publish-ready.
- `POST /api/shopfloor/workflows/{id}/clone`

### Legacy Product Types

- `GET /api/shopfloor/legacy-product-types`
- `POST /api/shopfloor/legacy-product-types/sync`

Filters for list endpoint:

- `search`
- `mapped` nullable bool
- `removed` nullable bool, default false
- `page`
- `pageSize`

### Product Type Mappings

- `GET /api/shopfloor/product-type-mappings/coverage`
- `POST /api/shopfloor/product-type-mappings/bulk-assign`
- `DELETE /api/shopfloor/product-type-mappings/{legacyCode}`

Response behavior:

- `404` for missing IDs.
- `409 Conflict` when deleting referenced work centers, work stations, or
  workflows.
- `400 Bad Request` for validation errors.

---

## 7. Application Rules

Workflow graph validation has two tiers.

**Lenient (on `PUT …/graph` save — lets you save work-in-progress):**

- valid JSON; node `kind` ∈ {start, task, finish}
- all edge endpoints reference existing nodes
- no duplicate edge pairs
- no cycles

**Full (on `POST …/publish` and the editor's Preview — publish-readiness):**

- everything above, plus:
- exactly one `start`
- exactly one `finish` — **deliberate**: parallel branches must converge into a single
  finish node (matches the prototype); a flow with multiple finishes is rejected
- every `task` node has `workStationId`
- every `task` node has `durationSec > 0`
- every `task` node is reachable from `start`
- `finish` is reachable from `start`
- no orphan task: each task must be part of a path from start toward finish

Workflow behavior:

- New workflow defaults to `Draft`.
- `Published` workflows can still be edited in MVP unless the UI explicitly
  warns; no versioning in this slice.
- Clone creates a new `Draft` workflow with copied `graph_json`.
- Clone does not copy legacy product mappings.

Mapping behavior:

- Bulk assign overwrites any existing mapping for the selected legacy codes.
- Removed legacy product types remain visible only when `removed=true` is
  requested.
- Coverage counts exclude removed legacy product types by default.

Delete behavior:

- Work center delete fails if stations reference it.
- Work station delete fails if any workflow graph references it.
- Workflow delete fails if any legacy type maps to it.

---

## 8. WebUI

Add typed `ShopfloorApiClient` in WebUI using `Contracts`.

Add `ShopfloorApiAuthHandler`:

- subclass `PortalBearerForwardingHandler`
- attach it to the named `ShopfloorApi` HTTP client
- register `ShopfloorApiClient`

Add pages. **Important:** the WebUI runs under `PathBase=/shopfloor` (see
`docker-compose.test.yml`), so Razor `@page` routes are **relative** — do NOT prefix them
with `/shopfloor` or the external URL becomes `/shopfloor/shopfloor/...`. The existing
`WorkflowEditor.razor` is `@page "/workflow-editor"` (external `/shopfloor/workflow-editor`).

| External URL | Razor `@page` directive |
|---|---|
| `/shopfloor/ref/work-centers` | `@page "/ref/work-centers"` |
| `/shopfloor/ref/stations` | `@page "/ref/stations"` |
| `/shopfloor/workflows` | `@page "/workflows"` |
| `/shopfloor/map/product-types` | `@page "/map/product-types"` |

Update `Shared/NavMenu.razor`:

- Workflows group: Production Families, Workflow Editor
- Reference Data group: Work Centers, Work Stations
- Mappings group: Legacy Types & Coverage

UI framework & style:

- **All CRUD/list/mapping pages are Blazor Server + MudBlazor 6.20.0** (already referenced
  by `Shopfloor.WebUI`), per the design system. **Read `AGENTS.md` first** (mandatory for
  any Blazor/MudBlazor/CSS change); source of truth is `docs/ux/lkvitai-mes-ux-handoff.html`.
- **Exception — the workflow editor canvas** stays the existing framework-free HTML/JS
  prototype in an **iframe** (graph drag/link has no MudBlazor equivalent). Its host page,
  family picker, and toolbar may be MudBlazor; the canvas itself is custom. This is deliberate.
  The iframe authors only the strict AUTH graph (`WorkflowGraphDto`) — see the strict MVP
  field contract in Section 9; legacy runtime/formula fields are removed, not just hidden.
- Reuse `shopfloor.css`, `portal-tokens.css`, custom chips, dense tables.
- Keep compact enterprise styling. Do not add new color families.
- Do not use MudBlazor default status colors/chips as final UI.

### Work Centers page

- Dense table with code/name/actions.
- Inline create/edit form or compact side panel.
- Validate non-empty code/name.

### Work Stations page

- Dense table with code/name/work center/WIP/active/actions.
- Create/edit form includes center dropdown, WIP limit numeric input, active
  toggle.

### Production Families page

- Dense table with code, name, status, mapped legacy count, task count, updated
  timestamp, actions.
- Actions: Open Editor, Create, Clone, Publish, Preview, Delete.
- Create form for code/name/description.
- **Preview** is a client-side dry-run: validate the graph, then show the
  topologically-ordered task plan (`step → task → line → duration`). No backend run, no
  formulas/materials. Real production-order execution is out of this slice ([RUN]).

### Legacy Types & Coverage page

- Header counters: mapped N / total, unmapped, families.
- Toolbar: search, mapped/unmapped filter, sync button.
- Dense table: select checkbox, code, kind, name, mapped family chip.
- Bulk action: selected rows -> family dropdown -> Assign.
- Show unmapped list naturally through the filter.

---

## 9. Workflow Editor Bridge

Keep `WorkflowEditor.razor` hosting the existing iframe.

Parent page responsibilities:

- Load selected workflow by ID.
- Load active work stations.
- Send payload to iframe using `postMessage`.
- Receive save payload from iframe.
- Call `PUT /api/shopfloor/workflows/{id}/graph`.
- Show save errors from API validation.

Iframe responsibilities:

- Replace hardcoded `ROLLER_STD` with family code/name from payload.
- Load graph from payload instead of static demo state.
- Replace task inspector fields with the MVP fields (see strict contract below).
- Keep add task, delete task, drag, link, cycle prevention, auto layout, export
  JSON.
- Add primary Save action that posts the graph back to the parent.
- Add a **Validate** action in the toolbar that runs the smart validator client-side and
  opens the report panel (errors / warnings / hints + metrics), with click-to-highlight on
  the canvas, and a live count badge on the button. Rules, severity, and the
  `ValidationReport` contract are defined in
  [`shopfloor-12-smart-validation.md`](./shopfloor-12-smart-validation.md); its visual
  presentation is the paired design guide. The same engine re-runs on the server at Publish.

### Strict MVP field contract (AUTH graph only)

The editor authors **only** the strict AUTH graph contract defined in
`shopfloor-10` and serialized as `WorkflowGraphDto` (Section 5). It must not
introduce, persist, or export any field outside that shape.

**Task inspector — the only editable, persisted fields:**

- `name` — task name
- `description` — optional, nullable free text. Operator-facing notes shown on
  the task card and in the workflow preview; editable in the inspector and
  round-tripped through `graph_json`.
- `workStationId` — WorkStation / line dropdown
- `durationSec` — approximate duration in seconds
- `taskTypeCode` — optional, nullable. Kept in the contract but **rendered
  disabled/read-only** in the MVP (reserved for a future execution engine);
  existing values round-trip but cannot be edited. Any other field that is not
  yet used must likewise be disabled rather than left editable.

**WorkStation / line dropdown** is populated from the WorkStations reference API
(the `stations` array in the `shopfloor.workflow.load` payload, sourced from
`GET /api/shopfloor/work-stations?activeOnly=true`). It is not free text.

**WorkCenter is not editable in the editor.** It is derived from the selected
WorkStation and may only be **displayed read-only** (e.g. `workCenterName` shown
beside the station). It is never a second dropdown and is never saved into
`graph_json`.

**Remove, hide, or disable every non-MVP field.** Any prototype field that is
not part of `WorkflowGraphDto` must be removed or rendered read-only/disabled so
it can never reach `graph_json`. Specifically eliminate:

- Excel formulas / formula text and any formula evaluation;
- materials and `kitImpact`;
- label / label-template fields;
- `terminal` flag and `required` / `mandatory` flag;
- any runtime execution flags;
- batching / CONWIP metadata;
- any "PlanJson" or "runtime preview" controls that imply executable runtime
  behavior. (Production-order execution is out of this slice — `[RUN]`.)

**`Export JSON` is debug-only.** It may remain solely as a read-only view of the
persisted MVP graph shape (`WorkflowGraphDto`: nodes + edges). It must not emit
the old runtime/formula payload.

**Save payload must match `WorkflowGraphDto` exactly** — `nodes` + `edges`, with
each task node containing only `id`, `kind`, `name`, `position`, `workStationId`,
`durationSec`, optional `taskTypeCode`, and optional `description`.
`start`/`finish` nodes carry `id`, `kind`, `name`, `position` only. The iframe must never serialize non-MVP fields
into the Save or Export payload.

Message shapes:

```json
{
  "type": "shopfloor.workflow.load",
  "workflow": { "id": "...", "code": "...", "name": "...", "graph": { } },
  "stations": [{ "id": "...", "code": "...", "name": "...", "workCenterName": "..." }]
}
```

```json
{
  "type": "shopfloor.workflow.save",
  "workflowId": "...",
  "graph": { "nodes": [], "edges": [] }
}
```

```json
{
  "type": "shopfloor.workflow.save-result",
  "ok": true,
  "message": "Saved"
}
```

Primary persistence is Save; `Export JSON` is the debug-only view described in
the strict MVP field contract above.

---

## 10. CI And Deployment

Add `Shopfloor.Api/Dockerfile`.

Update `.github/workflows/shopfloor-ci.yml`:

- restore solution
- build Shopfloor Contracts, Domain, Application, Infrastructure, Api, WebUI
- run Shopfloor tests if added
- docker build Shopfloor Api
- docker build Shopfloor WebUI

Update `.github/workflows/build-and-push.yml`:

- add `shopfloor-api` image:
  `ghcr.io/${{ github.repository_owner }}/lkvitai-mes-shopfloor-api`

Update `docker-compose.test.yml`:

- add `shopfloor-api`
- set:
  - `ConnectionStrings__ShopfloorDb`
  - `ConnectionStrings__LKvitaiDb`
  - `Shopfloor__LegacyProductTypes__DataSource=Sql`
  - `PortalAuth__DataProtectionKeysPath=/app/auth-keys`
- update `shopfloor-webui`:
  - `ShopfloorApi__BaseUrl=http://lkvitai-mes-shopfloor-api:8080`
  - remove direct database diagnostic dependency unless still needed for the
    dashboard
  - depend on `shopfloor-api`

Update deployment health checks:

- include `lkvitai-mes-shopfloor-api`

Update debug logs workflow:

- `shopfloor` service includes both `lkvitai-mes-shopfloor-api` and
  `lkvitai-mes-shopfloor-webui`
- Serilog patterns include `shopfloor-api-` and `shopfloor-webui-`

---

## 11. Tests

Add tests under `tests/Modules/Shopfloor`.

Recommended projects:

- `LKvitai.MES.Tests.Shopfloor.Unit`
- `LKvitai.MES.Tests.Shopfloor.Integration` if time permits

Unit tests:

- graph validator accepts a valid parallel flow
- graph validator rejects missing start
- graph validator rejects missing finish
- graph validator rejects more than one start/finish
- graph validator rejects cycles
- graph validator rejects unknown edge endpoint
- graph validator rejects task without station
- graph validator rejects task without positive duration
- graph validator rejects unreachable finish
- graph validator rejects orphan task
- clone copies graph but not mappings
- coverage counts mapped/unmapped/family totals correctly

Infrastructure tests:

- SQL sync maps `TipoID`, `RusiesPavadinimas`, `TipoTrPavadinimas` correctly
- sync upserts changed names
- sync marks missing rows removed
- sync preserves existing mappings
- missing `ConnectionStrings:LKvitaiDb` fails clearly

API/application tests:

- create/update/delete work center
- create/update/delete work station
- delete center with stations returns conflict
- create workflow and save graph
- invalid graph returns validation error
- bulk assign overwrites existing mappings
- workflow delete with mappings returns conflict

Build checks:

```bash
dotnet build src/LKvitai.MES.sln -c Release
dotnet test tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/LKvitai.MES.ArchitectureTests.csproj -c Release
```

Run Shopfloor test projects once created.

---

## 12. Acceptance Smoke

Manual smoke flow:

1. Start Shopfloor.Api and Shopfloor.WebUI.
2. Trigger legacy product-type sync.
3. Verify rows from `dbo.Zinynas_tipai` / `dbo.Zinynas_rusys` appear in Legacy
   Types.
4. Create a work center.
5. Create a work station under that center.
6. Create a Production Family.
7. Bulk-map several legacy product types to the family.
8. Open the family in Workflow Editor.
9. Add/edit tasks with station and duration.
10. Save graph.
11. Reopen editor and verify graph persists.
12. Clone the family and verify copied graph with no copied mappings.
13. Verify coverage shows mapped count, total count, family count, and unmapped
    list.

MVP is complete when all acceptance criteria in
`shopfloor-10-mvp-authoring-scope.md` are satisfied.
