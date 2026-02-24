# P-14 — System Administration & Compliance

**Status:** 🟡 Placeholder — BPMN and scenarios pending
**Priority:** Core (Phase 1 implemented)

---

## Summary

Manages system configuration, user access, API keys, compliance obligations (GDPR, audit trails), data retention, disaster recovery, and operational health.

**Evidence:**
- UI: 11 admin routes — `Admin/Settings.razor`, `Admin/AuditLogs.razor`, `Admin/GdprErasure.razor`, `Admin/Backups.razor`, `Admin/DisasterRecoveryDrills.razor`, etc.
- Controllers: 12 admin controllers — `AdminApiKeysController`, `AdminUsersController`, `AdminRolesController`, `AdminGdprController`, `AdminAuditLogsController`, `AdminBackupsController`, `AdminDisasterRecoveryController`, `AdminEncryptionController`, `AdminRetentionPoliciesController`, `AdminComplianceController`, `AdminApprovalRulesController`, `AdminSlaController`
- Services: `GdprErasureService.cs`, `PiiEncryptionService.cs`, `SecurityAuditLogService.cs`, `ApiKeyService.cs`, `BackupServices.cs`, `DisasterRecoveryService.cs`, `AlertEscalationService.cs`, `SlaMonitoringService.cs`

---

## Trigger

Admin action, scheduled DR drill, regulatory request, security audit, or system alert.

## Outcomes

- System configured; access controlled by role
- Audit trail maintained (immutable)
- GDPR erasure requests fulfilled
- Backups verified; DR drills documented
- API keys rotated

## Actors

| Role | Responsibility |
|------|---------------|
| System Administrator | All admin operations |
| Compliance Officer / Auditor | Reads audit logs, compliance reports; triggers GDPR erasure |

## UI Entry Points

| Route | File | Nav |
|-------|------|-----|
| `/admin/users` | `AdminUsers.razor` | Admin → Users |
| `/warehouse/admin/settings` | `Admin/Settings.razor` | Admin → Admin Settings |
| `/warehouse/admin/roles` | `Admin/Roles.razor` | Admin → Roles |
| `/warehouse/admin/api-keys` | `Admin/ApiKeys.razor` | Admin → API Keys |
| `/warehouse/admin/audit-logs` | `Admin/AuditLogs.razor` | Admin → Audit Logs |
| `/warehouse/admin/gdpr-erasure` | `Admin/GdprErasure.razor` | Admin → GDPR Erasure |
| `/warehouse/admin/backups` | `Admin/Backups.razor` | Admin → Backups |
| `/warehouse/admin/retention-policies` | `Admin/RetentionPolicies.razor` | Admin → Retention Policies |
| `/warehouse/admin/dr-drills` | `Admin/DisasterRecoveryDrills.razor` | Admin → DR Drills |
| `/warehouse/admin/approval-rules` | `Admin/ApprovalRules.razor` | Admin → Approval Rules |
| `/warehouse/admin/reason-codes` | `Admin/ReasonCodes.razor` | Admin → Reason Codes |

## Primary API Endpoints

| Method | Route | Controller | Auth |
|--------|-------|-----------|------|
| GET/POST/PUT/DELETE | `api/warehouse/v1/admin/api-keys` | AdminApiKeysController | AdminOnly |
| GET/POST/PUT/DELETE | `api/admin/users` | AdminUsersController | AdminOnly |
| GET/POST/PUT/DELETE | `api/warehouse/v1/admin/roles` | AdminRolesController | AdminOnly |
| GET | `api/warehouse/v1/admin/audit-logs` | AdminAuditLogsController | AdminOnly |
| POST | `api/warehouse/v1/admin/gdpr/erasure-request` | AdminGdprController | AdminOnly |
| GET/POST | `api/warehouse/v1/admin/backups` | AdminBackupsController | AdminOnly |
| GET/POST | `api/warehouse/v1/admin/disaster-recovery` | AdminDisasterRecoveryController | AdminOnly |
| GET/POST | `api/warehouse/v1/admin/compliance/audit-export` | AdminComplianceController | AdminOnly |

## Key Domain Objects

`User`, `Role`, `ApiKey`, `AuditLog`, `RetentionPolicy`, `BackupRecord`, `GdprRequest`

## Files

- [`bpmn.md`](bpmn.md) — Process flow (TODO)
- [`scenarios.md`](scenarios.md) — Scenarios (TODO)
- [`test-data.md`](test-data.md) — Test data (TODO)
