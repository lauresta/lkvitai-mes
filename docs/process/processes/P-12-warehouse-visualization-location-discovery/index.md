# P-12 — Warehouse Visualization & Location Discovery

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Phase 1 implemented

---

## Summary

Provides operators and managers a visual 2D/3D map of the warehouse to find stock, check location status, and understand layout.

**Evidence:**
- UI: `Visualization/Warehouse3D.razor` (`/warehouse/visualization/3d` and `/warehouse/visualization/2d`), `WarehouseLocationDetail.razor`
- Controller: `WarehouseVisualizationController` (`api/warehouse/v1/visualization`)
- Typed client: `VisualizationClient`

---

## Trigger

Operator searches for a SKU or browses the warehouse map interactively.

## Outcomes

- Location contents visible (current stock, HUs, balance)
- Location clicked → detail view showing balance information

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Operator | Browses map to find stock locations |
| Warehouse Manager | Views overall layout and capacity |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/visualization/3d` | `Visualization/Warehouse3D.razor` | Operations → Warehouse Map |
| `/warehouse/visualization/2d` | `Visualization/Warehouse3D.razor` (shared) | Operations → Warehouse Map |
| `/warehouse/locations/{Id:int}` | `WarehouseLocationDetail.razor` | — (drill-down) |

## Subprocesses Used

- SP-12 Layout Editor / Zone Setup (admin setup)

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/visualization/layout/{layoutId}` | WarehouseVisualizationController | OperatorOrAbove |
| GET | `api/warehouse/v1/visualization/3d/{layoutId}` | WarehouseVisualizationController | OperatorOrAbove |
| GET | `api/warehouse/v1/stock/location-balance` | StockController | OperatorOrAbove |

## Key Domain Objects

`WarehouseLayout`, `WarehouseLocation`, `LocationBalance`

## Notes

- 2D and 3D share the same Razor component (`Warehouse3D.razor`) — rendering mode differs
- Layout edited via `Admin/LayoutEditor.razor` (SP-12)

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
