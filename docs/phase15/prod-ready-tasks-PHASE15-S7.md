# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 7 (Execution Pack)

**Version:** 2.0
**Date:** February 12, 2026
**Sprint:** Phase 1.5 Sprint 7
**Source:** prod-ready-universe.md, prod-ready-tasks-progress.md
**Status:** SPEC COMPLETE (NO PLACEHOLDERS) - Ready for Codex Execution
**BATON:** 2026-02-12T14:00:00Z-PHASE15-S7-SPEC-COMPLETE-x9k4p2w7

---

## Sprint Overview

**Sprint Goal:** Complete valuation, Agnum integration, 3D visualization, cycle counting, label printing, and inter-warehouse transfers for production readiness.

**Sprint Duration:** 2 weeks
**Total Tasks:** 20
**Estimated Effort:** 19 days

**Focus Areas:**
1. **Valuation:** Cost streams, adjustments, landed cost, write-downs, UI (5 tasks)
2. **Agnum Integration:** Configuration, export job, reconciliation (3 tasks)
3. **3D Visualization:** Coordinates, 3D rendering, 2D toggle (3 tasks)
4. **Cycle Counting:** Scheduling, execution, discrepancy resolution, UI (4 tasks)
5. **Label Printing:** ZPL templates, TCP 9100, print queue (3 tasks)
6. **Inter-Warehouse Transfers:** Workflow, UI (2 tasks)

**Dependencies:**
- Sprint 6 complete (PRD-1581 to PRD-1600)

---

## Sprint 7 Task Index

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-1601 | Valuation | Valuation Stream & Events | M | PRD-1511 | Backend/API | Universe §4.Epic C |
| PRD-1602 | Valuation | Cost Adjustment Command | M | PRD-1601 | Backend/API | Universe §4.Epic C |
| PRD-1603 | Valuation | Landed Cost Allocation | M | PRD-1601 | Backend/API | Universe §4.Epic C |
| PRD-1604 | Valuation | Write-Down Command | M | PRD-1601 | Backend/API | Universe §4.Epic C |
| PRD-1605 | Valuation | Valuation UI & Reports | L | PRD-1601-1604 | UI | Universe §4.Epic C |
| PRD-1606 | Agnum | Agnum Configuration UI | M | PRD-1514 | UI | Universe §4.Epic D |
| PRD-1607 | Agnum | Agnum Export Job | M | PRD-1514,1515 | Backend/API | Universe §4.Epic D |
| PRD-1608 | Agnum | Agnum Reconciliation Report | M | PRD-1607 | UI/Backend | Universe §4.Epic D |
| PRD-1609 | 3D Viz | Location 3D Coordinates | M | PRD-1517 | Backend/API | Universe §4.Epic E |
| PRD-1610 | 3D Viz | 3D Warehouse Rendering | L | PRD-1609 | UI | Universe §4.Epic E |
| PRD-1611 | 3D Viz | 2D/3D Toggle & Interaction | M | PRD-1610 | UI | Universe §4.Epic E |
| PRD-1612 | Cycle Count | Cycle Count Scheduling | M | PRD-1520 | Backend/API | Universe §4.Epic M |
| PRD-1613 | Cycle Count | Cycle Count Execution | M | PRD-1612 | Backend/API | Universe §4.Epic M |
| PRD-1614 | Cycle Count | Discrepancy Resolution | M | PRD-1613 | Backend/API | Universe §4.Epic M |
| PRD-1615 | Cycle Count | Cycle Count UI | M | PRD-1612-1614 | UI | Universe §4.Epic M |
| PRD-1616 | Label Print | ZPL Template Engine | M | PRD-1516 | Backend/API | Universe §4.Epic F |
| PRD-1617 | Label Print | TCP 9100 Printer Integration | M | PRD-1616 | Backend/API | Universe §4.Epic F |
| PRD-1618 | Label Print | Print Queue & Retry | M | PRD-1617 | Backend/API | Universe §4.Epic F |
| PRD-1619 | Transfers | Inter-Warehouse Transfer Workflow | M | PRD-1519 | Backend/API | Universe §4.Epic G |
| PRD-1620 | Transfers | Inter-Warehouse Transfer UI | M | PRD-1619 | UI | Universe §4.Epic G |

**Total Effort:** 19 days

---
## Task PRD-1601: Valuation Stream & Events

**Epic:** Valuation | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1511
**SourceRefs:** Universe §4.Epic C (Valuation)

### Context
Financial valuation must be independent from physical quantities. Need event-sourced valuation stream to track cost changes over time for audit and compliance.

### Scope
**In Scope:** ItemValuation aggregate, ValuationInitialized/CostAdjusted/LandedCostApplied/WrittenDown events, stream per item
**Out of Scope:** Multi-currency valuation (single currency for Phase 1.5)

### Requirements
**Functional:**
1. ItemValuation aggregate with stream ID: `valuation-item-{itemId}`
2. Events: ValuationInitialized (initial cost), CostAdjusted (revaluation), LandedCostApplied (freight/duties), WrittenDown (damage/obsolescence)
3. Commands: InitializeValuation, AdjustCost, ApplyLandedCost, WriteDown
4. Validation: Cost >= 0, reason required for adjustments, approver required for write-downs > $1000

**Non-Functional:**
1. Event versioning support (upcasting for schema changes)
2. Audit trail: all cost changes logged with operator, timestamp, reason
3. Performance: valuation queries < 100ms (indexed by itemId)

**Data Model:**
```csharp
public record ValuationInitialized(int ItemId, decimal InitialCost, string Reason, DateTime Timestamp);
public record CostAdjusted(int ItemId, decimal OldCost, decimal NewCost, string Reason, string ApprovedBy, DateTime Timestamp);
public record LandedCostApplied(int ItemId, decimal FreightCost, decimal DutyCost, decimal TotalLandedCost, DateTime Timestamp);
public record WrittenDown(int ItemId, decimal OldValue, decimal NewValue, string Reason, string ApprovedBy, DateTime Timestamp);
```

**API:**
```http
POST /api/warehouse/v1/valuation/initialize
POST /api/warehouse/v1/valuation/adjust-cost
POST /api/warehouse/v1/valuation/apply-landed-cost
POST /api/warehouse/v1/valuation/write-down
```

### Acceptance Criteria
```gherkin
Scenario: Initialize valuation for new item
  Given Item RM-0001 exists with no valuation
  When POST /api/warehouse/v1/valuation/initialize with ItemId=1, InitialCost=10.50, Reason="Initial purchase"
  Then ValuationInitialized event appended to stream valuation-item-1
  And response status 200

Scenario: Adjust cost with approval
  Given Item RM-0001 has current cost $10.50
  When POST /api/warehouse/v1/valuation/adjust-cost with ItemId=1, NewCost=12.00, Reason="Supplier price increase", ApprovedBy="manager@example.com"
  Then CostAdjusted event appended
  And audit log includes approver and reason

Scenario: Write-down requires approval for large amounts
  Given Item FG-0002 has value $5000
  When POST /api/warehouse/v1/valuation/write-down with ItemId=2, NewValue=3000, Reason="Damage"
  Then validation requires ApprovedBy field (amount > $1000)
  And response status 400 if approver missing
```

### Validation
```bash
# Get dev token
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"admin","roles":["Admin"]}' | jq -r '.token' > /tmp/token.txt
TOKEN=$(cat /tmp/token.txt)

# Initialize valuation
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/valuation/initialize \
  -H "Content-Type: application/json" -d '{"commandId":"'$(uuidgen)'","itemId":1,"initialCost":10.50,"reason":"Initial"}'
```

### Definition of Done
- [ ] ItemValuation aggregate created in src/LKvitai.MES.Domain/Aggregates/
- [ ] 4 event types defined with versioning support
- [ ] Command handlers implemented with validation
- [ ] Marten stream configuration in MartenConfiguration.cs
- [ ] Unit tests: command validation, event application (15+ tests)
- [ ] Integration test: full valuation lifecycle
- [ ] API endpoints exposed in ValuationController.cs
- [ ] Audit logging implemented
- [ ] Documentation updated in docs/

---

## Task PRD-1602: Cost Adjustment Command

**Epic:** Valuation | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1601
**SourceRefs:** Universe §4.Epic C

### Context
Need ability to adjust item costs for revaluation (supplier price changes, market adjustments). Must maintain audit trail and require approval for significant changes.

### Scope
**In Scope:** AdjustCost command, validation rules, approval workflow, projection update
**Out of Scope:** Automated cost adjustment from market data (manual only for Phase 1.5)

### Requirements
**Functional:**
1. AdjustCost command: ItemId, NewCost, Reason, ApprovedBy
2. Validation: NewCost >= 0, Reason required (min 10 chars), ApprovedBy required if delta > 20%
3. CostAdjusted event emission
4. OnHandValue projection update (recalculate on-hand value = qty × new cost)

**Non-Functional:**
1. Idempotency: duplicate commands with same CommandId ignored
2. Concurrency: optimistic locking on aggregate version
3. Performance: adjustment processing < 500ms

**Data Model:**
```csharp
public record AdjustCostCommand(Guid CommandId, int ItemId, decimal NewCost, string Reason, string? ApprovedBy);
```

**API:**
```http
POST /api/warehouse/v1/valuation/adjust-cost
{
  "commandId": "uuid",
  "itemId": 1,
  "newCost": 12.50,
  "reason": "Supplier price increase effective 2026-02-01",
  "approvedBy": "manager@example.com"
}
```

### Acceptance Criteria
```gherkin
Scenario: Adjust cost with valid approval
  Given Item RM-0001 current cost $10.00
  When AdjustCost command with NewCost=$12.00, Reason="Price increase", ApprovedBy="manager@example.com"
  Then CostAdjusted event emitted
  And OnHandValue projection updated
  And response status 200

Scenario: Reject adjustment without approval for large delta
  Given Item RM-0001 current cost $10.00
  When AdjustCost command with NewCost=$15.00 (50% increase), no ApprovedBy
  Then validation fails with "Approval required for cost change > 20%"
  And response status 400

Scenario: Idempotency check
  Given AdjustCost command already processed with CommandId=X
  When same command resubmitted
  Then no duplicate event emitted
  And response status 200 (idempotent)
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/valuation/adjust-cost \
  -H "Content-Type: application/json" \
  -d '{"commandId":"'$(uuidgen)'","itemId":1,"newCost":12.50,"reason":"Supplier price increase","approvedBy":"manager@example.com"}'
```

