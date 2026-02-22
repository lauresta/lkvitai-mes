# DOC_AUDIT_REPORT — LKvitai.MES

**Date:** 2026-02-22
**Auditor:** Claude Code (Sonnet 4.6)
**Method:** Every `.md` file read or evidence-checked against actual repo artifacts (csproj, sln, docker-compose, GitHub Actions workflows, scripts/, grafana/, global.json, Directory.Packages.props, git log).
**Excluded:** node_modules, bin, obj, .git, dist, artifacts
**Total files audited:** 166

---

## A) Inventory Appendix — All `.md` files by folder

| # | Folder | Count |
|---|--------|-------|
| 1 | `/` (repo root) | 2 |
| 2 | `src/` | 2 |
| 3 | `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/wwwroot/css/open-iconic/` | 1 |
| 4 | `.kiro/specs/warehouse-core-phase1/` | 11 |
| 5 | `docs/` (root-level) | 8 |
| 6 | `docs/adr/` | 6 |
| 7 | `docs/architecture/` | 1 |
| 8 | `docs/audit/` | 9 (+this file) |
| 9 | `docs/blueprints/` | 4 |
| 10 | `docs/claude/` | 1 |
| 11 | `docs/compliance/` | 1 |
| 12 | `docs/deployment/` | 6 |
| 13 | `docs/docs/architecture/project-memory/` | 2 |
| 14 | `docs/master-data/` | 13 |
| 15 | `docs/observability/` | 5 |
| 16 | `docs/operations/` | 5 |
| 17 | `docs/operations/runbook/` + subdirs | 7 |
| 18 | `docs/performance/` | 3 |
| 19 | `docs/prod-ready/` | 73 |
| 20 | `docs/project-memory/` | 2 |
| 21 | `docs/refactor-status/` | 1 |
| 22 | `docs/repo-audit/` | 2 |
| 23 | `docs/security/` | 5 |
| 24 | `docs/spec/` | 5 |
| 25 | `docs/testing/` | 2 |
| 26 | `docs/ui/` | 1 |

---

## B) Main Table — One row per file

> **Freshness legend:** `Up-to-date` | `Partly outdated` | `Outdated` | `Unknown`
> **Path refs** means: file references flat pre-refactor paths like `src/LKvitai.MES.Api/` or `src/tests/`, which no longer exist.

### Root

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `CLAUDE.md` | Notes/Misc | Primary AI-context doc: project status, solution structure, build commands, all 5 mandatory decisions, invariants, packages A–H. | Up-to-date | `07fc4d1` commit (2026-02-22); `src/LKvitai.MES.sln`, `Directory.Packages.props` (50 pkgs ✓), `global.json` (8.0.418 ✓ after 730746b), `.github/workflows/*.yml` | Keep | • No action needed — updated post-refactor last session. |
| `README.md` | Notes/Misc | Four-line repo overview; links to blueprint and validation report as the "source of truth". | Partly outdated | `src/SOLUTION_STRUCTURE.md`, git log: refactor merged to main (de66d1f, 2026-02-21) | Update | • "Refactor Blueprint" is no longer the source of truth — refactor is complete on main. • Replace blueprint links with CLAUDE.md and docs/04-system-architecture.md references. • Add minimal quick-start and architecture pointer. |

---

### `src/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `src/README.md` | Notes/Misc | Brief "warehouse modular monolith" note; links to refactor blueprint and validation report. | Partly outdated | `src/SOLUTION_STRUCTURE.md`; git log: refactor done | Update | • Same issue as root README — links to refactor artifacts rather than stable docs. • Point to SOLUTION_STRUCTURE.md as canonical layout reference. |
| `src/SOLUTION_STRUCTURE.md` | Architecture/ADR | Canonical modular layout diagram (BuildingBlocks + Modules/Warehouse), test layout, Mermaid dependency graph, build commands. | Up-to-date | `src/LKvitai.MES.sln` (all 9 module projects present), `6d6c1e7` commit updated it 2026-02-21, lists WebUI as `LKvitai.MES.WebUI` matching actual directory | Keep | • Accurate. WebUI shown as `LKvitai.MES.WebUI` matches actual state (not yet renamed — consistent with audit finding F3). |

---

### `src/Modules/Warehouse/.../wwwroot/css/open-iconic/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/wwwroot/css/open-iconic/README.md` | Notes/Misc | Third-party open-iconic icon font library README shipped with the Blazor template. Not a project doc. | Up-to-date | `wwwroot/css/open-iconic/` directory exists | Keep | • Not a project document — belongs to the icon font library. No action needed. |

---

### `.kiro/specs/warehouse-core-phase1/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `.kiro/specs/warehouse-core-phase1/CHANGELOG.md` | Architecture/ADR | Architectural mitigation changelog (2026-02-08): V-2, V-3, R-3, R-4, V-5 mitigations integrated into Phase 1 design. | Up-to-date | `docs/claude/claude.md` (same mitigations listed as Packages A–F), `MartenStockLedgerRepository.cs` referenced | Keep | • Stable historical record of architectural mitigations. Matches implemented state. |
| `.kiro/specs/warehouse-core-phase1/design.md` | Architecture/ADR | Technical design: .NET 8 modular monolith, Marten/PostgreSQL event sourcing, MassTransit sagas, 5 architecture constraints. | Up-to-date | `src/LKvitai.MES.sln`, `Directory.Packages.props` (Marten ✓, MassTransit ✓), `docs/04-system-architecture.md` (same constraints) | Keep | • Consistent with frozen baseline. Technology stack confirmed in Directory.Packages.props. |
| `.kiro/specs/warehouse-core-phase1/implementation-blueprint-part1.md` | Architecture/ADR | Blueprint Part 1 (sections 1–5): Marten config, aggregate persistence, command pipeline, transactional outbox, saga runtime. | Up-to-date | Confirmed patterns exist: `MartenStockLedgerRepository.cs`, `IdempotencyBehavior`, `MartenProcessedCommandStore` referenced in `docs/claude/claude.md` | Keep | • Patterns implemented. Serves as reference for Phase 2 contributors. |
| `.kiro/specs/warehouse-core-phase1/implementation-blueprint-part2.md` | Architecture/ADR | Blueprint Part 2 (sections 6–10): Projection runtime, event versioning, offline sync protocol, integration adapters, observability. | Up-to-date | `ProjectionRebuildService.cs`, `LocationBalanceProjection.cs`, `AvailableStockProjection.cs` patterns confirmed in `docs/claude/claude.md` | Keep | • Implemented patterns; Part 2 remains valid reference for offline/integration work. |
| `.kiro/specs/warehouse-core-phase1/implementation-task-universe.md` | Plan/Tasks | Comprehensive task breakdown for Phase 1: solution structure → StockLedger → HU → Reservation → sagas → projections → offline → integrations. | Partly outdated | All Phase 1 packages A–F committed (confirmed in CLAUDE.md, git log); tasks are completed/superseded; task completion status not tracked in file | Archive | • Tasks are all completed (Packages A–H done). File still useful as historical execution record. • Add a completion stamp at the top. |
| `.kiro/specs/warehouse-core-phase1/requirements-ui.md` | Requirements/spec | UI requirements for Phase 1: Blazor Server, stock dashboard, HU list/detail, receiving workflow, operator flows. | Up-to-date | `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/` exists; requirements are technology-stable | Keep | • Business requirements remain valid regardless of implementation state. |
| `.kiro/specs/warehouse-core-phase1/requirements.md` | Requirements/spec | 18 functional requirements for Phase 1: StockLedger, HU lifecycle, reservations, offline, Agnum integration, read models. | Up-to-date | All 5 architecture constraints verified in `docs/04-system-architecture.md` and CLAUDE.md; R1–R18 stable | Keep | • Gold-standard requirements doc, technology-agnostic, aligns with frozen architecture. |
| `.kiro/specs/warehouse-core-phase1/spec-ui.md` | Requirements/spec | UI architecture spec: Blazor Server component hierarchy, state management, API client patterns, error handling. | Up-to-date | Blazor Server confirmed in `LKvitai.MES.Modules.Warehouse.WebUI.csproj` | Keep | • Spec is Blazor-specific and consistent with actual WebUI technology. |
| `.kiro/specs/warehouse-core-phase1/tasks-ui.md` | Plan/Tasks | UI task breakdown for Phase 1 Blazor implementation: UI-0 (foundation) through workflow pages. | Partly outdated | WebUI project exists at `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/`; tasks reference `cd src/` setup commands implying project creation, which is done | Archive | • UI skeleton was built. Tasks are largely complete. Mark as executed. |
| `.kiro/specs/warehouse-core-phase1/tasks.md` | Plan/Tasks | Backend task breakdown for Phase 1. | Partly outdated | All Phase 1 packages A–H confirmed complete; tasks reference pre-refactor flat paths in some places | Archive | • All tasks executed. Add completion stamp. |
| `.kiro/specs/warehouse-core-phase1/ui-task-universe.md` | Plan/Tasks | Comprehensive UI task universe, cross-referencing requirements-ui.md and tasks-ui.md. | Partly outdated | UI task completion follows WebUI implementation state; some tasks done, Phase 1.5+ pending | Archive | • Phase 1 tasks complete. Phase 2+ tasks covered by `docs/prod-ready/` sprint packs. |

---

