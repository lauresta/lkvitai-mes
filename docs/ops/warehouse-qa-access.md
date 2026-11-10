# Temporary Warehouse QA access

The test Warehouse WebUI exchanges `/warehouse/?qa=<64-hex-character-key>`
for a 12-hour HTTP-only cookie scoped to `/warehouse`, then redirects to
`/warehouse/` so the key leaves the address bar. This entry works only in
the `Test` environment while the following GitHub Environment `test`
settings are present and unexpired:

Automated tests send the key in `X-Warehouse-QA-Key` on `GET /warehouse/`
before Playwright tracing starts, so the key is absent from URLs and trace files.

- Secret `WAREHOUSE_QA_KEY` (32 random bytes, hex encoded).
- Variable `WAREHOUSE_QA_EXPIRES_AT_UTC` (ISO 8601 UTC).
- Variable `WAREHOUSE_QA_ENABLED=true` for Warehouse API proof validation.

The WebUI creates a DataProtection proof for the API. The API accepts that proof
only in `Test` with `WarehouseQa:Enabled=true`; normal SSO and production
authentication are unchanged. QA operations write to the test Warehouse
PostgreSQL database and are audited as `warehouse-qa`. The key is a bearer
credential until expiry and must stay out of issue bodies, logs and test
artifacts.

To revoke access early, clear `WAREHOUSE_QA_KEY` and redeploy the Warehouse
WebUI; also set `WAREHOUSE_QA_ENABLED=false` and redeploy the Warehouse API to
invalidate outstanding API proofs. The configured expiry ends entry and cookie
use without another deploy. Remove this temporary feature after the QA run.

The test-only `POST /api/warehouse/v1/admin/projections/qa-rebuild-stock`
operation is available only to this QA identity in the `Test` environment.
It uses Marten's event replay to repair AvailableStock and LocationBalance
after the stock movement projection fix. Remove this operation after the QA run.
