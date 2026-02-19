# Blueprint Validation Report — Recheck #2

**Blueprint:** `docs/blueprints/repo-refactor-blueprint.md` v1.1 (with recheck fixes 27-31)
**Auditor:** Claude Code
**Date:** 2026-02-19
**Repo state:** `main` branch
**History:** v1.0 → 3 blockers, 7 issues → all fixed → Recheck #1 → 3 HIGH issues → all fixed → **this recheck**

---

## A) Verdict: PASS — 1 LOW issue remaining (0 blockers, 0 HIGH)

All 3 original blockers: **FIXED** (v1.1 changelog items 14-16).
All 7 original issues: **FIXED** (v1.1 changelog items 17-23).
All 3 recheck #1 issues: **FIXED** (v1.1 changelog items 27-29).

**1 new LOW issue found:** Phase header task counts are inconsistent with actual task listings.

The blueprint is **mechanically executable by Codex** without modifications.

---

## B) Recheck #1 Issues — Fix Verification

| # | Issue | Fix Applied | Verified |
|---|-------|-------------|----------|
| R1 | P4.S0.T1 scope too narrow (event types only, missed 13 doc types) | P4.S0.T1 expanded: now covers 46 event types + 3 saga states + 2 snapshots + 8 projection docs (line 744-756) | **YES** |
| R2 | Event type count not specified, abstract class/upcaster handling | P4.S0.T1 now says "Expected: ~46 event types across 10 files. Skip abstract base class `WarehouseOperationalEvent`. For `StockMovedV1Event`, verify existing upcaster chain" (line 749) | **YES** |
| R3 | No validation step for Marten doc types after P4.S5 renames | P4.S5.T4 added: validation-only task with `dotnet test --filter` for Marten/Saga/Projection tests + STOP condition (lines 1034-1049) | **YES** |

### Verification details for P4.S0.T1:
- Title changed to "Register Marten type aliases (events + documents)" ✓
- Purpose updated to cover "events and documents" ✓
- Operations now list document types (13 total) with specific class names and namespaces ✓
- Offers both Marten `DocumentAlias` API and SQL migration as alternatives ✓
- Commands now include `dotnet test --filter "FullyQualifiedName~Saga"` ✓
- DoD covers "events and documents can round-trip" + "saga tests pass" ✓
- STOP condition covers both integration and saga test failures ✓
- Critical pre-requisite text updated to include "AND document type aliases" ✓

### Verification details for P4.S5.T4:
- Positioned correctly: after P4.S5.T3 (Integration rename), before Stage 4.6 ✓
- Purpose: "Verify saga states, snapshots, and projection documents still deserialize correctly" ✓
- Operations: validation only, no code changes ✓
- Commands: `dotnet test --filter "FullyQualifiedName~Marten OR FullyQualifiedName~Saga OR FullyQualifiedName~Projection"` ✓
- STOP condition: "If saga or projection integration tests fail, STOP and report. Document type aliasing from P4.S0.T1 may need adjustment." ✓

---

## C) New Issue Found (Recheck #2)

### 1. LOW — Phase header task counts don't match actual task listings

- **Location:** Phase headers (lines 157, 356, 1119) and total count (line 24, 46)
- **Actual vs declared counts:**

  | Phase | Actual Tasks | Header Count | Delta |
  |-------|-------------|-------------|-------|
  | 0 | 11 (S1:3 + S2:1 + S3:2 + S4:4 + S5:1) | 10 | +1 |
  | 1 | 5 (S1:2 + S2:3) | 6 | -1 |
  | 2 | 7 | 7 | ✓ |
  | 3 | 9 | 9 | ✓ |
  | 4 | 21 | 21 | ✓ |
  | 5 | 6 (S1:2 + S2:4) | 8 | -2 |
  | 6 | 3 | 3 | ✓ |
  | 7 | 5 | 5 | ✓ |
  | 8 | 4 | 4 | ✓ |
  | **Total** | **71** | **73** | **-2** |
  | Stages | 28 | 28 | ✓ |

- **Impact:** LOW — Codex reads individual task IDs (P0.S1.T1 etc.), not header counts. No operational impact.
- **Fix:** Update header counts: Phase 0 → 11, Phase 1 → 5, Phase 5 → 6, Total → 71 tasks.

---

## D) Updated Namespace Handling Verdict

### Verdict: **COMPLETE — all namespace-sensitive patterns are now handled**