### Definition of Done
- [ ] AdjustCost command handler implemented
- [ ] Validation rules enforced (cost >= 0, reason required, approval threshold)
- [ ] CostAdjusted event emission
- [ ] OnHandValue projection handler
- [ ] Idempotency check
- [ ] Optimistic locking
- [ ] Unit tests: validation, approval logic (15+ tests)
- [ ] Integration test: adjust cost → verify projection
- [ ] API endpoint exposed

---

## Task PRD-1603: Landed Cost Allocation

**Epic:** Valuation | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1601
**SourceRefs:** Universe §4.Epic C

### Context
Landed cost (freight, duties, insurance) must be allocated to inventory unit cost for accurate valuation. Need to distribute costs across shipment items proportionally.

### Scope
**In Scope:** ApplyLandedCost command, proportional allocation algorithm, LandedCostApplied event
**Out of Scope:** Automated duty calculation (manual entry for Phase 1.5)

### Requirements
**Functional:**
1. ApplyLandedCost command: ShipmentId, FreightCost, DutyCost, InsuranceCost
2. Allocation algorithm: distribute costs proportionally by item value (qty × unit cost)
3. LandedCostApplied event per item
4. OnHandValue projection update

**Non-Functional:**
1. Allocation accuracy: rounding errors < $0.01 per item
2. Performance: allocation for 100-item shipment < 1 second

**Data Model:**
```csharp
public record ApplyLandedCostCommand(Guid CommandId, Guid ShipmentId, decimal FreightCost, decimal DutyCost, decimal InsuranceCost);
public record LandedCostApplied(int ItemId, decimal FreightCost, decimal DutyCost, decimal InsuranceCost, decimal TotalLandedCost, DateTime Timestamp);
```

**API:**
```http
POST /api/warehouse/v1/valuation/apply-landed-cost
{
  "commandId": "uuid",
  "shipmentId": "uuid",
  "freightCost": 500.00,
  "dutyCost": 200.00,
  "insuranceCost": 50.00
}
```

### Acceptance Criteria
```gherkin
Scenario: Allocate landed cost proportionally
  Given Shipment ISH-0001 with 2 items: RM-0001 (qty=100, cost=$10) and RM-0002 (qty=50, cost=$20)
  And total shipment value = $2000 (100×10 + 50×20)
  When ApplyLandedCost with FreightCost=$200
  Then RM-0001 allocated $100 freight (50% of value)
  And RM-0002 allocated $100 freight (50% of value)
  And LandedCostApplied events emitted for both items

Scenario: Handle rounding errors
  Given Shipment with 3 items, total freight $100.00
  When allocation results in $33.33, $33.33, $33.34
  Then total allocated = $100.00 (no rounding loss)

Scenario: Reject negative costs
  When ApplyLandedCost with FreightCost=-50
  Then validation fails with "Costs must be >= 0"
  And response status 400
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/valuation/apply-landed-cost \
  -H "Content-Type: application/json" \
  -d '{"commandId":"'$(uuidgen)'","shipmentId":"'$(uuidgen)'","freightCost":500,"dutyCost":200,"insuranceCost":50}'
```

### Definition of Done
- [ ] ApplyLandedCost command handler
- [ ] Proportional allocation algorithm
- [ ] Rounding error handling
- [ ] LandedCostApplied event emission
- [ ] OnHandValue projection update
- [ ] Unit tests: allocation logic, rounding (15+ tests)
- [ ] Integration test: full landed cost flow
- [ ] API endpoint exposed
- [ ] Documentation: allocation algorithm

---

## Task PRD-1604: Write-Down Command

**Epic:** Valuation | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1601 | **SourceRefs:** Universe §4.Epic C

### Context
Stock damage, obsolescence, or shrinkage requires write-down (reduce value without changing quantity). Need approval workflow for financial accuracy.

### Scope
**In Scope:** WriteDown command, approval workflow, WrittenDown event, OnHandValue projection update
**Out of Scope:** Automated write-down triggers (manual only)

### Requirements
**Functional:**
1. WriteDown command: ItemId, NewValue, Reason, ApprovedBy (required if delta > $1000)
2. Validation: NewValue >= 0, NewValue < CurrentValue, Reason required
3. WrittenDown event emission
4. OnHandValue projection update (value reduced, qty unchanged)

**Non-Functional:**
1. Approval workflow: Manager role required for write-downs > $1000
2. Audit trail: all write-downs logged with approver, timestamp, reason
3. Performance: write-down processing < 500ms

**Data Model:**
```csharp
public record WriteDownCommand(Guid CommandId, int ItemId, decimal NewValue, string Reason, string ApprovedBy);
public record WrittenDown(int ItemId, decimal OldValue, decimal NewValue, string Reason, string ApprovedBy, DateTime Timestamp);
```

**API:** `POST /api/warehouse/v1/valuation/write-down`

### Acceptance Criteria
```gherkin
Scenario: Write-down with approval
  Given Item FG-0001 current value $5000
  When WriteDown command with NewValue=$3000, Reason="Damage", ApprovedBy="manager@example.com"
  Then WrittenDown event emitted
  And OnHandValue projection updated (value=$3000, qty unchanged)

Scenario: Reject write-down without approval for large amount
  Given Item FG-0001 value $5000
  When WriteDown with NewValue=$2000 (delta=$3000), no ApprovedBy
  Then validation fails "Approval required for write-down > $1000"

Scenario: Reject write-down increasing value
  Given Item RM-0001 value $100
  When WriteDown with NewValue=$150
  Then validation fails "Write-down must reduce value"
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/valuation/write-down \
  -H "Content-Type: application/json" -d '{"commandId":"'$(uuidgen)'","itemId":1,"newValue":3000,"reason":"Damage","approvedBy":"manager@example.com"}'
```

### Definition of Done
- [ ] WriteDown command handler
- [ ] Approval validation (threshold $1000)
- [ ] WrittenDown event emission
- [ ] OnHandValue projection handler
- [ ] Unit tests: validation, approval logic (15+ tests)
- [ ] Integration test: write-down flow
- [ ] API endpoint exposed
- [ ] Audit logging
- [ ] Documentation updated

---

## Task PRD-1605: Valuation UI & Reports

**Epic:** Valuation | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** L (2 days)
**OwnerType:** UI | **Dependencies:** PRD-1601-1604 | **SourceRefs:** Universe §4.Epic C

### Context
Valuation dashboard, cost adjustment/landed cost/write-down forms, on-hand value report, cost history report with CSV export

### Scope
**In Scope:** Valuation dashboard, cost adjustment/landed cost/write-down forms, on-hand value report, cost history report with CSV export
**Out of Scope:** Real-time cost alerts (deferred to Phase 2)

### Requirements
**Functional:**
1. Dashboard with summary cards (total on-hand value, items with recent cost changes, pending approvals)
2. Cost Adjustment form (ItemId dropdown, NewCost, Reason, ApprovedBy)
3. Landed Cost form (ShipmentId dropdown, FreightCost, DutyCost, InsuranceCost)
4. Write-Down form (ItemId dropdown, NewValue, Reason, ApprovedBy)
5. On-Hand Value report (Item, Qty, UnitCost, TotalValue) with CSV export
6. Cost History report (Item, Date, OldCost, NewCost, Reason, ApprovedBy) with filters

**Non-Functional:**
1. Responsive design (tablet/desktop)
2. Client-side + server-side validation
3. Dashboard load < 2 seconds

**Data Model:** Blazor components in src/LKvitai.MES.WebUI/Pages/Valuation/

**API:**
- GET /api/warehouse/v1/valuation/on-hand-value
- GET /api/warehouse/v1/valuation/cost-history

### Acceptance Criteria
```gherkin
Scenario: View on-hand value report
  Given logged in as Manager
  When navigate to /warehouse/valuation/dashboard
  Then on-hand value report displayed with all items
  And total on-hand value calculated correctly

Scenario: Adjust cost via UI
  When click "Adjust Cost"
  And fill form with ItemId=1, NewCost=12.50, Reason="Price increase", ApprovedBy="manager@example.com"
  Then API POST /api/warehouse/v1/valuation/adjust-cost called
  And toast "Cost adjusted successfully"

Scenario: Export on-hand value to CSV
  When click "Export CSV"
  Then CSV file downloaded with columns: Item, Qty, UnitCost, TotalValue
```

### Validation
```bash
dotnet run --project src/LKvitai.MES.WebUI
# Navigate to http://localhost:5001/warehouse/valuation/dashboard
# Test all forms and reports manually
```

### Definition of Done
- [ ] Valuation Dashboard page created (Dashboard.razor)
- [ ] Cost Adjustment form implemented (AdjustCost.razor)
- [ ] Landed Cost form implemented (ApplyLandedCost.razor)
- [ ] Write-Down form implemented (WriteDown.razor)
- [ ] On-Hand Value report with CSV export
- [ ] Cost History report with filters
- [ ] Client-side validation
- [ ] Responsive design tested
- [ ] Manual test completed

---


## Task PRD-1606: Agnum Configuration UI

**Epic:** Agnum Integration | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** UI | **Dependencies:** PRD-1514
**SourceRefs:** Universe §4.Epic D (Agnum Accounting Integration)

### Context
Inventory accountants need UI to configure Agnum export settings: schedule, scope, mappings (warehouse/category → GL account codes), API credentials.

### Scope
**In Scope:** Configuration page, mapping table, schedule picker, API endpoint/key fields, test connection button
**Out of Scope:** Real-time sync (batch only), two-way sync

### Requirements
**Functional:**
1. Configuration form with fields: ExportScope (BY_WAREHOUSE, BY_CATEGORY, BY_LOGICAL_WH, TOTAL_ONLY), Schedule (cron expression with presets), Format (CSV, JSON_API), ApiEndpoint, ApiKey (encrypted)
2. Mappings table: SourceType dropdown, SourceValue dropdown (filtered by type), AgnumAccountCode text input, Add/Remove buttons
3. Test Connection button (validates API endpoint reachable, credentials valid)
4. Active checkbox (enable/disable scheduled export)
5. Save button (validates at least 1 mapping if scope != TOTAL_ONLY)

