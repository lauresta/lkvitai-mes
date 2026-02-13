# Permission Model Guide

This document describes granular RBAC permissions for LKvitai.MES (`PRD-1629`).

## Model

Permission tuple format:

`Resource:Action:Scope`

- Resource: `ITEM`, `ORDER`, `LOCATION`, `QC`
- Action: `READ`, `UPDATE`
- Scope: `ALL`, `OWN`

Users receive permissions through role assignments (`user_role_assignments` + `role_permissions`).

## Scope Semantics

- `ALL`: operation allowed on any target resource owner.
- `OWN`: operation allowed only when target owner is the current user.

Permission checks use fallback:

- `OWN` request passes if user has `OWN` or `ALL`.
- `ALL` request passes only with `ALL`.

## API

Admin endpoints:

- `GET /api/warehouse/v1/admin/permissions`
- `POST /api/warehouse/v1/admin/permissions/check`

Check payload:

```json
{
  "userId": "00000000-0000-0000-0000-000000000105",
  "resource": "ORDER",
  "action": "UPDATE",
  "ownerId": "00000000-0000-0000-0000-000000000105"
}
```

Response:

```json
{
  "allowed": true
}
```

## Middleware Policy Layer

`PermissionPolicyMiddleware` applies resource-action checks on mapped operational routes for users that have explicit role assignments.

Examples:

- `GET /api/warehouse/v1/items` -> `ITEM:READ:ALL`
- `POST /api/warehouse/v1/sales-orders` -> `ORDER:UPDATE:ALL`
- `PUT /api/warehouse/v1/locations/{id}` -> `LOCATION:UPDATE:ALL`

## Cache Invalidation

Permission cache is invalidated on role create/update/delete and user-role assignment.
