# Repository Audit: LKvitai.MES vs Target Module Structure

**Date:** 2026-02-16
**Auditor:** Claude Code (automated)
**Target spec:** `lkvitai-revised-module-structure.md` (Denis, 2026-02-16)
**Repository:** LKvitai.MES (`main` branch, commit `74f2aae`)

---

## 1. Executive Summary

- **Overall compliance: LOW.** The repo uses a flat clean-architecture layout (`LKvitai.MES.<Layer>`), not the target modular layout (`LKvitai.MES.Modules.<Module>.<Layer>`).
- All 10 source projects use generic, non-module-scoped names (e.g., `LKvitai.MES.Domain` instead of `LKvitai.MES.Modules.Warehouse.Domain`).
- No `src/Modules/` directory exists. No `src/BuildingBlocks/` directory exists.
- No `deploy/` or `infra/` directory exists. Traefik file-provider config is absent.
- Tests live under `src/tests/` (target: `tests/` at repo root). No `ArchitectureTests` project exists.
- `Directory.Packages.props` does not exist (no central package management).
- Single shared database: both EF Core (`public` schema) and Marten (`warehouse_events` schema) use the same `WarehouseDb` connection string.
- `src/docker-compose.yml` deploys RabbitMQ locally, violating the "client infra only" constraint.
- No Authentik manifests found (compliant on this point).
- Dockerfiles hardcode all 9 project paths (will break on rename/restructure).
- GH Actions workflow `deploy.yml` hardcodes specific test filter strings.
- No `scripts/validate-module-dependencies.sh` enforcement script exists.
- Contracts project depends on SharedKernel (not pure DTOs per target spec).
- Application layer depends on Marten directly (tech leak into Application).
- SharedKernel depends on MediatR (not zero-dependency).
- Root is cluttered: `CONCURRENCY_BUG_ANALYSIS.md`, `CONCURRENCY_BUG_FIX_SUMMARY.md`, `MARTEN_V2_VERSIONING_FIX.md` are at repo root.
- Docs directory is flat and cluttered — target expects `docs/architecture/` and `docs/warehouse/runbooks/`.
- No cycles detected in ProjectReference graph.

---

## 2. As-Is Repository Tree

```
LKvitai.MES/                          # REPO ROOT
├── .github/workflows/
│   ├── build-and-push.yml
│   ├── deploy-test.yml
│   └── deploy.yml
├── .kiro/specs/warehouse-core-phase1/  # other-AI specs
├── .tools/.store/                      # local tool store
├── .vscode/
├── docs/
│   ├── 01-discovery.md
│   ├── 02-warehouse-domain-model.md
│   ├── 03-implementation-guide.md
│   ├── 04-system-architecture.md
│   ├── adr/                            # 3 ADRs
│   ├── claude/
│   ├── compliance/
│   ├── dependency-map.md
│   ├── dev-auth-guide.md
│   ├── dev-db-update.md
│   ├── master-data/                    # 13 files
│   ├── prod-ready/                     # 74 files
│   ├── repo-structure-audit.md         # previous audit
│   ├── security/                       # 5 files
│   ├── spec/                           # 5 files (10-50)
│   └── ui/
├── scripts/
│   ├── load/
│   ├── master-data-operational-smoke.sh
│   ├── seed-master-data.sql
│   └── validate-schema.sh
├── src/
│   ├── LKvitai.MES.sln
│   ├── docker-compose.yml              # DEV compose (includes RabbitMQ)
│   ├── Directory.Build.props
│   ├── LKvitai.MES.Api/               # ASP.NET Core API entrypoint
│   ├── LKvitai.MES.Application/       # Commands, Queries, Services
│   ├── LKvitai.MES.Contracts/         # Events, Messages, ReadModels
│   ├── LKvitai.MES.Domain/            # Aggregates, Entities
│   ├── LKvitai.MES.Infrastructure/    # Persistence, Outbox, Projections
│   ├── LKvitai.MES.Integration/       # Agnum, Carrier, LabelPrinting
│   ├── LKvitai.MES.Projections/       # Marten read-model projections
│   ├── LKvitai.MES.Sagas/             # MassTransit sagas
│   ├── LKvitai.MES.SharedKernel/      # Base types
│   ├── LKvitai.MES.WebUI/             # Blazor Server UI entrypoint
│   └── tests/
│       ├── LKvitai.MES.Tests.Integration/
│       ├── LKvitai.MES.Tests.Property/
│       └── LKvitai.MES.Tests.Unit/
├── CLAUDE.md
├── CONCURRENCY_BUG_ANALYSIS.md         # should move to docs/
├── CONCURRENCY_BUG_FIX_SUMMARY.md      # should move to docs/
├── MARTEN_V2_VERSIONING_FIX.md         # should move to docs/
├── docker-compose.test.yml
├── .dockerignore
└── .gitignore
```

