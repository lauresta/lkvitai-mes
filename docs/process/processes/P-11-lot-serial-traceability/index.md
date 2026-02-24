# P-11 — Lot & Serial Number Traceability

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Tracks the full lifecycle of lot/batch numbers and serial numbers across inbound → storage → outbound, and produces traceability reports for compliance and recall scenarios.

**Evidence:**
- UI: `ReportsTraceability.razor`, `ComplianceLotTrace.razor`, `ComplianceDashboard.razor`, `Admin/SerialNumbers.razor`, `Admin/Lots.razor`
- Controller: `LotsController` (`api/warehouse/v1/lots`), `AdvancedWarehouseController` (SerialsController — `api/warehouse/v1/serials`)
- Service: `LotTraceabilityService.cs`

---

## Trigger

- Lot/serial number created at receiving (P-01)
- Traceability query by compliance officer, customer, or QC manager

## Outcomes

- Lot/serial records maintained throughout stock lifecycle
- Full traceability chain: supplier → receiving → location → order → dispatch
- Compliance reports exportable (CSV, etc.)

## Actors

| Role | Responsibility |
|------|---------------|
| QC Inspector | Creates lots at receiving; assigns lot numbers to HU lines |
| Compliance Officer | Queries traceability chain; exports compliance reports |
| Warehouse Manager | Configures lot/serial rules |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/reports/traceability` | `ReportsTraceability.razor` | Reports → Traceability |
| `/warehouse/compliance/lot-trace` | `ComplianceLotTrace.razor` | Reports → Lot Traceability |
| `/warehouse/compliance/dashboard` | `ComplianceDashboard.razor` | Reports → Compliance Dashboard |
| `/warehouse/admin/lots` | `Admin/Lots.razor` | Admin → Lots |
| `/warehouse/admin/serial-numbers` | `Admin/SerialNumbers.razor` | Admin → Serial Numbers |

## Subprocesses Used

- SP-09 Lot / Batch Assignment
- SP-11 Serial Number Assignment

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET | `api/warehouse/v1/lots/item/{itemId}` | LotsController | OperatorOrAbove |
| POST | `api/warehouse/v1/lots` | LotsController | QcOrManager |
| GET/PUT | `api/warehouse/v1/lots/{id}` | LotsController | OperatorOrAbove / QcOrManager |
| GET | `api/warehouse/v1/serials` | AdvancedWarehouseController | — |

## Key Domain Objects

`Lot`, `SerialNumber`, `HandlingUnitLine`, `StockMovement`

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
