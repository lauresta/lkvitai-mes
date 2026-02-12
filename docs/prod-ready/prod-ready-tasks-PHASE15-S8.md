# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 8 (Execution Pack)

**Version:** 2.0
**Date:** February 12, 2026
**Sprint:** Phase 1.5 Sprint 8
**Source:** prod-ready-universe.md, prod-ready-tasks-progress.md
**Status:** SPEC COMPLETE (NO PLACEHOLDERS) - Ready for Codex Execution
**BATON:** 2026-02-12T14:00:00Z-PHASE15-S8-SPEC-COMPLETE-e8f2a4b6

---

## Sprint Overview

**Sprint Goal:** Complete admin configuration, security hardening, compliance & traceability, and data retention/GDPR for production readiness.

**Sprint Duration:** 2 weeks
**Total Tasks:** 20
**Estimated Effort:** 19 days

**Focus Areas:**
1. **Admin Configuration:** Warehouse settings, reason codes, approval rules, user roles, config UI
2. **Security Hardening:** SSO/OAuth integration, MFA, API key management, RBAC granular permissions, audit log
3. **Compliance & Traceability:** Full transaction log export, lot traceability report, variance analysis, compliance reports, FDA 21 CFR Part 11
4. **Data Retention & GDPR:** Retention policies, PII encryption, GDPR erasure, backup/restore procedures, disaster recovery

**Dependencies:**
- Sprint 7 complete (PRD-1601 to PRD-1620)

---

## Sprint 8 Task Index

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-1621 | Admin Config | Warehouse Settings Entity | M | None | Backend/API | Universe §4.Epic P |
| PRD-1622 | Admin Config | Reason Code Management | M | PRD-1621 | Backend/API | Universe §4.Epic P |
| PRD-1623 | Admin Config | Approval Rules Engine | M | PRD-1621 | Backend/API | Universe §4.Epic P |
| PRD-1624 | Admin Config | User Role Management | M | PRD-1547 | Backend/API | Universe §4.Epic P |
| PRD-1625 | Admin Config | Admin Configuration UI | M | PRD-1621-1624 | UI | Universe §4.Epic P |
| PRD-1626 | Security | SSO/OAuth Integration | L | None | Backend/API | Universe §5.Security |
| PRD-1627 | Security | MFA Implementation | M | PRD-1626 | Backend/API | Universe §5.Security |
| PRD-1628 | Security | API Key Management | M | None | Backend/API | Universe §5.Security |
| PRD-1629 | Security | RBAC Granular Permissions | M | PRD-1624 | Backend/API | Universe §5.Security |
| PRD-1630 | Security | Security Audit Log | M | PRD-1629 | Backend/API | Universe §5.Security |
| PRD-1631 | Compliance | Transaction Log Export | M | PRD-1550 | Backend/API | Universe §5.Compliance |
| PRD-1632 | Compliance | Lot Traceability Report | M | PRD-1551 | UI/Backend | Universe §5.Compliance |
| PRD-1633 | Compliance | Variance Analysis Report | M | PRD-1614 | UI/Backend | Universe §5.Compliance |
| PRD-1634 | Compliance | Compliance Reports Dashboard | M | PRD-1631-1633 | UI | Universe §5.Compliance |
| PRD-1635 | Compliance | FDA 21 CFR Part 11 Compliance | L | PRD-1630 | Backend/API | Universe §5.Compliance |
| PRD-1636 | Data Retention | Retention Policy Engine | M | None | Backend/API | Universe §5.DataRetention |
| PRD-1637 | Data Retention | PII Encryption | M | None | Backend/API | Universe §5.DataRetention |
| PRD-1638 | Data Retention | GDPR Erasure Workflow | M | PRD-1637 | Backend/API | Universe §5.DataRetention |
| PRD-1639 | Data Retention | Backup/Restore Procedures | M | None | Infra/DevOps | Universe §5.DataRetention |
| PRD-1640 | Data Retention | Disaster Recovery Plan | M | PRD-1639 | Infra/DevOps | Universe §5.DataRetention |

**Total Effort:** 19 days

---
## Task PRD-1621: Warehouse Settings Entity

**Epic:** Admin Config | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §4.Epic P

### Context
Warehouse-level configuration entity for thresholds, rules, and defaults. Enables runtime configuration without code deployment.

### Scope
**In Scope:** WarehouseSettings entity (capacity thresholds, FEFO/FIFO default, low stock alerts), CRUD APIs, validation
**Out of Scope:** Multi-tenant configuration (single warehouse for Phase 1.5)

### Requirements
**Functional:**
1. WarehouseSettings entity: CapacityThresholdPercent (default 80%), DefaultPickStrategy (FEFO/FIFO), LowStockThreshold, ReorderPoint, AutoAllocateOrders (bool)
2. CRUD APIs: GET/PUT /api/warehouse/v1/admin/settings
3. Validation: CapacityThreshold 0-100%, thresholds >= 0
4. Singleton pattern (one settings record per warehouse)

**Non-Functional:**
1. Settings cached in memory (refresh on update)
2. API response < 100ms
3. Audit logging for all changes

**RBAC:** Admin role required for PUT

**Data Model:**
```csharp
public class WarehouseSettings {
  public int Id { get; set; } // Always 1 (singleton)
  public int CapacityThresholdPercent { get; set; } // 80
  public PickStrategy DefaultPickStrategy { get; set; } // FEFO
  public int LowStockThreshold { get; set; } // 10
  public int ReorderPoint { get; set; } // 50
  public bool AutoAllocateOrders { get; set; } // true
  public string UpdatedBy { get; set; }
  public DateTime UpdatedAt { get; set; }
}
```

**API:**
- GET /api/warehouse/v1/admin/settings
- PUT /api/warehouse/v1/admin/settings

### Acceptance Criteria
```gherkin
Scenario: Update capacity threshold
  Given logged in as Admin
  When PUT /api/warehouse/v1/admin/settings with CapacityThresholdPercent=85
  Then settings updated
  And audit log created with Admin username
  And response status 200

Scenario: Validation error for invalid threshold
  Given logged in as Admin
  When PUT /api/warehouse/v1/admin/settings with CapacityThresholdPercent=150
  Then validation fails "Capacity threshold must be 0-100%"
  And response status 400

Scenario: Authorization check
  Given logged in as Operator
  When PUT /api/warehouse/v1/admin/settings
  Then response status 403 Forbidden
```

### Validation
```bash
curl -X POST http://localhost:5000/api/auth/dev-token -H "Content-Type: application/json" \
  -d '{"username":"admin","roles":["Admin"]}' | jq -r '.token' > /tmp/token.txt
TOKEN=$(cat /tmp/token.txt)

curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/settings

curl -H "Authorization: Bearer $TOKEN" -X PUT http://localhost:5000/api/warehouse/v1/admin/settings \
  -H "Content-Type: application/json" -d '{"capacityThresholdPercent":85,"defaultPickStrategy":"FEFO","lowStockThreshold":10,"reorderPoint":50,"autoAllocateOrders":true}'
```

### Definition of Done
- [ ] WarehouseSettings entity created in src/LKvitai.MES.Domain/Entities/
- [ ] EF migration generated and applied
- [ ] GET/PUT endpoints in AdminController.cs
- [ ] In-memory cache with refresh on update
- [ ] Validation rules enforced
- [ ] Unit tests: validation, cache refresh (10+ tests)
- [ ] Integration test: update settings → verify cache
- [ ] RBAC enforcement (Admin only)
- [ ] Audit logging implemented
- [ ] Documentation updated

---

## Task PRD-1622: Reason Code Management

**Epic:** Admin Config | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1621 | **SourceRefs:** Universe §4.Epic P

### Context
Hierarchical taxonomy for adjustment/revaluation reasons. Enables custom reason codes without code changes.

### Scope
**In Scope:** ReasonCode entity (hierarchical with ParentId), CRUD APIs, usage tracking, active/inactive flag
**Out of Scope:** Multi-language reason codes (English only for Phase 1.5)

### Requirements
**Functional:**
1. ReasonCode entity: Code (unique), Name, Description, ParentId (nullable for hierarchy), Category (ADJUSTMENT, REVALUATION, WRITEDOWN, RETURN), Active (bool), UsageCount
2. CRUD APIs: GET/POST/PUT/DELETE /api/warehouse/v1/admin/reason-codes
3. Validation: Code unique, Name required (min 3 chars), Category required
4. Hierarchy: Max 2 levels (parent → child), prevent circular references
5. Usage tracking: Increment UsageCount when reason code used in adjustment/revaluation
6. Soft delete: Cannot delete if UsageCount > 0, mark inactive instead

**Non-Functional:**
1. API response < 200ms
2. Audit logging for all changes

**RBAC:** Admin role required for POST/PUT/DELETE, Manager can view

**Data Model:**
```csharp
public class ReasonCode {
  public int Id { get; set; }
  public string Code { get; set; } // DAMAGE-FORKLIFT
  public string Name { get; set; } // Forklift Damage
  public string Description { get; set; }
  public int? ParentId { get; set; }
  public ReasonCategory Category { get; set; } // ADJUSTMENT
  public bool Active { get; set; } // true
  public int UsageCount { get; set; } // 0
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
}
```

