# CLAUDE.md - LKvitai.MES

## Project Overview

LKvitai.MES is a warehouse management module (WMS) for a Manufacturing Execution System (MES). It provides real-time inventory tracking, physical location visualization (2D/3D), cycle counting, unit conversion, and accounting integration with Agnum.

**Current status:** Phase 1 implementation in progress — Packages A–F committed, Hotfixes G & H applied. Architecture is frozen (v2.0 FINAL BASELINE). Do NOT redesign.

## Claude's Role

- Docs `01-04` are **Claude's architecture docs** — source of truth for domain design
- `docs/spec/` (10-50) and `.kiro/specs/warehouse-core-phase1/` were created by other AI — planning & task specs
- `docs/claude/claude.md` — operational context doc (packages A–F, invariants, commands)
- Claude's primary role going forward: **code review, implementation guidance, and architectural governance**

---

## Solution Structure (post-refactor)

The refactor changed from a flat `src/LKvitai.MES.<Layer>/` layout to a modular monolith under `src/Modules/Warehouse/` and `src/BuildingBlocks/`.

```
lkvitai-mes/
├── src/
│   ├── LKvitai.MES.sln
│   ├── Directory.Build.props          # LangVersion, TreatWarningsAsErrors, doc XML
│   ├── global.json                    # SDK pin: 8.0.418
│   ├── docker-compose.yml             # Dev infra: postgres + rabbitmq (profile) + jaeger
│   ├── BuildingBlocks/
│   │   ├── LKvitai.MES.BuildingBlocks.Cqrs.Abstractions/   # ICommand, IQuery, ICommandHandler
│   │   └── LKvitai.MES.BuildingBlocks.SharedKernel/        # DomainEvent, Result, exceptions
│   └── Modules/
│       └── Warehouse/
│           ├── LKvitai.MES.Modules.Warehouse.Api/           # Composition root, DI, Program.cs, controllers
│           ├── LKvitai.MES.Modules.Warehouse.Application/   # Commands, Queries, ports (interfaces), behaviors
│           ├── LKvitai.MES.Modules.Warehouse.Contracts/     # Events, messages, read-model view types (zero deps)
│           ├── LKvitai.MES.Modules.Warehouse.Domain/        # Aggregates, domain events, invariants (zero infra deps)
│           ├── LKvitai.MES.Modules.Warehouse.Infrastructure/# Marten repos, EF Core, outbox, orchestration impls
│           ├── LKvitai.MES.Modules.Warehouse.Integration/   # Agnum + LabelPrinting adapters (anti-corruption layer)
│           ├── LKvitai.MES.Modules.Warehouse.Projections/   # Marten inline + async projections
│           ├── LKvitai.MES.Modules.Warehouse.Sagas/         # MassTransit state machines
│           └── LKvitai.MES.Modules.Warehouse.WebUI/         # Blazor Server frontend
├── tests/
│   ├── ArchitectureTests/
│   │   └── LKvitai.MES.ArchitectureTests/                  # NetArchTest layer enforcement
│   └── Modules/
│       └── Warehouse/
│           ├── LKvitai.MES.Tests.Warehouse.Unit/            # Aggregate + projection + handler tests
│           ├── LKvitai.MES.Tests.Warehouse.Integration/     # Testcontainers + Marten E2E per workflow
│           ├── LKvitai.MES.Tests.Warehouse.Property/        # FsCheck invariant tests
│           └── LKvitai.MES.Tests.Warehouse.E2E/             # Black-box HTTP workflow tests
├── tools/
│   └── DependencyValidator/                                 # CLI tool: validates csproj dependency rules
├── deploy/
│   └── traefik/dynamic.yml
├── .github/workflows/                                       # CI/CD (see CI section below)
├── docker-compose.yml                                       # Full stack: 3 API instances + nginx + postgres + redis + grafana
├── docker-compose.test.yml                                  # Test stack (used by deploy-test.yml)
├── Directory.Packages.props                                 # Central Package Management — 50 packages, no Version= in csproj
└── docs/                                                    # Architecture, specs, audit, ADRs
```

