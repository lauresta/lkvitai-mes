# Implementation Plan: Warehouse Core Phase 1

## Overview

This implementation plan breaks down the Phase 1 warehouse system into incremental development tasks. The system will be built as a modular monolith in C# (.NET 8+) with event sourcing for critical aggregates, hybrid reservation locking, offline edge operations, and multi-layered integration.

**Implementation Language:** C# (.NET 8+)

**Architecture:** Modular monolith with clear module boundaries

**Key Technologies:**
- Marten for event sourcing and document storage
- PostgreSQL for relational storage
- MassTransit for event bus and saga orchestration
- ASP.NET Core for API
- Entity Framework Core for state-based aggregates

**Implementation Approach:**
- Build foundation first (event store, outbox, command infrastructure)
- Implement aggregates incrementally (StockLedger → HandlingUnit → Reservation)
- Add sagas for workflows
- Implement projections
- Add offline/edge support
- Integrate external systems

## Tasks

- [ ] 1. Solution Structure and Foundation
  - Create modular monolith solution structure
  - Set up project dependencies and module boundaries
  - Configure Marten for event sourcing
  - Configure PostgreSQL database
  - Set up transactional outbox pattern
  - _Requirements: All (foundation for entire system)_

- [x] 2. Implement StockLedger Aggregate (Event Sourced) — **DONE (Package A)**
  - [x] 2.1 Create StockLedger aggregate with RecordStockMovement command — **DONE**
    - Define StockMovement value objects (MovementId, SKU, Quantity, Location, etc.)
    - Implement RecordStockMovement command handler
    - Implement balance validation logic (physical locations only)
    - Emit StockMoved event
    - _Requirements: 1.1, 1.2, 1.3, 1.6, 1.7_
    - **Files:** `Domain/Aggregates/StockLedger.cs`, `Domain/MovementType.cs`, `Domain/StockLedgerStreamId.cs`, `Domain/InsufficientBalanceException.cs`, `Application/Commands/RecordStockMovementCommand.cs`, `Application/Commands/RecordStockMovementCommandHandler.cs`, `Application/Ports/IStockLedgerRepository.cs`, `Infrastructure/Persistence/MartenStockLedgerRepository.cs`
    - **Tests:** `Tests.Unit/StockLedgerTests.cs`, `Tests.Unit/StockLedgerStreamIdTests.cs`, `Tests.Unit/RecordStockMovementCommandHandlerTests.cs`
  
  - [x]* 2.2 Write property test for StockLedger balance non-negativity — **DONE**
    - **Property 2: Balance Non-Negativity**
    - **Validates: Requirements 1.2**
    - **Tests:** `Tests.Property/StockLedgerPropertyTests.cs` → `NoNegativeBalance_AfterAnyValidEventSequence`
  
  - [ ]* 2.3 Write property test for movement immutability
    - **Property 1: Movement Immutability**
    - **Validates: Requirements 1.1**
  
  - [x]* 2.4 Write property test for movement constraints — **DONE**
    - **Property 4: Movement Constraint Validation**
    - **Validates: Requirements 1.6, 1.7**
    - **Tests:** `Tests.Property/StockLedgerPropertyTests.cs` → `RecordMovement_AlwaysRejects_NonPositiveQuantity`, `SameFromTo_AlwaysRejected_ForTransfer`
  
  - [ ]* 2.5 Write property test for virtual location bypass
    - **Property 3: Virtual Location Bypass**
    - **Validates: Requirements 1.3**
  
  - [x] 2.6 Implement StockLedger concurrency enforcement [MITIGATION V-2] — **DONE**
    - Add expected-version append to RecordStockMovement
    - Implement retry logic with exponential backoff (max 3 retries)
    - Handle EventStreamUnexpectedMaxEventIdException
    - Return concurrency error after retries exhausted
    - _Requirements: 1.9, 1.10, 1.11_
    - **Files:** `Application/Commands/RecordStockMovementCommandHandler.cs` (Polly retry, max 3, exponential backoff), `Infrastructure/Persistence/MartenStockLedgerRepository.cs` (expected-version append via Marten)
    - **Tests:** `Tests.Unit/RecordStockMovementCommandHandlerTests.cs` → `Handle_ShouldRetry_OnConcurrencyConflict_AndSucceedOnSecondAttempt`, `Handle_ShouldFail_AfterMaxRetries`
  
  - [x]* 2.7 Write property test for StockLedger atomic balance validation [MITIGATION V-2] — **DONE**
    - **Property 50: StockLedger Atomic Balance Validation**
    - **Validates: Requirements 1.9-1.11**
    - **Tests:** `Tests.Property/StockLedgerPropertyTests.cs` → `NoNegativeBalance_AfterAnyValidEventSequence`, `InsufficientBalance_AlwaysDetected`, `TotalStock_IsConserved_ByInternalTransfers`

