# LKvitai.MES — Shopfloor Module · Solution & Component Architecture

**Module:** Shopfloor (ShopfloorPilot)
**Version:** 0.1 — Component architecture baseline
**Date:** 2026-06-15
**Status:** Draft — basis for implementation blueprints
**Parent:** [`shopfloor-00-architecture.md`](./shopfloor-00-architecture.md)

> This document details **the build**: which .NET projects/assemblies exist, what
> namespaces and classes live in each, how they depend on one another, and how they
> talk to external systems. It follows the repo's existing module conventions
> (`Warehouse`, `Sales`, `Frontline`) and the dependency rules in `CLAUDE.md`.
>
> Two slices are marked throughout:
> **[AUTH]** = the authoring slice the client wants *now* (workflows, tasks, lines).
> **[RUN]** = the runtime slice (order ingestion, queues, consumption) — later.

---

## 1. Project / assembly layout

Shopfloor currently ships only `…Shopfloor.WebUI`. It owns new state in PostgreSQL
(workflows, stations, task types), so — unlike read-only `Sales`/`Frontline` — it needs a
full **Domain + Infrastructure** stack, mirroring `Warehouse`.

```
src/Modules/Shopfloor/
├── LKvitai.MES.Modules.Shopfloor.Contracts        [AUTH]  zero-deps DTOs, view types, message contracts
├── LKvitai.MES.Modules.Shopfloor.Domain           [AUTH]  config aggregates now; ProductionOrder/WorkItem [RUN]
├── LKvitai.MES.Modules.Shopfloor.Application       [AUTH]  commands, queries, ports, behaviors
├── LKvitai.MES.Modules.Shopfloor.Infrastructure    [AUTH]  EF Core ShopfloorDbContext, repos, migrations
├── LKvitai.MES.Modules.Shopfloor.Api               [AUTH]  composition root, controllers, DI
├── LKvitai.MES.Modules.Shopfloor.WebUI             [AUTH]  Blazor Server (exists) — calls Api over HTTP
├── LKvitai.MES.Modules.Shopfloor.Projections       [RUN]   Marten projections (digital twin, read models)
└── LKvitai.MES.Modules.Shopfloor.Sagas             [RUN]   MassTransit: release→tasks, completion→warehouse

src/Modules/Integration/
└── LKvitai.MES.Modules.Integration.LegacyOrders    [RUN]   ACL: poll MS SQL, publish events, status write-back
```

### Dependency layering (enforced by ArchitectureTests / DependencyValidator)

```
Layer 0  Cqrs.Abstractions ······························ WebUI (leaf)
Layer 1  SharedKernel
Layer 2  Shopfloor.Contracts          (ZERO deps)
Layer 3  Shopfloor.Domain             (→ SharedKernel + Contracts only, ZERO NuGet)
Layer 4  Shopfloor.Application         (→ Domain, Contracts, Cqrs.Abstractions; NO Marten)
         Shopfloor.Projections [RUN]   (→ Contracts + Domain only)
Layer 5  Shopfloor.Infrastructure      (→ Application, Domain; EF Core here)
         Shopfloor.Sagas [RUN]
Layer 6  Shopfloor.Api                 (composition root)
```

`Integration.LegacyOrders` is its own module: it references `Shopfloor.Contracts` (to emit
domain messages) and the messaging bus only — never Shopfloor internals.

---

## 2. Namespace & class map

### 2.1 `…Shopfloor.Contracts` **[AUTH]**

Zero-dependency DTOs and message contracts shared across layers and modules.

```
Configuration/
  WorkflowTemplateDto · WorkflowTemplateSummaryDto · WorkflowGraphDto
  WorkStationDto · WorkCenterDto
  LegacyProductTypeDto · ProductTypeMappingDto · CoverageSummaryDto
  WorkflowStatus (enum: Draft|Published|Archived)
Integration/                                   [RUN]
  ProductionOrderReleased · OrderStatusChanged
  MaterialReservationRequested · MaterialConsumed
Views/                                         [RUN]
  WorkItemQueueView · StationLoadView
```

