# Retention Policy Guide (PRD-1636)

## Scope
This guide covers retention policy CRUD and execution for `AUDIT_LOGS` data in Phase 1.5.

## Endpoints
- `GET /api/warehouse/v1/admin/retention-policies`
- `POST /api/warehouse/v1/admin/retention-policies`
- `PUT /api/warehouse/v1/admin/retention-policies/{id}`
- `DELETE /api/warehouse/v1/admin/retention-policies/{id}`
- `POST /api/warehouse/v1/admin/retention-policies/execute`
- `PUT /api/warehouse/v1/admin/retention-policies/legal-hold/{auditLogId}`

## Behavior
- Policies are unique by `DataType`.
- Daily Hangfire job `retention-policy-daily` triggers at `02:00 UTC`.
- Execution archives old rows from `security_audit_logs` into `audit_logs_archive`.
- Expired archived rows are hard deleted based on retention settings.
- `LegalHold=true` rows are skipped for archive/delete.

## Notes
- `events_archive` table is created for Phase 1.5 schema parity.
- Current execution logic actively processes `AUDIT_LOGS`.