- [ ] 3. Implement Command Infrastructure
  - [ ] 3.1 Create command handler pipeline with validation
    - Implement ICommandHandler<TCommand, TResult> interface
    - Implement command validation pipeline
    - Implement command idempotency via processed_commands table
    - Wire up MediatR or custom command dispatcher
    - _Requirements: 12.1, 12.2, 12.3_
  
  - [ ]* 3.2 Write property test for command idempotency
    - **Property 36: Command Idempotency**
    - **Validates: Requirements 12.1-12.3**

- [ ] 4. Implement Transactional Outbox
  - [ ] 4.1 Create outbox message schema and processor
    - Define outbox_messages table schema
    - Implement outbox message writer (transactional with aggregate changes)
    - Implement outbox processor (background service)
    - Configure retry logic with exponential backoff
    - _Requirements: 1.5_
  
  - [ ]* 4.2 Write unit tests for outbox processor
    - Test message publishing
    - Test retry logic
    - Test failure scenarios
    - _Requirements: 1.5_

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implement HandlingUnit Aggregate (State-Based with Projection) — **Partially DONE (Package E: read model + projection)**
  - [ ] 6.1 Create HandlingUnit aggregate with state-based storage
    - Define HandlingUnit entity and HandlingUnitLine entity
    - Implement CreateHandlingUnit command
    - Implement AddLine, RemoveLine, SealHandlingUnit commands
    - Implement sealed immutability invariant
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.8_
    - **Note (Package E):** HU lifecycle events defined in `Contracts/Events/HandlingUnitEvents.cs` (HandlingUnitCreated, LineAdded, LineRemoved, Sealed, Split/Merge TODO placeholders). Full EF Core aggregate with sealed immutability deferred to later package.
  
  - [x] 6.2 Implement HandlingUnit projection from StockMoved events — **DONE (Package E)**
    - Subscribe to StockMoved events
    - Update HU lines when StockMoved event references HU
    - Update HU location when movement changes location
    - Implement idempotent event processing
    - _Requirements: 2.6, 2.7_
    - **Files:** `Contracts/Events/HandlingUnitEvents.cs` (6 event types), `Contracts/ReadModels/HandlingUnitView.cs` (flat doc keyed by huId), `Projections/HandlingUnitProjection.cs` (MultiStreamProjection with CustomGrouping — consumes HU lifecycle events + StockMovedEvent), `Projections/ProjectionRegistration.cs` (Async lifecycle)
    - **Tests:** `Tests.Unit/HandlingUnitProjectionTests.cs` (18 unit tests: created, lineAdded, lineRemoved, sealed, StockMoved receipt/pick/transfer/partial/multi-sku, before-created edge case, lifecycle, replay determinism, V-5 Rule B, IsEmpty)
    - **V-5 Compliance:** Rule B — all events self-contained; projection uses only event data; CustomGrouping routes by huId from event stream
  
  - [ ]* 6.3 Write property test for sealed HU immutability
    - **Property 7: Sealed Handling Unit Immutability**
    - **Validates: Requirements 2.3**
  
  - [ ]* 6.4 Write property test for empty HU seal rejection
    - **Property 8: Empty Handling Unit Seal Rejection**
    - **Validates: Requirements 2.4**
  
  - [ ]* 6.5 Write property test for HU single location invariant
    - **Property 11: Handling Unit Single Location**
    - **Validates: Requirements 2.8**