---

## 3. As-Is Project List

### 3.1 All Projects

| # | Project | Path | RootNamespace | AssemblyName |
|---|---------|------|---------------|--------------|
| 1 | Api | `src/LKvitai.MES.Api/` | LKvitai.MES.Api | LKvitai.MES.Api |
| 2 | Application | `src/LKvitai.MES.Application/` | LKvitai.MES.Application | LKvitai.MES.Application |
| 3 | Contracts | `src/LKvitai.MES.Contracts/` | LKvitai.MES.Contracts | LKvitai.MES.Contracts |
| 4 | Domain | `src/LKvitai.MES.Domain/` | LKvitai.MES.Domain | LKvitai.MES.Domain |
| 5 | Infrastructure | `src/LKvitai.MES.Infrastructure/` | LKvitai.MES.Infrastructure | LKvitai.MES.Infrastructure |
| 6 | Integration | `src/LKvitai.MES.Integration/` | LKvitai.MES.Integration | LKvitai.MES.Integration |
| 7 | Projections | `src/LKvitai.MES.Projections/` | LKvitai.MES.Projections | LKvitai.MES.Projections |
| 8 | Sagas | `src/LKvitai.MES.Sagas/` | LKvitai.MES.Sagas | LKvitai.MES.Sagas |
| 9 | SharedKernel | `src/LKvitai.MES.SharedKernel/` | LKvitai.MES.SharedKernel | LKvitai.MES.SharedKernel |
| 10 | WebUI | `src/LKvitai.MES.WebUI/` | LKvitai.MES.WebUI | LKvitai.MES.WebUI |
| 11 | Tests.Unit | `src/tests/LKvitai.MES.Tests.Unit/` | LKvitai.MES.Tests.Unit | LKvitai.MES.Tests.Unit |
| 12 | Tests.Integration | `src/tests/LKvitai.MES.Tests.Integration/` | LKvitai.MES.Tests.Integration | LKvitai.MES.Tests.Integration |
| 13 | Tests.Property | `src/tests/LKvitai.MES.Tests.Property/` | LKvitai.MES.Tests.Property | LKvitai.MES.Tests.Property |

*Note:* None set explicit `<RootNamespace>` or `<AssemblyName>` in csproj; they default to the project directory name.

### 3.2 ProjectReference Graph (Outgoing)

| Project | References |
|---------|------------|
| **SharedKernel** | *(none)* |
| **Contracts** | SharedKernel |
| **Domain** | SharedKernel, Contracts |
| **Application** | Domain, Contracts, SharedKernel |
| **Infrastructure** | Domain, Contracts, Application |
| **Integration** | Contracts |
| **Projections** | Contracts, Domain |
| **Sagas** | Contracts, Application |
| **Api** | Application, Infrastructure, Projections, Sagas, Integration |
| **WebUI** | *(none)* |
| Tests.Unit | Domain, Application, Contracts, Projections, Infrastructure, Sagas, Api, WebUI |
| Tests.Integration | Api, Infrastructure, Projections, Domain, Application, Contracts |
| Tests.Property | Domain, Application |

### 3.3 Reverse Dependencies (Who Depends On Me)