### Project Dependency Rules (enforced by CI)

```
Layer 0 (leaves):   BuildingBlocks.Cqrs.Abstractions, WebUI
Layer 1:            BuildingBlocks.SharedKernel
Layer 2:            Modules.Warehouse.Contracts  (zero deps — pure DTOs/events)
Layer 3:            Modules.Warehouse.Domain, Modules.Warehouse.Integration
Layer 4:            Modules.Warehouse.Application, Modules.Warehouse.Projections
Layer 5:            Modules.Warehouse.Infrastructure, Modules.Warehouse.Sagas
Layer 6:            Modules.Warehouse.Api
```

**Key rules:**
- BuildingBlocks NEVER reference Modules
- Contracts has ZERO PackageReferences and ZERO ProjectReferences
- Domain has ZERO NuGet dependencies (only refs SharedKernel + Contracts)
- Application does NOT reference Marten (Marten lives in Infrastructure + Projections only)
- Projections references Contracts + Domain only (no Infrastructure)

---

## Build & Test

```bash
# Restore (from repo root)
dotnet restore src/LKvitai.MES.sln

# Build
dotnet build src/LKvitai.MES.sln -c Release --no-restore

# Run all tests
dotnet test src/LKvitai.MES.sln -c Release

# Run specific test suite
dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Unit/LKvitai.MES.Tests.Warehouse.Unit.csproj -c Release
dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Integration/LKvitai.MES.Tests.Warehouse.Integration.csproj -c Release
dotnet test tests/Modules/Warehouse/LKvitai.MES.Tests.Warehouse.Property/LKvitai.MES.Tests.Warehouse.Property.csproj -c Release

# Architecture enforcement
dotnet run --project tools/DependencyValidator/DependencyValidator.csproj
./scripts/validate-module-dependencies.sh
dotnet test tests/ArchitectureTests/LKvitai.MES.ArchitectureTests/LKvitai.MES.ArchitectureTests.csproj
```

---

## Development Environment

Two docker-compose files serve different purposes:

| File | Purpose | When to use |
|------|---------|-------------|
| `src/docker-compose.yml` | Dev infra: postgres + rabbitmq (optional profile) + jaeger | Local dev |
| `docker-compose.yml` (root) | Full stack: 3 API replicas + nginx + postgres + redis + grafana | Load/integration testing |
| `docker-compose.test.yml` (root) | Test environment | CI deploy-test workflow |

```bash
# Start dev infrastructure (postgres + jaeger; rabbitmq optional)
docker compose -f src/docker-compose.yml up -d

# With RabbitMQ (MassTransit saga testing)
docker compose -f src/docker-compose.yml --profile dev-broker up -d
```

**Connection strings (local dev):**
- PostgreSQL: `Host=localhost;Port=5432;Database=lkvitai_warehouse_dev;Username=postgres;Password=postgres`
- Jaeger UI: http://localhost:16686
- RabbitMQ management: http://localhost:15672 (guest/guest)

**API local ports** (see `launchSettings.json`):
- Api: http://localhost:5000 / https://localhost:5001
- WebUI: http://localhost:5100 / https://localhost:5101

---

## CI/CD Workflows

| Workflow | File | Triggers | What it does |
|----------|------|---------|-------------|
| Warehouse CI | `warehouse-ci.yml` | PR to main (src/Modules/Warehouse, src/BuildingBlocks, tests/Modules/Warehouse changes) | restore → build → unit tests → integration tests |
| Architecture Checks | `architecture-checks.yml` | PR to main | DependencyValidator → validate-module-dependencies.sh → ArchitectureTests |
| Build & Push | `build-and-push.yml` | Push to main | Docker build + push to registry |
| Deploy Test | `deploy-test.yml` | Push to main | Deploys to test environment |
| Deploy | `deploy.yml` | Manual | Production deploy + integration smoke tests |
| E2E Tests | `e2e-tests.yml` | Scheduled | Black-box workflow E2E tests |

