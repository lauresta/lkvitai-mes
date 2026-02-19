# Blueprint Validation Report

**Blueprint:** `docs/blueprints/repo-refactor-blueprint.md` v1.1
**Auditor:** Claude Code
**Date:** 2026-02-19
**Repo state:** `main` branch

---

## A) Verdict: PASS_WITH_FIXES

The blueprint is mechanically executable but has **3 blockers** (will cause data loss or silent corruption if not addressed) and **7 significant issues** (will cause build breaks or require manual intervention).

---

## B) Top 10 Issues (by severity)

### 1. BLOCKER — Marten event store will break on namespace rename (Phase 4)

- **Location:** Phase 4 (all namespace rename tasks: P4.S1.T3, P4.S2.T3, etc.)
- **Why it breaks:** Marten stores the fully-qualified CLR type name in the `mt_dotnet_type` column of the `mt_events` table (e.g., `LKvitai.MES.Contracts.Events.ValuationInitialized, LKvitai.MES.Contracts`). No `MapEventType()` aliases are configured — only `AddEventType<T>()` is used (`src/LKvitai.MES.Infrastructure/Persistence/MartenConfiguration.cs:41-46`). After renaming `LKvitai.MES.Contracts.Events` → `LKvitai.MES.Modules.Warehouse.Contracts.Events`, Marten cannot deserialize any existing events.
- **Evidence:** `MartenConfiguration.cs:41-46` uses `AddEventType<T>()` without string aliases. No `MapEventType()` or `UseOptimizedArtifactWorkflow()` present.
- **Fix:**
  - Add a new task **P4.S0.T1** (before ANY namespace rename): register explicit Marten event type aliases for every event type using `options.Events.MapEventType<T>("old.qualified.name")`. This preserves backward compatibility.
  - OR: Add a SQL migration to update `mt_dotnet_type` in `warehouse_events.mt_events` after namespace rename.
  - Blueprint must make this a STOP-gate: if Marten aliases are not confirmed, do NOT proceed with namespace renames.

### 2. BLOCKER — EF Core migration files embed old namespaces (Phase 4)

- **Location:** Phase 4, P4.S4.T3 (Infrastructure namespace rename)
- **Why it breaks:** 60+ migration files under `src/LKvitai.MES.Infrastructure/Persistence/Migrations/` contain hardcoded `namespace LKvitai.MES.Infrastructure.Persistence.Migrations` and `using LKvitai.MES.Infrastructure.Persistence`. The `WarehouseDbContextModelSnapshot.cs` references `typeof(WarehouseDbContext)` via fully-qualified namespace. When the Infrastructure namespace changes to `LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations`, the `__EFMigrationsHistory` table's `MigrationId` values won't match the new type names.
- **Evidence:** `src/LKvitai.MES.Infrastructure/Persistence/Migrations/WarehouseDbContextModelSnapshot.cs:3,11` — `using LKvitai.MES.Infrastructure.Persistence; namespace LKvitai.MES.Infrastructure.Persistence.Migrations`
- **Fix:**
  - Add explicit task in Phase 4: after Infrastructure namespace rename, update ALL migration `.cs` and `.Designer.cs` files' namespaces. The `MigrationId` column in `__EFMigrationsHistory` uses the migration class name (not full namespace), so the table itself is safe — but the C# files must compile.
  - Alternatively: set `<RootNamespace>` in csproj but do NOT rename migration file namespaces (keep them as-is). This is messy but avoids the issue.
  - **Recommended:** Add task to explicitly handle migration namespaces — either bulk-rename them or pin them via `[Migration("old-id")]` attribute.

### 3. BLOCKER — Blazor `_Imports.razor` and 20+ `.razor` files hardcode old namespaces (Phase 4)

