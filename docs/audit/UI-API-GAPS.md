# UI-API Gaps

`GAP_NO_UI` endpoints: **0**.

All previously open operational gaps are now either:
- Covered by dedicated workflow UI pages (`/warehouse/stock/adjustments`, `/warehouse/putaway`, `/warehouse/picking/tasks`, `/warehouse/labels`) and existing workflow pages (`/reservations`, `/projections`).
- Reclassified as `INTENTIONAL_NO_UI` with explicit Phase 1.5 justification in `docs/audit/UI-FINAL-STATUS.md`.

No endpoint-caller/workbench surface is used.