| Project | Depended On By |
|---------|----------------|
| **SharedKernel** | Contracts, Domain, Application |
| **Contracts** | Domain, Application, Infrastructure, Integration, Projections, Sagas + all tests |
| **Domain** | Application, Infrastructure, Projections + all tests |
| **Application** | Infrastructure, Sagas, Api + Tests.Unit, Tests.Integration, Tests.Property |
| **Infrastructure** | Api + Tests.Unit, Tests.Integration |
| **Integration** | Api |
| **Projections** | Api + Tests.Unit, Tests.Integration |
| **Sagas** | Api + Tests.Unit |
| **Api** | Tests.Unit, Tests.Integration |
| **WebUI** | Tests.Unit |

---

## 4. Dependency Graph

### 4.1 Visual Dependency Flow

```
SharedKernel
  ├── Contracts
  │     ├── Domain
  │     │     ├── Application
  │     │     │     ├── Infrastructure
  │     │     │     │     └── Api (entrypoint)
  │     │     │     └── Sagas
  │     │     │           └── Api (entrypoint)
  │     │     └── Projections
  │     │           └── Api (entrypoint)
  │     └── Integration
  │           └── Api (entrypoint)
  └── Domain (also refs SharedKernel directly)

  WebUI (standalone, no ProjectReference to other src projects)
```

### 4.2 Cycle Analysis

**ProjectReference cycles: NONE detected.**

The dependency graph is a strict DAG (directed acyclic graph). No circular references exist.

### 4.3 Notable Anomalies

| Finding | Evidence | Severity |
|---------|----------|----------|
| Application depends on Marten (tech in app layer) | `src/LKvitai.MES.Application/LKvitai.MES.Application.csproj` — `PackageReference Include="Marten"` | HIGH |
| SharedKernel depends on MediatR | `src/LKvitai.MES.SharedKernel/LKvitai.MES.SharedKernel.csproj` — `PackageReference Include="MediatR"` | MEDIUM |
| Domain depends on Contracts (reverse of typical DDD) | `src/LKvitai.MES.Domain/LKvitai.MES.Domain.csproj` — `ProjectReference: Contracts` | MEDIUM |
| Projections depend on Domain directly | `src/LKvitai.MES.Projections/LKvitai.MES.Projections.csproj` — `ProjectReference: Domain` | LOW |
| WebUI has zero ProjectReferences | `src/LKvitai.MES.WebUI/LKvitai.MES.WebUI.csproj` — no `<ProjectReference>` | INFO |

---

## 5. Namespace Audit

### 5.1 Generic (Non-Module-Scoped) Namespaces

**ALL 45 namespaces are generic.** None follow the target `LKvitai.MES.Modules.Warehouse.*` pattern.

Current top-level namespaces:
- `LKvitai.MES.Api.*` (6 sub-namespaces)
- `LKvitai.MES.Application.*` (10 sub-namespaces)
- `LKvitai.MES.Contracts.*` (3 sub-namespaces)
- `LKvitai.MES.Domain.*` (3 sub-namespaces)
- `LKvitai.MES.Infrastructure.*` (7 sub-namespaces)
- `LKvitai.MES.Integration.*` (3 sub-namespaces)
- `LKvitai.MES.Projections` (1 namespace, flat)
- `LKvitai.MES.Sagas` (1 namespace, flat)
- `LKvitai.MES.SharedKernel` (1 namespace, flat)
- `LKvitai.MES.WebUI.*` (4 sub-namespaces)

### 5.2 Domain Mixing Hotspots

All warehouse-specific domain concepts (StockLedger, HandlingUnit, Reservation, Valuation, WarehouseLayout) plus sales entities (SalesOrder, Customer) and master data entities coexist in:

| Namespace | Contains | Issue |
|-----------|----------|-------|
| `LKvitai.MES.Domain.Aggregates` | StockLedger, HandlingUnit, Reservation, Valuation, WarehouseLayout, ItemValuation | All aggregates in one namespace |
| `LKvitai.MES.Domain.Entities` | Items, Customers, SalesOrders, SalesOrderLines, UoM, Warehouses, Locations, etc. | Warehouse + Sales entities mixed |
| `LKvitai.MES.Infrastructure.Persistence` | WarehouseDbContext with ALL entity configurations | Single DbContext for everything |
| `LKvitai.MES.Application.Commands` | Receive, Pick, Transfer, Adjust, CycleCount, Valuation, SalesOrder commands | All command types in one namespace |

