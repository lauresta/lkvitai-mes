# Layer Cross-Check P2 Decisions

## G-12: Valuation initialize/adjust-cost endpoint usage
- Date: 2026-02-23
- Decision: Keep `POST /api/warehouse/v1/valuation/initialize` and `POST /api/warehouse/v1/valuation/{itemId}/adjust-cost` as API-only endpoints for controlled operational runs.
- Reasoning:
  - Existing UI flow uses `POST /api/warehouse/v1/valuation/adjust-cost` and already covers day-to-day cost updates.
  - `initialize` and item-scoped adjust endpoints are higher-risk operations and currently better suited for explicit operational/API invocation.
- Removing endpoints now may break external automation or maintenance scripts.
- Follow-up: Revisit after usage telemetry is collected to decide UI exposure vs. deprecation.

## G-13: Compliance advanced endpoints usage
- Date: 2026-02-23
- Decision: Keep advanced compliance endpoints as API-only capabilities and document them as integration-facing operations.
- Endpoints:
  - `POST /api/warehouse/v1/admin/compliance/sign`
  - `GET /api/warehouse/v1/admin/compliance/signatures/{id}`
  - `POST /api/warehouse/v1/admin/compliance/verify-hash-chain`
  - `GET /api/warehouse/v1/admin/compliance/validation-report`
  - `POST /api/warehouse/v1/admin/compliance/export-transactions`
  - `GET /api/warehouse/v1/admin/compliance/exports`
- Reasoning:
  - Current WebUI already serves dashboard, lot-trace, and scheduled reporting.
  - The remaining operations are operational/compliance specialist workflows and are suitable for explicit API integrations.

## G-14: Barcode lookup endpoint usage
- Date: 2026-02-23
- Decision: Keep `GET /api/warehouse/v1/barcodes/lookup` as API-only for scanner/mobile and external integration paths.
- Reasoning:
  - Endpoint is suitable for hardware/mobile lookup flows that do not require a full desktop admin UI.
  - No safe, minimal placement in existing desktop workflows was identified without creating extra UI complexity.

## G-15: Feature flags endpoint usage
- Date: 2026-02-23
- Decision: Keep `GET /api/warehouse/v1/features/{flagKey}` as internal/API-only.
- Reasoning:
  - Feature flag lifecycle is governed by LaunchDarkly and service-level toggles.
  - Adding desktop management UI would duplicate existing operational controls and increase risk of accidental toggles.
