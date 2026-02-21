# Post-Refactor Audit: LKvitai.MES

**Date:** 2026-02-21
**Auditor:** Claude Code (independent review)
**Branch:** `refactor/modular-blueprint`
**Scope:** Full repo audit against target modular structure and best practices

---

## A) Executive Summary

**Overall verdict: PASS WITH WARNINGS**

The refactor from flat `LKvitai.MES.<Layer>` to modular `LKvitai.MES.Modules.<Module>.<Layer>` is substantially complete. Directory structure, solution file, test layout, CI/CD workflows, Central Package Management, and architecture enforcement are all in place. However, several stale references and naming inconsistencies remain from the migration.

**Top 5 Risks:**

1. **BLOCKER:** Api Dockerfile references non-existent `src/LKvitai.MES.SharedKernel/` path (lines 19, 34) — Docker build will fail
2. **BLOCKER:** Root `docker-compose.yml` references non-existent `src/LKvitai.MES.Api/Dockerfile` (lines 27, 48, 69) — `docker compose build` will fail
3. **HIGH:** WebUI project not renamed to `LKvitai.MES.Modules.Warehouse.Ui` — naming inconsistency breaks convention; `_Imports.razor` has mixed old/new namespaces
4. **HIGH:** Projections csproj missing `RootNamespace`/`AssemblyName` — implicit namespace derived from folder path, inconsistent with all other module projects
5. **MED:** Architecture tests are stubs — Domain and Application layer tests are `Skip`'d with `Assert.True(true)`, providing zero actual enforcement

---

## B) Original Recommendations vs Current State

| Recommendation | Implemented? | Evidence | Notes/Risks |
|---------------|-------------|----------|-------------|
| Projects under `src/Modules/Warehouse/` | **Yes** | 9 projects in `src/Modules/Warehouse/` | WebUI not renamed per convention |
| Projects under `src/BuildingBlocks/` | **Yes** | `Cqrs.Abstractions` + `SharedKernel` in `src/BuildingBlocks/` | SharedKernel moved from `src/` root |
| Tests at repo root `tests/` | **Yes** | 4 test projects + ArchitectureTests under `tests/` | Correctly relocated from `src/tests/` |
| Module-scoped naming `LKvitai.MES.Modules.Warehouse.<Layer>` | **Partial** | 8/9 projects renamed correctly | WebUI still `LKvitai.MES.WebUI` |
| `RootNamespace`/`AssemblyName` explicit in csproj | **Partial** | 10/12 module+BB projects have explicit | Api, Projections, WebUI missing |
| Contracts: zero dependencies | **Yes** | `Contracts.csproj` has 0 PackageRef, 0 ProjectRef | Fully pure |
| Domain: no tech deps | **Yes** | `Domain.csproj` has 0 PackageRef | Only refs SharedKernel + Contracts |
| SharedKernel: no MediatR | **Yes** | `SharedKernel.csproj` refs only Cqrs.Abstractions | MediatR extracted to Cqrs.Abstractions |
| Application: no Marten | **Yes** | `Application.csproj` has MediatR, FluentValidation, Logging only | Marten fully in Infrastructure |
| `Directory.Packages.props` at repo root | **Yes** | 50 packages centrally managed | No `Version=` in any csproj |
| `Directory.Build.props` sane | **Yes** | Root: CPM enable; `src/`: LangVersion, warnings | Two files, non-conflicting |
| ArchitectureTests project | **Yes** | `tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/` | Tests are stubs (see findings) |
| `validate-module-dependencies.sh` | **Yes** | `scripts/validate-module-dependencies.sh` (uses `rg`) | Also `tools/DependencyValidator/` |
| CI architecture-checks workflow | **Yes** | `.github/workflows/architecture-checks.yml` on PR | Runs both script + arch tests |
| Docker compose v2 in CI | **Yes** | All workflows use `docker compose` (space) | `src/docker-compose.yml` still has v1 `version:` |
| docker-compose.test.yml at repo root | **Yes** | `docker-compose.test.yml` at root | Correctly used by deploy-test.yml |
| RabbitMQ optional (profiles) | **Yes** | `src/docker-compose.yml` uses `profiles: ["dev-broker"]` | RabbitMQ only starts with `--profile dev-broker` |
| `deploy/traefik/dynamic.yml` | **Yes** | `deploy/traefik/dynamic.yml` exists | |
| `SOLUTION_STRUCTURE.md` up to date | **Yes** | `src/SOLUTION_STRUCTURE.md` reflects modular layout | Includes dependency graph |
| Old audit archived | **Yes** | `docs/repo-audit/2026-02-16-repo-audit-vs-target.md` marked RESOLVED | Completion note at `2026-02-19-refactor-completion.md` |

