# LKvitai.MES — Shopfloor Module · Architecture Foundation

**Module:** Shopfloor (ShopfloorPilot)
**Version:** 0.1 — Architecture baseline
**Date:** 2026-06-15
**Status:** Draft — basis for implementation blueprints
**Supersedes:** internal `CONTEXT_ShopfloorPilot.md` / "Technical Specification Draft v0.1"

> This document is **architecture only**. It locks module boundaries, ownership,
> integration contracts, and the major engines. Detailed domain schemas, APIs, and
> UI specs are produced as separate numbered blueprints (`shopfloor-01..NN`). Do not
> add table columns, endpoint shapes, or formula grammars here.

---

## 1. Purpose

Shopfloor turns **customer orders** into **production tasks for line operators**, routes
those tasks through a configurable per-product workflow, and keeps the warehouse stock
in sync with what is actually consumed.

Today the workshop runs on paper: every morning yesterday's orders are released, printed,
grouped by product type (blinds, rollers, nets…) and handed to workers as paper packs.
Shopfloor replaces those paper packs with a **digital, auto-sorted work queue per line**,
and replaces the manual "done / stopped" feedback with tracked events.

What we sell is not a "node-and-arrow editor" — it is a **smart production orchestration
engine**: the system decides which task is most valuable to do next, balances WIP so
assembly never starves, and shows a live digital twin of the floor.

---

## 2. Reconciliation with Draft v0.1 (what changed)

The original spec assumed a standalone stack. It must align with the **existing modular
monolith**. The concepts survive; the technology is the repo's.

| Concern | Draft v0.1 | This architecture (repo reality) |
|---|---|---|
| Topology | .NET microservices | **Modular monolith** under `src/Modules/Shopfloor/*` |
| Frontend | Vue + Vite + Rete.js + Monaco + Tailwind | **Blazor Server + MudBlazor**; workflow editor is a self-contained HTML/JS canvas embedded in WebUI |
| Database | MS SQL + Dapper + DbUp | **PostgreSQL**; Marten (event-sourced) + EF Core (state) — same split as Warehouse |
| Messaging | (unspecified) | **MassTransit + RabbitMQ** (already in stack) |
| Naming | `weblb_sf_*` MSSQL prefix | repo naming conventions (see `CLAUDE.md`) |
| Product types | own master data | **stay in the legacy system** — Shopfloor receives a product-type *code*, does not own the catalog |

The four "engines" from the pitch (Flow, Formula, Prioritization, Digital Twin) are kept
as first-class architectural capabilities — see §7.

---

## 3. Scope

**Phase 1 (in scope)**
- Ingest "order released to production" from the legacy system (poll + translate).
- `WorkflowTemplate` per **Production Family** (many legacy product types → one flow), authored in the existing Workflow Editor.
- Generate `WorkItem`s per order from the template (split by workstation, with dependencies).
- Per-line work **queue** with automatic ordering (Take-Next prioritization).
- Operator complete / stop with **actual quantity** capture.
- Material reservation on release, **actual consumption** write-off to Warehouse on completion.
- Push completion status **back** to the legacy system.
- Semi-finished + finished **label printing** hooks.

**Phase 1 (out of scope / deferred)**
- AI scheduling beyond weighted coefficients.
- Machine/PLC integration (scanners + printers only).
- 3D visualization.
- Offline operation on operator notebooks (network is assumed present).
- Native product-type catalog inside MES (legacy remains source of truth).

---

## 4. Where Shopfloor fits

```
                 ┌──────────────────────── Legacy system (MS Access + MS SQL) ────────────────────────┐
                 │  Orders · product types · "released to production" · "manufactured/stopped" status   │
                 └───────────────▲───────────────────────────────────────────────┬──────────────────────┘
                                 │ poll (read)                      write status  │ (back)
                                 │                                                │
                 ┌───────────────┴────────────────────────────────────────────────┴──────────────┐
                 │  Integration.LegacyOrders  (Anti-Corruption Layer)                              │
                 └───────────────┬─────────────────────────────────────────────────────────────────┘
                                 │ ProductionOrderReleased / OrderStatusChanged   (via RabbitMQ)
                                 ▼
   ┌─────────────────────────────────────────── Shopfloor module ───────────────────────────────────────────┐
   │  WorkflowTemplate · ProductionOrder · WorkItem · WorkStation(queue) · execution lifecycle               │
   │  Engines:  Flow · Formula · Prioritization (Take-Next) · Digital Twin                                    │
   └───────────────┬───────────────────────────────────────────────────────────────────┬─────────────────────┘
                   │ MaterialReservationRequested / MaterialConsumed (events/commands)   │ queue UI
                   ▼                                                                     ▼
   ┌──────────────────────────── Warehouse module ───────────────────────┐    Operator notebooks (per line)
   │  StockLedger (sole owner of movements) · Reservation · AvailableStock │
   └──────────────────────────────────────────────────────────────────────┘
```

