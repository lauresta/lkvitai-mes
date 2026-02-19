# Repo Refactor Blueprint

**Version:** 1.1  
**Date:** 2026-02-19  
**Purpose:** Incremental refactor from flat `LKvitai.MES.<Layer>` to modular `LKvitai.MES.Modules.<Module>.<Layer>` structure  
**Executor:** Codex (automated task execution agent)  
**Audit Source:** `docs/repo-audit/2026-02-16-repo-audit-vs-target.md`

## Changelog (v1.1)

**Key Changes:**
1. Reordered Phase 0: Tests relocation + ArchitectureTests + CI gate now BEFORE central package management
2. Replaced "fix before starting" with STOP condition: if baseline not green, STOP and report
3. Split central packages into safe incremental tasks with inventory step (now 4 tasks: inventory, create props, remove src versions, remove tests versions)
4. Added pilot end-to-end slice in Phase 1 with immediate Docker/CI validation
5. Removed "Warehouse-first" assumption language; clarified pilot module approach
6. Removed estimated duration and narrative fluff
7. Added explicit STOP conditions for cascading failures
8. Fixed .sln path syntax: `../tests/` (relative from src/ to repo root)
9. Improved CI path validation commands: grep for old paths to verify removal
10. Fixed PackageReference inventory command: use `find` instead of bash globstar
11. Made RabbitMQ optional via docker-compose profile instead of removing
12. Added STOP condition for cross-module entities in Domain before move
13. Updated task counts: Phase 0: 10 tasks, Total: 73 tasks (was 69)