| Pattern | Location | Handled? | How |
|---------|----------|----------|-----|
| MediatR assembly scanning | `MediatRConfiguration.cs` | N/A (safe) | `typeof(T).Assembly` |
| MassTransit consumer registration | `MassTransitConfiguration.cs` | N/A (safe) | `AddConsumer<T>()` |
| Marten event types (46) | `MartenConfiguration.cs` | **YES** | P4.S0.T1 `MapEventType` aliases |
| Marten snapshot types (2) | `MartenConfiguration.cs:48-49` | **YES** | P4.S0.T1 `DocumentAlias` or SQL migration |
| Marten saga state types (3) | `MassTransitConfiguration.cs` | **YES** | P4.S0.T1 `DocumentAlias` or SQL migration |
| Marten projection doc types (8) | `ProjectionRegistration.cs` | **YES** | P4.S0.T1 `DocumentAlias` or SQL migration |
| Marten event upcasting | `MartenConfiguration.cs:61-62` | N/A (safe) | Uses generic types |
| EF Core migrations (60+) | `Infrastructure/Persistence/Migrations/*` | **YES** | P4.S4.T3 bulk namespace update |
| Blazor `_Imports.razor` | `WebUI/_Imports.razor` | **YES** | P4.S6.T2 `@using` updates |
| Blazor page `@using` | 20+ `.razor` files | **YES** | P4.S6.T2 explicitly called out |
| Controller routes | All controllers | N/A (safe) | String literal routes |
| CI test filters | `deploy.yml` | N/A (safe) | Class names only |
| `InternalsVisibleTo` | (none) | N/A | Not used |
| Reflection / `Type.GetType()` | (none with strings) | N/A (safe) | Runtime type refs only |

---

## E) Quick Sanity Checklist (unchanged from Recheck #1)

### Phase 0 (Safety Net)
```bash
dotnet restore && dotnet build && dotnet test
grep -r "src/tests" .github/workflows/  # must return 0
grep -rn "Version=" src/**/*.csproj tests/**/*.csproj  # 0 PackageRef versions
```

### Phase 1 (Pilot slice)
```bash
dotnet restore && dotnet build
ls src/Modules/Warehouse/LKvitai.MES.Domain/  # must exist
docker build -f src/LKvitai.MES.Api/Dockerfile .
```

### Phase 3 (All moves)
```bash
dotnet restore && dotnet build && dotnet test
ls src/Modules/Warehouse/  # all 10 projects
grep -r "src/LKvitai.MES\." .github/workflows/  # 0 matches
docker build -f src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.WebUI/Dockerfile .
```

### Phase 4 (Renames)
```bash
# P4.S0.T1 — BEFORE any rename:
dotnet test --filter "FullyQualifiedName~Marten"
dotnet test --filter "FullyQualifiedName~Saga"

# After EACH P4.S*.T3:
dotnet restore && dotnet build && dotnet test
grep -rn "namespace LKvitai\.MES\.<OldName>" src/ tests/  # 0 matches

# P4.S5.T4 — After Projections/Sagas/Integration renames:
dotnet test --filter "FullyQualifiedName~Marten OR FullyQualifiedName~Saga OR FullyQualifiedName~Projection"

# After P4.S6.T3 (final Dockerfiles):
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile .
```

### Phase 5 (Test renames)
```bash
dotnet test
grep -r "LKvitai.MES.Tests\.\(Unit\|Integration\|Property\|E2E\)" .github/workflows/  # 0 matches
```

### Final validation
```bash
dotnet restore && dotnet build && dotnet test
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile .
./scripts/validate-module-dependencies.sh
dotnet test tests/ArchitectureTests/
grep -rn "namespace LKvitai\.MES\.\(Domain\|Application\|Infrastructure\|Contracts\|Projections\|Sagas\|Integration\|Api\|WebUI\|SharedKernel\)[^.]" src/ tests/
```

---

## F) Cumulative Issue Tracker

| Round | Issue | Severity | Status |
|-------|-------|----------|--------|
| v1.0 #1 | Marten event store breaks on namespace rename | BLOCKER | FIXED (P4.S0.T1) |
| v1.0 #2 | EF migration files embed old namespaces | BLOCKER | FIXED (P4.S4.T3) |
| v1.0 #3 | Blazor `.razor` files hardcode old namespaces | BLOCKER | FIXED (P4.S6.T2) |
| v1.0 #4 | `.sln` test path confusion | HIGH (note) | N/A (correct) |
| v1.0 #5 | `Directory.Packages.props` location | HIGH | FIXED (P0.S4.T2) |
| v1.0 #6 | `build-and-push.yml` not updated after Phase 3 | HIGH | FIXED (P3.S2.T3/T4) |
| v1.0 #7 | `e2e-tests.yml` not updated after test move | HIGH | FIXED (P0.S3.T2) |
| v1.0 #8 | Phase 2 Marten removal underestimates scope | MEDIUM | FIXED (P2.S0.T1) |
| v1.0 #9 | `docker-compose.test.yml` move backwards | MEDIUM | FIXED (P1.S1.T2) |
| v1.0 #10 | Phase 4 `build-and-push.yml` second update | MEDIUM | FIXED (P4.S6.T3) |
| Recheck #1-1 | P4.S0.T1 misses 13 Marten document types | HIGH | FIXED (P4.S0.T1 expanded) |
| Recheck #1-2 | Event type count not specified | HIGH | FIXED (P4.S0.T1 guidance) |
| Recheck #1-3 | No validation for doc types after P4.S5 | HIGH | FIXED (P4.S5.T4 added) |
| Recheck #2-1 | Phase header task counts inconsistent | LOW | OPEN |