---

## 6. Contracts / BuildingBlocks Audit

### 6.1 Current "Shared" Projects

| Project | Classification | Target Role | Issues |
|---------|---------------|-------------|--------|
| **SharedKernel** | Mixed (tech + base types) | Should become `BuildingBlocks.*` or dissolve | Depends on MediatR (tech leak); all modules will inherit this dependency |
| **Contracts** | Mostly DTOs | Should become `Modules.Warehouse.Contracts` | Depends on SharedKernel (violates "no deps" target for Contracts) |

### 6.2 Contracts Purity Check

**File:** `src/LKvitai.MES.Contracts/LKvitai.MES.Contracts.csproj`

```xml
<ProjectReference Include="..\LKvitai.MES.SharedKernel\LKvitai.MES.SharedKernel.csproj" />
```

**Violation:** Contracts reference SharedKernel, which pulls in MediatR. Target spec requires Contracts to be "pure records/DTOs only" with "no inheritance from tech base classes" and "prefer no dependencies at all."

### 6.3 BuildingBlocks Assessment

**No BuildingBlocks projects exist.** Target spec expects:
- `BuildingBlocks.Messaging.Abstractions`
- `BuildingBlocks.Messaging.RabbitMQ`
- `BuildingBlocks.Auth.Oidc`
- `BuildingBlocks.ProblemDetails`
- `BuildingBlocks.Observability`
- `BuildingBlocks.Testing`

Currently, messaging (MassTransit), auth, observability (OpenTelemetry), and error handling are wired directly in `Api/Program.cs`.

---

## 7. Entry Points / Deployability

### 7.1 Application Entry Points

| Entrypoint | Path | Container | Dockerfile |
|------------|------|-----------|------------|
| **Api** | `src/LKvitai.MES.Api/` | `lkvitai-mes-warehouse-api` | `src/LKvitai.MES.Api/Dockerfile` |
| **WebUI** | `src/LKvitai.MES.WebUI/` | `lkvitai-mes-warehouse-webui` | `src/LKvitai.MES.WebUI/Dockerfile` |

No Worker entrypoint exists (target allows optional `<module>-worker`).

### 7.2 Dockerfile Hardcoding

**Api Dockerfile** (`src/LKvitai.MES.Api/Dockerfile`) hardcodes paths to ALL 9 project directories:

```
COPY src/LKvitai.MES.Api/LKvitai.MES.Api.csproj LKvitai.MES.Api/
COPY src/LKvitai.MES.Application/LKvitai.MES.Application.csproj LKvitai.MES.Application/
COPY src/LKvitai.MES.Infrastructure/LKvitai.MES.Infrastructure.csproj LKvitai.MES.Infrastructure/
COPY src/LKvitai.MES.Projections/LKvitai.MES.Projections.csproj LKvitai.MES.Projections/
COPY src/LKvitai.MES.Sagas/LKvitai.MES.Sagas.csproj LKvitai.MES.Sagas/
COPY src/LKvitai.MES.Integration/LKvitai.MES.Integration.csproj LKvitai.MES.Integration/
COPY src/LKvitai.MES.Domain/LKvitai.MES.Domain.csproj LKvitai.MES.Domain/
COPY src/LKvitai.MES.Contracts/LKvitai.MES.Contracts.csproj LKvitai.MES.Contracts/
COPY src/LKvitai.MES.SharedKernel/LKvitai.MES.SharedKernel.csproj LKvitai.MES.SharedKernel/
```

**Impact:** Every project rename/move requires Dockerfile update. Both Dockerfiles must be rewritten during refactor.

**WebUI Dockerfile** only copies its own project (standalone, no project refs).

### 7.3 GitHub Actions Coupling

