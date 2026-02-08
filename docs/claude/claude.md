# LKvitai.MES Warehouse — Context for AI Assistants

## Project Summary

LKvitai.MES is a warehouse management module (WMS) for a Manufacturing Execution System.
DDD + Event Sourcing + CQRS + Sagas. C# / .NET 8 modular monolith. PostgreSQL + Marten (event store) + MassTransit (saga orchestration).
StockLedger is the sole source of truth for inventory. HandlingUnit, Reservation, Valuation are downstream.
Architecture is frozen (docs/04-system-architecture.md v2.0 FINAL BASELINE). Do NOT redesign.
Phase 1 implementation is in progress — Packages A–F committed.

## Non-Negotiable Constraints

1. **Ledger-first ordering (Decision 2):** StockMovement MUST commit before HU/Reservation updates
2. **No projection waits (V-3):** Sagas/handlers NEVER wait for async projections to catch up
3. **No distributed locks or global transactions:** Use optimistic concurrency + advisory locks
4. **StockLedger sole owner (Decision 1):** Only StockLedger writes to stock_movement_events
5. **HARD locks cannot be bumped (Decision 4):** Only SOFT reservations are bumpable
6. **Offline restricted (Decision 3):** Only PickStock (HARD) and TransferStock (assigned HUs) offline
7. **Clean Architecture boundaries:** Domain has ZERO infra dependencies. Application defines ports (interfaces). Infrastructure implements them via Marten/EF Core. Projections project references Contracts only.
8. **V-5 Rule B:** All projection handlers use ONLY self-contained event data — no external queries

## Implemented Packages (A–F)

| Package | Description | Key Files |
|---------|-------------|-----------|
| **A** | StockLedger V-2: event-sourced aggregate, stream partitioning (warehouseId:location:sku), expected-version append, bounded retry (3x) | `StockLedger.cs`, `StockLedgerStreamId.cs`, `RecordStockMovementCommandHandler.cs`, `MartenStockLedgerRepository.cs` |
| **B** | StartPicking R-3 + ActiveHardLocks R-4: advisory-lock serialization, balance re-validation from event stream, inline projection, `PickingStartedEvent` carries `HardLockedLines` | `MartenStartPickingOrchestration.cs`, `ActiveHardLocksProjection.cs`, `ActiveHardLockView.cs` |
| **C** | LocationBalance async projection + V-5 rebuild tooling: shadow table, stream-order replay, MD5 checksum, atomic swap | `LocationBalanceProjection.cs`, `ProjectionRebuildService.cs` |
| **D** | AvailableStock async projection: onHandQty - hardLockedQty, custom grouper across StockLedger + Reservation streams | `AvailableStockProjection.cs`, `AvailableStockView.cs` |
| **E** | HandlingUnit projection + ReceiveGoods: HU lifecycle events, StockMoved-driven line updates, sealed-HU guard, atomic multi-stream commit | `HandlingUnitProjection.cs`, `MartenReceiveGoodsOrchestration.cs` |
| **F** | Allocation + PickStock sagas: SOFT allocation from AvailableStock, PickStock with durable MassTransit retry, DLQ via `PickStockFailedPermanentlyEvent` | `MartenAllocateReservationOrchestration.cs`, `MartenPickStockOrchestration.cs`, `PickStockSaga.cs` |

## Key Invariants & Enforcement

| Invariant | Where Enforced |
|-----------|----------------|
| No negative balance | `StockLedger.RecordMovement()` — throws `InsufficientBalanceException` |
| Optimistic concurrency (V-2) | `MartenStockLedgerRepository.AppendEventAsync()` — expected-version, catches `EventStreamUnexpectedMaxEventIdException` |
| HARD lock serialization (R-3) | `MartenStartPickingOrchestration` — `pg_advisory_xact_lock` per (location, SKU), sorted to prevent deadlocks |
| ActiveHardLocks consistency (R-4) | `ActiveHardLocksProjection` — Inline lifecycle, same-transaction as event append |
| Ledger-first pick ordering (V-3) | `MartenPickStockOrchestration.ExecuteAsync()` — StockMovement Step 2, Reservation Step 3 |
| Command idempotency | `IdempotencyBehavior` + `MartenProcessedCommandStore` — atomic INSERT-based claim |
| Sealed HU immutability | `HandlingUnitProjection` — ignores post-seal mutations |
| Self-contained events (V-5 Rule B) | `PickingStartedEvent.HardLockedLines`, `ReservationConsumedEvent.ReleasedHardLockLines` |