- [ ] 7. Implement Reservation Aggregate (Event Sourced)
  - [ ] 7.1 Create Reservation aggregate with hybrid locking
    - Define Reservation entity and ReservationLine entity
    - Implement CreateReservation command
    - Implement AllocateReservation command (SOFT lock)
    - Implement ConsumeReservation command
    - Implement bumping logic
    - _Requirements: 3.1, 3.2, 3.3, 3.9, 3.10-3.12_
  
  - [ ]* 7.2 Write property test for soft lock overbooking
    - **Property 16: Soft Lock Overbooking**
    - **Validates: Requirements 3.3**
  
  - [ ]* 7.3 Write property test for hard lock exclusivity
    - **Property 20: Hard Lock Exclusivity**
    - **Validates: Requirements 3.7**
  
  - [ ]* 7.4 Write property test for hard reservation no auto-expiry
    - **Property 22: Hard Reservation No Auto-Expiry**
    - **Validates: Requirements 3.10-3.12**
  
  - [x] 7.5 Implement StartPicking orchestration [MITIGATION R-3] — **DONE (Package B)**
    - Create StartPicking command handler (delegates to IStartPickingOrchestration)
    - Re-validate balance from event stream (not projection)
    - Query ActiveHardLocks projection for conflict detection
    - Acquire HARD lock with expected-version append
    - PostgreSQL advisory locks (pg_advisory_xact_lock) for cross-reservation serialization
    - Implement retry logic with exponential backoff (max 3 retries)
    - Update ActiveHardLocks projection inline (same transaction)
    - _Requirements: 3.4, 3.5, 3.13, 3.14, 3.15, 3.16_
    - **Files:** `Application/Commands/StartPickingCommandHandler.cs`, `Application/Orchestration/IStartPickingOrchestration.cs`, `Application/Ports/IReservationRepository.cs`, `Application/Ports/IActiveHardLocksRepository.cs`, `Infrastructure/Persistence/MartenStartPickingOrchestration.cs`, `Infrastructure/Persistence/MartenReservationRepository.cs`, `Infrastructure/Persistence/MartenActiveHardLocksRepository.cs`, `Infrastructure/DependencyInjection.cs`
    - **Tests:** `Tests.Unit/ActiveHardLocksProjectionTests.cs` (AdvisoryLockKeyTests), `Tests.Integration/StartPickingConcurrencyTests.cs` (concurrent StartPicking, different stock, non-existent, pending, hard lock rows created)
  
  - [ ]* 7.6 Write property test for StartPicking atomic HARD lock acquisition [MITIGATION R-3]
    - **Property 51: StartPicking Atomic HARD Lock Acquisition**
    - **Validates: Requirements 3.13-3.16**

- [ ] 8. Implement Valuation Aggregate (Event Sourced)
  - [ ] 8.1 Create Valuation aggregate for cost management
    - Define Valuation entity and CostAdjustment entity
    - Implement ApplyCostAdjustment command
    - Implement AllocateLandedCost command
    - Implement WriteDownStock command
    - Require reason and approver for all adjustments
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.7_
  
  - [ ]* 8.2 Write property test for valuation independence
    - **Property 33: Valuation Independence**
    - **Validates: Requirements 10.7**

- [ ] 9. Implement WarehouseLayout Aggregate (State-Based)
  - [ ] 9.1 Create WarehouseLayout aggregate for physical topology
    - Define Warehouse, Aisle, Rack, Bin entities
    - Implement DefineBin command
    - Implement coordinate uniqueness validation
    - Implement capacity constraints
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.6_
  
  - [ ]* 9.2 Write property test for coordinate uniqueness
    - **Property 34: Layout Coordinate Uniqueness**
    - **Validates: Requirements 11.1**

- [ ] 10. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Implement LocationBalance Projection — **DONE (Package C)**
  - [x] 11.1 Create LocationBalance projection from StockMoved events — **DONE**
    - Subscribe to StockMoved events
    - Update location_balance table (UPSERT for idempotency)
    - Implement projection lag monitoring
    - _Requirements: 6.1, 6.2_
    - **Files:** `Projections/LocationBalanceProjection.cs` (MultiStreamProjection with custom grouper), `Application/Queries/GetLocationBalanceQuery.cs`, `Application/Ports/ILocationBalanceRepository.cs`, `Infrastructure/Persistence/MartenLocationBalanceRepository.cs`
    - **Tests:** `Tests.Unit/LocationBalanceProjectionTests.cs` (4 unit tests for Apply logic), `Tests.Integration/LocationBalanceRebuildTests.cs` (3 integration tests for rebuild)
  
  - [ ]* 11.2 Write property test for projection rebuild correctness
    - **Property 27: Projection Rebuild Correctness**
    - **Validates: Requirements 6.11, 6.12**