| Workflow | File | Coupling Issues |
|----------|------|-----------------|
| `build-and-push.yml` | `.github/workflows/build-and-push.yml` | Hardcodes Dockerfile paths; triggers on all `main` pushes (no path filters) |
| `deploy-test.yml` | `.github/workflows/deploy-test.yml` | Hardcodes `docker-compose.test.yml`, `scripts/seed-master-data.sql`, EF Core migration via `--project src/LKvitai.MES.Infrastructure --startup-project src/LKvitai.MES.Api` |
| `deploy.yml` | `.github/workflows/deploy.yml` | Hardcodes specific test class names as filter strings; hardcodes `src/tests/LKvitai.MES.Tests.Integration/LKvitai.MES.Tests.Integration.csproj` |

**Target expects:** `warehouse-ci.yml` + `architecture-checks.yml` with path-based triggers. None of these exist.

### 7.4 RabbitMQ / Authentik Deployment Manifests

| Service | Found? | Location | Violation? |
|---------|--------|----------|------------|
| **RabbitMQ** | YES | `src/docker-compose.yml` (service `rabbitmq`) | YES — target says "LKvitai integrates as a client; it does NOT deploy RabbitMQ" |
| **Authentik** | NO | *(not found anywhere)* | NO — compliant |
| **Jaeger** | YES | `src/docker-compose.yml` (service `jaeger`) | MINOR — not mentioned in target, but dev-only compose is arguably acceptable |

Note: `docker-compose.test.yml` (at repo root) does NOT include RabbitMQ — the API uses in-memory transport for test. This is partially compliant.

---

## 8. Data Ownership Signals

### 8.1 DbContext

**Single DbContext:** `WarehouseDbContext` at `src/LKvitai.MES.Infrastructure/Persistence/WarehouseDbContext.cs`

- Schema: `public` (EF Core state tables)
- Manages: HandlingUnit, WarehouseLayout, Items, Customers, SalesOrders, SalesOrderLines, UoM, Warehouses, Locations, and ~20+ other entities
- All in one context = single database assumption

### 8.2 Marten Event Store

**Configuration:** `src/LKvitai.MES.Infrastructure/Persistence/MartenConfiguration.cs`

- Schema: `warehouse_events` (separate from EF Core)
- Same connection string (`WarehouseDb`) as EF Core
- Daemon mode: `DaemonMode.Solo`
- Projections registered inline

### 8.3 Connection String Usage

**Single connection string key:** `WarehouseDb`

Resolution chain (from `src/LKvitai.MES.Api/Program.cs`):
```csharp
builder.Configuration.GetConnectionString("WarehouseDb")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Warehouse")
    ?? builder.Configuration["ConnectionStrings:WarehouseDb"];
```

**Used by:**
- EF Core (`WarehouseDbContext`)
- Marten (`MartenConfiguration`)
- Hangfire (`Hangfire.PostgreSql`)
- Direct Npgsql connections (`PostgresDistributedLock`, `PostgresBalanceGuardLock`)
- `MartenStartPickingOrchestration`

### 8.4 DB-Per-Service Readiness

| Criterion | Status | Evidence |
|-----------|--------|----------|
| Single connection string for all | YES (not ready) | All components share `WarehouseDb` |
| Single DbContext for all entities | YES (not ready) | `WarehouseDbContext` manages warehouse + sales entities |
| Schema separation | PARTIAL | EF Core = `public`, Marten = `warehouse_events` |
| Cross-concern entities in one context | YES (not ready) | SalesOrder, Customer mixed with warehouse entities |

**Verdict:** The codebase assumes a single shared database. Significant refactoring is needed to support DB-per-service.

---

## 9. Compliance Matrix