**API:**
- GET /api/warehouse/v1/admin/reason-codes (with filters: category, active)
- POST /api/warehouse/v1/admin/reason-codes
- PUT /api/warehouse/v1/admin/reason-codes/{id}
- DELETE /api/warehouse/v1/admin/reason-codes/{id}

### Acceptance Criteria
```gherkin
Scenario: Create hierarchical reason code
  Given logged in as Admin
  And parent reason code "DAMAGE" exists with Id=1
  When POST /api/warehouse/v1/admin/reason-codes with Code="DAMAGE-FORKLIFT", Name="Forklift Damage", ParentId=1, Category="ADJUSTMENT"
  Then reason code created
  And response status 201

Scenario: Prevent deletion of used reason code
  Given reason code "DAMAGE-FORKLIFT" with UsageCount=5
  When DELETE /api/warehouse/v1/admin/reason-codes/2
  Then validation fails "Cannot delete reason code with usage history. Mark inactive instead."
  And response status 400

Scenario: Filter active reason codes
  Given 10 reason codes (5 active, 5 inactive)
  When GET /api/warehouse/v1/admin/reason-codes?active=true
  Then response contains 5 active reason codes only
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/reason-codes

curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/reason-codes \
  -H "Content-Type: application/json" -d '{"code":"DAMAGE-FORKLIFT","name":"Forklift Damage","category":"ADJUSTMENT","active":true}'
```

### Definition of Done
- [ ] ReasonCode entity created
- [ ] EF migration generated
- [ ] CRUD endpoints in AdminController.cs
- [ ] Hierarchical validation (max 2 levels, no circular refs)
- [ ] Usage tracking integration (increment on adjustment/revaluation)
- [ ] Soft delete logic
- [ ] Unit tests: validation, hierarchy, soft delete (15+ tests)
- [ ] Integration test: create → use → attempt delete
- [ ] RBAC enforcement
- [ ] Audit logging
- [ ] Documentation updated

---

## Task PRD-1623: Approval Rules Engine

**Epic:** Admin Config | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1621 | **SourceRefs:** Universe §4.Epic P

### Context
Configurable approval rules for cost adjustments, write-downs, transfers. Enables business rule changes without code deployment.

### Scope
**In Scope:** ApprovalRule entity (conditions, thresholds, approver roles), rule evaluation engine, API
**Out of Scope:** Complex rule expressions (simple threshold-based only for Phase 1.5)

### Requirements
**Functional:**
1. ApprovalRule entity: RuleType (COST_ADJUSTMENT, WRITEDOWN, TRANSFER), ThresholdType (AMOUNT, PERCENTAGE), ThresholdValue, ApproverRole, Active
2. Rule evaluation: Check if operation requires approval based on rules
3. API: GET/POST/PUT/DELETE /api/warehouse/v1/admin/approval-rules
4. Evaluation API: POST /api/warehouse/v1/admin/approval-rules/evaluate (input: operation type, value → output: required approver role)
5. Validation: ThresholdValue >= 0, ApproverRole must exist

**Non-Functional:**
1. Rule evaluation < 50ms
2. Rules cached in memory (refresh on update)

**RBAC:** Admin role required for CRUD, all roles can call evaluate

**Data Model:**
```csharp
public class ApprovalRule {
  public int Id { get; set; }
  public RuleType RuleType { get; set; } // COST_ADJUSTMENT
  public ThresholdType ThresholdType { get; set; } // AMOUNT
  public decimal ThresholdValue { get; set; } // 10000
  public string ApproverRole { get; set; } // Manager
  public bool Active { get; set; } // true
  public int Priority { get; set; } // 1 (lower = higher priority)
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
}
```

**API:**
- GET /api/warehouse/v1/admin/approval-rules
- POST /api/warehouse/v1/admin/approval-rules
- PUT /api/warehouse/v1/admin/approval-rules/{id}
- DELETE /api/warehouse/v1/admin/approval-rules/{id}
- POST /api/warehouse/v1/admin/approval-rules/evaluate

### Acceptance Criteria
```gherkin
Scenario: Create approval rule for large cost adjustments
  Given logged in as Admin
  When POST /api/warehouse/v1/admin/approval-rules with RuleType="COST_ADJUSTMENT", ThresholdType="AMOUNT", ThresholdValue=10000, ApproverRole="Manager"
  Then rule created
  And response status 201

Scenario: Evaluate approval requirement
  Given approval rule: COST_ADJUSTMENT > $10000 requires Manager
  When POST /api/warehouse/v1/admin/approval-rules/evaluate with RuleType="COST_ADJUSTMENT", Value=15000
  Then response: {"requiresApproval":true,"approverRole":"Manager"}

Scenario: No approval required below threshold
  Given approval rule: COST_ADJUSTMENT > $10000 requires Manager
  When POST /api/warehouse/v1/admin/approval-rules/evaluate with RuleType="COST_ADJUSTMENT", Value=5000
  Then response: {"requiresApproval":false}
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/approval-rules \
  -H "Content-Type: application/json" -d '{"ruleType":"COST_ADJUSTMENT","thresholdType":"AMOUNT","thresholdValue":10000,"approverRole":"Manager","active":true,"priority":1}'

curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/approval-rules/evaluate \
  -H "Content-Type: application/json" -d '{"ruleType":"COST_ADJUSTMENT","value":15000}'
```

### Definition of Done
- [ ] ApprovalRule entity created
- [ ] EF migration generated
- [ ] CRUD endpoints in AdminController.cs
- [ ] Rule evaluation engine with caching
- [ ] Evaluate API endpoint
- [ ] Unit tests: rule evaluation logic, priority handling (15+ tests)
- [ ] Integration test: create rule → evaluate → verify result
- [ ] RBAC enforcement
- [ ] Audit logging
- [ ] Documentation updated

---

## Task PRD-1624: User Role Management

**Epic:** Admin Config | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1547 | **SourceRefs:** Universe §4.Epic P

### Context
Role entity with permission matrix. Enables custom role definitions and permission assignments.

### Scope
**In Scope:** Role entity, Permission entity, role-permission mapping, role assignment APIs, RBAC enforcement
**Out of Scope:** Dynamic permission creation (predefined permissions only for Phase 1.5)

### Requirements
**Functional:**
1. Role entity: Name, Description, Permissions (many-to-many)
2. Permission entity: Resource (ITEM, LOCATION, ORDER, etc.), Action (CREATE, READ, UPDATE, DELETE), Scope (ALL, OWN)
3. APIs: GET/POST/PUT/DELETE /api/warehouse/v1/admin/roles
4. Role assignment: POST /api/warehouse/v1/admin/users/{userId}/roles
5. Predefined roles: Admin (all permissions), Manager (most permissions), Operator (limited), QCInspector (QC only)
6. Validation: Role name unique, at least 1 permission per role

**Non-Functional:**
1. Permission check < 10ms (cached)
2. Role changes take effect immediately (cache invalidation)

**RBAC:** Admin role required for role management

**Data Model:**
```csharp
public class Role {
  public int Id { get; set; }
  public string Name { get; set; } // Manager
  public string Description { get; set; }
  public List<Permission> Permissions { get; set; }
  public bool IsSystemRole { get; set; } // true for predefined roles
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class Permission {
  public int Id { get; set; }
  public string Resource { get; set; } // ITEM
  public string Action { get; set; } // UPDATE
  public string Scope { get; set; } // ALL
}
```

**API:**
- GET /api/warehouse/v1/admin/roles
- POST /api/warehouse/v1/admin/roles
- PUT /api/warehouse/v1/admin/roles/{id}
- DELETE /api/warehouse/v1/admin/roles/{id}
- POST /api/warehouse/v1/admin/users/{userId}/roles

### Acceptance Criteria
```gherkin
Scenario: Create custom role
  Given logged in as Admin
  When POST /api/warehouse/v1/admin/roles with Name="Inventory Clerk", Permissions=[{Resource:"ITEM",Action:"READ"},{Resource:"LOCATION",Action:"READ"}]
  Then role created
  And response status 201

Scenario: Prevent deletion of system role
  Given role "Admin" with IsSystemRole=true
  When DELETE /api/warehouse/v1/admin/roles/1
  Then validation fails "Cannot delete system role"
  And response status 400

Scenario: Assign role to user
  Given user with Id=5
  And role "Inventory Clerk" with Id=10
  When POST /api/warehouse/v1/admin/users/5/roles with RoleId=10
  Then user assigned to role
  And permission cache invalidated for user
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/roles

curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/roles \
  -H "Content-Type: application/json" -d '{"name":"Inventory Clerk","description":"Read-only inventory access","permissions":[{"resource":"ITEM","action":"READ"},{"resource":"LOCATION","action":"READ"}]}'
```

### Definition of Done
- [ ] Role and Permission entities created
- [ ] EF migration generated
- [ ] CRUD endpoints in AdminController.cs
- [ ] Role assignment API
- [ ] Permission caching with invalidation
- [ ] Predefined roles seeded in migration
- [ ] Unit tests: permission checks, cache invalidation (15+ tests)
- [ ] Integration test: create role → assign → verify permissions
- [ ] RBAC enforcement
- [ ] Audit logging
- [ ] Documentation updated

---

## Task PRD-1625: Admin Configuration UI

**Epic:** Admin Config | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** UI | **Dependencies:** PRD-1621-1624 | **SourceRefs:** Universe §4.Epic P

### Context
Blazor UI for warehouse settings, reason codes, approval rules, and role management. Enables non-technical users to configure system.

