# P-08 — Agnum Integration & Reconciliation

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented — batch scheduled)

---

## Summary

Exports the daily stock valuation snapshot to the Agnum accounting system and reconciles discrepancies.

**Evidence:**
- UI: `Agnum/Configuration.razor`, `Agnum/Reconciliation.razor`
- Controller: `AgnumController` (`api/warehouse/v1/agnum`)
- Service: `AgnumExportOrchestrator` in `AgnumExportServices.cs` (~150 KB), `AgnumReconciliationServices.cs`
- Scheduler: Hangfire recurring job at 23:00 daily

---

## Trigger

- Daily scheduled batch at 23:00 (Hangfire)
- Manual trigger by Inventory Accountant via UI

## Outcomes

- CSV file generated from `OnHandValueView`
- File exported to Agnum via API
- Reconciliation report produced and stored
- Discrepancies flagged for accountant review

## Actors

| Role | Responsibility |
|------|---------------|
| Inventory Accountant | Configures mapping, reviews reconciliation report, triggers manual export |
| Warehouse Manager | Co-reviews reconciliation |
| System (Hangfire) | Runs scheduled batch |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/warehouse/agnum/config` | `Agnum/Configuration.razor` | Finance → Agnum Config |
| `/warehouse/agnum/reconcile` | `Agnum/Reconciliation.razor` | Finance → Agnum Reconcile |

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET/PUT | `api/warehouse/v1/agnum/config` | AgnumController | InventoryAccountantOrManager |
| POST | `api/warehouse/v1/agnum/schedule` | AgnumController | InventoryAccountantOrManager |
| GET | `api/warehouse/v1/agnum/export-status` | AgnumController | InventoryAccountantOrManager |
| POST | `api/warehouse/v1/agnum/reconciliation` | AgnumController | InventoryAccountantOrManager |
| GET | `api/warehouse/v1/agnum/reconciliation-report` | AgnumController | InventoryAccountantOrManager |

## Key Domain Objects

`AgnumConfig`, `ExportJob`, `ReconciliationReport`, `OnHandValueView`

## Architectural Notes

- Decision 5: Financial integration is batch-tier — latency = scheduled daily, NOT real-time
- Retry 3x on API failure; configurable CSV field mapping

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