| # | Target Rule (from spec) | Compliant? | Evidence | Notes |
|---|------------------------|-----------|----------|-------|
| **2.1** | Repo root: only `docs/`, `deploy/`, `scripts/`, `src/`, `tests/`, meta files | **NO** | Root has `CONCURRENCY_BUG_ANALYSIS.md`, `CONCURRENCY_BUG_FIX_SUMMARY.md`, `MARTEN_V2_VERSIONING_FIX.md`, `docker-compose.test.yml` | 3 stale .md files + compose at root |
| **2.2** | `src/Modules/Warehouse/` directory structure | **NO** | All projects at `src/LKvitai.MES.<Layer>/` (flat) | No Modules directory exists |
| **2.3** | `src/BuildingBlocks/` directory | **NO** | Does not exist | No BuildingBlocks projects at all |
| **2.4** | `tests/` at repo root (not `src/tests/`) | **NO** | Tests at `src/tests/` | Wrong location |
| **2.5** | `tests/ArchitectureTests/` project | **NO** | Does not exist | No architecture enforcement |
| **2.6** | `tests/Modules/Warehouse/` test layout | **NO** | Tests at `src/tests/LKvitai.MES.Tests.*` | Wrong naming + location |
| **2.7** | `deploy/traefik/dynamic.yml` | **NO** | No `deploy/` directory | No Traefik file-provider config |
| **2.8** | `Directory.Packages.props` at root | **NO** | Does not exist | No central package management |
| **3.1** | Module project naming: `LKvitai.MES.Modules.<M>.<Layer>` | **NO** | All projects named `LKvitai.MES.<Layer>` | 10 projects need renaming |
| **3.2** | Module namespace: `LKvitai.MES.Modules.<M>.<Layer>` | **NO** | All 45 namespaces use `LKvitai.MES.<Layer>.*` | Massive namespace rename needed |
| **3.3** | No generic namespaces like `LKvitai.MES.Domain` | **NO** | `LKvitai.MES.Domain`, `LKvitai.MES.Application`, etc. | All current namespaces are generic |
| **4.1** | Domain: no deps on Infra, ASP.NET, EF, Marten, MassTransit | **YES** | Domain csproj has no tech PackageRefs | Compliant |
| **4.2** | Application: no dep on Infrastructure | **YES** | Application csproj has no ref to Infrastructure | Compliant (but has Marten — see 4.3) |
| **4.3** | Application: no tech deps (Marten, etc.) | **NO** | `PackageReference Include="Marten" Version="7.0.0"` in Application.csproj | Marten leaks into Application layer |
| **4.4** | Contracts: pure DTOs, no tech deps | **NO** | `ProjectReference: SharedKernel` (which pulls MediatR) | SharedKernel → MediatR chain |
| **4.5** | BuildingBlocks: never reference modules | **N/A** | BuildingBlocks don't exist yet | Cannot evaluate |
| **4.6** | Cross-module: only via Contracts | **N/A** | Only one module exists | Cannot evaluate (but Sales entities in Warehouse Domain is a concern) |
| **5.1** | DB-per-service: each module owns its own DB | **NO** | Single `WarehouseDb` connection string, single `WarehouseDbContext` | Shared DB assumption throughout |
| **6.1** | RabbitMQ: not deployed by LKvitai | **NO** | `src/docker-compose.yml` defines `rabbitmq` service | Dev compose deploys RabbitMQ |
| **6.2** | Integration events in Contracts | **YES** | Events in `LKvitai.MES.Contracts.Events` | Compliant |
| **7.1** | Authentik: not deployed by LKvitai | **YES** | No Authentik manifests found | Compliant |
| **7.2** | Auth: IdP-agnostic, standard JwtBearer | **PARTIAL** | `Otp.NET`, `QRCoder` in Api suggest custom 2FA | Need to verify if JwtBearer is primary |
| **8.1** | Traefik: file provider only, no Docker labels | **N/A** | No Traefik config exists at all | Not implemented |
| **9.1** | CI: `warehouse-ci.yml` with path triggers | **NO** | `build-and-push.yml` triggers on all main pushes | No path filtering |
| **9.2** | CI: `architecture-checks.yml` | **NO** | Does not exist | No architecture gate |
| **10.1** | `scripts/validate-module-dependencies.sh` | **NO** | Does not exist | No dependency enforcement |
| **10.2** | Architecture tests (assembly-level) | **NO** | No ArchitectureTests project | No automated checks |
| **12.1** | No remaining `LKvitai.MES.Domain` / `LKvitai.MES.Application` | **NO** | These are the current project names | Entire rename pending |

### Compliance Score

- **Compliant:** 5 / 28 rules
- **Partially compliant:** 1 / 28
- **Not compliant:** 19 / 28
- **Not applicable:** 3 / 28

---

## 10. Refactor Plan (Proposal Only — NOT Implemented)

