# PII Encryption Guide (PRD-1637)

## Scope
PII-at-rest encryption for customer data using AES-256-GCM with transparent EF Core value converters.

## Protected fields
- `Customer.Name`
- `Customer.Email`
- `Customer.BillingAddress.*`
- `Customer.DefaultShippingAddress.*`

## Key management
- Keys are loaded from `PII_ENCRYPTION_KEYS` (`keyId:base64,keyId:base64` format).
- Active key can be pinned with `PII_ENCRYPTION_ACTIVE_KEY`.
- If env keys are absent, a development fallback key is generated for non-production continuity.

## Rotation
- Endpoint: `POST /api/warehouse/v1/admin/encryption/rotate-key`
- Rotation stores key metadata in `pii_encryption_keys` and marks prior key with 30-day grace.
- A Hangfire background job (`PiiReencryptionJob`) rewrites customer rows so converter re-encrypts using the new active key.

## Validation notes
- Ciphertext is stored with `enc:<keyId>:<nonce>:<tag>:<cipher>` format.
- Read paths continue returning plain text values via converter decryption.