- [x] 12. Implement AvailableStock Projection — **DONE (Package D)**
  - [x] 12.1 Create AvailableStock projection from StockMoved and Reservation events — **DONE**
    - Subscribe to StockMovedEvent (onHand), PickingStartedEvent / ReservationConsumedEvent / ReservationCancelledEvent (hardLocked)
    - Compute available = max(0, onHand - hardLocked); SOFT allocations do NOT reduce availability (Req 3.3 overbooking)
    - V-5 Rule B: self-contained event data (ReleasedHardLockLines added to Consumed/Cancelled events)
    - MultiStreamProjection<AvailableStockView, string> with CustomGrouping (Async lifecycle)
    - _Requirements: 6.7_
    - **Files:** `Contracts/ReadModels/AvailableStockView.cs`, `Projections/AvailableStockProjection.cs` (projection + grouper + static aggregation helper), `Projections/ProjectionRegistration.cs` (Async lifecycle), `Application/Queries/GetAvailableStockQuery.cs` (query + DTO + handler), `Application/Ports/IAvailableStockRepository.cs`, `Infrastructure/Persistence/MartenAvailableStockRepository.cs`, `Infrastructure/DependencyInjection.cs`
    - **Events enriched:** `Contracts/Events/ReservationEvents.cs` — added `ReleasedHardLockLines` to `ReservationConsumedEvent` and `ReservationCancelledEvent` for V-5 Rule B compliance
    - **Tests:** `Tests.Unit/AvailableStockProjectionTests.cs` (16 unit tests: receipt, pick, transfer, hardLock, consume, cancel, non-negative guard, full lifecycle, ComputeId, empty lines, V-5 Rule B), `Tests.Integration/AvailableStockIntegrationTests.cs` (3 Docker-gated integration tests: StockMoved-only, StockMoved+PickingStarted, full lifecycle)

- [ ] 13. Implement OnHandValue Projection
  - [ ] 13.1 Create OnHandValue projection from LocationBalance and Valuation
    - Subscribe to StockMoved and CostAdjusted events
    - Compute value = quantity × unit cost
    - Update on_hand_value table
    - _Requirements: 6.8_

- [x] 14. Implement ActiveHardLocks Projection [MITIGATION R-4] — **DONE (Package B)**
  - [x] 14.1 Create ActiveHardLocks projection from Reservation events — **DONE**
    - Subscribe to PickingStarted events (INSERT row)
    - Subscribe to ReservationConsumed events (DELETE row)
    - Subscribe to ReservationCancelled events (DELETE row)
    - Update active_hard_locks table inline (same transaction as event)
    - Implement idempotent event processing
    - _Requirements: 19.1-19.8_
    - **Files:** `Projections/ActiveHardLocksProjection.cs` (EventProjection with multi-row store), `Projections/ProjectionRegistration.cs` (Inline lifecycle), `Contracts/ReadModels/ActiveHardLockView.cs` (read-model schema shared by Projections and Infrastructure)
    - **Tests:** `Tests.Unit/ActiveHardLocksProjectionTests.cs` (12 unit tests: Store, DeleteWhere, ComputeId, idempotency)
    - **Boundary fix:** ActiveHardLockView moved from Projections to Contracts to avoid Infrastructure→Projections reference
  
  - [ ]* 14.2 Write property test for ActiveHardLocks consistency [MITIGATION R-4]
    - **Property 52: ActiveHardLocks Consistency**
    - **Validates: Requirement 19**

- [ ] 14. Implement ReceiveGoodsSaga — **Partially DONE (Package E: minimal orchestration)**
  - [x] 14.1 Create ReceiveGoods minimal workflow — **DONE (Package E)**
    - Implement minimal ReceiveGoods orchestration (command handler + orchestration port + Marten implementation)
    - StockLedger-first ordering: HandlingUnitCreated → StockMoved (per line) → HandlingUnitSealed in single atomic Marten session
    - Atomic transaction boundary (all or nothing via lightweight session)
    - _Requirements: 15.1-15.9 (minimal Phase 1 subset)_
    - **Files:** `Application/Commands/ReceiveGoodsCommand.cs` (command + ReceiveGoodsLineDto), `Application/Commands/ReceiveGoodsCommandHandler.cs` (MediatR handler with validation), `Application/Orchestration/IReceiveGoodsOrchestration.cs` (port), `Infrastructure/Persistence/MartenReceiveGoodsOrchestration.cs` (atomic multi-stream implementation), `Infrastructure/DependencyInjection.cs` (DI registration)
    - **Tests:** `Tests.Integration/ReceiveGoodsIntegrationTests.cs` (3 Docker-gated integration tests: HU view creation, LocationBalance update, AvailableStock update)
    - **Deferred:** Full MassTransit saga state machine (STARTED → MOVEMENT_RECORDED → HU_CREATED → HU_SEALED → LABEL_REQUESTED → COMPLETED), compensation logic, label print step
  
  - [ ]* 14.2 Write unit tests for ReceiveGoodsSaga
    - Test happy path
    - Test compensation scenarios
    - Test idempotency
    - _Requirements: 15.1-15.9_