### Phase 0: Safety Net (prerequisite for everything)

**Goal:** Ensure refactor cannot silently break things.

1. **Add `tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/`** project using NetArchTest or ArchUnitNET
   - Test: Domain must not reference Infrastructure, EF Core, Marten, MassTransit
   - Test: Application must not reference Infrastructure
   - Test: Contracts must have zero PackageReferences
   - Test: BuildingBlocks must not reference any Modules assembly
2. **Add `scripts/validate-module-dependencies.sh`** — parse csproj XML, validate ProjectReference paths
3. **Add `Directory.Packages.props`** — centralize all package versions
4. **Ensure full green CI** — all existing tests must pass before any rename

**Risk:** Low. Additive only. No existing code changes.
**Prerequisite:** None.

### Phase 1: Root Cleanup + Directory Restructure

**Goal:** Match target directory layout without renaming projects/namespaces yet.

1. Move `src/tests/` → `tests/` at repo root
2. Move stale root `.md` files → `docs/adr/` or `docs/warehouse/`
3. Create `deploy/traefik/dynamic.yml` with example routing
4. Create `docs/architecture/` and move architecture docs there
5. Create `src/Modules/Warehouse/` and move all 8 Warehouse projects into it (keeping current names temporarily)
6. Create `src/BuildingBlocks/` as empty placeholder
7. Update `.sln` file paths, Dockerfiles, CI workflows
8. Move `src/docker-compose.yml` → `docker-compose.dev.yml` at root, remove RabbitMQ service (or clearly label as "developer convenience, not deployment artifact")

**Risk:** MEDIUM. Build paths change. Dockerfiles break. CI workflows break.
**Mitigation:**
- Script all moves (`git mv`)
- Update Dockerfiles atomically in same commit
- Update `.sln` with `dotnet sln` commands
- Run full build + test after each step
- Single PR, reviewed before merge

### Phase 2: Project + Namespace Rename

**Goal:** Match target naming conventions.

1. Rename projects:
   - `LKvitai.MES.Domain` → `LKvitai.MES.Modules.Warehouse.Domain`
   - `LKvitai.MES.Application` → `LKvitai.MES.Modules.Warehouse.Application`
   - `LKvitai.MES.Infrastructure` → `LKvitai.MES.Modules.Warehouse.Infrastructure`
   - `LKvitai.MES.Api` → `LKvitai.MES.Modules.Warehouse.Api`
   - `LKvitai.MES.WebUI` → `LKvitai.MES.Modules.Warehouse.Ui`
   - `LKvitai.MES.Contracts` → `LKvitai.MES.Modules.Warehouse.Contracts`
   - `LKvitai.MES.Projections` → merge into `Warehouse.Infrastructure` or keep as `Warehouse.Projections` (decision needed)
   - `LKvitai.MES.Sagas` → merge into `Warehouse.Infrastructure` or keep as `Warehouse.Sagas` (decision needed)
   - `LKvitai.MES.Integration` → merge into `Warehouse.Infrastructure` or keep as `Warehouse.Integration` (decision needed)
   - `LKvitai.MES.SharedKernel` → decide: `BuildingBlocks.SharedKernel` or dissolve into Contracts
2. Rename ALL namespaces (find-and-replace across entire codebase)
3. Update all test projects to match new references
4. Update Dockerfiles, CI, docker-compose files

**Risk:** HIGH. Massive churn. Every `.cs` file changes namespace. Every `using` statement may change.
**Mitigation:**
- Use IDE refactor (Rider/VS "Rename Namespace") per project — safer than scripted sed
- Batch by project: rename one project at a time, build, test, commit
- Property-based tests help catch silent breaks
- Create a rename mapping document before starting

### Phase 3: Dependency Cleanup

**Goal:** Fix dependency rule violations.

1. **Remove Marten from Application.csproj**
   - Extract interfaces/ports in Application that Marten types implement
   - Move Marten-dependent code to Infrastructure
   - Application should only depend on Domain, Contracts, abstractions