---

## C) Findings (Grouped)

### Structure & Naming

#### F1. BLOCKER — Api Dockerfile references non-existent SharedKernel path

- **Severity:** BLOCKER
- **Evidence:** `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile` lines 19, 34
  ```dockerfile
  COPY src/LKvitai.MES.SharedKernel/LKvitai.MES.SharedKernel.csproj LKvitai.MES.SharedKernel/
  COPY src/LKvitai.MES.SharedKernel/ LKvitai.MES.SharedKernel/
  ```
- **Why it matters:** `src/LKvitai.MES.SharedKernel/` no longer exists — it was moved to `src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/`. `docker build` will fail with COPY error.
- **Suggested fix:** Update lines 19 and 34 to:
  ```dockerfile
  COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/LKvitai.MES.BuildingBlocks.SharedKernel.csproj BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
  COPY src/BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/ BuildingBlocks/LKvitai.MES.BuildingBlocks.SharedKernel/
  ```

#### F2. BLOCKER — Root docker-compose.yml references non-existent Dockerfile path

- **Severity:** BLOCKER
- **Evidence:** `docker-compose.yml` (repo root) lines 27, 48, 69
  ```yaml
  dockerfile: src/LKvitai.MES.Api/Dockerfile
  ```
- **Why it matters:** `src/LKvitai.MES.Api/` does not exist. The Dockerfile is now at `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile`. `docker compose build` will fail.
- **Suggested fix:** Change all 3 occurrences to:
  ```yaml
  dockerfile: src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile
  ```

#### F3. HIGH — WebUI project not renamed to module convention

- **Severity:** HIGH
- **Evidence:**
  - Directory: `src/Modules/Warehouse/LKvitai.MES.WebUI/` (should be `LKvitai.MES.Modules.Warehouse.Ui/`)
  - csproj: `LKvitai.MES.WebUI.csproj` — no `RootNamespace` or `AssemblyName` set
  - Sln line 34: display name `LKvitai.MES.WebUI`, path `Modules\Warehouse\LKvitai.MES.WebUI\LKvitai.MES.WebUI.csproj`
  - WebUI Dockerfile ENTRYPOINT: `LKvitai.MES.WebUI.dll`
- **Why it matters:** All other module projects follow `LKvitai.MES.Modules.Warehouse.<Layer>` convention. WebUI breaks the pattern, making tooling and discovery inconsistent.
- **Suggested fix:** Rename directory, csproj, set explicit `RootNamespace`/`AssemblyName`, update all references.

#### F4. HIGH — `_Imports.razor` has mixed old and new namespaces

- **Severity:** HIGH
- **Evidence:** `src/Modules/Warehouse/LKvitai.MES.WebUI/_Imports.razor`
  ```razor
  @using LKvitai.MES.WebUI                              (line 8 — OLD)
  @using LKvitai.MES.WebUI.Components                   (line 9 — OLD)
  @using LKvitai.MES.WebUI.Components.Dashboard         (line 10 — OLD)
  @using LKvitai.MES.WebUI.Components.Stock             (line 11 — OLD)
  @using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure  (line 12 — NEW)
  @using LKvitai.MES.Modules.Warehouse.WebUI.Models          (line 13 — NEW)
  @using LKvitai.MES.Modules.Warehouse.WebUI.Services        (line 14 — NEW)
  @using LKvitai.MES.WebUI.Shared                       (line 15 — OLD)
  ```
- **Why it matters:** 5 of 8 `@using` directives use old namespace, 3 use a partially-new namespace `LKvitai.MES.Modules.Warehouse.WebUI.*`. Neither matches the target convention. This may cause compile warnings or failures depending on which namespace the classes actually reside in.
- **Suggested fix:** Unify all `@using` directives to the same namespace pattern once WebUI is fully renamed.