---

## Naming Conventions

| Type | Pattern | Example |
|------|---------|---------|
| Source projects | `LKvitai.MES.Modules.<Module>.<Layer>` | `LKvitai.MES.Modules.Warehouse.Application` |
| BuildingBlock projects | `LKvitai.MES.BuildingBlocks.<Concern>` | `LKvitai.MES.BuildingBlocks.SharedKernel` |
| Test projects | `LKvitai.MES.Tests.<Module>.<Type>` | `LKvitai.MES.Tests.Warehouse.Unit` |
| Namespaces | Match assembly name (explicit `<RootNamespace>` in csproj) | |
| Events | Past tense, noun phrase | `StockMovedEvent`, `PickingStartedEvent` |
| Commands | Imperative verb | `RecordStockMovementCommand`, `StartPickingCommand` |
| Stream IDs | `stock-ledger:{warehouseId}:{location}:{sku}` (via `StockLedgerStreamId.For()`) | |
| Documentation | `NN-description.md` numbered naming | `01-discovery.md`, `10-feature-decomposition.md` |

---

## Architecture Summary

### Patterns
- DDD, Event Sourcing, CQRS, Sagas (MassTransit state machines)
- Transactional Outbox, command dedup, event handler idempotency
- Saga step checkpoints, optimistic concurrency, READ COMMITTED isolation
- Daily consistency checks (balance integrity, negative balances, orphaned HUs)

### 5 Mandatory Architectural Decisions (DO NOT VIOLATE)

| # | Decision | Rule |
|---|----------|------|
| **1 (CRITICAL)** | StockLedger sole owner | ONLY StockLedger writes StockMovement events — no other aggregate or service |
| **2 (CRITICAL)** | Ledger-first pick ordering | StockMovement commits BEFORE HU/Reservation updates in any picking flow |
| **3** | Offline operations | Only PickStock (HARD lock) + TransferStock (assigned HUs) may operate offline |
| **4** | Reservation bump rules | SOFT reservations are bumpable; HARD locks cannot be bumped under any condition |
| **5** | Integration latency tiers | Operational <5s, Process <30s, Financial batch (scheduled, daily 23:00) |

### Non-Negotiable Constraints
- No distributed locks or global transactions — use optimistic concurrency + PostgreSQL advisory locks
- No projection waits — sagas/handlers NEVER await async projection catch-up
- Domain has ZERO infra dependencies — Application defines ports (interfaces), Infrastructure implements them
- All projection handlers use ONLY self-contained event data — no external queries during projection (V-5 Rule B)

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# / .NET 8 (SDK pinned to 8.0.418 via `global.json`) |
| Event store + document DB | PostgreSQL 16 + Marten 7.0.0 |
| Message bus + sagas | MassTransit 8.1.3 + RabbitMQ (dev only) |
| State-based persistence | EF Core 8.0.13 + Npgsql 8.0.8 |
| API | ASP.NET Core 8, Swashbuckle 6.5.0 |
| Frontend | Blazor Server |
| Validation | FluentValidation 11.9.0 |
| Scheduling | Hangfire 1.8.x (PostgreSQL backend) |
| Logging | Serilog 3.1.1 + ASP.NET Core sink |
| Observability | OpenTelemetry (4 packages) + Jaeger |
| Feature flags | LaunchDarkly 8.11.1 |
| Caching | Redis (StackExchange.Redis 2.8.24) |
| Package management | Central Package Management (`Directory.Packages.props` — 50 packages, no `Version=` in csproj) |

---

## Core Aggregates

| Aggregate | Storage | Dependency Level |
|-----------|---------|-----------------|
| StockLedger | Event-sourced (Marten) | 1 — foundation, no deps |
| HandlingUnit | State-based (Marten projection) | 2 |
| WarehouseLayout | State-based (EF Core) | 2 |
| LogicalWarehouse | State-based (EF Core) | 2 |
| Reservation | Event-sourced (Marten) | 3 |
| Valuation | Event-sourced (Marten) | 3 |

