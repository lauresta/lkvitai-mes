# UI E2E Test Plan (Process + Manual Derived)

Sources used:
- `docs/process/README.md`
- `docs/process/universe/process-universe.md` (+ pointer target semantics)
- `docs/process/processes/P-01-goods-receiving-inbound/index.md`
- `docs/process/processes/P-01-goods-receiving-inbound/scenarios.md`
- `docs/process/processes/P-03-outbound-order-fulfillment/index.md`
- `docs/process/processes/P-05-cycle-count-stock-reconciliation/index.md`
- `docs/manuals/docs/index.md`
- `docs/manuals/docs/procesai/P-01-priemimas-inbound.md`
- `docs/manuals/docs/procesai/P-03-israsymas-isuntimas-outbound.md`
- `docs/manuals/docs/procesai/P-05-inventorizacija-cycle-count.md`
- `docs/manuals/docs/trikciu-salinimas/daznos-klaidos.md`

## Proposed tests

### E2E-P01-01 Inbound create: navigation + required field validation
- Tag: `NOW`
- Routes: `/warehouse/inbound/shipments/create`
- Key UI actions:
  - Open create page.
  - Verify form controls and line section render.
  - Click `Create Shipment` with missing supplier.
- Expected outcomes:
  - Validation blocks submit.
  - Error toast appears (`Supplier is required.`).
  - User remains on create route.
- Preconditions/test data:
  - None for submit-blocked validation path.
  - API availability for initial lookup load is helpful but not required for validation assertion.

### E2E-P01-02 Inbound list -> create -> detail happy path
- Tag: `NEXT`
- Routes: `/warehouse/inbound/shipments`, `/warehouse/inbound/shipments/create`, `/warehouse/inbound/shipments/{id}`
- Key UI actions:
  - Open inbound list.
  - Create shipment with supplier + valid line.
  - Submit and navigate to detail.
- Expected outcomes:
  - Success toast and detail route open.
  - New shipment appears in list after refresh.
- Preconditions/test data:
  - Deterministic supplier/item seed available.
  - Optional cleanup strategy for created shipment.

### E2E-P03-01 Outbound list + create entrypoint
- Tag: `NOW`
- Routes: `/warehouse/sales/orders`, `/warehouse/sales/orders/create`
- Key UI actions:
  - Open sales orders list.
  - Validate create action visibility.
  - Navigate to create page and verify key controls.
- Expected outcomes:
  - No runtime UI errors.
  - Navigation to create form succeeds.
- Preconditions/test data:
  - None (navigation + availability test only).

### E2E-P03-02 Outbound reserve/picking/dispatch happy path
- Tag: `NEXT`
- Routes: `/warehouse/sales/orders`, `/warehouse/sales/orders/{id}`, `/warehouse/picking/tasks`, `/warehouse/outbound/dispatch`
- Key UI actions:
  - Open order detail.
  - Reserve and release for picking.
  - Complete picking task and dispatch shipment.
- Expected outcomes:
  - Status transitions through reserve/picking/dispatch.
  - No error banner/toast in happy flow.
- Preconditions/test data:
  - Seeded order and stock quantities.
  - Deterministic user role permissions for outbound actions.

### E2E-P05-01 Cycle count list -> schedule navigation
- Tag: `NOW`
- Routes: `/warehouse/cycle-counts`, `/warehouse/cycle-counts/schedule`
- Key UI actions:
  - Open cycle count list.
  - Open schedule screen.
  - Verify scheduling controls and return back.
- Expected outcomes:
  - Navigation works both directions.
  - Key form fields are rendered and actionable.
- Preconditions/test data:
  - None for navigation-only flow.

### E2E-P05-02 Cycle count execution + reconciliation
- Tag: `NEXT`
- Routes: `/warehouse/cycle-counts/{id}/execute`, `/warehouse/cycle-counts/{id}/discrepancies`
- Key UI actions:
  - Execute a scheduled count.
  - Submit counted quantities.
  - Review discrepancies and approve adjustment.
- Expected outcomes:
  - Count transitions to completed/reviewable state.
  - Reconciliation actions produce expected confirmation.
- Preconditions/test data:
  - Seeded cycle count tasks with deterministic IDs/data.
  - Known location/item inventory baseline.

## Implementation scope in this branch

NOW tests targeted for implementation:
- `E2E-P01-01`
- `E2E-P05-01`

NEXT tests deferred due missing deterministic end-to-end seed/auth orchestration for stateful multi-step outcomes.