**Non-Functional:**
1. Client-side + server-side validation
2. API key masked in UI (password field), encrypted at rest
3. Page load < 2 seconds

**Data Model:** Blazor page in src/LKvitai.MES.WebUI/Pages/Agnum/Configuration.razor

**API:**
- GET /api/warehouse/v1/agnum/config
- PUT /api/warehouse/v1/agnum/config
- POST /api/warehouse/v1/agnum/test-connection

**UI (Blazor):**
- Route: /warehouse/agnum/config
- Components: Form with dropdowns, text inputs, table for mappings
- Validations: Required fields, cron syntax validation, at least 1 mapping
- Empty states: "No mappings configured. Add your first mapping."

### Acceptance Criteria
```gherkin
Scenario: Configure Agnum export with mappings
  Given logged in as Inventory Accountant
  When navigate to /warehouse/agnum/config
  And select ExportScope "BY_CATEGORY"
  And set Schedule "0 23 * * *" (daily 23:00)
  And select Format "CSV"
  And add mapping: SourceType=CATEGORY, SourceValue="Raw Materials", AgnumAccountCode="1500-RAW"
  And add mapping: SourceType=CATEGORY, SourceValue="Finished Goods", AgnumAccountCode="1510-FG"
  And check Active
  And click Save
  Then API PUT /api/warehouse/v1/agnum/config called
  And toast "Agnum configuration saved successfully"

Scenario: Test API connection
  Given Agnum config form filled with ApiEndpoint and ApiKey
  When click "Test Connection"
  Then API POST /api/warehouse/v1/agnum/test-connection called
  And if success show toast "Connection successful"
  And if failure show error "Connection failed: Invalid API key"

Scenario: Validation error for missing mappings
  Given ExportScope "BY_CATEGORY"
  And no mappings added
  When click Save
  Then validation error "At least 1 mapping required for scope BY_CATEGORY"
  And form not submitted
```

### Validation
```bash
dotnet run --project src/LKvitai.MES.WebUI
# Navigate to http://localhost:5001/warehouse/agnum/config
# Test all form fields, validations, save, test connection
```

### Definition of Done
- [ ] Configuration.razor page created in src/LKvitai.MES.WebUI/Pages/Agnum/
- [ ] Form fields implemented with dropdowns, text inputs, password field for API key
- [ ] Mappings table with Add/Remove functionality
- [ ] Test Connection button with API call
- [ ] Client-side validation (required fields, cron syntax)
- [ ] Server-side validation in API
- [ ] Responsive design (tablet/desktop)
- [ ] Manual test completed (all scenarios)
- [ ] Documentation updated

---

## Task PRD-1607: Agnum Export Job

**Epic:** Agnum Integration | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1514, PRD-1515
**SourceRefs:** Universe §4.Epic D

### Context
Scheduled job queries on-hand value (qty × cost), applies mappings, generates CSV/JSON, sends to Agnum API or file storage. Runs daily at configured time.

### Scope
**In Scope:** Hangfire recurring job, query AvailableStock + ItemValuation, apply mappings, generate CSV/JSON, send to API or save file, record history
**Out of Scope:** Real-time sync, two-way sync

### Requirements
**Functional:**
1. AgnumExportJob (Hangfire recurring job) triggered by cron schedule from config
2. Query: JOIN AvailableStock (qty) + ItemValuation (cost) + Item (category) → compute on-hand value
3. Apply mappings: Group by Agnum account code per config (BY_WAREHOUSE, BY_CATEGORY, BY_LOGICAL_WH, TOTAL_ONLY)
4. Generate output: CSV format (ExportDate, AccountCode, SKU, ItemName, Quantity, UnitCost, OnHandValue) OR JSON payload
5. Send: If CSV → save to blob storage (exports/agnum/{date}/export-{timestamp}.csv), If JSON_API → POST to configured endpoint
6. Record history: Insert AgnumExportHistory (ExportNumber, ExportedAt, Status, RowCount, FilePath, ErrorMessage)
7. Retry logic: If API fails, retry 3x with exponential backoff (1h, 2h, 4h), then mark FAILED
8. Notification: Email accountant on success/failure

**Non-Functional:**
1. Idempotency: Include exportId in API header X-Export-ID
2. Performance: Export 10k items < 30 seconds
3. Reliability: 99.9% success rate (with retries)

**Data Model:**
```csharp
public class AgnumExportHistory {
  public Guid Id { get; set; }
  public string ExportNumber { get; set; } // AUTO-AGNUM-20260210-001
  public DateTime ExportedAt { get; set; }
  public ExportStatus Status { get; set; } // SUCCESS, FAILED, RETRYING
  public int RowCount { get; set; }
  public string FilePath { get; set; }
  public string ErrorMessage { get; set; }
  public int RetryCount { get; set; }
}
```

**API:**
- POST /api/warehouse/v1/agnum/export (manual trigger)
- GET /api/warehouse/v1/agnum/history

### Acceptance Criteria
```gherkin
Scenario: Scheduled export generates CSV
  Given Agnum config with schedule "0 23 * * *", scope "BY_CATEGORY", format "CSV"
  And mappings: Raw Materials → 1500-RAW, Finished Goods → 1510-FG
  And AvailableStock has 150 items across 2 categories
  When Hangfire triggers job at 23:00
  Then query joins AvailableStock + ItemValuation + Item
  And groups by category, applies mappings
  And generates CSV with 150 rows
  And saves to exports/agnum/2026-02-12/export-230000.csv
  And inserts AgnumExportHistory (Status=SUCCESS, RowCount=150)
  And emails accountant "Agnum export completed: 150 rows"

Scenario: API export with retry on failure
  Given Agnum config with format "JSON_API", endpoint "https://agnum.example.com/api/v1/inventory"
  When export triggered
  And Agnum API returns 503 Service Unavailable
  Then job retries after 1 hour
  And retries after 2 hours
  And retries after 4 hours
  And if still fails marks Status=FAILED
  And alerts admin "Agnum export failed after 3 retries"

Scenario: Manual export trigger
  Given logged in as Inventory Accountant
  When POST /api/warehouse/v1/agnum/export
  Then export job runs immediately (bypasses schedule)
  And returns exportId in response
  And user can poll GET /api/warehouse/v1/agnum/history/{exportId} for status
```

### Validation
```bash
# Get dev token
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"accountant","roles":["InventoryAccountant"]}' | jq -r '.token' > /tmp/token.txt
TOKEN=$(cat /tmp/token.txt)

# Manual trigger export
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/agnum/export

# Check history
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/agnum/history
```

### Definition of Done
- [ ] AgnumExportJob class created in src/LKvitai.MES.Api/Services/
- [ ] Hangfire recurring job registered in Program.cs
- [ ] Query logic: JOIN AvailableStock + ItemValuation + Item
- [ ] Mapping application logic (group by account code)
- [ ] CSV generation (using CsvHelper library)
- [ ] JSON API integration (HttpClient with retry policy)
- [ ] File storage (save to exports/agnum/ directory)
- [ ] AgnumExportHistory entity + EF migration
- [ ] Retry logic with exponential backoff
- [ ] Email notification (success/failure)
- [ ] Unit tests: mapping logic, CSV generation (15+ tests)
- [ ] Integration test: full export flow
- [ ] API endpoints exposed
- [ ] Documentation updated

---

## Task PRD-1608: Agnum Reconciliation Report

**Epic:** Agnum Integration | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** UI/Backend | **Dependencies:** PRD-1607
**SourceRefs:** Universe §4.Epic D

### Context
Accountants need to reconcile warehouse on-hand value vs Agnum GL balance. Report compares warehouse data (from last export) vs Agnum balance (manual upload or API query).

### Scope
**In Scope:** Reconciliation report page, date picker, variance calculation, CSV export
**Out of Scope:** Automated Agnum balance fetch (manual upload for Phase 1.5)

### Requirements
**Functional:**
1. Reconciliation report API: Query last export data (from AgnumExportHistory + file), compare to Agnum balance (manual upload CSV or API query)
2. Report columns: SKU, ItemName, WarehouseQty, WarehouseCost, WarehouseValue, AgnumBalance, Variance (WarehouseValue - AgnumBalance), VariancePercent
3. Filters: Date (default: yesterday), AccountCode, Variance threshold (show only variances > $100 or > 5%)
4. Variance summary: Total variance, Count of items with variance, Largest variance
5. CSV export

**Non-Functional:**
1. Report generation < 5 seconds (for 10k items)
2. Variance calculation accuracy: ±$0.01

**Data Model:** Blazor page in src/LKvitai.MES.WebUI/Pages/Agnum/Reconciliation.razor

**API:**
- POST /api/warehouse/v1/agnum/reconcile (input: date, Agnum balance CSV upload)
- GET /api/warehouse/v1/agnum/reconcile/{reportId}

**UI (Blazor):**
- Route: /warehouse/agnum/reconcile
- Components: Date picker, File upload (Agnum balance CSV), Generate Report button, Variance table, Export CSV button
- Validations: Date required, CSV format validation
- Empty states: "No reconciliation report generated. Upload Agnum balance and click Generate."

### Acceptance Criteria
```gherkin
Scenario: Generate reconciliation report
  Given logged in as Inventory Accountant
  And last Agnum export on 2026-02-11 with 150 items
  When navigate to /warehouse/agnum/reconcile
  And select date "2026-02-11"
  And upload Agnum balance CSV (150 rows)
  And click "Generate Report"
  Then API POST /api/warehouse/v1/agnum/reconcile called
  And report displays 150 items with variance calculation
  And summary shows: Total variance=$250, Items with variance=5, Largest variance=$100 (SKU RM-0001)

Scenario: Filter variances by threshold
  Given reconciliation report generated with 150 items
  When set variance threshold "> $50"
  Then table filters to show only 8 items with variance > $50
  And summary updates: Total variance=$220, Items=8

Scenario: Export reconciliation report to CSV
  Given reconciliation report displayed
  When click "Export CSV"
  Then CSV file downloaded with columns: SKU, ItemName, WarehouseQty, WarehouseCost, WarehouseValue, AgnumBalance, Variance, VariancePercent
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
# Generate reconciliation report (requires Agnum balance CSV upload via UI)
# Manual test: Navigate to /warehouse/agnum/reconcile, upload CSV, generate report, verify variance calculations
```

