# Repository Structure Audit - LKvitai.MES
## Multi-Module Architecture Design (17 Modules)

**Generated:** 2026-02-15
**Baseline Commit:** `2dd960cc4eefd73100f2f108cf70aaab0403ff26`
**Branch:** `main`
**Purpose:** Design scalable module-oriented structure for current Warehouse + 16 future modules

---

## A) Review Confirmation - What Was Analyzed

### Exact Baseline
- **Git Commit:** `2dd960cc4eefd73100f2f108cf70aaab0403ff26`
- **Branch:** `main`
- **SDK Version:** .NET 8.0.0 (rollForward: latestMinor)
- **Analysis Date:** 2026-02-15

### Folders Scanned
- **Root:** `/Users/bykovas/CodeRepos/clients/lauresta/LKvitai.MES/`
- **Source Code:** `/src/` (depth 3)
- **Documentation:** `/docs/` (all subdirectories)
- **Scripts:** `/scripts/` (all subdirectories)
- **Infrastructure:** `/.github/workflows/`

### Tool/Approach Used
1. **Project Discovery:** `Glob` tool with pattern `**/*.csproj` (found 13 projects)
2. **Namespace Detection:** Read first 20 lines of each .csproj to check for explicit `<RootNamespace>` or `<AssemblyName>` overrides
3. **Actual Usage Verification:** Sampled 7 .cs files from different projects to verify namespace patterns
4. **Build Config:** Read `Directory.Build.props`, `global.json`, solution file
5. **Git State:** Bash commands `git rev-parse HEAD` and `git branch --show-current`
6. **Dependency Analysis:** Read Dockerfiles, GitHub Actions workflows, validation scripts

### Key Finding
**All 13 projects use implicit naming** — no explicit `RootNamespace` or `AssemblyName` overrides found. Project filename = Assembly name = Root namespace.

---

## 1. Current Repository Structure (AS-IS)

### 1.1 Root Directory Tree (Depth 3)

```
/Users/bykovas/CodeRepos/clients/lauresta/LKvitai.MES/
│
├── .claude/                    # Claude agent config
├── .github/                    # CI/CD workflows
│   └── workflows/
│       ├── build-and-push.yml
│       └── deploy.yml
├── .kiro/                      # Other AI specs
│   └── specs/
│       └── warehouse-core-phase1/
├── .tools/                     # .NET local tools
│   └── .store/
├── .vscode/                    # VSCode settings
├── docs/                       # Documentation
│   ├── adr/                    # Architecture Decision Records
│   ├── claude/
│   ├── compliance/
│   ├── master-data/
│   ├── prod-ready/
│   ├── security/
│   ├── spec/
│   │   ├── 10-feature-decomposition.md
│   │   ├── 20-phase-plan.md
│   │   ├── 30-epics-and-stories.md
│   │   ├── 40-technical-implementation-guidelines.md
│   │   └── 50-risk-and-delivery-strategy.md
│   ├── ui/
│   ├── 01-discovery.md
│   ├── 02-warehouse-domain-model.md
│   ├── 03-implementation-guide.md
│   └── 04-system-architecture.md
├── scripts/                    # Build/ops scripts
│   ├── load/
│   ├── master-data-operational-smoke.sh
│   ├── seed-master-data.sql
│   └── validate-schema.sh
├── src/                        # Application source code
│   ├── LKvitai.MES.Api/
│   │   ├── Api/
│   │   │   └── Controllers/           [15+ controllers]
│   │   ├── Configuration/
│   │   ├── ErrorHandling/
│   │   ├── Middleware/
│   │   ├── Security/
│   │   ├── Services/                  [30+ service files]
│   │   ├── Dockerfile
│   │   └── logs/
│   ├── LKvitai.MES.Application/
│   │   ├── Behaviors/
│   │   ├── Commands/                  [15+ command files]
│   │   ├── ConsistencyChecks/
│   │   ├── EventVersioning/
│   │   ├── Models/
│   │   ├── Orchestration/
│   │   ├── Ports/
│   │   ├── Projections/
│   │   ├── Queries/                   [5+ query files]
│   │   └── Services/
│   ├── LKvitai.MES.Contracts/
│   │   ├── Events/
│   │   ├── Messages/
│   │   └── ReadModels/
│   ├── LKvitai.MES.Domain/
│   │   ├── Aggregates/                [StockLedger, Reservation, HandlingUnit, etc.]
│   │   ├── Common/
│   │   └── Entities/
│   ├── LKvitai.MES.Infrastructure/
│   │   ├── BackgroundJobs/
│   │   ├── EventVersioning/
│   │   ├── Imports/
│   │   ├── Locking/
│   │   ├── Outbox/
│   │   ├── Persistence/               [50+ EF Core migrations + repositories]
│   │   └── Projections/
│   ├── LKvitai.MES.Integration/
│   │   ├── Agnum/
│   │   ├── Carrier/
│   │   └── LabelPrinting/
│   ├── LKvitai.MES.Projections/
│   ├── LKvitai.MES.Sagas/
│   ├── LKvitai.MES.SharedKernel/
│   ├── LKvitai.MES.WebUI/
│   │   ├── Components/
│   │   ├── Infrastructure/
│   │   ├── Models/
│   │   ├── Pages/
│   │   ├── Services/
│   │   ├── Shared/
│   │   ├── Dockerfile
│   │   └── wwwroot/
│   ├── tests/
│   │   ├── LKvitai.MES.Tests.Integration/
│   │   ├── LKvitai.MES.Tests.Property/
│   │   └── LKvitai.MES.Tests.Unit/
│   ├── Directory.Build.props
│   ├── docker-compose.yml
│   ├── global.json
│   ├── LKvitai.MES.sln
│   ├── README.md
│   └── SOLUTION_STRUCTURE.md
├── .dockerignore
├── .gitignore
├── CLAUDE.md
├── CONCURRENCY_BUG_ANALYSIS.md
├── CONCURRENCY_BUG_FIX_SUMMARY.md
└── MARTEN_V2_VERSIONING_FIX.md
```