## Operational Policies

| Policy | Implementation |
|--------|---------------|
| **Durable retry (PickStock)** | MassTransit `Schedule` — 5s, 15s, 45s exponential backoff (NOT Task.Delay) |
| **DLQ / supervisor** | `PickStockFailedPermanentlyEvent` published after 3 exhausted retries |
| **Orphan HARD lock detection** | `OrphanHardLockCheck` — compares ActiveHardLocks vs Reservation PICKING state |
| **Stuck reservation detection** | `StuckReservationCheck` — flags PICKING reservations > 2 hours |
| **Projection rebuild (V-5)** | `ProjectionRebuildService` — shadow table + stream-order replay + checksum verify + atomic swap. Currently LocationBalance only |
| **HARD reservation timeout** | 2-hour policy (doc 04). Detected by `StuckReservationCheck`. Auto-cancellation NOT implemented yet |

## How to Implement UI Safely

### Read Models Available (query these, NEVER query event streams directly)
- `LocationBalanceView` — balance per (warehouseId, location, SKU). Async, may lag <5s
- `AvailableStockView` — available = onHand - hardLocked. Async. Use for allocation UI
- `ActiveHardLockView` — current HARD locks. Inline (instant). Use for conflict display
- `HandlingUnitView` — HU state with lines. Async. Use for HU list/detail screens

### Commands Available
- `RecordStockMovementCommand` — receipt, transfer, dispatch, adjustment
- `ReceiveGoodsCommand` — goods receipt (creates HU + movements atomically)
- `AllocateReservationCommand` — SOFT lock allocation from AvailableStock
- `StartPickingCommand` — SOFT→HARD transition with balance re-validation
- `PickStockCommand` — pick against HARD-locked reservation
- `RebuildProjectionCommand` — trigger projection rebuild with verification

### What NOT To Do
- NEVER bypass MediatR pipeline (idempotency behavior must execute)
- NEVER query StockLedger event stream for UI display (use LocationBalance/AvailableStock)
- NEVER modify ActiveHardLockView directly (it's an inline projection, updated by events)
- NEVER assume projection is up-to-date (show "Refreshing…" indicator if lag > 5s)
- NEVER write StockMoved events from outside StockLedger module (Decision 1)
- NEVER wrap cross-aggregate operations in a single DB transaction (use sagas)

### Stream ID Conventions
- StockLedger: `stock-ledger:{warehouseId}:{location}:{sku}` (via `StockLedgerStreamId.For()`)
- Reservation: `reservation-{reservationId}` (via `Reservation.StreamIdFor()`)
- HandlingUnit: `hu-{huId}` (Guid-based)

## Project Structure

```
src/
├── LKvitai.MES.Api/              # Composition root, DI, Program.cs
├── LKvitai.MES.Application/      # Commands, Queries, Ports, Orchestration interfaces, Behaviors
├── LKvitai.MES.Domain/           # Aggregates (StockLedger, Reservation, HandlingUnit)
├── LKvitai.MES.Infrastructure/   # Marten repos, orchestration impls, outbox, rebuild service
├── LKvitai.MES.Projections/      # Marten projections (inline + async)
├── LKvitai.MES.Sagas/            # MassTransit state machines
├── LKvitai.MES.Contracts/        # Events, Messages, ReadModel view types
├── LKvitai.MES.SharedKernel/     # DomainEvent, ICommand, Result, exceptions
├── LKvitai.MES.Integration/      # Agnum, LabelPrinting adapters (stubs)
└── tests/
    ├── Tests.Unit/                # Aggregate + projection + handler tests
    ├── Tests.Property/            # FsCheck invariant tests
    └── Tests.Integration/         # Testcontainers + Marten end-to-end
```