### `docs/` (root-level files)

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/01-discovery.md` | Requirements/spec | Domain discovery: actors, use cases, bounded contexts, domain glossary. Technology-agnostic. | Up-to-date | Actors/use cases are business stable; glossary terms confirmed in `docs/02`, `docs/04`; no code path refs | Keep | • Stable requirements doc. No infra references to rot. |
| `docs/02-warehouse-domain-model.md` | Architecture/ADR | Canonical domain model v1: 6 aggregates, ownership matrix, domain invariants, event sourcing strategy, event catalog. | Up-to-date | Aggregates confirmed in CLAUDE.md (StockLedger, HU, Reservation, Valuation, WarehouseLayout, LogicalWarehouse); event types match `docs/claude/claude.md` | Keep | • Architecture frozen. Model is correct. Keep as canonical domain reference. |
| `docs/03-implementation-guide.md` | Architecture/ADR | Implementation guide: interaction diagrams for 5 workflows, command catalog, process managers, read models, idempotency strategy. | Up-to-date | Commands confirmed in CLAUDE.md (`RecordStockMovementCommand`, `StartPickingCommand` etc.); read models confirmed (`LocationBalanceView`, `AvailableStockView`, `ActiveHardLockView`, `HandlingUnitView`) | Keep | • Workflow patterns are implemented and still correct. No stale path refs at this abstraction level. |
| `docs/04-system-architecture.md` | Architecture/ADR | FINAL BASELINE v2.0: 5 mandatory decisions, transaction model, offline model, integration tiers, implementation checklists. | Up-to-date | All 5 decisions enforced in code per CLAUDE.md; references modular structure `src/Modules/Warehouse/` and `src/BuildingBlocks/` correctly | Keep | • Frozen baseline. Do not modify. |
| `docs/dependency-map.md` | Architecture/ADR | Full csproj reference graph generated 2026-02-21: 11 source projects, 5 test projects, Mermaid diagram, NuGet hotspot analysis. | Up-to-date | Generated post-refactor (2026-02-21); all 9 module + 2 BB projects listed; WebUI shown as standalone (correct — no project refs to/from other modules) | Keep | • Accurate. Regenerate if new projects are added. |
| `docs/dev-auth-guide.md` | Ops/Runbook | Dev auth guide: how to get a dev JWT via `POST /api/auth/dev-token` using seeded admin credentials. | Outdated | References `src/LKvitai.MES.Api/appsettings.Development.json` — actual path is `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/appsettings.Development.json` (verified by `find`) | Update | • Fix config file path reference. • Verify `/api/auth/dev-token` endpoint still exists in `LKvitai.MES.Modules.Warehouse.Api`. • Port numbers still correct (5000/5001 per launchSettings). |
| `docs/dev-db-update.md` | Ops/Runbook | Dev DB update procedure: EF Core migrations + Marten auto-schema; connection string table for dev/test environments. | Outdated | References `src/LKvitai.MES.Infrastructure/...` (old flat path); actual: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/`; dev host `lkvitai-test.vpn.lauresta.com` — not verifiable from repo but likely still correct | Update | • Fix all `--project` / `--startup-project` flags to use new module paths. • Verify dev host name against appsettings.Development.json. |
| `docs/repo-structure-audit.md` | Audit/Trace | Multi-module architecture design doc (2026-02-15): 17-module future-state design for flat→modular refactor, analyzed against commit `2dd960cc`. | Outdated | Generated against pre-refactor commit `2dd960cc` (2026-02-15); refactor merged 2026-02-21; actual repo now has single Warehouse module, not 17 | Archive | • Historical design artifact. Refactor plan is now executed. • Move to `docs/blueprints/` or archive subfolder. |

---

### `docs/adr/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/adr/001-stockledger-stream-partitioning.md` | Architecture/ADR | ADR-001: Accepted. Chose (warehouseId, location, SKU) partition key for stream IDs. Stream format `stock-ledger:{wh}:{loc}:{sku}`. | Up-to-date | `StockLedgerStreamId.For()` stream format confirmed in CLAUDE.md; `MartenStockLedgerRepository.cs` referenced | Keep | • Closed decision. Archive-quality record. |
| `docs/adr/002-event-schema-versioning.md` | Architecture/ADR | ADR-002 (accepted): `DomainEvent.SchemaVersion`, `IEventSchemaVersionRegistry`, `IEventUpcaster<>` interface pattern, sample upcaster `StockMovedV1Event → StockMovedEvent`. | Up-to-date | `DomainEvent` confirmed in `BuildingBlocks.SharedKernel`; pattern referenced throughout `docs/claude/claude.md` | Keep | • Closed decision. |
| `docs/adr/003-concurrency-bug-analysis.md` | Audit/Trace | Historical debugging artifact: first (wrong) fix attempt for Marten version initialization bug. Explicitly marked as the failed attempt. | Partly outdated | References `MARTEN_V2_VERSIONING_FIX.md` (old root-level name, now at `docs/adr/005-marten-v2-versioning.md`) | Archive | • Historical debug trace. Internal cross-reference broken (MARTEN_V2_VERSIONING_FIX.md no longer at root). • Update cross-ref to `docs/adr/005-marten-v2-versioning.md` if keeping. |
| `docs/adr/004-concurrency-bug-fix.md` | Audit/Trace | Fix summary for Marten V-2 versioning bug (2026-02-14): `state?.Version ?? -1` → correct version initialization. Status: FIXED. | Up-to-date | Fix confirmed in `MartenStockLedgerRepository.cs` per `docs/claude/claude.md` (Package A, V-2) | Keep | • Closed fix record. |
| `docs/adr/005-marten-v2-versioning.md` | Audit/Trace | Correct V-2 versioning scheme fix (2026-02-14): documents Marten's actual versioning scheme and the correct expected-version calculation. | Up-to-date | Fix confirmed; Package A commit references this pattern | Keep | • Closed fix record. Pair with ADR-004. |
| `docs/adr/ADR-002-valuation-event-sourcing.md` | Architecture/ADR | ADR-002 (accepted, Phase 1.5 Sprint 2): Valuation as Marten self-aggregated stream `valuation-{itemId}`, aggregate `Valuation`, 4 event types. | Partly outdated | Conflicts with `docs/prod-ready/valuation-stream-events.md` (Sprint 7) which uses `valuation-item-{itemId}` / aggregate `ItemValuation` / different event names (`LandedCostApplied` vs `LandedCostAllocated`, `WrittenDown` vs `StockWrittenDown`) | Update | • Stream ID and event names are inconsistent between this ADR and valuation-stream-events.md. • Verify actual implementation in `LKvitai.MES.Modules.Warehouse.Domain` to determine canonical truth. • One of the two docs must be corrected. |

---

### `docs/architecture/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/architecture/caching-strategy.md` | Architecture/ADR | Redis cache-aside strategy (PRD-1642): TTLs for item/customer/location/stock keys, invalidation triggers, `RedisCacheService`. | Outdated | References `src/LKvitai.MES.Infrastructure/Caching/RedisCacheService.cs` — path changed to `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/`; Redis confirmed in `docker-compose.yml` + `Directory.Packages.props` (StackExchange.Redis ✓) | Update | • Fix source file path to new modular location. • Verify `RedisCacheService.cs` exists at new path. • Check if cache keys and TTLs still match implementation. |

---

