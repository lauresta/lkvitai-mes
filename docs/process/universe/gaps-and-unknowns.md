# Gaps and Unknowns

Items identified during repo scanning that have surface-level evidence (UI page or API endpoint) but **lack complete implementation detail**, or are explicitly **Phase 2 scope** in architecture docs.

**Source:** `docs/processes/process-universe.md` §Appendix D + CLAUDE.md known open items.
**Last updated:** 2026-02-24

---

## Gap Register

| ID | Item | Evidence | Status | Owner |
|----|------|----------|--------|-------|
| U-01 | **Replenishment rules** — automatic reorder / zone replenishment triggers | No dedicated UI route or controller found | Phase 2 / Not implemented | TBD |
| U-02 | **AdvancedWarehouseController auth policies** — waves, cross-dock, RMA, serials, handling-unit endpoints lack explicit auth policy strings in scan output | `AdvancedWarehouseController.cs` exists; nested controller policies not captured | Needs code verification | Dev |
| U-03 | **ERP/Kafka inbound integration** — `MaterialRequested` consumed, `CreateReservation` issued | Mentioned in CLAUDE.md + `docs/04`; anti-corruption layer in `Modules.Warehouse.Integration/` | Implemented, no UI; integration-only | Dev |
| U-04 | **ReservationTimeoutSaga** — auto-cancel HARD locks after 2-hour policy | CLAUDE.md HIGH-02 open item | Phase 2 scope | TBD |
| U-05 | **FedEx shipping integration** — `FedExApiService.cs` registered in Program.cs | No UI route or controller endpoint exposes FedEx directly | Integration scaffold only | TBD |
| U-06 | **PagerDuty integration** — HTTP client registered in Program.cs | No UI or controller found; alert escalation service mentioned | Integration scaffold only | TBD |
| U-07 | **Capacity planning UI** — `AdminCapacityController` exists (`api/warehouse/v1/admin/capacity`) | No Razor page found in WebUI | API exists, UI missing | Dev |
| U-08 | **MFA setup page** — `MfaController` endpoints (`api/auth/mfa`) | No dedicated Razor page found | Auth flow handled differently (possible SSO) | Dev |
| U-09 | **Idempotency query endpoint** — `GET api/warehouse/v1/idempotency/{key:guid}` | `IdempotencyController` exists; no UI page | Developer/diagnostic tool only | Dev |
| U-10 | **Virtual location phantom stock** — SUPPLIER, PRODUCTION, SCRAP, SYSTEM locations create phantom AvailableStock | CLAUDE.md MED-02 open item | Phase 2 fix | Dev |
| U-11 | **SLA configuration UI** — `AdminSlaController` exists | No SLA admin page found in WebUI scan | API exists, UI unknown | Dev |
| U-12 | **Chaos resilience / DR drill detail** — `ChaosResilienceService.cs` registered | DR Drills page exists at `/warehouse/admin/dr-drills`; internal chaos testing detail unclear | Implementation detail unknown | Dev |

---

## Known Tech Debt (from CLAUDE.md)

| ID | Severity | Description |
|----|----------|-------------|
| ARCH-01 | High | `MasterDataEntities.cs` god object (~1400 LOC, 50+ entities) — must be decomposed before module extraction |
| ARCH-02 | High | Business logic in `Api.Services/` (34 files, e.g. `SalesOrderCommandHandlers.cs`, `ValuationLifecycleCommandHandlers.cs`) — belongs in Application layer |

---

## How to resolve a gap

1. Verify the gap by reading the relevant source file(s)
2. If implemented: update the process universe doc (`docs/processes/process-universe.md`) and remove from this list
3. If truly missing: create a GitHub issue / backlog item; update "Status" here
4. Close gaps with evidence (file path + route) — never by assumption
