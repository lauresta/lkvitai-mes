# P-01 — Scenarios

**Status:** 🔴 TODO — scenarios not yet authored

---

## Scenario List

| ID | Name | Type | Priority |
|----|------|------|---------|
| S-01-01 | Happy path: receive full shipment, QC pass | Happy path | P0 |
| S-01-02 | Partial receipt: some items missing from shipment | Edge case | P1 |
| S-01-03 | QC rejection: goods quarantined | Negative | P0 |
| S-01-04 | Duplicate shipment ID | Error case | P1 |
| S-01-05 | ERP-triggered receipt (Kafka `MaterialRequested`) | Integration | P1 |
| S-01-06 | Lot-tracked items received with lot assignment | Feature | P1 |
| S-01-07 | Serial-numbered items received | Feature | P2 |
| S-01-08 | Label printer unavailable — fallback to manual queue | Resilience | P1 |
| S-01-09 | Optimistic concurrency conflict on StockLedger stream | Resilience | P1 |

---

## S-01-01: Happy Path — Full Shipment Receipt

**Preconditions:**
- Inbound shipment exists with status = Expected
- Items and supplier exist in master data
- QC Inspector available

**Steps:**
1. Operator navigates to `/warehouse/inbound/shipments`
2. Opens shipment record
3. Registers received quantities for each line
4. System calls `POST api/warehouse/v1/receiving/shipments/{id}/receive-items`
5. QC task auto-created; QC Inspector navigates to `/warehouse/inbound/qc`
6. QC Inspector approves: `POST api/warehouse/v1/qc/inspections/{id}/approve`
7. System dispatches `ReceiveGoodsCommand`
8. HU created + sealed; `StockMoved(RECEIPT)` event appended to StockLedger
9. Labels printed
10. Shipment status = Received

**Expected Result:**
- `LocationBalanceView` shows new stock (≤5 s)
- `AvailableStockView` updated
- HU barcode scannable

**Assertions (test):**
- [ ] StockLedger stream contains exactly one `StockMoved` event per line
- [ ] HU state = Sealed
- [ ] QcInspection status = Approved

---

## Other Scenarios

> TODO: expand S-01-02 through S-01-09 using the same template above.
