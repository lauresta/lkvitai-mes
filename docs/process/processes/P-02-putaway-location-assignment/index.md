# P-02 — Putaway & Location Assignment

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Moves received goods from the receiving/dock area to their designated warehouse storage locations.

**Evidence:**
- UI: `Putaway.razor` (`/warehouse/putaway`)
- Controller: `PutawayController` (`api/warehouse/v1/putaway`)
- Typed client: `PutawayClient` + `MasterDataAdminClient` (location lookup)

---

## Trigger

Putaway task generated after inbound shipment receipt (P-01) + QC pass, or created manually by manager.

## Outcomes

- HU physically moved from dock/receiving to storage bin
- `StockMoved` (TRANSFER, FROM receiving TO storage) appended to StockLedger
- `LocationBalanceView` updated for both source and target locations (≤5 s)

## Actors

| Role | Responsibility |
|------|---------------|
| Warehouse Operator | Executes putaway task (scans HU + target location) |
| Warehouse Manager | Creates manual putaway tasks |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/putaway` | `Putaway.razor` | Inbound → Putaway |

## Subprocesses Used

- SP-05 Handling Unit Lifecycle
- SP-10 Warehouse Location Lookup

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/putaway/tasks` | PutawayController | OperatorOrAbove |
| POST | `api/warehouse/v1/putaway/tasks` | PutawayController | ManagerOrAdmin |
| POST | `api/warehouse/v1/putaway/tasks/{id}/complete` | PutawayController | OperatorOrAbove |
| GET | `api/warehouse/v1/putaway/history` | PutawayController | OperatorOrAbove |

## Key Domain Objects

`PutawayTask`, `HandlingUnit`, `WarehouseLocation`, `StockMovement` (type=TRANSFER)

## Files

- [`bpmn.md`](bpmn.md) — Process flow diagram (TODO)
- [`scenarios.md`](scenarios.md) — Happy path + edge cases (TODO)
- [`test-data.md`](test-data.md) — Test fixtures (TODO)