- [ ] 15. Implement TransferStockSaga
  - [ ] 15.1 Create TransferStockSaga for HU transfer workflow
    - Implement saga state machine
    - Validate destination location exists
    - Record StockMovement for each HU line
    - Update HU location
    - Implement compensation (abort if any movement fails)
    - _Requirements: 16.1-16.5_
  
  - [ ]* 15.2 Write unit tests for TransferStockSaga
    - Test happy path
    - Test validation failures
    - Test compensation
    - _Requirements: 16.1-16.5_

- [x] 16. Implement PickStockSaga (CRITICAL - Transaction Ordering) [MITIGATION V-3] — **DONE (Package F)**
  - [x] 16.1 Create PickStockSaga with simplified two-step process
    - MassTransit state machine with durable retry (no Task.Delay) + DLQ
    - V-3 compliant: StockMovement recorded FIRST, HU projection NOT waited on
    - Step 1: Validate reservation is PICKING (HARD locked)
    - Step 2: Record StockMovement via StockLedger FIRST
    - Step 3: Consume reservation (independent of HU projection)
    - Durable retry via MassTransit Schedule with exponential backoff (5s, 15s, 45s)
    - Permanent failure → PickStockFailedPermanentlyEvent (DLQ/supervisor alert)
    - Files: Sagas/PickStockSaga.cs, Application/Commands/PickStockCommand.cs,
      Application/Commands/PickStockCommandHandler.cs, Application/Orchestration/IPickStockOrchestration.cs,
      Infrastructure/Persistence/MartenPickStockOrchestration.cs, Contracts/Messages/PickStockMessages.cs,
      Application/Ports/IEventBus.cs, Api/Services/MassTransitEventBus.cs
    - _Requirements: 4.1-4.7, 17.1-17.12_
  
  - [ ]* 16.2 Write property test for pick transaction ordering
    - **Property 23: Pick Transaction Ordering**
    - **Validates: Requirements 4.1-4.7, 17.3-17.12**
    - Deferred to Phase 2 (property tests)
  
  - [x]* 16.3 Write unit tests for PickStockSaga — **DONE (18 tests)**
    - Activity tests (3): orchestration call success/failure/exception
    - Saga definition tests (4): states, events, schedule, max retries
    - Saga state tests (3): defaults, properties, interface compliance
    - Message contract tests (3): default values, field access
    - Handler tests (5): full success, movement fail, consumption deferred, not picking, correlation id
    - _Requirements: 4.1-4.7, 17.1-17.12_

- [ ] 17. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 18. Implement AllocationSaga — **DONE (Package F)**
  - [x] 18.1 Create AllocationSaga for reservation allocation
    - MediatR command handler + Marten orchestration (single-step, no saga needed)
    - Queries AvailableStock projection for candidates (warehouseId, SKU)
    - Allocates stock to reservation (SOFT lock via StockAllocatedEvent)
    - Expected-version append with bounded retry for concurrency safety
    - Idempotent via CommandId + ProcessedCommandStore
    - Files: Application/Commands/AllocateReservationCommand.cs,
      Application/Commands/AllocateReservationCommandHandler.cs,
      Application/Orchestration/IAllocateReservationOrchestration.cs,
      Infrastructure/Persistence/MartenAllocateReservationOrchestration.cs
    - Tests: 4 unit tests (handler delegation, error codes, parameters)
    - _Requirements: 3.2_