**Validation Report Fixes (v1.1):**
14. Added Phase 4 Stage 4.0 with P4.S0.T1: Register Marten event type aliases BEFORE any namespace rename (BLOCKER #1 fix)
15. Updated P4.S4.T3 to explicitly include 60+ EF migration files in namespace update (BLOCKER #2 fix)
16. Updated P4.S6.T2 to include `_Imports.razor` and 20+ `.razor` files with `@using` directives (BLOCKER #3 fix)
17. Fixed Directory.Packages.props location to repo root (not src/) in P0.S4.T2 and target state diagram (Issue #5)
18. Added e2e-tests.yml to P0.S3.T2 scope (Issue #7)
19. Updated P3.S2.T3 to include build-and-push.yml Dockerfile path update for Api (Issue #6)
20. Updated P3.S2.T4 to include build-and-push.yml Dockerfile path update for WebUI (Issue #6)
21. Added P2.S0.T1 before P2.S1.T1: Inventory Marten usage in Application with grep command and STOP condition if >10 files (Issue #8)
22. Fixed P1.S1.T2: Keep docker-compose.test.yml at repo root (validation only, no move) to match CI expectations (Issue #9)
23. Updated P4.S6.T3 to include build-and-push.yml matrix path updates after final renames (Issue #10)
24. Updated Phase 2 counts: 4 stages, 7 tasks (was 3 stages, 6 tasks)
25. Updated Phase 4 counts: 7 stages, 21 tasks (was 6 stages, 19 tasks)
26. Updated total counts: 28 stages, 73 tasks (was 26 stages, 70 tasks)

**Validation Report Recheck Fixes (v1.1):**
27. Expanded P4.S0.T1 to include 13 Marten document types (saga states, snapshots, projection docs) in addition to 46 event types (New Issue #1)
28. Added explicit guidance in P4.S0.T1: expected event count ~46, skip abstract base class, verify StockMovedV1Event upcaster chain (New Issue #2)
29. Added P4.S5.T4: Validate Marten document types after Projections/Sagas/Integration renames with STOP condition (New Issue #3)
30. Updated Phase 4 counts: 7 stages, 21 tasks (was 7 stages, 20 tasks)
31. Updated total counts: 28 stages, 73 tasks (was 28 stages, 72 tasks)

**Note:** Token Budget lines remain in Phase 4-8 tasks from v1.0 but should be ignored by Codex.

---

## 1. Baseline

### Current Structure

**Solution:** `src/LKvitai.MES.sln` (14 projects)

**Source Projects (10):**
- `src/LKvitai.MES.Api/` — API entrypoint
- `src/LKvitai.MES.Application/` — Commands, Queries
- `src/LKvitai.MES.Contracts/` — Events, DTOs
- `src/LKvitai.MES.Domain/` — Aggregates, Entities
- `src/LKvitai.MES.Infrastructure/` — Persistence, EF Core
- `src/LKvitai.MES.Integration/` — External integrations
- `src/LKvitai.MES.Projections/` — Marten projections
- `src/LKvitai.MES.Sagas/` — MassTransit sagas
- `src/LKvitai.MES.SharedKernel/` — Base types
- `src/LKvitai.MES.WebUI/` — Blazor UI entrypoint

**Test Projects (4):**
- `src/tests/LKvitai.MES.Tests.Unit/`
- `src/tests/LKvitai.MES.Tests.Integration/`
- `src/tests/LKvitai.MES.Tests.Property/`
- `src/tests/LKvitai.MES.Tests.E2E/`

**Deployment Artifacts:**
- `src/LKvitai.MES.Api/Dockerfile` — hardcodes 9 project paths
- `src/LKvitai.MES.WebUI/Dockerfile` — standalone
- `src/docker-compose.yml` — dev environment (includes RabbitMQ)

**CI Workflows:**
- `.github/workflows/build-and-push.yml`
- `.github/workflows/deploy.yml` — hardcodes test filter strings
- `.github/workflows/deploy-test.yml`
- `.github/workflows/e2e-tests.yml`

### Baseline Validation

```bash
cd src
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity minimal
```

**STOP Condition:** If baseline is not green (build fails or tests fail), STOP immediately and report to human. Do NOT attempt to fix unrelated failing tests. Codex must NOT proceed with refactor until baseline is confirmed green by human.

---

## 2. Target State

### Directory Structure

```
LKvitai.MES/
├── src/
│   ├── Modules/
│   │   └── Warehouse/
│   │       ├── LKvitai.MES.Modules.Warehouse.Api/
│   │       ├── LKvitai.MES.Modules.Warehouse.Application/
│   │       ├── LKvitai.MES.Modules.Warehouse.Contracts/
│   │       ├── LKvitai.MES.Modules.Warehouse.Domain/
│   │       ├── LKvitai.MES.Modules.Warehouse.Infrastructure/
│   │       └── LKvitai.MES.Modules.Warehouse.Ui/
│   ├── BuildingBlocks/
│   │   ├── LKvitai.MES.BuildingBlocks.Messaging.Abstractions/
│   │   ├── LKvitai.MES.BuildingBlocks.Auth/
│   │   └── LKvitai.MES.BuildingBlocks.Testing/
│   └── LKvitai.MES.sln
├── tests/
│   ├── ArchitectureTests/
│   │   └── LKvitai.MES.ArchitectureTests/
│   └── Modules/
│       └── Warehouse/
│           ├── LKvitai.MES.Tests.Warehouse.Unit/
│           ├── LKvitai.MES.Tests.Warehouse.Integration/
│           ├── LKvitai.MES.Tests.Warehouse.Property/
│           └── LKvitai.MES.Tests.Warehouse.E2E/
├── deploy/
│   └── traefik/
│       └── dynamic.yml
├── scripts/
│   └── validate-module-dependencies.sh
├── docs/
└── Directory.Packages.props
```

### Naming Conventions

**Assemblies:** `LKvitai.MES.Modules.<Module>.<Layer>`  
**Namespaces:** `LKvitai.MES.Modules.<Module>.<Layer>.*`  
**BuildingBlocks:** `LKvitai.MES.BuildingBlocks.<Concern>`

**Note:** Current repo content is treated as pilot module "Warehouse". Future modules (Sales, Production, etc.) will follow same pattern.

### Dependency Rules

- Domain: no tech deps (EF, Marten, MassTransit)
- Application: no Infrastructure ref, no Marten
- Contracts: pure DTOs, zero deps
- BuildingBlocks: never reference Modules

---

## 3. Phases, Stages, Tasks

### Phase 0: Safety Net (5 stages, 10 tasks)

**Goal:** Add enforcement and validation before any structural changes.

#### Stage 0.1: Move Tests + Create ArchitectureTests

**P0.S1.T1: Move tests directory**

- **Purpose:** Relocate tests from `src/tests/` to `tests/` at repo root
- **Scope:** `src/tests/`, `tests/`
- **Operations:**
  - `git mv src/tests tests`
  - Update solution file paths in `src/LKvitai.MES.sln`: change `tests\LKvitai.MES.Tests.*` to `..\tests\LKvitai.MES.Tests.*` (Windows) or `../tests/LKvitai.MES.Tests.*` (Unix)
  - Note: .sln is in `src/`, tests moved to repo root, so relative path is `..` (up one level)
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds, test projects load in IDE
- **Rollback:** `git mv tests src/tests`, revert sln
- **STOP Condition:** If build fails with errors beyond path changes, STOP and report

**P0.S1.T2: Create ArchitectureTests project**

- **Purpose:** Add assembly-level boundary enforcement
- **Scope:** `tests/ArchitectureTests/`
- **Operations:**
  - Create `tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/` directory
  - Create `.csproj` with NetArchTest.Rules package
  - Add to solution: `dotnet sln src/LKvitai.MES.sln add tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/LKvitai.MES.ArchitectureTests.csproj`
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Project builds, appears in solution
- **Rollback:** Remove directory, remove from sln

**P0.S1.T3: Add baseline architecture tests (skipped)**

- **Purpose:** Document current violations as skipped tests
- **Scope:** `tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/`
- **Operations:**
  - Create `DomainLayerTests.cs` with test: Domain must not reference Infrastructure (Skip="Known violation")
  - Create `ApplicationLayerTests.cs` with test: Application must not reference Marten (Skip="Known violation")
  - Create `ContractsLayerTests.cs` with test: Contracts must have zero deps (Skip="Known violation")
- **Commands:**
  ```bash
  dotnet test tests/ArchitectureTests/
  ```
- **DoD:** All tests skipped (not failed), test run succeeds
- **Rollback:** Delete test files

#### Stage 0.2: Dependency Validation Script

**P0.S2.T1: Create validate-module-dependencies.sh**

- **Purpose:** Script to parse csproj and validate ProjectReference paths
- **Scope:** `scripts/`
- **Operations:**
  - Create `scripts/validate-module-dependencies.sh`
  - Parse all `.csproj` files for `<ProjectReference>` elements
  - Check: BuildingBlocks projects must not reference Modules projects
  - Check: Cross-module references only via Contracts
  - Exit 0 if valid, exit 1 with error message if invalid
- **Commands:**
  ```bash
  chmod +x scripts/validate-module-dependencies.sh
  ./scripts/validate-module-dependencies.sh
  ```
- **DoD:** Script runs, exits 0 (current structure has no BuildingBlocks yet, so passes trivially)
- **Rollback:** Delete script

#### Stage 0.3: CI Gate

**P0.S3.T1: Create architecture-checks.yml workflow**

- **Purpose:** Add PR gate for architecture rules
- **Scope:** `.github/workflows/`
- **Operations:**
  - Create `.github/workflows/architecture-checks.yml`
  - Trigger on PR to main
  - Run `scripts/validate-module-dependencies.sh`
  - Run `dotnet test tests/ArchitectureTests/`
- **Commands:**
  ```bash
  ./scripts/validate-module-dependencies.sh
  dotnet test tests/ArchitectureTests/
  ```
- **DoD:** Workflow file valid, can be triggered manually
- **Rollback:** Delete workflow file

**P0.S3.T2: Update CI workflow test paths**

- **Purpose:** Fix hardcoded test paths in workflows after test move
- **Scope:** `.github/workflows/deploy.yml`, `.github/workflows/deploy-test.yml`, `.github/workflows/e2e-tests.yml`
- **Operations:**
  - Change `src/tests/LKvitai.MES.Tests.Integration/` to `tests/LKvitai.MES.Tests.Integration/` in `deploy.yml`
  - Change `src/tests/LKvitai.MES.Tests.E2E/` to `tests/LKvitai.MES.Tests.E2E/` in `e2e-tests.yml`
  - Change `src\\tests\\` (Windows paths) to `tests\\` if present
  - Change any other `src/tests/` references to `tests/` in `deploy-test.yml`
- **Commands:**
  ```bash
  grep -R "src/tests" .github/workflows/
  grep -R "src\\\\tests" .github/workflows/
  ```
- **DoD:** Workflows reference correct paths, grep returns no matches for old paths
- **Rollback:** Revert workflow files
- **STOP Condition:** If grep still finds old paths after changes, STOP and report

#### Stage 0.4: Central Package Management (Incremental)

**P0.S4.T1: Inventory PackageReferences**

- **Purpose:** Document all package versions before centralization
- **Scope:** All `.csproj` files
- **Operations:**
  - Scan all `.csproj` files for `<PackageReference>` elements
  - Create `docs/blueprints/package-inventory.md` listing all packages with versions
  - Report any version conflicts found
- **Commands:**
  ```bash
  find src tests -name "*.csproj" -exec grep -H "PackageReference" {} \;
  ```
- **DoD:** Inventory document created, no action taken on csproj files yet
- **Rollback:** Delete inventory document
- **STOP Condition:** If version conflicts found, report to human for resolution before proceeding

**P0.S4.T2: Create Directory.Packages.props (minimal)**

- **Purpose:** Enable central package version management
- **Scope:** Repo root (NOT src/)
- **Operations:**
  - Create `Directory.Packages.props` at **repo root** (covers both `src/` and `tests/` directories)
  - Add `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
  - List all packages with versions from inventory in `<ItemGroup>`
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Build succeeds with central props file present at repo root
- **Rollback:** Delete `Directory.Packages.props`
- **STOP Condition:** If build fails, STOP and report

**P0.S4.T3: Remove versions from src csproj files**

- **Purpose:** Migrate product projects to central package management
- **Scope:** All `.csproj` files in `src/`
- **Operations:**
  - For each `<PackageReference Include="X" Version="Y" />` in `src/`, change to `<PackageReference Include="X" />`
  - Keep all other attributes (PrivateAssets, etc.)
  - Do NOT touch test projects yet
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Build succeeds, packages resolve from central props
- **Rollback:** `git checkout -- src/**/*.csproj`
- **STOP Condition:** If build fails, STOP and report

**P0.S4.T4: Remove versions from tests csproj files**

- **Purpose:** Complete migration of test projects to central package management
- **Scope:** All `.csproj` files in `tests/`
- **Operations:**
  - For each `<PackageReference Include="X" Version="Y" />` in `tests/`, change to `<PackageReference Include="X" />`
  - Keep all other attributes (PrivateAssets, etc.)
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test --no-build
  ```
- **DoD:** All tests pass, packages resolve from central props
- **Rollback:** `git checkout -- tests/**/*.csproj`
- **STOP Condition:** If tests fail that passed in baseline, STOP and report

#### Stage 0.5: Root File Cleanup

**P0.S5.T1: Move stale markdown files**

- **Purpose:** Clean repo root of temporary docs
- **Scope:** Repo root, `docs/adr/`
- **Operations:**
  - `git mv CONCURRENCY_BUG_ANALYSIS.md docs/adr/003-concurrency-bug-analysis.md`
  - `git mv CONCURRENCY_BUG_FIX_SUMMARY.md docs/adr/004-concurrency-bug-fix.md`
  - `git mv MARTEN_V2_VERSIONING_FIX.md docs/adr/005-marten-v2-versioning.md`
- **Commands:**
  ```bash
  ls -la | grep ".md"
  ```
- **DoD:** Only README.md and standard meta files at root
- **Rollback:** `git mv` files back

---

### Phase 1: Directory Structure + Pilot Slice (3 stages, 6 tasks)

**Goal:** Create target directories and validate one end-to-end project slice.

#### Stage 1.1: Create Directory Structure

**P1.S1.T1: Create Modules and BuildingBlocks directories**

- **Purpose:** Establish target folder structure
- **Scope:** `src/`, `deploy/`
- **Operations:**
  - Create `src/Modules/Warehouse/` directory
  - Create `src/BuildingBlocks/` directory
  - Create `deploy/traefik/` directory
  - Create placeholder `deploy/traefik/dynamic.yml` with example routing
- **Commands:**
  ```bash
  ls -la src/Modules/Warehouse/
  ls -la src/BuildingBlocks/
  ```
- **DoD:** Directories exist, empty
- **Rollback:** Remove directories

**P1.S1.T2: Keep docker-compose.test.yml at repo root**

- **Purpose:** Ensure CI workflows can find test compose file
- **Scope:** Repo root, `.github/workflows/deploy-test.yml`
- **Operations:**
  - Verify `docker-compose.test.yml` remains at repo root (do NOT move into `src/`)
  - Verify all 4 references in `deploy-test.yml` use root-relative path `docker-compose.test.yml`
  - No file moves needed, validation only
- **Commands:**
  ```bash
  grep -r "docker-compose.test.yml" .github/workflows/deploy-test.yml
  ls -la docker-compose.test.yml
  ```
- **DoD:** File confirmed at repo root, CI references correct path
- **Rollback:** N/A (validation only)

#### Stage 1.2: Pilot Slice - Domain (End-to-End Validation)

**P1.S2.T1: Move Domain project to Modules/Warehouse**

- **Purpose:** Validate move + build + Docker in one slice
- **Scope:** `src/LKvitai.MES.Domain/`, `src/Modules/Warehouse/`
- **Operations:**
  - **STOP and verify first:** Review Domain project for cross-module concerns (Sales, Production entities). If found, STOP and ask human whether to proceed or extract first.
  - `git mv src/LKvitai.MES.Domain src/Modules/Warehouse/LKvitai.MES.Domain`
  - Update solution file path
  - Update all ProjectReferences in other projects
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert sln and csproj changes
- **STOP Condition:** If Domain contains non-Warehouse entities (SalesOrder, Customer, etc.), STOP and report to human before proceeding

**P1.S2.T2: Update Api Dockerfile for Domain move**

- **Purpose:** Validate Docker build after first project move
- **Scope:** `src/LKvitai.MES.Api/Dockerfile`
- **Operations:**
  - Change `COPY src/LKvitai.MES.Domain/` to `COPY src/Modules/Warehouse/LKvitai.MES.Domain/`
  - Update corresponding restore line
- **Commands:**
  ```bash
  docker build -f src/LKvitai.MES.Api/Dockerfile .
  ```
- **DoD:** Docker build succeeds
- **Rollback:** Revert Dockerfile
- **STOP Condition:** If Docker build fails, STOP and report

**P1.S2.T3: Run full test suite after pilot slice**

- **Purpose:** Validate no behavioral regressions from move
- **Scope:** All tests
- **Operations:**
  - No code changes, validation only
- **Commands:**
  ```bash
  dotnet test
  ```
- **DoD:** All tests pass (same as baseline)
- **Rollback:** N/A (validation only)
- **STOP Condition:** If tests fail that passed in baseline, STOP and report

---

### Phase 2: Dependency Cleanup (4 stages, 7 tasks)

**Goal:** Fix layer violations before renaming.

#### Stage 2.0: Marten Usage Inventory

**P2.S0.T1: Inventory Marten usage in Application**

- **Purpose:** Document Marten coupling before extraction
- **Scope:** `src/LKvitai.MES.Application/`
- **Operations:**
  - Run grep to find all Marten types used in Application: `IDocumentSession`, `IDocumentStore`, `IQuerySession`, `Marten.*`
  - Create `docs/blueprints/marten-usage-inventory.md` listing all files and usage patterns
  - Count total files with Marten dependencies
- **Commands:**
  ```bash
  grep -rn 'Marten\|IDocumentSession\|IDocumentStore\|IQuerySession' src/LKvitai.MES.Application/
  ```
- **DoD:** Inventory document created with file count and usage patterns
- **Rollback:** Delete inventory document
- **STOP Condition:** If more than 10 files use Marten types directly, STOP and report to human for architectural review before proceeding

#### Stage 2.1: Remove Marten from Application

**P2.S1.T1: Extract Marten interfaces to Infrastructure**

- **Purpose:** Remove direct Marten dependency from Application layer
- **Scope:** `src/LKvitai.MES.Application/`, `src/LKvitai.MES.Infrastructure/`
- **Operations:**
  - Identify all Marten types used in Application (IDocumentSession, IQuerySession)
  - Create abstraction interfaces in Application (e.g., `IReadModelSession`)
  - Implement adapters in Infrastructure that wrap Marten types
  - Update Application code to use abstractions
- **Commands:**
  ```bash
  dotnet build src/LKvitai.MES.Application/
  dotnet test --filter "FullyQualifiedName~Application"
  ```
- **DoD:** Application builds without Marten PackageReference
- **Rollback:** Revert code changes, restore Marten ref
- **STOP Condition:** If Application tests fail that passed in baseline, STOP and report

**P2.S1.T2: Remove Marten PackageReference from Application.csproj**

- **Purpose:** Complete Application layer isolation
- **Scope:** `src/LKvitai.MES.Application/LKvitai.MES.Application.csproj`
- **Operations:**
  - Remove `<PackageReference Include="Marten" />`
  - Verify no Marten usings remain in Application code
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass, no Marten in Application
- **Rollback:** Re-add PackageReference
- **STOP Condition:** If tests fail that passed in baseline, STOP and report

#### Stage 2.2: Remove MediatR from SharedKernel

**P2.S2.T1: Create BuildingBlocks.Cqrs.Abstractions**

- **Purpose:** Extract CQRS interfaces from SharedKernel
- **Scope:** `src/BuildingBlocks/`
- **Operations:**
  - Create `src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/` project
  - Define `ICommand`, `IQuery<TResult>`, `ICommandHandler<TCommand>`, `IQueryHandler<TQuery, TResult>`
  - Add to solution
- **Commands:**
  ```bash
  dotnet restore
  dotnet build src/BuildingBlocks/LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/
  ```
- **DoD:** Project builds, in solution
- **Rollback:** Remove project, remove from sln

**P2.S2.T2: Replace MediatR interfaces in SharedKernel**

- **Purpose:** Remove MediatR dependency
- **Scope:** `src/LKvitai.MES.SharedKernel/`
- **Operations:**
  - Add ProjectReference to BuildingBlocks.Cqrs.Abstractions
  - Replace `IRequest` with `ICommand` or `IQuery<T>`
  - Remove MediatR PackageReference
  - Update all consuming projects to reference BuildingBlocks.Cqrs.Abstractions
- **Commands:**
  ```bash
  dotnet build
  dotnet test
  ```
- **DoD:** SharedKernel has no MediatR dep, all tests pass
- **Rollback:** Revert changes, restore MediatR ref
- **STOP Condition:** If tests fail that passed in baseline, STOP and report

#### Stage 2.3: Purify Contracts

**P2.S3.T1: Remove SharedKernel reference from Contracts**

- **Purpose:** Make Contracts pure DTOs
- **Scope:** `src/LKvitai.MES.Contracts/`
- **Operations:**
  - Identify types from SharedKernel used in Contracts
  - Inline small value types (e.g., Result<T>, Error) into Contracts if needed
  - Remove `<ProjectReference>` to SharedKernel
- **Commands:**
  ```bash
  dotnet build src/LKvitai.MES.Contracts/
  ```
- **DoD:** Contracts builds with zero ProjectReferences
- **Rollback:** Restore ProjectReference
- **STOP Condition:** If build fails, STOP and report

**P2.S3.T2: Update architecture tests to enforce Contracts purity**

- **Purpose:** Enable skipped test
- **Scope:** `tests/ArchitectureTests/`
- **Operations:**
  - Remove `Skip` attribute from Contracts purity test
  - Run test, verify it passes
- **Commands:**
  ```bash
  dotnet test tests/ArchitectureTests/
  ```
- **DoD:** Test passes (not skipped)
- **Rollback:** Re-add Skip attribute

---

### Phase 3: Remaining Project Relocation (2 stages, 9 tasks)

**Goal:** Move remaining projects into Modules/Warehouse/ (Domain already moved in Phase 1).

#### Stage 3.1: Move Core Projects

**P3.S1.T1: Move Contracts project**

- **Purpose:** Relocate Contracts to Modules/Warehouse/
- **Scope:** `src/LKvitai.MES.Contracts/`, `src/Modules/Warehouse/`
- **Operations:**
  - `git mv src/LKvitai.MES.Contracts src/Modules/Warehouse/LKvitai.MES.Contracts`
  - Update solution file path
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **STOP Condition:** If build fails beyond path changes, STOP and report

**P3.S1.T2: Move Application project**

- **Purpose:** Relocate Application to Modules/Warehouse/
- **Scope:** `src/LKvitai.MES.Application/`, `src/Modules/Warehouse/`
- **Operations:**
  - `git mv src/LKvitai.MES.Application src/Modules/Warehouse/LKvitai.MES.Application`
  - Update solution file path
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **STOP Condition:** If build fails beyond path changes, STOP and report

**P3.S1.T3: Move Infrastructure project**

- **Purpose:** Relocate Infrastructure to Modules/Warehouse/
- **Scope:** `src/LKvitai.MES.Infrastructure/`, `src/Modules/Warehouse/`
- **Operations:**
  - `git mv src/LKvitai.MES.Infrastructure src/Modules/Warehouse/LKvitai.MES.Infrastructure`
  - Update solution file path
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **STOP Condition:** If build fails beyond path changes, STOP and report

**P3.S1.T4: Move Projections, Sagas, Integration projects**

- **Purpose:** Relocate remaining support projects
- **Scope:** `src/LKvitai.MES.Projections/`, `src/LKvitai.MES.Sagas/`, `src/LKvitai.MES.Integration/`
- **Operations:**
  - `git mv src/LKvitai.MES.Projections src/Modules/Warehouse/LKvitai.MES.Projections`
  - `git mv src/LKvitai.MES.Sagas src/Modules/Warehouse/LKvitai.MES.Sagas`
  - `git mv src/LKvitai.MES.Integration src/Modules/Warehouse/LKvitai.MES.Integration`
  - Update solution file paths
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` all back, revert changes
- **STOP Condition:** If build fails beyond path changes, STOP and report

#### Stage 3.2: Move Entrypoints + Update Docker/CI

**P3.S2.T1: Move Api project**

- **Purpose:** Relocate Api to Modules/Warehouse/
- **Scope:** `src/LKvitai.MES.Api/`, `src/Modules/Warehouse/`
- **Operations:**
  - `git mv src/LKvitai.MES.Api src/Modules/Warehouse/LKvitai.MES.Api`
  - Update solution file path
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **STOP Condition:** If build fails beyond path changes, STOP and report

**P3.S2.T2: Move WebUI project**

- **Purpose:** Relocate WebUI to Modules/Warehouse/
- **Scope:** `src/LKvitai.MES.WebUI/`, `src/Modules/Warehouse/`
- **Operations:**
  - `git mv src/LKvitai.MES.WebUI src/Modules/Warehouse/LKvitai.MES.WebUI`
  - Update solution file path
  - Update test ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **STOP Condition:** If build fails beyond path changes, STOP and report

**P3.S2.T3: Update Api Dockerfile paths (complete)**

- **Purpose:** Fix all hardcoded project paths in Dockerfile
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile`, `.github/workflows/build-and-push.yml`
- **Operations:**
  - Change all `COPY src/LKvitai.MES.*/` to `COPY src/Modules/Warehouse/LKvitai.MES.*/` in Dockerfile
  - Update solution file path reference
  - Update WORKDIR paths
  - Update `build-and-push.yml` matrix `dockerfile:` path from `src/LKvitai.MES.Api/Dockerfile` to `src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile`
- **Commands:**
  ```bash
  docker build -f src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile .
  ```
- **DoD:** Docker build succeeds, CI workflow references correct path
- **Rollback:** Revert Dockerfile and workflow
- **STOP Condition:** If Docker build fails, STOP and report

**P3.S2.T4: Update WebUI Dockerfile paths**

- **Purpose:** Fix project path in Dockerfile
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.WebUI/Dockerfile`, `.github/workflows/build-and-push.yml`
- **Operations:**
  - Change `COPY src/LKvitai.MES.WebUI/` to `COPY src/Modules/Warehouse/LKvitai.MES.WebUI/`
  - Update WORKDIR path
  - Update `build-and-push.yml` matrix `dockerfile:` path from `src/LKvitai.MES.WebUI/Dockerfile` to `src/Modules/Warehouse/LKvitai.MES.WebUI/Dockerfile`
- **Commands:**
  ```bash
  docker build -f src/Modules/Warehouse/LKvitai.MES.WebUI/Dockerfile .
  ```
- **DoD:** Docker build succeeds, CI workflow references correct path
- **Rollback:** Revert Dockerfile and workflow
- **STOP Condition:** If Docker build fails, STOP and report

**P3.S2.T5: Run full test suite after all moves**

- **Purpose:** Validate no behavioral regressions from moves
- **Scope:** All tests
- **Operations:**
  - No code changes, validation only
- **Commands:**
  ```bash
  dotnet test
  ```
- **DoD:** All tests pass (same as baseline)
- **Rollback:** N/A (validation only)
- **STOP Condition:** If tests fail that passed in baseline, STOP and report

---

### Phase 4: Project + Namespace Rename (7 stages, 21 tasks)

**Goal:** Rename projects and namespaces to target conventions.

**CRITICAL PRE-REQUISITE:** Before ANY namespace rename, Marten event type aliases AND document type aliases must be registered to prevent event store corruption.

#### Stage 4.0: Marten Event Type Alias Registration (BLOCKER FIX)

**P4.S0.T1: Register Marten type aliases (events + documents)**

- **Purpose:** Preserve backward compatibility for existing events and documents in warehouse_events schema
- **Scope:** `src/LKvitai.MES.Infrastructure/Persistence/MartenConfiguration.cs`
- **Operations:**
  - **Event types (46 total):** List all event types from `src/LKvitai.MES.Contracts/Events/` directory. Expected: ~46 event types across 10 files. Skip abstract base class `WarehouseOperationalEvent`. For `StockMovedV1Event`, verify existing upcaster chain works with new namespace.
  - Add `MapEventType<T>("old.qualified.name")` for each event type before `AddEventType<T>()` calls
  - Example: `options.Events.MapEventType<ValuationInitialized>("LKvitai.MES.Contracts.Events.ValuationInitialized, LKvitai.MES.Contracts");`
  - **Document types (13 total):** Register type aliases for Marten-persisted documents:
    - 3 saga state classes: `PickStockSagaState`, `ReceiveGoodsSagaState`, `AgnumExportSagaState` (namespace `LKvitai.MES.Sagas`)
    - 2 inline snapshot aggregates: `Valuation`, `ItemValuation` (namespace `LKvitai.MES.Domain.Aggregates`)
    - 8 projection read models from `ProjectionRegistration.cs` (namespace `LKvitai.MES.Projections`)
  - Use Marten's document type aliasing API (e.g., `options.Schema.For<T>().DocumentAlias("old.qualified.name")`) OR prepare SQL migration to update `mt_doc_type` column in `mt_doc_*` tables after namespace rename
- **Commands:**
  ```bash
  dotnet build src/LKvitai.MES.Infrastructure/
  dotnet test --filter "FullyQualifiedName~Marten"
  dotnet test --filter "FullyQualifiedName~Saga"
  ```
- **DoD:** Integration tests pass, events and documents can round-trip with old type names, saga tests pass
- **Rollback:** Remove MapEventType and document alias calls
- **STOP Condition:** If integration tests or saga tests fail, STOP and report. Do NOT proceed to namespace renames without green tests.

#### Stage 4.1: Rename Domain

**P4.S1.T1: Rename Domain project directory**

- **Purpose:** Change directory name to target convention
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Domain/`
- **Operations:**
  - `git mv src/Modules/Warehouse/LKvitai.MES.Domain src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain`
  - Update solution file path
  - Update all ProjectReferences in other projects
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **Token Budget:** 150 tokens

**P4.S1.T2: Rename Domain csproj and set RootNamespace**

- **Purpose:** Update project file name and namespace
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/`
- **Operations:**
  - Rename `.csproj` file to `LKvitai.MES.Modules.Warehouse.Domain.csproj`
  - Add `<RootNamespace>LKvitai.MES.Modules.Warehouse.Domain</RootNamespace>` to csproj
  - Add `<AssemblyName>LKvitai.MES.Modules.Warehouse.Domain</AssemblyName>` to csproj
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Project builds with new assembly name
- **Rollback:** Rename back, remove properties
- **Token Budget:** 100 tokens

**P4.S1.T3: Update Domain namespaces**

- **Purpose:** Change all namespaces in Domain project
- **Scope:** All `.cs` files in Domain project
- **Operations:**
  - Find-replace: `namespace LKvitai.MES.Domain` → `namespace LKvitai.MES.Modules.Warehouse.Domain`
  - Update all `using LKvitai.MES.Domain` statements in other projects
- **Commands:**
  ```bash
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass
- **Rollback:** Revert namespace changes
- **Token Budget:** 200 tokens

#### Stage 4.2: Rename Contracts

**P4.S2.T1: Rename Contracts project directory**

- **Purpose:** Change directory name to target convention
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Contracts/`
- **Operations:**
  - `git mv src/Modules/Warehouse/LKvitai.MES.Contracts src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts`
  - Update solution file path
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **Token Budget:** 150 tokens

**P4.S2.T2: Rename Contracts csproj and set RootNamespace**

- **Purpose:** Update project file name and namespace
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/`
- **Operations:**
  - Rename `.csproj` to `LKvitai.MES.Modules.Warehouse.Contracts.csproj`
  - Add RootNamespace and AssemblyName properties
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Project builds
- **Rollback:** Rename back
- **Token Budget:** 100 tokens

**P4.S2.T3: Update Contracts namespaces**

- **Purpose:** Change all namespaces in Contracts project
- **Scope:** All `.cs` files in Contracts project
- **Operations:**
  - Find-replace: `namespace LKvitai.MES.Contracts` → `namespace LKvitai.MES.Modules.Warehouse.Contracts`
  - Update all `using LKvitai.MES.Contracts` in other projects
- **Commands:**
  ```bash
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass
- **Rollback:** Revert changes
- **Token Budget:** 200 tokens

#### Stage 4.3: Rename Application

**P4.S3.T1: Rename Application project directory**

- **Purpose:** Change directory name to target convention
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Application/`
- **Operations:**
  - `git mv src/Modules/Warehouse/LKvitai.MES.Application src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application`
  - Update solution file path
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **Token Budget:** 150 tokens

**P4.S3.T2: Rename Application csproj and set RootNamespace**

- **Purpose:** Update project file name and namespace
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/`
- **Operations:**
  - Rename `.csproj` to `LKvitai.MES.Modules.Warehouse.Application.csproj`
  - Add RootNamespace and AssemblyName properties
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Project builds
- **Rollback:** Rename back
- **Token Budget:** 100 tokens

**P4.S3.T3: Update Application namespaces**

- **Purpose:** Change all namespaces in Application project
- **Scope:** All `.cs` files in Application project
- **Operations:**
  - Find-replace: `namespace LKvitai.MES.Application` → `namespace LKvitai.MES.Modules.Warehouse.Application`
  - Update all `using LKvitai.MES.Application` in other projects
- **Commands:**
  ```bash
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass
- **Rollback:** Revert changes
- **Token Budget:** 200 tokens

#### Stage 4.4: Rename Infrastructure

**P4.S4.T1: Rename Infrastructure project directory**

- **Purpose:** Change directory name to target convention
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Infrastructure/`
- **Operations:**
  - `git mv src/Modules/Warehouse/LKvitai.MES.Infrastructure src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure`
  - Update solution file path
  - Update all ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **Token Budget:** 150 tokens

**P4.S4.T2: Rename Infrastructure csproj and set RootNamespace**

- **Purpose:** Update project file name and namespace
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure/`
- **Operations:**
  - Rename `.csproj` to `LKvitai.MES.Modules.Warehouse.Infrastructure.csproj`
  - Add RootNamespace and AssemblyName properties
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Project builds
- **Rollback:** Rename back
- **Token Budget:** 100 tokens

**P4.S4.T3: Update Infrastructure namespaces**

- **Purpose:** Change all namespaces in Infrastructure project including 60+ migration files
- **Scope:** All `.cs` files in Infrastructure project, including `Persistence/Migrations/` directory
- **Operations:**
  - Find-replace in ALL `.cs` files (including migrations): `namespace LKvitai.MES.Infrastructure` → `namespace LKvitai.MES.Modules.Warehouse.Infrastructure`
  - Update all `using LKvitai.MES.Infrastructure` in other projects
  - Update migration `.Designer.cs` files: change `namespace` and `using` directives
  - Update `WarehouseDbContextModelSnapshot.cs` namespace
- **Commands:**
  ```bash
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass, all 60+ migration files compile
- **Rollback:** Revert changes
- **Token Budget:** 200 tokens
- **STOP Condition:** If migrations fail to compile, STOP and report

#### Stage 4.5: Rename Remaining Projects (Projections, Sagas, Integration)

**P4.S5.T1: Rename Projections (directory + csproj + namespaces)**

- **Purpose:** Full rename of Projections project
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Projections/`
- **Operations:**
  - `git mv` to `LKvitai.MES.Modules.Warehouse.Projections`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Projections` → `LKvitai.MES.Modules.Warehouse.Projections`
  - Update solution and ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

**P4.S5.T2: Rename Sagas (directory + csproj + namespaces)**

- **Purpose:** Full rename of Sagas project
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Sagas/`
- **Operations:**
  - `git mv` to `LKvitai.MES.Modules.Warehouse.Sagas`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Sagas` → `LKvitai.MES.Modules.Warehouse.Sagas`
  - Update solution and ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

**P4.S5.T3: Rename Integration (directory + csproj + namespaces)**

- **Purpose:** Full rename of Integration project
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Integration/`
- **Operations:**
  - `git mv` to `LKvitai.MES.Modules.Warehouse.Integration`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Integration` → `LKvitai.MES.Modules.Warehouse.Integration`
  - Update solution and ProjectReferences
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

**P4.S5.T4: Validate Marten document types after namespace renames**

- **Purpose:** Verify saga states, snapshots, and projection documents still deserialize correctly
- **Scope:** All Marten integration tests
- **Operations:**
  - No code changes, validation only
  - Run Marten integration tests to verify event store still works
  - Run saga tests to verify saga state persistence works
  - Run projection tests to verify projection documents work
- **Commands:**
  ```bash
  dotnet test --filter "FullyQualifiedName~Marten OR FullyQualifiedName~Saga OR FullyQualifiedName~Projection"
  ```
- **DoD:** All Marten, saga, and projection tests pass
- **Rollback:** N/A (validation only)
- **STOP Condition:** If saga or projection integration tests fail, STOP and report. Document type aliasing from P4.S0.T1 may need adjustment.

#### Stage 4.6: Rename Entrypoints

**P4.S6.T1: Rename Api (directory + csproj + namespaces)**

- **Purpose:** Full rename of Api project
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.Api/`
- **Operations:**
  - `git mv` to `LKvitai.MES.Modules.Warehouse.Api`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Api` → `LKvitai.MES.Modules.Warehouse.Api`
  - Update solution and ProjectReferences
  - Update Dockerfile ENTRYPOINT to `LKvitai.MES.Modules.Warehouse.Api.dll`
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile .
  ```
- **DoD:** Build + Docker build succeed
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

**P4.S6.T2: Rename WebUI (directory + csproj + namespaces + Blazor files)**

- **Purpose:** Full rename of WebUI project to Ui including .razor files
- **Scope:** `src/Modules/Warehouse/LKvitai.MES.WebUI/`
- **Operations:**
  - `git mv` to `LKvitai.MES.Modules.Warehouse.Ui`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces in `.cs` files: `LKvitai.MES.WebUI` → `LKvitai.MES.Modules.Warehouse.Ui`
  - **CRITICAL:** Update `@using` directives in `_Imports.razor` (8 directives) and all 20+ `.razor` files
  - Update solution and test ProjectReferences
  - Update Dockerfile ENTRYPOINT to `LKvitai.MES.Modules.Warehouse.Ui.dll`
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  grep -r "LKvitai.MES.WebUI" src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/
  docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile .
  ```
- **DoD:** Build + Docker build succeed, grep returns zero matches for old namespace
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens
- **STOP Condition:** If grep still finds old namespaces, STOP and report

**P4.S6.T3: Update Dockerfile project paths and CI workflow**

- **Purpose:** Fix all COPY paths in Dockerfiles after renames AND update build-and-push.yml for second time
- **Scope:** Api and Ui Dockerfiles, `.github/workflows/build-and-push.yml`
- **Operations:**
  - Update all `COPY src/Modules/Warehouse/LKvitai.MES.*/` paths to new names in both Dockerfiles
  - Update WORKDIR paths
  - Verify solution file path
  - **CRITICAL:** Update `build-and-push.yml` matrix `dockerfile:` paths from `src/Modules/Warehouse/LKvitai.MES.Api/Dockerfile` to `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile`
  - Update `build-and-push.yml` matrix `dockerfile:` paths from `src/Modules/Warehouse/LKvitai.MES.WebUI/Dockerfile` to `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile`
- **Commands:**
  ```bash
  docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile .
  docker build -f src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Ui/Dockerfile .
  grep "dockerfile:" .github/workflows/build-and-push.yml
  ```
- **DoD:** Both Docker builds succeed, CI workflow references correct Dockerfile paths
- **Rollback:** Revert Dockerfiles and workflow
- **Token Budget:** 150 tokens
- **STOP Condition:** If Docker builds fail or CI workflow has incorrect paths, STOP and report

---

### Phase 5: Test Projects Rename (2 stages, 8 tasks)

**Goal:** Rename test projects to match module conventions.

#### Stage 5.1: Rename Unit and Property Tests

**P5.S1.T1: Rename Tests.Unit (directory + csproj + namespaces)**

- **Purpose:** Rename to module-scoped test project
- **Scope:** `tests/LKvitai.MES.Tests.Unit/`
- **Operations:**
  - Create `tests/Modules/Warehouse/` directory
  - `git mv tests/LKvitai.MES.Tests.Unit tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Tests.Unit` → `LKvitai.MES.Tests.Warehouse.Unit`
  - Update solution path
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/
  ```
- **DoD:** Tests run and pass
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

**P5.S1.T2: Rename Tests.Property (directory + csproj + namespaces)**

- **Purpose:** Rename to module-scoped test project
- **Scope:** `tests/LKvitai.MES.Tests.Property/`
- **Operations:**
  - `git mv tests/LKvitai.MES.Tests.Property tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Property`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Tests.Property` → `LKvitai.MES.Tests.Warehouse.Property`
  - Update solution path
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Property/
  ```
- **DoD:** Tests run and pass
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

#### Stage 5.2: Rename Integration and E2E Tests

**P5.S2.T1: Rename Tests.Integration (directory + csproj + namespaces)**

- **Purpose:** Rename to module-scoped test project
- **Scope:** `tests/LKvitai.MES.Tests.Integration/`
- **Operations:**
  - `git mv tests/LKvitai.MES.Tests.Integration tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Tests.Integration` → `LKvitai.MES.Tests.Warehouse.Integration`
  - Update solution path
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/
  ```
- **DoD:** Tests run and pass
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

**P5.S2.T2: Rename Tests.E2E (directory + csproj + namespaces)**

- **Purpose:** Rename to module-scoped test project
- **Scope:** `tests/LKvitai.MES.Tests.E2E/`
- **Operations:**
  - `git mv tests/LKvitai.MES.Tests.E2E tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E`
  - Rename csproj, add RootNamespace/AssemblyName
  - Update namespaces: `LKvitai.MES.Tests.E2E` → `LKvitai.MES.Tests.Warehouse.E2E`
  - Update solution path
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/
  ```
- **DoD:** Tests run and pass
- **Rollback:** Revert all changes
- **Token Budget:** 250 tokens

**P5.S2.T3: Update CI workflow test paths**

- **Purpose:** Fix hardcoded test paths in workflows after rename
- **Scope:** `.github/workflows/deploy.yml`, `.github/workflows/e2e-tests.yml`
- **Operations:**
  - Change `tests/LKvitai.MES.Tests.Integration/` to `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/`
  - Change `tests/LKvitai.MES.Tests.E2E/` to `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/`
  - Update test filter strings if they reference old namespaces
- **Commands:**
  ```bash
  grep -r "LKvitai.MES.Tests" .github/workflows/
  ```
- **DoD:** Workflows reference correct paths and namespaces
- **Rollback:** Revert workflow files
- **Token Budget:** 150 tokens

**P5.S2.T4: Update deploy-test.yml EF migration paths**

- **Purpose:** Fix EF Core migration command paths
- **Scope:** `.github/workflows/deploy-test.yml`
- **Operations:**
  - Change `--project src/LKvitai.MES.Infrastructure` to `--project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Infrastructure`
  - Change `--startup-project src/LKvitai.MES.Api` to `--startup-project src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api`
- **Commands:**
  ```bash
  cat .github/workflows/deploy-test.yml | grep "dotnet ef"
  ```
- **DoD:** Workflow has correct paths
- **Rollback:** Revert workflow file
- **Token Budget:** 100 tokens

---

### Phase 6: SharedKernel Decision (2 stages, 3 tasks)

**Goal:** Decide fate of SharedKernel - dissolve or convert to BuildingBlock.

**Decision Point:** SharedKernel currently contains base types used across modules. Options:
1. Convert to `BuildingBlocks.SharedKernel` (if truly cross-module)
2. Move into `Warehouse.Domain` (if Warehouse-specific)
3. Dissolve and inline into consuming projects

**Recommended:** Option 1 (convert to BuildingBlock) for now, revisit when adding second module.

#### Stage 6.1: Convert SharedKernel to BuildingBlock

**P6.S1.T1: Move SharedKernel to BuildingBlocks**

- **Purpose:** Relocate SharedKernel to BuildingBlocks directory
- **Scope:** `src/LKvitai.MES.SharedKernel/`, `src/BuildingBlocks/`
- **Operations:**
  - `git mv src/LKvitai.MES.SharedKernel src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel`
  - Update solution file path
  - Update all ProjectReferences in Warehouse projects
- **Commands:**
  ```bash
  dotnet restore
  dotnet build
  ```
- **DoD:** Solution builds
- **Rollback:** `git mv` back, revert changes
- **Token Budget:** 150 tokens

**P6.S1.T2: Rename SharedKernel csproj and namespaces**

- **Purpose:** Update to BuildingBlocks naming convention
- **Scope:** `src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/`
- **Operations:**
  - Rename csproj to `LKvitai.MES.BuildingBlocks.SharedKernel.csproj`
  - Add RootNamespace and AssemblyName properties
  - Update namespaces: `LKvitai.MES.SharedKernel` → `LKvitai.MES.BuildingBlocks.SharedKernel`
  - Update all `using` statements in consuming projects
- **Commands:**
  ```bash
  dotnet build
  dotnet test
  ```
- **DoD:** Full build + test pass
- **Rollback:** Revert changes
- **Token Budget:** 200 tokens

#### Stage 6.2: Validate BuildingBlocks Isolation

**P6.S2.T1: Update architecture tests for BuildingBlocks**

- **Purpose:** Enforce BuildingBlocks cannot reference Modules
- **Scope:** `tests/ArchitectureTests/`
- **Operations:**
  - Add test: BuildingBlocks assemblies must not reference Modules assemblies
  - Run test, verify it passes
  - Update `scripts/validate-module-dependencies.sh` to check BuildingBlocks isolation
- **Commands:**
  ```bash
  dotnet test tests/ArchitectureTests/
  ./scripts/validate-module-dependencies.sh
  ```
- **DoD:** Tests pass, script validates correctly
- **Rollback:** Remove test
- **Token Budget:** 150 tokens

---

### Phase 7: CI/CD Updates (2 stages, 5 tasks)

**Goal:** Update all CI workflows for new structure.

#### Stage 7.1: Update Build Workflows

**P7.S1.T1: Update build-and-push.yml paths**

- **Purpose:** Add path filters for module-based triggers
- **Scope:** `.github/workflows/build-and-push.yml`
- **Operations:**
  - Add `paths:` filter to trigger only on changes to `src/Modules/Warehouse/**`, `src/BuildingBlocks/**`
  - Update any hardcoded project paths in build steps
  - Update Docker build context paths
- **Commands:**
  ```bash
  # Validate workflow syntax
  cat .github/workflows/build-and-push.yml
  ```
- **DoD:** Workflow has path filters, references correct paths
- **Rollback:** Revert workflow file
- **Token Budget:** 150 tokens

**P7.S1.T2: Create warehouse-ci.yml workflow**

- **Purpose:** Add module-specific CI workflow
- **Scope:** `.github/workflows/`
- **Operations:**
  - Create `.github/workflows/warehouse-ci.yml`
  - Trigger on PR to main with paths: `src/Modules/Warehouse/**`, `tests/Modules/Warehouse/**`
  - Run build, unit tests, integration tests for Warehouse module only
- **Commands:**
  ```bash
  # Validate workflow syntax
  cat .github/workflows/warehouse-ci.yml
  ```
- **DoD:** Workflow file valid, can be triggered
- **Rollback:** Delete workflow file
- **Token Budget:** 200 tokens

#### Stage 7.2: Update Deployment Workflows

**P7.S2.T1: Update docker-compose.yml image names**

- **Purpose:** Update container image names to reflect new structure
- **Scope:** `src/docker-compose.yml`, `docker-compose.test.yml`
- **Operations:**
  - Update image names from `lkvitai-mes-warehouse-api` to `lkvitai-mes-warehouse-api` (no change needed, but verify)
  - Update build context paths if needed
  - Verify service names match Dockerfile locations
- **Commands:**
  ```bash
  docker-compose -f src/docker-compose.yml config
  ```
- **DoD:** Compose file validates
- **Rollback:** Revert compose files
- **Token Budget:** 100 tokens

**P7.S2.T2: Make RabbitMQ optional in dev docker-compose**

- **Purpose:** Comply with target spec while preserving dev experience
- **Scope:** `src/docker-compose.yml`
- **Operations:**
  - Add comment above `rabbitmq:` service: "# DEV ONLY: RabbitMQ for local development. Production uses external broker."
  - Add docker-compose profile to make RabbitMQ optional: `profiles: ["dev-broker"]`
  - Update README with instructions: use `--profile dev-broker` to include RabbitMQ, or connect to external broker
- **Commands:**
  ```bash
  docker-compose -f src/docker-compose.yml config
  ```
- **DoD:** Compose file validates, RabbitMQ marked as optional dev-only service
- **Rollback:** Revert compose file
- **STOP Condition:** If existing CI/tests depend on RabbitMQ and break, STOP and report

**P7.S2.T3: Update architecture-checks.yml to run on PR**

- **Purpose:** Enable architecture gate on all PRs
- **Scope:** `.github/workflows/architecture-checks.yml`
- **Operations:**
  - Ensure trigger is `pull_request: branches: [main]`
  - Verify it runs architecture tests and validation script
  - Add as required check in branch protection (manual step, document in PR)
- **Commands:**
  ```bash
  cat .github/workflows/architecture-checks.yml
  ```
- **DoD:** Workflow triggers on PR
- **Rollback:** Revert workflow file
- **Token Budget:** 100 tokens

---

### Phase 8: Documentation Updates (1 stage, 4 tasks)

**Goal:** Update all documentation to reflect new structure.

#### Stage 8.1: Update Documentation

**P8.S1.T1: Update README files**

- **Purpose:** Document new structure in README
- **Scope:** `README.md`, `src/README.md`
- **Operations:**
  - Update project structure diagrams
  - Update build/test instructions with new paths
  - Document module conventions
  - Add link to this blueprint
- **Commands:**
  ```bash
  cat README.md
  ```
- **DoD:** README reflects current structure
- **Rollback:** Revert README changes
- **Token Budget:** 150 tokens

**P8.S1.T2: Update architecture documentation**

- **Purpose:** Reflect new modular structure in docs
- **Scope:** `docs/04-system-architecture.md`, `docs/dependency-map.md`
- **Operations:**
  - Update architecture diagrams with Modules/BuildingBlocks structure
  - Update dependency graphs with new project names
  - Document module boundaries and communication patterns
- **Commands:**
  ```bash
  cat docs/04-system-architecture.md
  ```
- **DoD:** Architecture docs match current structure
- **Rollback:** Revert doc changes
- **Token Budget:** 150 tokens

**P8.S1.T3: Archive old audit documents**

- **Purpose:** Mark audit as completed
- **Scope:** `docs/repo-audit/`
- **Operations:**
  - Add note to `2026-02-16-repo-audit-vs-target.md`: "RESOLVED: See refactor blueprint and completion status"
  - Create `docs/repo-audit/2026-02-19-refactor-completion.md` documenting what was done
- **Commands:**
  ```bash
  cat docs/repo-audit/2026-02-19-refactor-completion.md
  ```
- **DoD:** Completion document exists
- **Rollback:** Delete completion doc
- **Token Budget:** 100 tokens

**P8.S1.T4: Update SOLUTION_STRUCTURE.md**

- **Purpose:** Document current solution structure
- **Scope:** `src/SOLUTION_STRUCTURE.md`
- **Operations:**
  - Update project list with new names and paths
  - Document Modules/BuildingBlocks organization
  - Update dependency graph
- **Commands:**
  ```bash
  cat src/SOLUTION_STRUCTURE.md
  ```
- **DoD:** Document reflects current structure
- **Rollback:** Revert changes
- **Token Budget:** 100 tokens

---

## 4. Task Template (Reference)

**Note:** Some tasks in Phase 4-8 still contain "Token Budget" lines from v1.0. These are informational only and should be ignored by Codex. Focus on Purpose, Operations, Commands, DoD, Rollback, and STOP Condition fields.

Each task follows this structure:

```
**P{phase}.S{stage}.T{task}: Task Name**

- **Purpose:** One sentence describing what this task achieves
- **Scope:** Explicit folders/projects touched
- **Operations:**
  - Bullet list of exact file operations (move/rename/add)
  - Specific commands or code changes
- **Commands:**
  ```bash
  # 1-3 commands to validate the change
  ```
- **DoD:** Measurable definition of done
- **Rollback:** How to revert if broken
- **STOP Condition:** (if applicable) When to stop and report to human
```

---

## 5. Module Extraction Playbook (Repeatable)

**When adding a new module (e.g., Sales, Production), follow this 3-stage loop:**

### Stage M.1: Create Module Structure

**M.1.T1: Create module directory and projects**
- Create `src/Modules/<ModuleName>/` directory
- Create projects: `Domain`, `Contracts`, `Application`, `Infrastructure`, `Api`
- Add to solution
- Set RootNamespace and AssemblyName in each csproj

**M.1.T2: Implement module contracts**
- Define integration events in `<Module>.Contracts`
- Ensure Contracts has zero dependencies
- Add architecture test for new module

### Stage M.2: Extract Domain Logic

**M.2.T1: Move domain entities**
- Identify entities belonging to new module (currently in Warehouse.Domain)
- Move to new module's Domain project
- Update namespaces
- Fix references in Warehouse module

**M.2.T2: Extract application logic**
- Move commands/queries to new module's Application
- Update namespaces
- Fix references

**M.2.T3: Extract infrastructure**
- Create new DbContext for module (DB-per-service)
- Move persistence logic to new module's Infrastructure
- Update connection strings

### Stage M.3: Validate and Test

**M.3.T1: Run architecture tests**
- Verify no cross-module dependencies except via Contracts
- Verify BuildingBlocks isolation
- Run `scripts/validate-module-dependencies.sh`

**M.3.T2: Run full test suite**
- All unit tests pass
- All integration tests pass
- All property tests pass

**M.3.T3: Update CI**
- Add `<module>-ci.yml` workflow
- Add path filters for new module
- Update architecture-checks.yml

### BuildingBlocks Decision Rule

**Create a BuildingBlock when:**
- Functionality is needed by 2+ modules
- It's a cross-cutting concern (auth, messaging, observability)
- It has no business logic

**Keep in module when:**
- Only one module needs it
- It contains module-specific logic
- Uncertain if other modules will need it (YAGNI)

---

## 6. Risk Log

### Risk 1: Dockerfile Path Breakage

**Severity:** HIGH  
**Likelihood:** CERTAIN  
**Impact:** Docker builds fail, deployment blocked

**Mitigation:**
- Phase 3 explicitly updates Dockerfiles after project moves
- Phase 4 updates Dockerfiles after renames
- Each Dockerfile change includes Docker build validation
- Test Docker builds in CI before merge

**Checkpoints:**
- P3.S2.T3: Validate Api Dockerfile after move
- P3.S2.T4: Validate WebUI Dockerfile after move
- P4.S6.T3: Validate both Dockerfiles after rename

### Risk 2: CI Workflow Path Breakage

**Severity:** HIGH  
**Likelihood:** CERTAIN  
**Impact:** CI fails, PRs blocked

**Mitigation:**
- Phase 1 updates test paths in workflows
- Phase 5 updates test paths after test rename
- Phase 7 adds path filters and creates new workflows
- Each workflow change validated with syntax check

**Checkpoints:**
- P1.S1.T3: Update CI test paths after test move
- P5.S2.T3: Update CI test paths after test rename
- P7.S1.T1: Add path filters to build workflow

### Risk 3: Namespace Churn (Massive Find-Replace)

**Severity:** MEDIUM  
**Likelihood:** CERTAIN  
**Impact:** Compilation errors, broken references

**Mitigation:**
- Phase 4 breaks namespace changes into per-project stages
- Each namespace change followed by full build + test
- Use IDE refactoring tools (Rider "Adjust Namespaces") when possible
- Property-based tests catch behavioral regressions

**Checkpoints:**
- After each P4.S*.T3 task: full build + test must pass
- Architecture tests validate no broken references

### Risk 4: Cross-Module Dependencies

**Severity:** MEDIUM  
**Likelihood:** LOW (only one module currently)  
**Impact:** Violates module boundaries

**Mitigation:**
- Phase 0 adds architecture tests and validation script
- Tests run on every PR via architecture-checks.yml
- Manual review of ProjectReferences during Phase 2-4

**Checkpoints:**
- P0.S2.T2: Architecture tests document expected boundaries
- P0.S4.T1: CI gate enforces boundaries
- P6.S2.T1: Validate BuildingBlocks isolation

### Risk 5: Test Failures After Rename

**Severity:** MEDIUM  
**Likelihood:** MEDIUM  
**Impact:** Tests fail, unclear if code or test issue

**Mitigation:**
- Baseline validation in Phase 0 ensures tests pass before refactor
- Each phase ends with full test run
- Property-based tests provide high-confidence regression detection
- Integration tests validate end-to-end behavior

**Checkpoints:**
- Baseline: All tests green before starting
- After each phase: `dotnet test` must pass
- P5.S2.T1-T2: Validate integration/E2E tests after rename

### Risk 6: Merge Conflicts (Long-Running Refactor)

**Severity:** LOW  
**Likelihood:** MEDIUM  
**Impact:** Difficult merges if main branch changes

**Mitigation:**
- Execute refactor in dedicated branch
- Communicate refactor timeline to team
- Freeze feature development during refactor (or coordinate carefully)
- Small PRs per phase reduce conflict surface

**Checkpoints:**
- Create `refactor/modular-structure` branch before Phase 0
- Merge to main after each phase (or after Phase 2, 4, 7)
- Rebase frequently if feature work continues

---

## 7. Execution Rules for Codex

### Rule 1: One Task = One PR

- Execute exactly one task per PR
- PR title: `[Refactor] P{phase}.S{stage}.T{task}: {task name}`
- PR description: Copy task Purpose, Operations, Commands, DoD
- Include command outputs in PR description

### Rule 2: No Unrelated Cleanup

- Do not fix unrelated code issues
- Do not refactor logic while renaming
- Do not add features
- Stick to the task scope exactly

### Rule 3: Never Touch More Than Necessary

- Only modify files listed in task Scope
- Do not "improve" code structure beyond task requirements
- Do not rename variables/methods unless required for namespace change

### Rule 4: Consistent Commit Messages

- Format: `refactor(P{phase}.S{stage}.T{task}): {task name}`
- Example: `refactor(P4.S1.T1): Rename Domain project directory`
- One commit per task (squash if multiple commits made)

### Rule 5: Always Run Listed Commands

- Execute all commands in task's Commands section
- Paste full output in PR description
- If command fails, stop and report error (do not proceed)

### Rule 6: Stop and Ask on Cascading Failures

- If task causes failures beyond its scope, STOP
- Report: "Task P{x}.S{y}.T{z} caused unexpected failures in {areas}"
- List failing tests/builds
- Wait for human decision before proceeding

### Rule 7: Validate Before Commit

- Run `dotnet restore && dotnet build` after every change
- Run `dotnet test` if task touches code (not just docs/config)
- Run Docker build if task touches Dockerfiles
- Only commit if validation passes

### Rule 8: Rollback on Failure

- If validation fails and fix is not obvious, execute Rollback steps
- Report failure to human
- Do not attempt creative fixes beyond task scope

### Rule 9: Track Progress

- Mark task as complete in tracking document (create if needed)
- Update `docs/blueprints/refactor-progress.md` after each task
- Format: `- [x] P{phase}.S{stage}.T{task}: {name} - {date} - {PR#}`

### Rule 10: No Reasoning Essays

- Follow task instructions mechanically
- Do not explain why you're doing something (it's in the blueprint)
- Do not suggest alternatives (blueprint is the plan)
- Report only: what you did, command outputs, pass/fail

---

## 8. Phase Summary

| Phase | Stages | Tasks | Goal | Risk |
|-------|--------|-------|------|------|
| 0 | 5 | 10 | Safety net (tests, validation, central packages) | LOW |
| 1 | 3 | 6 | Directory structure + pilot slice validation | MEDIUM |
| 2 | 4 | 7 | Dependency cleanup (Marten, MediatR, Contracts) | MEDIUM |
| 3 | 2 | 9 | Move remaining projects to Modules/Warehouse/ | HIGH |
| 4 | 7 | 21 | Rename projects + namespaces to target conventions | HIGH |
| 5 | 2 | 8 | Rename test projects | MEDIUM |
| 6 | 2 | 3 | Convert SharedKernel to BuildingBlock | LOW |
| 7 | 2 | 5 | Update CI/CD workflows | MEDIUM |
| 8 | 1 | 4 | Update documentation | LOW |
| **Total** | **28** | **73** | **Complete refactor to modular structure** | **MANAGED** |

---

## 9. Validation Checklist (Final)

After completing all phases, verify:

- [ ] All projects in `src/Modules/Warehouse/` with correct names
- [ ] All tests in `tests/Modules/Warehouse/` with correct names
- [ ] BuildingBlocks projects in `src/BuildingBlocks/`
- [ ] ArchitectureTests project exists and passes
- [ ] `Directory.Packages.props` at repo root
- [ ] `scripts/validate-module-dependencies.sh` exists and passes
- [ ] `deploy/traefik/dynamic.yml` exists
- [ ] No stale files at repo root
- [ ] All namespaces follow `LKvitai.MES.Modules.Warehouse.*` pattern
- [ ] All namespaces follow `LKvitai.MES.BuildingBlocks.*` pattern
- [ ] Domain has no tech dependencies
- [ ] Application has no Infrastructure reference
- [ ] Application has no Marten dependency
- [ ] Contracts has zero dependencies
- [ ] SharedKernel has no MediatR dependency
- [ ] BuildingBlocks do not reference Modules
- [ ] All Dockerfiles build successfully
- [ ] All CI workflows reference correct paths
- [ ] `dotnet restore && dotnet build` succeeds
- [ ] `dotnet test` succeeds (all tests pass)
- [ ] Docker builds succeed for Api and Ui
- [ ] Architecture tests pass
- [ ] Dependency validation script passes
- [ ] Documentation updated (README, architecture docs)

---

## 10. Next Steps After Refactor

Once this blueprint is complete:

1. **DB-per-Service Preparation** (Phase 9, not in this blueprint)
   - Audit entities in WarehouseDbContext
   - Identify cross-module entities (SalesOrder, Customer)
   - Plan extraction of Sales module
   - Prepare connection string configuration for multiple DBs

2. **Extract Sales Module** (use Module Extraction Playbook)
   - Create `src/Modules/Sales/` structure
   - Move SalesOrder, Customer entities
   - Create Sales.Contracts for integration events
   - Update Warehouse to consume Sales events

3. **Add More BuildingBlocks** (as needed)
   - `BuildingBlocks.Messaging.RabbitMQ` (MassTransit wrapper)
   - `BuildingBlocks.Auth.Oidc` (JwtBearer config)
   - `BuildingBlocks.Observability` (OpenTelemetry setup)
   - `BuildingBlocks.ProblemDetails` (error handling)

4. **Traefik Configuration** (deployment)
   - Implement `deploy/traefik/dynamic.yml` with real routing
   - Configure path-based routing (`/api/warehouse`, `/api/sales`)
   - Set up health checks and load balancing

5. **Production Readiness**
   - Add monitoring dashboards per module
   - Implement circuit breakers for cross-module calls
   - Set up distributed tracing
   - Document runbooks per module

---

**End of Blueprint**

**Execution Start:** After human approval  
**Executor:** Codex (automated task agent)  
**Reviewer:** Human (PR review after each task or phase)
