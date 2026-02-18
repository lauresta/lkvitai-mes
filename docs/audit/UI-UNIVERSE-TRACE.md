# UI-Universe Trace

Trace sources:
- `docs/prod-ready/prod-ready-universe.md`
- `.kiro/specs/warehouse-core-phase1/ui-task-universe.md`

| Universe item | UI routes/pages | Status | Notes |
|---|---|---|---|
| Receiving -> QC -> Putaway | `/warehouse/inbound/shipments`, `/warehouse/inbound/qc`, `/warehouse/putaway` | PASS | Putaway now has dedicated task list + execute action on canonical `/api/warehouse/v1/putaway*`. |
| Transfers / moves | `/warehouse/transfers`, `/warehouse/transfers/create`, `/warehouse/transfers/{id}/execute` | PASS | Existing transfer workflow unchanged, still canonical. |
| Sales order to dispatch flow | `/warehouse/sales/orders*`, `/reservations`, `/warehouse/picking/tasks`, `/warehouse/outbound/orders*`, `/warehouse/outbound/pack/{OrderId}`, `/warehouse/outbound/dispatch` | PASS | Allocation/pick/pack/dispatch actions all UI-accessible. |
| Adjustments + audit/history | `/warehouse/stock/adjustments` | PASS | Dedicated create + history surface on `/api/warehouse/v1/adjustments`. |
| Label operations | `/warehouse/labels` | PASS | Templates/preview/print/queue/retry/PDF retrieval covered. |

Scope exclusions for Phase 1.5 are intentionally documented in `docs/audit/UI-FINAL-STATUS.md` under `INTENTIONAL_NO_UI`.