`WorkflowGraphDto` carries the editor's exported shape: `nodes[]` (id, kind, name,
position, workStationId, durationSec, optional taskTypeCode) + `edges[]`. It is the
contract between the editor (iframe) and the Api. Formulas/materials are added later.

### 2.2 `…Shopfloor.Domain` **[AUTH]** (config) + **[RUN]** (execution)

```
Configuration/                                 [AUTH]
  WorkflowTemplate   (aggregate root, = Production Family) — Id, Code, Name, Description,
                     Status, GraphJson, audit; UpdateGraph(), Rename(), Publish(), Clone()
  WorkStation        (aggregate) — Id, Code, Name, WorkCenterId, WipLimit, IsActive
  WorkCenter         — Id, Code, Name
  ProductTypeWorkflowMap  — LegacyProductTypeCode → WorkflowTemplateId   (many : 1)
  LegacyProductType  (read-only cache) — Code, KindName, Name, LastSyncedAt, RemovedAt
  ValueObjects/ WipLimit
  Events/ WorkflowPublishedEvent                (raised now, consumed later)
Execution/                                     [RUN]
  ProductionOrder (ES aggregate) · WorkItem (ES aggregate) · MaterialLine
  Events/ ProductionOrderReleased · WorkItemStarted · WorkItemCompleted · WorkItemStopped
```

Domain has **zero infrastructure deps**: persistence is via ports defined in Application.

### 2.3 `…Shopfloor.Application` **[AUTH]**

```
Configuration/
  Commands/  CreateWorkflowTemplate · RenameWorkflowTemplate · UpdateWorkflowGraph
             PublishWorkflowTemplate · CloneWorkflowTemplate · DeleteWorkflowTemplate
             CreateWorkStation · UpdateWorkStation · CreateWorkCenter · UpdateWorkCenter
             AssignProductTypes (bulk) · UnassignProductType · SyncLegacyProductTypes
  Queries/   GetWorkflowTemplates · GetWorkflowTemplateById
             GetWorkStations · GetWorkCenters
             GetLegacyProductTypes(search, unmapped) · GetCoverage
Abstractions/  (ports — implemented in Infrastructure)
  IWorkflowTemplateRepository · IWorkStationRepository · IWorkCenterRepository
  IProductTypeMappingRepository · ILegacyProductTypeReader · IShopfloorUnitOfWork
Behaviors/  ValidationBehavior · LoggingBehavior   (FluentValidation; Cqrs.Abstractions handlers)
Execution/  …commands/queries for orders & work items   [RUN]
```

### 2.4 `…Shopfloor.Infrastructure` **[AUTH]**

```
Persistence/
  ShopfloorDbContext               (EF Core, PostgreSQL — schema "shopfloor")
  Configurations/  WorkflowTemplateConfig · WorkStationConfig · WorkCenterConfig
                   · ProductTypeWorkflowMapConfig · LegacyProductTypeConfig
  Repositories/    WorkflowTemplateRepository · WorkStationRepository · …
  Migrations/      (EF Core migrations) + ShopfloorDesignTimeDbContextFactory
Sql/              SqlLegacyProductTypeReader : ILegacyProductTypeReader (Microsoft.Data.SqlClient)
DependencyInjection/  AddShopfloorInfrastructure(IServiceCollection, IConfiguration)
Messaging/        (Marten event store wiring + bus)           [RUN]
Warehouse/        WarehouseStockGateway (calls Warehouse commands/contracts) [RUN]
```

### 2.5 `…Shopfloor.Api` **[AUTH]**

```
Endpoints/    Workflows · WorkStations · WorkCenters
              ProductTypeMappings · LegacyProductTypes   (minimal APIs, mirror Sales.Api)
Program.cs    UseScaffoldSerilog → AddScaffoldApiCore
              → AddShopfloorApplication → AddShopfloorInfrastructure → Portal auth
              → MapGroup("/api/shopfloor").RequireAuthorization()
```