- [ ] 19. Implement Split and Merge Operations
  - [ ] 19.1 Implement SplitHandlingUnit command
    - Validate source HU is not SEALED
    - Create new HU at same location
    - Reduce source HU quantity
    - Emit HandlingUnitSplit event
    - Do NOT record StockMovement (same location)
    - _Requirements: 2.9, 18.1-18.5_
  
  - [ ] 19.2 Implement MergeHandlingUnits command
    - Validate all HUs at same location
    - Validate target HU is not SEALED
    - Transfer lines to target HU
    - Mark source HUs as EMPTY
    - Emit HandlingUnitMerged event
    - Do NOT record StockMovement (same location)
    - _Requirements: 2.10, 18.6-18.11_
  
  - [ ]* 19.3 Write property test for split operation correctness
    - **Property 12: Split Operation Correctness**
    - **Validates: Requirements 2.9**
  
  - [ ]* 19.4 Write property test for merge operation correctness
    - **Property 13: Merge Operation Correctness**
    - **Validates: Requirements 2.10**

- [ ] 20. Implement Label Printing Integration (Operational Integration)
  - [ ] 20.1 Create label printing adapter
    - Subscribe to HandlingUnitSealed events
    - Generate ZPL (Zebra Programming Language) label format
    - Send to printer via HTTP or TCP
    - Implement retry logic (3 attempts)
    - Implement idempotency via PrintJobId
    - Alert operator on failure
    - _Requirements: 8.1-8.7_
  
  - [ ]* 20.2 Write property test for label print idempotency
    - **Property 29: Label Print Idempotency**
    - **Validates: Requirements 8.5, 8.6**

- [ ] 21. Implement Agnum Export Integration (Financial Integration)
  - [ ] 21.1 Create AgnumExportSaga
    - Implement saga state machine
    - Query StockMovement ledger for balances
    - Query Valuation for unit costs
    - Query LogicalWarehouse for category mappings
    - Apply Agnum mapping rules
    - Transform to Agnum format (CSV or JSON)
    - POST to Agnum API with ExportId
    - Implement retry with exponential backoff
    - _Requirements: 9.1-9.10_
  
  - [ ]* 21.2 Write property test for Agnum export idempotency
    - **Property 31: Agnum Export Idempotency**
    - **Validates: Requirements 9.6**

- [ ] 22. Implement ERP Integration (Process Integration)
  - [ ] 22.1 Create ERP Integration Saga
    - Subscribe to MaterialRequested events from ERP
    - Translate to CreateReservation command
    - Create reservation
    - Wait for AllocationSaga to allocate
    - Send MaterialReserved event to ERP
    - Implement compensation and retry logic
    - _Requirements: 14.1-14.12_
  
  - [ ] 22.2 Create MaterialConsumed event publisher
    - Subscribe to StockMoved events (to PRODUCTION location)
    - Translate to MaterialConsumed event
    - Send to ERP with unique event ID
    - Implement idempotency
    - _Requirements: 14.3, 14.11, 14.12_
  
  - [ ]* 22.3 Write property test for ERP event idempotency
    - **Property 42: ERP Event Idempotency**
    - **Validates: Requirements 14.11-14.12**

- [ ] 23. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 24. Implement Offline/Edge Architecture
  - [ ] 24.1 Create edge command queue and sync engine
    - Define offline_command_queue schema
    - Implement command whitelist enforcement (PickStock with HARD lock, TransferStock with assigned HUs)
    - Implement sync engine (FIFO queue processing)
    - Implement conflict detection (reservation bumped, HU moved)
    - Generate reconciliation report
    - _Requirements: 5.1-5.9_
  
  - [ ]* 24.2 Write property test for offline operation whitelist
    - **Property 24: Offline Operation Whitelist**
    - **Validates: Requirements 5.1-5.6**
  
  - [ ]* 24.3 Write property test for offline sync rejection
    - **Property 25: Offline Sync Rejection**
    - **Validates: Requirements 5.8**