### Scope
**In Scope:** Settings page, reason code CRUD, approval rules UI, role management UI
**Out of Scope:** Advanced rule builder (simple form-based only)

### Requirements
**Functional:**
1. Settings page: Form with all WarehouseSettings fields, Save button
2. Reason Codes page: Table with Add/Edit/Delete, hierarchical display (parent → children indented)
3. Approval Rules page: Table with Add/Edit/Delete, rule evaluation test form
4. Roles page: Table with Add/Edit/Delete, permission checkboxes, user assignment

**Non-Functional:**
1. Responsive design (tablet/desktop)
2. Client-side + server-side validation
3. Page load < 2 seconds

**RBAC:** Admin role required for all pages

**UI (Blazor):**
- Routes: /warehouse/admin/settings, /warehouse/admin/reason-codes, /warehouse/admin/approval-rules, /warehouse/admin/roles
- Components: Forms, tables, modals for add/edit
- Validations: Required fields, range checks
- Empty states: "No reason codes configured. Add your first reason code."

### Acceptance Criteria
```gherkin
Scenario: Update warehouse settings
  Given logged in as Admin
  When navigate to /warehouse/admin/settings
  And change CapacityThresholdPercent to 85
  And click Save
  Then API PUT /api/warehouse/v1/admin/settings called
  And toast "Settings saved successfully"

Scenario: Add reason code
  Given logged in as Admin
  When navigate to /warehouse/admin/reason-codes
  And click "Add Reason Code"
  And fill form: Code="DAMAGE-WATER", Name="Water Damage", Category="ADJUSTMENT"
  And click Save
  Then API POST /api/warehouse/v1/admin/reason-codes called
  And new reason code appears in table

Scenario: Test approval rule
  Given logged in as Admin
  And approval rule: COST_ADJUSTMENT > $10000 requires Manager
  When navigate to /warehouse/admin/approval-rules
  And enter test value $15000
  And click "Test Rule"
  Then result shows "Requires approval: Manager"
```

### Validation
```bash
dotnet run --project src/LKvitai.MES.WebUI
# Navigate to http://localhost:5001/warehouse/admin/settings
# Test all forms, validations, CRUD operations
```

### Definition of Done
- [ ] Settings.razor page created in src/LKvitai.MES.WebUI/Pages/Admin/
- [ ] ReasonCodes.razor page with hierarchical table
- [ ] ApprovalRules.razor page with test form
- [ ] Roles.razor page with permission checkboxes
- [ ] Client-side validation
- [ ] Responsive design tested
- [ ] RBAC enforcement (Admin only)
- [ ] Manual test completed (all scenarios)
- [ ] Documentation updated
## Task PRD-1631: Transaction Log Export

**Epic:** Compliance | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1550 | **SourceRefs:** Universe §5.Compliance

### Context
Full event stream export for compliance audits. Enables external auditors to verify all warehouse transactions.

### Scope
**In Scope:** Full event stream export, CSV/JSON format, date range filter, SFTP upload
**Out of Scope:** Real-time streaming (batch export only for Phase 1.5)

### Requirements
**Functional:**
1. Export API: POST /api/warehouse/v1/admin/compliance/export-transactions (input: startDate, endDate, format)
2. Query Marten event store: All events in date range (StockMoved, ReservationCreated, etc.)
3. Format options: CSV (flat structure) or JSON (nested structure)
4. CSV columns: EventId, EventType, Timestamp, AggregateId, UserId, Payload (JSON string)
5. SFTP upload: Optional SFTP destination (host, username, path)
6. Export history: Track exports (ExportId, ExportedAt, RowCount, FilePath, Status)

**Non-Functional:**
1. Export 100k events < 60 seconds
2. File size limit: 500MB per export (split if larger)
3. Audit logging: all exports logged

**RBAC:** Admin or Auditor role required

**Data Model:**
```csharp
public class TransactionExport {
  public Guid Id { get; set; }
  public DateTime StartDate { get; set; }
  public DateTime EndDate { get; set; }
  public ExportFormat Format { get; set; } // CSV, JSON
  public int RowCount { get; set; }
  public string FilePath { get; set; }
  public ExportStatus Status { get; set; } // PENDING, COMPLETED, FAILED
  public string ErrorMessage { get; set; }
  public string ExportedBy { get; set; }
  public DateTime ExportedAt { get; set; }
}
```

**API:**
- POST /api/warehouse/v1/admin/compliance/export-transactions
- GET /api/warehouse/v1/admin/compliance/exports (history)

### Acceptance Criteria
```gherkin
Scenario: Export transactions to CSV
  Given logged in as Admin
  And 1000 events in date range 2026-02-01 to 2026-02-12
  When POST /api/warehouse/v1/admin/compliance/export-transactions with StartDate="2026-02-01", EndDate="2026-02-12", Format="CSV"
  Then CSV file generated with 1000 rows
  And saved to exports/compliance/transactions-20260212-123456.csv
  And TransactionExport record created with Status=COMPLETED, RowCount=1000

Scenario: Export with SFTP upload
  Given SFTP configured (host, username, path)
  When POST /api/warehouse/v1/admin/compliance/export-transactions with SftpUpload=true
  Then CSV file uploaded to SFTP server
  And local file deleted after successful upload

Scenario: Export large dataset with split
  Given 600k events (exceeds 500MB limit)
  When export triggered
  Then 2 files generated: transactions-part1.csv, transactions-part2.csv
  And TransactionExport record includes both file paths
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Export transactions
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/compliance/export-transactions \
  -H "Content-Type: application/json" -d '{"startDate":"2026-02-01","endDate":"2026-02-12","format":"CSV"}'

# Check export history
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/compliance/exports
```

### Definition of Done
- [ ] TransactionExport entity created
- [ ] EF migration generated
- [ ] Export API endpoint
- [ ] Marten event query (date range filter)
- [ ] CSV generation (CsvHelper library)
- [ ] JSON generation
- [ ] SFTP upload integration (SSH.NET library)
- [ ] File split logic for large exports
- [ ] Export history tracking
- [ ] Unit tests: CSV generation, file split (10+ tests)
- [ ] Integration test: export → verify file content
- [ ] Audit logging
- [ ] Documentation: export format specification

---

## Task PRD-1632: Lot Traceability Report

**Epic:** Compliance | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** UI/Backend | **Dependencies:** PRD-1551 | **SourceRefs:** Universe §5.Compliance

### Context
Forward/backward lot traceability for recalls and compliance. Enables tracking lot from supplier to customer.

### Scope
**In Scope:** Forward/backward trace (lot → orders → customers), genealogy tree, CSV export
**Out of Scope:** Real-time traceability alerts (report-based only for Phase 1.5)

### Requirements
**Functional:**
1. Traceability API: POST /api/warehouse/v1/admin/compliance/lot-trace (input: lotNumber, direction)
2. Backward trace: Lot → InboundShipment → Supplier
3. Forward trace: Lot → Reservations → OutboundOrders → Customers
4. Genealogy tree: Nested structure showing all related entities
5. Report format: JSON (tree structure) or CSV (flat with parent-child relationships)
6. UI: Traceability page with lot search, tree visualization, CSV export

**Non-Functional:**
1. Trace query < 2 seconds (for 1000 related records)
2. Tree depth: Unlimited (recursive query)

**RBAC:** Manager or Auditor role required

**Data Model:**
```csharp
public class LotTraceNode {
  public string NodeType { get; set; } // LOT, SHIPMENT, ORDER, CUSTOMER
  public string NodeId { get; set; }
  public string NodeName { get; set; }
  public DateTime Timestamp { get; set; }
  public List<LotTraceNode> Children { get; set; }
}
```

**API:**
- POST /api/warehouse/v1/admin/compliance/lot-trace
- GET /api/warehouse/v1/admin/compliance/lot-trace/{traceId}

**UI (Blazor):**
- Route: /warehouse/compliance/lot-trace
- Components: Lot search input, tree visualization (recursive component), CSV export button

### Acceptance Criteria
```gherkin
Scenario: Backward trace from lot to supplier
  Given lot "LOT-2026-001" received from supplier "ACME Corp" on 2026-02-01
  When POST /api/warehouse/v1/admin/compliance/lot-trace with LotNumber="LOT-2026-001", Direction="BACKWARD"
  Then response tree: LOT-2026-001 → InboundShipment ISH-0001 → Supplier ACME Corp

Scenario: Forward trace from lot to customers
  Given lot "LOT-2026-001" used in 3 sales orders (SO-001, SO-002, SO-003)
  And orders shipped to customers (Customer A, Customer B, Customer C)
  When POST /api/warehouse/v1/admin/compliance/lot-trace with LotNumber="LOT-2026-001", Direction="FORWARD"
  Then response tree: LOT-2026-001 → [SO-001 → Customer A, SO-002 → Customer B, SO-003 → Customer C]

Scenario: Export traceability report to CSV
  Given lot trace completed
  When click "Export CSV"
  Then CSV file downloaded with columns: Level, NodeType, NodeId, NodeName, Timestamp, ParentNodeId
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Backward trace
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/compliance/lot-trace \
  -H "Content-Type: application/json" -d '{"lotNumber":"LOT-2026-001","direction":"BACKWARD"}'

# Forward trace
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/compliance/lot-trace \
  -H "Content-Type: application/json" -d '{"lotNumber":"LOT-2026-001","direction":"FORWARD"}'
```

