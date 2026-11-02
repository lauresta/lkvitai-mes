# Sales M2 automatic test-deployment contract

The shared legacy SQL database is production data. Every automatic test deploy
must preserve the previously approved v25 restrictions rather than rely on a
manually patched container's environment:

- Order entry enabled only for the four fixed Testavimas series and the verified
  database fingerprint in `docker-compose.test.yml`.
- Cancellation explicitly disabled; the owner declined the shared audit repair.
- Payments enabled with EUR (ID 2), the approved receiver and existing four native
  method IDs. No existing payments or currencies are migrated.
- Production confirmation enabled only inside the same Testavimas scope, prefix
  `MEST`; actual production confirmation remains for owner manual verification.
- Build version follows the deployed image tag.

Warehouse's durable user store and Portal CMS use the same configured test
PostgreSQL target. Warehouse has the existing shared Portal DataProtection keys
mounted read-only for the purpose-bound identity bridge. Sales API and WebUI
retain the shared SSO metadata and internal Cikada-user service URL. SQL strings,
OIDC client secret and other credentials remain in the existing GitHub `test`
environment secrets; none are copied into this contract.

`SalesDeploymentContractTests` checks these gates and wiring so ordinary main
rollouts cannot silently drop M2 or broaden the approved test-write scope.