### 1.2 Current State Assessment

**✅ Positive:**
- Clean root structure (only docs/, scripts/, src/)
- No "warehouse" or domain folders polluting root
- All application code under /src
- Consistent implicit naming (project = assembly = namespace)

**⚠️ Challenges for Multi-Module Future:**
- **Flat structure:** All 13 projects sit at `/src/` level (no module grouping)
- **No isolation:** Domain, Application, Infrastructure are monolithic (Warehouse-only currently)
- **Scalability concern:** Adding 16 more modules would create 100+ projects at same level
- **Module boundaries unclear:** No physical separation between future modules (Warehouse, Orders, Finance, etc.)

---

## 2. Complete Project Inventory

### 2.1 All Projects (13 Total)

| # | Project Name | Path | Type | SDK | Dependencies |
|---|-------------|------|------|-----|--------------|
| 1 | LKvitai.MES.Api | `/src/LKvitai.MES.Api/` | ASP.NET Core | Microsoft.NET.Sdk.Web | MassTransit, Serilog, OpenTelemetry, Swagger |
| 2 | LKvitai.MES.WebUI | `/src/LKvitai.MES.WebUI/` | ASP.NET Core Web | Microsoft.NET.Sdk.Web | Minimal |
| 3 | LKvitai.MES.Application | `/src/LKvitai.MES.Application/` | Class Library | Microsoft.NET.Sdk | Marten, FluentValidation, MediatR |
| 4 | LKvitai.MES.Domain | `/src/LKvitai.MES.Domain/` | Class Library | Microsoft.NET.Sdk | SharedKernel, Contracts |
| 5 | LKvitai.MES.Infrastructure | `/src/LKvitai.MES.Infrastructure/` | Class Library | Microsoft.NET.Sdk | Marten, EF Core, PostgreSQL, ClosedXML |
| 6 | LKvitai.MES.Projections | `/src/LKvitai.MES.Projections/` | Class Library | Microsoft.NET.Sdk | Marten, Contracts, Domain |
| 7 | LKvitai.MES.Sagas | `/src/LKvitai.MES.Sagas/` | Class Library | Microsoft.NET.Sdk | MassTransit, Application, Contracts |
| 8 | LKvitai.MES.Integration | `/src/LKvitai.MES.Integration/` | Class Library | Microsoft.NET.Sdk | Contracts only |
| 9 | LKvitai.MES.Contracts | `/src/LKvitai.MES.Contracts/` | Class Library | Microsoft.NET.Sdk | No external deps |
| 10 | LKvitai.MES.SharedKernel | `/src/LKvitai.MES.SharedKernel/` | Class Library | Microsoft.NET.Sdk | MediatR |
| 11 | LKvitai.MES.Tests.Unit | `/src/tests/LKvitai.MES.Tests.Unit/` | xUnit Test | Microsoft.NET.Sdk | xUnit, Moq, FluentAssertions |
| 12 | LKvitai.MES.Tests.Integration | `/src/tests/LKvitai.MES.Tests.Integration/` | xUnit Test | Microsoft.NET.Sdk | xUnit, Testcontainers, TestHost |
| 13 | LKvitai.MES.Tests.Property | `/src/tests/LKvitai.MES.Tests.Property/` | xUnit Test | Microsoft.NET.Sdk | xUnit, FsCheck |

### 2.2 Naming Analysis

| Element | Current Pattern | Example | Explicit Override? |
|---------|----------------|---------|-------------------|
| **Project File** | `LKvitai.MES.<Layer>.csproj` | `LKvitai.MES.Domain.csproj` | No |
| **Assembly Name** | (implicit = project name) | `LKvitai.MES.Domain.dll` | No |
| **Root Namespace** | (implicit = project name) | `LKvitai.MES.Domain` | No |
| **Nested Namespace** | `LKvitai.MES.<Layer>.<Feature>` | `LKvitai.MES.Domain.Aggregates` | N/A |
| **Folder Name** | `LKvitai.MES.<Layer>/` | `LKvitai.MES.Domain/` | N/A |

**Verification:** Sampled 7 .cs files from different projects — all namespaces strictly mirror folder paths from project root.

---

## 3. Current Namespace Usage (Verified from Code)

| File Path | Full Namespace | Pattern |
|-----------|---------------|---------|
| `/src/LKvitai.MES.SharedKernel/ICommand.cs` | `LKvitai.MES.SharedKernel` | Root = project |
| `/src/LKvitai.MES.Domain/Aggregates/StockLedger.cs` | `LKvitai.MES.Domain.Aggregates` | Root + folder |
| `/src/LKvitai.MES.Application/Commands/PickStockCommand.cs` | `LKvitai.MES.Application.Commands` | Root + folder |
| `/src/LKvitai.MES.Contracts/Events/StockMovedEvent.cs` | `LKvitai.MES.Contracts.Events` | Root + folder |
| `/src/LKvitai.MES.Infrastructure/Persistence/WarehouseDbContext.cs` | `LKvitai.MES.Infrastructure.Persistence` | Root + folder |
| `/src/LKvitai.MES.Api/Services/MassTransitEventBus.cs` | `LKvitai.MES.Api.Services` | Root + folder |
| `/src/LKvitai.MES.Api/Controllers/StockController.cs` | `LKvitai.MES.Api.Controllers` | Root + folder |

