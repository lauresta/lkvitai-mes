# P-01 — BPMN / Process Flow

**Status:** 🔴 TODO — BPMN diagram not yet authored
**Phase:** BPMN expansion is Phase 2

---

## Swimlanes (planned)

| Lane | Actor |
|------|-------|
| Lane 1 | Warehouse Operator |
| Lane 2 | QC Inspector |
| Lane 3 | System (LKvitai.MES) |
| Lane 4 | ERP / Kafka (external) |

## High-Level Flow Steps (placeholder)

1. **Start event:** Inbound shipment expected (manual or ERP-triggered)
2. Create Inbound Shipment record
3. Goods physically arrive at dock
4. Operator scans/registers items against shipment
5. QC Inspection task generated
6. QC Inspector inspects goods
7. **Gateway:** QC passed?
   - YES → approve inspection → `ReceiveGoodsCommand` dispatched → HU created + sealed + `StockMoved(RECEIPT)`
   - NO → reject inspection → goods quarantined → notify manager
8. Labels printed (ZPL)
9. Shipment status = Received
10. **End event:** Stock visible in LocationBalance / AvailableStock

## Detailed BPMN (TODO)

> Insert BPMN 2.0 XML or Mermaid diagram here.

```
[BPMN diagram placeholder]
```

## Notes for BPMN author

- Use Camunda Modeler or draw.io for BPMN 2.0
- Export as `.bpmn` file to this folder alongside this `.md`
- Reference `ReceiveGoodsCommand` as the system task that commits atomically
- Mark offline vs online steps (Decision 3: receiving is online-only)