### Definition of Done
- [ ] Lot trace API endpoint
- [ ] Backward trace query (lot → shipment → supplier)
- [ ] Forward trace query (lot → reservations → orders → customers)
- [ ] Recursive tree builder
- [ ] CSV export with parent-child relationships
- [ ] LotTrace.razor page with tree visualization
- [ ] Unit tests: trace queries, tree building (15+ tests)
- [ ] Integration test: full trace flow
- [ ] RBAC enforcement
- [ ] Documentation: traceability model

---

## Task PRD-1633: Variance Analysis Report

**Epic:** Compliance | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** UI/Backend | **Dependencies:** PRD-1614 | **SourceRefs:** Universe §5.Compliance

### Context
Cycle count variance trends for inventory accuracy monitoring. Identifies problem locations and operators.

### Scope
**In Scope:** Cycle count variance trends, ABC analysis, location accuracy metrics, charts
**Out of Scope:** Predictive analytics (descriptive only for Phase 1.5)

### Requirements
**Functional:**
1. Variance report API: GET /api/warehouse/v1/admin/compliance/variance-analysis (filters: dateRange, location, operator)
2. Metrics: Total variance count, Total variance value, Accuracy percentage, Top 10 problem locations, Top 10 problem operators
3. ABC analysis: Variance by item category (A/B/C classification)
4. Trend chart: Variance count over time (daily/weekly/monthly)
5. CSV export

**Non-Functional:**
1. Report generation < 3 seconds (for 1 year of data)
2. Charts: Interactive (Blazor chart library)

**RBAC:** Manager or Auditor role required

**Data Model:**
```csharp
public class VarianceAnalysis {
  public int TotalVarianceCount { get; set; }
  public decimal TotalVarianceValue { get; set; }
  public decimal AccuracyPercentage { get; set; }
  public List<LocationVariance> TopProblemLocations { get; set; }
  public List<OperatorVariance> TopProblemOperators { get; set; }
  public List<AbcVariance> AbcAnalysis { get; set; }
  public List<TrendPoint> TrendData { get; set; }
}
```

**API:**
- GET /api/warehouse/v1/admin/compliance/variance-analysis

**UI (Blazor):**
- Route: /warehouse/compliance/variance-analysis
- Components: Date range picker, filters, summary cards, charts, tables, CSV export button

### Acceptance Criteria
```gherkin
Scenario: Generate variance analysis report
  Given logged in as Manager
  And 50 cycle counts in date range 2026-01-01 to 2026-02-12
  And 10 variances detected (total value $5000)
  When GET /api/warehouse/v1/admin/compliance/variance-analysis?startDate=2026-01-01&endDate=2026-02-12
  Then response: TotalVarianceCount=10, TotalVarianceValue=5000, AccuracyPercentage=80%
  And TopProblemLocations includes location with most variances

Scenario: ABC analysis breakdown
  Given variances: 5 in category A (high value), 3 in category B, 2 in category C
  When variance analysis generated
  Then ABC analysis shows: A=5 variances ($4000), B=3 variances ($800), C=2 variances ($200)

Scenario: Trend chart over time
  Given variances: 2 in Jan, 5 in Feb
  When variance analysis generated
  Then trend chart shows: Jan=2, Feb=5
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

curl -H "Authorization: Bearer $TOKEN" "http://localhost:5000/api/warehouse/v1/admin/compliance/variance-analysis?startDate=2026-01-01&endDate=2026-02-12"
```

### Definition of Done
- [ ] Variance analysis API endpoint
- [ ] Query logic: aggregate cycle count variances
- [ ] ABC analysis calculation
- [ ] Trend data aggregation (daily/weekly/monthly)
- [ ] VarianceAnalysis.razor page with charts
- [ ] CSV export
- [ ] Unit tests: aggregation logic, ABC calculation (10+ tests)
- [ ] Integration test: generate report → verify metrics
- [ ] RBAC enforcement
- [ ] Documentation: variance metrics definition

---

## Task PRD-1634: Compliance Reports Dashboard

**Epic:** Compliance | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** UI | **Dependencies:** PRD-1631-1633 | **SourceRefs:** Universe §5.Compliance

### Context
Unified compliance dashboard with report scheduler, email delivery, and PDF generation.

### Scope
**In Scope:** Compliance dashboard, report scheduler, email delivery, PDF generation
**Out of Scope:** Custom report builder (predefined reports only for Phase 1.5)

### Requirements
**Functional:**
1. Dashboard: Summary cards (pending exports, recent traces, variance alerts), quick links to reports
2. Report scheduler: Configure recurring reports (daily/weekly/monthly), email recipients, format (CSV/PDF)
3. Email delivery: Send reports via SMTP, include summary in email body, attach file
4. PDF generation: Convert reports to PDF (using library like QuestPDF)
5. Report history: List of all generated reports with download links

**Non-Functional:**
1. Dashboard load < 2 seconds
2. PDF generation < 5 seconds (for 100-page report)
3. Email delivery < 10 seconds

**RBAC:** Manager or Auditor role required

**Data Model:**
```csharp
public class ScheduledReport {
  public int Id { get; set; }
  public ReportType ReportType { get; set; } // TRANSACTION_EXPORT, LOT_TRACE, VARIANCE_ANALYSIS
  public string Schedule { get; set; } // Cron expression
  public List<string> EmailRecipients { get; set; }
  public ReportFormat Format { get; set; } // CSV, PDF
  public bool Active { get; set; }
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
}
```

**API:**
- GET /api/warehouse/v1/admin/compliance/dashboard
- GET/POST/PUT/DELETE /api/warehouse/v1/admin/compliance/scheduled-reports

**UI (Blazor):**
- Route: /warehouse/compliance/dashboard
- Components: Summary cards, report scheduler form, report history table

### Acceptance Criteria
```gherkin
Scenario: View compliance dashboard
  Given logged in as Manager
  When navigate to /warehouse/compliance/dashboard
  Then summary cards displayed: Pending exports=2, Recent traces=5, Variance alerts=3
  And quick links to all compliance reports

Scenario: Schedule recurring report
  Given logged in as Manager
  When click "Schedule Report"
  And select ReportType="VARIANCE_ANALYSIS", Schedule="0 8 * * 1" (weekly Monday 8am), EmailRecipients=["manager@example.com"], Format="PDF"
  And click Save
  Then scheduled report created
  And Hangfire job registered

Scenario: Email delivery of scheduled report
  Given scheduled report: VARIANCE_ANALYSIS, weekly Monday 8am
  When Hangfire triggers job on Monday 8am
  Then variance analysis report generated
  And PDF created
  And email sent to manager@example.com with PDF attachment
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# View dashboard
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/compliance/dashboard

# Schedule report
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/compliance/scheduled-reports \
  -H "Content-Type: application/json" -d '{"reportType":"VARIANCE_ANALYSIS","schedule":"0 8 * * 1","emailRecipients":["manager@example.com"],"format":"PDF","active":true}'
```

### Definition of Done
- [ ] ComplianceDashboard.razor page
- [ ] ScheduledReport entity and CRUD APIs
- [ ] Hangfire job for scheduled reports
- [ ] PDF generation (QuestPDF library)
- [ ] Email delivery (SMTP)
- [ ] Report history tracking
- [ ] Unit tests: scheduler logic, email delivery (10+ tests)
- [ ] Integration test: schedule → trigger → verify email
- [ ] RBAC enforcement
- [ ] Documentation: report scheduler guide

---

## Task PRD-1635: FDA 21 CFR Part 11 Compliance

**Epic:** Compliance | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** L (2 days)
**OwnerType:** Backend/API | **Dependencies:** PRD-1630 | **SourceRefs:** Universe §5.Compliance

### Context
FDA 21 CFR Part 11 compliance for electronic records and signatures. Required for pharmaceutical/medical device warehouses.

### Scope
**In Scope:** Electronic signatures, audit trail immutability, record retention, validation documentation
**Out of Scope:** Full FDA validation (IQ/OQ/PQ) - technical implementation only for Phase 1.5

### Requirements
**Functional:**
1. Electronic signatures: Capture user signature (typed name + password re-entry) for critical operations (QC approval, cost adjustments > $10k)
2. Signature entity: UserId, Action, ResourceId, SignatureText, Timestamp, IpAddress, Meaning (approval/rejection)
3. Audit trail immutability: Event store append-only, no updates/deletes, cryptographic hash chain
4. Record retention: 7 years minimum (configurable)
5. Validation documentation: Generate validation report (system configuration, test results, audit trail sample)

**Non-Functional:**
1. Signature capture < 500ms
2. Hash chain verification < 1 second (for 100k events)
3. Validation report generation < 30 seconds

**RBAC:** All users can sign, Admin can generate validation reports

**Data Model:**
```csharp
public class ElectronicSignature {
  public long Id { get; set; }
  public int UserId { get; set; }
  public string Action { get; set; } // QC_APPROVAL, COST_ADJUSTMENT
  public string ResourceId { get; set; }
  public string SignatureText { get; set; } // "John Doe"
  public string Meaning { get; set; } // APPROVED, REJECTED
  public DateTime Timestamp { get; set; }
  public string IpAddress { get; set; }
  public string PreviousHash { get; set; } // Hash of previous signature
  public string CurrentHash { get; set; } // Hash of this signature
}
```