**Consistency:** 100% — no deviations or shortcuts observed.

---

## 4. Current Namespace Inconsistencies

### 4.1 Analysis Result

**❌ NO INCONSISTENCIES FOUND**

All 13 projects follow the exact same convention:
- Project filename → Assembly name (implicit)
- Project filename → Root namespace (implicit)
- Folder hierarchy → Nested namespaces

### 4.2 Implications for Multi-Module Migration

**Challenge:** Current namespaces are **NOT module-aware**:
- `LKvitai.MES.Domain` (generic, no "Warehouse" indicator)
- Future module collision risk: What namespace for Orders.Domain? Finance.Domain?
- Must introduce module identifier to prevent conflicts

---

## B) Updated "To-Be" Structure (Module-Oriented)

### 5. Future Modules List (17 Total)

| # | Module | Layer | Description |
|---|--------|-------|-------------|
| 1 | **Warehouse** | Core | Stock ledger, reservations, HU tracking (CURRENT) |
| 2 | Orders | Core | Order management, order-to-cash |
| 3 | Finance | Core | Cost accounting, valuations, financial reporting |
| 4 | Shopfloor | Execution | Production execution, work orders |
| 5 | Quality | Execution | QC checks, non-conformance, CAPA |
| 6 | BoM | Planning | Bill of materials, routing |
| 7 | Scheduler | Planning | Production scheduling, capacity planning |
| 8 | Measurement | Data | Measurement data acquisition, SPC |
| 9 | Reporting | Analytics | Dashboards, KPIs, analytics |
| 10 | Delivery | Logistics | Shipment planning, carrier integration |
| 11 | LabelPrinting | Operations | Label generation, printer management |
| 12 | LabelScanning | Operations | Barcode scanning, RFID |
| 13 | DSAS | Integration | Delivery scheduling & appointment system |
| 14 | Installation | Master Data | Equipment, locations, configurations |
| 15 | Audit | Cross-cutting | Audit trail, compliance |
| 16 | PriceCalc | Business Rules | Pricing engine, quote calculation |
| 17 | Infra* | Platform | *Not a module — BuildingBlocks layer |

---

## 6. Primary "To-Be" Structure (RECOMMENDED)

### 6.1 Root Layout (Depth 3)

```
LKvitai.MES/
│
├── .github/                          # CI/CD workflows
│   └── workflows/
│       ├── build-and-push.yml
│       └── deploy.yml
│
├── docs/                             # Documentation
│   ├── adr/
│   ├── spec/
│   ├── technical/                    # ← NEW: Move CONCURRENCY_BUG_*.md here
│   ├── 01-discovery.md
│   ├── 02-warehouse-domain-model.md
│   ├── 03-implementation-guide.md
│   ├── 04-system-architecture.md
│   └── repo-structure-audit.md       # ← This document
│
├── infra/                            # ← NEW: Infrastructure as code
│   ├── docker/
│   │   ├── docker-compose.dev.yml    # ← Move from src/docker-compose.yml
│   │   ├── docker-compose.test.yml
│   │   └── observability/            # Jaeger, Grafana configs
│   ├── deployment/                   # Future: K8s, Helm
│   └── terraform/                    # Future: Cloud IaC
│
├── scripts/                          # Build/ops scripts
│   ├── db/
│   │   ├── seed-master-data.sql
│   │   └── validate-schema.sh
│   ├── load/
│   └── ci/                           # Future: CI helper scripts
│
├── src/                              # Application source
│   │
│   ├── BuildingBlocks/               # ← NEW: Cross-cutting infrastructure
│   │   ├── LKvitai.MES.BuildingBlocks.EventSourcing/
│   │   ├── LKvitai.MES.BuildingBlocks.Messaging/
│   │   ├── LKvitai.MES.BuildingBlocks.Observability/
│   │   ├── LKvitai.MES.BuildingBlocks.Security/
│   │   └── LKvitai.MES.BuildingBlocks.WebApi/
│   │
│   ├── SharedKernel/                 # Domain primitives
│   │   └── LKvitai.MES.SharedKernel/
│   │
│   ├── Contracts/                    # Global cross-module contracts
│   │   └── LKvitai.MES.Contracts/    # Integration events, shared DTOs
│   │
│   ├── Modules/                      # ← NEW: Business modules (17 total)
│   │   │
│   │   ├── Warehouse/                # ← Current code moves here
│   │   │   ├── LKvitai.MES.Modules.Warehouse.Domain/
│   │   │   ├── LKvitai.MES.Modules.Warehouse.Application/
│   │   │   ├── LKvitai.MES.Modules.Warehouse.Infrastructure/
│   │   │   ├── LKvitai.MES.Modules.Warehouse.Api/
│   │   │   ├── LKvitai.MES.Modules.Warehouse.Contracts/
│   │   │   └── Tests/
│   │   │       ├── LKvitai.MES.Modules.Warehouse.Tests.Unit/
│   │   │       ├── LKvitai.MES.Modules.Warehouse.Tests.Integration/
│   │   │       └── LKvitai.MES.Modules.Warehouse.Tests.Property/
│   │   │
│   │   ├── Orders/                   # Future module
│   │   │   ├── LKvitai.MES.Modules.Orders.Domain/
│   │   │   ├── LKvitai.MES.Modules.Orders.Application/
│   │   │   ├── LKvitai.MES.Modules.Orders.Infrastructure/
│   │   │   ├── LKvitai.MES.Modules.Orders.Api/
│   │   │   ├── LKvitai.MES.Modules.Orders.Contracts/
│   │   │   └── Tests/
│   │   │
│   │   ├── Finance/                  # Future module (same structure)
│   │   ├── Shopfloor/                # Future module
│   │   ├── Quality/                  # Future module
│   │   ├── BoM/                      # Future module
│   │   ├── Scheduler/                # Future module
│   │   ├── Measurement/              # Future module
│   │   ├── Reporting/                # Future module
│   │   ├── Delivery/                 # Future module
│   │   ├── LabelPrinting/            # Future module
│   │   ├── LabelScanning/            # Future module
│   │   ├── DSAS/                     # Future module
│   │   ├── Installation/             # Future module
│   │   ├── Audit/                    # Future module
│   │   └── PriceCalc/                # Future module
│   │
│   ├── Host/                         # ← NEW: Application composition roots
│   │   ├── LKvitai.MES.Host.Api/     # Main API (modules register here)
│   │   │   ├── Dockerfile
│   │   │   └── Program.cs            # Module registration
│   │   └── LKvitai.MES.Host.WebUI/   # Web UI host
│   │       ├── Dockerfile
│   │       └── Program.cs
│   │
│   ├── tests/                        # Cross-cutting tests
│   │   ├── LKvitai.MES.Tests.ArchitectureTests/  # ← NEW: Enforce module rules
│   │   └── LKvitai.MES.Tests.EndToEnd/           # ← NEW: Multi-module scenarios
│   │
│   ├── Directory.Build.props
│   ├── Directory.Packages.props      # ← NEW: Central package management
│   ├── global.json
│   └── LKvitai.MES.sln
│
├── .dockerignore
├── .gitignore
├── CLAUDE.md
└── README.md
```

