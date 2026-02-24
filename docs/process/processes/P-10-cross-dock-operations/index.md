# P-10 — Cross-Dock Operations

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Phase 1 scaffolded

---

## Summary

Routes incoming goods directly from the receiving dock to outbound dispatch without formal putaway into storage.

**Evidence:**
- UI: `CrossDock.razor` (`/warehouse/cross-dock`)
- Controller: `AdvancedWarehouseController` (CrossDockController) — `api/warehouse/v1/cross-dock`
- Typed client: `AdvancedWarehouseClient`

---

## Trigger

Inbound shipment arrives; a pending outbound order matches the arriving goods — cross-dock opportunity identified.

## Outcomes

- Cross-dock record created and tracked
- Stock transferred directly from receiving dock to dispatch dock
- `LocationBalanceView` updated; no putaway task generated

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Manager | Identifies and approves cross-dock opportunity |
| Dispatch Clerk | Confirms outbound dispatch |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/cross-dock` | `CrossDock.razor` | Outbound → Cross-Dock |

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/cross-dock` | AdvancedWarehouseController | — (auth TBD — U-02) |
| POST | `api/warehouse/v1/cross-dock` | AdvancedWarehouseController | — |
| POST | `api/warehouse/v1/cross-dock/{id}/status` | AdvancedWarehouseController | — |

## Key Domain Objects

`CrossDockRecord`, `InboundShipment`, `OutboundOrder`

## Unknowns

- Auth policies for CrossDockController not confirmed (see `gaps-and-unknowns.md` U-02)
- Matching rules (how inbound is matched to outbound) not documented

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