**API:**
- POST /api/warehouse/v1/admin/compliance/sign (capture signature)
- GET /api/warehouse/v1/admin/compliance/signatures/{resourceId}
- POST /api/warehouse/v1/admin/compliance/verify-hash-chain
- GET /api/warehouse/v1/admin/compliance/validation-report

### Acceptance Criteria
```gherkin
Scenario: Capture electronic signature for QC approval
  Given logged in as QCInspector
  And QC inspection pending for HU-001
  When POST /api/warehouse/v1/qc/approve with HU="HU-001", SignatureText="Jane Smith", Password="***"
  Then password verified
  And electronic signature created: Action="QC_APPROVAL", ResourceId="HU-001", Meaning="APPROVED"
  And signature hash chain updated

Scenario: Verify audit trail immutability
  Given 1000 electronic signatures
  When POST /api/warehouse/v1/admin/compliance/verify-hash-chain
  Then hash chain verified (each signature's PreviousHash matches previous signature's CurrentHash)
  And response: {"valid":true,"signatureCount":1000}

Scenario: Generate validation report
  Given logged in as Admin
  When GET /api/warehouse/v1/admin/compliance/validation-report
  Then PDF report generated with sections: System Configuration, Audit Trail Sample (100 records), Hash Chain Verification, User Access Log
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Capture signature (requires password re-entry)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/compliance/sign \
  -H "Content-Type: application/json" -d '{"action":"QC_APPROVAL","resourceId":"HU-001","signatureText":"Jane Smith","password":"***","meaning":"APPROVED"}'

# Verify hash chain
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/compliance/verify-hash-chain

# Generate validation report
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/compliance/validation-report -o validation-report.pdf
```

### Definition of Done
- [ ] ElectronicSignature entity created
- [ ] EF migration generated
- [ ] Signature capture API with password re-entry
- [ ] Hash chain implementation (SHA256)
- [ ] Hash chain verification API
- [ ] Validation report generation (PDF)
- [ ] Integration with QC approval and cost adjustment workflows
- [ ] Unit tests: signature capture, hash chain (15+ tests)
- [ ] Integration test: sign → verify hash chain
- [ ] Documentation: FDA 21 CFR Part 11 compliance guide
## Task PRD-1626: SSO/OAuth Integration

**Epic:** Security | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** L (2 days)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Security

### Context
Enterprise-grade authentication via OAuth 2.0 (Azure AD, Okta). Replaces basic username/password with SSO for compliance.

### Scope
**In Scope:** OAuth 2.0 provider integration (Azure AD/Okta), token validation, user provisioning, role mapping
**Out of Scope:** SAML integration (OAuth only for Phase 1.5)

### Requirements
**Functional:**
1. OAuth 2.0 integration: Authorization Code flow with PKCE
2. Supported providers: Azure AD, Okta (configurable via appsettings)
3. Token validation: JWT signature verification, expiry check, issuer validation
4. User provisioning: Auto-create user on first login, map OAuth claims to roles
5. Role mapping: Configure claim → role mapping (e.g., Azure AD group → Warehouse role)
6. Fallback: Dev auth still available for local development

**Non-Functional:**
1. Token validation < 50ms (cached public keys)
2. Session timeout: 8 hours (configurable)
3. Audit logging: all login attempts

**RBAC:** No role required (authentication mechanism)

**Data Model:**
```csharp
public class OAuthConfig {
  public string Provider { get; set; } // AzureAD, Okta
  public string Authority { get; set; } // https://login.microsoftonline.com/{tenant}
  public string ClientId { get; set; }
  public string ClientSecret { get; set; }
  public Dictionary<string, string> RoleMappings { get; set; } // claim value → role name
}
```

**API:**
- GET /api/auth/oauth/login (redirect to provider)
- GET /api/auth/oauth/callback (handle OAuth callback)
- POST /api/auth/oauth/logout

### Acceptance Criteria
```gherkin
Scenario: Login via Azure AD
  Given OAuth configured for Azure AD
  When user navigates to /api/auth/oauth/login
  Then redirected to Azure AD login page
  And after successful login redirected to /api/auth/oauth/callback
  And JWT token issued with user claims
  And user auto-created if not exists

Scenario: Role mapping from OAuth claims
  Given Azure AD user with group claim "Warehouse-Managers"
  And role mapping: "Warehouse-Managers" → "Manager"
  When user logs in via OAuth
  Then user assigned Manager role
  And can access Manager-protected endpoints

Scenario: Token validation failure
  Given expired JWT token
  When API request with expired token
  Then response status 401 Unauthorized
  And error message "Token expired"
```

### Validation
```bash
# Manual test: Configure Azure AD in appsettings.json
# Navigate to http://localhost:5000/api/auth/oauth/login
# Complete Azure AD login flow
# Verify JWT token issued and user created

# Test token validation
curl -H "Authorization: Bearer <expired-token>" http://localhost:5000/api/warehouse/v1/items
# Expected: 401 Unauthorized
```

### Definition of Done
- [ ] OAuth 2.0 middleware configured in Program.cs
- [ ] Azure AD and Okta provider support
- [ ] Token validation with public key caching
- [ ] User auto-provisioning on first login
- [ ] Role mapping from OAuth claims
- [ ] Login/callback/logout endpoints
- [ ] Unit tests: token validation, role mapping (15+ tests)
- [ ] Integration test: full OAuth flow (mock provider)
- [ ] Audit logging for login attempts
- [ ] Documentation: OAuth setup guide
- [ ] Dev auth fallback preserved

---

## Task PRD-1627: MFA Implementation

**Epic:** Security | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1626 | **SourceRefs:** Universe §5.Security

### Context
Multi-factor authentication (TOTP-based) for enhanced security. Required for Admin and Manager roles.

### Scope
**In Scope:** TOTP-based MFA, QR code generation, backup codes, MFA enforcement policy
**Out of Scope:** SMS-based MFA (TOTP only for Phase 1.5)

### Requirements
**Functional:**
1. MFA enrollment: Generate TOTP secret, display QR code, verify initial code
2. MFA verification: Validate TOTP code on login (after OAuth)
3. Backup codes: Generate 10 single-use backup codes on enrollment
4. MFA enforcement: Configurable per role (Admin/Manager required, others optional)
5. MFA reset: Admin can reset user's MFA (requires approval)

**Non-Functional:**
1. TOTP validation < 100ms
2. QR code generation < 500ms
3. Backup codes encrypted at rest

**RBAC:** All users can enroll MFA, Admin can reset MFA for others

**Data Model:**
```csharp
public class UserMFA {
  public int UserId { get; set; }
  public string TotpSecret { get; set; } // Encrypted
  public bool MfaEnabled { get; set; }
  public DateTime? MfaEnrolledAt { get; set; }
  public List<string> BackupCodes { get; set; } // Encrypted, hashed
  public int FailedAttempts { get; set; }
  public DateTime? LockedUntil { get; set; }
}
```

**API:**
- POST /api/auth/mfa/enroll (generate secret, return QR code)
- POST /api/auth/mfa/verify-enrollment (verify initial code)
- POST /api/auth/mfa/verify (verify code on login)
- POST /api/auth/mfa/reset/{userId} (Admin only)
- GET /api/auth/mfa/backup-codes

### Acceptance Criteria
```gherkin
Scenario: Enroll MFA
  Given logged in user without MFA
  When POST /api/auth/mfa/enroll
  Then TOTP secret generated
  And QR code returned (data URI)
  And 10 backup codes generated
  And MfaEnabled=false (pending verification)

Scenario: Verify MFA enrollment
  Given user enrolled MFA with secret
  When POST /api/auth/mfa/verify-enrollment with valid TOTP code
  Then MfaEnabled=true
  And response status 200

Scenario: MFA required on login
  Given user with MfaEnabled=true and role=Admin
  When login via OAuth
  Then redirected to MFA verification page
  And must enter TOTP code before access granted

Scenario: Use backup code
  Given user with MfaEnabled=true
  And backup code "ABC123DEF456"
  When POST /api/auth/mfa/verify with backupCode="ABC123DEF456"
  Then backup code marked as used
  And access granted
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Enroll MFA
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/auth/mfa/enroll

# Verify enrollment (use TOTP app to generate code)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/auth/mfa/verify-enrollment \
  -H "Content-Type: application/json" -d '{"code":"123456"}'

# Get backup codes
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/auth/mfa/backup-codes
```

### Definition of Done
- [ ] UserMFA entity created
- [ ] EF migration generated
- [ ] TOTP library integrated (OtpNet or similar)
- [ ] QR code generation (QRCoder library)
- [ ] Enroll/verify/reset endpoints
- [ ] Backup codes generation and validation
- [ ] MFA enforcement middleware (check role policy)
- [ ] Unit tests: TOTP validation, backup codes (15+ tests)
- [ ] Integration test: enroll → verify → login with MFA
- [ ] Audit logging for MFA events
- [ ] Documentation: MFA setup guide

---

## Task PRD-1628: API Key Management

**Epic:** Security | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Security

### Context
API keys for system-to-system integration (ERP, MES, external services). Enables secure API access without user credentials.

### Scope
**In Scope:** APIKey entity, key generation/rotation, scope-based permissions, rate limiting per key
**Out of Scope:** OAuth client credentials flow (simple API keys only for Phase 1.5)