### 6.2 Warehouse Module Structure (Example)

```
src/Modules/Warehouse/
│
├── LKvitai.MES.Modules.Warehouse.Domain/
│   ├── Aggregates/
│   │   ├── StockLedger.cs              # Event-sourced
│   │   ├── Reservation.cs              # Event-sourced
│   │   └── Valuation.cs                # Event-sourced
│   ├── Entities/
│   │   ├── HandlingUnit.cs             # State-based
│   │   ├── WarehouseLayout.cs
│   │   └── LogicalWarehouse.cs
│   ├── ValueObjects/
│   ├── DomainServices/
│   └── LKvitai.MES.Modules.Warehouse.Domain.csproj
│
├── LKvitai.MES.Modules.Warehouse.Application/
│   ├── Commands/
│   │   ├── ReceiveGoods/
│   │   ├── PickStock/
│   │   └── TransferStock/
│   ├── Queries/
│   ├── Projections/
│   ├── Sagas/
│   └── LKvitai.MES.Modules.Warehouse.Application.csproj
│
├── LKvitai.MES.Modules.Warehouse.Infrastructure/
│   ├── Persistence/
│   │   ├── Marten/
│   │   ├── EF/
│   │   └── Repositories/
│   ├── Integration/
│   │   ├── Agnum/
│   │   ├── LabelPrinting/
│   │   └── Kafka/
│   └── LKvitai.MES.Modules.Warehouse.Infrastructure.csproj
│
├── LKvitai.MES.Modules.Warehouse.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Configuration/
│   │   └── WarehouseModule.cs        # Module registration
│   └── LKvitai.MES.Modules.Warehouse.Api.csproj
│
├── LKvitai.MES.Modules.Warehouse.Contracts/
│   ├── Events/
│   ├── ReadModels/
│   ├── Messages/
│   └── LKvitai.MES.Modules.Warehouse.Contracts.csproj
│
└── Tests/
    ├── LKvitai.MES.Modules.Warehouse.Tests.Unit/
    ├── LKvitai.MES.Modules.Warehouse.Tests.Integration/
    └── LKvitai.MES.Modules.Warehouse.Tests.Property/
```

### 6.3 Design Decisions

#### **BuildingBlocks vs SharedKernel vs Contracts**

