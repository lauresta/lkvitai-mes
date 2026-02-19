# Blueprint Validation Report — Recheck v1.1

**Blueprint:** `docs/blueprints/repo-refactor-blueprint.md` v1.1
**Auditor:** Claude Code
**Date:** 2026-02-19
**Repo state:** `main` branch
**Previous report:** v1.0 (same date) — 3 blockers, 7 issues
**This report:** Recheck after v1.1 fixes applied

---

## A) Verdict: PASS — 3 remaining issues (0 blockers)

All 3 original blockers are **FIXED**. All 7 original issues are **FIXED**. The blueprint is mechanically executable by Codex.

**3 new issues found** during recheck — all HIGH severity, all in P4.S0.T1 scope expansion. No new blockers.

---

## B) Original Issues — Fix Verification

### Blockers (all 3 FIXED)

| # | Original Issue | Fix Applied | Verified |
|---|---------------|-------------|----------|
| 1 | Marten event store breaks on namespace rename | P4.S0.T1 added: `MapEventType` aliases before any rename, STOP-gate | YES — lines 733-753 |
| 2 | EF migration files embed old namespaces | P4.S4.T3 updated: explicitly includes 60+ migration files | YES — lines 944-961 |
| 3 | Blazor `.razor` files hardcode old namespaces | P4.S6.T2 updated: includes `_Imports.razor` + 20+ `.razor` files | YES — lines 1044-1054 |

### Issues #4-#10 (all 7 FIXED)

| # | Original Issue | Fix Applied | Verified |
|---|---------------|-------------|----------|
| 4 | `.sln` test path confusion | Already correct in v1.0 (note only) | N/A |
| 5 | `Directory.Packages.props` location | P0.S4.T2 says "repo root"; target state diagram updated | YES — line 282 |
| 6 | `build-and-push.yml` not updated after Phase 3 | P3.S2.T3 and P3.S2.T4 now include `build-and-push.yml` update | YES — changelog items 19-20 |
| 7 | `e2e-tests.yml` not updated after test move | P0.S3.T2 scope now includes `e2e-tests.yml` | YES — line 246 |
| 8 | Phase 2 Marten removal underestimates scope | P2.S0.T1 added: inventory grep + STOP if >10 files | YES — changelog item 21 |
| 9 | `docker-compose.test.yml` move backwards | P1.S1.T2 now keeps it at repo root (validation only) | YES — changelog item 22 |
| 10 | Phase 4 `build-and-push.yml` second update | P4.S6.T3 now includes matrix path update | YES — changelog item 23 |

---

## C) New Issues Found (Recheck)

### 1. HIGH — P4.S0.T1 scope too narrow: only covers event types, misses Marten document types

- **Location:** P4.S0.T1 (line 737-753)
- **What P4.S0.T1 says:** "List all event types from `src/LKvitai.MES.Contracts/Events/` directory, add mapping for each"
- **What's missing:** Marten stores FQ CLR type names not only in `mt_events.mt_dotnet_type` but also in `mt_doc_*` document tables for:
  - **Saga state classes (3):** `PickStockSagaState`, `ReceiveGoodsSagaState`, `AgnumExportSagaState` — all persisted via `.MartenRepository()` in MassTransit config. Stored in `warehouse_events` schema as Marten documents with FQ type names.
  - **Inline snapshot aggregates (2):** `Valuation` (Domain), `ItemValuation` (Domain) — registered as `Snapshot<T>(SnapshotLifecycle.Inline)` in MartenConfiguration.cs:48-49. Stored in `mt_doc_valuation` / `mt_doc_itemvaluation` with FQ type names in document metadata.
  - **Projection document types (8):** All 8 projections in `ProjectionRegistration.cs` store their read-model documents in Marten `mt_doc_*` tables with FQ type names.
