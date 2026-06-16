# LKvitai.MES — Shopfloor Module · UI Screen Inventory

**Module:** Shopfloor (ShopfloorPilot)
**Version:** 0.1 — UI scope baseline
**Date:** 2026-06-15
**Status:** Draft — feeds the operator-queue and digital-twin blueprints
**Parent:** [`shopfloor-00-architecture.md`](./shopfloor-00-architecture.md)

> Cross-cutting **screen inventory** for the whole module. This is the *what screens
> exist and who uses them* map, not per-screen UX specs. Detailed layouts live in the
> per-area blueprints (e.g. `shopfloor-06-operator-queue-ui`). Routes are proposed in
> the `/shopfloor/...` convention, mirroring Warehouse's `/warehouse/...`.

---

## Navigation groups

| Group | Audience | Nav label |
|-------|----------|-----------|
| A | Line operator (notebook terminal) | Shopfloor |
| B | Foreman / production manager | Production |
| C | Technologist | Workflows |
| D | Technologist / admin | Reference data |
| E | Technologist / admin | Mappings |
| F | Admin | Settings |
| G | Manager / COO | Monitoring |
| H | Admin / integrator | Integration ops |
| I | Admin | Admin |

Groups **D, E, F** together are the full "edit all reference data, mappings, and
settings" surface. The load-bearing mappings are **E0/E1** (product type → Family — the
authoring MVP), then **E2, E3** (order fields, material → SKU) which close the runtime
order → task → stock loop.

---

## A. Operator terminal

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| A1 | Line Work Queue | `/shopfloor/line/{stationId}` | Auto-sorted task queue for one line | Prototype prompt ready |
| A2 | Task Execution Detail | `/shopfloor/line/{stationId}/task/{id}` | Full task card: dimensions, materials, label | New |
| A3 | Complete modal (actual qty) | modal | Capture actual consumption → triggers write-off | New |
| A4 | Stop / exception modal | modal | Stop with reason (no material, scrap) | New |
| A5 | Barcode scan view | `/shopfloor/scan` | Scan on Start/Complete | Reuse `Scanning` |
| A6 | My lines / station switcher | modal/drawer | Switch between owned lines | New |
| A7 | Label reprint | modal | Reprint semi-finished / finished label | New |

## B. Production management

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| B1 | Production Orders list | `/shopfloor/orders` | All released orders + status | New |
| B2 | Production Order detail | `/shopfloor/orders/{id}` | WorkItems, progress, materials, reservation | New |
| B3 | Daily Release Batch | `/shopfloor/releases` | Morning batch — what was released, by type | New |
| B4 | Manual dispatch / reorder | `/shopfloor/dispatch` | Foreman override of queue order (if allowed) | New |
| B5 | Exceptions / stopped queue | `/shopfloor/exceptions` | Stopped items, material shortages | New |

## C. Workflow & formula authoring

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| C1 | Production Families (workflow list) | `/shopfloor/workflows` | Flow-groups; many legacy product types → one flow; clone | New |
| C2 | Workflow Editor | `/workflow-editor` | DAG editor (nodes/edges; formulas later) | **Exists** |
| C3 | Formula editor / sandbox | in-editor | Excel formulas + test values | Partial |
| C4 | Template version diff / publish | modal | Compare versions, publish | New |

## D. Reference data (catalogs)

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| D1 | Work Stations / Lines | `/shopfloor/ref/stations` | Lines: code, name, parent center, WIP/CONWIP limit | New |
| D3 | Operators / badges | `/shopfloor/ref/operators` | Workers, badges, owned lines | New |
| D4 | Label Templates | `/shopfloor/ref/labels` | Semi / finished label templates | New |
| D5 | Work Centers | `/shopfloor/ref/work-centers` | Station grouping; parent of stations | New |

> No **Task Types** catalog: a task is a generic node inside a workflow (name + line +
> time + later materials/formulas). `taskTypeCode` is just an optional grouping label on a
> node, not a managed catalog. See [`shopfloor-10`](./shopfloor-10-mvp-authoring-scope.md) §2.

