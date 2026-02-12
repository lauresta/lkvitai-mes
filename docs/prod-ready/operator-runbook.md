# Operator Runbook

## Wave Picking

1. Open `/warehouse/waves`.
2. Create wave with allocated order IDs.
3. Assign operator and start wave.
4. Track completed lines until status becomes `COMPLETED`.

## Cross-Dock

1. Open `/warehouse/cross-dock`.
2. Create match from inbound shipment + outbound order.
3. Move status `PENDING -> MATCHED -> COMPLETED`.

## RMAs

1. Open `/warehouse/rmas`.
2. Create RMA from SalesOrder ID and return reason.
3. Receive returned goods.
4. Inspect and choose disposition (`RESTOCK` or `SCRAP`).

## Common Issues

- `401/403` responses:
  - Validate auth headers or dev token in local development.
- `429` responses:
  - API rate limit exceeded (100 req/min per user). Retry after reset window.
- Empty analytics:
  - Ensure orders/shipments/defects exist in data.

## Escalation

- First line: Warehouse supervisor.
- Second line: MES on-call engineer.
- Attach trace ID from UI error banner for incident triage.
