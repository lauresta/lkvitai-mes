# Security Audit Log Retention

Retention policy for security audit logs (`PRD-1630`):

- Retention period: **7 years** minimum.
- Storage model: append-only records for compliance.
- Deletion policy: controlled by retention engine tasks in later sprint (`PRD-1636`).
- Query access: admin-only endpoint `GET /api/warehouse/v1/admin/audit-logs`.

Operational guidance:

1. Keep online storage/indexes sized for active query window.
2. Archive records older than operational window before deletion eligibility.
3. Never mutate historical records; corrections must be recorded as new events.