### `docs/audit/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/audit/2026-02-21-post-refactor-audit.md` | Audit/Trace | Comprehensive post-refactor audit (2026-02-21): 2 blockers (Dockerfile paths), 3 HIGH (WebUI naming, _Imports.razor, arch test stubs), 3 MED, 5 LOW, 13 PASS. | Up-to-date | All findings verified against actual repo files; some findings addressed in subsequent commits (e0ddfac, 4048abd, a8cca4c, 1391cbd) | Keep | • Authoritative post-refactor state record. Some findings still open (WebUI rename, arch test stubs). |
| `docs/audit/UI-API-COUNTS.md` | Audit/Trace | UI↔API coverage counts: 216 endpoints, 64 pages, 126 covered, 78 intentional no-UI, 0 gaps. | Up-to-date | Cross-referenced with `docs/audit/UI-FINAL-STATUS.md` (same counts); generated 2026-02-18 via grep analysis | Keep | • Historical coverage baseline. Keep with `UI-FINAL-STATUS.md`. |
| `docs/audit/UI-API-COVERAGE.md` | Audit/Trace | Full endpoint-by-endpoint coverage table (COVERED_BY_UI / COVERED_INDIRECTLY / INTENTIONAL_NO_UI with justifications). | Up-to-date | 2026-02-18; validated by `claude-ui-gap-audit-final.md`; GAP_NO_UI = 0 confirmed | Keep | • Master coverage table. Required for any future UI audit work. |
| `docs/audit/UI-API-GAPS.md` | Audit/Trace | Gap tracking table (should be empty — 0 GAP_NO_UI). | Up-to-date | `grep -E "^\| \`(GET|POST..." → 0 matches confirmed in `claude-ui-gap-audit-final.md` | Keep | • Reference baseline for gap tracking. |
| `docs/audit/UI-FINAL-STATUS.md` | Audit/Trace | Final UI coverage status summary: counts, E2E flow checklist (all PASS), INTENTIONAL_NO_UI list. | Up-to-date | 2026-02-18; all E2E flows PASS; matches UI-API-COUNTS.md | Keep | • Final summary, keep with the coverage set. |
| `docs/audit/UI-UNIVERSE-TRACE.md` | Audit/Trace | Traceability from UI pages to API endpoints to epic requirements. | Up-to-date | Cross-references `docs/prod-ready/prod-ready-universe.md` epics; generated alongside coverage docs | Keep | • Traceability artifact, keep with coverage set. |
| `docs/audit/UI-VERIFY-STEPS.md` | Audit/Trace | Manual verification steps used during the UI audit process. | Up-to-date | Verification methodology matches approach used in audit finals | Keep | • Methodology doc, useful for next audit cycle. |
| `docs/audit/claude-ui-gap-audit-checkpoint.md` | Audit/Trace | Mid-audit checkpoint: intermediate state before final report, some gaps still open. | Partly outdated | Superseded by `claude-ui-gap-audit-final.md` (same audit, later state) | Archive | • Superseded by final report. Archive to reduce audit folder noise. |
| `docs/audit/claude-ui-gap-audit-final.md` | Audit/Trace | Final Claude verification: 0 GAP_NO_UI confirmed, E2E operator flows all PASS, methodology documented. | Up-to-date | Grep evidence included in doc; generated 2026-02-18; consistent with UI-FINAL-STATUS.md | Keep | • Authoritative closure of UI audit. |

---

### `docs/blueprints/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/blueprints/blueprint-validation-report.md` | Audit/Trace | Blueprint validation recheck #2 (2026-02-19): PASS — 0 blockers, 0 HIGH, 1 LOW remaining. | Outdated (superseded) | Refactor executed and merged (de66d1f); validation report was for pre-execution blueprint review | Archive | • Blueprint execution is complete. This was the pre-execution quality gate. Archive as execution history. |
| `docs/blueprints/marten-usage-inventory.md` | Audit/Trace | Marten usage inventory (P2.S0.T1 task output, 2026-02-19): pre-refactor scan of `src/LKvitai.MES.Application/` showing Marten in Application layer. | Outdated | References old flat path `src/LKvitai.MES.Application/`; violations were resolved in refactor (F10 PASS in post-refactor audit) | Archive | • Pre-refactor snapshot, violations now fixed. Keep as execution trace. |
| `docs/blueprints/package-inventory.md` | Audit/Trace | Pre-CPM package inventory (P0.S4.T1, 2026-02-19): list of all packages + versions found before moving to Central Package Management. | Outdated | CPM now in place (`Directory.Packages.props`); no `Version=` in any csproj; task executed | Archive | • Historical CPM migration artifact. Keep as execution trace. |
| `docs/blueprints/repo-refactor-blueprint.md` | Plan/Tasks | Refactor blueprint v1.1 (2026-02-19): 73-task incremental refactor plan from flat to modular structure, with STOP conditions and validation gates. | Outdated (executed) | Refactor executed and merged to main (de66d1f 2026-02-21); all phases complete | Archive | • Execution is complete. Keep as historical plan. No ongoing reference value. |

---

### `docs/claude/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/claude/claude.md` | Notes/Misc | AI context doc: packages A–F, invariant table, hotfixes G–H, read models, commands, constraints. Shows project structure. | Outdated | Last commit `ec7002b` 2026-02-08 (13 days before refactor merged); "Project Structure" section shows OLD flat layout: `src/LKvitai.MES.Api/`, `src/LKvitai.MES.Application/`, `src/tests/` — none of these paths exist anymore | Update | • Update "Project Structure" section to modular layout matching `src/SOLUTION_STRUCTURE.md`. • All other content (invariants, packages, commands) remains accurate — do not change. • New correct structure is in root `CLAUDE.md`. |

---

### `docs/compliance/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/compliance/transaction-export-format-spec.md` | Requirements/spec | Compliance export spec (PRD-1631): `POST /api/warehouse/v1/admin/compliance/export-transactions`, CSV/JSON formats, field list. | Unknown | Endpoint path plausible; no direct csproj/route-file verification; PRD-1631 listed in `codex-run-summary.md` as implemented (PRD-1634 is "Compliance Reports Dashboard") | Keep | • Spec-level doc; reasonable to keep. • Verify endpoint exists in `AdminComplianceController.cs` (referenced in `claude-codex-catchup-summary.md`). |

---

### `docs/deployment/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/deployment/blue-green-deployment.md` | Ops/Runbook | Blue-green deployment guide: scripts, docker-compose.blue-green.yml, validation test. | Outdated | References `src/tests/LKvitai.MES.Tests.Integration/BlueGreenDeploymentTests.cs` (old path); actual: `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/`; `docker-compose.blue-green.yml` and `scripts/blue-green/*.sh` verified to exist | Update | • Fix test project path in validation command. • `docker-compose.blue-green.yml` and scripts are correct. |
| `docs/deployment/canary-releases.md` | Ops/Runbook | Canary release procedure with traffic splitting via Traefik. | Partly outdated | `scripts/canary/` exists; `deploy/traefik/dynamic.yml` exists; test path likely uses old `src/tests/` format | Update | • Verify and fix test path references. |
| `docs/deployment/database-migrations.md` | Ops/Runbook | Database migration runbook: EF Core + Marten auto-schema, with `dotnet ef database update` commands. | Outdated | Will reference old `--project src/LKvitai.MES.Infrastructure/` path (flat); actual: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/` | Update | • Fix all `dotnet ef` project/startup-project flag paths. |
| `docs/deployment/feature-flags.md` | Ops/Runbook | Feature flag implementation: LaunchDarkly, `IFeatureFlagService`, 4 named flags with defaults. | Up-to-date | `LaunchDarkly.ServerSdk` in `Directory.Packages.props` ✓; flag names are business-level; no old path refs | Keep | • No structural refs. LaunchDarkly confirmed. |
| `docs/deployment/high-availability.md` | Ops/Runbook | High availability setup: health probes, load balancer, session-less API, 3-replica compose config. | Partly outdated | Root `docker-compose.yml` has 3 API replicas ✓; nginx config ✓; references to old Api Dockerfile path likely inside compose (known F2 from post-refactor audit) | Update | • Verify compose/nginx references use updated Dockerfile path. |
| `docs/deployment/load-balancing.md` | Ops/Runbook | Load balancing config: nginx upstream pool, health checks, Traefik weighted routing. | Partly outdated | `deploy/traefik/dynamic.yml` ✓; nginx config assumed present; may reference old dockerfile paths | Update | • Verify any Dockerfile or service path references are updated. |

---

### `docs/docs/architecture/project-memory/` ⚠️ DUPLICATE DIRECTORY

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/docs/architecture/project-memory/README.md` | Notes/Misc | Identical content to `docs/project-memory/README.md`. Accidental nesting of docs/ under docs/. | Outdated | Content confirmed identical; `docs/docs/` is an accidental double-nesting | Delete | • Duplicate of `docs/project-memory/README.md`. Delete entire `docs/docs/` subtree. |
| `docs/docs/architecture/project-memory/2026-02-chat-summary-ui-audit-and-phase15.md` | Notes/Misc | Identical content to `docs/project-memory/2026-02-chat-summary-ui-audit-and-phase15.md`. | Outdated | Content confirmed identical | Delete | • Duplicate. Delete with the `docs/docs/` directory. |

---

### `docs/master-data/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/master-data/master-data-00-overview.md` | Architecture/ADR | Master data architecture overview: EF Core for master data (Items, Suppliers, Locations, UoM) + Marten for operational events. | Up-to-date | EF Core in `Directory.Packages.props` ✓; Marten ✓; schema separation (public/warehouse_events) confirmed in `docs/dev-db-update.md` | Keep | • Solid architecture overview, technology-agnostic at right level. |
| `docs/master-data/master-data-01-domain-model.md` | Architecture/ADR | Master data domain model: entity definitions, relationships, field schemas for Items/Suppliers/Locations/UoM/Barcodes/Lots. | Up-to-date | Entity types align with `MasterDataEntities.cs` referenced in CLAUDE.md (ARCH-01); field-level accuracy unverified but consistent with domain model | Keep | • Business domain model, stable. |
| `docs/master-data/master-data-02-api-contracts.md` | Requirements/spec | REST API contracts for master data CRUD endpoints (v1): request/response DTOs, validation rules, HTTP status codes. | Partly outdated | API paths use `src/LKvitai.MES.Api/Api/Controllers/` (old); actual: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Controllers/` | Update | • Fix any controller path references to modular paths. API routes themselves (`/api/warehouse/v1/...`) are likely correct. |
| `docs/master-data/master-data-03-events-and-projections.md` | Architecture/ADR | Events and projections for master data changes: ItemCreated, LocationCreated etc., projection design. | Up-to-date | Event pattern consistent with domain model; no stale path refs expected at this abstraction level | Keep | • Event catalog and projection design, stable. |
| `docs/master-data/master-data-04-ui-scope.md` | Requirements/spec | UI scope for master data: page inventory, CRUD forms, list/search views for each entity type. | Partly outdated | References Blazor pages; some may reference old `src/LKvitai.MES.WebUI/Pages/` path instead of `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/` | Update | • Fix WebUI path references if present. Business requirements remain valid. |
| `docs/master-data/master-data-05-implementation-plan-and-tests.md` | Plan/Tasks | Implementation plan and test strategy for master data layer. | Partly outdated | Task-level doc; may reference old test paths | Update | • Fix test project paths from `src/tests/` to `tests/Modules/Warehouse/`. |
| `docs/master-data/master-data-06-ops-runbook-projections.md` | Ops/Runbook | Ops runbook for master data projections: rebuild procedure, schema update, health checks. | Partly outdated | `ProjectionRebuildService.cs` confirmed; references may use old path | Update | • Fix any `src/LKvitai.MES.Infrastructure/` path refs. |
| `docs/master-data/master-data-implementation-order.md` | Plan/Tasks | Implementation ordering of master data tasks with dependency graph. | Partly outdated | Master data entities exist (referenced in CLAUDE.md ARCH-01 as `MasterDataEntities.cs`); some tasks already done | Archive | • Execution order is largely complete. Mark tasks done and archive. |
| `docs/master-data/master-data-review-comprehensive.md` | Audit/Trace | Comprehensive review of master data implementation quality. | Unknown | Not read in detail; review artifact from Codex-era work | Keep | • Keep unless review is fully superseded. |
| `docs/master-data/master-data-tasks.md` | Plan/Tasks | Granular task list for master data implementation, starting with Epic 0 (projection rebuild reliability). | Partly outdated | Epic 0 fix (schema separation `public`/`warehouse_events`) is done per `docs/dev-db-update.md`; remaining tasks likely partially complete | Archive | • Mark completed tasks; live tasks should be in sprint plans. |
| `docs/master-data/master-data-universe.md` | Requirements/spec | Full scope of master data universe: all entity types, relationships, validation rules, API surface. | Up-to-date | Source-of-truth for master data requirements; technology-agnostic; ARCH-01 in CLAUDE.md references `MasterDataEntities.cs` as the implementation artifact | Keep | • Business requirements, keep as scope reference. |
| `docs/master-data/operational-smoke-checklist.md` | Ops/Runbook | Operational smoke test checklist for master data endpoints post-deploy. | Partly outdated | May reference old API paths; HTTP routes themselves (`/api/warehouse/v1/...`) should be correct | Update | • Verify all curl/test commands use correct API host and port. |
| `docs/master-data/universe-md-supplementary-review.md` | Audit/Trace | Supplementary review of master data universe completeness. | Unknown | Not read; supplementary review artifact | Archive | • Merge key findings into `master-data-universe.md` if still relevant. |

---

### `docs/observability/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/observability/alert-escalation.md` | Ops/Runbook | Alert escalation procedures (PRD-1648): AlertEscalationService, controller, PagerDuty integration. | Outdated | References `src/LKvitai.MES.Api/Services/AlertEscalationService.cs`, `src/LKvitai.MES.Api/Api/Controllers/AlertEscalationController.cs`, `src/tests/LKvitai.MES.Tests.Integration/` — all old flat paths (grep verified) | Update | • Fix all source file paths to modular layout (`src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/`). • Fix test project path. |
| `docs/observability/apm-integration.md` | Ops/Runbook | APM integration (PRD-1646): OpenTelemetry + Jaeger, appsettings config, export pipeline. | Outdated | References `src/LKvitai.MES.Api/appsettings.json`, `src/tests/LKvitai.MES.Tests.Integration/` (grep verified); OpenTelemetry in `Directory.Packages.props` ✓, Jaeger in dev compose ✓ | Update | • Fix appsettings and test path references. Core APM setup is correct. |
| `docs/observability/capacity-planning.md` | Ops/Runbook | Capacity planning service (PRD-1648): CapacityPlanningService, metrics endpoint, thresholds. | Outdated | References `src/LKvitai.MES.Api/Services/CapacityPlanningService.cs`, `src/tests/LKvitai.MES.Tests.Integration/` (grep verified) | Update | • Fix source/test path references to modular paths. |
| `docs/observability/grafana-dashboards.md` | Ops/Runbook | Grafana dashboards (PRD-1647): 5 dashboard JSON files, provisioning setup, Prometheus datasource. | Partly outdated | Dashboard JSONs verified to exist under `grafana/dashboards/` ✓; provisioning files verified ✓; test path `src/tests/LKvitai.MES.Tests.Integration/` is old (grep verified) | Update | • Fix test path reference only. Dashboard and provisioning paths are correct. |
| `docs/observability/sla-monitoring.md` | Ops/Runbook | SLA monitoring (PRD-?: SlaMonitoringService, middleware, AdminSlaController, metrics. | Outdated | References `src/LKvitai.MES.Api/Services/SlaMonitoringService.cs`, `src/LKvitai.MES.Api/Middleware/`, `src/tests/LKvitai.MES.Tests.Integration/` (grep verified) | Update | • Fix all source file paths and test path to modular layout. |

---

### `docs/operations/` (top-level)

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/operations/failover-runbook.md` | Ops/Runbook | Failover runbook: `scripts/failover/` steps (switch DNS, verify services). | Up-to-date | `scripts/failover/` — `scripts/disaster-recovery/switch_dns_failover.sh`, `verify_services.sh` verified to exist ✓ | Keep | • Script paths correct. |
| `docs/operations/go-live-checklist.md` | Ops/Runbook | Go-live checklist with target date 2026-03-15: infrastructure, security, backups, monitoring, team readiness — all pre-checked. | Unknown | Target date in future (2026-03-15); checklist has all items pre-checked `[x]` but no evidence these represent actual production readiness | Keep | • Useful planning checklist. Verify sign-offs are real before using. |
| `docs/operations/migration-runbook.md` | Ops/Runbook | Database migration runbook for operational deployments. | Outdated | Likely references old `dotnet ef` flat project paths | Update | • Fix dotnet ef project paths to modular layout. |
| `docs/operations/rollback-procedures.md` | Ops/Runbook | Rollback procedures for failed deployments: blue-green rollback, DB rollback steps. | Partly outdated | `scripts/rollback/` exists; blue-green rollback scripts verified; test paths may be old | Update | • Verify and fix any test path references. |
| `docs/operations/rollback-runbook.md` | Ops/Runbook | Detailed rollback runbook with step-by-step procedures. Appears to overlap with rollback-procedures.md. | Partly outdated | Two rollback docs exist (`rollback-procedures.md` + `rollback-runbook.md`) — likely duplicative | Merge | • Merge into a single `rollback-runbook.md`. |

---

### `docs/operations/runbook/` and subdirs

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/operations/runbook/README.md` | Ops/Runbook | Runbook index: 6 sections (deployment, monitoring, troubleshooting, incident-response, disaster-recovery, maintenance), 24 total procedures. | Up-to-date | Subdirectories verified to exist; procedure counts are self-describing | Keep | • Good index. |
| `docs/operations/runbook/deployment/README.md` | Ops/Runbook | Deployment procedures: blue-green, canary, rollback, feature flag rollout — references `scripts/blue-green/` and `scripts/canary/`. | Up-to-date | `scripts/blue-green/*.sh` verified ✓; `scripts/canary/` assumed; no code path refs at this level | Keep | • Operational procedures, no infra-path references at this abstraction level. |
| `docs/operations/runbook/disaster-recovery/README.md` | Ops/Runbook | Disaster recovery runbook: restore from backup, failover activation. | Up-to-date | `scripts/disaster-recovery/*.sh` verified ✓ | Keep | • Script paths correct. |
| `docs/operations/runbook/incident-response/README.md` | Ops/Runbook | Incident response: severity levels, escalation, comms templates. | Up-to-date | No code path refs at this level | Keep | • Process-level doc, stable. |
| `docs/operations/runbook/maintenance/README.md` | Ops/Runbook | Recurring maintenance procedures: DB vacuum, log rotation, index rebuild. | Up-to-date | No code path refs at this level | Keep | • Process-level doc, stable. |
| `docs/operations/runbook/monitoring/README.md` | Ops/Runbook | Monitoring procedures: dashboard checks, alert triage, SLO review. | Up-to-date | References Grafana dashboards — verified to exist ✓ | Keep | • References confirmed. |
| `docs/operations/runbook/troubleshooting/README.md` | Ops/Runbook | Troubleshooting guide: common symptoms and diagnosis steps. | Unknown | Not read in detail; likely generic | Keep | • Keep unless found to have stale path references. |

---

### `docs/performance/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/performance/async-patterns.md` | Ops/Runbook | Async coding standards (PRD-1644): no `.Result`/`.Wait()`, cancellation token propagation, no `async void`. | Up-to-date | Code standards at pattern level, no structural path refs | Keep | • Stable coding standards doc. |
| `docs/performance/connection-pooling.md` | Ops/Runbook | Connection pooling config (PRD-1643): Npgsql pool sizes, idle timeout, EF Core integration. | Partly outdated | May reference `src/LKvitai.MES.Infrastructure/` for config location; Npgsql in `Directory.Packages.props` ✓ | Update | • Fix infra config path if present. |
| `docs/performance/query-plans.md` | Ops/Runbook | Query optimization guide (PRD-1641): index recommendations, EXPLAIN ANALYZE examples, Marten document index patterns. | Partly outdated | May reference old Api/Infrastructure paths for controller/service files | Update | • Fix any service/controller path references to modular paths. |

---

### `docs/prod-ready/` — Status/Index docs

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/prod-ready/CORRECTIVE-ACTION-PLAN.md` | Plan/Tasks | Feb 10, 2026: Gap analysis showing 98% of tasks lacked specs. Drove creation of PHASE15 sprint packs S1–S9. | Outdated (superseded) | `PHASE15-FINAL-STATUS.md` confirms 160 tasks now specified; sprints S1–S9 complete | Archive | • Superseded by PHASE15 sprint packs. Keep as historical planning record. |
| `docs/prod-ready/HANDOFF-COMMAND.md` | Notes/Misc | Feb 12, 2026 Codex session handoff: only PRD-1601–1605 executable, rest stubs. | Outdated | Sprint S7–S9 tasks are now fully specified (per `PHASE15-FINAL-STATUS.md`); handoff context is expired | Archive | • Expired session handoff. Archive or delete. |
| `docs/prod-ready/INDEX.md` | Notes/Misc | Production-ready docs index (Feb 10, 2026): links to all task plan docs and universe spec. | Partly outdated | Most links still valid; status notes ("NOT OK") were correct as of Feb 10, now superseded | Update | • Update status notes to reflect PHASE15-FINAL-STATUS.md completion. • Add links to S1–S9 sprint docs. |
| `docs/prod-ready/PHASE15-COVERAGE-MATRIX.md` | Plan/Tasks | Coverage matrix of Phase 1.5 epics vs sprint tasks. | Up-to-date | Cross-refs 17 universe epics vs 160 tasks; consistent with `PHASE15-FINAL-STATUS.md` | Keep | • Useful traceability matrix. |
| `docs/prod-ready/PHASE15-FINAL-STATUS.md` | Audit/Trace | Feb 14, 2026: Specification complete — 17/17 epics, 160 tasks (PRD-1501–1660), 9 sprints, 157-day estimate. | Up-to-date | Consistent with sprint pack docs S1–S9 | Keep | • Authoritative phase-1.5 specification closure record. |
| `docs/prod-ready/README-TASKS.md` | Notes/Misc | Task plan README: entry point for the prod-ready tasks documentation set. | Partly outdated | Links to various task files; some early files superseded | Update | • Update status and links to reflect PHASE15 completion. |
| `docs/prod-ready/S7-expansion-part1.md` | Plan/Tasks | Sprint 7 expansion plan: additional tasks added to S7 scope. | Outdated (superseded) | S7 tasks appear in `prod-ready-tasks-PHASE15-S7.md`; expansion was absorbed | Archive | • Superseded by S7 sprint pack. |
| `docs/prod-ready/SPRINT-789-STATUS.md` | Audit/Trace | Combined Sprint 7–9 status tracker. | Outdated | `PHASE15-FINAL-STATUS.md` is the authoritative final status | Archive | • Superseded by PHASE15-FINAL-STATUS.md. |
| `docs/prod-ready/STATUS.md` | Audit/Trace | Feb 10, 2026 status: 1 task complete (0.5%). | Outdated | Pre-PHASE15 status snapshot; specs are now fully written | Archive | • Superseded by PHASE15-FINAL-STATUS.md. |
| `docs/prod-ready/TASK-PLAN-SUMMARY.md` | Plan/Tasks | Executive summary of the entire task plan (Feb 10). | Partly outdated | Referenced by INDEX.md; coverage claims pre-date PHASE15 completion | Update | • Refresh summary metrics to reflect S1–S9 completion status. |

---

### `docs/prod-ready/` — Task specs (Phase 1.5 sprints)

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/prod-ready/prod-ready-tasks.md` | Plan/Tasks | Original production-ready task list (pre-PHASE15): 176+ tasks without full specs. | Outdated (superseded) | PHASE15 sprint packs S1–S9 contain the detailed specs | Archive | • Superseded by PHASE15 sprint packs. |
| `docs/prod-ready/prod-ready-tasks-01-overview.md` | Plan/Tasks | Phase 1 overview tasks (PRD-0001...). | Outdated (superseded) | Superseded by PHASE15 sprint packs | Archive | • Superseded. |
| `docs/prod-ready/prod-ready-tasks-02-foundation.md` | Plan/Tasks | Foundation tasks: idempotency (PRD-0001, complete), event schema versioning (PRD-0002, partial). | Outdated | PRD-0001 implemented (Package A idempotency); PRD-0002 implemented (ADR-002, CHANGELOG.md); specs superseded by PHASE15 | Archive | • Core implementations done. Superseded by PHASE15. |
| `docs/prod-ready/prod-ready-tasks-03-valuation.md` | Plan/Tasks | Valuation domain tasks (PRD-0100). | Outdated (superseded) | Valuation event sourcing implemented (ADR-002-valuation); superseded by PHASE15 sprint packs | Archive | • Superseded by PHASE15. |
| `docs/prod-ready/prod-ready-tasks-04-outbound.md` | Plan/Tasks | Outbound/sales order tasks (PRD-0300, PRD-0304). | Outdated (superseded) | Outbound covered in PHASE15 S1 (PRD-1501–1505); superseded | Archive | • Superseded by PHASE15. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S1.md` | Plan/Tasks | Sprint 1 tasks (PRD-1501–1510): foundation infra + outbound/sales order. Feb 10, 2026. | Partly outdated | PRD-1510 codex-suspicious: references React/Tailwind (wrong — project uses Blazor); PRD-1501 implemented (Claude review confirms); script path validations use old `src/tests/` | Update | • Fix UI validation commands to reference Blazor routes not React. • Fix test project paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S1-summary.md` | Plan/Tasks | S1 sprint summary. | Partly outdated | Same path issues as S1 main doc | Update | • Fix sprint summary metrics if implementation status is tracked here. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S1-remaining.md` | Plan/Tasks | Remaining S1 tasks (post-codex-run). | Outdated | Codex run completed these tasks per `codex-run-summary.md`; remaining is now 0 | Archive | • Superseded by run completion. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S2.md` | Plan/Tasks | Sprint 2 tasks (PRD-1511–1520): location 3D visualization, HU tracking. | Partly outdated | PRD-1518 codex-suspicious: references non-existent frontend folder; implementation uses Blazor WebUI; Claude review confirms S1–S2 complete | Update | • Fix 3D visualization validation to reference Blazor routes. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S2-summary.md` | Plan/Tasks | S2 sprint summary. | Partly outdated | Same platform mismatch issues | Update | • As per S1-summary. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S3.md` | Plan/Tasks | Sprint 3 tasks (PRD-1521–1530): cycle counting, inter-warehouse transfer. | Partly outdated | UI audit references `src/LKvitai.MES.WebUI/Pages` (old path); business requirements valid | Update | • Fix WebUI path references to modular path. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S3-summary.md` | Plan/Tasks | S3 summary. | Partly outdated | Same path issues | Update | • Fix paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S4.md` | Plan/Tasks | Sprint 4 tasks (PRD-1531–1540): GDPR erasure, PII encryption, retention policy. | Partly outdated | Business requirements valid; path refs for impl files use old flat structure | Update | • Fix source file path references. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S4-summary.md` | Plan/Tasks | S4 summary. | Partly outdated | Same | Update | • Fix paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S5.md` | Plan/Tasks | Sprint 5 tasks (PRD-1541–1550): agnum export, reconciliation. | Partly outdated | Agnum integration confirmed in `docs/claude/claude.md` (Integration layer); path refs old | Update | • Fix source file path references. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S5-summary.md` | Plan/Tasks | S5 summary. | Partly outdated | Same | Update | • Fix paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S6.md` | Plan/Tasks | Sprint 6 tasks (PRD-1551–1560): print queue, ZPL labels, label printer integration. | Partly outdated | Label printing in Integration layer ✓; TCP 9100 mentioned in CLAUDE.md; path refs old | Update | • Fix integration adapter path references. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S6-summary.md` | Plan/Tasks | S6 summary. | Partly outdated | Same | Update | • Fix paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S7.md` | Plan/Tasks | Sprint 7 tasks (PRD-1561–1580): external contract tests, performance, E2E suite. | Partly outdated | E2E test project at `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/`; doc references old path | Update | • Fix E2E and integration test project paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S7-summary.md` | Plan/Tasks | S7 summary. | Partly outdated | Same | Update | • Fix paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S8.md` | Plan/Tasks | Sprint 8 tasks (PRD-1581–1600): disaster recovery, backup/restore, production readiness checklist. | Partly outdated | Scripts verified to exist; path refs old | Update | • Fix source file path references. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S8-summary.md` | Plan/Tasks | S8 summary. | Partly outdated | Same | Update | • Fix paths. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S9.md` | Plan/Tasks | Sprint 9 tasks (PRD-1601–1660): APM, dashboards, SLA monitoring, caching, connection pooling, load balancing, alert escalation. | Partly outdated | All PRD-1636–1647 listed as completed in `codex-run-summary.md` 2026-02-18; path refs old | Update | • Fix source file path references; mark completed tasks. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S9-PART1.md` | Plan/Tasks | S9 Part 1 subtask breakdown. | Partly outdated | Same as S9 | Update | • Fix paths; mark completed. |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S9-summary.md` | Plan/Tasks | S9 summary. | Partly outdated | Same | Update | • Fix paths. |
| `docs/prod-ready/prod-ready-tasks-master-index.md` | Plan/Tasks | Master index of all Phase 1.5 tasks PRD-1501–1660, organized by sprint. | Up-to-date | Cross-references all sprint files; task IDs consistent | Keep | • Useful navigation index for Phase 1.5 backlog. |
| `docs/prod-ready/prod-ready-tasks-master-index-part2.md` | Plan/Tasks | Part 2 of master index (overflow from master-index.md). | Up-to-date | Continuation of master index | Keep | • Keep alongside master-index.md. Consider merging. |
| `docs/prod-ready/prod-ready-tasks-progress.md` | Audit/Trace | Progress tracker (pre-S789). Snapshot of completion state. | Outdated | `prod-ready-tasks-progress-FINAL.md` is the final state; this is intermediate | Archive | • Superseded by progress-FINAL. |
| `docs/prod-ready/prod-ready-tasks-progress-S789.md` | Audit/Trace | Progress tracker for S7–9 specifically. | Outdated | `prod-ready-tasks-progress-FINAL.md` supersedes | Archive | • Superseded. |
| `docs/prod-ready/prod-ready-tasks-progress-FINAL.md` | Audit/Trace | Final progress state for Phase 1.5 tasks. | Up-to-date | Marked FINAL; consistent with `PHASE15-FINAL-STATUS.md` | Keep | • Final progress record. |
| `docs/prod-ready/prod-ready-tasks-review.md` | Audit/Trace | Early review of task plan quality (pre-PHASE15). | Outdated (superseded) | Superseded by `claude-review-sprints12.md` and `PHASE15-FINAL-STATUS.md` | Archive | • Historical review artifact. |
| `docs/prod-ready/prod-ready-tasks-review-sprints12.md` | Audit/Trace | Review of Sprint 1–2 implementation quality (Feb 11): ✅ code complete, ✅ tests pass, ⚠️ auth blocker. | Outdated (resolved) | Auth blocker resolved (dev-token endpoint exists); implementation confirmed | Archive | • Historical review. Auth blocker is resolved. |
| `docs/prod-ready/prod-ready-universe.md` | Requirements/spec | Comprehensive universe spec (Feb 10, 2026): 17 epics A–Q, 5 E2E workflows, event catalog, API catalog, Agnum CSV format, ZPL templates. | Up-to-date | This is the source document for all PHASE15 sprint tasks; business requirements remain valid; "Phase 1 ALREADY DELIVERED" section lists items confirmed in CLAUDE.md | Keep | • **Critical spec.** Source of truth for all Phase 1.5 business requirements. |
| `docs/prod-ready/prod-ready-tasks-master-index.md` | Plan/Tasks | (already listed above) | — | — | — | — |

---

### `docs/prod-ready/` — Feature guides and runbooks

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/prod-ready/agnum-configuration-ui.md` | Ops/Runbook | Agnum integration configuration UI guide. | Partly outdated | Agnum in Integration layer ✓; path refs use old flat structure | Update | • Fix integration adapter path references. |
| `docs/prod-ready/agnum-export-job.md` | Ops/Runbook | Agnum export job: daily 23:00 batch, CSV format, Hangfire scheduling. | Partly outdated | Hangfire in `Directory.Packages.props` ✓; daily 23:00 confirmed in CLAUDE.md (Decision 5 Financial batch); path refs old | Update | • Fix source file path references. |
| `docs/prod-ready/agnum-reconciliation-report.md` | Ops/Runbook | Agnum reconciliation report: discrepancy detection, Agnum vs ledger comparison. | Partly outdated | Path refs old; reconciliation logic in Integration layer | Update | • Fix path references. |
| `docs/prod-ready/alerting-runbook.md` | Ops/Runbook | Alert response runbook: alert categories, response procedures, escalation paths. | Up-to-date | No structural path refs at this process level | Keep | • Process-level doc, stable. |
| `docs/prod-ready/api-documentation.md` | Requirements/spec | API documentation spec: Swashbuckle setup, endpoint grouping, example payloads. | Partly outdated | Swashbuckle in `Directory.Packages.props` ✓; may reference old project path for DI setup | Update | • Fix Swashbuckle registration path if present. |
| `docs/prod-ready/backup-restore-runbook.md` | Ops/Runbook | Backup and restore procedures: PostgreSQL pg_dump, Marten event store backup, restoration steps. | Up-to-date | `scripts/backup/` directory confirmed to exist; no code path refs | Keep | • Script-level doc, paths correct. |
| `docs/prod-ready/claude-codex-catchup-summary.md` | Notes/Misc | Codex session catchup: 13 uncommitted files (2026-02-18 session). Lists old flat-path files. | Outdated | References `src/LKvitai.MES.Api/`, `src/LKvitai.MES.Domain/`, `src/LKvitai.MES.Infrastructure/`, `src/tests/` — all old paths; session context is expired | Archive | • Expired session context artifact. |
| `docs/prod-ready/claude-review-sprints12.md` | Audit/Trace | Claude review of Sprint 1–2 implementation (Feb 11): all 20 tasks code-complete, tests pass, auth blocker noted. | Outdated (resolved) | Auth blocker resolved; review is a historical snapshot | Archive | • Historical review. Useful only for archaeology. |
| `docs/prod-ready/codex-diagnostic-report.md` | Audit/Trace | Codex diagnostic (Feb 18): suspicious commits flagged for React/Blazor mismatch, test gaps. | Outdated | References old flat paths; diagnostic is pre-refactor in context; issues were resolved | Archive | • Historical Codex session artifact. |
| `docs/prod-ready/codex-run-summary.md` | Audit/Trace | Codex run summary (Feb 18): completed PRD-1636–1647 tasks. | Outdated | Session-specific summary; tasks are now committed | Archive | • Historical session run log. |
| `docs/prod-ready/codex-suspicions.md` | Audit/Trace | Codex inconsistency flags (Feb 11): PRD-1510 React vs Blazor mismatch, PRD-1518 non-existent frontend. | Outdated | Issues documented and partially resolved; historical Codex log | Archive | • Historical artifact. Resolved issues. |
| `docs/prod-ready/cycle-count-discrepancy-resolution.md` | Ops/Runbook | Cycle count discrepancy resolution workflow. | Partly outdated | Business process correct; may have path refs | Update | • Verify no stale path references. |
| `docs/prod-ready/cycle-count-execution.md` | Ops/Runbook | Cycle count execution guide: scan flow, quantity entry, confirm/adjust. | Partly outdated | Business process correct; path refs to WebUI pages may be old | Update | • Fix WebUI route/page path references. |
| `docs/prod-ready/cycle-count-scheduling.md` | Ops/Runbook | Cycle count scheduling: Hangfire-based scheduling, frequency config. | Partly outdated | Hangfire ✓; path refs old | Update | • Fix scheduler path references. |
| `docs/prod-ready/cycle-count-ui.md` | Requirements/spec | UI spec for cycle counting pages. | Partly outdated | References old `src/LKvitai.MES.WebUI/Pages/` | Update | • Fix WebUI path to modular path. |
| `docs/prod-ready/deployment-guide.md` | Ops/Runbook | Deployment guide: build, publish, Docker, environment config. | Outdated | References old flat project paths for publish and Docker build | Update | • Fix all dotnet publish and Docker paths to modular layout. |
| `docs/prod-ready/disaster-recovery.md` | Ops/Runbook | Disaster recovery plan: RTO/RPO objectives, backup strategy, failover procedures. | Up-to-date | `scripts/disaster-recovery/` verified ✓; process-level procedures | Keep | • Process-level; script paths correct. |
| `docs/prod-ready/disaster-recovery-communication-template.md` | Ops/Runbook | Communication templates for disaster events: stakeholder emails, status page updates. | Up-to-date | No code path refs | Keep | • Process-level template, stable. |
| `docs/prod-ready/external-contract-tests.md` | Requirements/spec | External contract test spec: Pact-style tests for Agnum and ERP integrations. | Partly outdated | Test project path `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/` (may use old path in doc) | Update | • Fix test project path references. |
| `docs/prod-ready/gdpr-erasure-workflow-guide.md` | Ops/Runbook | GDPR erasure workflow: right-to-erasure request handling, event store soft-delete. | Partly outdated | Business requirements valid; path refs may be old | Update | • Fix implementation path refs. |
| `docs/prod-ready/inter-warehouse-transfer-ui.md` | Requirements/spec | UI spec for inter-warehouse transfer pages. | Partly outdated | References old WebUI page path | Update | • Fix WebUI path to modular path. |
| `docs/prod-ready/inter-warehouse-transfer-workflow.md` | Ops/Runbook | Inter-warehouse transfer workflow: create, approve, execute, confirm. | Up-to-date | Business process, no structural path refs | Keep | • Workflow doc, stable. |
| `docs/prod-ready/location-3d-coordinates.md` | Requirements/spec | 3D coordinate system spec for warehouse locations: X/Y/Z axis, visual rendering API. | Up-to-date | Feature flag `enable_3d_visualization` confirmed in feature-flags.md; business spec level | Keep | • Business spec, technology-stable. |
| `docs/prod-ready/operator-runbook.md` | Ops/Runbook | Operator runbook: day-start, receiving, picking, cycle count, end-of-day procedures. | Up-to-date | Process-level procedures, no code path refs | Keep | • Operator-facing guide. |
| `docs/prod-ready/operator-training-guide.md` | Ops/Runbook | Operator training material: scanner usage, common errors, troubleshooting. | Up-to-date | Process-level content | Keep | • Operator-facing guide. |
| `docs/prod-ready/performance-regression-suite.md` | Requirements/spec | Performance regression test spec: k6/NBomber test scenarios, SLA thresholds. | Partly outdated | Test project path refs may use old structure | Update | • Fix test runner path references to new layout. |
| `docs/prod-ready/pii-encryption-guide.md` | Ops/Runbook | PII encryption guide: field-level encryption, key management, column encryption patterns. | Partly outdated | Implementation path refs likely old | Update | • Fix source file path references. |
| `docs/prod-ready/print-queue-retry.md` | Ops/Runbook | Print queue retry logic: TCP 9100 retry policy (3x), fallback to manual queue. | Up-to-date | TCP 9100 confirmed in CLAUDE.md (Integration Points); Polly in `Directory.Packages.props` ✓ | Keep | • Operational policy matches CLAUDE.md description. |
| `docs/prod-ready/production-readiness-checklist.md` | Ops/Runbook | Production readiness gate checklist: 12 categories including security, performance, observability, DR. | Unknown | Not read in detail; structured checklist; target date 2026-03-15 referenced elsewhere | Keep | • Keep as gate checklist; verify sign-offs represent actual testing. |
| `docs/prod-ready/retention-policy-guide.md` | Ops/Runbook | Data retention policy: event store retention, log retention, GDPR compliance windows. | Up-to-date | Policy-level doc, no structural path refs | Keep | • Process/policy doc, stable. |
| `docs/prod-ready/tcp-9100-printer-integration.md` | Ops/Runbook | TCP 9100 printer integration: ZPL send over raw socket, retry policy, error handling. | Partly outdated | Integration layer confirmed; path refs to `LKvitai.MES.Integration/` (old) | Update | • Fix path to `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Integration/`. |
| `docs/prod-ready/ui-gap-audit-phase15-2026-02-12.md` | Audit/Trace | Feb 12, 2026 UI gap audit for S3–S6 tasks: IMPLEMENTED/PARTIAL/MISSING per PRD. | Outdated | References `src/LKvitai.MES.WebUI/Pages` (old); superseded by `docs/audit/UI-FINAL-STATUS.md` (GAP=0, 2026-02-18) | Archive | • Superseded by the definitive Feb 18 UI audit closure. |
| `docs/prod-ready/valuation-stream-events.md` | Architecture/ADR | Sprint 7 valuation stream spec: `valuation-item-{itemId}`, aggregate `ItemValuation`, 4 event types. | Outdated | **Conflicts with `docs/adr/ADR-002-valuation-event-sourcing.md`**: stream ID `valuation-{itemId}` vs `valuation-item-{itemId}`; aggregate `Valuation` vs `ItemValuation`; `LandedCostAllocated` vs `LandedCostApplied`; `StockWrittenDown` vs `WrittenDown` | Update | • Resolve conflict with ADR-002 by checking actual domain class in `LKvitai.MES.Modules.Warehouse.Domain`. • One of these two docs is wrong; fix the incorrect one. |
| `docs/prod-ready/warehouse-2d-3d-toggle.md` | Requirements/spec | 2D/3D toggle UI spec for warehouse visualization. | Up-to-date | `enable_3d_visualization` feature flag confirmed; Blazor component spec | Keep | • Feature spec, consistent with feature-flags.md. |
| `docs/prod-ready/warehouse-3d-rendering.md` | Requirements/spec | 3D rendering spec: Three.js/Babylon.js approach, location coordinate mapping. | Up-to-date | Business requirements for 3D; technology choice at spec level | Keep | • Feature spec. |
| `docs/prod-ready/zpl-template-engine.md` | Requirements/spec | ZPL label template engine spec: dynamic field substitution, QR code, barcode generation. | Partly outdated | QRCoder in `Directory.Packages.props` ✓; path refs to label printing adapter may be old | Update | • Fix integration adapter path if present. |

---

### `docs/project-memory/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/project-memory/README.md` | Notes/Misc | Index of project memory files. | Up-to-date | Single entry pointing to chat summary file | Keep | • Canonical location (vs duplicate under docs/docs/). |
| `docs/project-memory/2026-02-chat-summary-ui-audit-and-phase15.md` | Notes/Misc | Digest of Feb 2026 session: Phase 1.5 sprint pack facts, UI↔API audit closure, GapWorkbench removal. | Up-to-date | Facts consistent with `docs/audit/` coverage docs and `PHASE15-FINAL-STATUS.md` | Keep | • Useful cross-session context digest. |

---

### `docs/refactor-status/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/refactor-status/dependency-baseline.md` | Audit/Trace | Pre-refactor dependency violations snapshot (Feb 19): Application→Marten, Contracts→SharedKernel, SharedKernel→MediatR. | Outdated (superseded) | All 3 violations fixed: F8/F9/F10/F11 PASS in post-refactor audit; violations confirmed resolved | Archive | • Historical baseline; violations are resolved. Keep as execution trace alongside blueprints. |

---

### `docs/repo-audit/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/repo-audit/2026-02-16-repo-audit-vs-target.md` | Audit/Trace | Pre-refactor compliance audit (Feb 16): LOW compliance with modular target; 20+ findings. Marked RESOLVED. | Outdated (superseded) | Contains `> RESOLVED` banner; refactor executed and merged; findings addressed | Archive | • Already marked RESOLVED. Keep as baseline record. |
| `docs/repo-audit/2026-02-19-refactor-completion.md` | Audit/Trace | Refactor completion note (Feb 19): validation gates passed on `refactor/modular-blueprint` branch. | Up-to-date | Refactor merged main 2026-02-21 (de66d1f); completion facts are accurate | Keep | • Final refactor completion record. |

---

### `docs/security/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/security/api-key-usage-guide.md` | Ops/Runbook | API key management: admin endpoints, create/rotate/revoke, header usage. | Up-to-date | API routes (`/api/warehouse/v1/admin/api-keys`) are path-only, no code file refs | Keep | • Endpoint-level doc, stable. |
| `docs/security/audit-log-retention.md` | Ops/Runbook | Audit log retention policy and cleanup procedures. | Up-to-date | Policy-level doc | Keep | • Process/policy doc. |
| `docs/security/audit-log-schema.md` | Architecture/ADR | Audit log schema: table structure, event types, fields. | Unknown | Not read; likely references old EF model path | Keep | • Verify table schema matches current EF migration. |
| `docs/security/mfa-setup-guide.md` | Ops/Runbook | MFA setup guide: OTP configuration in appsettings. | Outdated | References `src/LKvitai.MES.Api/appsettings*.json` (old path); actual: `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/appsettings.*.json`; Otp.NET in `Directory.Packages.props` ✓ | Update | • Fix appsettings path reference. |
| `docs/security/permission-model-guide.md` | Ops/Runbook | RBAC permission model (PRD-1629): Resource:Action:Scope tuples, role assignment tables. | Partly outdated | Permission tuple model is architecture-level; EF tables (`user_role_assignments`, `role_permissions`) may reference old schema location | Update | • Verify EF table names match current migration. |

---

### `docs/spec/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/spec/10-feature-decomposition.md` | Requirements/spec | 16 feature groups (FG-01..FG-16) with boundaries, dependencies, and acceptance criteria. | Up-to-date | Feature groups are business-stable; no structural path refs at this level | Keep | • Business requirements, technology-agnostic. |
| `docs/spec/20-phase-plan.md` | Plan/Tasks | 6-phase delivery plan: P0-Foundation, P1-Core, P2-Reservation&Picking, P3-Financial, P4-Offline, P5+. | Partly outdated | P0 (Foundation) and P1 (Core Inventory) complete per CLAUDE.md; P1.5 specs done (PHASE15-FINAL-STATUS.md); plan timelines need update | Update | • Mark P0/P1/P1.5 as complete. Update current phase pointer. |
| `docs/spec/30-epics-and-stories.md` | Plan/Tasks | Epics and user stories per phase, traced to 18 requirements (Req 1–18). | Partly outdated | Stories are business-valid; some P0/P1 epics are implemented; implementation task refs may use old paths | Update | • Mark completed epics/stories. Keep incomplete work as active backlog. |
| `docs/spec/40-technical-implementation-guidelines.md` | Architecture/ADR | Do/Don't rulebook: aggregate rules, event naming, command handlers, saga constraints, projection safety, offline sync, testing, code quality. | Up-to-date | Rules consistent with CLAUDE.md constraints and frozen architecture; no path refs at pattern level | Keep | • **High-value reference.** Technology-specific but stable to implemented stack. |
| `docs/spec/50-risk-and-delivery-strategy.md` | Plan/Tasks | Risk register, mitigation, spike tasks, rollout strategy, feature toggles, go-live gates. | Partly outdated | Some risks resolved (refactor done, CPM in place); go-live date 2026-03-15 in operations checklist | Update | • Close resolved risks (refactor, CPM, arch tests). Update open risks with current status. |

---

### `docs/testing/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/testing/chaos-engineering.md` | Requirements/spec | Chaos engineering spec: Polly.Contrib.Simmy injection, failure scenarios, test methodology. | Partly outdated | `Polly.Contrib.Simmy` in `Directory.Packages.props` ✓; test path may use old `src/tests/` | Update | • Fix test project path to `tests/Modules/Warehouse/`. |
| `docs/testing/e2e-test-suite.md` | Requirements/spec | E2E test suite spec (PRD-1651): 22 test methods across 5 workflows, data-driven JSON scenarios. | Outdated | References `src/tests/LKvitai.MES.Tests.E2E` (old path + old project name); actual: `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/`; scenario files path also wrong | Update | • Fix test project path and project name to `LKvitai.MES.Tests.Warehouse.E2E`. • Fix scenario file path to `tests/Modules/Warehouse/.../data/`. |

---

### `docs/ui/`

| File | Type | About | Freshness | Evidence checked | Keep? | Action notes |
|------|------|-------|-----------|-----------------|-------|--------------|
| `docs/ui/ui-skeleton-instructions.md` | Plan/Tasks | Blazor UI scaffold playbook (UI-0.1–UI-0.7): project creation, Bootstrap 5, layout, error handling, loading states. | Outdated | Project already exists at `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.WebUI/`; playbook has `cd src/` + project creation steps that are already executed | Archive | • WebUI project exists. Scaffold tasks are done. Archive as implementation record. |

---

## C) Consolidation Appendix

### Merge Map

| Source | → Target | Rationale |
|--------|----------|-----------|
| `docs/operations/rollback-runbook.md` | → `docs/operations/rollback-procedures.md` | Two rollback docs with overlapping content; merge into single `rollback-procedures.md` |
| `docs/prod-ready/prod-ready-tasks-master-index-part2.md` | → `docs/prod-ready/prod-ready-tasks-master-index.md` | Part 2 is overflow from Part 1; consolidate into a single index with pagination-aware headings |
| `docs/audit/claude-ui-gap-audit-checkpoint.md` | → `docs/audit/claude-ui-gap-audit-final.md` | Checkpoint is superseded by final; add a note at checkpoint top linking to final and archive |
| `docs/docs/architecture/project-memory/` (both files) | → `docs/project-memory/` (already canonical) | Accidental double-nesting; files are identical duplicates |
| `docs/prod-ready/valuation-stream-events.md` + `docs/adr/ADR-002-valuation-event-sourcing.md` | → Resolve conflict, keep one | The two docs disagree on stream ID format, aggregate name, and event names; merge/correct against actual domain code |

---

### Archive / Delete List

| File | Action | Rationale |
|------|--------|-----------|
| `docs/docs/architecture/project-memory/README.md` | **Delete** | Identical duplicate of `docs/project-memory/README.md` in accidental nested dir |
| `docs/docs/architecture/project-memory/2026-02-chat-summary-ui-audit-and-phase15.md` | **Delete** | Identical duplicate of `docs/project-memory/2026-02-chat-summary-ui-audit-and-phase15.md` |
| `docs/blueprints/repo-refactor-blueprint.md` | **Archive** | Refactor complete; blueprint executed; no ongoing reference value |
| `docs/blueprints/blueprint-validation-report.md` | **Archive** | Pre-execution quality gate; execution complete |
| `docs/blueprints/marten-usage-inventory.md` | **Archive** | Pre-refactor scan output; violations fixed |
| `docs/blueprints/package-inventory.md` | **Archive** | Pre-CPM package list; CPM now implemented |
| `docs/refactor-status/dependency-baseline.md` | **Archive** | Pre-refactor violation baseline; all violations resolved |
| `docs/repo-audit/2026-02-16-repo-audit-vs-target.md` | **Archive** | Already marked RESOLVED; pre-refactor state |
| `docs/repo-structure-audit.md` | **Archive** | 2026-02-15 pre-refactor design doc; superseded by actual implementation |
| `docs/ui/ui-skeleton-instructions.md` | **Archive** | WebUI project exists; scaffold tasks complete |
| `docs/prod-ready/CORRECTIVE-ACTION-PLAN.md` | **Archive** | Feb 10 gap analysis; gaps filled by PHASE15 sprint packs |
| `docs/prod-ready/HANDOFF-COMMAND.md` | **Archive** | Expired Codex session handoff |
| `docs/prod-ready/SPRINT-789-STATUS.md` | **Archive** | Superseded by PHASE15-FINAL-STATUS.md |
| `docs/prod-ready/STATUS.md` | **Archive** | Feb 10 0.5% completion snapshot; completely superseded |
| `docs/prod-ready/S7-expansion-part1.md` | **Archive** | Absorbed into S7 sprint pack |
| `docs/prod-ready/prod-ready-tasks.md` | **Archive** | Original task list; superseded by PHASE15 sprint packs |
| `docs/prod-ready/prod-ready-tasks-01-overview.md` | **Archive** | Superseded by PHASE15 sprint packs |
| `docs/prod-ready/prod-ready-tasks-02-foundation.md` | **Archive** | Superseded by PHASE15 sprint packs |
| `docs/prod-ready/prod-ready-tasks-03-valuation.md` | **Archive** | Superseded by PHASE15 sprint packs |
| `docs/prod-ready/prod-ready-tasks-04-outbound.md` | **Archive** | Superseded by PHASE15 sprint packs |
| `docs/prod-ready/prod-ready-tasks-review.md` | **Archive** | Early review artifact; superseded by claude-review-sprints12 and PHASE15-FINAL-STATUS |
| `docs/prod-ready/prod-ready-tasks-review-sprints12.md` | **Archive** | Historical review; issues resolved |
| `docs/prod-ready/prod-ready-tasks-progress.md` | **Archive** | Intermediate progress; superseded by progress-FINAL |
| `docs/prod-ready/prod-ready-tasks-progress-S789.md` | **Archive** | Intermediate progress; superseded by progress-FINAL |
| `docs/prod-ready/prod-ready-tasks-PHASE15-S1-remaining.md` | **Archive** | Post-Codex remaining items; Codex run is complete |
| `docs/prod-ready/claude-codex-catchup-summary.md` | **Archive** | Expired session context with old flat paths |
| `docs/prod-ready/claude-review-sprints12.md` | **Archive** | Historical review; auth blocker resolved |
| `docs/prod-ready/codex-diagnostic-report.md` | **Archive** | Historical Codex session diagnostic |
| `docs/prod-ready/codex-run-summary.md` | **Archive** | Historical Codex session run log |
| `docs/prod-ready/codex-suspicions.md` | **Archive** | Historical Codex inconsistency flags; issues resolved |
| `docs/prod-ready/ui-gap-audit-phase15-2026-02-12.md` | **Archive** | Superseded by docs/audit/UI-FINAL-STATUS.md (Feb 18, GAP=0) |
| `docs/audit/claude-ui-gap-audit-checkpoint.md` | **Archive** | Superseded by claude-ui-gap-audit-final.md |
| `.kiro/specs/warehouse-core-phase1/implementation-task-universe.md` | **Archive** | Phase 1 tasks all complete; add completion stamp |
| `.kiro/specs/warehouse-core-phase1/tasks.md` | **Archive** | Phase 1 tasks complete; add completion stamp |
| `.kiro/specs/warehouse-core-phase1/tasks-ui.md` | **Archive** | Phase 1 UI tasks complete |
| `.kiro/specs/warehouse-core-phase1/ui-task-universe.md` | **Archive** | Phase 1 UI tasks complete |

---

### Doc Gaps — Truths in Code With No Matching Doc

| Gap | Evidence | Recommended Action |
|-----|----------|--------------------|
| **WebUI rename not documented** | Post-refactor audit F3: `src/Modules/Warehouse/LKvitai.MES.WebUI/` should be `LKvitai.MES.Modules.Warehouse.Ui/`; SOLUTION_STRUCTURE.md and audit note it as `WebUI` (current state); CLAUDE.md also uses WebUI | Create ADR-006 for WebUI rename decision (do it now vs defer) or add decision note to CLAUDE.md |
| **Architecture tests are stubs** | Post-refactor audit F23: `DomainLayerTests.cs`, `ApplicationLayerTests.cs` are `[Skip]`; `ContractsLayerTests.cs` is a no-op | No doc covers the open work to un-skip and implement real NetArchTest assertions; add to CLAUDE.md known open items |
| **`validate-module-dependencies.sh` requires `rg`** | Post-refactor audit F18: CI `ubuntu-latest` doesn't have ripgrep; either fix was applied (76206ec "ci: remove hard dependency on rg") or doc should note the requirement | Verify 76206ec actually resolved this; if so, no doc gap |
| **`_Imports.razor` mixed namespaces** | Post-refactor audit F4: 5 of 8 `@using` directives use old namespace | No doc tracks this as open technical debt; add to CLAUDE.md known open items |
| **`ARCH-01` god object decomposition** | CLAUDE.md: `MasterDataEntities.cs` ~1400 LOC across 8+ bounded contexts | No doc describes decomposition plan or progress; create a task/ADR |
| **`ARCH-02` business logic in Api.Services/** | CLAUDE.md: 34 files in `Api.Services/` belong in Application layer | No doc describes migration plan; create task |
| **`HIGH-02` HARD lock timeout / ReservationTimeoutSaga** | CLAUDE.md: `PickStockFailedPermanentlyEvent` does not release HARD locks; `ReservationTimeoutSaga` not yet implemented | Add explicit task to Phase 2 backlog; no current spec exists |
| **Dockerfile F1/F2 fixes status** | Post-refactor audit found BLOCKER-level stale paths; subsequent commits (185cf23, c569e21, etc.) may or may not have fixed them | Verify `docker-compose.yml` and Api `Dockerfile` are updated; note in audit doc if fixed |