### Definition of Done
- [ ] Reconciliation.razor page created in src/LKvitai.MES.WebUI/Pages/Agnum/
- [ ] Date picker, file upload, generate report button
- [ ] Variance table with filters
- [ ] CSV export functionality
- [ ] API endpoint POST /api/warehouse/v1/agnum/reconcile
- [ ] Reconciliation logic: compare warehouse vs Agnum balance
- [ ] Variance calculation (absolute and percentage)
- [ ] Summary statistics (total variance, count, largest)
- [ ] Unit tests: variance calculation logic (15+ tests)
- [ ] Integration test: full reconciliation flow
- [ ] Manual test completed
- [ ] Documentation updated

---
## Task PRD-1609: Location 3D Coordinates

**Epic:** 3D Visualization | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1517
**SourceRefs:** Universe §4.Epic E (3D/2D Warehouse Visualization)

### Context
Add 3D coordinates (X, Y, Z) to Location entity for visual warehouse rendering. Coordinates represent meters from warehouse origin (corner).

### Scope
**In Scope:** Location entity update (add CoordinateX, CoordinateY, CoordinateZ, CapacityWeight, CapacityVolume), EF migration, API to update coords, bulk upload CSV
**Out of Scope:** Real-time location tracking (RTLS)

### Requirements
**Functional:**
1. Update Location entity: Add CoordinateX (decimal, nullable), CoordinateY (decimal, nullable), CoordinateZ (decimal, nullable), CapacityWeight (decimal, nullable, kg), CapacityVolume (decimal, nullable, m³)
2. EF migration: Add columns to locations table
3. API endpoint: PUT /api/warehouse/v1/locations/{code} (update coords + capacity)
4. Bulk upload: POST /api/warehouse/v1/locations/bulk-coordinates (CSV upload: LocationCode, X, Y, Z, CapacityWeight, CapacityVolume)
5. Validation: X, Y, Z >= 0, no overlapping bins (check 3D bounding boxes if capacity defined)

**Non-Functional:**
1. Bulk upload: 1000 locations < 10 seconds
2. Coordinate precision: 2 decimal places (cm accuracy)

**Data Model:**
```csharp
public class Location {
  // Existing fields...
  public decimal? CoordinateX { get; set; }
  public decimal? CoordinateY { get; set; }
  public decimal? CoordinateZ { get; set; }
  public decimal? CapacityWeight { get; set; }
  public decimal? CapacityVolume { get; set; }
}
```

**API:**
- PUT /api/warehouse/v1/locations/{code}
- POST /api/warehouse/v1/locations/bulk-coordinates

### Acceptance Criteria
```gherkin
Scenario: Update location coordinates
  Given Location "R3-C6-L3B3" exists
  When PUT /api/warehouse/v1/locations/R3-C6-L3B3 with CoordinateX=15.5, CoordinateY=32.0, CoordinateZ=6.0, CapacityWeight=1000, CapacityVolume=2.0
  Then Location updated in database
  And response status 200

Scenario: Bulk upload coordinates via CSV
  Given CSV file with 200 locations (LocationCode, X, Y, Z, CapacityWeight, CapacityVolume)
  When POST /api/warehouse/v1/locations/bulk-coordinates with CSV upload
  Then all 200 locations updated
  And response includes success count=200, errors=0
  And processing time < 10 seconds

Scenario: Validation error for negative coordinates
  When PUT /api/warehouse/v1/locations/R1-C1-L1 with CoordinateX=-5
  Then validation fails "Coordinates must be >= 0"
  And response status 400
```

### Validation
```bash
# Get dev token
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"admin","roles":["Admin"]}' | jq -r '.token' > /tmp/token.txt
TOKEN=$(cat /tmp/token.txt)

# Update location coordinates
curl -H "Authorization: Bearer $TOKEN" -X PUT http://localhost:5000/api/warehouse/v1/locations/R3-C6-L3B3 \
  -H "Content-Type: application/json" \
  -d '{"coordinateX":15.5,"coordinateY":32.0,"coordinateZ":6.0,"capacityWeight":1000,"capacityVolume":2.0}'

# Verify migration applied
dotnet ef database update --project src/LKvitai.MES.Infrastructure
```

### Definition of Done
- [ ] Location entity updated with 5 new fields
- [ ] EF migration created and applied
- [ ] API endpoint PUT /api/warehouse/v1/locations/{code}
- [ ] Bulk upload endpoint POST /api/warehouse/v1/locations/bulk-coordinates
- [ ] CSV parsing logic (using CsvHelper)
- [ ] Validation: coordinates >= 0, no overlapping bins
- [ ] Unit tests: validation logic (15+ tests)
- [ ] Integration test: bulk upload flow
- [ ] API endpoints exposed
- [ ] Documentation updated

---

## Task PRD-1610: 3D Warehouse Rendering

**Epic:** 3D Visualization | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** L (2 days)
**OwnerType:** UI | **Dependencies:** PRD-1609
**SourceRefs:** Universe §4.Epic E

### Context
Interactive 3D warehouse view using Three.js. Render bins as 3D boxes, color-coded by status (empty=gray, low=yellow, full=orange, reserved=blue). Click bin to show details.

### Scope
**In Scope:** 3D rendering (Three.js), camera controls (rotate, zoom, pan), bin click interaction, color coding by utilization, details panel
**Out of Scope:** Real-time updates (manual refresh only), operator location tracking