### Key Invariants

| Invariant | Enforced In |
|-----------|------------|
| No negative balance | `StockLedger.RecordMovement()` — throws `InsufficientBalanceException` |
| Optimistic concurrency | `MartenStockLedgerRepository.AppendEventAsync()` — expected-version check |
| HARD lock serialization | `MartenStartPickingOrchestration` — `pg_advisory_xact_lock` per (location, SKU) |
| Balance-vs-HardLock serialization (CRIT-01) | `StockLockKey` canonical key + `IBalanceGuardLockFactory` |
| ActiveHardLocks consistency | `ActiveHardLocksProjection` — inline, same transaction as event append |
| Ledger-first pick ordering | `MartenPickStockOrchestration.ExecuteAsync()` |
| Command idempotency | `IdempotencyBehavior` + `MartenProcessedCommandStore` — atomic INSERT claim |
| Sealed HU immutability | `HandlingUnitProjection` — ignores post-seal mutations |
| Self-contained events | `PickingStartedEvent.HardLockedLines`, `ReservationConsumedEvent.ReleasedHardLockLines` |

---

## Implemented Packages (A–F + Hotfixes G–H)

| Package | Description |
|---------|-------------|
| **A** | StockLedger V-2: event-sourced aggregate, stream partitioning, expected-version append, bounded retry (3x) |
| **B** | StartPicking R-3 + ActiveHardLocks R-4: advisory-lock serialization, balance re-validation, inline projection |
| **C** | LocationBalance async projection + V-5 rebuild tooling: shadow table, global-sequence replay, atomic swap |
| **D** | AvailableStock async projection: onHandQty - hardLockedQty, custom grouper across StockLedger + Reservation streams |
| **E** | HandlingUnit projection + ReceiveGoods: HU lifecycle events, StockMoved-driven line updates, sealed-HU guard |
| **F** | Allocation + PickStock sagas: SOFT allocation from AvailableStock, PickStock with durable MassTransit retry, DLQ |
| **G (CRIT-01)** | Advisory lock gap fix: `StockLockKey` canonical key, `IBalanceGuardLockFactory` abstraction for all balance-decreasing movements |
| **H (CRIT-02)** | AvailableStock rebuild fix: global-sequence event replay, field-based checksums (replacing JSONB `data::text`) |

---

## Available Read Models & Commands

### Read Models (query these — NEVER query event streams directly for UI)

| View | Currency | Description |
|------|---------|-------------|
| `LocationBalanceView` | Async, ≤5s lag | Balance per (warehouseId, location, SKU) |
| `AvailableStockView` | Async, ≤5s lag | onHand minus hardLocked — use for allocation UI |
| `ActiveHardLockView` | Inline (instant) | Current HARD locks — use for conflict display |
| `HandlingUnitView` | Async | HU state with lines — use for HU list/detail |

### Commands Available

- `RecordStockMovementCommand` — receipt, transfer, dispatch, adjustment
- `ReceiveGoodsCommand` — goods receipt (creates HU + movements atomically)
- `AllocateReservationCommand` — SOFT lock allocation from AvailableStock
- `StartPickingCommand` — SOFT→HARD transition with balance re-validation
- `PickStockCommand` — pick against HARD-locked reservation
- `RebuildProjectionCommand` — trigger projection rebuild with verification

---

## Integration Points

| System | Mechanism | Notes |
|--------|----------|-------|
| **Agnum** | Scheduled batch (daily 23:00) | CSV + API, configurable mapping |
| **ERP/MES** | Kafka-based | Anti-corruption layer: `MaterialRequested` ↔ `CreateReservation` |
| **Label Printing** | ZPL over TCP:9100 | Retry 3x, fallback to manual queue |
| **Scanners** | Keyboard wedge | No server integration |

---

## Document Map

### Architecture docs (by Claude — source of truth)