### Requirements
**Functional:**
1. APIKey entity: Name, Key (hashed), Scopes (permissions), ExpiresAt, Active, RateLimitPerMinute
2. Key generation: Generate secure random key (32 bytes, base64), hash before storage
3. Key rotation: Generate new key, expire old key after grace period (7 days)
4. Scopes: Predefined scopes (read:items, write:orders, read:stock, etc.)
5. Rate limiting: Per-key rate limit (default 100 req/min)
6. Validation: Check key hash, expiry, active status, scopes

**Non-Functional:**
1. Key validation < 50ms (cached)
2. Rate limiting enforced via middleware
3. Audit logging: all API key usage

**RBAC:** Admin role required for API key management

**Data Model:**
```csharp
public class APIKey {
  public int Id { get; set; }
  public string Name { get; set; } // ERP Integration
  public string KeyHash { get; set; } // SHA256 hash
  public List<string> Scopes { get; set; } // ["read:items","write:orders"]
  public DateTime? ExpiresAt { get; set; }
  public bool Active { get; set; }
  public int RateLimitPerMinute { get; set; } // 100
  public DateTime LastUsedAt { get; set; }
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
}
```

**API:**
- GET /api/warehouse/v1/admin/api-keys
- POST /api/warehouse/v1/admin/api-keys (returns plain key once)
- PUT /api/warehouse/v1/admin/api-keys/{id}/rotate
- DELETE /api/warehouse/v1/admin/api-keys/{id}

### Acceptance Criteria
```gherkin
Scenario: Generate API key
  Given logged in as Admin
  When POST /api/warehouse/v1/admin/api-keys with Name="ERP Integration", Scopes=["read:items","write:orders"], RateLimitPerMinute=100
  Then API key generated
  And plain key returned once (never shown again)
  And key hash stored in database
  And response status 201

Scenario: Use API key for authentication
  Given API key "wh_abc123..." with scope "read:items"
  When GET /api/warehouse/v1/items with header "X-API-Key: wh_abc123..."
  Then request authenticated
  And response status 200

Scenario: Reject API key without required scope
  Given API key with scope "read:items" only
  When POST /api/warehouse/v1/orders with header "X-API-Key: wh_abc123..."
  Then response status 403 Forbidden
  And error "Insufficient scope: write:orders required"

Scenario: Rate limiting enforcement
  Given API key with RateLimitPerMinute=100
  When 101 requests in 1 minute
  Then 101st request returns 429 Too Many Requests
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Generate API key
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/api-keys \
  -H "Content-Type: application/json" -d '{"name":"ERP Integration","scopes":["read:items","write:orders"],"rateLimitPerMinute":100}'

# Use API key (save key from previous response)
curl -H "X-API-Key: wh_abc123..." http://localhost:5000/api/warehouse/v1/items
```

### Definition of Done
- [ ] APIKey entity created
- [ ] EF migration generated
- [ ] Key generation with secure random + hashing
- [ ] CRUD endpoints in AdminController.cs
- [ ] API key authentication middleware
- [ ] Scope-based authorization
- [ ] Rate limiting middleware (per key)
- [ ] Unit tests: key validation, scope checks, rate limiting (15+ tests)
- [ ] Integration test: generate key → use key → verify rate limit
- [ ] Audit logging for API key usage
- [ ] Documentation: API key usage guide

---

## Task PRD-1629: RBAC Granular Permissions

**Epic:** Security | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1624 | **SourceRefs:** Universe §5.Security

### Context
Fine-grained permissions beyond role-based access. Enables resource:action level authorization (e.g., can update own orders but not others).

### Scope
**In Scope:** Permission entity (resource:action), role-permission mapping, policy-based authorization
**Out of Scope:** Attribute-based access control (ABAC) with complex conditions (simple resource:action only)

### Requirements
**Functional:**
1. Permission entity: Resource (ITEM, ORDER, LOCATION, etc.), Action (CREATE, READ, UPDATE, DELETE), Scope (ALL, OWN, NONE)
2. Role-permission mapping: Many-to-many relationship
3. Policy-based authorization: Check user's roles → aggregate permissions → evaluate policy
4. Predefined permissions: Seed common permissions in migration
5. Permission check API: POST /api/warehouse/v1/admin/permissions/check (input: resource, action, userId → output: allowed)

**Non-Functional:**
1. Permission check < 10ms (cached)
2. Cache invalidation on role/permission changes

**RBAC:** Admin role required for permission management

**Data Model:**
```csharp
public class Permission {
  public int Id { get; set; }
  public string Resource { get; set; } // ITEM
  public string Action { get; set; } // UPDATE
  public string Scope { get; set; } // ALL
  public string Description { get; set; }
}

public class RolePermission {
  public int RoleId { get; set; }
  public int PermissionId { get; set; }
}
```

**API:**
- GET /api/warehouse/v1/admin/permissions
- POST /api/warehouse/v1/admin/permissions/check

### Acceptance Criteria
```gherkin
Scenario: Check permission for user
  Given user with role "Operator"
  And Operator role has permission: Resource="ITEM", Action="READ", Scope="ALL"
  When POST /api/warehouse/v1/admin/permissions/check with UserId=5, Resource="ITEM", Action="READ"
  Then response: {"allowed":true}

Scenario: Deny permission for insufficient scope
  Given user with role "Operator"
  And Operator role has permission: Resource="ORDER", Action="UPDATE", Scope="OWN"
  When POST /api/warehouse/v1/admin/permissions/check with UserId=5, Resource="ORDER", Action="UPDATE", OwnerId=10 (different user)
  Then response: {"allowed":false}

Scenario: Aggregate permissions from multiple roles
  Given user with roles "Operator" and "QCInspector"
  And Operator has permission: Resource="ITEM", Action="READ"
  And QCInspector has permission: Resource="QC", Action="UPDATE"
  When check permissions for user
  Then user has both permissions
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Check permission
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/permissions/check \
  -H "Content-Type: application/json" -d '{"userId":5,"resource":"ITEM","action":"READ"}'
```

### Definition of Done
- [ ] Permission entity created (if not exists from PRD-1624)
- [ ] RolePermission mapping table
- [ ] Permission check service with caching
- [ ] Policy-based authorization middleware
- [ ] Predefined permissions seeded
- [ ] Check API endpoint
- [ ] Unit tests: permission aggregation, scope checks (15+ tests)
- [ ] Integration test: assign permissions → check → verify
- [ ] Cache invalidation on changes
- [ ] Documentation: permission model guide

---

## Task PRD-1630: Security Audit Log

**Epic:** Security | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1629 | **SourceRefs:** Universe §5.Security

### Context
Comprehensive audit log for all security-relevant actions. Required for compliance (SOC 2, ISO 27001).

### Scope
**In Scope:** AuditLog entity (user, action, resource, timestamp, IP), log retention, query APIs
**Out of Scope:** Real-time alerting (log only for Phase 1.5)

### Requirements
**Functional:**
1. AuditLog entity: UserId, Action (LOGIN, LOGOUT, CREATE, UPDATE, DELETE, etc.), Resource (ITEM, ORDER, etc.), ResourceId, IpAddress, UserAgent, Timestamp, Details (JSON)
2. Automatic logging: Middleware captures all API requests (POST/PUT/DELETE)
3. Manual logging: Explicit audit log calls for sensitive operations (MFA reset, role changes)
4. Query API: GET /api/warehouse/v1/admin/audit-logs (filters: userId, action, resource, dateRange)
5. Retention: Keep logs for 7 years (compliance requirement)

**Non-Functional:**
1. Log write < 50ms (async, non-blocking)
2. Query performance < 1 second (indexed by userId, timestamp)
3. Storage: Append-only table, no updates/deletes

**RBAC:** Admin role required for audit log queries

**Data Model:**
```csharp
public class AuditLog {
  public long Id { get; set; }
  public int? UserId { get; set; } // Nullable for anonymous actions
  public string Action { get; set; } // LOGIN, CREATE_ITEM
  public string Resource { get; set; } // ITEM
  public string ResourceId { get; set; } // "1"
  public string IpAddress { get; set; }
  public string UserAgent { get; set; }
  public DateTime Timestamp { get; set; }
  public string Details { get; set; } // JSON payload
}
```

**API:**
- GET /api/warehouse/v1/admin/audit-logs (with filters)

### Acceptance Criteria
```gherkin
Scenario: Automatic audit log on API request
  Given logged in as Manager
  When POST /api/warehouse/v1/items with item data
  Then audit log created: Action="CREATE_ITEM", Resource="ITEM", ResourceId="1", UserId=5, IpAddress="192.168.1.100"

Scenario: Query audit logs by user
  Given 100 audit logs (50 for UserId=5, 50 for UserId=10)
  When GET /api/warehouse/v1/admin/audit-logs?userId=5
  Then response contains 50 logs for UserId=5 only

Scenario: Query audit logs by date range
  Given audit logs from 2026-01-01 to 2026-02-12
  When GET /api/warehouse/v1/admin/audit-logs?startDate=2026-02-01&endDate=2026-02-12
  Then response contains logs from Feb 2026 only
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Perform action to generate audit log
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/items \
  -H "Content-Type: application/json" -d '{"sku":"TEST-001","name":"Test Item","categoryId":1}'

# Query audit logs
curl -H "Authorization: Bearer $TOKEN" "http://localhost:5000/api/warehouse/v1/admin/audit-logs?action=CREATE_ITEM"
```

