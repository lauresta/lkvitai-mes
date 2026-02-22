# GDPR Erasure Workflow Guide (PRD-1638)

## Endpoints
- `POST /api/warehouse/v1/admin/gdpr/erasure-request`
- `GET /api/warehouse/v1/admin/gdpr/erasure-requests`
- `PUT /api/warehouse/v1/admin/gdpr/erasure-request/{id}/approve`
- `PUT /api/warehouse/v1/admin/gdpr/erasure-request/{id}/reject`

## Workflow
1. Request is created with `PENDING` status.
2. Admin approves or rejects.
3. Approval enqueues Hangfire `GdprErasureJob`.
4. Job anonymizes customer PII, related shipping addresses, and summary read-model customer names.
5. Customer is marked `Inactive`, request marked `COMPLETED`, and immutable audit entries are written.

## Anonymization rules
- `Customer.Name` -> `Customer-<guid>`
- `Customer.Email` -> `***@***.com`
- `Customer.Phone` -> null
- `BillingAddress` / `DefaultShippingAddress` / `SalesOrder.ShippingAddress` fields -> `REDACTED`
- `OutboundOrderSummary.CustomerName` and `ShipmentSummary.CustomerName` rewritten to anonymized name

## Confirmation
Email delivery is represented by immutable audit event `GDPR_ERASURE_CONFIRMATION_SENT` with original email for operational follow-up.