#### F5. MED — Projections and Api csproj missing explicit RootNamespace/AssemblyName

- **Severity:** MED
- **Evidence:**
  - `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Projections/LKvitai.MES.Modules.Warehouse.Projections.csproj` — no `<RootNamespace>` or `<AssemblyName>`
  - `src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/LKvitai.MES.Modules.Warehouse.Api.csproj` — no `<RootNamespace>` or `<AssemblyName>`
- **Why it matters:** Implicit defaults derive from folder/csproj name which is correct here, but 8 other module projects explicitly set them. Inconsistency may confuse future contributors. For Projections specifically, if the assembly/namespace defaults ever drift, event sourcing projections will silently break.
- **Suggested fix:** Add explicit `<RootNamespace>` and `<AssemblyName>` to both csproj files for consistency.

#### F6. LOW — Solution display names inconsistent

- **Severity:** LOW
- **Evidence:** `src/LKvitai.MES.sln` lines 10-24
  - Some use short display names: `LKvitai.MES.Api`, `LKvitai.MES.Domain`, `LKvitai.MES.Contracts`
  - Some use full module names: `LKvitai.MES.Modules.Warehouse.Sagas`, `LKvitai.MES.Modules.Warehouse.Integration`
  - Mixed convention in the same solution file
- **Why it matters:** IDE experience is confusing — some projects show short names, others show full module-qualified names. Not a build issue.
- **Suggested fix:** Standardize all display names to either short or full form.

#### F7. LOW — ArchitectureTests not nested under "tests" solution folder

- **Severity:** LOW
- **Evidence:** `src/LKvitai.MES.sln` line 265 — ArchitectureTests GUID `{D1280BF0...}` is NOT in the `NestedProjects` section, meaning it appears at solution root in IDE instead of under the "tests" folder.
- **Suggested fix:** Add `{D1280BF0-1F6F-47B5-9C00-9BB86F970AB8} = {B2C3D4E5-F6A7-8901-BCDE-F12345678901}` to NestedProjects.

### Dependency Rules / Layering

#### F8. PASS — Contracts is fully pure

- **Evidence:** `LKvitai.MES.Modules.Warehouse.Contracts.csproj` — 0 PackageReference, 0 ProjectReference.
- Original audit found Contracts depending on SharedKernel. This has been **fully resolved**.

#### F9. PASS — Domain has no tech dependencies

- **Evidence:** `LKvitai.MES.Modules.Warehouse.Domain.csproj` — 0 PackageReference. Only references SharedKernel and Contracts.

#### F10. PASS — Application does not reference Marten

- **Evidence:** `LKvitai.MES.Modules.Warehouse.Application.csproj` — PackageReferences are MediatR, FluentValidation, FluentValidation.DependencyInjectionExtensions, Microsoft.Extensions.Logging.Abstractions. No Marten.
- Original audit found Application depending on Marten directly. This has been **fully resolved**.

#### F11. PASS — SharedKernel does not reference MediatR

- **Evidence:** `LKvitai.MES.BuildingBlocks.SharedKernel.csproj` — 0 PackageReference. Only ProjectReference to Cqrs.Abstractions.
- MediatR was extracted to `BuildingBlocks.Cqrs.Abstractions`. **Fully resolved**.

#### F12. PASS — BuildingBlocks do not reference Modules

- **Evidence:** Both BuildingBlocks csproj files have no references containing "Modules". Verified by `validate-module-dependencies.sh` and `BuildingBlocksLayerTests.cs`.

#### F13. PASS — No cyclic references detected

- **Evidence:** Dependency graph in `SOLUTION_STRUCTURE.md` shows a clean DAG. All ProjectReferences flow downward through the layer stack.

### Packages & CPM

#### F14. PASS — Central Package Management correctly implemented

