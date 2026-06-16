# LKvitai.MES — Shopfloor Module · MVP Authoring Scope

**Module:** Shopfloor (ShopfloorPilot)
**Version:** 0.1 — MVP authoring slice
**Date:** 2026-06-16
**Status:** Draft — direct basis for the first implementation blueprint
**Parent:** [`shopfloor-00-architecture.md`](./shopfloor-00-architecture.md) ·
[`shopfloor-09-solution-architecture.md`](./shopfloor-09-solution-architecture.md)

> **Goal of this slice:** let the client *author production flows today* — pull the legacy
> product types, group them, draw the flows, assign lines — so they can **see the scale**
> before any runtime (orders, queues, stock) exists. This is the **[AUTH]** slice only.

---

## 1. What this MVP delivers

A technologist can, end to end:

1. Define **production lines** (work stations) and their **groups** (work centers).
2. Pull the **~400 legacy product types** into MES (read-only) and see them all.
3. **Map** many legacy types to one **Production Family** (a group sharing one flow).
4. In the **Workflow Editor**: open a family, create tasks (name + line + approx. time),
   connect them with arrows, name the flow, **save / reopen / edit**.
5. See **coverage**: how many of the 400 types are mapped, how many families exist.

That is the whole point: a measurable picture of the production-flow landscape.

---

## 2. Core concept & naming

A set of legacy product types that differ in price/components but are **identical in
production flow** is a **Production Family** (MES term: product/routing family).

> **One record holds both the group and its flow.** The aggregate is `WorkflowTemplate`;
> its UI name is **"Production Family" / flow-group**. Many legacy product types map to one
> `WorkflowTemplate` (many : 1). We do *not* create a separate family entity — the template
> *is* the family.

```
Legacy (MS SQL)                 MES
~400 product types ──sync──▶ LegacyProductType        (read-only cache: code + name)
                                    │  map  (many : 1)        ← survives re-sync
                                    ▼
                             WorkflowTemplate  "Roller — standard"   ← Production Family
                                    │  graph (nodes + edges)
                                    ▼
                             TaskDefinition → WorkStation → WorkCenter
```

---

## 3. Catalogs & data model (MVP)

| Table | Ownership | Purpose |
|-------|-----------|---------|
| `LegacyProductType` | **read-only cache** (synced) | All legacy types: `code`, `name`, `lastSyncedAt`. Not editable in MES. |
| `WorkflowTemplate` | owned | The flow-group: `id`, `code`, `name`, `description`, `status` (Draft/Published), `graphJson`, audit. |
| `ProductTypeWorkflowMap` | owned | Mapping `legacyProductTypeCode → workflowTemplateId` (many : 1). Separate table so re-sync never drops mappings. |
| `WorkStation` | owned | Production line: `id`, `code`, `name`, `workCenterId`, `wipLimit`, `isActive`. |
| `WorkCenter` | owned | Station group: `id`, `code`, `name`. Parent of WorkStation. |

**4 owned tables + 1 cache.**

`graphJson` stores the editor's exported shape (matches today's prototype):

```
graphJson = {
  nodes: [
    { id, kind: "start" | "task" | "finish", name, position,
      workStationId,        // task only — the single line picker
      durationSec,          // approximate operation time
      taskTypeCode? }       // OPTIONAL label for later grouping (analytics/SetupGain)
    …
  ],
  edges: [ { from, to } ]   // dependency arrows
}
```

For MVP a task carries only **name + line + approx. time**. Materials, formulas, kitImpact,
labels are **deferred** (§8).

### WorkCenter is a parent, not a second task field

A task assigns to **one WorkStation only**. The WorkCenter is reached through
`WorkStation.workCenterId` — it is **not** a second dropdown in the editor. It exists for
later routing/reports/prioritization grouping.

```
WorkCenter "Cutting"
   ├── WorkStation "Audinio pjovimas" (wipLimit)   ← task → here
   └── WorkStation "Profilio pjovimas"
```

---

## 4. Legacy product-type ingestion

We need the ~400 types in MES to map them — you can't map what you can't see.

- **Recommended:** a **read-only sync** — a tiny `SELECT code, name FROM <product types>`
  against legacy MS SQL (same subnet, user/pwd already available). Upserts the
  `LegacyProductType` cache; flags new/removed types. This pulls in only a *minimal* legacy
  read, far smaller than the full order ACL (which stays **[RUN]**).
- **Fallback:** one-time **Excel/CSV import** of the type list — no connector, but goes stale.

Either way: MES never edits these; legacy stays source of truth (decision **S-1**).

---

## 5. Screens (MVP)