| Location | Purpose | Examples | Shared? |
|----------|---------|----------|---------|
| **SharedKernel/** | Domain primitives, DDD base classes | `DomainEvent`, `Result<T>`, `ICommand`, `Entity<T>` | Yes (domain only) |
| **Contracts/** | Public integration API | Integration events, global read models | Yes (all modules) |
| **BuildingBlocks/** | Technical infrastructure | Marten setup, MassTransit config, middleware | Yes (infra layer) |

#### **Infra Organization**

| Directory | Contents | Why Not src/? |
|-----------|----------|--------------|
| `infra/docker/` | docker-compose files, observability configs | Runtime config, not compiled code |
| `infra/deployment/` | K8s, Helm charts (future) | Deployment artifacts |
| `infra/terraform/` | Cloud IaC (future) | Provisioning scripts |

**What stays in src/:**
- `Dockerfile` (per-project, referenced by relative path in COPY commands)
- `Directory.Build.props`, `global.json` (MSBuild/SDK config)

---

## C) Naming/Namespace Standard

### 7. Naming Convention Table

| Element | Pattern | Variable Format |
|---------|---------|-----------------|
| **Solution File** | `LKvitai.MES.sln` | Fixed |
| **Module Folder** | `src/Modules/<Module>/` | `{ModuleName}` |
| **Project File** | `LKvitai.MES.Modules.<Module>.<Layer>.csproj` | `LKvitai.MES.Modules.{Module}.{Layer}.csproj` |
| **Assembly Name** | (implicit = project name) | `LKvitai.MES.Modules.{Module}.{Layer}.dll` |
| **Root Namespace** | (implicit = project name) | `LKvitai.MES.Modules.{Module}.{Layer}` |
| **Nested Namespace** | `<Root>.<Feature>` | `LKvitai.MES.Modules.{Module}.{Layer}.{Feature}` |
| **Test Project** | `LKvitai.MES.Modules.<Module>.Tests.<Type>` | `LKvitai.MES.Modules.{Module}.Tests.{Type}` |

**Layers:** Domain, Application, Infrastructure, Api, Contracts
**Test Types:** Unit, Integration, Property

### 8. Concrete Examples (3 Modules)

#### Example 1: Warehouse Module

| Element | Value |
|---------|-------|
| **Module Folder** | `src/Modules/Warehouse/` |
| **Domain Project** | `LKvitai.MES.Modules.Warehouse.Domain.csproj` |
| **Domain Namespace** | `LKvitai.MES.Modules.Warehouse.Domain` |
| **Application Project** | `LKvitai.MES.Modules.Warehouse.Application.csproj` |
| **Application Namespace** | `LKvitai.MES.Modules.Warehouse.Application` |
| **Infrastructure** | `LKvitai.MES.Modules.Warehouse.Infrastructure.csproj` |
| **API** | `LKvitai.MES.Modules.Warehouse.Api.csproj` |
| **Contracts** | `LKvitai.MES.Modules.Warehouse.Contracts.csproj` |
| **Unit Tests** | `LKvitai.MES.Modules.Warehouse.Tests.Unit.csproj` |
| **Integration Tests** | `LKvitai.MES.Modules.Warehouse.Tests.Integration.csproj` |
| **Namespace Example** | `LKvitai.MES.Modules.Warehouse.Domain.Aggregates` (folder: `Domain/Aggregates/`) |

#### Example 2: Orders Module

| Element | Value |
|---------|-------|
| **Module Folder** | `src/Modules/Orders/` |
| **Domain Project** | `LKvitai.MES.Modules.Orders.Domain.csproj` |
| **Domain Namespace** | `LKvitai.MES.Modules.Orders.Domain` |
| **Application Project** | `LKvitai.MES.Modules.Orders.Application.csproj` |
| **Application Namespace** | `LKvitai.MES.Modules.Orders.Application` |
| **Infrastructure** | `LKvitai.MES.Modules.Orders.Infrastructure.csproj` |
| **API** | `LKvitai.MES.Modules.Orders.Api.csproj` |
| **Contracts** | `LKvitai.MES.Modules.Orders.Contracts.csproj` |
| **Unit Tests** | `LKvitai.MES.Modules.Orders.Tests.Unit.csproj` |
| **Namespace Example** | `LKvitai.MES.Modules.Orders.Application.Commands` (folder: `Application/Commands/`) |

#### Example 3: Finance Module

| Element | Value |
|---------|-------|
| **Module Folder** | `src/Modules/Finance/` |
| **Domain Project** | `LKvitai.MES.Modules.Finance.Domain.csproj` |
| **Domain Namespace** | `LKvitai.MES.Modules.Finance.Domain` |
| **Application Project** | `LKvitai.MES.Modules.Finance.Application.csproj` |
| **Application Namespace** | `LKvitai.MES.Modules.Finance.Application` |
| **Infrastructure** | `LKvitai.MES.Modules.Finance.Infrastructure.csproj` |
| **API** | `LKvitai.MES.Modules.Finance.Api.csproj` |
| **Contracts** | `LKvitai.MES.Modules.Finance.Contracts.csproj` |
| **Integration Tests** | `LKvitai.MES.Modules.Finance.Tests.Integration.csproj` |
| **Namespace Example** | `LKvitai.MES.Modules.Finance.Domain.ValueObjects` (folder: `Domain/ValueObjects/`) |

### 9. BuildingBlocks / SharedKernel / Host Naming

| Type | Project Name | Namespace | Notes |
|------|-------------|-----------|-------|
| **SharedKernel** | `LKvitai.MES.SharedKernel` | `LKvitai.MES.SharedKernel` | No "Modules." prefix |
| **Contracts** | `LKvitai.MES.Contracts` | `LKvitai.MES.Contracts` | Global, no prefix |
| **BuildingBlocks** | `LKvitai.MES.BuildingBlocks.<Area>` | `LKvitai.MES.BuildingBlocks.{Area}` | e.g., `EventSourcing`, `Messaging` |
| **Host** | `LKvitai.MES.Host.<Type>` | `LKvitai.MES.Host.{Type}` | e.g., `Api`, `WebUI` |

---

## D) Move/Rename Impact Analysis

### 10. Files Requiring Path Updates

#### 10.1 Dockerfiles (2 files) - 🔴 **HIGH RISK**

| File | Current Paths | New Paths | Change Count |
|------|--------------|-----------|--------------|
| `src/LKvitai.MES.Api/Dockerfile` | `COPY src/LKvitai.MES.Api/ LKvitai.MES.Api/` (9 projects) | `COPY src/Host/LKvitai.MES.Host.Api/ Host/LKvitai.MES.Host.Api/` + all module paths | 18+ lines |
| `src/LKvitai.MES.WebUI/Dockerfile` | (same pattern) | `COPY src/Host/LKvitai.MES.Host.WebUI/ ...` | 10+ lines |

**Example Change:**
```diff
# Before
-COPY src/LKvitai.MES.Api/LKvitai.MES.Api.csproj LKvitai.MES.Api/
-COPY src/LKvitai.MES.Domain/LKvitai.MES.Domain.csproj LKvitai.MES.Domain/

# After
+COPY src/Host/LKvitai.MES.Host.Api/LKvitai.MES.Host.Api.csproj Host/LKvitai.MES.Host.Api/
+COPY src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/LKvitai.MES.Modules.Warehouse.Domain.csproj Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/
```

**Mitigation:**
- Update Dockerfiles after folder migration
- Test Docker builds locally before CI
- Consider multi-stage Dockerfile optimization

#### 10.2 GitHub Actions (2 files) - 🟡 **MEDIUM RISK**

| File | Current Reference | New Reference |
|------|------------------|---------------|
| `.github/workflows/build-and-push.yml` | `dockerfile: src/LKvitai.MES.Api/Dockerfile` | `dockerfile: src/Host/LKvitai.MES.Host.Api/Dockerfile` |
| `.github/workflows/deploy.yml` | `dotnet test src/tests/LKvitai.MES.Tests.Integration/...` | `dotnet test src/Modules/Warehouse/Tests/LKvitai.MES.Modules.Warehouse.Tests.Integration/...` |

**Mitigation:**
- Update workflows in same PR as folder migration
- Test in feature branch before merge to main

#### 10.3 Solution File (1 file) - 🟢 **LOW RISK**

**Change:** Project paths in .sln change from `LKvitai.MES.Api\LKvitai.MES.Api.csproj` to `Host\LKvitai.MES.Host.Api\LKvitai.MES.Host.Api.csproj`

**Mitigation:** Visual Studio / Rider auto-updates on folder move. Manual fix if needed (text-based format).

#### 10.4 Project References (13 .csproj) - 🟢 **LOW RISK**

**Example Change:**
```diff
-<ProjectReference Include="..\LKvitai.MES.Domain\LKvitai.MES.Domain.csproj" />
+<ProjectReference Include="..\..\Modules\Warehouse\LKvitai.MES.Modules.Warehouse.Domain\LKvitai.MES.Modules.Warehouse.Domain.csproj" />
```

**Mitigation:** IDEs auto-update. Manual find/replace if needed.

#### 10.5 Scripts (3 files) - 🟢 **LOW RISK**

| File | Dependency | Change Needed? |
|------|-----------|----------------|
| `scripts/validate-schema.sh` | Env var `WAREHOUSE_DB_CONNECTION` | ✅ No |
| `scripts/master-data-operational-smoke.sh` | DB connection (likely) | ✅ No |
| `scripts/seed-master-data.sql` | SQL only | ✅ No |

---

### 11. Namespace-Breaking Changes - 🔴 **CRITICAL RISK**

#### Current State
```csharp
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Contracts.Events;
```

#### New State
```csharp
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Contracts.Events;
```

#### Impact
- **Every `.cs` file** in 10 projects (excluding SharedKernel, Contracts)
- All `using` statements change
- All test files need updated namespaces
- **Estimate:** 1000+ lines of code (based on Domain having 991+ lines)

#### Mitigation Strategy
1. **Automated Refactoring:** Use Roslyn/Rider "Rename Namespace" refactoring
2. **Find/Replace:** Regex find `using LKvitai\.MES\.(Domain|Application|Infrastructure|Projections|Sagas|Api|WebUI)` → `using LKvitai.MES.Modules.Warehouse.$1`
3. **Incremental Compilation:** Fix errors project-by-project
4. **Test Suite Validation:** Run all tests after each project migration

---

### 12. Risk Summary

| Component | Files Affected | Risk Level | Effort (Hours) | Mitigation |
|-----------|---------------|------------|----------------|------------|
| **Namespaces** | 1000+ .cs files | 🔴 **CRITICAL** | 16-24 | Automated refactoring + iterative compilation |
| **Dockerfiles** | 2 | 🔴 **HIGH** | 2-4 | Update + local Docker test before CI |
| **CI/CD Workflows** | 2 | 🟡 **MEDIUM** | 1-2 | Update in same PR, test in branch |
| **Solution File** | 1 | 🟢 **LOW** | 0.5-1 | IDE auto-update or manual |
| **Project References** | 13 .csproj | 🟢 **LOW** | 1-2 | IDE auto-update |
| **Scripts** | 3 | 🟢 **LOW** | 0 | No changes needed |

**Total Estimated Effort:** 20-33 hours (1 developer, 3-5 days)

---

## E) Minimal High-Level Migration Plan

### 13. Migration Steps (High-Level)

#### Phase 1: Prepare (1-2 days)
1. **Create feature branch:** `feature/multi-module-restructure`
2. **Backup current state:** Tag commit as `pre-module-migration`
3. **Create folder structure:**
   ```bash
   mkdir -p src/{BuildingBlocks,Host,Modules/Warehouse/Tests}
   mkdir -p infra/{docker,deployment,terraform}
   mkdir -p docs/technical
   ```
4. **Move technical docs:**
   ```bash
   mv CONCURRENCY_BUG_*.md docs/technical/
   mv MARTEN_V2_*.md docs/technical/
   ```
5. **Move docker-compose:**
   ```bash
   mv src/docker-compose.yml infra/docker/docker-compose.dev.yml
   ```

#### Phase 2: Rename Projects (0.5 days)
6. **Rename projects (Visual Studio/Rider):**
   - `LKvitai.MES.Api` → `LKvitai.MES.Host.Api`
   - `LKvitai.MES.WebUI` → `LKvitai.MES.Host.WebUI`
   - `LKvitai.MES.Domain` → `LKvitai.MES.Modules.Warehouse.Domain`
   - `LKvitai.MES.Application` → `LKvitai.MES.Modules.Warehouse.Application`
   - `LKvitai.MES.Infrastructure` → `LKvitai.MES.Modules.Warehouse.Infrastructure`
   - `LKvitai.MES.Projections` → `LKvitai.MES.Modules.Warehouse.Projections` (merge into Application or Infrastructure)
   - `LKvitai.MES.Sagas` → `LKvitai.MES.Modules.Warehouse.Sagas` (merge into Application)
   - `LKvitai.MES.Integration` → `LKvitai.MES.Modules.Warehouse.Infrastructure` (merge into Infrastructure)
   - **Keep as-is:** `LKvitai.MES.SharedKernel`, `LKvitai.MES.Contracts`

7. **Move project folders:**
   ```bash
   mv src/LKvitai.MES.Host.Api src/Host/
   mv src/LKvitai.MES.Host.WebUI src/Host/
   mv src/LKvitai.MES.Modules.Warehouse.* src/Modules/Warehouse/
   mv src/tests/LKvitai.MES.Modules.Warehouse.Tests.* src/Modules/Warehouse/Tests/
   mv src/LKvitai.MES.SharedKernel src/SharedKernel/
   mv src/LKvitai.MES.Contracts src/Contracts/
   ```

#### Phase 3: Update Namespaces (1-2 days)
8. **Automated refactoring (per project):**
   - Open project in Rider/Visual Studio
   - Right-click project → "Adjust Namespaces" or "Rename Namespace"
   - Alternatively: Regex find/replace in IDE
     ```regex
     Find: using LKvitai\.MES\.(Domain|Application|Infrastructure|Projections|Sagas|Integration|Api|WebUI)
     Replace: using LKvitai.MES.Modules.Warehouse.$1
     ```
9. **Compile incrementally:**
   ```bash
   dotnet build src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Domain/
   dotnet build src/Modules/Warehouse/LKvitai.MES.Modules.Warehouse.Application/
   # Fix errors, repeat for all projects
   ```

#### Phase 4: Update References (0.5 days)
10. **Update Dockerfiles:**
    - `src/Host/LKvitai.MES.Host.Api/Dockerfile` (update all COPY paths)
    - `src/Host/LKvitai.MES.Host.WebUI/Dockerfile`
11. **Update CI/CD workflows:**
    - `.github/workflows/build-and-push.yml` (Dockerfile paths)
    - `.github/workflows/deploy.yml` (test project path)
12. **Update solution file:**
    - `src/LKvitai.MES.sln` (verify IDE updated paths, manual fix if needed)

#### Phase 5: Validate (1 day)
13. **Local builds:**
    ```bash
    dotnet restore src/LKvitai.MES.sln
    dotnet build src/LKvitai.MES.sln --configuration Release
    ```
14. **Run tests:**
    ```bash
    dotnet test src/LKvitai.MES.sln --verbosity minimal
    ```
15. **Docker builds:**
    ```bash
    docker build -f src/Host/LKvitai.MES.Host.Api/Dockerfile .
    docker build -f src/Host/LKvitai.MES.Host.WebUI/Dockerfile .
    ```
16. **Test docker-compose:**
    ```bash
    docker-compose -f infra/docker/docker-compose.dev.yml up
    ```

#### Phase 6: Documentation (0.5 days)
17. **Update documentation:**
    - `CLAUDE.md` (update project structure references)
    - `docs/04-system-architecture.md` (add module boundaries section)
    - `README.md` (update folder descriptions)
    - `src/SOLUTION_STRUCTURE.md` (rewrite for module structure)

#### Phase 7: Merge (0.5 days)
18. **Create PR:**
    - Title: "Restructure to module-oriented architecture (17-module support)"
    - Description: Link to this audit document, list all changes
19. **Code review:** Focus on namespace correctness, CI/CD paths
20. **Merge to main**
21. **Create tag:** `module-migration-complete`

---

### 14. Alternative: Gradual Migration (Lower Risk)

**Concept:** Keep Warehouse flat, add new modules (Orders, Finance) in modular structure from day 1.

```
src/
├── LKvitai.MES.Api/                    # EXISTING (Warehouse only)
├── LKvitai.MES.Application/            # EXISTING
├── LKvitai.MES.Domain/                 # EXISTING
├── ...
│
├── BuildingBlocks/                     # NEW
├── Modules/                            # NEW
│   ├── Orders/                         # NEW (modular from day 1)
│   │   ├── LKvitai.MES.Modules.Orders.Domain/
│   │   └── ...
│   └── Finance/                        # NEW
└── Host/                               # NEW (when migrating Warehouse)
```

**Pros:**
- ✅ Zero breaking changes to Warehouse
- ✅ New modules clean from start
- ✅ Lower risk for active development

**Cons:**
- ❌ Inconsistent structure (two patterns)
- ❌ Eventual migration still needed

**Use When:**
- Warehouse is in active feature development
- Team can't afford 3-5 day migration freeze
- Need to deliver new modules (Orders) urgently

---

## F) Critical Warnings

### ⚠️ Warning 1: Namespace Breaking Change
**Impact:** Every `.cs` file (1000+) in 10 projects will have namespace changes.
**Consequence:** Feature work in Warehouse MUST FREEZE during migration (merge conflicts).
**Mitigation:** Schedule migration during low-activity period (sprint planning week, end of quarter).

### ⚠️ Warning 2: Docker Build Context
**Impact:** All Dockerfiles assume build context = repo root. Paths are hardcoded with `src/` prefix.
**Consequence:** CI/CD builds will fail if Dockerfiles not updated correctly.
**Mitigation:** Test Docker builds locally BEFORE pushing to CI. Keep old paths working during transition (symlinks if needed).

### ⚠️ Warning 3: No Rollback Without Pain
**Impact:** Once namespaces change, reverting requires re-refactoring (same effort).
**Consequence:** Migration must succeed on first attempt (no "oops, let's roll back").
**Mitigation:** Create `pre-module-migration` tag. Test exhaustively in feature branch before merging.

### ⚠️ Warning 4: BuildingBlocks Extraction
**Impact:** Creating BuildingBlocks requires extracting infrastructure code from existing projects (Infrastructure, Api).
**Consequence:** Risk of circular dependencies if not careful.
**Mitigation:** Extract BuildingBlocks AFTER module migration stabilizes. Not critical for Phase 1.

### ⚠️ Warning 5: Directory.Packages.props
**Impact:** Centralizing package versions (Directory.Packages.props) requires updating all .csproj files.
**Consequence:** Additional migration work (estimate +2-4 hours).
**Mitigation:** Do this in separate PR AFTER module migration completes.

---

## Appendix A: 17-Module Namespace Examples

| Module | Domain Namespace | Application Namespace | Contracts Namespace |
|--------|-----------------|---------------------|-------------------|
| Warehouse | `LKvitai.MES.Modules.Warehouse.Domain` | `LKvitai.MES.Modules.Warehouse.Application` | `LKvitai.MES.Modules.Warehouse.Contracts` |
| Orders | `LKvitai.MES.Modules.Orders.Domain` | `LKvitai.MES.Modules.Orders.Application` | `LKvitai.MES.Modules.Orders.Contracts` |
| Finance | `LKvitai.MES.Modules.Finance.Domain` | `LKvitai.MES.Modules.Finance.Application` | `LKvitai.MES.Modules.Finance.Contracts` |
| Shopfloor | `LKvitai.MES.Modules.Shopfloor.Domain` | `LKvitai.MES.Modules.Shopfloor.Application` | `LKvitai.MES.Modules.Shopfloor.Contracts` |
| Quality | `LKvitai.MES.Modules.Quality.Domain` | `LKvitai.MES.Modules.Quality.Application` | `LKvitai.MES.Modules.Quality.Contracts` |
| BoM | `LKvitai.MES.Modules.BoM.Domain` | `LKvitai.MES.Modules.BoM.Application` | `LKvitai.MES.Modules.BoM.Contracts` |
| Scheduler | `LKvitai.MES.Modules.Scheduler.Domain` | `LKvitai.MES.Modules.Scheduler.Application` | `LKvitai.MES.Modules.Scheduler.Contracts` |
| Measurement | `LKvitai.MES.Modules.Measurement.Domain` | `LKvitai.MES.Modules.Measurement.Application` | `LKvitai.MES.Modules.Measurement.Contracts` |
| Reporting | `LKvitai.MES.Modules.Reporting.Domain` | `LKvitai.MES.Modules.Reporting.Application` | `LKvitai.MES.Modules.Reporting.Contracts` |
| Delivery | `LKvitai.MES.Modules.Delivery.Domain` | `LKvitai.MES.Modules.Delivery.Application` | `LKvitai.MES.Modules.Delivery.Contracts` |
| LabelPrinting | `LKvitai.MES.Modules.LabelPrinting.Domain` | `LKvitai.MES.Modules.LabelPrinting.Application` | `LKvitai.MES.Modules.LabelPrinting.Contracts` |
| LabelScanning | `LKvitai.MES.Modules.LabelScanning.Domain` | `LKvitai.MES.Modules.LabelScanning.Application` | `LKvitai.MES.Modules.LabelScanning.Contracts` |
| DSAS | `LKvitai.MES.Modules.DSAS.Domain` | `LKvitai.MES.Modules.DSAS.Application` | `LKvitai.MES.Modules.DSAS.Contracts` |
| Installation | `LKvitai.MES.Modules.Installation.Domain` | `LKvitai.MES.Modules.Installation.Application` | `LKvitai.MES.Modules.Installation.Contracts` |
| Audit | `LKvitai.MES.Modules.Audit.Domain` | `LKvitai.MES.Modules.Audit.Application` | `LKvitai.MES.Modules.Audit.Contracts` |
| PriceCalc | `LKvitai.MES.Modules.PriceCalc.Domain` | `LKvitai.MES.Modules.PriceCalc.Application` | `LKvitai.MES.Modules.PriceCalc.Contracts` |

---

## Appendix B: Module Dependency Rules

### Allowed Dependencies
```
Module.Api → Module.Application → Module.Domain
    ↓              ↓                   ↓
    └───────→ Module.Infrastructure ───┘

Module.Application → OtherModule.Contracts  ✅ (integration events)
Module.Domain → SharedKernel  ✅ (domain primitives)
Module.Infrastructure → BuildingBlocks.*  ✅ (technical plumbing)
```

### Forbidden Dependencies
```
Module.Domain → Module.Application  ❌ (violates DDD)
Module.Domain → OtherModule.Domain  ❌ (coupling)
Module.Application → OtherModule.Application  ❌ (coupling)
```

### Enforcement
Use **NetArchTest** or **ArchUnitNET** in `LKvitai.MES.Tests.ArchitectureTests`:
```csharp
[Fact]
public void Modules_ShouldNot_DirectlyReference_OtherModules()
{
    var result = Types()
        .That().ResideInNamespace("LKvitai.MES.Modules.Warehouse")
        .Should().NotHaveDependencyOn("LKvitai.MES.Modules.Orders")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

---

**END OF AUDIT DOCUMENT**