- **Evidence:**
  - `Directory.Packages.props` at repo root with 50 `<PackageVersion>` entries
  - Root `Directory.Build.props` enables `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
  - No `Version=` attributes found in any csproj PackageReference
  - `src/Directory.Build.props` handles `LangVersion`, `TreatWarningsAsErrors`, `GenerateDocumentationFile` — no conflict with root

#### F15. LOW — E2E test project has no ProjectReferences

- **Severity:** LOW
- **Evidence:** `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.E2E/LKvitai.MES.Tests.Warehouse.E2E.csproj` — 0 ProjectReferences, only test framework packages.
- **Why it matters:** E2E tests presumably test via HTTP/process-level, so no project refs may be intentional. However, without any source project reference, the test project cannot access shared types/fixtures.
- **Inference:** Likely intentional for true black-box E2E testing.

### CI/Workflows

#### F16. PASS — Architecture checks wired into CI

- **Evidence:** `.github/workflows/architecture-checks.yml` — triggers on PR to main, runs `DependencyValidator`, `validate-module-dependencies.sh`, and architecture tests.

#### F17. PASS — Workflows use docker compose v2 syntax

- **Evidence:** All 6 workflow files use `docker compose` (with space). No hyphenated `docker-compose` (v1) found in workflows.

#### F18. MED — `validate-module-dependencies.sh` requires `rg` (ripgrep)

- **Severity:** MED
- **Evidence:** `scripts/validate-module-dependencies.sh` line 8: `violations="$(rg -n --glob '*.csproj' ...)"`
- **Why it matters:** `rg` is not installed by default on Ubuntu runners. The `architecture-checks.yml` workflow runs this on `ubuntu-latest` which does NOT have ripgrep pre-installed. The CI step will fail.
- **Suggested fix:** Either install `rg` in the workflow step, or replace with `grep -rn` which is available everywhere.

#### F19. LOW — `warehouse-ci.yml` path filter misses BuildingBlocks changes

- **Severity:** LOW
- **Evidence:** `.github/workflows/warehouse-ci.yml` lines 6-8:
  ```yaml
  paths:
    - src/Modules/Warehouse/**
    - tests/Modules/Warehouse/**
  ```
- **Why it matters:** Changes to `src/BuildingBlocks/` won't trigger this CI workflow, even though all Warehouse projects depend on BuildingBlocks.
- **Suggested fix:** Add `src/BuildingBlocks/**` to the paths filter.

### Docker / Compose / Dev Experience

#### F20. (F1 + F2 above) — See BLOCKER findings for Dockerfile and docker-compose.yml stale paths.

#### F21. MED — `src/docker-compose.yml` uses deprecated `version: '3.8'`

- **Severity:** MED
- **Evidence:** `src/docker-compose.yml` line 1: `version: '3.8'`
- **Why it matters:** Docker Compose v2 ignores the `version` field and emits a deprecation warning. Root-level compose files correctly omit it.
- **Suggested fix:** Remove the `version: '3.8'` line.

#### F22. LOW — Two separate dev compose files create confusion

- **Severity:** LOW
- **Evidence:** Both `docker-compose.yml` (repo root) and `src/docker-compose.yml` exist.
  - Root: 3 API instances + nginx + redis + grafana + postgres (full stack)
  - src/: postgres + rabbitmq (optional) + jaeger (dev services)
- **Why it matters:** Developers may not know which to use. Root compose references old Dockerfile path (see F2).
- **Suggested fix:** Document the intended workflow in README or consolidate.

### Tests

#### F23. HIGH — Architecture tests are non-functional stubs

- **Severity:** HIGH
- **Evidence:**
  - `DomainLayerTests.cs`: `[Fact(Skip = "Known violation")] ... Assert.True(true);`
  - `ApplicationLayerTests.cs`: `[Fact(Skip = "Known violation")] ... Assert.True(true);`
  - `ContractsLayerTests.cs`: `[Fact] ... Assert.True(true);` (not skipped but still a no-op)
  - Only `BuildingBlocksLayerTests.cs` has a real implementation with XML parsing
- **Why it matters:** 3 of 4 test files provide zero enforcement. The original violations (Domain→Infrastructure, Application→Marten) have been **fixed** in the csproj files, but the tests haven't been un-skipped and upgraded to use NetArchTest.Rules like BuildingBlocksLayerTests does. The CI "architecture checks" step runs these tests but they all trivially pass.
- **Suggested fix:** Implement real NetArchTest-based assertions now that the violations are resolved. Remove the `Skip` attributes. Match the pattern from `BuildingBlocksLayerTests.cs`.

#### F24. LOW — `UnitTest1.cs` and `IntegrationTest1.cs` are likely scaffolding leftovers

- **Severity:** LOW
- **Evidence:**
  - `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/UnitTest1.cs`
  - `tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/IntegrationTest1.cs`
- **Suggested fix:** Delete if they contain only `dotnet new` boilerplate.

### Docs / Maintainability

#### F25. PASS — SOLUTION_STRUCTURE.md reflects reality

- **Evidence:** `src/SOLUTION_STRUCTURE.md` accurately shows the modular layout including BuildingBlocks, Modules/Warehouse, test structure, dependency graph (mermaid), and build commands. Minor note: shows `LKvitai.MES.WebUI` which is the current actual state (not yet renamed).

#### F26. PASS — Old audit archived with RESOLVED note

- **Evidence:** `docs/repo-audit/2026-02-16-repo-audit-vs-target.md` line 10: `> RESOLVED: See docs/blueprints/repo-refactor-blueprint.md and docs/repo-audit/2026-02-19-refactor-completion.md`

#### F27. PASS — Completion status documented

- **Evidence:** `docs/repo-audit/2026-02-19-refactor-completion.md` describes what was done, validation gates run, and notes on branch context.

---

## D) Quick Command Checklist

Commands a human can run to validate key claims. All are non-destructive.

```bash
# 1. Verify no PackageReference Version= attributes remain (should return 0 matches)
grep -rn 'PackageReference.*Version=' --include='*.csproj' src/ tests/

# 2. Verify no old flat paths in csproj ProjectReferences
grep -rn 'src/LKvitai.MES\.' --include='*.csproj' src/ tests/

# 3. Verify Dockerfile stale SharedKernel path (should show the problem)
grep -n 'LKvitai.MES.SharedKernel' src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Api/Dockerfile

# 4. Verify root docker-compose.yml stale Dockerfile paths
grep -n 'LKvitai.MES.Api/Dockerfile' docker-compose.yml

# 5. Verify BuildingBlocks do not reference Modules
grep -rn 'Modules' --include='*.csproj' src/BuildingBlocks/

# 6. Verify Contracts has zero dependencies
grep -c 'Reference' src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Contracts/LKvitai.MES.Modules.Warehouse.Contracts.csproj

# 7. Verify Application does not reference Marten
grep -i 'marten' src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/LKvitai.MES.Modules.Warehouse.Application.csproj

# 8. Check for deprecated docker compose version syntax
grep -rn "^version:" src/docker-compose.yml docker-compose*.yml

# 9. Check _Imports.razor for mixed namespaces
grep -n 'using LKvitai' src/Modules/Warehouse/LKvitai.MES.WebUI/_Imports.razor

# 10. List projects missing explicit RootNamespace (should be empty for full compliance)
for f in $(find src -name '*.csproj'); do grep -qL 'RootNamespace' "$f" && echo "MISSING RootNamespace: $f"; done

# 11. Verify architecture-checks workflow exists and triggers on PR
head -10 .github/workflows/architecture-checks.yml

# 12. Verify rg availability for CI (will fail if not installed)
which rg || echo "rg (ripgrep) not found — validate-module-dependencies.sh will fail in CI"
```

---

## Summary Counts

| Severity | Count | Details |
|----------|-------|---------|
| BLOCKER | 2 | F1 (Dockerfile SharedKernel path), F2 (docker-compose.yml Dockerfile path) |
| HIGH | 3 | F3 (WebUI not renamed), F4 (_Imports.razor mixed namespaces), F23 (arch tests are stubs) |
| MED | 3 | F5 (missing RootNamespace), F18 (rg dependency in CI), F21 (compose v1 version syntax) |
| LOW | 5 | F6 (sln display names), F7 (ArchTests nesting), F15 (E2E no project refs), F19 (CI path filter), F22 (two compose files), F24 (scaffold files) |
| PASS | 13 | F8-F14 (deps/layering/CPM), F16-F17 (CI), F25-F27 (docs) |