| # | Screen | Route | Notes |
|---|--------|-------|-------|
| 1 | Work Centers | `/shopfloor/ref/work-centers` | CRUD — small |
| 2 | Work Stations (lines) | `/shopfloor/ref/stations` | CRUD, pick parent center, WIP limit |
| 3 | Production Families | `/shopfloor/workflows` | **Grid of all flows**; row actions: Open editor, Create, Clone, Publish, Preview, Delete |
| 4 | Legacy Types & Coverage | `/shopfloor/map/product-types` | All ~400 types, search, mapped/unmapped flag, counters |
| 5 | Mapping | (within #4) | **Bulk** assign: select many types → one family |
| 6 | Workflow Editor | `/workflow-editor` | **Exists** — gains select-family + save/open/edit |

**Coverage view (#4) is the "see the scale" payoff:** `mapped N / 400 · families M ·
unmapped list`.

---

## 6. Workflow Editor changes (the main UI work)

Today the prototype hardcodes one product and only *exports* JSON. MVP adds:

- **Pick a Production Family** as the editing context (replaces hardcoded `ROLLER_STD`).
- **Task node fields (MVP):** name (free text), **line** (WorkStation dropdown),
  approximate duration. Arrows = dependencies (already works; cycles already blocked).
- **Name the flow** (the family name/code).
- **Save / Open / Edit:** load `graphJson` from Api into the canvas; save canvas back.
- **Clone:** duplicate a family's flow into a new family (accelerates reaching coverage).
- **Save validation:** exactly one `start`, a reachable `finish`, every task has a line,
  no orphan task.
- **Preview (dry-run):** validate + show the topologically-ordered task plan
  (`step → task → line → duration`). No runtime, no formulas/materials — just proves the
  flow is sound. (The prototype's `topoOrder`/`PlanJson` is the seed.) Real "run" — creating
  a production order — is **[RUN]**, not this slice.

---

## 7. Backend for this slice

New projects (per [`shopfloor-09`](./shopfloor-09-solution-architecture.md) §1):
`Contracts`, `Domain`, `Application`, `Infrastructure`, `Api` (+ wire existing `WebUI`).
**No** Marten / Sagas / Projections / `Integration.LegacyOrders` order ACL yet.

| Layer | MVP content |
|-------|-------------|
| Domain | `WorkflowTemplate`, `WorkStation`, `WorkCenter` (state-based config aggregates) |
| Contracts | `WorkflowTemplateDto`, `WorkflowGraphDto`, `WorkStationDto`, `WorkCenterDto`, `LegacyProductTypeDto`, `ProductTypeMappingDto` |
| Application | Commands/queries for the four catalogs + mapping; `ILegacyProductTypeReader` port |
| Infrastructure | `ShopfloorDbContext` (EF Core, schema `shopfloor`) + repos + migrations; `LegacyProductTypeSync` (read-only SELECT) |
| Api | `WorkflowTemplatesController` (+ graph, publish, clone), `WorkStationsController`, `WorkCentersController`, `ProductTypeMappingController`, `LegacyProductTypesController` (list + trigger sync) |

`LegacyProductTypeSync` is the *only* legacy touch-point in MVP and is read-only.

---

## 8. Explicitly deferred (NOT in this MVP)

Materials (order-bound + template-fixed) · formulas / computed specs · kitImpact ·
label templates · prioritization weights · operator queue & execution · stock
reservation/write-off · `Integration.LegacyOrders` order ingestion & status write-back ·
digital twin · operators catalog. All are **[RUN]** or later authoring layers.

A task's optional `taskTypeCode` label may be captured now (cheap) but drives nothing yet.

---

## 9. Acceptance criteria

- [ ] Create/edit work centers and work stations (station has center + WIP limit).
- [ ] Sync (or import) the ~400 legacy product types; list + search them.
- [ ] Create a Production Family; bulk-map many legacy types to it.
- [ ] Author its flow: tasks (name + line + time), arrows, named flow; save & reopen & edit.
- [ ] Clone a family's flow into a new family.
- [ ] Coverage screen shows `mapped / total`, family count, unmapped list.
- [ ] Save validation rejects no-line tasks, missing start/finish, orphans, cycles.

---

## 10. Cross-references

- Boundaries & decisions: [`shopfloor-00-architecture.md`](./shopfloor-00-architecture.md) (S-1)
- Projects/namespaces/classes: [`shopfloor-09-solution-architecture.md`](./shopfloor-09-solution-architecture.md)
- Full screen inventory: [`shopfloor-08-ui-scope.md`](./shopfloor-08-ui-scope.md)
- Reference implementation to mirror: `Warehouse` master data (EF Core + migrations)
