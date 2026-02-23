# Layer Cross-Check P2 Decisions

## G-12: Valuation initialize/adjust-cost endpoint usage
- Date: 2026-02-23
- Decision: Keep `POST /api/warehouse/v1/valuation/initialize` and `POST /api/warehouse/v1/valuation/{itemId}/adjust-cost` as API-only endpoints for controlled operational runs.
- Reasoning:
  - Existing UI flow uses `POST /api/warehouse/v1/valuation/adjust-cost` and already covers day-to-day cost updates.
  - `initialize` and item-scoped adjust endpoints are higher-risk operations and currently better suited for explicit operational/API invocation.
  - Removing endpoints now may break external automation or maintenance scripts.
- Follow-up: Revisit after usage telemetry is collected to decide UI exposure vs. deprecation.