| Doc | Contents |
|-----|---------|
| `docs/01-discovery.md` | Actors, use cases, bounded contexts, domain glossary |
| `docs/02-warehouse-domain-model.md` | Aggregates, invariants, event sourcing strategy |
| `docs/03-implementation-guide.md` | Workflows, commands, process managers, read models |
| `docs/04-system-architecture.md` | **FINAL BASELINE v2.0** — 5 mandatory decisions, transaction model, offline model, integration arch |

### Spec docs (planning & implementation)

| Doc | Contents |
|-----|---------|
| `docs/spec/10-feature-decomposition.md` | 16 feature groups (FG-01..FG-16) |
| `docs/spec/20-phase-plan.md` | 6 phases: P0-P5+ |
| `docs/spec/30-epics-and-stories.md` | Epics/stories per phase, traced to 18 requirements |
| `docs/spec/40-technical-implementation-guidelines.md` | Do/Don't rulebook |
| `docs/spec/50-risk-and-delivery-strategy.md` | Risk register, rollout strategy |

### Kiro specs (Phase 1 detail)

| Doc | Contents |
|-----|---------|
| `.kiro/specs/warehouse-core-phase1/requirements.md` | 18 requirements |
| `.kiro/specs/warehouse-core-phase1/design.md` | Technical design |
| `.kiro/specs/warehouse-core-phase1/tasks.md` | Task breakdown |
| `.kiro/specs/warehouse-core-phase1/implementation-blueprint-part1.md` | Marten config, aggregate persistence, command pipeline, outbox, saga runtime |
| `.kiro/specs/warehouse-core-phase1/implementation-blueprint-part2.md` | Projection runtime, event versioning, offline sync, integration adapters, observability |

### Supporting docs

| Doc | Contents |
|-----|---------|
| `src/SOLUTION_STRUCTURE.md` | Canonical solution layout + dependency graph + build commands |
| `docs/dependency-map.md` | Full csproj reference graph, NuGet hotspot analysis, coupling findings |
| `docs/audit/2026-02-21-post-refactor-audit.md` | Post-refactor audit: what passed, open issues |
| `docs/claude/claude.md` | Operational context: packages A–F, invariant table, hotfixes G–H |
| `docs/adr/` | Architecture Decision Records (001-005 + ADR-002) |
| `docs/dev-auth-guide.md` | Dev authentication setup |
| `docs/dev-db-update.md` | Database migration guide |

---

## What NOT To Do

- NEVER bypass MediatR pipeline (idempotency behavior must execute for every command)
- NEVER query StockLedger event stream directly for UI display (use LocationBalance/AvailableStock read models)
- NEVER modify `ActiveHardLockView` directly (it is an inline projection updated by events only)
- NEVER assume a projection is up-to-date (show "Refreshing…" if lag > 5s)
- NEVER write StockMoved events from outside StockLedger (Decision 1)
- NEVER wrap cross-aggregate operations in a single DB transaction (use sagas)
- NEVER add a `Version=` attribute to any `<PackageReference>` in a csproj (CPM is enforced)
- NEVER add project references from BuildingBlocks to Modules
- NEVER add Marten dependencies to Application layer (Marten belongs in Infrastructure + Projections)
- NEVER redesign the architecture — `docs/04-system-architecture.md` v2.0 is the frozen baseline

---

## Known Open Items (Phase 2 scope)

| ID | Severity | Description |
|----|----------|-------------|
| HIGH-02 | By design | `PickStockFailedPermanentlyEvent` does NOT release HARD locks — needs supervisor intervention. Implement `ReservationTimeoutSaga` for auto-cancel after 2-hour policy |
| MED-02 | Low impact | Virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM) create phantom AvailableStock documents — filter at projection or query layer |
| ARCH-01 | Tech debt | `MasterDataEntities.cs` is a god object (~1400 LOC, 50+ entities across 8+ bounded contexts) — must be decomposed before any module extraction |
| ARCH-02 | Tech debt | Business logic in `Api.Services/` (34 files) belongs in Application layer — `SalesOrderCommandHandlers.cs`, `ValuationLifecycleCommandHandlers.cs`, etc. |
