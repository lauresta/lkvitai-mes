# CLAUDE.md - LKvitai.MES

## Project Overview

LKvitai.MES is a warehouse management module for a Manufacturing Execution System (MES). It provides real-time inventory tracking, physical location visualization (2D/3D), cycle counting, unit conversion, and accounting integration with Agnum.

**Current status:** Discovery & design done, implementation not started yet.

## Claude's Role

- Docs 01-04 are **my (Claude's) docs** — source of truth for architecture
- Other AI created: `docs/spec/` (10-50) and `.kiro/specs/warehouse-core-phase1/`
- My role going forward: **review reqs, design, tasks, and code review**

## Document Map

### Architecture docs (01-04, by Claude)
| Doc | What's inside |
|-----|---------------|
| `docs/01-discovery.md` | Actors, use cases, bounded contexts, domain glossary |
| `docs/02-warehouse-domain-model.md` | Aggregates, invariants, event sourcing strategy |
| `docs/03-implementation-guide.md` | Workflows, commands, process managers, read models |
| `docs/04-system-architecture.md` | **FINAL BASELINE v2.0** — 5 mandatory decisions, transaction model, offline model, integration arch, implementation checklists |

### Spec docs (other AI) — planning & implementation specs
| Doc | What's inside |
|-----|---------------|
| `docs/spec/10-feature-decomposition.md` | 16 feature groups (FG-01..FG-16): Foundation, Business Logic, Query, Operations, Integration, Infrastructure layers |
| `docs/spec/20-phase-plan.md` | 6 phases: P0-Foundation(4w), P1-Core Inventory(6w), P2-Reservation&Picking(6w), P3-Financial&Integration(5w), P4-Offline&Edge(4w), P5+ |
| `docs/spec/30-epics-and-stories.md` | Epics/stories per phase, traces to reqs (Req 1-18), design, tasks |
| `docs/spec/40-technical-implementation-guidelines.md` | Do/Don't rulebook: arch constraints, aggregate rules, event naming, command handlers, saga constraints, projection safety, offline sync, integration adapters, testing, code quality. References blueprint. |
| `docs/spec/50-risk-and-delivery-strategy.md` | Risk register, mitigation, spike tasks, rollout strategy, feature toggles, go-live gates, monitoring, rollback, team composition |

### Kiro specs (other AI) — detailed Phase 1 implementation
| Doc | What's inside |
|-----|---------------|
| `.kiro/.../requirements.md` | 18 requirements for Phase 1 |
| `.kiro/.../design.md` | Technical design: .NET 8+ modular monolith, Marten/PostgreSQL, MassTransit |
| `.kiro/.../tasks.md` | Task breakdown: solution structure → StockLedger → HU → Reservation → sagas → projections → offline → integrations |
| `.kiro/.../implementation-blueprint.md` | Part 1 (sect 1-5): Marten config, aggregate persistence, command pipeline, outbox, saga runtime |
| `.kiro/.../implementation-blueprint-part2.md` | Part 2 (sect 6-10): Projection runtime, event versioning, offline sync protocol, integration adapters, observability |

## Architecture Summary

- **DDD, Event Sourcing, CQRS, Sagas**
- **Decision 1 (CRITICAL):** StockLedger sole owner of StockMovement events
- **Decision 2 (CRITICAL):** Pick order: Ledger → HU projection → Reservation
- **Decision 3:** Offline: only PickStock (HARD lock) + TransferStock (assigned HUs)
- **Decision 4:** SOFT reservations bumpable, HARD not
- **Decision 5:** Integration: Operational (<5s), Financial (batch), Process (<30s)

## Tech Stack

- C# / .NET 8+, modular monolith
- PostgreSQL + Marten (event sourcing + document store)
- MassTransit (event bus, saga orchestration)
- ASP.NET Core API, EF Core for state-based aggregates

## Core Aggregates

| Aggregate | Storage | Level |
|-----------|---------|-------|
| StockLedger | Event-sourced | 1 (foundation, no deps) |
| HandlingUnit | State-based (projection) | 2 |
| WarehouseLayout | State-based | 2 |
| LogicalWarehouse | State-based | 2 |
| Reservation | Event-sourced | 3 |
| Valuation | Event-sourced | 3 |

## Key Invariants

- No negative stock
- Sealed handling units are immutable
- HARD reservation locks are exclusive
- Valuation is independent from physical quantities
- StockMovements are append-only and immutable

## Key Patterns

- Transactional Outbox, command dedup, event handler idempotency
- Saga step checkpoints, optimistic concurrency, READ COMMITTED
- Daily consistency checks (balance integrity, negative balances, orphaned HUs)

## Integration Points

- **Agnum** — scheduled export (daily 23:00), CSV + API, configurable mapping
- **ERP/MES** — Kafka-based, anti-corruption layer translates MaterialRequested <-> CreateReservation
- **Label Printing** — ZPL over TCP 9100, retry 3x, fallback to manual queue
- **Scanners** — keyboard wedge (no server integration)

## Conventions

- Documents follow numbered naming: `NN-description.md`
- Domain language is defined in the glossary (see `01-discovery.md`)
- Physical quantities and financial valuations are always kept separate
- Unit conversions are configuration-driven (data, not code)
- All operations produce immutable events for audit trail