- **Impact:** After namespace renames in P4.S5.T1 (Projections), P4.S5.T2 (Sagas), and P4.S1.T3 (Domain), Marten cannot deserialize existing saga states, snapshots, or projection documents.
- **Fix:** Expand P4.S0.T1 to register `MapDocumentType` (or Marten's equivalent document type aliasing) for all 13 Marten-persisted document types:
  - 3 saga state classes (namespace `LKvitai.MES.Sagas` → `LKvitai.MES.Modules.Warehouse.Sagas`)
  - 2 snapshot aggregates (namespace `LKvitai.MES.Domain.Aggregates` → `LKvitai.MES.Modules.Warehouse.Domain.Aggregates`)
  - 8 projection read models (namespace `LKvitai.MES.Projections` → `LKvitai.MES.Modules.Warehouse.Projections`)
- **Alternative fix:** Add SQL migration to update `mt_doc_type` column in each `mt_doc_*` table after namespace rename. This is simpler than Marten-level aliasing for documents.

### 2. HIGH — P4.S0.T1 event type count is 46, not "list from directory"

- **Location:** P4.S0.T1 (line 744)
- **What P4.S0.T1 says:** "List all event types from `src/LKvitai.MES.Contracts/Events/` directory"
- **Actual inventory (46 event classes in 10 files):**

  | File | Event Types | Count |
  |------|-------------|-------|
  | `ValuationEvents.cs` | `ValuationInitialized`, `CostAdjusted`, `LandedCostAllocated`, `StockWrittenDown`, `LandedCostApplied`, `WrittenDown` | 6 |
  | `HandlingUnitEvents.cs` | `HandlingUnitCreatedEvent`, `LineAddedToHandlingUnitEvent`, `LineRemovedFromHandlingUnitEvent`, `HandlingUnitSealedEvent`, `HandlingUnitSplitEvent`, `HandlingUnitMergedEvent` | 6 |
  | `ReservationEvents.cs` | `ReservationCreatedEvent`, `StockAllocatedEvent`, `PickingStartedEvent`, `ReservationConsumedEvent`, `ReservationCancelledEvent`, `ReservationBumpedEvent` | 6 |
  | `StockMovedEvent.cs` | `StockMovedV1Event`, `StockMovedEvent` | 2 |
  | `SalesOrderEvents.cs` | `SalesOrderCreatedEvent`, `SalesOrderAllocatedEvent`, `SalesOrderReleasedEvent`, `SalesOrderCancelledEvent` | 4 |
  | `TransferOperationalEvents.cs` | `TransferCreatedEvent`, `TransferApprovedEvent`, `TransferExecutedEvent`, `TransferCompletedEvent` | 4 |
  | `CycleCountOperationalEvents.cs` | `CycleCountScheduledEvent`, `CountRecordedEvent`, `CycleCountCompletedEvent` | 3 |
  | `OutboundOperationalEvents.cs` | `OutboundOrderCreatedEvent`, `ShipmentPackedEvent`, `ShipmentDispatchedEvent` | 3 |
  | `AgnumExportEvents.cs` | `AgnumExportStartedEvent`, `AgnumExportCompletedEvent`, `AgnumExportFailedEvent` | 3 |
  | `MasterDataOperationalEvents.cs` | `WarehouseOperationalEvent` (abstract), `InboundShipmentCreatedEvent`, `GoodsReceivedEvent`, `PickCompletedEvent`, `StockAdjustedEvent`, `ReservationCreatedMasterDataEvent`, `ReservationReleasedMasterDataEvent`, `QCPassedEvent`, `QCFailedEvent` | 9 |
  | **TOTAL** | | **46** |

- **Risk:** Codex "listing from directory" is correct behavior, but the task should call out that abstract base class `WarehouseOperationalEvent` should be skipped (or mapped), and legacy `StockMovedV1Event` needs special handling (it already has an upcaster).
- **Fix:** Add explicit note in P4.S0.T1: "Expected: ~46 event types. Skip abstract base classes. For `StockMovedV1Event`, the existing upcaster already maps it — verify the upcaster chain works with the new namespace."

### 3. HIGH — No validation step for Marten document types after namespace renames in P4.S5

- **Location:** Phase 4, after P4.S5.T1-T3 (Projections, Sagas, Integration renames)
- **What's missing:** P4.S0.T1 has a STOP condition for event type aliases. But there is no equivalent STOP condition or validation for Marten document types (saga states, projection documents, snapshots) after their namespaces change in P4.S1 (Domain snapshots), P4.S5.T1 (projection docs), and P4.S5.T2 (saga states).
- **Fix:** Add validation command after P4.S5.T2:
  ```bash
  # Verify Marten can still load saga states and projection documents
  dotnet test --filter "FullyQualifiedName~Marten OR FullyQualifiedName~Saga OR FullyQualifiedName~Projection"
  ```
  Add STOP condition: "If saga or projection integration tests fail, STOP and report."

---

## D) Updated Namespace Handling Verdict

### Verdict: **Namespace handling is now ADEQUATE — with the 3 new issues above addressed**

The v1.1 blueprint correctly handles:
- Event type aliasing via `MapEventType` (P4.S0.T1)
- EF migration namespace updates (P4.S4.T3)
- Blazor `.razor` file `@using` updates (P4.S6.T2)
- Phase 3 moves NOT requiring namespace changes (directory names preserved)

**Updated namespace-sensitive patterns table:**

| Pattern | Location | Handled in Blueprint? | Notes |
|---------|----------|-----------------------|-------|
| MediatR assembly scanning | `MediatRConfiguration.cs` | N/A (safe) | Uses `typeof(T).Assembly` |
| MassTransit consumer registration | `MassTransitConfiguration.cs` | N/A (safe) | Uses `AddConsumer<T>()` |
| Marten event types (46) | `MartenConfiguration.cs` | **YES** (P4.S0.T1) | `MapEventType` aliases |
| Marten snapshot types (2) | `MartenConfiguration.cs:48-49` | **NO** | Valuation, ItemValuation — need doc type mapping |
| Marten saga state types (3) | `MassTransitConfiguration.cs` | **NO** | `.MartenRepository()` stores FQ type name |
| Marten projection doc types (8) | `ProjectionRegistration.cs` | **NO** | `mt_doc_*` tables use FQ type name |
| Marten event upcasting | `MartenConfiguration.cs:61-62` | N/A (safe) | Uses generic types |
| EF Core migrations (60+) | `Infrastructure/Persistence/Migrations/*` | **YES** (P4.S4.T3) | Bulk namespace update |
| Blazor `_Imports.razor` | `WebUI/_Imports.razor` | **YES** (P4.S6.T2) | 8 `@using` directives |
| Blazor page `@using` | 20+ `.razor` files | **YES** (P4.S6.T2) | Explicitly called out |
| Controller routes | All controllers | N/A (safe) | String literal routes |
| CI test filters | `deploy.yml` | N/A (safe) | Class names only |
| `InternalsVisibleTo` | (none) | N/A | Not used |
| Reflection / `Type.GetType()` | (none with strings) | N/A (safe) | Runtime type refs only |

---

## E) Quick Sanity Checklist (updated)

### Phase 0 (Safety Net) — unchanged
```bash
# After P0.S1.T1 (test move):
dotnet restore && dotnet build && dotnet test
grep -r "src/tests" .github/workflows/  # must return 0 matches

# After P0.S4.T4 (central packages):
dotnet restore && dotnet build && dotnet test
grep -rn "Version=" src/**/*.csproj tests/**/*.csproj  # should show 0 PackageRef versions
```

### Phase 1 (Pilot slice) — unchanged
```bash
# After P1.S2.T1 (Domain move):
dotnet restore && dotnet build
ls src/Modules/Warehouse/LKvitai.MES.Domain/  # must exist
ls src/LKvitai.MES.Domain/  # must NOT exist

# After P1.S2.T2 (Dockerfile):
docker build -f src/LKvitai.MES.Api/Dockerfile .
```

### Phase 3 (All moves) — unchanged
```bash
# After P3.S2.T5:
dotnet restore && dotnet build && dotnet test
ls src/Modules/Warehouse/  # should list all 10 projects
ls src/LKvitai.MES.*/  # should return "No such file" for all
docker build -f src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.WebUI/Dockerfile .
grep -r "src/LKvitai.MES\." .github/workflows/  # must return 0 matches
```

### Phase 4 (Renames) — UPDATED
```bash
# BEFORE any rename (P4.S0.T1):
# Verify Marten event type aliases registered (46 event types)
# Verify Marten document type mapping for saga states (3), snapshots (2), projections (8)
dotnet test --filter "FullyQualifiedName~Marten"

# After EACH P4.S*.T3:
dotnet restore && dotnet build && dotnet test
grep -rn "namespace LKvitai\.MES\.<OldName>" src/ tests/  # must return 0

# After P4.S5.T2 (Sagas rename) — NEW:
dotnet test --filter "FullyQualifiedName~Saga"  # saga state deserialization must work

# After P4.S6.T3 (final Dockerfiles):
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile .
```

### Phase 5 (Test renames) — unchanged
```bash
# After P5.S2.T4:
dotnet test
grep -r "LKvitai.MES.Tests\.\(Unit\|Integration\|Property\|E2E\)" .github/workflows/  # must return 0
```

### Final validation — unchanged
```bash
dotnet restore && dotnet build && dotnet test
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile .
./scripts/validate-module-dependencies.sh
dotnet test tests/ArchitectureTests/
grep -rn "namespace LKvitai\.MES\.\(Domain\|Application\|Infrastructure\|Contracts\|Projections\|Sagas\|Integration\|Api\|WebUI\|SharedKernel\)[^.]" src/ tests/
```

---

## F) Appendix: Discovery Evidence

### Marten-persisted types — complete inventory

**Event types (46):** See table in Issue #2 above.

**Inline snapshot aggregates (2):**
- `Valuation` — `src/LKvitai.MES.Domain/Aggregates/Valuation.cs` → stored as `mt_doc_valuation`
- `ItemValuation` — `src/LKvitai.MES.Domain/Aggregates/ItemValuation.cs` → stored as `mt_doc_itemvaluation`

**Saga state classes (3):** All use `.MartenRepository()` for Marten persistence.
- `PickStockSagaState` — `src/LKvitai.MES.Sagas/PickStockSaga.cs`
- `ReceiveGoodsSagaState` — `src/LKvitai.MES.Sagas/ReceiveGoodsSaga.cs`
- `AgnumExportSagaState` — `src/LKvitai.MES.Sagas/AgnumExportSaga.cs`

**Projection document types (8):** Registered in `src/LKvitai.MES.Projections/ProjectionRegistration.cs`.
- `ActiveHardLocksProjection` (Inline)
- `LocationBalanceProjection` (Async)
- `AvailableStockProjection` (Async)
- `HandlingUnitProjection` (Async)
- `ReservationSummaryProjection` (Async)
- `ActiveReservationsProjection` (Async)
- `InboundShipmentSummaryProjection` (Async)
- `AdjustmentHistoryProjection` (Async)

### MartenConfiguration.cs — current registrations
- Only 6 of 46 event types explicitly registered via `AddEventType<T>()`
- Remaining 40 are implicitly discovered by Marten at runtime
- `MapEventType<T>()` must map ALL 46 types (explicit + implicit) since Marten stores FQ type names regardless of registration method

### .sln paths (current state)
- Source projects: `LKvitai.MES.Api\LKvitai.MES.Api.csproj` (relative to `src/`)
- Test projects: `tests\LKvitai.MES.Tests.Unit\LKvitai.MES.Tests.Unit.csproj` (relative to `src/`)
- Tests E2E project exists: `tests\LKvitai.MES.Tests.E2E\LKvitai.MES.Tests.E2E.csproj` (in sln)

### CI workflows hardcoded paths
- `build-and-push.yml`: `src/LKvitai.MES.Api/Dockerfile`, `src/LKvitai.MES.WebUI/Dockerfile`
- `deploy.yml`: `src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj`
- `deploy-test.yml`: `docker-compose.test.yml` (root), `src/LKvitai.MES.Infrastructure`, `src/LKvitai.MES.Api`
- `e2e-tests.yml`: `src/tests/LKvitai.MES.Tests.E2E/LKvitai.MES.Tests.E2E.csproj`

### EF migrations
- 60+ migration files in `src/LKvitai.MES.Infrastructure/Persistence/Migrations/`
- All embed `namespace LKvitai.MES.Infrastructure.Persistence.Migrations`

### Blazor namespace usage
- `_Imports.razor`: 8 `@using LKvitai.MES.WebUI.*` directives
- 20+ `.razor` files with additional `@using` directives