### Requirements
**Functional:**
1. 3D scene setup: Three.js renderer, camera (PerspectiveCamera), lights (AmbientLight + DirectionalLight), OrbitControls
2. Bin rendering: Query GET /api/warehouse/v1/visualization/3d → render each bin as BoxGeometry with coordinates (X, Y, Z) and size (1m × 1m × 1m default)
3. Color coding: Empty (gray #808080), Low <50% utilization (yellow #FFFF00), Full >80% utilization (orange #FFA500), Reserved (blue #0000FF), Over capacity (red #FF0000)
4. Utilization calculation: (OnHandQty / CapacityWeight) × 100 (if capacity defined, else gray)
5. Click interaction: Raycaster detects bin click → highlight bin (gold #FFD700) → show details panel (right side)
6. Details panel: Location code, Capacity utilization %, Handling units list (HU code, SKU, Qty), "View Details" button (link to /warehouse/locations/{id})
7. Camera controls: Mouse drag (rotate), Scroll (zoom), Shift+drag (pan)
8. Refresh button: Reload data from API

**Non-Functional:**
1. Rendering performance: 1000 bins render < 3 seconds
2. Interaction latency: Click → details panel < 100ms
3. Responsive design: Canvas resizes with window

**Data Model:** Blazor page in src/LKvitai.MES.WebUI/Pages/Visualization/Warehouse3D.razor

**API:**
- GET /api/warehouse/v1/visualization/3d (returns bins with coords, status, HUs)

**UI (Blazor):**
- Route: /warehouse/visualization/3d
- Components: Canvas (Three.js), Search box (location code), Filters (zone, status), Details panel (right side), Refresh button
- Validations: N/A (read-only)
- Empty states: "No bins configured with coordinates. Configure layout first."

### Acceptance Criteria
```gherkin
Scenario: Render 3D warehouse with 200 bins
  Given 200 locations configured with coordinates
  When navigate to /warehouse/visualization/3d
  Then API GET /api/warehouse/v1/visualization/3d called
  And 200 bins rendered as 3D boxes
  And bins colored by status (50 gray, 100 yellow, 40 orange, 10 blue)
  And camera positioned to view entire warehouse
  And rendering time < 3 seconds

Scenario: Click bin to view details
  Given 3D warehouse view loaded
  When user clicks bin "R3-C6-L3B3"
  Then bin highlights in gold
  And details panel opens on right side
  And displays: Location code "R3-C6-L3B3", Utilization "85%", HUs: HU-001 (RM-0001, 50 units), HU-002 (RM-0002, 30 units)
  And "View Details" button links to /warehouse/locations/{id}

Scenario: Camera controls
  Given 3D warehouse view loaded
  When user drags mouse
  Then camera rotates around warehouse
  When user scrolls mouse wheel
  Then camera zooms in/out
  When user shift+drags
  Then camera pans (moves without rotating)
```

### Validation
```bash
dotnet run --project src/LKvitai.MES.WebUI
# Navigate to http://localhost:5001/warehouse/visualization/3d
# Test: Rendering, color coding, click interaction, camera controls, refresh button
```

### Definition of Done
- [ ] Warehouse3D.razor page created in src/LKvitai.MES.WebUI/Pages/Visualization/
- [ ] Three.js library integrated (via CDN or npm)
- [ ] 3D scene setup (renderer, camera, lights, OrbitControls)
- [ ] Bin rendering logic (BoxGeometry per location)
- [ ] Color coding by utilization
- [ ] Click interaction (Raycaster + highlight)
- [ ] Details panel component
- [ ] Camera controls (rotate, zoom, pan)
- [ ] Refresh button
- [ ] API endpoint GET /api/warehouse/v1/visualization/3d
- [ ] Responsive design (canvas resizes)
- [ ] Manual test completed (all scenarios)
- [ ] Documentation updated

---

## Task PRD-1611: 2D/3D Toggle & Interaction

**Epic:** 3D Visualization | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** UI | **Dependencies:** PRD-1610
**SourceRefs:** Universe §4.Epic E

### Context
Add 2D floor plan (top-down view) as alternative to 3D. Toggle button switches between 2D/3D. Search location by code → fly to bin and highlight.

### Scope
**In Scope:** 2D rendering (SVG or Canvas), toggle button, search box with fly-to animation, color legend
**Out of Scope:** Heatmap, path optimization

### Requirements
**Functional:**
1. 2D floor plan: Render bins as rectangles (top-down view, Z coordinate ignored), same color coding as 3D
2. Toggle button: Switch between 2D/3D views (preserves selected bin)
3. Search box: Type location code → autocomplete suggestions → select → fly camera to bin (animated) → highlight bin → open details panel
4. Color legend: Display legend (gray=Empty, yellow=Low, orange=Full, blue=Reserved, red=Over capacity)
5. Zoom controls: +/- buttons for 2D zoom

**Non-Functional:**
1. 2D rendering: 1000 bins < 2 seconds
2. Search autocomplete: < 200ms
3. Fly-to animation: 1 second duration

**Data Model:** Extend Warehouse3D.razor with 2D rendering logic

**API:** Same as PRD-1610 (GET /api/warehouse/v1/visualization/3d)

**UI (Blazor):**
- Route: /warehouse/visualization/3d (same page, toggle changes view)
- Components: Toggle button (2D/3D), Search box with autocomplete, Color legend, Zoom controls (+/-)
- Validations: Search input matches existing location codes
- Empty states: "No bins found matching search."

### Acceptance Criteria
```gherkin
Scenario: Toggle between 2D and 3D views
  Given 3D warehouse view loaded
  When click "2D View" toggle button
  Then view switches to 2D floor plan (top-down)
  And bins rendered as rectangles
  And same color coding applied
  When click "3D View" toggle button
  Then view switches back to 3D

Scenario: Search location and fly to it
  Given 3D warehouse view loaded
  When type "R5-C2" in search box
  And select from autocomplete suggestions
  Then camera flies to bin R5-C2 (animated, 1 second)
  And bin highlights in gold
  And details panel opens

Scenario: Color legend displayed
  Given warehouse view loaded
  Then color legend displayed in bottom-left corner
  And shows: Gray=Empty, Yellow=Low, Orange=Full, Blue=Reserved, Red=Over capacity
```

### Validation
```bash
dotnet run --project src/LKvitai.MES.WebUI
# Navigate to http://localhost:5001/warehouse/visualization/3d
# Test: Toggle 2D/3D, search with fly-to, color legend, zoom controls
```

### Definition of Done
- [ ] 2D rendering logic added to Warehouse3D.razor (SVG or Canvas)
- [ ] Toggle button (2D/3D) with state management
- [ ] Search box with autocomplete (query locations API)
- [ ] Fly-to animation (camera transition)
- [ ] Color legend component
- [ ] Zoom controls for 2D (+/- buttons)
- [ ] Unit tests: search autocomplete logic (10+ tests)
- [ ] Manual test completed (all scenarios)
- [ ] Documentation updated
## Task PRD-1612: Cycle Count Scheduling

**Epic:** Cycle Counting | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1520
**SourceRefs:** Universe §4.Epic M (Cycle Counting)

### Context
Scheduled physical inventory verification with ABC classification (A-monthly, B-quarterly, C-annual). Manager creates cycle count, assigns locations/operators.

### Scope
**In Scope:** CycleCount entity, scheduling API, ABC classification logic, operator assignment
**Out of Scope:** Automated ABC classification (manual category assignment for Phase 1.5)

### Requirements
**Functional:**
1. CycleCount entity: Id, CountNumber (AUTO-CC-20260212-001), ScheduledDate, Status (SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED), ABCClass (A, B, C, ALL), AssignedOperator, Locations (list), CreatedBy, CreatedAt
2. CycleCountLine entity: Id, CycleCountId, LocationId, ItemId, SystemQty (from AvailableStock), PhysicalQty (counted), Variance (PhysicalQty - SystemQty), CountedAt, CountedBy
3. API endpoint: POST /api/warehouse/v1/cycle-counts/schedule (input: ScheduledDate, ABCClass, Locations[], AssignedOperator)
4. ABC classification: Query items by category prefix (A/B/C) or manual location selection
5. Validation: ScheduledDate >= today, at least 1 location, operator exists

**Non-Functional:**
1. Scheduling: < 1 second
2. Support 1000 locations per cycle count

**Data Model:**
```csharp
public class CycleCount {
  public Guid Id { get; set; }
  public string CountNumber { get; set; }
  public DateTime ScheduledDate { get; set; }
  public CycleCountStatus Status { get; set; }
  public string ABCClass { get; set; } // A, B, C, ALL
  public string AssignedOperator { get; set; }
  public List<int> LocationIds { get; set; }
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class CycleCountLine {
  public Guid Id { get; set; }
  public Guid CycleCountId { get; set; }
  public int LocationId { get; set; }
  public int ItemId { get; set; }
  public decimal SystemQty { get; set; }
  public decimal? PhysicalQty { get; set; }
  public decimal? Variance { get; set; }
  public DateTime? CountedAt { get; set; }
  public string CountedBy { get; set; }
}

public enum CycleCountStatus { SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED }
```

**API:**
- POST /api/warehouse/v1/cycle-counts/schedule
- GET /api/warehouse/v1/cycle-counts
- GET /api/warehouse/v1/cycle-counts/{id}

### Acceptance Criteria
```gherkin
Scenario: Schedule cycle count for ABC class A
  Given logged in as Warehouse Manager
  And 50 locations with ABC class A items
  When POST /api/warehouse/v1/cycle-counts/schedule with ScheduledDate=2026-02-15, ABCClass=A, AssignedOperator="operator1"
  Then CycleCount created with Status=SCHEDULED
  And 50 locations assigned
  And CountNumber generated: CC-20260212-001
  And response status 200

Scenario: Schedule cycle count with manual location selection
  Given logged in as Manager
  When POST /api/warehouse/v1/cycle-counts/schedule with ScheduledDate=2026-02-15, ABCClass=ALL, Locations=[R1-C1-L1, R2-C2-L2, R3-C3-L3]
  Then CycleCount created with 3 locations
  And Status=SCHEDULED

Scenario: Validation error for past date
  When POST /api/warehouse/v1/cycle-counts/schedule with ScheduledDate=2026-02-01 (past)
  Then validation fails "Scheduled date must be >= today"
  And response status 400
```

### Validation
```bash
# Get dev token
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"manager","roles":["Manager"]}' | jq -r '.token' > /tmp/token.txt
TOKEN=$(cat /tmp/token.txt)

# Schedule cycle count
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/cycle-counts/schedule \
  -H "Content-Type: application/json" \
  -d '{"scheduledDate":"2026-02-15","abcClass":"A","assignedOperator":"operator1"}'
```

### Definition of Done
- [ ] CycleCount entity created in src/LKvitai.MES.Domain/Entities/
- [ ] CycleCountLine entity created
- [ ] EF migration for cycle_counts and cycle_count_lines tables
- [ ] API endpoint POST /api/warehouse/v1/cycle-counts/schedule
- [ ] ABC classification logic (query items by category)
- [ ] Validation: date, locations, operator
- [ ] Unit tests: scheduling logic, validation (15+ tests)
- [ ] Integration test: schedule cycle count
- [ ] API endpoints exposed
- [ ] Documentation updated

---

## Task PRD-1613: Cycle Count Execution

**Epic:** Cycle Counting | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1612
**SourceRefs:** Universe §4.Epic M

### Context
Operator executes cycle count: scan location, scan items, enter physical qty, system compares to AvailableStock, flags discrepancies.

### Scope
**In Scope:** Record count API, variance calculation, discrepancy flagging
**Out of Scope:** Automated count (RFID), real-time updates

### Requirements
**Functional:**
1. API endpoint: POST /api/warehouse/v1/cycle-counts/{id}/record-count (input: LocationCode, ItemBarcode, PhysicalQty)
2. Lookup: Resolve LocationCode → LocationId, ItemBarcode → ItemId
3. Query system qty: AvailableStock projection (LocationId, ItemId) → SystemQty
4. Calculate variance: Variance = PhysicalQty - SystemQty
5. Flag discrepancy: If |Variance| > threshold (5% or 10 units) → flag for review
6. Update CycleCountLine: Set PhysicalQty, Variance, CountedAt, CountedBy
7. Update CycleCount status: If all lines counted → Status=COMPLETED

**Non-Functional:**
1. Record count API: < 500ms
2. Variance calculation accuracy: ±0.01 units

**Data Model:** CycleCountLine (updated with PhysicalQty, Variance, CountedAt, CountedBy)

**API:**
- POST /api/warehouse/v1/cycle-counts/{id}/record-count
- GET /api/warehouse/v1/cycle-counts/{id}/lines

### Acceptance Criteria
```gherkin
Scenario: Record count with no discrepancy
  Given CycleCount CC-001 with Status=SCHEDULED
  And Location R1-C1-L1 has Item RM-0001 with SystemQty=100
  When POST /api/warehouse/v1/cycle-counts/CC-001/record-count with LocationCode=R1-C1-L1, ItemBarcode=RM-0001, PhysicalQty=100
  Then CycleCountLine updated: PhysicalQty=100, Variance=0, CountedAt=now, CountedBy=operator1
  And no discrepancy flagged
  And response status 200

Scenario: Record count with discrepancy
  Given Location R2-C2-L2 has Item RM-0002 with SystemQty=50
  When POST /api/warehouse/v1/cycle-counts/CC-001/record-count with LocationCode=R2-C2-L2, ItemBarcode=RM-0002, PhysicalQty=45
  Then CycleCountLine updated: PhysicalQty=45, Variance=-5, CountedAt=now
  And discrepancy flagged (variance > 5%)
  And response includes warning "Discrepancy detected: -5 units (-10%)"

Scenario: Complete cycle count when all lines counted
  Given CycleCount CC-001 with 10 locations
  And 9 locations already counted
  When POST /api/warehouse/v1/cycle-counts/CC-001/record-count for last location
  Then all 10 lines have PhysicalQty
  And CycleCount Status updated to COMPLETED
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
# Record count
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/cycle-counts/{id}/record-count \
  -H "Content-Type: application/json" \
  -d '{"locationCode":"R1-C1-L1","itemBarcode":"RM-0001","physicalQty":100}'
```

### Definition of Done
- [ ] API endpoint POST /api/warehouse/v1/cycle-counts/{id}/record-count
- [ ] Lookup logic: LocationCode → LocationId, ItemBarcode → ItemId
- [ ] Query AvailableStock for SystemQty
- [ ] Variance calculation
- [ ] Discrepancy flagging (threshold 5% or 10 units)
- [ ] Update CycleCountLine
- [ ] Update CycleCount status (COMPLETED when all lines counted)
- [ ] Unit tests: variance calculation, discrepancy logic (15+ tests)
- [ ] Integration test: record count flow
- [ ] API endpoint exposed
- [ ] Documentation updated

---

## Task PRD-1614: Discrepancy Resolution

**Epic:** Cycle Counting | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1613
**SourceRefs:** Universe §4.Epic M

### Context
Manager reviews discrepancies, approves adjustments. Large discrepancies (>10% or >$1000 value) require recount or CFO approval.

### Scope
**In Scope:** Discrepancy report API, approval workflow, auto-adjustment (create StockAdjusted event)
**Out of Scope:** Automated recount triggers

### Requirements
**Functional:**
1. API endpoint: GET /api/warehouse/v1/cycle-counts/{id}/discrepancies (returns lines with |Variance| > threshold)
2. Discrepancy report columns: Location, Item, SystemQty, PhysicalQty, Variance, VariancePercent, ValueImpact (Variance × UnitCost)
3. Approval workflow: POST /api/warehouse/v1/cycle-counts/{id}/approve-adjustment (input: LineIds[], ApprovedBy, Reason)
4. Validation: If ValueImpact > $1000 → require CFO approval (role check)
5. Auto-adjustment: For each approved line → create StockAdjusted event (SystemQty → PhysicalQty), update AvailableStock projection
6. Update CycleCountLine: Set AdjustmentApprovedBy, AdjustmentApprovedAt

**Non-Functional:**
1. Discrepancy report: < 2 seconds (for 1000 lines)
2. Approval processing: < 1 second per line

**Data Model:** CycleCountLine (add AdjustmentApprovedBy, AdjustmentApprovedAt)

**API:**
- GET /api/warehouse/v1/cycle-counts/{id}/discrepancies
- POST /api/warehouse/v1/cycle-counts/{id}/approve-adjustment

### Acceptance Criteria
```gherkin
Scenario: Review discrepancies
  Given CycleCount CC-001 completed with 10 lines
  And 3 lines have |Variance| > 5%
  When GET /api/warehouse/v1/cycle-counts/CC-001/discrepancies
  Then response includes 3 lines with variance details
  And columns: Location, Item, SystemQty, PhysicalQty, Variance, VariancePercent, ValueImpact

Scenario: Approve adjustment for small discrepancy
  Given CycleCountLine with Variance=-5, ValueImpact=$50
  When POST /api/warehouse/v1/cycle-counts/CC-001/approve-adjustment with LineIds=[line1], ApprovedBy="manager", Reason="Cycle count correction"
  Then StockAdjusted event emitted (SystemQty=50 → PhysicalQty=45)
  And AvailableStock projection updated
  And CycleCountLine updated: AdjustmentApprovedBy="manager", AdjustmentApprovedAt=now

Scenario: Reject adjustment for large discrepancy without CFO approval
  Given CycleCountLine with Variance=-100, ValueImpact=$5000
  When POST /api/warehouse/v1/cycle-counts/CC-001/approve-adjustment with ApprovedBy="manager" (not CFO)
  Then validation fails "CFO approval required for adjustments > $1000"
  And response status 400
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
# Get discrepancies
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/cycle-counts/{id}/discrepancies

# Approve adjustment
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/cycle-counts/{id}/approve-adjustment \
  -H "Content-Type: application/json" \
  -d '{"lineIds":["line1"],"approvedBy":"manager","reason":"Cycle count correction"}'
```

### Definition of Done
- [ ] API endpoint GET /api/warehouse/v1/cycle-counts/{id}/discrepancies
- [ ] Discrepancy report logic (filter by variance threshold)
- [ ] ValueImpact calculation (Variance × UnitCost)
- [ ] API endpoint POST /api/warehouse/v1/cycle-counts/{id}/approve-adjustment
- [ ] Approval validation (CFO role check for large adjustments)
- [ ] Auto-adjustment: emit StockAdjusted event
- [ ] Update CycleCountLine (AdjustmentApprovedBy, AdjustmentApprovedAt)
- [ ] Unit tests: approval logic, validation (15+ tests)
- [ ] Integration test: approve adjustment flow
- [ ] API endpoints exposed
- [ ] Documentation updated

---

## Task PRD-1615: Cycle Count UI

**Epic:** Cycle Counting | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** UI | **Dependencies:** PRD-1612-1614
**SourceRefs:** Universe §4.Epic M

### Context
Cycle count UI: schedule page, execution page (scan location/item, enter qty), discrepancy review page, approval workflow.

### Scope
**In Scope:** Schedule form, execution page, discrepancy report, approval modal
**Out of Scope:** Mobile app (Blazor responsive design for tablet)

### Requirements
**Functional:**
1. Schedule page (/warehouse/cycle-counts/schedule): Form with ScheduledDate, ABCClass dropdown, Locations multi-select, AssignedOperator dropdown, Submit button
2. Execution page (/warehouse/cycle-counts/{id}/execute): Scan location barcode, Scan item barcode, Enter physical qty, Submit button, Progress indicator (X of Y locations counted)
3. Discrepancy report page (/warehouse/cycle-counts/{id}/discrepancies): Table with Location, Item, SystemQty, PhysicalQty, Variance, VariancePercent, ValueImpact, Approve button per row
4. Approval modal: Reason text input, ApprovedBy (auto-filled from current user), Confirm button
5. Cycle counts list (/warehouse/cycle-counts): Table with CountNumber, ScheduledDate, Status, AssignedOperator, Actions (View, Execute, Review Discrepancies)

**Non-Functional:**
1. Responsive design (tablet/desktop)
2. Client-side + server-side validation
3. Page load < 2 seconds

**Data Model:** Blazor pages in src/LKvitai.MES.WebUI/Pages/CycleCounts/

**API:**
- POST /api/warehouse/v1/cycle-counts/schedule
- POST /api/warehouse/v1/cycle-counts/{id}/record-count
- GET /api/warehouse/v1/cycle-counts/{id}/discrepancies
- POST /api/warehouse/v1/cycle-counts/{id}/approve-adjustment

**UI (Blazor):**
- Routes: /warehouse/cycle-counts, /warehouse/cycle-counts/schedule, /warehouse/cycle-counts/{id}/execute, /warehouse/cycle-counts/{id}/discrepancies
- Components: Forms, tables, modals
- Validations: Required fields, date >= today, physical qty >= 0
- Empty states: "No cycle counts scheduled.", "No discrepancies found."

### Acceptance Criteria
```gherkin
Scenario: Schedule cycle count via UI
  Given logged in as Manager
  When navigate to /warehouse/cycle-counts/schedule
  And fill form: ScheduledDate=2026-02-15, ABCClass=A, AssignedOperator=operator1
  And click Submit
  Then API POST /api/warehouse/v1/cycle-counts/schedule called
  And redirect to /warehouse/cycle-counts
  And toast "Cycle count CC-001 scheduled"

Scenario: Execute cycle count via UI
  Given CycleCount CC-001 with Status=SCHEDULED
  When navigate to /warehouse/cycle-counts/CC-001/execute
  And scan location barcode "R1-C1-L1"
  And scan item barcode "RM-0001"
  And enter physical qty 100
  And click Submit
  Then API POST /api/warehouse/v1/cycle-counts/CC-001/record-count called
  And toast "Count recorded for R1-C1-L1"
  And progress indicator updates (1 of 10 locations counted)

Scenario: Review and approve discrepancies
  Given CycleCount CC-001 completed with 3 discrepancies
  When navigate to /warehouse/cycle-counts/CC-001/discrepancies
  Then table displays 3 lines with variance details
  When click "Approve" for line 1
  And enter Reason "Cycle count correction"
  And click Confirm
  Then API POST /api/warehouse/v1/cycle-counts/CC-001/approve-adjustment called
  And toast "Adjustment approved"
  And line 1 removed from table
```

### Validation
```bash
dotnet run --project src/LKvitai.MES.WebUI
# Navigate to http://localhost:5001/warehouse/cycle-counts
# Test: Schedule, Execute, Review discrepancies, Approve adjustments
```

### Definition of Done
- [ ] Schedule.razor page created in src/LKvitai.MES.WebUI/Pages/CycleCounts/
- [ ] Execute.razor page created
- [ ] Discrepancies.razor page created
- [ ] List.razor page created
- [ ] Forms with validation
- [ ] Scan input fields (barcode scanner compatible)
- [ ] Progress indicator
- [ ] Approval modal
- [ ] Client-side validation
- [ ] Responsive design
- [ ] Manual test completed (all scenarios)
- [ ] Documentation updated
## Task PRD-1616: ZPL Template Engine

**Epic:** Label Printing | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1516
**SourceRefs:** Universe §4.Epic G (Label Printing)

### Context
Generate ZPL (Zebra Programming Language) labels for locations, handling units, items. Template engine replaces placeholders with data.

### Scope
**In Scope:** ZPL template engine, 3 templates (location, HU, item), barcode generation (Code 128), preview API (ZPL → PDF)
**Out of Scope:** Custom template editor UI (hard-coded templates for Phase 1.5)

### Requirements
**Functional:**
1. ZPL template engine: Load template string, replace placeholders ({{LocationCode}}, {{ItemSKU}}, {{HUBarcode}}, {{Qty}}, {{LotNumber}}, {{ExpiryDate}})
2. Templates:
   - Location label: 4"×2" label with location code barcode (Code 128), text (Aisle, Rack, Level, Bin)
   - HU label: 4"×6" label with HU barcode, item SKU, qty, lot, expiry date
   - Item label: 2"×1" label with item SKU barcode, description
3. Barcode generation: Code 128 format (ZPL ^BC command)
4. Preview API: POST /api/warehouse/v1/labels/preview (input: TemplateType, Data) → returns PDF (ZPL rendered via Labelary API or local renderer)

**Non-Functional:**
1. Template rendering: < 100ms
2. Preview generation: < 2 seconds

**Data Model:**
```csharp
public enum LabelTemplateType { LOCATION, HANDLING_UNIT, ITEM }

public class LabelTemplate {
  public LabelTemplateType Type { get; set; }
  public string ZplTemplate { get; set; }
}

public class LabelData {
  public Dictionary<string, string> Placeholders { get; set; }
}
```

**API:**
- POST /api/warehouse/v1/labels/preview (input: TemplateType, Data)
- GET /api/warehouse/v1/labels/templates (returns available templates)

### Acceptance Criteria
```gherkin
Scenario: Generate location label ZPL
  Given location label template with placeholders {{LocationCode}}, {{Aisle}}, {{Rack}}, {{Level}}, {{Bin}}
  When render template with data: LocationCode=R3-C6-L3B3, Aisle=R3, Rack=C6, Level=L3, Bin=B3
  Then ZPL output includes:
    - ^BC command for Code 128 barcode (R3-C6-L3B3)
    - ^FD commands for text fields (Aisle: R3, Rack: C6, Level: L3, Bin: B3)
  And ZPL length < 1KB

Scenario: Generate HU label ZPL
  Given HU label template with placeholders {{HUBarcode}}, {{ItemSKU}}, {{Qty}}, {{LotNumber}}, {{ExpiryDate}}
  When render template with data: HUBarcode=HU-001, ItemSKU=RM-0001, Qty=50, LotNumber=LOT-2026-001, ExpiryDate=2027-02-12
  Then ZPL output includes barcode + text fields
  And ZPL length < 2KB

Scenario: Preview label as PDF
  Given location label data
  When POST /api/warehouse/v1/labels/preview with TemplateType=LOCATION, Data={LocationCode: R1-C1-L1}
  Then ZPL rendered to PDF via Labelary API
  And response Content-Type: application/pdf
  And PDF size < 100KB
```

### Validation
```bash
# Get dev token
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"operator","roles":["Operator"]}' | jq -r '.token' > /tmp/token.txt
TOKEN=$(cat /tmp/token.txt)

# Preview location label
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/labels/preview \
  -H "Content-Type: application/json" \
  -d '{"templateType":"LOCATION","data":{"LocationCode":"R1-C1-L1","Aisle":"R1","Rack":"C1","Level":"L1","Bin":"B1"}}' \
  --output /tmp/label-preview.pdf
```

### Definition of Done
- [ ] LabelTemplateEngine class created in src/LKvitai.MES.Api/Services/
- [ ] 3 ZPL templates defined (location, HU, item)
- [ ] Placeholder replacement logic
- [ ] Barcode generation (Code 128)
- [ ] Preview API endpoint POST /api/warehouse/v1/labels/preview
- [ ] Labelary API integration (ZPL → PDF) or local renderer
- [ ] Unit tests: template rendering, placeholder replacement (15+ tests)
- [ ] Integration test: preview API
- [ ] API endpoint exposed
- [ ] Documentation updated (ZPL template format)

---

## Task PRD-1617: TCP 9100 Printer Integration

**Epic:** Label Printing | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1616
**SourceRefs:** Universe §4.Epic G

### Context
Send ZPL to Zebra printer via TCP 9100 (raw socket). Printer config (IP, port) stored in settings. Retry 3x if printer offline.

### Scope
**In Scope:** TCP 9100 client, printer config, retry logic, print queue (in-memory for Phase 1.5)
**Out of Scope:** Printer discovery (manual IP config), print job history (deferred to Phase 2)

### Requirements
**Functional:**
1. Printer config: appsettings.json section "LabelPrinting" with PrinterIP, PrinterPort (default 9100), RetryCount (default 3), RetryDelayMs (default 1000)
2. TCP 9100 client: Open socket to PrinterIP:PrinterPort, send ZPL string, close socket
3. Retry logic: If socket connection fails or timeout → retry 3x with 1 second delay
4. Print API: POST /api/warehouse/v1/labels/print (input: TemplateType, Data) → render ZPL → send to printer
5. Fallback: If all retries fail → return PDF preview URL (manual print)

**Non-Functional:**
1. Print latency: < 3 seconds (including retries)
2. Timeout: 5 seconds per socket connection attempt
3. Reliability: 99% success rate (with retries)

**Data Model:**
```csharp
public class LabelPrintingConfig {
  public string PrinterIP { get; set; }
  public int PrinterPort { get; set; } = 9100;
  public int RetryCount { get; set; } = 3;
  public int RetryDelayMs { get; set; } = 1000;
}
```

**API:**
- POST /api/warehouse/v1/labels/print

### Acceptance Criteria
```gherkin
Scenario: Print location label to Zebra printer
  Given printer configured at 192.168.1.100:9100
  And printer online
  When POST /api/warehouse/v1/labels/print with TemplateType=LOCATION, Data={LocationCode: R1-C1-L1}
  Then ZPL rendered
  And TCP socket opened to 192.168.1.100:9100
  And ZPL sent to printer
  And socket closed
  And response status 200 with message "Label printed successfully"

Scenario: Retry on printer offline
  Given printer configured at 192.168.1.100:9100
  And printer offline (socket connection fails)
  When POST /api/warehouse/v1/labels/print
  Then retry 1: wait 1 second, attempt connection (fails)
  And retry 2: wait 1 second, attempt connection (fails)
  And retry 3: wait 1 second, attempt connection (fails)
  And response status 500 with message "Printer offline after 3 retries. Download PDF: {url}"

Scenario: Print HU label on goods receipt
  Given InboundShipment received, HandlingUnit HU-001 created
  When auto-print triggered (event handler)
  Then HU label printed with barcode HU-001, item SKU, qty, lot
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
# Print location label (requires Zebra printer or TCP 9100 simulator)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/labels/print \
  -H "Content-Type: application/json" \
  -d '{"templateType":"LOCATION","data":{"LocationCode":"R1-C1-L1","Aisle":"R1","Rack":"C1","Level":"L1","Bin":"B1"}}'
```

### Definition of Done
- [ ] LabelPrintingConfig class created
- [ ] appsettings.json section "LabelPrinting" added
- [ ] TCP 9100 client implemented (TcpClient, NetworkStream)
- [ ] Retry logic with exponential backoff
- [ ] Print API endpoint POST /api/warehouse/v1/labels/print
- [ ] Fallback: return PDF URL if print fails
- [ ] Unit tests: retry logic, socket connection (mocked) (15+ tests)
- [ ] Integration test: print to simulator or real printer
- [ ] API endpoint exposed
- [ ] Documentation updated (printer setup guide)

---

## Task PRD-1618: Print Queue & Retry

**Epic:** Label Printing | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1617
**SourceRefs:** Universe §4.Epic G

### Context
Print queue stores failed print jobs for retry. Background job processes queue every 5 minutes. Operators can view queue and manually retry.

### Scope
**In Scope:** PrintQueue entity (in-memory for Phase 1.5, persistent in Phase 2), background job (Hangfire), manual retry API
**Out of Scope:** Print job history (audit log)

### Requirements
**Functional:**
1. PrintQueue entity: Id, TemplateType, Data (JSON), Status (PENDING, PRINTING, COMPLETED, FAILED), RetryCount, CreatedAt, LastAttemptAt, ErrorMessage
2. Enqueue: When print fails after 3 retries → add to PrintQueue with Status=PENDING
3. Background job: Hangfire recurring job (every 5 minutes) → query PrintQueue where Status=PENDING → attempt print → update Status
4. Manual retry API: POST /api/warehouse/v1/labels/queue/{id}/retry
5. Queue list API: GET /api/warehouse/v1/labels/queue (returns pending/failed jobs)

**Non-Functional:**
1. Queue processing: < 10 seconds for 100 jobs
2. In-memory queue: ConcurrentQueue (thread-safe)

**Data Model:**
```csharp
public class PrintQueueItem {
  public Guid Id { get; set; }
  public LabelTemplateType TemplateType { get; set; }
  public string DataJson { get; set; }
  public PrintQueueStatus Status { get; set; }
  public int RetryCount { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? LastAttemptAt { get; set; }
  public string ErrorMessage { get; set; }
}

public enum PrintQueueStatus { PENDING, PRINTING, COMPLETED, FAILED }
```

**API:**
- GET /api/warehouse/v1/labels/queue
- POST /api/warehouse/v1/labels/queue/{id}/retry

### Acceptance Criteria
```gherkin
Scenario: Enqueue failed print job
  Given print attempt fails after 3 retries
  When print API returns error
  Then PrintQueueItem created with Status=PENDING, RetryCount=0
  And response includes "Print failed. Job queued for retry. Queue ID: {id}"

Scenario: Background job processes queue
  Given PrintQueue has 5 PENDING jobs
  When Hangfire job runs (every 5 minutes)
  Then for each PENDING job:
    - Attempt print
    - If success: Status=COMPLETED
    - If failure: RetryCount++, Status=PENDING (if RetryCount < 10), else Status=FAILED
  And 3 jobs succeed, 2 jobs remain PENDING

Scenario: Manual retry from queue
  Given PrintQueueItem with Status=FAILED, RetryCount=10
  When POST /api/warehouse/v1/labels/queue/{id}/retry
  Then attempt print immediately
  And if success: Status=COMPLETED
  And if failure: ErrorMessage updated
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)
# Get print queue
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/labels/queue

# Manual retry
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/labels/queue/{id}/retry
```

### Definition of Done
- [ ] PrintQueueItem class created
- [ ] In-memory queue (ConcurrentQueue) in LabelPrintingService
- [ ] Enqueue logic when print fails
- [ ] Hangfire recurring job (every 5 minutes)
- [ ] Background job processes queue
- [ ] Manual retry API endpoint POST /api/warehouse/v1/labels/queue/{id}/retry
- [ ] Queue list API endpoint GET /api/warehouse/v1/labels/queue
- [ ] Unit tests: queue logic, retry logic (15+ tests)
- [ ] Integration test: enqueue → background job → retry
- [ ] API endpoints exposed
- [ ] Documentation updated
## Task PRD-1619: Inter-Warehouse Transfer Workflow

**Epic:** Inter-Warehouse Transfers | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1519
**SourceRefs:** Universe §4.Epic F (Inter-Warehouse Transfers)

### Context
Transfer stock between logical warehouses (RES → PROD, NLQ → SCRAP). Workflow: request → approve → execute. In-transit virtual location tracks movement.

### Scope
**In Scope:** Transfer entity, state machine (DRAFT → PENDING_APPROVAL → APPROVED → IN_TRANSIT → COMPLETED), approval workflow, StockMoved events
**Out of Scope:** Physical inter-building transfers (single warehouse assumption)

### Requirements
**Functional:**
1. Transfer entity: Id, TransferNumber (AUTO-TRF-20260212-001), FromWarehouse, ToWarehouse, Status (DRAFT, PENDING_APPROVAL, APPROVED, IN_TRANSIT, COMPLETED, CANCELLED), RequestedBy, ApprovedBy, ExecutedBy, Lines (ItemId, Qty, FromLocationId, ToLocationId), CreatedAt, ApprovedAt, ExecutedAt
2. TransferLine entity: Id, TransferId, ItemId (int), Qty, FromLocationId (int), ToLocationId (int), LotId (nullable)
3. State machine:
   - DRAFT → Submit() → PENDING_APPROVAL (if ToWarehouse=SCRAP, else APPROVED)
   - PENDING_APPROVAL → Approve() → APPROVED
   - APPROVED → Execute() → IN_TRANSIT → COMPLETED
4. Approval rules: Transfers to SCRAP require Manager approval
5. Execute: Emit StockMoved events (FromLocation → IN_TRANSIT_{transferId} → ToLocation)
6. Validation: FromWarehouse != ToWarehouse, Qty > 0, sufficient stock at FromLocation

**Non-Functional:**
1. Transfer creation: < 1 second
2. Execute transfer: < 2 seconds (for 100 lines)

**Data Model:**
```csharp
public class Transfer {
  public Guid Id { get; set; }
  public string TransferNumber { get; set; }
  public string FromWarehouse { get; set; } // RES, PROD, NLQ, SCRAP
  public string ToWarehouse { get; set; }
  public TransferStatus Status { get; set; }
  public string RequestedBy { get; set; }
  public string ApprovedBy { get; set; }
  public string ExecutedBy { get; set; }
  public List<TransferLine> Lines { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? ApprovedAt { get; set; }
  public DateTime? ExecutedAt { get; set; }
}

public class TransferLine {
  public Guid Id { get; set; }
  public Guid TransferId { get; set; }
  public int ItemId { get; set; }
  public decimal Qty { get; set; }
  public int FromLocationId { get; set; }
  public int ToLocationId { get; set; }
  public Guid? LotId { get; set; }
}

public enum TransferStatus { DRAFT, PENDING_APPROVAL, APPROVED, IN_TRANSIT, COMPLETED, CANCELLED }
```

**API:**
- POST /api/warehouse/v1/transfers (create transfer)
- POST /api/warehouse/v1/transfers/{id}/submit
- POST /api/warehouse/v1/transfers/{id}/approve
- POST /api/warehouse/v1/transfers/{id}/execute
- GET /api/warehouse/v1/transfers
- GET /api/warehouse/v1/transfers/{id}

### Acceptance Criteria
```gherkin
Scenario: Create transfer request
  Given logged in as Operator
  When POST /api/warehouse/v1/transfers with FromWarehouse=RES, ToWarehouse=PROD, Lines=[{ItemId=1, Qty=50, FromLocationId=10, ToLocationId=20}]
  Then Transfer created with Status=DRAFT
  And TransferNumber generated: TRF-20260212-001
  And response status 200

Scenario: Submit transfer for approval (SCRAP destination)
  Given Transfer TRF-001 with Status=DRAFT, ToWarehouse=SCRAP
  When POST /api/warehouse/v1/transfers/TRF-001/submit
  Then Status updated to PENDING_APPROVAL
  And notification sent to Manager

Scenario: Approve transfer
  Given Transfer TRF-001 with Status=PENDING_APPROVAL
  And logged in as Manager
  When POST /api/warehouse/v1/transfers/TRF-001/approve
  Then Status updated to APPROVED
  And ApprovedBy set to current user
  And ApprovedAt set to now

Scenario: Execute transfer
  Given Transfer TRF-001 with Status=APPROVED
  And FromLocation has sufficient stock (Item 1, Qty 50)
  When POST /api/warehouse/v1/transfers/TRF-001/execute
  Then StockMoved event emitted: FromLocation → IN_TRANSIT_TRF-001 (Qty 50)
  And StockMoved event emitted: IN_TRANSIT_TRF-001 → ToLocation (Qty 50)
  And Status updated to COMPLETED
  And ExecutedBy set to current user
  And ExecutedAt set to now

Scenario: Validation error for insufficient stock
  Given Transfer TRF-002 with Line: ItemId=1, Qty=100, FromLocationId=10
  And FromLocation has only 50 units
  When POST /api/warehouse/v1/transfers/TRF-002/execute
  Then validation fails "Insufficient stock at FromLocation"
  And response status 400
```

### Validation
```bash
# Get dev token
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"operator","roles":["Operator"]}' | jq -r '.token' > /tmp/token.txt
TOKEN=$(cat /tmp/token.txt)

# Create transfer
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/transfers \
  -H "Content-Type: application/json" \
  -d '{"fromWarehouse":"RES","toWarehouse":"PROD","lines":[{"itemId":1,"qty":50,"fromLocationId":10,"toLocationId":20}]}'

# Submit transfer
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/transfers/{id}/submit

# Approve transfer (requires Manager token)
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"manager","roles":["Manager"]}' | jq -r '.token' > /tmp/manager-token.txt
MANAGER_TOKEN=$(cat /tmp/manager-token.txt)
curl -H "Authorization: Bearer $MANAGER_TOKEN" -X POST http://localhost:5000/api/warehouse/v1/transfers/{id}/approve

# Execute transfer
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/transfers/{id}/execute
```

### Definition of Done
- [ ] Transfer entity created in src/LKvitai.MES.Domain/Entities/
- [ ] TransferLine entity created
- [ ] EF migration for transfers and transfer_lines tables
- [ ] State machine logic (DRAFT → PENDING_APPROVAL → APPROVED → IN_TRANSIT → COMPLETED)
- [ ] Approval workflow (Manager role check for SCRAP transfers)
- [ ] Execute logic: emit StockMoved events
- [ ] Validation: FromWarehouse != ToWarehouse, sufficient stock
- [ ] API endpoints: POST /api/warehouse/v1/transfers, /submit, /approve, /execute
- [ ] Unit tests: state machine, approval logic, validation (20+ tests)
- [ ] Integration test: full transfer flow
- [ ] API endpoints exposed
- [ ] Documentation updated

---

## Task PRD-1620: Inter-Warehouse Transfer UI

**Epic:** Inter-Warehouse Transfers | **Phase:** 1.5 | **Sprint:** 7 | **Estimate:** M (1 day)
**OwnerType:** UI | **Dependencies:** PRD-1619
**SourceRefs:** Universe §4.Epic F

### Context
Transfer UI: create transfer form, transfer list, approval page, execution page.

### Scope
**In Scope:** Create form, list page, approval modal, execution page
**Out of Scope:** Mobile app (Blazor responsive design for tablet)

### Requirements
**Functional:**
1. Create transfer form (/warehouse/transfers/create): FromWarehouse dropdown, ToWarehouse dropdown, Lines table (Add Line button: ItemId dropdown, Qty, FromLocationId dropdown, ToLocationId dropdown), Submit button
2. Transfer list (/warehouse/transfers): Table with TransferNumber, FromWarehouse, ToWarehouse, Status, RequestedBy, Actions (View, Submit, Approve, Execute, Cancel)
3. Approval modal: Reason text input (optional), Approve button
4. Execution page (/warehouse/transfers/{id}/execute): Transfer details, Lines table, Execute button, Confirmation dialog

**Non-Functional:**
1. Responsive design (tablet/desktop)
2. Client-side + server-side validation
3. Page load < 2 seconds

**Data Model:** Blazor pages in src/LKvitai.MES.WebUI/Pages/Transfers/

**API:**
- POST /api/warehouse/v1/transfers
- POST /api/warehouse/v1/transfers/{id}/submit
- POST /api/warehouse/v1/transfers/{id}/approve
- POST /api/warehouse/v1/transfers/{id}/execute
- GET /api/warehouse/v1/transfers

**UI (Blazor):**
- Routes: /warehouse/transfers, /warehouse/transfers/create, /warehouse/transfers/{id}/execute
- Components: Forms, tables, modals
- Validations: Required fields, FromWarehouse != ToWarehouse, Qty > 0
- Empty states: "No transfers found.", "No lines added. Add your first line."

### Acceptance Criteria
```gherkin
Scenario: Create transfer via UI
  Given logged in as Operator
  When navigate to /warehouse/transfers/create
  And select FromWarehouse "RES"
  And select ToWarehouse "PROD"
  And add line: ItemId=1, Qty=50, FromLocationId=10, ToLocationId=20
  And click Submit
  Then API POST /api/warehouse/v1/transfers called
  And redirect to /warehouse/transfers
  And toast "Transfer TRF-001 created"

Scenario: Submit transfer for approval
  Given Transfer TRF-001 with Status=DRAFT
  When navigate to /warehouse/transfers
  And click "Submit" for TRF-001
  Then API POST /api/warehouse/v1/transfers/TRF-001/submit called
  And Status updated to PENDING_APPROVAL
  And toast "Transfer submitted for approval"

Scenario: Approve transfer
  Given Transfer TRF-001 with Status=PENDING_APPROVAL
  And logged in as Manager
  When navigate to /warehouse/transfers
  And click "Approve" for TRF-001
  And approval modal opens
  And click Approve
  Then API POST /api/warehouse/v1/transfers/TRF-001/approve called
  And Status updated to APPROVED
  And toast "Transfer approved"

Scenario: Execute transfer
  Given Transfer TRF-001 with Status=APPROVED
  When navigate to /warehouse/transfers/{id}/execute
  And click Execute
  And confirmation dialog "Execute transfer? Stock will be moved."
  And click Confirm
  Then API POST /api/warehouse/v1/transfers/TRF-001/execute called
  And Status updated to COMPLETED
  And toast "Transfer executed successfully"
```

### Validation
```bash
dotnet run --project src/LKvitai.MES.WebUI
# Navigate to http://localhost:5001/warehouse/transfers
# Test: Create, Submit, Approve, Execute transfers
```

### Definition of Done
- [ ] Create.razor page created in src/LKvitai.MES.WebUI/Pages/Transfers/
- [ ] List.razor page created
- [ ] Execute.razor page created
- [ ] Approval modal component
- [ ] Forms with validation
- [ ] Lines table with Add/Remove functionality
- [ ] Client-side validation
- [ ] Confirmation dialogs
- [ ] Responsive design
- [ ] Manual test completed (all scenarios)
- [ ] Documentation updated
