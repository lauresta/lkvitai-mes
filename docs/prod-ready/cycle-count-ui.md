# Cycle Count UI (Sprint 7)

## Routes
- `/warehouse/cycle-counts`
- `/warehouse/cycle-counts/schedule`
- `/warehouse/cycle-counts/{id}/execute`
- `/warehouse/cycle-counts/{id}/discrepancies`

## Implemented
- Schedule form with `ScheduledDate`, `ABCClass`, location multi-select, and assigned operator.
- Execution page with scan-friendly location/item inputs and physical qty submission.
- Progress indicator: counted lines vs total lines.
- Discrepancy table with per-line approval modal and reason input.
- Cycle count list with actions to execute and review discrepancies.