### Definition of Done
- [ ] AuditLog entity created
- [ ] EF migration generated with indexes
- [ ] Audit logging middleware (captures all POST/PUT/DELETE)
- [ ] Manual audit log service for explicit calls
- [ ] Query API with filters
- [ ] Async log writing (non-blocking)
- [ ] Unit tests: log creation, query filters (10+ tests)
- [ ] Integration test: perform action → verify log created
- [ ] Retention policy documented (7 years)
- [ ] Documentation: audit log schema
## Task PRD-1636: Retention Policy Engine

**Epic:** Data Retention | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.DataRetention

### Context
Configurable data retention policies for compliance (GDPR, SOX, industry regulations). Automates archival and deletion.

### Scope
**In Scope:** RetentionPolicy entity (data type, retention period), policy evaluation, archival job
**Out of Scope:** Automated legal hold (manual override only for Phase 1.5)

### Requirements
**Functional:**
1. RetentionPolicy entity: DataType (EVENTS, PROJECTIONS, AUDIT_LOGS, CUSTOMER_DATA), RetentionPeriodDays, ArchiveAfterDays, DeleteAfterDays, Active
2. Policy evaluation: Hangfire job runs daily, checks data age against policies
3. Archival: Move old data to archive tables (events_archive, audit_logs_archive)
4. Deletion: Hard delete data exceeding retention period (with confirmation)
5. Legal hold: Flag records to prevent deletion (manual override)
6. API: GET/POST/PUT/DELETE /api/warehouse/v1/admin/retention-policies

**Non-Functional:**
1. Policy evaluation < 5 minutes (for 1M records)
2. Archival: Non-blocking (background job)
3. Audit logging: all retention actions logged

**RBAC:** Admin role required

**Data Model:**
```csharp
public class RetentionPolicy {
  public int Id { get; set; }
  public DataType DataType { get; set; } // EVENTS, AUDIT_LOGS
  public int RetentionPeriodDays { get; set; } // 2555 (7 years)
  public int? ArchiveAfterDays { get; set; } // 365 (1 year)
  public bool Active { get; set; }
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class RetentionExecution {
  public Guid Id { get; set; }
  public DateTime ExecutedAt { get; set; }
  public int RecordsArchived { get; set; }
  public int RecordsDeleted { get; set; }
  public string Status { get; set; } // COMPLETED, FAILED
}
```

**API:**
- GET /api/warehouse/v1/admin/retention-policies
- POST /api/warehouse/v1/admin/retention-policies
- PUT /api/warehouse/v1/admin/retention-policies/{id}
- DELETE /api/warehouse/v1/admin/retention-policies/{id}
- POST /api/warehouse/v1/admin/retention-policies/execute (manual trigger)

### Acceptance Criteria
```gherkin
Scenario: Create retention policy
  Given logged in as Admin
  When POST /api/warehouse/v1/admin/retention-policies with DataType="AUDIT_LOGS", RetentionPeriodDays=2555, ArchiveAfterDays=365
  Then retention policy created
  And response status 201

Scenario: Archive old audit logs
  Given retention policy: AUDIT_LOGS, archive after 365 days
  And 1000 audit logs older than 365 days
  When Hangfire triggers retention job
  Then 1000 audit logs moved to audit_logs_archive table
  And RetentionExecution record created: RecordsArchived=1000

Scenario: Delete data exceeding retention period
  Given retention policy: AUDIT_LOGS, retention 2555 days (7 years)
  And 100 audit logs older than 2555 days
  When retention job runs
  Then 100 audit logs deleted from audit_logs_archive
  And RetentionExecution record: RecordsDeleted=100
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Create retention policy
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/retention-policies \
  -H "Content-Type: application/json" -d '{"dataType":"AUDIT_LOGS","retentionPeriodDays":2555,"archiveAfterDays":365,"active":true}'

# Manual trigger retention job
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/retention-policies/execute
```

### Definition of Done
- [ ] RetentionPolicy and RetentionExecution entities created
- [ ] EF migration generated (including archive tables)
- [ ] CRUD endpoints in AdminController.cs
- [ ] Hangfire job for policy evaluation
- [ ] Archival logic (move to archive tables)
- [ ] Deletion logic (hard delete from archive)
- [ ] Legal hold flag support
- [ ] Unit tests: policy evaluation, archival logic (15+ tests)
- [ ] Integration test: create policy → trigger job → verify archival
- [ ] Audit logging
- [ ] Documentation: retention policy guide

---

## Task PRD-1637: PII Encryption

**Epic:** Data Retention | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.DataRetention

### Context
Encrypt PII fields (customer name, address, email) at rest for GDPR compliance. Enables transparent encryption/decryption.

### Scope
**In Scope:** Encrypt PII fields (customer name, address, email), key management, transparent decryption
**Out of Scope:** Field-level encryption for all data (PII only for Phase 1.5)

### Requirements
**Functional:**
1. PII fields: Customer.Name, Customer.Email, Customer.ShippingAddress, Customer.BillingAddress
2. Encryption: AES-256-GCM, encrypt before save, decrypt on read
3. Key management: Store encryption key in Azure Key Vault or environment variable (not in database)
4. Key rotation: Support key rotation with re-encryption job
5. EF Core value converter: Transparent encryption/decryption (application-level)

**Non-Functional:**
1. Encryption/decryption < 10ms per field
2. Key rotation: Non-blocking background job
3. Audit logging: key rotation events

**RBAC:** Admin role required for key rotation

**Data Model:**
```csharp
// Customer entity with encrypted fields
public class Customer {
  public int Id { get; set; }
  [Encrypted] // Custom attribute
  public string Name { get; set; } // Encrypted at rest
  [Encrypted]
  public string Email { get; set; }
  [Encrypted]
  public string ShippingAddress { get; set; }
  // ... other fields
}
```

**API:**
- POST /api/warehouse/v1/admin/encryption/rotate-key (trigger key rotation)

### Acceptance Criteria
```gherkin
Scenario: Encrypt customer PII on save
  Given logged in as Admin
  When POST /api/warehouse/v1/customers with Name="John Doe", Email="john@example.com"
  Then customer saved to database
  And Name field encrypted in database (ciphertext stored)
  And Email field encrypted

Scenario: Decrypt customer PII on read
  Given customer with encrypted Name="<ciphertext>"
  When GET /api/warehouse/v1/customers/1
  Then Name decrypted transparently: "John Doe"
  And response contains plain text

Scenario: Key rotation
  Given 1000 customers with PII encrypted using key v1
  When POST /api/warehouse/v1/admin/encryption/rotate-key
  Then new key v2 generated
  And background job re-encrypts all PII fields with key v2
  And old key v1 retained for 30 days (grace period)
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Create customer (PII encrypted)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/customers \
  -H "Content-Type: application/json" -d '{"name":"John Doe","email":"john@example.com","shippingAddress":"123 Main St"}'

# Verify encryption in database (manual SQL query)
# psql -d warehouse -c "SELECT name, email FROM customers WHERE id=1;"
# Expected: ciphertext, not plain text

# Trigger key rotation
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/encryption/rotate-key
```

### Definition of Done
- [ ] EF Core value converter for encryption/decryption
- [ ] AES-256-GCM encryption implementation
- [ ] Key management (Azure Key Vault or environment variable)
- [ ] [Encrypted] attribute for PII fields
- [ ] Key rotation API endpoint
- [ ] Background job for re-encryption
- [ ] Unit tests: encryption/decryption, key rotation (15+ tests)
- [ ] Integration test: save → verify encrypted → read → verify decrypted
- [ ] Audit logging for key rotation
- [ ] Documentation: PII encryption guide

---

## Task PRD-1638: GDPR Erasure Workflow

**Epic:** Data Retention | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1637 | **SourceRefs:** Universe §5.DataRetention

### Context
GDPR Right to Erasure (Right to be Forgotten) workflow. Enables customers to request data deletion.

### Scope
**In Scope:** Erasure request entity, data discovery, anonymization, erasure confirmation
**Out of Scope:** Automated erasure (manual approval required for Phase 1.5)

### Requirements
**Functional:**
1. ErasureRequest entity: CustomerId, RequestedAt, RequestedBy, Status (PENDING, APPROVED, COMPLETED, REJECTED), CompletedAt
2. Data discovery: Find all customer data (Customer, SalesOrders, Shipments, AuditLogs)
3. Anonymization: Replace PII with anonymized values (e.g., "Customer-<id>", "***@***.com")
4. Erasure: Soft delete customer (mark inactive), anonymize PII in related records
5. Confirmation: Email customer with erasure confirmation
6. API: POST /api/warehouse/v1/admin/gdpr/erasure-request, PUT /api/warehouse/v1/admin/gdpr/erasure-request/{id}/approve

**Non-Functional:**
1. Data discovery < 5 seconds (for 1000 related records)
2. Anonymization: Non-blocking background job
3. Audit logging: all erasure actions logged (immutable)

**RBAC:** Customer can request, Admin can approve/reject

**Data Model:**
```csharp
public class ErasureRequest {
  public Guid Id { get; set; }
  public int CustomerId { get; set; }
  public string Reason { get; set; }
  public ErasureStatus Status { get; set; } // PENDING, APPROVED, COMPLETED, REJECTED
  public DateTime RequestedAt { get; set; }
  public string RequestedBy { get; set; }
  public DateTime? ApprovedAt { get; set; }
  public string ApprovedBy { get; set; }
  public DateTime? CompletedAt { get; set; }
  public string RejectionReason { get; set; }
}
```