- [ ] 25. Implement Consistency Verification
  - [ ] 25.1 Create daily consistency check job
    - Verify LocationBalance matches event stream computation
    - Verify no negative balances
    - Verify HU contents match LocationBalance
    - Verify no orphaned HUs
    - Verify consumed reservations released HUs
    - Verify event stream has no sequence gaps
    - Generate alerts for failures (P0, P1, P2)
    - _Requirements: 13.1-13.7_
  
  - [ ]* 25.2 Write property test for balance consistency verification
    - **Property 39: Balance Consistency Verification**
    - **Validates: Requirements 13.1**
  
  - [ ]* 25.3 Write property test for no negative balance verification
    - **Property 40: No Negative Balance Verification**
    - **Validates: Requirements 13.2**
  
  - [x] 25.4 Implement projection rebuild tooling [MITIGATION V-5] — **DONE (Package C)**
    - Create ProjectionRebuildService with shadow table approach
    - Implement stream-ordered replay (by sequence number, not timestamp)
    - Implement checksum computation for verification
    - Implement diff report generation (skeleton)
    - Implement atomic table swap with rollback capability
    - Create CLI command for rebuild operations (command/handler exists)
    - _Requirements: 6.13, 6.14, 6.15_
    - **Files:** `Infrastructure/Projections/ProjectionRebuildService.cs` (full shadow+verify+swap implementation), `Application/Commands/RebuildProjectionCommand.cs`, `Application/Projections/IProjectionRebuildService.cs`
    - **Tests:** `Tests.Integration/LocationBalanceRebuildTests.cs` (3 tests: live projection match, stream order validation, verification gate enforcement)
    - **V-5 Compliance:** Rule A (stream order), Rule B (self-contained), Rule C (shadow+verify+swap) all enforced
  
  - [ ]* 25.5 Write property test for projection rebuild determinism [MITIGATION V-5]
    - **Property 53: Projection Rebuild Determinism**
    - **Validates: Requirements 6.13-6.15**

- [ ] 26. Implement Observability Infrastructure
  - [ ] 26.1 Set up structured logging with Serilog
    - Configure structured logging format
    - Add correlation ID and trace ID propagation
    - Configure log levels and retention
    - _Requirements: All (cross-cutting)_
  
  - [ ] 26.2 Set up metrics with Prometheus
    - Instrument business metrics (movements/hour, picks/hour, etc.)
    - Instrument technical metrics (event store latency, projection lag, etc.)
    - Configure Prometheus scraping
    - _Requirements: All (cross-cutting)_
  
  - [ ] 26.3 Set up distributed tracing with OpenTelemetry
    - Configure OpenTelemetry SDK
    - Add tracing spans for commands, events, sagas
    - Configure trace export to Jaeger or Zipkin
    - _Requirements: All (cross-cutting)_
  
  - [ ] 26.4 Set up alerting with PagerDuty/Slack
    - Configure P0, P1, P2 alerts
    - Set up alert channels (PagerDuty, Slack, Email)
    - Test alert delivery
    - _Requirements: All (cross-cutting)_

- [ ] 27. Implement Operator UI (Basic Flows)
  - [ ] 27.1 Create goods receipt UI flow
    - Scan barcode input
    - Assign location dropdown
    - Submit button triggers ReceiveGoodsSaga
    - Display success/error messages
    - _Requirements: 7.1_
  
  - [ ] 27.2 Create transfer UI flow
    - Scan HU barcode input
    - Scan destination location input
    - Submit button triggers TransferStockSaga
    - Display success/error messages
    - _Requirements: 7.2_
  
  - [ ] 27.3 Create pick UI flow
    - Select reservation dropdown
    - Scan HU barcode input
    - Confirm quantity input
    - Submit button triggers PickStockSaga
    - Display success/error messages
    - _Requirements: 7.3_
  
  - [ ] 27.4 Add offline mode indicator and reconciliation report
    - Display "Offline Mode" indicator when disconnected
    - Display reconciliation report after sync
    - Disable forbidden operation buttons when offline
    - _Requirements: 7.7, 7.8_

- [ ] 28. Final Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 29. Integration Testing and End-to-End Scenarios
  - [ ]* 29.1 Write integration tests for complete workflows
    - Test goods receipt end-to-end
    - Test transfer end-to-end
    - Test pick end-to-end
    - Test offline sync end-to-end
    - _Requirements: All_

- [ ] 30. Performance Testing and Optimization
  - [ ]* 30.1 Run performance tests against targets
    - Test command processing latency
    - Test query performance
    - Test projection lag
    - Test throughput (movements/second)
    - Optimize as needed
    - _Requirements: All (performance targets in design)_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (minimum 100 iterations each)
- Unit tests validate specific examples and edge cases
- Integration tests validate end-to-end workflows
- The implementation follows strict architectural constraints:
  - StockLedger is the ONLY module that can append StockMoved events
  - Pick operations follow mandatory transaction ordering (StockLedger → HU Projection → Reservation)
  - Offline operations restricted to whitelist (PickStock with HARD lock, TransferStock with assigned HUs)
  - HARD reservations do NOT auto-expire
  - Integration separated into three layers (Operational <5s, Financial minutes, Process <30s)
