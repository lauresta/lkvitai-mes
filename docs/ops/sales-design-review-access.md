# Temporary Sales design review access

The test Sales WebUI can exchange `/sales/?review=<64-hex-character-key>` for a
12-hour, HTTP-only cookie scoped to `/sales`. It immediately redirects to
`/sales/`, removing the key from the address bar. The link works only when the
host runs as `Test` and both settings are present:

- GitHub Environment `test` secret `SALES_REVIEW_KEY` (32 random bytes as hex).
- GitHub Environment `test` variable `SALES_REVIEW_EXPIRES_AT_UTC` (ISO 8601 UTC).

The link is a bearer credential until its expiration. Share it only with the
intended reviewer. The reviewer sees the Sales UI, the in-memory sample orders
and the UI-only new-order draft. `SalesApiClient` never calls the legacy SQL
API for review sessions. The review cookie is distinct from PortalAuth and is
not sent to `/portal` or `/api/sales`. Ordinary users still sign in through
Portal and see live orders.

To revoke access, clear `SALES_REVIEW_KEY` in the `test` environment and
redeploy Sales WebUI. Existing review cookies are then rejected. Rotating the
key also rejects existing cookies. To remove the feature from code, remove
`SalesReviewAccess`, its Program.cs registration/middleware, the review branch
in `SalesApiClient`, and the two deployment settings; no shared Portal auth
code or Sales API code is modified by this feature.
