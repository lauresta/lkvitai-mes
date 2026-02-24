# P-01 — Test Data

**Status:** 🔴 TODO

---

## Required Master Data

| Entity | Minimum set |
|--------|-------------|
| Supplier | At least 1 supplier (e.g. `SUP-001 "Test Supplier"`) |
| Item/SKU | At least 2 items (e.g. `SKU-001`, `SKU-002`) |
| UoM | `EA` (each), `BOX` (with conversion) |
| WarehouseLocation | Receiving dock location + 1 storage bin |
| HandlingUnitType | `PALLET`, `BOX` |

## Seed Scripts

> TODO: add SQL/JSON seed scripts or reference to `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/` fixtures

## Test Shipment Payloads

```json
// POST api/warehouse/v1/receiving/shipments
{
  "supplierId": 1,
  "expectedDate": "2026-03-01",
  "lines": [
    { "itemId": 1, "expectedQty": 100, "uomId": 1, "lotNumber": "LOT-2026-001" },
    { "itemId": 2, "expectedQty": 50,  "uomId": 1 }
  ]
}
```

```json
// POST api/warehouse/v1/receiving/shipments/{id}/receive-items
{
  "lines": [
    { "lineId": 1, "receivedQty": 100, "locationId": 5 },
    { "lineId": 2, "receivedQty": 50,  "locationId": 5 }
  ]
}
```

## Notes

- Use `Testcontainers` + Marten for integration tests
- See `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/`