## E. Mappings (integration)

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| E0 | Legacy types & coverage | `/shopfloor/map/product-types` | ~400 legacy types (read-only cache), search, mapped/unmapped, coverage | New · **critical** |
| E1 | Product type → Family | (within E0) | Bulk-map many legacy product types → one Production Family (many:1) | New · **critical** |
| E2 | Legacy field → Order params | `/shopfloor/map/order-fields` | Legacy columns → width_mm, height_mm, fabric_sku | New · **critical** [RUN] |
| E3 | Material binding → Warehouse SKU | `/shopfloor/map/materials` | order-bound + template-fixed → `Item.InternalSKU` | New · **critical** |
| E4 | Legacy status ↔ MES status | `/shopfloor/map/statuses` | Status mapping, both directions | New |
| E5 | Operation → Label template | `/shopfloor/map/labels` | Which label for which operation (by `taskTypeCode`) | New [RUN] |

## F. Settings / configuration

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| F1 | Prioritization weights | `/shopfloor/settings/priority` | KitImpact / DueSoon / SetupGain / Fairness / WIPGate | New · differentiator |
| F2 | Batching rules | `/shopfloor/settings/batching` | Grouping rules (by size and color) | New |
| F3 | Legacy connection | `/shopfloor/settings/legacy` | DB connection, poll cadence, table/proc | New |
| F4 | Warehouse integration | `/shopfloor/settings/warehouse` | Reservation policy, PRODUCTION location, rounding | New |
| F5 | Label printer | `/shopfloor/settings/printers` | RPi / serial printers, ZPL | New |
| F6 | General module settings | `/shopfloor/settings` | Misc | New |

## G. Monitoring & Digital Twin

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| G1 | Live Flow Map (Digital Twin) | `/shopfloor/twin` | Nodes+edges: colour=bottleneck, thickness=load, animation=flow | New · differentiator |
| G2 | WIP & Bottleneck dashboard | `/shopfloor/monitor/wip` | Station load, bottlenecks | New |
| G3 | KPI / Metrics | `/shopfloor/monitor/kpi` | Lead time, throughput, CPH | New |
| G4 | Order tracking | `/shopfloor/monitor/track` | "Where is my order" across lines | New |

## H. Integration operations

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| H1 | Legacy sync status | `/shopfloor/ops/sync` | Polling monitor: last pull, errors | New |
| H2 | Reservation monitor | `/shopfloor/ops/reservations` | Reserved vs available | New |
| H3 | Consumption / write-off log | `/shopfloor/ops/consumption` | Audit of actual write-offs | New |
| H4 | Message / DLQ monitor | `/shopfloor/ops/messages` | Stuck / failed integration messages | New |

## I. Admin & audit

| # | Screen | Route | Purpose | Status |
|---|--------|-------|---------|--------|
| I1 | Users & roles | — | RBAC | Reuse `Portal` |
| I2 | Audit / event history | `/shopfloor/admin/audit` | WorkItem / Order event history | New |
| I3 | Projection health | `/shopfloor/admin/projections` | Read-model lag / rebuild | Reuse Warehouse pattern |

---

## Role → primary screens

| Role | Lives in |
|------|----------|
| Line operator | A1–A7 |
| Foreman / production manager | B1–B5, G2, G4 |
| Technologist | C1–C4, D1–D5, E1–E5 |
| Admin / integrator | F1–F6, H1–H4, I1–I3 |
| Manager / COO | G1–G4 |

## Phasing

**MVP — closes the core loop (order → tasks → queue → consume → status back):**
A1–A4, B1–B2, C1, D1–D3, E1–E3, F1, F3–F4, H1.

**Differentiator layer:** G1 (Digital Twin), F1 (priority weights), G2–G3.

**Reuse (not net-new builds):** A5 (`Scanning`), I1 (`Portal` RBAC), I3 (Warehouse
projection-health pattern).

**Count:** ~40 screens; 1 exists (C2), 1 partial (C3), 1 prototyped (A1), rest new.
