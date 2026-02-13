# Security Audit Log Schema

`security_audit_logs` captures security-relevant operations.

## Columns

- `Id` (`bigint`, PK, identity)
- `UserId` (`varchar(200)`, nullable)
- `Action` (`varchar(100)`, required)
- `Resource` (`varchar(100)`, required)
- `ResourceId` (`varchar(200)`, nullable)
- `IpAddress` (`varchar(100)`, required)
- `UserAgent` (`varchar(1000)`, required)
- `Timestamp` (`timestamptz`, required)
- `Details` (`text`, required JSON payload)

## Indexes

- `IX_security_audit_logs_UserId`
- `IX_security_audit_logs_Timestamp`
- `IX_security_audit_logs_Action_Resource`

## Notes

- Table is append-only by application policy.
- Automatic entries are generated for mutating API requests (`POST/PUT/PATCH/DELETE`).
- Manual entries are generated for sensitive events (for example MFA reset and role changes).
