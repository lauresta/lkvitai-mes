# Technical Implementation Guidelines

**Project:** LKvitai.MES Warehouse Management System  
**Document:** Technical Implementation Guidelines  
**Version:** 1.0  
**Date:** February 2026  
**Status:** Implementation Specification

---

## Document Purpose

This document provides a "Do/Don't" rulebook for implementing the warehouse system. It translates architectural decisions into concrete coding standards, patterns, and constraints that developers must follow.

**CRITICAL:** This document references (but does NOT duplicate) the implementation blueprint. Always consult the blueprint for detailed technical specifications.

**Blueprint References:**
- `.kiro/specs/warehouse-core-phase1/implementation-blueprint.md` (Sections 1-6)
- `.kiro/specs/warehouse-core-phase1/implementation-blueprint-part2.md` (Sections 7-10)

---

## Table of Contents

1. [Architectural Constraints (MUST NOT VIOLATE)](#1-architectural-constraints-must-not-violate)
2. [Aggregate Implementation Rules](#2-aggregate-implementation-rules)
3. [Event Naming and Schema Conventions](#3-event-naming-and-schema-conventions)
4. [Command Handler Patterns](#4-command-handler-patterns)
5. [Saga Orchestration Constraints](#5-saga-orchestration-constraints)
6. [Projection Safety Rules](#6-projection-safety-rules)
7. [Offline Sync Rules](#7-offline-sync-rules)
8. [Integration Adapter Rules](#8-integration-adapter-rules)
9. [Testing Expectations](#9-testing-expectations)
10. [Code Quality Standards](#10-code-quality-standards)

---

## 1. Architectural Constraints (MUST NOT VIOLATE)

These are non-negotiable architectural rules. Violating these will break system correctness.

### Rule 1.1: Movement Ownership

**DO:**
- ✅ ONLY StockLedger aggregate can append StockMoved events
- ✅ All stock quantity changes MUST go through StockLedger.RecordMovement()
- ✅ Use database permissions to enforce: ONLY StockLedger service can INSERT into stock_movement_events

**DON'T:**
- ❌ NEVER write StockMoved events from HandlingUnit, Reservation, or any other aggregate
- ❌ NEVER directly INSERT into stock_movement_events table from application code
- ❌ NEVER bypass StockLedger for "convenience" or "performance"

**Why:** StockLedger is the single source of truth for quantities. Bypassing it creates inconsistencies.

**Blueprint Reference:** Section 2.1 (Event-Sourced Aggregates)

---

### Rule 1.2: Pick Transaction Ordering

**DO:**
- ✅ ALWAYS record StockMovement FIRST (commit transaction)
- ✅ THEN wait for HandlingUnit projection to process StockMoved event
- ✅ THEN update Reservation consumption
- ✅ Use PickStockSaga to enforce ordering

**DON'T:**
- ❌ NEVER mutate HandlingUnit state directly during pick
- ❌ NEVER update Reservation before StockMovement is recorded
- ❌ NEVER combine StockMovement + HU update in same transaction

**Why:** This ordering ensures consistency even during failures. If StockMovement fails, nothing happens. If HU projection fails, we replay from event. If Reservation fails, we retry.

**Blueprint Reference:** Section 3.3 (Transaction Boundaries)

**Code Example (CORRECT):**
```csharp
// PickStockSaga
public async Task Handle(PickStockCommand command)
{
    // Step 1: Record StockMovement FIRST
    var movementResult = await _mediator.Send(new RecordStockMovementCommand
    {
        SKU = command.SKU,
        Quantity = command.Quantity,
        FromLocation = command.FromLocation,
        ToLocation = "PRODUCTION",
        MovementType = MovementType.PICK,
        HandlingUnitId = command.HandlingUnitId
    });
    
    if (!movementResult.IsSuccess)
    {
        MarkAsFailed("StockMovement recording failed");
        return;
    }
    
    // Step 2: Wait for HU projection
    await WaitForProjection(command.HandlingUnitId, command.SKU, command.Quantity);
    
    // Step 3: Update Reservation
    await _mediator.Send(new ConsumeReservationCommand
    {
        ReservationId = command.ReservationId,
        Quantity = command.Quantity
    });
    
    MarkAsComplete();
}
```

---

### Rule 1.3: Offline Operation Whitelist

**DO:**
- ✅ ONLY allow PickStock offline (if reservation already HARD locked)
- ✅ ONLY allow TransferStock offline (if HU already assigned to operator)
- ✅ Enforce whitelist in edge agent code

**DON'T:**
- ❌ NEVER allow AllocateReservation offline
- ❌ NEVER allow StartPicking offline
- ❌ NEVER allow AdjustStock offline
- ❌ NEVER allow SplitHU or MergeHU offline

**Why:** Offline operations must be safe without server validation. Only operations with pre-validated state (HARD lock, assigned HU) are safe.

**Blueprint Reference:** Section 8 (Offline Sync Protocol)

---

### Rule 1.4: Reservation Bumping Rules

**DO:**
- ✅ SOFT locks CAN be bumped by higher priority or HARD lock
- ✅ HARD locks CANNOT be bumped (exclusive)
- ✅ Notify owner when reservation is bumped

**DON'T:**
- ❌ NEVER bump a HARD lock reservation
- ❌ NEVER auto-expire or auto-cancel HARD reservations
- ❌ NEVER allow manual cancellation of HARD reservation without explicit operator action

**Why:** HARD locks represent active picking operations. Bumping them would cause operators to lose work.

**Blueprint Reference:** Section 2.1 (Reservation Aggregate)

---

### Rule 1.5: Integration Layer Separation

**DO:**
- ✅ Operational integration (<5s SLA): Label printing, scanners
- ✅ Financial integration (minutes SLA): Agnum export
- ✅ Process integration (<30s SLA): MES/ERP coordination
- ✅ Use different retry strategies for each layer

**DON'T:**
- ❌ NEVER treat all integrations the same
- ❌ NEVER block operational workflows for financial integration failures
- ❌ NEVER use same timeout for all integrations

**Why:** Different integrations have different SLAs and failure modes. Operational failures need immediate alerts, financial failures can retry in background.

**Blueprint Reference:** Section 9 (Integration Adapter Contracts)

---

## 2. Aggregate Implementation Rules

### Rule 2.1: Event-Sourced vs State-Based

**Event-Sourced Aggregates:**
- StockLedger (append-only, no in-memory state)
- Reservation (state rebuilt from events)
- Valuation (state rebuilt from events)

**State-Based Aggregates:**
- HandlingUnit (projection-driven from StockMoved events)
- WarehouseLayout (configuration data)
- LogicalWarehouse (metadata)

**DO:**
- ✅ Use event sourcing for aggregates with complex state transitions
- ✅ Use state-based for aggregates with simple CRUD operations
- ✅ Use Marten for event-sourced aggregates
- ✅ Use EF Core for state-based aggregates

**DON'T:**
- ❌ NEVER mix event sourcing and state-based in same aggregate
- ❌ NEVER store event-sourced aggregate state in relational table
- ❌ NEVER query event stream for every read (use projections)

**Blueprint Reference:** Section 2 (Aggregate Persistence and Concurrency)

---

### Rule 2.2: Aggregate Boundaries

**DO:**
- ✅ Keep aggregates small and focused
- ✅ One aggregate = one transaction boundary
- ✅ Use sagas to coordinate multiple aggregates
- ✅ Enforce invariants within aggregate only

**DON'T:**
- ❌ NEVER modify multiple aggregates in same transaction
- ❌ NEVER enforce cross-aggregate invariants directly
- ❌ NEVER load entire aggregate graph (use projections for queries)

**Example (CORRECT):**
```csharp
// PickStockSaga coordinates 3 aggregates
public class PickStockSaga
{
    // Transaction 1: StockLedger
    await _stockLedger.RecordMovement(...);
    
    // Transaction 2: HandlingUnit (via projection)
    await WaitForProjection(...);
    
    // Transaction 3: Reservation
    await _reservation.Consume(...);
}
```

**Example (WRONG):**
```csharp
// DON'T DO THIS - multiple aggregates in one transaction
using (var transaction = _dbContext.BeginTransaction())
{
    _stockLedger.RecordMovement(...);
    _handlingUnit.RemoveLine(...);
    _reservation.Consume(...);
    transaction.Commit(); // WRONG!
}
```

---

### Rule 2.3: Optimistic Concurrency

**DO:**
- ✅ Use version numbers for optimistic locking
- ✅ EF Core: Use `[ConcurrencyCheck]` or `[Timestamp]` attribute
- ✅ Marten: Use stream version for event-sourced aggregates
- ✅ Retry on concurrency exception (with exponential backoff)

**DON'T:**
- ❌ NEVER use pessimistic locking (database locks)
- ❌ NEVER ignore concurrency exceptions
- ❌ NEVER retry indefinitely (max 3 retries)

**Code Example:**
```csharp
public class HandlingUnit
{
    public int Version { get; private set; } // Optimistic lock
}

// EF Core configuration
builder.Property(hu => hu.Version).IsConcurrencyToken();

// Retry logic
for (int i = 0; i < 3; i++)
{
    try
    {
        await _dbContext.SaveChangesAsync();
        break;
    }
    catch (DbUpdateConcurrencyException)
    {
        if (i == 2) throw; // Max retries
        await Task.Delay(100 * (i + 1)); // Exponential backoff
        _dbContext.Entry(hu).Reload();
    }
}
```

---

## 3. Event Naming and Schema Conventions

### Rule 3.1: Event Naming

**DO:**
- ✅ Use past tense: StockMoved, ReservationCreated, HandlingUnitSealed
- ✅ Use domain language (not technical terms)
- ✅ Be specific: StockAllocated (not StockChanged)

**DON'T:**
- ❌ NEVER use present tense: StockMove, ReservationCreate
- ❌ NEVER use generic names: DataChanged, EntityUpdated
- ❌ NEVER use technical jargon: RowInserted, RecordModified

---

### Rule 3.2: Event Schema

**DO:**
- ✅ Include all data needed to process event (no lookups required)
- ✅ Include metadata: eventId, timestamp, correlationId, causationId
- ✅ Use value objects (not primitives)
- ✅ Version events (V1, V2) when schema changes

**DON'T:**
- ❌ NEVER include only IDs (include full data)
- ❌ NEVER mutate event schema (create new version)
- ❌ NEVER delete old event versions (support upcasting)

**Code Example:**
```csharp
public class StockMovedEvent
{
    public Guid MovementId { get; set; }
    public DateTime Timestamp { get; set; }
    public string SKU { get; set; }
    public decimal Quantity { get; set; }
    public string FromLocation { get; set; }
    public string ToLocation { get; set; }
    public MovementType MovementType { get; set; }
    public Guid OperatorId { get; set; }
    public Guid? HandlingUnitId { get; set; }
    public string Reason { get; set; }
    public int Version { get; set; } = 2; // Event version
}
```

---

### Rule 3.3: Event Versioning and Upcasting

**DO:**
- ✅ Create new event version when schema changes (StockMovedEvent_V2)
- ✅ Implement upcaster to convert V1 → V2
- ✅ Register upcaster in Marten configuration
- ✅ Keep old event versions in codebase

**DON'T:**
- ❌ NEVER modify existing event schema
- ❌ NEVER delete old event versions
- ❌ NEVER break backward compatibility

**Blueprint Reference:** Section 7 (Event Schema Versioning)

---

## 4. Command Handler Patterns

### Rule 4.1: Command Structure

**DO:**
- ✅ Include CommandId (GUID) for idempotency
- ✅ Include CorrelationId and CausationId for tracing
- ✅ Use value objects (not primitives)
- ✅ Validate in command handler (not in aggregate)

**DON'T:**
- ❌ NEVER reuse CommandId
- ❌ NEVER skip validation
- ❌ NEVER put business logic in command (belongs in aggregate)

**Code Example:**
```csharp
public class RecordStockMovementCommand : ICommand
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
    
    public string SKU { get; set; }
    public decimal Quantity { get; set; }
    public string FromLocation { get; set; }
    public string ToLocation { get; set; }
    public MovementType MovementType { get; set; }
    public Guid OperatorId { get; set; }
}
```

---

### Rule 4.2: Command Pipeline

**DO:**
- ✅ Use MediatR pipeline behaviors: Validation → Idempotency → Transaction → Logging
- ✅ Check idempotency BEFORE executing command
- ✅ Return cached result if command already processed
- ✅ Store processed commands for 7 days

**DON'T:**
- ❌ NEVER skip idempotency check
- ❌ NEVER execute command twice
- ❌ NEVER store processed commands indefinitely (disk space)

**Blueprint Reference:** Section 3 (Command Pipeline Architecture)

---

### Rule 4.3: Command Validation

**DO:**
- ✅ Validate command structure (required fields, formats)
- ✅ Validate business rules in aggregate (not in handler)
- ✅ Return Result<T> (not exceptions for business rule violations)
- ✅ Use FluentValidation for structural validation

**DON'T:**
- ❌ NEVER throw exceptions for business rule violations
- ❌ NEVER validate in multiple places (DRY)
- ❌ NEVER skip validation for "trusted" sources

**Code Example:**
```csharp
public class RecordStockMovementValidator : AbstractValidator<RecordStockMovementCommand>
{
    public RecordStockMovementValidator()
    {
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.FromLocation).NotEmpty().NotEqual(x => x.ToLocation);
        RuleFor(x => x.ToLocation).NotEmpty();
    }
}
```

---

## 5. Saga Orchestration Constraints

### Rule 5.1: Saga State Persistence

**DO:**
- ✅ Store saga state after each step
- ✅ Use saga_state table with saga_id, current_step, step_results
- ✅ Resume saga from last completed step on crash
- ✅ Implement idempotency for each step

**DON'T:**
- ❌ NEVER keep saga state in memory only
- ❌ NEVER re-execute completed steps
- ❌ NEVER assume saga completes in one execution

**Blueprint Reference:** Section 5 (Saga Runtime and Orchestration)

---

### Rule 5.2: Saga Compensation

**DO:**
- ✅ Implement compensation for each step
- ✅ Compensate in reverse order (LIFO)
- ✅ Make compensation idempotent
- ✅ Log compensation actions

**DON'T:**
- ❌ NEVER skip compensation
- ❌ NEVER assume compensation always succeeds
- ❌ NEVER compensate in forward order

**Code Example:**
```csharp
public class ReceiveGoodsSaga
{
    public async Task Handle(ReceiveGoodsCommand command)
    {
        try
        {
            // Step 1: Record movement
            var movementId = await RecordMovement(...);
            SaveState(step: 1, movementId);
            
            // Step 2: Create HU
            var huId = await CreateHU(...);
            SaveState(step: 2, huId);
            
            // Step 3: Seal HU
            await SealHU(huId);
            SaveState(step: 3);
            
            MarkAsComplete();
        }
        catch (Exception ex)
        {
            // Compensate in reverse order
            if (CurrentStep >= 2) await DeleteHU(huId);
            // Movement remains (correct - source of truth)
            MarkAsFailed(ex.Message);
        }
    }
}
```

---

### Rule 5.3: Saga Timeout

**DO:**
- ✅ Set timeout for each saga (default: 5 minutes)
- ✅ Alert if saga exceeds timeout
- ✅ Provide manual intervention tools

**DON'T:**
- ❌ NEVER let sagas run indefinitely
- ❌ NEVER auto-cancel sagas (may need manual resolution)
- ❌ NEVER ignore stuck sagas

---

## 6. Projection Safety Rules

### Rule 6.1: Idempotency

**DO:**
- ✅ Use UPSERT operations (INSERT ... ON CONFLICT UPDATE)
- ✅ Check if event already processed (event_processing_checkpoints table)
- ✅ Skip processing if event already handled
- ✅ Use event sequence number for ordering

**DON'T:**
- ❌ NEVER use INSERT (use UPSERT)
- ❌ NEVER assume events arrive in order
- ❌ NEVER process same event twice

**Code Example:**
```csharp
public async Task Handle(StockMovedEvent evt)
{
    // Check idempotency
    var checkpoint = await _session.Query<EventProcessingCheckpoint>()
        .FirstOrDefaultAsync(c => c.HandlerName == "LocationBalanceProjection" 
                               && c.EventId == evt.MovementId);
    
    if (checkpoint != null)
    {
        _logger.LogInformation("Event {EventId} already processed", evt.MovementId);
        return; // Skip
    }
    
    // Update projection (UPSERT)
    var balance = await _session.LoadAsync<LocationBalance>(evt.ToLocation, evt.SKU)
                  ?? new LocationBalance { Location = evt.ToLocation, SKU = evt.SKU };
    
    balance.Quantity += evt.Quantity;
    balance.LastUpdated = evt.Timestamp;
    
    _session.Store(balance);
    
    // Record checkpoint
    _session.Store(new EventProcessingCheckpoint
    {
        HandlerName = "LocationBalanceProjection",
        EventId = evt.MovementId,
        ProcessedAt = DateTime.UtcNow
    });
    
    await _session.SaveChangesAsync();
}
```

---

### Rule 6.2: Projection Lag Monitoring

**DO:**
- ✅ Monitor projection lag (latest event timestamp - projection timestamp)
- ✅ Alert if lag > 30 seconds
- ✅ Display stale data indicator in UI if lag > 5 seconds
- ✅ Provide projection rebuild tool

**DON'T:**
- ❌ NEVER ignore projection lag
- ❌ NEVER assume projections are always current
- ❌ NEVER perform full event stream replay synchronously

**Blueprint Reference:** Section 6 (Projection Runtime Architecture)

---

### Rule 6.3: Projection Rebuild

**DO:**
- ✅ Support manual projection rebuild
- ✅ Replay all events from event stream
- ✅ Run rebuild in background (async)
- ✅ Validate rebuilt projection matches original

**DON'T:**
- ❌ NEVER rebuild projection synchronously (blocks system)
- ❌ NEVER delete projection before rebuild completes
- ❌ NEVER skip validation after rebuild

---

## 7. Offline Sync Rules

### Rule 7.1: Command Whitelist Enforcement

**DO:**
- ✅ Enforce whitelist in edge agent code
- ✅ Block forbidden commands with clear error message
- ✅ Cache required data before going offline (reservations, HUs)

**DON'T:**
- ❌ NEVER allow non-whitelisted commands offline
- ❌ NEVER assume server will validate (edge agent must enforce)
- ❌ NEVER cache all data (only what's needed)

**Code Example:**
```csharp
public async Task<Result> QueueCommand(ICommand command)
{
    // Whitelist check
    var allowedTypes = new[] { nameof(PickStockCommand), nameof(TransferStockCommand) };
    
    if (!allowedTypes.Contains(command.GetType().Name))
    {
        return Result.Fail($"{command.GetType().Name} is not allowed offline");
    }
    
    // Additional validation
    if (command is PickStockCommand pickCmd)
    {
        var reservation = await _cache.GetReservation(pickCmd.ReservationId);
        if (reservation?.Status != ReservationStatus.PICKING)
        {
            return Result.Fail("Reservation must be in PICKING state");
        }
    }
    
    // Queue command
    await _queue.Enqueue(command);
    return Result.Ok();
}
```

---

### Rule 7.2: Server-Side Re-Validation

**DO:**
- ✅ Re-validate ALL offline commands on server
- ✅ Check reservation still PICKING
- ✅ Check HU still allocated
- ✅ Check balance still sufficient
- ✅ Reject if validation fails

**DON'T:**
- ❌ NEVER trust offline commands without validation
- ❌ NEVER skip validation for "trusted" devices
- ❌ NEVER execute invalid commands

**Blueprint Reference:** Section 8 (Offline Sync Protocol)

---

### Rule 7.3: Conflict Resolution

**DO:**
- ✅ Detect conflicts (reservation bumped, HU moved)
- ✅ Reject conflicting commands
- ✅ Generate reconciliation report for operator
- ✅ Provide suggested actions

**DON'T:**
- ❌ NEVER auto-resolve conflicts (operator must decide)
- ❌ NEVER hide conflicts from operator
- ❌ NEVER retry conflicting commands automatically

---

## 8. Integration Adapter Rules

### Rule 8.1: Operational Integration (<5s SLA)

**DO:**
- ✅ Retry 3 times immediately
- ✅ Alert operator on failure
- ✅ Provide manual fallback (e.g., manual label print)
- ✅ Complete within 5 seconds

**DON'T:**
- ❌ NEVER block workflow for operational integration failure
- ❌ NEVER retry indefinitely
- ❌ NEVER use exponential backoff (too slow)

**Example:** Label printing

---

### Rule 8.2: Financial Integration (minutes SLA)

**DO:**
- ✅ Retry with exponential backoff (1m, 5m, 15m)
- ✅ Alert administrator on failure
- ✅ Provide manual fallback (CSV export)
- ✅ Complete within minutes

**DON'T:**
- ❌ NEVER block operational workflows
- ❌ NEVER alert operator (alert administrator)
- ❌ NEVER fail silently

**Example:** Agnum export

---

### Rule 8.3: Process Integration (<30s SLA)

**DO:**
- ✅ Use saga compensation on failure
- ✅ Notify both systems (warehouse + ERP)
- ✅ Maintain eventual consistency
- ✅ Complete within 30 seconds

**DON'T:**
- ❌ NEVER assume integration always succeeds
- ❌ NEVER leave systems in inconsistent state
- ❌ NEVER skip notification

**Example:** MES/ERP material requests

**Blueprint Reference:** Section 9 (Integration Adapter Contracts)

---

## 9. Testing Expectations

### Rule 9.1: Unit Tests

**DO:**
- ✅ Test each aggregate command with valid inputs
- ✅ Test each aggregate command with invalid inputs (expect errors)
- ✅ Test aggregate invariants are enforced
- ✅ Test saga happy path and compensation
- ✅ Achieve 90%+ code coverage

**DON'T:**
- ❌ NEVER skip unit tests
- ❌ NEVER test only happy path
- ❌ NEVER mock aggregates (test real logic)

---

### Rule 9.2: Property-Based Tests

**DO:**
- ✅ Implement ALL 49 correctness properties from design document
- ✅ Run minimum 100 iterations per property test
- ✅ Tag tests with property number and requirement
- ✅ Use FsCheck (.NET) or fast-check (TypeScript)

**DON'T:**
- ❌ NEVER skip property tests
- ❌ NEVER run < 100 iterations
- ❌ NEVER ignore failing property tests

**Code Example:**
```csharp
[Property]
[Tag("Feature: warehouse-core-phase1, Property 2: Balance Non-Negativity")]
public Property BalanceNeverNegative()
{
    return Prop.ForAll(
        Arb.Generate<List<StockMovement>>(),
        movements =>
        {
            var ledger = new StockLedger();
            foreach (var movement in movements)
            {
                try { ledger.RecordMovement(movement); }
                catch (InsufficientBalanceError) { /* Expected */ }
            }
            
            var balances = ledger.ComputeAllBalances();
            return balances.All(b => b.Quantity >= 0);
        }
    );
}
```

---

### Rule 9.3: Integration Tests

**DO:**
- ✅ Test end-to-end workflows (receive goods, transfer, pick)
- ✅ Test saga compensation scenarios
- ✅ Test offline sync end-to-end
- ✅ Use Testcontainers for database

**DON'T:**
- ❌ NEVER skip integration tests
- ❌ NEVER use in-memory database (use real PostgreSQL)
- ❌ NEVER share database between tests (isolation)

---

## 10. Code Quality Standards

### Rule 10.1: Naming Conventions

**DO:**
- ✅ Use domain language (not technical terms)
- ✅ Be explicit: RecordStockMovement (not AddStock)
- ✅ Use consistent naming: Command, Event, Aggregate, Saga

**DON'T:**
- ❌ NEVER use abbreviations: RecordStockMvmt
- ❌ NEVER use generic names: Process, Handle, Execute
- ❌ NEVER use technical jargon: Persist, Hydrate, Materialize

---

### Rule 10.2: Error Handling

**DO:**
- ✅ Return Result<T> for business rule violations
- ✅ Throw exceptions for infrastructure failures
- ✅ Log all errors with context (aggregate ID, command, user)
- ✅ Provide actionable error messages

**DON'T:**
- ❌ NEVER throw exceptions for business rule violations
- ❌ NEVER swallow exceptions
- ❌ NEVER return generic error messages

---

### Rule 10.3: Logging

**DO:**
- ✅ Use structured logging (Serilog)
- ✅ Include correlation ID and trace ID
- ✅ Log at appropriate levels: INFO (operations), WARN (recoverable), ERROR (failures)
- ✅ Log business events (movement recorded, reservation created)

**DON'T:**
- ❌ NEVER log sensitive data (passwords, tokens)
- ❌ NEVER log at DEBUG level in production
- ❌ NEVER log without context

**Blueprint Reference:** Section 10 (Observability Implementation)

---

## Summary

This document provides concrete coding standards and patterns for implementing the warehouse system. Key takeaways:

1. **NEVER violate architectural constraints** (Movement Ownership, Pick Transaction Ordering, Offline Whitelist, Reservation Bumping, Integration Separation)
2. **Use event sourcing for complex state**, state-based for simple CRUD
3. **Enforce idempotency** at all levels (commands, events, projections, sagas)
4. **Test comprehensively** (unit, property-based, integration)
5. **Monitor and alert** (projection lag, saga timeout, integration failures)

**Always consult the implementation blueprint for detailed technical specifications.**

**Next Document:** 50-risk-and-delivery-strategy.md (Risk Management and Rollout Strategy)
