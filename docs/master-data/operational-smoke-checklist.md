# Master Data Operational Smoke Checklist

## Purpose
Quick verification for the Phase 1 operational chain:

`seed -> import -> receive -> putaway -> pick -> adjust`

## Prerequisites
- API is running and reachable.
- Seed data applied (virtual locations, categories, reason codes).
- User has roles: `WarehouseAdmin,WarehouseManager,QCInspector,Operator`.

## Automated Script
Run:

```bash
API_BASE="https://localhost:5001" \
USER_ID="smoke-admin" \
USER_ROLES="WarehouseAdmin,WarehouseManager,QCInspector,Operator" \
./scripts/master-data-operational-smoke.sh
```

Expected result:
- Script ends with `Smoke flow completed successfully.`

## Manual Verification Points
1. Health:
   - `GET /api/warehouse/v1/health` returns `Healthy` or `Degraded`.
2. Import:
   - Dry-run on `/admin/import` with no errors enables `Commit Changes`.
   - Dry-run with errors disables `Commit Changes`.
3. Receiving:
   - Create shipment and receive quantity.
   - Receiving history report shows shipment row.
4. Putaway:
   - Item appears in putaway task list before move.
   - Putaway succeeds and destination location receives stock.
5. Picking:
   - Pick task can be created and completed.
   - Pick history report includes completed task.
6. Adjustment:
   - Adjustment endpoint accepts reason code and appends history row.
   - Adjustment appears in `/api/warehouse/v1/adjustments`.
7. Projections:
   - Projections page shows lag table and runbook section.
   - Rebuild failures show friendly hint and copyable error payload.

## Troubleshooting
- If rebuild fails with schema/shadow errors, follow:
  - `docs/master-data/master-data-06-ops-runbook-projections.md`
- Use `traceId` from UI error banner for correlation in server logs.