Sibling modules already scaffolded: `Sales`, `Frontline`, `Scanning`, `Portal`. Shopfloor
reuses Portal auth and the Cikada.MES design system, exactly like the other WebUIs.

---

## 5. Mandatory Architectural Decisions

These are locked. Blueprints must not relitigate them.

| # | Decision | Rule |
|---|----------|------|
| **S-1** | Product types live in legacy | Shopfloor never owns the 400-type catalog. It caches a **read-only snapshot** (code + name) of legacy product types and **maps many of them (many : 1) to a Production Family** that carries one workflow. Legacy codes are opaque strings; the mapping is a separate table so re-sync never drops it. A native catalog is revisited only when Sales moves into MES. |
| **S-2** | Legacy integration is one-directional read + status write-back | MES **polls** legacy MS SQL (no webhooks/CDC — Access/SQL can't push). Status flows back as a write to a legacy table/proc. Both directions cross the ACL only. |
| **S-3** | Warehouse owns all stock truth | Shopfloor never writes stock. It emits intent (`MaterialReservationRequested`, `MaterialConsumed`); Warehouse applies it via existing commands. Honors Warehouse **Decision 1** (StockLedger is sole movement owner). |
| **S-4** | Reserve on release, consume on completion | Reservation (SOFT) is placed when an order is released. Real write-off happens only when an operator completes a task and enters the **actual** quantity. A stopped task consumes nothing — the reservation simply remains. |
| **S-5** | Two material sources per task | A task's materials are either **order-bound** (SKU comes from the order, e.g. fabric `R432`) or **template-fixed** (SKU is set in the template, e.g. screws, ribbon). Both resolve to concrete Warehouse SKUs at WorkItem creation. |
| **S-6** | Workstation queue is auto-ordered | Operators do not get a "Take Next" lottery in isolation — each line shows a **prioritized queue** computed by the engine. Assignment to a line is automatic from the workflow; operators may switch between lines they own. |
| **S-7** | Configuration over code | New/changed product workflows and formulas are authored in the UI and stored as data. No recompilation, no developer in the loop to onboard a product type. |

---

## 6. Bounded Context & Core Aggregates (conceptual)

Names are conceptual; fields are defined later in `shopfloor-01-domain-model.md`.

| Aggregate | Responsibility | Persistence (proposed) |
|-----------|----------------|------------------------|
| **WorkflowTemplate** (= Production Family) | The flow for a **family** of legacy product types that share one production flow (many codes → one template). Nodes (operations), edges (dependencies); per-node materials + formulas come later. Authored in Workflow Editor. | State-based (changes rarely; versioned) |
| **ProductionOrder** | One legacy order released to production. Carries `productTypeCode` + `params` (width, height, fabric SKU, qty…) verbatim. Lifecycle: `Released → InProgress → Done / Stopped`. | Event-sourced (auditable lifecycle) |
| **WorkItem** | One atomic task instance = (ProductionOrder × WorkflowTemplate node). Bound to a WorkStation, carries resolved materials and dependency links. Lifecycle: `Blocked → Pending → InProgress → Done / Stopped`. | Event-sourced (traceability + actual qty) |
| **WorkStation** | A physical workplace (fabric cutting, aluminium saw, steel saw, assembly, semi-assembly, profile cutting). Holds the ordered queue of its WorkItems; has a WIP/CONWIP limit. | State-based |

Supporting concepts (not necessarily their own aggregates in Phase 1):
- **LegacyProductType** (read-only cache) + **ProductTypeWorkflowMap** (many : 1) — the
  legacy snapshot and the product-type → Production Family mapping. See
  [`shopfloor-10`](./shopfloor-10-mvp-authoring-scope.md).
- **Release batch** — the morning grouping of orders. Modeled as an ingestion grouping key,
  not a rigid aggregate. The "packs by product type" behavior emerges from queue ordering
  (SetupGain), not from a hard batch entity.
- **Label** — semi-finished + finished print artifacts, attached to WorkItem / ProductionOrder.

**Parallel branches** in a workflow (e.g. the pliss-blind example: `ATSVARO PJOVIMAS`,
`VELENO PJOVIMAS`, `AUDINIO PJOVIMAS` → `AUDINIO KLIJAVIMAS` → finish) become independent
WorkItems on possibly different WorkStations. A downstream WorkItem stays `Blocked` until
its `dependsOn` predecessors are `Done`. One operator covering several stations simply sees
those WorkItems across the queues they own.

---

## 7. The Four Engines (architectural placement)

These are the product's differentiators. Each is a capability with a clear home; depth is
specced in its own blueprint.

**7.1 Flow Engine** — `WorkflowTemplate` interpretation. Already prototyped as the
Workflow Editor (DAG of nodes/edges, cycle-safe, exportable JSON). Architecturally: the
template is data; a runtime expander turns `order × template` into WorkItems + dependencies.

**7.2 Formula Engine** — Excel-like expressions per task (`fabric_length_mm = CEILING((height_mm+20)*1.01, 1)`).
Evaluated server-side at WorkItem creation against the order's `params`, producing concrete
quantities and cut dimensions. Inputs are the order params + upstream results; outputs feed
material resolution and operator instructions. (Engine choice — e.g. NCalc-class — is a
blueprint decision, not an architecture one.)

**7.3 Prioritization Engine (Take-Next)** — orders each WorkStation queue by a weighted score.
This is the headline selling point. Conceptual score:

```
Score(workItem) =
      KitImpact      // how much this moves an order toward final assembly (highest weight)
    + DueSoon        // urgency vs. due date
    + SetupGain      // bonus when it matches the current batch (same size/colour/material) → fewer changeovers
    + QueuePressure  // de-prioritize overloaded downstream stations (WIP/CONWIP gate)
    + Fairness       // prevent cherry-picking only easy tasks ("Sąžiningumo taisyklė")
    + CustomRules     // configurable per deployment
```

Weights and the exact function are configuration (S-7), tuned without redeploy. The engine
is pure/deterministic over a queue snapshot so it can be tested and audited.

**7.4 Digital Twin & Monitoring** — read-model projection of live floor state for the
dashboard: per node (current task, operator, elapsed, WIP, throughput) and per edge (queue
length, mean/P90 wait, throughput), rendered as colour = bottleneck severity, thickness =
load, animation = flow. Built from WorkItem lifecycle events; exported to Prometheus/Grafana.

---

## 8. Integration — Legacy System (ACL)

New project: `Integration.LegacyOrders` (Anti-Corruption Layer).

- **Inbound (read):** a polling worker queries legacy MS SQL on the same subnet
  (username/password connection) for orders newly flagged "released to production".
  It translates each into a domain message and publishes to RabbitMQ
  (`ProductionOrderReleased`). Translation isolates all legacy schema quirks here.
- **Outbound (status write-back):** when a `ProductionOrder` reaches `Done` or `Stopped`,
  the ACL consumes the internal event and writes the status back to the legacy
  table/stored procedure ("manufactured" / "stopped").
- **Idempotency:** polling must be safe to re-run; the ACL dedups by legacy order id +
  release marker so the same order is not ingested twice.

Direct DB access (not API) is acceptable for Phase 1 given the same-subnet trust boundary;
the ACL is the single seam, so a future API/CDC swap touches only this module.

---

## 9. Integration — Warehouse

All stock interaction respects Warehouse's existing contracts and Decision 1.

| Shopfloor event | Warehouse reaction | Existing command |
|---|---|---|
| Order released → reserve materials | SOFT reservation across resolved WorkItem materials | `AllocateReservationCommand` |
| WorkItem completed (actual qty entered) | Write-off the **actual** quantity from stock | `RecordStockMovementCommand` (→ PRODUCTION) |
| WorkItem stopped | nothing consumed — reservation persists | — |
| Order stopped / cancelled | release reservation | (reservation cancel) |

Material resolution (`fabric_sku` from order, or template-fixed SKU) maps directly onto a
Warehouse `Item.InternalSKU`. Note the seam already anticipated in master data:
`Item.ProductConfigId -- FK to Product Config module`.

**Edge case — material not on hand at start:** treated as a stop condition for that
WorkItem / order, surfaced to the operator and back to legacy as "stopped". Not a happy path.

---

## 10. End-to-End Flow (happy path)

```
1. Morning poll: legacy orders flagged "released"  ──▶  Integration.LegacyOrders
2. ACL publishes ProductionOrderReleased {legacyId, productTypeCode, params}  ──▶ RabbitMQ
3. Shopfloor creates ProductionOrder; resolves its Production Family via the
     product-type mapping → loads that family's WorkflowTemplate
4. Flow Engine expands order × template  ──▶  WorkItems (+ dependencies, + resolved materials)
5. Formula Engine computes cut sizes / quantities from params
6. Reservation: MaterialReservationRequested  ──▶  Warehouse AllocateReservation (SOFT)
7. Each WorkStation queue is ordered by the Prioritization Engine
8. Operator opens their line queue, works the top item, presses "Complete"
9. Popup: operator enters ACTUAL quantity used (scrap / offcut aware)
10. WorkItemCompleted  ──▶  MaterialConsumed  ──▶  Warehouse RecordStockMovement (actual)
11. Dependent WorkItems unblock; Digital Twin + Grafana update
12. When all WorkItems Done  ──▶  ProductionOrderCompleted
13. ACL writes "manufactured" back to legacy
```

---

## 11. Open Questions

**Legacy contract**
- Which legacy table/column marks an order "released to production", and which to update on
  "manufactured/stopped"? Is it the same source the polling reads?
- Are orders released individually or as a daily batch row? Poll cadence (30–60s acceptable)?
- Exact `params` available per order (confirm fabric SKU, cut dimensions, qty multiplier).

**Domain**
- Is WorkItem lifecycle event-sourced (Marten) or state + audit log? (Architecture leans
  event-sourced for traceability — confirm in domain blueprint.)
- Queue ordering authority: pure engine score, or may a foreman manually reorder/override?
- Do we need an explicit `ReleaseBatch` aggregate, or is grouping just a queue concern?

**Execution**
- Label format: plain barcode vs GS1-128/QR for semi-finished traceability?
- WorkStation ↔ operator assignment model (a worker owning multiple stations — switch vs.
  smart single-queue ordering).

**Stock**
- Reservation on release when stock is insufficient: create order as `MaterialShortage`, or
  block creation?
- Unit/rounding policy alignment with Warehouse `BaseUoM` (mm vs m for fabric).

---

## 12. Blueprint Roadmap (next documents)

This file is the umbrella. Implementation blueprints follow, each self-contained:

| # | Blueprint | Covers |
|---|-----------|--------|
| `shopfloor-01-domain-model` | Aggregates, events, lifecycle states, persistence split |
| `shopfloor-02-legacy-integration` | ACL: poll query, message contracts, status write-back, idempotency |
| `shopfloor-03-workflow-and-formula` | Template storage/versioning, formula grammar + evaluation |
| `shopfloor-04-warehouse-integration` | Reservation + consumption contracts, SKU resolution |
| `shopfloor-05-prioritization-engine` | Score model, coefficients, configuration, tests |
| `shopfloor-06-operator-queue-ui` | Per-line queue UI, complete/stop + actual-qty flow |
| `shopfloor-07-digital-twin` | Live projection, dashboard, Prometheus/Grafana export |
| `shopfloor-08-ui-scope` | **Cross-cutting** — full UI screen inventory (all groups, mappings, settings) |
| `shopfloor-09-solution-architecture` | **Cross-cutting** — projects, namespaces, classes, component & external interactions |
| `shopfloor-10-mvp-authoring-scope` | **MVP cut** — author workflows + lines + product-type mapping (the "see the scale" slice) |
| `shopfloor-11-mvp-authoring-implementation-blueprint` | Buildable spec for the MVP authoring slice (projects, schema, legacy sync, editor bridge) |
| `shopfloor-12-smart-validation` | Editor **Validate** button: smart rule catalog (errors/warnings/hints), severity, result contract — the client WOW |

**Build order:** 01 → 02 → 04 → 06 deliver the core loop (order → tasks → queue → consume →
status back). 03/05/07 layer on the differentiators.
