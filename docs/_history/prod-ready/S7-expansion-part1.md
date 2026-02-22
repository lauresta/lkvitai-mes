## Task PRD-1606: Agnum Configuration UI

**Epic:** Agnum | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** UI | **Dependencies:** PRD-1514 | **SourceRefs:** Universe §4.Epic D

### Context
Admin UI for configuring Agnum export settings: scope (by warehouse/category/logical WH), schedule (cron), format (CSV/JSON API), mappings (source → account code).

### Scope
**In Scope:** Configuration form, mapping table editor, schedule picker, test export button
**Out of Scope:** Real-time sync configuration (batch only)

### Requirements
**Functional:**
1. Export Scope: Radio buttons (BY_WAREHOUSE, BY_CATEGORY, BY_LOGICAL_WH, TOTAL_ONLY)
2. Schedule: Cron input with presets (Daily 23:00, Weekly Sunday, Monthly 1st)
3. Format: Radio (CSV, JSON_API)
4. API Endpoint: Text input (visible if JSON_API selected)
5. API Key: Password input (encrypted storage)
6. Mappings Table: Source Type dropdown, Source Value dropdown (filtered), Agnum Account Code text, Add/Remove buttons
7. Active: Checkbox (enable/disable export)
8. Save button, Test Export button (manual trigger)

**Non-Functional:**
1. Client-side + server-side validation
2. Responsive design (desktop/tablet)
3. Form load < 1 second

**Data Model:** Blazor page in src/LKvitai.MES.WebUI/Pages/Agnum/Configuration.razor

**API:**
- GET /api/warehouse/v1/agnum/config
- PUT /api/warehouse/v1/agnum/config
- POST /api/warehouse/v1/agnum/export (test export)

### Acceptance Criteria
```gherkin
Scenario: Configure Agnum export
  Given logged in as Admin
  When navigate to /warehouse/agnum/config
  Then configuration form displayed with current settings
  When select scope "BY_WAREHOUSE"
  And set schedule "0 23 * * *" (daily 23:00)
  And add mapping: Warehouse=Main → Account=1500-RAW-MAIN
  And click Save
  Then API PUT /api/warehouse/v1/agnum/config called
  And toast "Configuration saved"

Scenario: Test export manually
  When click "Test Export"
  Then API POST /api/warehouse/v1/agnum/export called
  And toast "Export queued, check history for status"