### 2.6 `…Shopfloor.WebUI` **[AUTH]** (exists)

```
Pages/
  Workflows/ WorkflowList.razor · WorkflowEditor.razor (hosts the editor iframe)
  Reference/ WorkStations.razor · WorkCenters.razor
  Mapping/   ProductTypes.razor (legacy types, search, bulk-map, coverage)
Services/  ShopfloorApiClient (typed HttpClient) · ShopfloorApiAuthHandler : PortalBearerForwardingHandler
wwwroot/prototypes/ shopfloor-workflow-editor-prototype.html  (exists; gains load/save via postMessage)
```

---

## 3. Component diagram (authoring slice + runtime seams)

```
            ┌──────────────────────────────────────────────────────────────────┐
            │                       Operator notebooks / technologist browser    │
            └───────────────┬──────────────────────────────────────────────────-┘
                            │ HTTPS (Blazor Server circuit)
                            ▼
        ┌────────────────────────────  Shopfloor.WebUI  ───────────────────────┐
        │  Families/editor · Stations · WorkCenters · Legacy types & mapping    │
        └───────────────┬───────────────────────────────────────────────────────┘
                        │ typed HttpClient (ShopfloorApiClient)
                        ▼
        ┌────────────────────────────  Shopfloor.Api  ─────────────────────────┐
        │  Endpoints → Cqrs.Abstractions handlers → Application (commands/queries)│
        └───────────────┬───────────────────────────────────────────────────────┘
              ┌──────────┴───────────┐
              ▼                      ▼
   Shopfloor.Application     Shopfloor.Domain
   (ports, handlers)         (WorkflowTemplate, WorkStation, …)
              │ IRepository ports
              ▼
   Shopfloor.Infrastructure  ──EF Core──▶  PostgreSQL  (schema "shopfloor")
              │
              ├───────────────────[RUN]──────────────────────────────────────────┐
              ▼                                                                   ▼
   Integration.LegacyOrders                                          Warehouse module
   ── poll ──▶ MS SQL (legacy)                                       AllocateReservation
   ◀─ status ─ write-back                                            RecordStockMovement
              │                                                            ▲
              └──── RabbitMQ (MassTransit) ──── Shopfloor.Sagas ───────────┘
                    ProductionOrderReleased / MaterialConsumed
```

---

## 4. Interaction flows

### 4.1 Authoring a workflow **[AUTH]** — available now

```
Technologist → WebUI WorkflowEditorPage
  → load: GET /api/shopfloor/workflows/{id}     → GetWorkflowTemplateById → WorkflowGraphDto
  → edit on JS canvas (nodes, edges, formulas, materials)
  → save: PUT /api/shopfloor/workflows/{id}/graph (WorkflowGraphDto)
      → UpdateWorkflowGraphCommand → WorkflowTemplate.UpdateGraph() → EF Core persist
  → publish: POST /api/shopfloor/workflows/{id}/publish
      → PublishWorkflowTemplateCommand → status=Published (+ WorkflowPublishedEvent)
```

### 4.2 Creating a line / task type **[AUTH]** — available now

```
Technologist → WebUI WorkStations.razor
  → POST /api/shopfloor/stations (WorkStationDto)
      → CreateWorkStationCommand → WorkStation aggregate → EF Core
```

### 4.3 Order → tasks → consumption **[RUN]** — later (full loop)

```
Integration.LegacyOrders poll → ProductionOrderReleased (RabbitMQ)
  → Shopfloor.Sagas: resolve family via product-type mapping → load its WorkflowTemplate
                      → expand WorkItems (Flow+Formula engines)
  → MaterialReservationRequested → Warehouse.AllocateReservation
  → operator completes WorkItem (+actual qty) → WorkItemCompleted
  → MaterialConsumed → Warehouse.RecordStockMovement
  → all done → ProductionOrderCompleted → ACL writes status back to MS SQL
```

