# P-07 — Inventory Valuation & Costing

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Maintains the financial valuation of warehouse stock — revalues items, applies landed costs, records write-downs, and produces valuation summaries.

**Evidence:**
- UI: `Valuation/Dashboard.razor`, `Valuation/AdjustCost.razor`, `Valuation/ApplyLandedCost.razor`, `Valuation/WriteDown.razor`
- Controller: `ValuationController` (`api/warehouse/v1/valuation`)
- Commands: `ValuationCommands.cs`
- Services: `ValuationCommandHandlers.cs`, `ValuationLifecycleCommandHandlers.cs`, `LandedCostAllocationService.cs`, `ValuationWriteDownPolicy.cs`
- ADR: `docs/adr/ADR-002-valuation-event-sourcing.md`

---

## Trigger

- Goods received (initial valuation)
- Manual cost adjustment by Inventory Accountant
- Landed cost receipt (freight, duties, etc.)
- Write-down decision (damage, obsolescence)

## Outcomes

- `CostAdjusted`, `LandedCostAllocated`, or `StockWrittenDown` events recorded in Valuation aggregate
- `OnHandValueView` updated
- Valuation data available for Agnum export (P-08)

## Actors

| Role | Responsibility |
|------|---------------|
| Inventory Accountant | Adjusts costs, applies landed costs |
| CFO | Approves write-downs; reads valuation summary |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/valuation/dashboard` | `Valuation/Dashboard.razor` | Finance → Valuation |
| `/warehouse/valuation/adjust-cost` | `Valuation/AdjustCost.razor` | Finance → Valuation → Adjust Cost |
| `/warehouse/valuation/apply-landed-cost` | `Valuation/ApplyLandedCost.razor` | Finance → Valuation → Apply Landed Cost |
| `/warehouse/valuation/write-down` | `Valuation/WriteDown.razor` | Finance → Valuation → Write Down |

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/valuation/summary` | ValuationController | CfoOrAdmin |
| GET | `api/warehouse/v1/valuation/by-location` | ValuationController | CfoOrAdmin |
| POST | `api/warehouse/v1/valuation/revalue` | ValuationController | CfoOrAdmin |

## Key Domain Objects

`Valuation` (event-sourced aggregate), `CostAdjustment`, `LandedCost`, `WriteDown`, `OnHandValueView`

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
