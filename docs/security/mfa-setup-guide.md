# MFA Setup Guide

This guide documents TOTP-based MFA setup for LKvitai.MES API (`PRD-1627`).

## Configuration

Set in `src/LKvitai.MES.Api/appsettings*.json`:

```json
"Mfa": {
  "Enabled": true,
  "RequiredRoles": ["WarehouseAdmin", "WarehouseManager", "Admin", "Manager"],
  "SessionTimeoutHours": 8,
  "ChallengeTimeoutMinutes": 10,
  "MaxFailedAttempts": 5,
  "LockoutMinutes": 15
}
```

## Login Flow (OAuth + MFA)

1. User authenticates via `GET /api/auth/oauth/login` and callback `GET /api/auth/oauth/callback`.
2. If role requires MFA, callback response contains:
- `mfaRequired=true`
- `mfaEnrollmentRequired` flag
- `challengeToken`
3. Client proceeds to MFA enroll or verify step.

## Enrollment

1. `POST /api/auth/mfa/enroll` with bearer token.
2. Render `qrCodeDataUri` in authenticator app.
3. Save one-time `backupCodes` securely.
4. `POST /api/auth/mfa/verify-enrollment` with first TOTP code.

## Verification

Use challenge token from OAuth callback:

```http
POST /api/auth/mfa/verify
Content-Type: application/json

{
  "challengeToken": "...",
  "code": "123456"
}
```

Or use backup code:

```json
{
  "challengeToken": "...",
  "backupCode": "ABC123DEF456"
}
```

Success returns an MFA-verified access token.

## Backup Codes

- `GET /api/auth/mfa/backup-codes` returns remaining count.
- `GET /api/auth/mfa/backup-codes?regenerate=true` rotates and returns a new 10-code set.

## Reset

Admin reset endpoint:

```http
POST /api/auth/mfa/reset/{userId}
```

Request body must include approval confirmation:

```json
{
  "approved": true,
  "reason": "Security support request"
}
```

## Security Notes

- TOTP secret is encrypted at rest using ASP.NET Data Protection.
- Backup codes are hashed and encrypted at rest.
- Failed verification attempts trigger temporary lockout.
- MFA enforcement middleware blocks protected API access until MFA verification succeeds.