- **Location:** Phase 4, P4.S6.T2 (WebUI → Ui rename)
- **Why it breaks:** `src/LKvitai.MES.WebUI/_Imports.razor` has 8 `@using LKvitai.MES.WebUI.*` directives. 20+ individual `.razor` files have additional `@using LKvitai.MES.WebUI.*` directives. Blueprint's P4.S6.T2 says "Update namespaces" but does NOT mention `.razor` files — only `.cs` files.
- **Evidence:** `src/LKvitai.MES.WebUI/_Imports.razor` lines 8-15; 20+ pages with `@using LKvitai.MES.WebUI.*`
- **Fix:**
  - P4.S6.T2 Operations must explicitly include: "Update `@using` directives in `_Imports.razor` and all `.razor` files: `LKvitai.MES.WebUI` → `LKvitai.MES.Modules.Warehouse.Ui`"
  - Add a grep validation: `grep -r "LKvitai.MES.WebUI" src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/` must return zero matches.

### 4. HIGH — `.sln` test paths are already `tests\` (not `src\tests\`), so P0.S1.T1 will double-move

- **Location:** Phase 0, P0.S1.T1 (Move tests directory)
- **Why it breaks:** The `.sln` file already uses `tests\LKvitai.MES.Tests.Unit\LKvitai.MES.Tests.Unit.csproj` (line 28 of sln). These paths are relative to `src/` (where the `.sln` lives). The actual files are at `src/tests/`. Blueprint says to `git mv src/tests tests` and then update sln paths from `tests\` to `..\tests\`. But the sln already says `tests\`, which resolves to `src/tests/` — after the move, the files will be at repo-root `tests/`, so sln must change to `..\tests\`. **The blueprint's instruction is correct**, but the description is misleading: it says "change `tests\LKvitai.MES.Tests.*` to `..\tests\LKvitai.MES.Tests.*`" which is accurate.
- **Actual risk:** LOW — the blueprint got this right. Marking as "note" rather than "fix needed."
- **Fix:** No fix needed, just confirm Codex understands the sln paths are relative to `src/`.

### 5. HIGH — `Directory.Packages.props` location mismatch (Phase 0, P0.S4.T2)

- **Location:** Phase 0, P0.S4.T2
- **Why it breaks:** Blueprint says "Create `Directory.Packages.props` at repo root." Target state (Section 2) shows it at `src/Directory.Packages.props`. The existing `Directory.Build.props` is at `src/Directory.Build.props`. If `Directory.Packages.props` is placed at repo root while `.sln` is at `src/`, NuGet may not find it (MSBuild walks up from project dir). After tests move to `tests/` at repo root, they need the props file at or above their directory.
- **Evidence:** `src/Directory.Build.props` exists at `src/` level; `.sln` is at `src/LKvitai.MES.sln`; target state diagram shows `src/Directory.Packages.props`.
- **Fix:**
  - Place `Directory.Packages.props` at **repo root** (not `src/`). NuGet walks up from each csproj to find it. Since tests are at `tests/` (repo root) and source is at `src/` (repo root), putting it at repo root covers both.
  - Update blueprint to be explicit: "Create at repo root `/Directory.Packages.props`" and update target state diagram in Section 2 (currently shows `src/Directory.Packages.props`).

### 6. HIGH — `build-and-push.yml` Dockerfile paths not updated after Phase 3 moves

- **Location:** Phase 3, P3.S2.T3 and P3.S2.T4
- **Why it breaks:** `build-and-push.yml` (lines 20,23) hardcodes `src/LKvitai.MES.Api/Dockerfile` and `src/LKvitai.MES.WebUI/Dockerfile`. Blueprint Phase 3 tasks P3.S2.T3 and P3.S2.T4 update the Dockerfiles themselves but do NOT update `build-and-push.yml` matrix entries. The CI will fail on the next push to `main`.
- **Evidence:** `.github/workflows/build-and-push.yml` lines 20,23
- **Fix:**
  - Add explicit step in P3.S2.T3 or create P3.S2.T3b: "Update `build-and-push.yml` matrix `dockerfile:` paths from `src/LKvitai.MES.Api/Dockerfile` to `src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile`"
  - Same for WebUI after P3.S2.T4.

### 7. HIGH — `e2e-tests.yml` not updated after test move (Phase 0)

- **Location:** Phase 0, P0.S3.T2
- **Why it breaks:** P0.S3.T2 updates `deploy.yml` and `deploy-test.yml` test paths but does NOT mention `e2e-tests.yml`. This workflow (`.github/workflows/e2e-tests.yml`) hardcodes `src/tests/LKvitai.MES.Tests.E2E/LKvitai.MES.Tests.E2E.csproj`. After `git mv src/tests tests`, this path breaks.
- **Evidence:** `.github/workflows/e2e-tests.yml` lines 24,27 reference `src/tests/LKvitai.MES.Tests.E2E/`
- **Fix:**
  - Add `e2e-tests.yml` to the scope of P0.S3.T2: "Change `src/tests/LKvitai.MES.Tests.E2E/` to `tests/LKvitai.MES.Tests.E2E/` in `.github/workflows/e2e-tests.yml`"

### 8. MEDIUM — Phase 2 (Marten removal from Application) underestimates scope

- **Location:** Phase 2, P2.S1.T1
- **Why it breaks:** The task says "Identify all Marten types used in Application (IDocumentSession, IQuerySession)." However, the Application project has 10 sub-namespaces and a significant amount of code. Searching for actual Marten usage patterns (`IDocumentSession`, `IDocumentStore`, `IQuerySession`, `Marten.*`) across Application will likely reveal deep coupling. The task's token budget of "unspecified" is unrealistic for a Codex agent to handle — this is a multi-file refactor requiring architectural judgment.
- **Fix:**
  - Add a discovery sub-task before P2.S1.T1: "P2.S0.T1: Inventory Marten usage in Application. Run `grep -rn 'Marten\|IDocumentSession\|IDocumentStore\|IQuerySession' src/LKvitai.MES.Application/` and create an inventory of files and usages."
  - Add STOP condition: "If more than 10 files use Marten types directly, STOP and escalate."

### 9. MEDIUM — `docker-compose.test.yml` move direction is backwards (Phase 1)

- **Location:** Phase 1, P1.S1.T2
- **Why it breaks:** Blueprint says `git mv docker-compose.test.yml src/docker-compose.test.yml` (move from root INTO `src/`). But the `deploy-test.yml` CI workflow references it as `docker-compose.test.yml` (root-relative, since CI runs from repo root). Moving it into `src/` means CI must change to `src/docker-compose.test.yml`. This contradicts the convention of having compose files at repo root.
- **Evidence:** `.github/workflows/deploy-test.yml` lines 35,76,90,105 all reference `docker-compose.test.yml` (root-relative)
- **Fix:**
  - Reconsider: keep `docker-compose.test.yml` at repo root (where CI expects it). Move `src/docker-compose.yml` (dev compose) to repo root as `docker-compose.dev.yml` instead.
  - OR: If moving into `src/`, update ALL 4 references in `deploy-test.yml` in the same task.

### 10. MEDIUM — Phase 4 renames don't update `build-and-push.yml` Dockerfile paths a SECOND time

- **Location:** Phase 4, P4.S6.T1 and P4.S6.T2
- **Why it breaks:** After Phase 4 renames, Dockerfiles move from `src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile` to `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile`. The `build-and-push.yml` was updated in Phase 3 but needs ANOTHER update in Phase 4. P4.S6.T3 handles Dockerfile contents but not `build-and-push.yml` matrix paths.
- **Fix:**
  - Add to P4.S6.T3: "Update `build-and-push.yml` matrix `dockerfile:` paths to final names."

---

## C) Namespace Handling Verdict

### Verdict: **Namespace changes ARE required — and the blueprint addresses them, but incompletely**

### Evidence

**csproj analysis:**
- No project sets `<RootNamespace>` or `<AssemblyName>` explicitly — they default to the directory/project name.
- When projects move under `src/Modules/Warehouse/` in Phase 3 (without rename), the directory name stays the same (e.g., `LKvitai.MES.Domain`), so `RootNamespace` defaults remain unchanged. **Phase 3 does NOT require namespace changes.**
- Phase 4 renames directories AND explicitly sets `<RootNamespace>` and `<AssemblyName>` in each csproj. The namespace find-replace is then mandatory.

**Namespace-sensitive patterns found:**

| Pattern | Location | Safe? | Notes |
|---------|----------|-------|-------|
| MediatR assembly scanning | `MediatRConfiguration.cs:16,18` | YES | Uses `typeof(T).Assembly`, not strings |
| MassTransit consumer registration | `MassTransitConfiguration.cs:22-36` | YES | Uses `AddConsumer<T>()` generics |
| Marten event type registration | `MartenConfiguration.cs:41-46` | **NO** | `AddEventType<T>()` stores FQ CLR name in DB |
| Marten event upcasting | `MartenConfiguration.cs:61-62` | YES | Uses generic types |
| EF Core migrations (60+ files) | `Infrastructure/Persistence/Migrations/*` | **PARTIAL** | Namespace in `.cs` must match, but `MigrationId` in DB is safe |
| Blazor `_Imports.razor` | `WebUI/_Imports.razor` | **NO** | 8 `@using` directives must update |
| Blazor page `@using` directives | 20+ `.razor` files | **NO** | Must update |
| Controller route attributes | All controllers | YES | Use string literal routes, not namespace |
| CI test filter strings | `deploy.yml:23` | YES | Uses class names only, not FQ names |
| `InternalsVisibleTo` | (none found) | N/A | Not used |
| Reflection / `Type.GetType()` | (none with string args) | YES | All use runtime type refs |
| Serilog `ForContext` | (none with namespace strings) | YES | Uses `typeof(T)` |

### Proposed Minimal Strategy

1. **Phase 3 (moves only):** No namespace changes needed. Directory names preserved. Build stays green.

2. **Phase 4 pre-requisite (NEW TASK):** Before any namespace rename:
   - Register Marten event type aliases: `options.Events.MapEventType<T>("LKvitai.MES.Contracts.Events.OldName")` for every event type.
   - Validate with integration test that events can round-trip.

3. **Phase 4 per-project rename:** Each rename task (P4.S*.T3) must:
   - Update `namespace` declarations in `.cs` files
   - Update `using` directives in ALL consuming projects (including `.razor` files for WebUI)
   - Update EF migration file namespaces (60+ files for Infrastructure)
   - Run `dotnet build && dotnet test`

4. **Phase 4 post-rename:** Grep validation that no old namespaces remain:
   ```bash
   grep -rn "namespace LKvitai\.MES\.\(Domain\|Application\|Infrastructure\|Contracts\|Projections\|Sagas\|Integration\|Api\|WebUI\|SharedKernel\)[^.]" src/ tests/
   ```

---

## D) Quick Sanity Checklist

### Phase 0 (Safety Net)
```bash
# After P0.S1.T1 (test move):
dotnet restore && dotnet build && dotnet test
grep -r "src/tests" .github/workflows/  # must return 0 matches

# After P0.S4.T4 (central packages):
dotnet restore && dotnet build && dotnet test
grep -rn "Version=" src/**/*.csproj tests/**/*.csproj  # should show 0 PackageRef versions
```

### Phase 1 (Pilot slice)
```bash
# After P1.S2.T1 (Domain move):
dotnet restore && dotnet build
ls src/Modules/Warehouse/LKvitai.MES.Domain/  # must exist
ls src/LKvitai.MES.Domain/  # must NOT exist

# After P1.S2.T2 (Dockerfile):
docker build -f src/LKvitai.MES.Api/Dockerfile .
```

### Phase 3 (All moves)
```bash
# After P3.S2.T5:
dotnet restore && dotnet build && dotnet test
ls src/Modules/Warehouse/  # should list all 10 projects
ls src/LKvitai.MES.*/  # should return "No such file" for all
docker build -f src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.WebUI/Dockerfile .
grep -r "src/LKvitai.MES\." .github/workflows/  # must return 0 matches
```

### Phase 4 (Renames)
```bash
# BEFORE any rename (new pre-req task):
# Verify Marten aliases are registered and integration tests pass

# After EACH P4.S*.T3:
dotnet restore && dotnet build && dotnet test
grep -rn "namespace LKvitai\.MES\.<OldName>" src/ tests/  # must return 0

# After P4.S6.T3 (final Dockerfiles):
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile .
docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile .
```

### Phase 5 (Test renames)
```bash
# After P5.S2.T4:
dotnet test
grep -r "LKvitai.MES.Tests\.\(Unit\|Integration\|Property\|E2E\)" .github/workflows/  # must return 0
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

## Appendix: Discovery Evidence

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
