# ADR-006: Warehouse-Agnum Integration Is Document-Based, Not Stock-Ledger-Event-Based

**Status:** Accepted  
**Date:** 2026-05-18  
**Decision Makers:** Implementation Team  
**Relates To:** Warehouse module, StockLedger, Agnum integration, accounting export

---

## Context

Warehouse uses `StockLedger` as the operational stock truth. The ledger records granular technical movements such as receipts, picks, transfers, adjustments, and internal stock changes through `StockMovedEvent`.

Agnum is an accounting/business system. The reviewed Agnum API exposes business document operations such as sales documents, purchase receipts, customer returns, and accounting-like corrections. It does not behave like a raw stock event sink.

Earlier planning included XML/CSV-style stock export and later a generic JSON stock snapshot. The Agnum API direction requires a clearer architectural boundary.

---

## Decision

Warehouse-Agnum integration is document-based, not stock-ledger-event-based.

MES `StockLedger` remains the operational stock truth.

Agnum receives only confirmed accounting/business documents through explicit export outbox records.

Raw `StockMovedEvent` is not exported directly to Agnum.

Every Agnum export must have:

- source business document/process;
- mapped Agnum endpoint/document type;
- idempotency key;
- payload snapshot;
- export status;
- retry/review workflow.

---

## Rationale

MES stock movements are too granular and technical for Agnum accounting semantics.

Agnum expects business documents such as sales, purchase receipts, customer returns, and accounting corrections. Directly exporting raw stock ledger events would risk:

- incorrect accounting semantics;
- duplicate or fragmented Agnum documents;
- loss of business context required by Agnum document APIs;
- hard-to-debug reconciliation problems;
- retries that replay technical movements instead of idempotent business documents.

The integration boundary should translate confirmed MES business processes into Agnum documents, not replicate the internal stock ledger.

---

## Consequences

Positive:

- Accounting integration follows Agnum's document model.
- Export retries can be idempotent per business document.
- Operators can review failed exports using payload snapshots and source process context.
- Stock ledger design remains focused on MES operational correctness.

Negative:

- Additional outbox/export state is required.
- Each business flow needs explicit mapping to an Agnum endpoint and document type.
- Some StockLedger movements may not have an Agnum export until business document semantics are confirmed.

Implementation consequences:

- Add an explicit Agnum export outbox model before exporting operational flows.
- Do not subscribe to `StockMovedEvent` and POST directly to Agnum.
- Use `StockMovedEvent` only as supporting evidence/input when building a confirmed business document payload.
- Reconciliation must compare MES operational state and Agnum document/balance state through dedicated reports, not by assuming event-to-document parity.

---

## Rejected Alternative: Export Each StockMovedEvent

Rejected because `StockMovedEvent` represents internal operational stock movement, not necessarily an accounting document.

Examples:

- Picking may be a warehouse operation, while Agnum may need a sales/invoice document only at shipment or accounting approval.
- Internal transfers may need different Agnum handling depending on source/destination warehouse semantics.
- Adjustments/write-offs may require reason codes, approval, or accounting correction document types.

Direct event export would couple Agnum to MES internals and make later business corrections unsafe.

---

## References

- `docs/agnum-integration-plan/01-as-is-warehouse.md`
- `docs/agnum-integration-plan/02-agnum-api-findings.md`
- `docs/agnum-integration-plan/03-target-solution-architecture.md`
- `docs/agnum-integration-plan/04-implementation-plan.md`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/Aggregates/StockLedger.cs`
- `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/Events/StockMovedEvent.cs`

