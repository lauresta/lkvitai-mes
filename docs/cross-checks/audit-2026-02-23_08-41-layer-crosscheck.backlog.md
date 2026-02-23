# Implementation Backlog: Layer Cross-Check Audit
**Generated:** 2026-02-23
**Source:** audit-2026-02-23_08-41-layer-crosscheck.md
**Repo Root:** C:\Sources\clients\lauresta\lkvitai-mes

---

## STEP 1: Repo Reconnaissance — Concrete Anchors

### A) Navigation Menu Definition
**File:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Shared\NavMenu.razor`
- **Description:** Data-driven accordion navigation from static `_sections` array
- **Search Keywords:** `_sections`, `NavLink`, `href=`, `@onclick`, `NavigationSection`

### B) UI Routing Mechanism
**Pattern:** Blazor Server with `@page` directive
**Location:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\**\*.razor`
- **Search Keywords:** `@page`, `@inject`, `NavigationManager`, `RouteData`
- **Example Files:**
  - `Pages\Dashboard.razor` → `@page "/dashboard"`
  - `Pages\Admin\Settings.razor` → `@page "/warehouse/admin/settings"`
  - `Pages\Valuation\Dashboard.razor` → `@page "/warehouse/valuation/dashboard"`

### C) API Routing
**Pattern:** ASP.NET Core MVC Controllers with `[Route]` attribute
**Location:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\**\*Controller.cs`
- **Search Keywords:** `[Route(`, `[ApiController]`, `[HttpGet]`, `[HttpPost]`, `api/warehouse/v1`
- **Example Files:**
  - `Controllers\StockController.cs` → `[Route("api/warehouse/v1/stock")]`
  - `Controllers\AdminApiKeysController.cs` → `[Route("api/warehouse/v1/admin/api-keys")]`
  - `Controllers\WarehouseVisualizationController.cs` → `[Route("api/warehouse/v1")]`

### D) Service/Use-Case Layer
**Two Patterns Exist (ARCH-02 tech debt):**

#### Pattern 1: Application Layer (MediatR - Properly Layered)
**Location:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Application\Commands\**\*CommandHandler.cs`
**Location:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Application\Queries\**\*QueryHandler.cs`
- **Search Keywords:** `IRequestHandler`, `MediatR`, `CommandHandler`, `QueryHandler`
- **Example Files:**
  - `Commands\AllocateReservationCommandHandler.cs`
  - `Commands\ReceiveGoodsCommandHandler.cs`
  - `Queries\GetAvailableStockQueryHandler.cs`
  - `Queries\SearchReservationsQueryHandler.cs`

#### Pattern 2: Api/Services Layer (ARCH-02 - Business Logic in Wrong Layer)
**Location:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Services\**\*Service.cs`
- **Search Keywords:** `sealed class`, `Service`, `IDocumentStore`, `WarehouseDbContext`, `_dbContext`
- **Example Files:**
  - `Services\AgnumExportServices.cs`
  - `Services\CycleCountServices.cs`
  - `Services\TransferServices.cs`
  - `Services\ValuationCommandHandlers.cs`
  - `Services\RoleManagementService.cs`
  - `Services\SecurityAuditLogService.cs`

**Note:** 34 service files in Api/Services contain business logic that should be in Application layer. Controllers inject DbContext/IDocumentStore directly, bypassing MediatR pipeline.

### E) DB Schema Source-of-Truth
**Primary:** EF Core Model Snapshot
**File:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Infrastructure\Persistence\Migrations\WarehouseDbContextModelSnapshot.cs`
- **Search Keywords:** `modelBuilder.Entity`, `ToTable`, `HasKey`, `Property`
- **Migration Files:** 34 migration files in `Persistence\Migrations\` directory

**Secondary:** Marten Document Store (Event-Sourced + Projections)
**Location:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Infrastructure\Persistence\Marten*Repository.cs`
- **Search Keywords:** `IDocumentStore`, `StockLedger`, `Reservation`, `HandlingUnit`, `AvailableStockView`
- **Example Files:**
  - `MartenStockLedgerRepository.cs` → StockLedger event stream
  - `MartenReservationRepository.cs` → Reservation event stream
  - `MartenAvailableStockRepository.cs` → AvailableStockView projection
  - `MartenLocationBalanceRepository.cs` → LocationBalanceView projection

### F) WebUI Client Services
**Location:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\**\*Client.cs`
- **Search Keywords:** `HttpClient`, `GetFromJsonAsync`, `PostAsJsonAsync`, `api/warehouse/v1`
- **Example Files:**
  - `Services\StockClient.cs`
  - `Services\MasterDataAdminClient.cs`
  - `Services\AdminConfigurationClient.cs`
  - `Services\AgnumClient.cs`
  - `Services\ReportsClient.cs`

---

## STEP 2: Implementation Backlog

### PRIORITY OVERRIDE: Start with G-09 (Owner Request)


---

## G-09: Warehouse Layout Editor UI (PRIORITY 1 - OWNER REQUEST)

**Type:** Gap  
**Priority:** P0 (Owner Request - Execute First)  
**Scope:** UI / Client / Nav  
**Status:** `[x] done` (commits: `0ee363c`)

### Description
No UI page/editor for warehouse layout management. The `warehouse_layouts` table exists, API endpoints `GET/PUT api/warehouse/v1/layout` exist in `WarehouseVisualizationController`, but there's no UI to view or edit layout definitions. The 3D visualization works but layout data is not editable from UI.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/layout-editor` with `@page` directive
- [ ] Add `LayoutEditorClient` service in `WebUI/Services/` with methods: `GetLayoutAsync()`, `UpdateLayoutAsync()`
- [ ] Client calls `GET api/warehouse/v1/layout` and `PUT api/warehouse/v1/layout`
- [ ] UI displays current layout JSON/structure in editable form (textarea or structured editor)
- [ ] UI validates layout JSON before submission
- [ ] Add nav entry in `NavMenu.razor` under "admin" section: "Layout Editor" → `/warehouse/admin/layout-editor`
- [ ] Test: Load existing layout, modify, save, verify changes persist

### Evidence Pointers
**Anchor Files to Change:**
- **Nav:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Shared\NavMenu.razor`
  - Search: `_sections`, `admin` section
  - Add new nav item after "Import Wizard"
- **New Page:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\Admin\LayoutEditor.razor`
  - Pattern: Copy from `Pages\Admin\Settings.razor` structure
- **New Client:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\LayoutEditorClient.cs`
  - Pattern: Copy from `Services\AdminConfigurationClient.cs`
  - Endpoints: `GET/PUT api/warehouse/v1/layout`
- **API Controller (already exists):** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\WarehouseVisualizationController.cs`
  - Search: `GetLayout`, `UpdateLayout`
  - Verify authorization policies

### Suggested Commits
1. `feat(ui): add LayoutEditorClient service for warehouse layout API`
2. `feat(ui): create warehouse layout editor page at /warehouse/admin/layout-editor`
3. `feat(nav): add Layout Editor nav item to admin section`

### Risks/Unknowns
- **Layout JSON schema:** Need to understand expected structure from `warehouse_layouts` table and API
- **Authorization:** Verify policy requirements (likely `AdminOnly`)
- **Validation:** Layout JSON validation rules may need discovery from backend
- **3D Visualization Integration:** May need to refresh 3D view after layout changes


---

## P0 BUGS (Critical - Execute After G-09)

### B-01: Dead Dispatch History Endpoint

**Type:** Bug  
**Priority:** P0  
**Scope:** API / Cleanup  
**Status:** `[ ] open`

### Description
`GET api/warehouse/v1/dispatch/history` in `DispatchController` is dead code. UI uses `/reports/dispatch-history` endpoint from `ReportsController` instead. This creates confusion and maintenance burden.

### Definition of Done
- [ ] Verify no UI client calls `DispatchController.GetHistory()`
- [ ] Verify no external integrations use this endpoint (check API logs if available)
- [ ] Delete `DispatchController.cs` entirely OR remove only the `GetHistory()` method
- [ ] Run tests to ensure no regressions
- [ ] Update API documentation if it references this endpoint

### Evidence Pointers
**Anchor Files to Change:**
- **Controller to Delete/Modify:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\DispatchController.cs`
  - Search: `GetHistory`, `[Route("api/warehouse/v1/dispatch")]`
- **Verify UI Client:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\ReportsClient.cs`
  - Search: `dispatch-history`, confirm it uses `/reports/dispatch-history`
- **Verify UI Page:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\ReportsDispatchHistory.razor`
  - Search: `ReportsClient`, confirm injection

### Suggested Commits
1. `fix(api): remove dead DispatchController.GetHistory endpoint (duplicate of ReportsController)`

### Risks/Unknowns
- **External API consumers:** If external systems use this endpoint, need migration plan
- **Backward compatibility:** May need deprecation period if this is a public API

---

### B-02: Dead QCPanel Redirect Page

**Type:** Bug  
**Priority:** P0  
**Scope:** UI / Cleanup  
**Status:** `[ ] open`

### Description
`QCPanel.razor` at `/warehouse/qc/pending` is a redirect-only dead page. It contains no UI, just `NavigationManager.NavigateTo("/warehouse/inbound/qc")`. This is wasteful and confusing.

### Definition of Done
- [ ] Delete `QCPanel.razor` file entirely
- [ ] Verify no nav items point to `/warehouse/qc/pending`
- [ ] Search codebase for any hardcoded links to `/warehouse/qc/pending`
- [ ] Update any links to point directly to `/warehouse/inbound/qc`
- [ ] Test navigation flows to ensure no broken links

### Evidence Pointers
**Anchor Files to Change:**
- **Page to Delete:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\QCPanel.razor`
  - Search: `@page "/warehouse/qc/pending"`, `NavigationManager.NavigateTo`
- **Nav Menu (verify):** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Shared\NavMenu.razor`
  - Search: `/warehouse/qc/pending`, confirm no references
- **Search All Razor Files:** `**/*.razor`
  - Search: `/warehouse/qc/pending`, replace with `/warehouse/inbound/qc`

### Suggested Commits
1. `fix(ui): remove dead QCPanel redirect page, use direct /warehouse/inbound/qc route`

### Risks/Unknowns
- **Bookmarks:** Users may have bookmarked `/warehouse/qc/pending` (low risk, redirect was immediate)

---

### B-03: Test Endpoint Exposed in Production

**Type:** Security  
**Priority:** P0  
**Scope:** API / Security  
**Status:** `[ ] open`

### Description
`POST api/test/simulate-capacity-alert` in `CapacitySimulationController` with `[Route("api/test")]` is a test endpoint exposed in production builds. This is a security risk and should be removed or gated behind environment flag.

### Definition of Done
- [ ] Option A: Delete `CapacitySimulationController.cs` entirely if not needed
- [ ] Option B: Add environment check: `#if DEBUG` or runtime `IsDevelopment()` check
- [ ] Option C: Add feature flag gate: `if (!_featureFlags.IsEnabled("AllowTestEndpoints")) return Forbid();`
- [ ] Verify endpoint is not accessible in production environment
- [ ] Add integration test to verify test endpoints are blocked in production config

### Evidence Pointers
**Anchor Files to Change:**
- **Controller to Modify/Delete:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\CapacitySimulationController.cs`
  - Search: `[Route("api/test")]`, `[AllowAnonymous]`, `SimulateCapacityAlert`
- **Environment Check Pattern:** Look at other controllers for `IWebHostEnvironment` injection
  - Search: `IWebHostEnvironment`, `IsDevelopment()`
- **Feature Flag Pattern:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Services\FeatureFlagService.cs`
  - Search: `IsEnabled`, `IFeatureFlagService`

### Suggested Commits
1. `fix(security): remove test endpoint CapacitySimulationController from production builds`
   OR
1. `fix(security): gate test endpoints behind IsDevelopment() environment check`

### Risks/Unknowns
- **Monitoring dependencies:** Check if monitoring/alerting systems depend on this endpoint
- **Test automation:** Verify test suites don't rely on this endpoint in non-dev environments


---

### B-04: Route Conflict Between Controllers

**Type:** Bug  
**Priority:** P0  
**Scope:** API / Routing  
**Status:** `[ ] open`

### Description
`AdvancedWarehouseController` and `QCController` both declare `[Route("api/warehouse/v1/qc")]`. Overlapping actions will cause ambiguous route exceptions at runtime. This is a critical routing conflict.

### Definition of Done
- [ ] Identify all actions in both controllers with overlapping routes
- [ ] Split routes: Move QC checklist-templates and defects to different route prefix
- [ ] Option A: `AdvancedWarehouseController` → `[Route("api/warehouse/v1/qc-templates")]` for templates
- [ ] Option B: `AdvancedWarehouseController` → `[Route("api/warehouse/v1/advanced/qc")]` for advanced QC features
- [ ] Update any UI clients that call these endpoints (if any exist)
- [ ] Run API tests to verify no ambiguous route errors
- [ ] Test QC workflows end-to-end

### Evidence Pointers
**Anchor Files to Change:**
- **Controller 1:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\QCController.cs`
  - Search: `[Route("api/warehouse/v1/qc")]`, `[HttpGet]`, `[HttpPost]`
  - Actions: `GetPending()`, `Pass()`, `Fail()`
- **Controller 2:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\AdvancedWarehouseController.cs`
  - Search: `[Route("api/warehouse/v1/qc")]`, `checklist-templates`, `defects`
  - Actions: QC checklist-templates (POST/GET), QC defects (POST/GET)
- **UI Client (verify):** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\ReceivingClient.cs`
  - Search: `api/warehouse/v1/qc`, verify which endpoints are called
- **UI Page:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\ReceivingQc.razor`
  - Search: `ReceivingClient`, verify QC workflow

### Suggested Commits
1. `fix(api): resolve route conflict - move QC templates to /qc-templates route`
2. `fix(api): update AdvancedWarehouseController QC routes to /advanced/qc`

### Risks/Unknowns
- **Breaking change:** If external systems call these endpoints, need API versioning strategy
- **UI impact:** Need to verify which endpoints UI actually uses (audit suggests none for advanced QC)

---

## P0 GAPS (Critical Missing UI - Execute After Bugs)

### G-01: API Key Management UI

**Type:** Gap  
**Priority:** P0  
**Scope:** UI / Client / Nav / Security  
**Status:** `[ ] open`

### Description
No UI page for API Key management. The `api_keys` table exists, `AdminApiKeysController` provides full CRUD (GET, POST, PUT rotate, DELETE), but no UI to manage keys. This is a security gap for admin operations.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/api-keys` with `@page` directive
- [ ] Add `ApiKeysClient` service in `WebUI/Services/` with methods: `GetKeysAsync()`, `CreateKeyAsync()`, `RotateKeyAsync()`, `DeleteKeyAsync()`
- [ ] UI displays list of API keys (masked), creation date, last used, expiry
- [ ] UI provides "Create New Key" button with form (name, permissions, expiry)
- [ ] UI provides "Rotate Key" action (generates new key, invalidates old)
- [ ] UI provides "Delete Key" action with confirmation
- [ ] Add nav entry in `NavMenu.razor` under "admin" section: "API Keys" → `/warehouse/admin/api-keys`
- [ ] Test: Create key, rotate key, delete key, verify operations work

### Evidence Pointers
**Anchor Files to Change:**
- **Nav:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Shared\NavMenu.razor`
  - Search: `_sections`, `admin` section
- **New Page:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\Admin\ApiKeys.razor`
  - Pattern: Copy from `Pages\Admin\Roles.razor` structure (similar CRUD)
- **New Client:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\ApiKeysClient.cs`
  - Pattern: Copy from `Services\AdminConfigurationClient.cs`
  - Endpoints: `GET/POST/PUT/DELETE api/warehouse/v1/admin/api-keys`
- **API Controller (already exists):** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\AdminApiKeysController.cs`
  - Search: `GetAll`, `Create`, `Rotate`, `Delete`

### Suggested Commits
1. `feat(ui): add ApiKeysClient service for API key management`
2. `feat(ui): create API keys management page at /warehouse/admin/api-keys`
3. `feat(nav): add API Keys nav item to admin section`

### Risks/Unknowns
- **Authorization:** Verify `AdminOnly` policy is enforced
- **Key display:** Never show full API key after creation (show once, then mask)
- **Audit logging:** Verify key operations are logged in `security_audit_logs`


---

### G-02: GDPR Erasure Requests UI

**Type:** Gap  
**Priority:** P0  
**Scope:** UI / Client / Nav / Compliance  
**Status:** `[ ] open`

### Description
No UI page for GDPR erasure requests. The `gdpr_erasure_requests` table exists, `AdminGdprController` provides full workflow (POST create, GET list, PUT approve/reject), but no UI. This is a compliance gap.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/gdpr-erasure` with `@page` directive
- [ ] Add `GdprClient` service in `WebUI/Services/` with methods: `GetRequestsAsync()`, `CreateRequestAsync()`, `ApproveRequestAsync()`, `RejectRequestAsync()`
- [ ] UI displays list of erasure requests (user ID, status, requested date, reviewed date)
- [ ] UI provides "New Erasure Request" button with form (user ID, reason)
- [ ] UI provides "Approve" and "Reject" actions for pending requests
- [ ] Add nav entry in `NavMenu.razor` under "admin" section: "GDPR Erasure" → `/warehouse/admin/gdpr-erasure`
- [ ] Test: Create request, approve request, reject request, verify workflow

### Evidence Pointers
**Anchor Files to Change:**
- **Nav:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Shared\NavMenu.razor`
  - Search: `_sections`, `admin` section
- **New Page:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\Admin\GdprErasure.razor`
  - Pattern: Copy from `Pages\Admin\ApprovalRules.razor` (similar approval workflow)
- **New Client:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\GdprClient.cs`
  - Pattern: Copy from `Services\AdminConfigurationClient.cs`
  - Endpoints: `GET/POST/PUT api/warehouse/v1/admin/gdpr`
- **API Controller (already exists):** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\AdminGdprController.cs`
  - Search: `CreateErasureRequest`, `GetRequests`, `Approve`, `Reject`

### Suggested Commits
1. `feat(ui): add GdprClient service for GDPR erasure workflow`
2. `feat(ui): create GDPR erasure requests page at /warehouse/admin/gdpr-erasure`
3. `feat(nav): add GDPR Erasure nav item to admin section`

### Risks/Unknowns
- **Authorization:** Verify `AdminOnly` policy
- **Audit trail:** Ensure all GDPR operations are logged
- **Data deletion scope:** Understand what data is actually erased (check `GdprErasureService`)

---

### G-03: Audit Log Browsing UI

**Type:** Gap  
**Priority:** P0  
**Scope:** UI / Client / Nav / Security  
**Status:** `[ ] open`

### Description
No UI page for browsing security audit logs. The `security_audit_logs` table exists, `AdminAuditLogsController` provides GET endpoint, but no UI. This is a security/compliance gap for audit trail visibility.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/audit-logs` with `@page` directive
- [ ] Add `AuditLogsClient` service in `WebUI/Services/` with method: `GetLogsAsync(filters)`
- [ ] UI displays paginated list of audit logs (timestamp, user, action, resource, IP address)
- [ ] UI provides filters: date range, user, action type, resource type
- [ ] UI provides export to CSV functionality
- [ ] Add nav entry in `NavMenu.razor` under "admin" section: "Audit Logs" → `/warehouse/admin/audit-logs`
- [ ] Test: View logs, apply filters, export logs

### Evidence Pointers
**Anchor Files to Change:**
- **Nav:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Shared\NavMenu.razor`
  - Search: `_sections`, `admin` section
- **New Page:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\Admin\AuditLogs.razor`
  - Pattern: Copy from `Pages\ReportsComplianceAudit.razor` (similar log viewing)
- **New Client:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\AuditLogsClient.cs`
  - Pattern: Copy from `Services\ReportsClient.cs`
  - Endpoints: `GET api/warehouse/v1/admin/audit-logs`
- **API Controller (already exists):** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\AdminAuditLogsController.cs`
  - Search: `GetLogs`, query parameters

### Suggested Commits
1. `feat(ui): add AuditLogsClient service for security audit log viewing`
2. `feat(ui): create audit logs browser page at /warehouse/admin/audit-logs`
3. `feat(nav): add Audit Logs nav item to admin section`

### Risks/Unknowns
- **Authorization:** Verify `AdminOnly` or `Auditor` policy
- **Performance:** Large audit log tables may need pagination/indexing
- **PII:** Ensure sensitive data is masked in logs

---

### G-04: Backup Management UI

**Type:** Gap  
**Priority:** P0  
**Scope:** UI / Client / Nav / Operations  
**Status:** `[ ] open`

### Description
No UI page for backup management. The `backup_executions` table exists, `AdminBackupsController` provides POST trigger, GET list, POST restore, but no UI. This is an operational gap for disaster recovery.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/backups` with `@page` directive
- [ ] Add `BackupsClient` service in `WebUI/Services/` with methods: `TriggerBackupAsync()`, `GetBackupsAsync()`, `RestoreBackupAsync()`
- [ ] UI displays list of backup executions (timestamp, status, size, duration)
- [ ] UI provides "Trigger Backup Now" button
- [ ] UI provides "Restore" action for completed backups (with confirmation)
- [ ] Add nav entry in `NavMenu.razor` under "admin" section: "Backups" → `/warehouse/admin/backups`
- [ ] Test: Trigger backup, view backup list, restore backup (in test environment)

### Evidence Pointers
**Anchor Files to Change:**
- **Nav:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Shared\NavMenu.razor`
  - Search: `_sections`, `admin` section
- **New Page:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Pages\Admin\Backups.razor`
  - Pattern: Copy from `Pages\Admin\Settings.razor` structure
- **New Client:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.WebUI\Services\BackupsClient.cs`
  - Pattern: Copy from `Services\AdminConfigurationClient.cs`
  - Endpoints: `POST/GET api/warehouse/v1/admin/backups`
- **API Controller (already exists):** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Api\Controllers\AdminBackupsController.cs`
  - Search: `TriggerBackup`, `GetBackups`, `RestoreBackup`

### Suggested Commits
1. `feat(ui): add BackupsClient service for backup management`
2. `feat(ui): create backup management page at /warehouse/admin/backups`
3. `feat(nav): add Backups nav item to admin section`

### Risks/Unknowns
- **Authorization:** Verify `AdminOnly` policy
- **Restore safety:** Add prominent warnings for restore operations
- **Long-running operations:** Backup/restore may need progress indicators


---

## P1 GAPS (Important Missing UI)

### G-05: Retention Policy Management UI

**Type:** Gap  
**Priority:** P1  
**Scope:** UI / Client / Nav / Compliance  
**Status:** `[ ] open`

### Description
No UI page for retention policy management. The `retention_policies` and `retention_executions` tables exist, `AdminRetentionPoliciesController` provides full CRUD + execute + legal-hold, but no UI.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/retention-policies` with `@page` directive
- [ ] Add `RetentionPoliciesClient` service with methods: `GetPoliciesAsync()`, `CreatePolicyAsync()`, `UpdatePolicyAsync()`, `DeletePolicyAsync()`, `ExecutePolicyAsync()`, `SetLegalHoldAsync()`
- [ ] UI displays list of retention policies (name, retention period, status, last execution)
- [ ] UI provides CRUD operations for policies
- [ ] UI provides "Execute Now" button for manual execution
- [ ] UI provides "Legal Hold" toggle for policies
- [ ] Add nav entry in `NavMenu.razor` under "admin" section
- [ ] Test: Create policy, execute policy, set legal hold

### Evidence Pointers
- **Nav:** `NavMenu.razor` → `admin` section
- **New Page:** `Pages\Admin\RetentionPolicies.razor`
- **New Client:** `Services\RetentionPoliciesClient.cs`
- **API Controller:** `Controllers\AdminRetentionPoliciesController.cs`

### Suggested Commits
1. `feat(ui): add RetentionPoliciesClient service`
2. `feat(ui): create retention policies management page`
3. `feat(nav): add Retention Policies nav item`

### Risks/Unknowns
- **Data deletion:** Understand scope of retention policy execution
- **Legal hold implications:** Verify legal hold prevents deletion

---

### G-06: Disaster Recovery Drills UI

**Type:** Gap  
**Priority:** P1  
**Scope:** UI / Client / Nav / Operations  
**Status:** `[ ] open`

### Description
No UI page for disaster recovery drills. The `dr_drills` table exists, `AdminDisasterRecoveryController` provides POST drill, GET drills, but no UI.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/dr-drills` with `@page` directive
- [ ] Add `DisasterRecoveryClient` service with methods: `TriggerDrillAsync()`, `GetDrillsAsync()`
- [ ] UI displays list of DR drills (timestamp, type, status, duration, results)
- [ ] UI provides "Start DR Drill" button with drill type selection
- [ ] Add nav entry in `NavMenu.razor` under "admin" section
- [ ] Test: Trigger drill, view drill history

### Evidence Pointers
- **Nav:** `NavMenu.razor` → `admin` section
- **New Page:** `Pages\Admin\DisasterRecoveryDrills.razor`
- **New Client:** `Services\DisasterRecoveryClient.cs`
- **API Controller:** `Controllers\AdminDisasterRecoveryController.cs`

### Suggested Commits
1. `feat(ui): add DisasterRecoveryClient service`
2. `feat(ui): create DR drills management page`
3. `feat(nav): add DR Drills nav item`

### Risks/Unknowns
- **Drill impact:** Understand what DR drills actually do (read-only tests vs. actual failover)

---

### G-07: Agnum Export History UI

**Type:** Gap  
**Priority:** P1  
**Scope:** UI / Client  
**Status:** `[ ] open`

### Description
Agnum export history (`agnum_export_history` table) has API endpoint `GET api/warehouse/v1/agnum/history` but no WebUI client method and no UI page. `AgnumClient` is missing `GetHistoryAsync()`.

### Definition of Done
- [ ] Add `GetHistoryAsync()` method to existing `AgnumClient` service
- [ ] Add history section to existing `/warehouse/agnum/config` page OR create new `/warehouse/agnum/history` page
- [ ] UI displays list of export history (timestamp, status, record count, errors)
- [ ] UI provides "View Details" action for each export
- [ ] Test: View export history, verify details display

### Evidence Pointers
- **Existing Client:** `Services\AgnumClient.cs`
  - Search: `GetConfigAsync`, add `GetHistoryAsync()` method
- **Existing Page:** `Pages\Agnum\Configuration.razor`
  - Add history tab/section
- **API Controller:** `Controllers\AgnumController.cs`
  - Search: `GetHistory`, `GetHistoryById`

### Suggested Commits
1. `feat(ui): add GetHistoryAsync to AgnumClient`
2. `feat(ui): add export history section to Agnum configuration page`

### Risks/Unknowns
- **UI placement:** Decide if history belongs on config page or separate page

---

### G-08: Serial Number Management UI

**Type:** Gap  
**Priority:** P1  
**Scope:** UI / Client / Nav  
**Status:** `[ ] open`

### Description
No UI page for serial number management. The `serial_numbers` table exists, `AdvancedWarehouseController` provides POST/GET `/serials` endpoints, but no UI.

### Definition of Done
- [ ] Create new Blazor page at `/warehouse/admin/serial-numbers` with `@page` directive
- [ ] Add `SerialNumbersClient` service with methods: `GetSerialsAsync()`, `CreateSerialAsync()`, `UpdateStatusAsync()`
- [ ] UI displays list of serial numbers (serial, item, status, location)
- [ ] UI provides search/filter by item, status, location
- [ ] UI provides "Register Serial" button
- [ ] Add nav entry in `NavMenu.razor` under "admin" section
- [ ] Test: Register serial, search serials, update status

### Evidence Pointers
- **Nav:** `NavMenu.razor` → `admin` section
- **New Page:** `Pages\Admin\SerialNumbers.razor`
- **New Client:** `Services\SerialNumbersClient.cs`
- **API Controller:** `Controllers\AdvancedWarehouseController.cs`
  - Search: `[Route("api/warehouse/v1/serials")]`

### Suggested Commits
1. `feat(ui): add SerialNumbersClient service`
2. `feat(ui): create serial numbers management page`
3. `feat(nav): add Serial Numbers nav item`

### Risks/Unknowns
- **Serial tracking scope:** Understand full serial number lifecycle


---

### G-10: Master Data CRUD Operations (Read-Only UI Issue)

**Type:** Gap  
**Priority:** P1  
**Scope:** UI / Client  
**Status:** `[ ] open`

### Description
AdminItems, AdminSuppliers, AdminLocations, AdminCategories, AdminSupplierMappings pages are READ-ONLY in UI. `MasterDataAdminClient` only calls GET endpoints. POST/PUT/DELETE endpoints exist in API but are never called from WebUI. Cannot create or update master data records from UI.

### Definition of Done
- [ ] Add POST/PUT/DELETE methods to `MasterDataAdminClient` for: Items, Suppliers, Locations, Categories, SupplierMappings
- [ ] Update `AdminItems.razor`: Add "Create Item" button, "Edit" action, "Deactivate" action
- [ ] Update `AdminSuppliers.razor`: Add "Create Supplier" button, "Edit" action
- [ ] Update `AdminLocations.razor`: Add "Create Location" button, "Edit" action
- [ ] Update `AdminCategories.razor`: Add "Create Category" button, "Edit" action (DELETE already exists)
- [ ] Update `AdminSupplierMappings.razor`: Add "Create Mapping" button, "Edit" action
- [ ] Test: Create, update, delete operations for each entity type

### Evidence Pointers
- **Client to Modify:** `Services\MasterDataAdminClient.cs`
  - Search: `GetItemsAsync`, add `CreateItemAsync`, `UpdateItemAsync`, `DeactivateItemAsync`
  - Search: `GetSuppliersAsync`, add `CreateSupplierAsync`, `UpdateSupplierAsync`
  - Search: `GetLocationsAsync`, add `CreateLocationAsync`, `UpdateLocationAsync`
  - Search: `GetCategoriesAsync`, add `CreateCategoryAsync`, `UpdateCategoryAsync`
  - Search: `GetSupplierMappingsAsync`, add `CreateMappingAsync`, `UpdateMappingAsync`
- **Pages to Modify:**
  - `Pages\AdminItems.razor`
  - `Pages\AdminSuppliers.razor`
  - `Pages\AdminLocations.razor`
  - `Pages\AdminCategories.razor`
  - `Pages\AdminSupplierMappings.razor`
- **API Controllers (already exist):**
  - `Controllers\ItemsController.cs` → POST, PUT, POST deactivate
  - `Controllers\SuppliersController.cs` → POST, PUT
  - `Controllers\LocationsController.cs` → POST, PUT
  - `Controllers\CategoriesController.cs` → POST, PUT
  - `Controllers\SupplierItemMappingsController.cs` → POST, PUT

### Suggested Commits
1. `feat(ui): add CRUD methods to MasterDataAdminClient for all entities`
2. `feat(ui): add create/edit/delete UI for Items page`
3. `feat(ui): add create/edit UI for Suppliers page`
4. `feat(ui): add create/edit UI for Locations page`
5. `feat(ui): add create/edit UI for Categories page`
6. `feat(ui): add create/edit UI for Supplier Mappings page`

### Risks/Unknowns
- **Validation:** Understand validation rules for each entity type
- **Authorization:** Verify create/update/delete policies
- **Referential integrity:** Understand cascade delete behavior

---

## P2 GAPS (Lower Priority - Nice to Have)

### G-11: Location Balance Endpoint Unused

**Type:** Gap  
**Priority:** P2  
**Scope:** API / Client  
**Status:** `[ ] open`

### Description
`GET api/warehouse/v1/stock/location-balance` endpoint exists and is wired to `GetLocationBalanceQueryHandler`, but no WebUI client calls it. Marten `LocationBalance` read model is unused by UI.

### Definition of Done
- [ ] Option A: Add location balance view to existing stock pages (e.g., Stock Dashboard)
- [ ] Option B: Create new page `/warehouse/stock/location-balance`
- [ ] Add `GetLocationBalanceAsync()` method to `StockClient`
- [ ] UI displays stock by location (location, item, quantity, reserved, available)
- [ ] Test: View location balance, verify data accuracy

### Evidence Pointers
- **Client to Modify:** `Services\StockClient.cs`
- **API Controller:** `Controllers\StockController.cs` → `GetLocationBalance`
- **Query Handler:** `Application\Queries\GetLocationBalanceQueryHandler.cs`

### Suggested Commits
1. `feat(ui): add location balance view to stock dashboard`

### Risks/Unknowns
- **UI placement:** Decide where location balance fits best
- **Performance:** Large location balance queries may need pagination

---

### G-12: Valuation Initialize/Adjust-Cost Endpoints Unused

**Type:** Gap  
**Priority:** P2  
**Scope:** API / Client  
**Status:** `[ ] open`

### Description
`ValuationController` has `POST(initialize)` and `POST({itemId}/adjust-cost)` endpoints but no UI consumers. `ValuationClient` only calls base `adjust-cost`.

### Definition of Done
- [ ] Understand use case for `initialize` endpoint (initial valuation setup?)
- [ ] Understand difference between base `adjust-cost` and `{itemId}/adjust-cost`
- [ ] Option A: Add UI for these operations if needed
- [ ] Option B: Remove endpoints if not needed
- [ ] Document decision

### Evidence Pointers
- **API Controller:** `Controllers\ValuationController.cs`
- **Client:** `Services\ValuationClient.cs`
- **Pages:** `Pages\Valuation\AdjustCost.razor`

### Suggested Commits
1. `docs: document valuation endpoint usage decisions`

### Risks/Unknowns
- **Business logic:** Need to understand valuation workflow requirements


---

### G-13: Compliance Endpoints Unused

**Type:** Gap  
**Priority:** P2  
**Scope:** API / Client  
**Status:** `[ ] open`

### Description
`AdminComplianceController` has multiple endpoints not called by any WebUI client: `POST(sign)`, `GET(signatures/{id})`, `POST(verify-hash-chain)`, `GET(validation-report)`, `POST(export-transactions)`, `GET(exports)`. Compliance pages only use dashboard, lot-trace, scheduled-reports.

### Definition of Done
- [ ] Understand use cases for each unused endpoint
- [ ] Option A: Add UI for electronic signatures, hash chain verification, transaction exports
- [ ] Option B: Document as API-only features for external integrations
- [ ] Option C: Remove if not needed

### Evidence Pointers
- **API Controller:** `Controllers\AdminComplianceController.cs`
- **Client:** `Services\ReportsClient.cs` (compliance methods)
- **Pages:** `Pages\ComplianceDashboard.razor`, `Pages\ComplianceLotTrace.razor`

### Suggested Commits
1. `docs: document compliance API-only features`

### Risks/Unknowns
- **Compliance requirements:** May be required for regulatory compliance even without UI

---

### G-14: Barcode Lookup Endpoint Unused

**Type:** Gap  
**Priority:** P2  
**Scope:** API / Client  
**Status:** `[ ] open`

### Description
`BarcodesController GET api/warehouse/v1/barcodes/lookup` endpoint exists but is not consumed by any WebUI client.

### Definition of Done
- [ ] Understand use case for barcode lookup
- [ ] Option A: Add barcode lookup to relevant pages (receiving, picking, etc.)
- [ ] Option B: Document as API-only feature
- [ ] Option C: Remove if not needed

### Evidence Pointers
- **API Controller:** `Controllers\BarcodesController.cs`

### Suggested Commits
1. `feat(ui): add barcode lookup to receiving/picking pages`

### Risks/Unknowns
- **Integration:** May be used by barcode scanners/mobile devices

---

### G-15: Feature Flags Endpoint Unused

**Type:** Gap  
**Priority:** P2  
**Scope:** API / Client  
**Status:** `[ ] open`

### Description
`FeatureFlagsController GET api/warehouse/v1/features/{flagKey}` endpoint exists but is not consumed by any WebUI client. LaunchDarkly is used internally in services only.

### Definition of Done
- [ ] Option A: Add feature flag UI for admin management
- [ ] Option B: Document as internal-only feature
- [ ] Option C: Remove endpoint if not needed

### Evidence Pointers
- **API Controller:** `Controllers\FeatureFlagsController.cs`
- **Service:** `Services\FeatureFlagService.cs`

### Suggested Commits
1. `docs: document feature flags as internal-only`

### Risks/Unknowns
- **LaunchDarkly integration:** May be managed externally

---

### G-16: QC Checklist Templates & Defects Endpoints Unused

**Type:** Gap  
**Priority:** P2  
**Scope:** API / Client  
**Status:** `[ ] open`

### Description
`AdvancedWarehouseController` has QC checklist-templates endpoints (POST/GET) and QC defects (GET all) with no WebUI consumer.

### Definition of Done
- [ ] Understand QC checklist templates use case
- [ ] Option A: Add QC templates management UI
- [ ] Option B: Document as API-only feature
- [ ] Option C: Remove if not needed

### Evidence Pointers
- **API Controller:** `Controllers\AdvancedWarehouseController.cs`
  - Search: `checklist-templates`, `defects`

### Suggested Commits
1. `feat(ui): add QC templates management page`

### Risks/Unknowns
- **QC workflow:** Need to understand full QC process requirements

---

### G-17: Handling Unit Split/Merge/Hierarchy Endpoints Unused

**Type:** Gap  
**Priority:** P2  
**Scope:** API / Client  
**Status:** `[ ] open`

### Description
`AdvancedWarehouseController` has HU split/merge/hierarchy endpoints with no WebUI consumer.

### Definition of Done
- [ ] Understand handling unit split/merge use cases
- [ ] Option A: Add HU management UI
- [ ] Option B: Document as API-only feature
- [ ] Option C: Remove if not needed

### Evidence Pointers
- **API Controller:** `Controllers\AdvancedWarehouseController.cs`
  - Search: `handling-units`, `split`, `merge`, `hierarchy`

### Suggested Commits
1. `feat(ui): add handling unit management page`

### Risks/Unknowns
- **HU workflow:** Need to understand full HU lifecycle

---

### G-18: Lots Table No Dedicated Endpoint/UI

**Type:** Gap  
**Priority:** P2  
**Scope:** API / UI  
**Status:** `[ ] open`

### Description
`lots` table has no dedicated API endpoint and no UI page for lot listing/management. Lots are created via receiving but never listed.

### Definition of Done
- [ ] Create `LotsController` with GET endpoint for lot listing
- [ ] Create `LotsClient` service
- [ ] Create `/warehouse/admin/lots` page
- [ ] UI displays list of lots (lot number, item, quantity, expiry, status)
- [ ] Add nav entry

### Evidence Pointers
- **New Controller:** `Controllers\LotsController.cs`
- **New Client:** `Services\LotsClient.cs`
- **New Page:** `Pages\Admin\Lots.razor`
- **DB Table:** `lots` in `WarehouseDbContextModelSnapshot.cs`

### Suggested Commits
1. `feat(api): add LotsController for lot management`
2. `feat(ui): add lots management page`

### Risks/Unknowns
- **Lot tracking scope:** Understand full lot traceability requirements

---

### G-19: UoM and Conversions No Management UI

**Type:** Gap  
**Priority:** P2  
**Scope:** API / UI  
**Status:** `[ ] open`

### Description
`handling_unit_types`, `unit_of_measures`, `item_uom_conversions` tables have no dedicated API or UI. Items reference UoM but no management screen exists.

### Definition of Done
- [ ] Create controllers for UoM, HU types, UoM conversions
- [ ] Create client services
- [ ] Create management pages
- [ ] Add nav entries

### Evidence Pointers
- **DB Tables:** `unit_of_measures`, `handling_unit_types`, `item_uom_conversions` in snapshot

### Suggested Commits
1. `feat(api): add UoM management endpoints`
2. `feat(ui): add UoM management pages`

### Risks/Unknowns
- **UoM complexity:** Understand conversion rules and validation

---

### G-20: Permissions Table No Management UI

**Type:** Gap  
**Priority:** P2  
**Scope:** UI / Client  
**Status:** `[ ] open`

### Description
`permissions` table exposed via `AdminPermissionsController` (GET list + POST check) but no UI page. Roles page manages role-permission assignments but permission definitions are not manageable from UI.

### Definition of Done
- [ ] Create `/warehouse/admin/permissions` page
- [ ] Add `PermissionsClient` service
- [ ] UI displays list of permissions (name, description, category)
- [ ] UI is read-only (permissions are code-defined, not user-created)
- [ ] Add nav entry

### Evidence Pointers
- **API Controller:** `Controllers\AdminPermissionsController.cs`
- **New Client:** `Services\PermissionsClient.cs`
- **New Page:** `Pages\Admin\Permissions.razor`

### Suggested Commits
1. `feat(ui): add permissions browser page (read-only)`

### Risks/Unknowns
- **Permission model:** Understand if permissions are code-defined or data-driven


---

## ARCHITECTURAL ISSUES

### A-01: ARCH-02 Tech Debt - Business Logic in Api/Services

**Type:** Architecture  
**Priority:** P1 (Long-term refactor)  
**Scope:** API / Application / Architecture  
**Status:** `[ ] open`

### Description
34 service files in `Api/Services/` contain business logic that belongs in Application layer. Controllers inject `DbContext` and `IDocumentStore` directly, bypassing MediatR pipeline. This violates clean architecture principles and makes testing difficult.

### Definition of Done
- [ ] Audit all 34 service files in `Api/Services/`
- [ ] Create migration plan: prioritize high-value services first
- [ ] For each service:
  - [ ] Create corresponding Command/Query handlers in Application layer
  - [ ] Move business logic from service to handler
  - [ ] Update controller to use MediatR instead of direct service injection
  - [ ] Update tests
- [ ] Document architectural decision records (ADRs)

### Evidence Pointers
- **Services to Migrate:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Api\Services\**\*Service.cs`
  - Examples: `AgnumExportServices.cs`, `CycleCountServices.cs`, `TransferServices.cs`, `ValuationCommandHandlers.cs`
- **Target Pattern:** `src\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Application\Commands\**\*CommandHandler.cs`
- **Controllers to Update:** All controllers that inject services directly

### Suggested Commits
(Per service migration)
1. `refactor(app): migrate AgnumExportServices to Application layer`
2. `refactor(api): update AgnumController to use MediatR handlers`

### Risks/Unknowns
- **Large refactor:** 34 services is significant work
- **Breaking changes:** May affect existing integrations
- **Testing:** Need comprehensive test coverage before refactoring
- **Prioritization:** Should be done incrementally, not all at once

---

### A-02: WarehouseLocationDetail Page Not in Nav

**Type:** Architecture  
**Priority:** P2  
**Scope:** UI / Nav  
**Status:** `[ ] open`

### Description
`WarehouseLocationDetail.razor` at `/warehouse/locations/{Id:int}` is not reachable from nav. Injected client is unverified. Likely navigated from AdminLocations page. Needs explicit nav path or deregistration.

### Definition of Done
- [ ] Verify `WarehouseLocationDetail.razor` is navigated from `AdminLocations.razor`
- [ ] Verify client injection works correctly
- [ ] Option A: Add breadcrumb navigation from AdminLocations
- [ ] Option B: Add "View Details" link in AdminLocations table
- [ ] Option C: Remove page if not used
- [ ] Test navigation flow

### Evidence Pointers
- **Page:** `Pages\WarehouseLocationDetail.razor`
- **Parent Page:** `Pages\AdminLocations.razor`
  - Search: `NavigationManager.NavigateTo`, `/warehouse/locations/`

### Suggested Commits
1. `fix(ui): add navigation link to WarehouseLocationDetail from AdminLocations`

### Risks/Unknowns
- **Usage:** Need to verify if this page is actually used

---

## STOP CASES FOR IMPLEMENTATION AGENT

When implementing items from this backlog, STOP and ask for user guidance if you encounter:

1. **Build Failing Outside Scope**
   - Compilation errors in files not related to the current task
   - Missing dependencies or packages
   - Configuration issues

2. **Missing Backend**
   - API endpoint doesn't exist when expected
   - Service/handler not implemented
   - Database table/column missing

3. **Unclear Auth**
   - Authorization policy not defined
   - Role requirements ambiguous
   - Permission model unclear

4. **Ambiguous Nav**
   - Multiple possible nav locations
   - Nav structure conflicts
   - Unclear nav hierarchy

5. **Broad Refactor Required**
   - Task requires changes across many files
   - Architectural changes needed
   - Breaking changes to existing functionality

6. **Route Conflicts Not Solvable Minimally**
   - Multiple controllers claim same route
   - Cannot resolve without major refactoring
   - Requires API versioning strategy

7. **Data Model Unclear**
   - Database schema ambiguous
   - Entity relationships unclear
   - Validation rules unknown

8. **External Dependencies**
   - Third-party service integration required
   - External API not documented
   - Configuration secrets needed

---

## EXECUTION ORDER SUMMARY

### Phase 1: Owner Priority (Execute First)
1. **G-09** - Warehouse Layout Editor UI

### Phase 2: Critical Bugs (P0)
2. **B-01** - Dead Dispatch History Endpoint
3. **B-02** - Dead QCPanel Redirect Page
4. **B-03** - Test Endpoint Exposed in Production
5. **B-04** - Route Conflict Between Controllers

### Phase 3: Critical Gaps (P0)
6. **G-01** - API Key Management UI
7. **G-02** - GDPR Erasure Requests UI
8. **G-03** - Audit Log Browsing UI
9. **G-04** - Backup Management UI

### Phase 4: Important Gaps (P1)
10. **G-05** - Retention Policy Management UI
11. **G-06** - Disaster Recovery Drills UI
12. **G-07** - Agnum Export History UI
13. **G-08** - Serial Number Management UI
14. **G-10** - Master Data CRUD Operations (Read-Only Fix)

### Phase 5: Lower Priority Gaps (P2)
15. **G-11** through **G-20** - Various unused endpoints and missing UI

### Phase 6: Architectural (Long-term)
16. **A-01** - ARCH-02 Tech Debt Migration
17. **A-02** - WarehouseLocationDetail Nav Fix

---

## NOTES

- All file paths are relative to repo root: `C:\Sources\clients\lauresta\lkvitai-mes`
- Use Windows path separators (`\`) when referencing files
- All Blazor pages use `@page` directive for routing
- All API controllers use `[Route]` attribute for routing
- WebUI clients follow pattern: `*Client.cs` in `WebUI/Services/`
- Nav menu is data-driven from `NavMenu.razor` `_sections` array
- Authorization policies defined in `WarehousePolicies` class
- EF Core migrations in `Infrastructure/Persistence/Migrations/`
- Marten projections in `Infrastructure/Persistence/Marten*Repository.cs`

---

**END OF BACKLOG**