---

## 5. External system interactions

| External system | Direction | Mechanism | Owned by |
|---|---|---|---|
| Legacy MS SQL / Access | read orders | polling (same-subnet, user/pwd) | `Integration.LegacyOrders` |
| Legacy MS SQL | write status | proc/table update | `Integration.LegacyOrders` |
| RabbitMQ | pub/sub | MassTransit | Infrastructure / Sagas |
| Warehouse module | reserve + write-off | in-process commands / contracts | `Infrastructure.Warehouse` gateway |
| PostgreSQL | config state | EF Core (`shopfloor` schema) | Infrastructure |
| PostgreSQL | event store / read models | Marten | Infrastructure / Projections |
| Label printers (RPi/serial) | print | ZPL over TCP | Infrastructure (later) |
| Portal | auth | cookie / PortalAuth building block | WebUI + Api |
| Prometheus / Grafana | metrics | OpenTelemetry export | Api / Projections |

**Why these boundaries:** Decision **S-3** (Warehouse owns stock) and **S-2** (legacy via
ACL only) mean Shopfloor never touches the legacy DB or the stock tables directly — both
are isolated behind a single seam, so they can be swapped (API, CDC) without touching the
domain.

---

## 6. Persistence strategy

| Aggregate | Slice | Store | Rationale |
|---|---|---|---|
| WorkflowTemplate (Production Family) | [AUTH] | EF Core (state) + GraphJson `jsonb` | Config, changes rarely; JSON matches editor export |
| WorkStation · WorkCenter | [AUTH] | EF Core (state) | Reference data |
| ProductTypeWorkflowMap | [AUTH] | EF Core (state) | Many legacy codes → one family |
| LegacyProductType | [AUTH] | EF Core (state) cache | Read-only snapshot synced from legacy SQL |
| ProductionOrder | [RUN] | Marten (event-sourced) | Auditable lifecycle |
| WorkItem | [RUN] | Marten (event-sourced) | Traceability + actual qty capture |
| Digital-twin read models | [RUN] | Marten projections | Live floor state |

The state/event split intentionally mirrors Warehouse (EF Core master data + Marten stock).

---

## 7. What to build for the authoring slice (Task-1 cut)

Smallest set that lets the client author workflows, describe tasks, create lines, and see scale:

**New projects:** `Contracts`, `Domain`, `Application`, `Infrastructure`, `Api`
(+ wire existing `WebUI`).

**Domain:** `WorkflowTemplate` (Production Family), `WorkStation`, `WorkCenter`,
`ProductTypeWorkflowMap`, `LegacyProductType` (cache). No `TaskType` catalog — a task is a
generic node in the graph.

**Api endpoints:** workflows (+ graph save / publish / clone), stations, work centers,
legacy-type list + sync, product-type mapping + coverage.

**UI:** Production Families list (C1), Workflow Editor wired to load/save (C2), Work
Stations (D1), Work Centers (D5), Legacy types & mapping (E0/E1). No warehouse/queue code yet.

**Explicitly NOT now:** order-ingestion ACL, `Sagas`, `Projections`, Warehouse gateway,
operator queue runtime, digital twin, materials/formulas. Those are the **[RUN]** slice.
The authoritative MVP cut is [`shopfloor-10`](./shopfloor-10-mvp-authoring-scope.md) /
[`shopfloor-11`](./shopfloor-11-mvp-authoring-implementation-blueprint.md).

---

## 8. Cross-references

- Boundaries & decisions: [`shopfloor-00-architecture.md`](./shopfloor-00-architecture.md)
- Screen inventory: [`shopfloor-08-ui-scope.md`](./shopfloor-08-ui-scope.md)
- Module conventions & dependency rules: `CLAUDE.md`, `src/SOLUTION_STRUCTURE.md`
- Reference implementation to mirror: `Warehouse` (EF Core master data + Marten stock)