2. **Remove MediatR from SharedKernel**
   - Move MediatR interfaces to a `BuildingBlocks.Cqrs.Abstractions` project, or
   - Define own `ICommand`/`IQuery` interfaces in SharedKernel without MediatR dependency
3. **Remove SharedKernel reference from Contracts**
   - Inline any needed types (should be minimal records/enums)
   - Or remove the dependency and duplicate small value types
4. **Extract BuildingBlocks projects** (only those needed now):
   - `BuildingBlocks.Messaging.Abstractions` — publish/consume interfaces
   - `BuildingBlocks.Auth.Oidc` — JwtBearer config extension methods
   - `BuildingBlocks.Observability` — OpenTelemetry wiring
   - `BuildingBlocks.Testing` — shared test fixtures

**Risk:** MEDIUM. Behavioral changes possible if interfaces change.
**Mitigation:**
- Integration tests catch regressions
- Extract interfaces first, then move implementations
- One violation fix per PR

### Phase 4: Data Ownership + DB-Per-Service Preparation

**Goal:** Prepare for DB-per-service without splitting DBs yet.

1. Audit all entities in `WarehouseDbContext` — classify as Warehouse-owned vs future-module-owned
2. Move Sales entities (SalesOrder, Customer, SalesOrderLine) into a separate EF context or clearly mark as "cross-module read models"
3. Ensure Marten schema (`warehouse_events`) is already namespaced correctly
4. Document which entities belong to which future module
5. Prepare connection string configuration to support multiple DB keys

**Risk:** LOW-MEDIUM. Schema changes possible.
**Mitigation:**
- EF migrations handle schema changes
- Run against test DB first

### Phase 5: CI + Enforcement

**Goal:** Match target CI strategy and enforce boundaries.

1. Create `warehouse-ci.yml` with path triggers for `src/Modules/Warehouse/**` and `src/BuildingBlocks/**`
2. Create `architecture-checks.yml` as PR gate running:
   - `scripts/validate-module-dependencies.sh`
   - `tests/ArchitectureTests`
3. Retire old `deploy.yml` workflow (or refactor to use new paths)
4. Update `build-and-push.yml` with path triggers

**Risk:** LOW. CI changes only.
**Mitigation:**
- Test workflows on a feature branch first
- Keep old workflows until new ones are proven

---

### Open Decisions (from target spec Section 13 — must be confirmed before Phase 2)

| # | Question | Options | Impact |
|---|----------|---------|--------|
| 1 | DB-per-service topology | Separate DBs in one Postgres instance vs. separate Postgres instances | Connection string config, Docker setup |
| 2 | UI per module | All modules have UI, or some are API-only? | Whether WebUI stays as `Warehouse.Ui` or becomes shared |
| 3 | Messaging stack | MassTransit vs raw RabbitMQ client | Defines `BuildingBlocks.Messaging` implementation |
| 4 | Routing convention | PathPrefix (`/api/warehouse`) vs subdomain (`warehouse.<domain>`) | Traefik config, CORS, auth config |
| 5 | Projections/Sagas/Integration | Merge into Infrastructure, or keep as separate projects? | Affects project count, layer purity, build times |

---

### Suggested Automation Approach

| Step | Tool | Why |
|------|------|-----|
| Directory moves | `git mv` scripts | Preserves git history |
| Project renames | `dotnet sln` + `git mv` + sed on csproj | Must update .sln, csproj, Dockerfiles atomically |
| Namespace renames | Rider "Adjust Namespaces" (per project) | IDE handles `using` updates across entire solution |
| Validation | `dotnet build` + `dotnet test` after each step | Catch breaks immediately |
| Architecture tests | NetArchTest.eNET / ArchUnitNET | Standard .NET architecture testing libraries |

### First Safe Step (Audit Scaffolding)

If you want to start without risk, the single safest first action is:

1. Create `tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/` project
2. Add tests that document current violations as `[Fact(Skip = "Known violation — tracked in refactor plan")]`
3. Add `scripts/validate-module-dependencies.sh` that checks current csproj structure
4. Add `Directory.Packages.props` for central package management

This adds safety infrastructure without changing any existing code, and creates the foundation for enforcing boundaries as the refactor progresses.

---

*End of audit report.*