**API:**
- POST /api/warehouse/v1/admin/gdpr/erasure-request
- GET /api/warehouse/v1/admin/gdpr/erasure-requests
- PUT /api/warehouse/v1/admin/gdpr/erasure-request/{id}/approve
- PUT /api/warehouse/v1/admin/gdpr/erasure-request/{id}/reject

### Acceptance Criteria
```gherkin
Scenario: Customer requests data erasure
  Given customer with Id=5
  When POST /api/warehouse/v1/admin/gdpr/erasure-request with CustomerId=5, Reason="No longer using service"
  Then erasure request created with Status=PENDING
  And Admin notified via email

Scenario: Admin approves erasure request
  Given erasure request with Id=X, Status=PENDING
  When PUT /api/warehouse/v1/admin/gdpr/erasure-request/X/approve
  Then Status=APPROVED
  And background job triggered for anonymization

Scenario: Anonymize customer data
  Given erasure request approved for CustomerId=5
  And customer has 10 sales orders, 5 shipments
  When anonymization job runs
  Then Customer.Name = "Customer-5", Customer.Email = "***@***.com"
  And all related SalesOrders.CustomerName = "Customer-5"
  And Customer.Active = false (soft delete)
  And Status=COMPLETED
  And confirmation email sent to customer (original email, before anonymization)
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Create erasure request
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/gdpr/erasure-request \
  -H "Content-Type: application/json" -d '{"customerId":5,"reason":"No longer using service"}'

# Approve erasure request
curl -H "Authorization: Bearer $TOKEN" -X PUT http://localhost:5000/api/warehouse/v1/admin/gdpr/erasure-request/<id>/approve
```

### Definition of Done
- [ ] ErasureRequest entity created
- [ ] EF migration generated
- [ ] CRUD endpoints in AdminController.cs
- [ ] Data discovery logic (find all customer-related records)
- [ ] Anonymization logic (replace PII with anonymized values)
- [ ] Background job for anonymization
- [ ] Email confirmation
- [ ] Unit tests: data discovery, anonymization (15+ tests)
- [ ] Integration test: request → approve → verify anonymization
- [ ] Audit logging (immutable)
- [ ] Documentation: GDPR erasure workflow guide

---

## Task PRD-1639: Backup/Restore Procedures

**Epic:** Data Retention | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** None | **SourceRefs:** Universe §5.DataRetention

### Context
Automated daily backups (PostgreSQL), point-in-time recovery, restore testing, documentation.

### Scope
**In Scope:** Automated daily backups (PostgreSQL), point-in-time recovery, restore testing, documentation
**Out of Scope:** Multi-region replication (single region for Phase 1.5)

### Requirements
**Functional:**
1. Automated backups: Daily PostgreSQL pg_dump (full backup), store in blob storage (Azure Blob or S3)
2. Point-in-time recovery: WAL archiving enabled, restore to any point in last 7 days
3. Backup retention: Keep daily backups for 90 days, weekly backups for 1 year
4. Restore testing: Monthly automated restore test (restore to test environment, verify data integrity)
5. Documentation: Runbook with restore procedures, RTO/RPO targets

**Non-Functional:**
1. Backup duration < 30 minutes (for 100GB database)
2. Restore duration < 2 hours (RTO target)
3. RPO: < 1 hour (WAL archiving)

**RBAC:** DevOps/Admin role required

**Data Model:**
```csharp
public class BackupExecution {
  public Guid Id { get; set; }
  public DateTime BackupStartedAt { get; set; }
  public DateTime? BackupCompletedAt { get; set; }
  public BackupType Type { get; set; } // FULL, INCREMENTAL
  public long BackupSizeBytes { get; set; }
  public string BlobPath { get; set; }
  public BackupStatus Status { get; set; } // COMPLETED, FAILED
  public string ErrorMessage { get; set; }
}
```

**API:**
- POST /api/warehouse/v1/admin/backups/trigger (manual backup)
- GET /api/warehouse/v1/admin/backups (backup history)
- POST /api/warehouse/v1/admin/backups/restore (restore from backup)

### Acceptance Criteria
```gherkin
Scenario: Automated daily backup
  Given Hangfire job scheduled for daily backup at 2am
  When job triggers at 2am
  Then pg_dump executed
  And backup file uploaded to blob storage: backups/warehouse-20260212-020000.sql.gz
  And BackupExecution record created: Status=COMPLETED, BackupSizeBytes=5GB

Scenario: Point-in-time recovery
  Given WAL archiving enabled
  And database state at 2026-02-12 10:00:00
  When restore to 2026-02-12 09:30:00
  Then database restored to exact state at 09:30:00
  And all transactions after 09:30:00 discarded

Scenario: Monthly restore test
  Given Hangfire job scheduled for monthly restore test
  When job triggers on 1st of month
  Then latest backup restored to test environment
  And data integrity checks run (row counts, checksums)
  And test results logged
  And alert sent if test fails
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Trigger manual backup
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/backups/trigger

# View backup history
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/backups

# Restore from backup (requires backup ID)
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/backups/restore \
  -H "Content-Type: application/json" -d '{"backupId":"<guid>","targetEnvironment":"test"}'
```

### Definition of Done
- [ ] BackupExecution entity created
- [ ] EF migration generated
- [ ] Hangfire job for daily backups
- [ ] pg_dump script with compression
- [ ] Blob storage upload (Azure Blob or S3)
- [ ] WAL archiving configuration
- [ ] Point-in-time recovery script
- [ ] Monthly restore test job
- [ ] Backup/restore API endpoints
- [ ] Runbook documentation (restore procedures, RTO/RPO)
- [ ] Unit tests: backup logic (5+ tests)
- [ ] Integration test: backup → restore → verify
- [ ] Audit logging

---

## Task PRD-1640: Disaster Recovery Plan

**Epic:** Data Retention | **Phase:** 1.5 | **Sprint:** 8 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1639 | **SourceRefs:** Universe §5.DataRetention

### Context
Disaster recovery runbook, RTO/RPO targets, failover procedures, DR testing schedule.

### Scope
**In Scope:** DR runbook, RTO/RPO targets, failover procedures, DR testing schedule
**Out of Scope:** Multi-region active-active (single region with DR site for Phase 1.5)

### Requirements
**Functional:**
1. DR runbook: Step-by-step procedures for disaster scenarios (data center outage, database corruption, ransomware)
2. RTO/RPO targets: RTO < 4 hours, RPO < 1 hour
3. Failover procedures: Restore from backup, switch DNS, verify services
4. DR testing: Quarterly DR drill (simulate disaster, execute failover, measure RTO)
5. Communication plan: Stakeholder notification, status updates

**Non-Functional:**
1. Runbook clarity: Executable by on-call engineer without prior DR experience
2. DR drill duration: < 4 hours (RTO target)

**RBAC:** DevOps/Admin role required

**Data Model:**
```csharp
public class DRDrill {
  public Guid Id { get; set; }
  public DateTime DrillStartedAt { get; set; }
  public DateTime? DrillCompletedAt { get; set; }
  public DisasterScenario Scenario { get; set; } // DATA_CENTER_OUTAGE, DATABASE_CORRUPTION
  public TimeSpan ActualRTO { get; set; }
  public DrillStatus Status { get; set; } // COMPLETED, FAILED
  public string Notes { get; set; }
  public List<string> IssuesIdentified { get; set; }
}
```

**API:**
- POST /api/warehouse/v1/admin/dr/drill (trigger DR drill)
- GET /api/warehouse/v1/admin/dr/drills (drill history)

### Acceptance Criteria
```gherkin
Scenario: Execute DR drill
  Given DR runbook documented
  When POST /api/warehouse/v1/admin/dr/drill with Scenario="DATA_CENTER_OUTAGE"
  Then DR drill started
  And runbook steps executed: 1) Restore from backup, 2) Switch DNS, 3) Verify services
  And ActualRTO measured: 3.5 hours
  And DRDrill record created: Status=COMPLETED, ActualRTO=3.5h

Scenario: Identify issues during DR drill
  Given DR drill in progress
  And DNS switch fails (manual intervention required)
  When drill completes
  Then IssuesIdentified includes "DNS switch automation failed"
  And action items created for remediation

Scenario: Quarterly DR testing schedule
  Given Hangfire job scheduled for quarterly DR drill
  When job triggers on 1st of quarter
  Then DR drill executed automatically
  And results emailed to DevOps team
```

### Validation
```bash
TOKEN=$(cat /tmp/token.txt)

# Trigger DR drill
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/admin/dr/drill \
  -H "Content-Type: application/json" -d '{"scenario":"DATA_CENTER_OUTAGE"}'

# View drill history
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/admin/dr/drills
```

### Definition of Done
- [ ] DR runbook documented (Markdown in docs/disaster-recovery.md)
- [ ] DRDrill entity created
- [ ] EF migration generated
- [ ] DR drill API endpoints
- [ ] Hangfire job for quarterly DR testing
- [ ] Failover scripts (restore, DNS switch, service verification)
- [ ] Communication plan template
- [ ] Unit tests: drill execution logic (5+ tests)
- [ ] Integration test: trigger drill → verify steps executed
- [ ] Audit logging
- [ ] Documentation: DR runbook, RTO/RPO targets
