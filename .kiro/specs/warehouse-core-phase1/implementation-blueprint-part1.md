# Implementation Blueprint: Warehouse Core Phase 1

**Project:** LKvitai.MES Warehouse Management System  
**Phase:** Phase 1 - Core Implementation  
**Version:** 1.0  
**Date:** February 2026  
**Status:** Implementation Ready

---

## Document Purpose

This Implementation Blueprint provides detailed technical guidance for implementing the Warehouse Core Phase 1 system. It bridges the gap between high-level design and actual code implementation by defining:

- Marten event store configuration strategy
- Aggregate persistence and concurrency model
- Command pipeline and transaction boundaries
- Outbox wiring and delivery guarantees
- Saga runtime persistence and retry orchestration
- Projection runtime architecture and checkpointing
- Event schema versioning and upcasting strategy
- Offline sync protocol and conflict resolution algorithm
- Integration adapter contract boundaries
- Observability propagation model

**Target Audience:** Implementation team (developers, architects, DevOps)

**Prerequisites:**
- Read: `requirements.md`, `design.md`, `tasks.md`
- Read: `docs/02-warehouse-domain-model-v1.md`, `docs/04-system-architecture.md`
- Understand: Event sourcing, CQRS, saga pattern, transactional outbox

---

## Table of Contents

1. [Marten Event Store Configuration](#1-marten-event-store-configuration)
2. [Aggregate Persistence and Concurrency](#2-aggregate-persistence-and-concurrency)
3. [Command Pipeline Architecture](#3-command-pipeline-architecture)
4. [Transactional Outbox Implementation](#4-transactional-outbox-implementation)
5. [Saga Runtime and Orchestration](#5-saga-runtime-and-orchestration)
6. [Projection Runtime Architecture](#6-projection-runtime-architecture)
7. [Event Schema Versioning](#7-event-schema-versioning)
8. [Offline Sync Protocol](#8-offline-sync-protocol)
9. [Integration Adapter Contracts](#9-integration-adapter-contracts)
10. [Observability Implementation](#10-observability-implementation)

---

## 1. Marten Event Store Configuration

### 1.1 Marten Setup and Database Schema

**Technology:** Marten 7.x with PostgreSQL 15+

**Connection Configuration:**

```csharp
// Program.cs or Startup.cs
services.AddMarten(options =>
{
    options.Connection(Configuration.GetConnectionString("WarehouseDb"));
    
    // Event store configuration
    options.Events.DatabaseSchemaName = "warehouse_events";
    options.Events.StreamIdentity = StreamIdentity.AsGuid;
    
    // Enable async daemon for projections
    options.Events.UseAsyncDaemon = true;
    
    // Configure event archival (events older than 1 year)
    options.Events.ArchiveOldEvents = true;
    options.Events.ArchiveAfterDays = 365;
    
    // Performance tuning
    options.Events.MetadataConfig.HeadersEnabled = true;
    options.Events.MetadataConfig.CausationIdEnabled = true;
    options.Events.MetadataConfig.CorrelationIdEnabled = true;
});
```

**Database Schema Structure:**

Marten creates these tables automatically:
- `warehouse_events.mt_events` - Event stream storage
- `warehouse_events.mt_streams` - Stream metadata
- `warehouse_events.mt_event_progression` - Projection checkpoints
- `warehouse_events.mt_doc_*` - Document storage for state-based aggregates

---

### 1.2 Event Stream Partitioning Strategy

**StockLedger Stream:**
- **Stream Type:** Singleton stream (one stream for entire warehouse)
- **Stream ID:** `stock-ledger-{warehouseId}` (e.g., `stock-ledger-main`)
- **Rationale:** All movements in single stream for global ordering

```csharp
// StockLedger event appending
public class StockLedgerRepository
{
    private readonly IDocumentSession _session;
    
    public async Task AppendMovement(StockMovedEvent evt)
    {
        var streamId = $"stock-ledger-{evt.WarehouseId}";
        _session.Events.Append(streamId, evt);
        await _session.SaveChangesAsync();
    }
}
```

**Reservation Streams:**
- **Stream Type:** Per-aggregate stream
- **Stream ID:** `reservation-{reservationId}` (e.g., `reservation-042`)
- **Rationale:** Each reservation has independent lifecycle

```csharp
// Reservation event appending
public class ReservationRepository
{
    private readonly IDocumentSession _session;
    
    public async Task AppendEvent(Guid reservationId, object evt)
    {
        var streamId = $"reservation-{reservationId}";
        _session.Events.Append(streamId, evt);
        await _session.SaveChangesAsync();
    }
}
```

**Valuation Streams:**
- **Stream Type:** Per-SKU stream
- **Stream ID:** `valuation-{sku}` (e.g., `valuation-SKU933`)
- **Rationale:** Each SKU has independent cost history

---

### 1.3 Event Metadata Configuration

**Metadata Fields:**

```csharp
// Configure event metadata
public class EventMetadata
{
    public Guid EventId { get; set; }
    public string EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid CorrelationId { get; set; }  // Links events from same command
    public Guid CausationId { get; set; }    // Links cause-effect chain
    public string UserId { get; set; }
    public string TenantId { get; set; }
}

// Append event with metadata
_session.Events.Append(streamId, evt, metadata =>
{
    metadata.CorrelationId = _correlationContext.CorrelationId;
    metadata.CausationId = _correlationContext.CausationId;
    metadata.Headers["UserId"] = _userContext.UserId;
    metadata.Headers["TenantId"] = _userContext.TenantId;
});
```

---

## 2. Aggregate Persistence and Concurrency

### 2.1 Event-Sourced Aggregates

**StockLedger Aggregate (Append-Only):**

```csharp
public class StockLedger
{
    // No in-memory state - purely append-only
    private readonly IDocumentSession _session;
    private readonly string _streamId;
    
    public StockLedger(IDocumentSession session, string warehouseId)
    {
        _session = session;
        _streamId = $"stock-ledger-{warehouseId}";
    }
    
    public async Task<Result> RecordMovement(
        string sku, 
        decimal quantity, 
        string fromLocation, 
        string toLocation,
        MovementType type,
        Guid operatorId,
        Guid? handlingUnitId = null,
        string reason = null)
    {
        // Validate: from != to, quantity > 0
        if (fromLocation == toLocation)
            return Result.Fail("From and to locations cannot be the same");
        
        if (quantity <= 0)
            return Result.Fail("Quantity must be positive");
        
        // [MITIGATION V-2] Atomic balance validation and event append with retry
        var maxRetries = 3;
        var retryDelayMs = 100;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Get current stream version
                var stream = await _session.Events.FetchStreamStateAsync(_streamId);
                var expectedVersion = stream?.Version ?? 0;
                
                // Validate balance (only for physical locations)
                if (!IsVirtualLocation(fromLocation))
                {
                    var balance = await GetBalanceAt(fromLocation, sku);
                    if (balance < quantity)
                        return Result.Fail($"Insufficient balance at {fromLocation}");
                }
                
                // Append event with expected version (atomic)
                var evt = new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(),
                    SKU = sku,
                    Quantity = quantity,
                    FromLocation = fromLocation,
                    ToLocation = toLocation,
                    MovementType = type,
                    Timestamp = DateTime.UtcNow,
                    OperatorId = operatorId,
                    HandlingUnitId = handlingUnitId,
                    Reason = reason
                };
                
                _session.Events.Append(_streamId, expectedVersion, evt);
                await _session.SaveChangesAsync();
                
                return Result.Ok();
            }
            catch (EventStreamUnexpectedMaxEventIdException ex)
            {
                // Concurrency conflict - retry with exponential backoff
                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelayMs * (int)Math.Pow(2, attempt));
                    continue;
                }
                
                // Retries exhausted
                return Result.Fail($"Concurrency conflict after {maxRetries} retries. Please try again.");
            }
        }
        
        return Result.Fail("Unexpected error during movement recording");
    }
    
    private async Task<decimal> GetBalanceAt(string location, string sku)
    {
        // Query projection (fast path)
        var projection = await _session.Query<LocationBalanceProjection>()
            .FirstOrDefaultAsync(p => p.Location == location && p.SKU == sku);
        
        if (projection != null && projection.LastUpdated > DateTime.UtcNow.AddSeconds(-5))
            return projection.Quantity;
        
        // Fallback: compute from event stream (slow path)
        var events = await _session.Events.FetchStreamAsync(_streamId);
        var balance = events
            .OfType<StockMovedEvent>()
            .Where(e => e.SKU == sku)
            .Sum(e => e.ToLocation == location ? e.Quantity : 
                     e.FromLocation == location ? -e.Quantity : 0);
        
        return balance;
    }
    
    private bool IsVirtualLocation(string location)
    {
        return location is "SUPPLIER" or "PRODUCTION" or "SCRAP" or "SYSTEM";
    }
}
```

**Reservation Aggregate (Event-Sourced with State):**

```csharp
public class Reservation
{
    // State rebuilt from events
    public Guid ReservationId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public ReservationLockType LockType { get; private set; }
    public int Priority { get; private set; }
    public List<ReservationLine> Lines { get; private set; } = new();
    
    // Event sourcing: Apply methods
    public void Apply(ReservationCreatedEvent evt)
    {
        ReservationId = evt.ReservationId;
        Status = ReservationStatus.PENDING;
        Priority = evt.Priority;
        Lines = evt.RequestedLines.Select(l => new ReservationLine
        {
            SKU = l.SKU,
            RequestedQuantity = l.Quantity,
            AllocatedHUs = new List<Guid>()
        }).ToList();
    }
    
    public void Apply(StockAllocatedEvent evt)
    {
        Status = ReservationStatus.ALLOCATED;
        LockType = ReservationLockType.SOFT;
        
        foreach (var allocation in evt.Allocations)
        {
            var line = Lines.First(l => l.SKU == allocation.SKU);
            line.AllocatedHUs.AddRange(allocation.HandlingUnitIds);
        }
    }
    
    public void Apply(PickingStartedEvent evt)
    {
        Status = ReservationStatus.PICKING;
        LockType = ReservationLockType.HARD;
    }
    
    public void Apply(ReservationConsumedEvent evt)
    {
        Status = ReservationStatus.CONSUMED;
    }
    
    // Command: Allocate
    public Result<StockAllocatedEvent> Allocate(List<AllocationRequest> allocations)
    {
        if (Status != ReservationStatus.PENDING)
            return Result.Fail("Reservation must be PENDING to allocate");
        
        // Validate allocations match requested lines
        foreach (var line in Lines)
        {
            var allocation = allocations.FirstOrDefault(a => a.SKU == line.SKU);
            if (allocation == null)
                return Result.Fail($"Missing allocation for SKU {line.SKU}");
            
            if (allocation.Quantity > line.RequestedQuantity)
                return Result.Fail($"Allocated quantity exceeds requested for SKU {line.SKU}");
        }
        
        return Result.Ok(new StockAllocatedEvent
        {
            ReservationId = ReservationId,
            Allocations = allocations,
            LockType = ReservationLockType.SOFT
        });
    }
    
    // Command: StartPicking (SOFT → HARD transition)
    public Result<PickingStartedEvent> StartPicking()
    {
        if (Status != ReservationStatus.ALLOCATED)
            return Result.Fail("Reservation must be ALLOCATED to start picking");
        
        if (LockType != ReservationLockType.SOFT)
            return Result.Fail("Reservation must have SOFT lock to start picking");
        
        // Re-validation happens in saga (balance check, conflict check)
        
        return Result.Ok(new PickingStartedEvent
        {
            ReservationId = ReservationId,
            LockType = ReservationLockType.HARD
        });
    }
}
```

---

### 2.2 State-Based Aggregates

**HandlingUnit Aggregate (State-Based with EF Core):**

```csharp
public class HandlingUnit
{
    public Guid HUId { get; private set; }
    public string LPN { get; private set; }
    public HandlingUnitType Type { get; private set; }
    public HandlingUnitStatus Status { get; private set; }
    public string Location { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SealedAt { get; private set; }
    public int Version { get; private set; }  // Optimistic locking
    
    public List<HandlingUnitLine> Lines { get; private set; } = new();
    
    // Factory method
    public static HandlingUnit Create(HandlingUnitType type, string location, Guid operatorId)
    {
        return new HandlingUnit
        {
            HUId = Guid.NewGuid(),
            LPN = GenerateLPN(),
            Type = type,
            Status = HandlingUnitStatus.OPEN,
            Location = location,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };
    }
    
    // Command: AddLine
    public Result AddLine(string sku, decimal quantity)
    {
        if (Status == HandlingUnitStatus.SEALED)
            return Result.Fail("Cannot modify sealed handling unit");
        
        var existingLine = Lines.FirstOrDefault(l => l.SKU == sku);
        if (existingLine != null)
        {
            existingLine.Quantity += quantity;
        }
        else
        {
            Lines.Add(new HandlingUnitLine
            {
                HUId = HUId,
                SKU = sku,
                Quantity = quantity
            });
        }
        
        return Result.Ok();
    }
    
    // Command: Seal
    public Result Seal()
    {
        if (Status == HandlingUnitStatus.SEALED)
            return Result.Fail("Handling unit already sealed");
        
        if (!Lines.Any())
            return Result.Fail("Cannot seal empty handling unit");
        
        Status = HandlingUnitStatus.SEALED;
        SealedAt = DateTime.UtcNow;
        
        return Result.Ok();
    }
    
    private static string GenerateLPN()
    {
        return $"HU-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }
}
```

**EF Core Configuration:**

```csharp
public class HandlingUnitConfiguration : IEntityTypeConfiguration<HandlingUnit>
{
    public void Configure(EntityTypeBuilder<HandlingUnit> builder)
    {
        builder.ToTable("handling_units");
        builder.HasKey(hu => hu.HUId);
        
        builder.Property(hu => hu.LPN).IsRequired().HasMaxLength(100);
        builder.HasIndex(hu => hu.LPN).IsUnique();
        
        builder.Property(hu => hu.Location).IsRequired().HasMaxLength(200);
        builder.HasIndex(hu => hu.Location);
        
        builder.Property(hu => hu.Status).IsRequired().HasConversion<string>();
        builder.Property(hu => hu.Type).IsRequired().HasConversion<string>();
        
        // Optimistic concurrency
        builder.Property(hu => hu.Version).IsConcurrencyToken();
        
        // Owned collection
        builder.OwnsMany(hu => hu.Lines, lines =>
        {
            lines.ToTable("handling_unit_lines");
            lines.WithOwner().HasForeignKey(l => l.HUId);
            lines.Property(l => l.SKU).IsRequired().HasMaxLength(100);
            lines.Property(l => l.Quantity).HasPrecision(18, 4);
        });
    }
}
```

---

### 2.3 Optimistic Concurrency Control

**Version-Based Concurrency:**

```csharp
// EF Core automatically handles version checking
public async Task<Result> UpdateHandlingUnit(HandlingUnit hu)
{
    try
    {
        _dbContext.HandlingUnits.Update(hu);
        await _dbContext.SaveChangesAsync();
        return Result.Ok();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        return Result.Fail("Handling unit was modified by another user. Please refresh and try again.");
    }
}
```

**Marten Stream Version Concurrency:**

```csharp
// Marten event stream concurrency
public async Task<Result> AppendReservationEvent(Guid reservationId, object evt, int expectedVersion)
{
    try
    {
        var streamId = $"reservation-{reservationId}";
        _session.Events.Append(streamId, expectedVersion, evt);
        await _session.SaveChangesAsync();
        return Result.Ok();
    }
    catch (EventStreamUnexpectedMaxEventIdException ex)
    {
        return Result.Fail("Reservation was modified concurrently. Please retry.");
    }
}
```

---

## 3. Command Pipeline Architecture

### 3.1 Command Handler Infrastructure

**Command Interface:**

```csharp
public interface ICommand
{
    Guid CommandId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}

public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}
```

**Command Pipeline with MediatR:**

```csharp
// Program.cs
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // Add pipeline behaviors
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
});
```

---

### 3.2 Idempotency Behavior

**Implementation:**

```csharp
public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
{
    private readonly IDocumentSession _session;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Check if command already processed
        var processedCommand = await _session.Query<ProcessedCommand>()
            .FirstOrDefaultAsync(pc => pc.CommandId == request.CommandId, cancellationToken);
        
        if (processedCommand != null)
        {
            _logger.LogInformation(
                "Command {CommandId} already processed. Returning cached result.",
                request.CommandId);
            
            return JsonSerializer.Deserialize<TResponse>(processedCommand.Result);
        }
        
        // Execute command
        var response = await next();
        
        // Store result
        var commandRecord = new ProcessedCommand
        {
            CommandId = request.CommandId,
            CommandType = typeof(TRequest).Name,
            Timestamp = DateTime.UtcNow,
            Result = JsonSerializer.Serialize(response)
        };
        
        _session.Store(commandRecord);
        await _session.SaveChangesAsync(cancellationToken);
        
        return response;
    }
}

public class ProcessedCommand
{
    public Guid CommandId { get; set; }
    public string CommandType { get; set; }
    public DateTime Timestamp { get; set; }
    public string Result { get; set; }
}
```

---

### 3.3 Transaction Boundaries

**Transaction Behavior:**

```csharp
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
{
    private readonly IDocumentSession _session;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        // Marten session is already transactional
        // This behavior ensures SaveChanges is called
        
        try
        {
            var response = await next();
            
            // Commit transaction
            await _session.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "Command {CommandId} committed successfully",
                request.CommandId);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Command {CommandId} failed. Transaction rolled back.",
                request.CommandId);
            
            throw;
        }
    }
}
```

**CRITICAL: Pick Transaction Ordering:**

```csharp
// PickStockSaga enforces strict ordering
public class PickStockSaga : Saga<PickStockSagaState>
{
    public async Task Handle(PickStockCommand command)
    {
        // Step 1: Record StockMovement via StockLedger (MUST BE FIRST)
        var movementResult = await _mediator.Send(new RecordStockMovementCommand
        {
            CommandId = Guid.NewGuid(),
            SKU = command.SKU,
            Quantity = command.Quantity,
            FromLocation = command.FromLocation,
            ToLocation = "PRODUCTION",
            MovementType = MovementType.PICK,
            OperatorId = command.OperatorId,
            HandlingUnitId = command.HandlingUnitId
        });
        
        if (!movementResult.IsSuccess)
        {
            MarkAsFailed("StockMovement recording failed");
            return;
        }
        
        // Step 2: Wait for HandlingUnit projection to process StockMoved event
        await WaitForProjection(command.HandlingUnitId, command.SKU, command.Quantity);
        
        // Step 3: Update Reservation consumption
        var consumeResult = await _mediator.Send(new ConsumeReservationCommand
        {
            CommandId = Guid.NewGuid(),
            ReservationId = command.ReservationId,
            Quantity = command.Quantity
        });
        
        if (!consumeResult.IsSuccess)
        {
            // Retry consumption (StockMovement already recorded)
            await RetryConsumption(command.ReservationId, command.Quantity);
        }
        
        MarkAsComplete();
    }
    
    private async Task WaitForProjection(Guid huId, string sku, decimal quantity)
    {
        // Poll projection until updated
        var maxAttempts = 10;
        var delayMs = 500;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            var hu = await _session.LoadAsync<HandlingUnit>(huId);
            var line = hu.Lines.FirstOrDefault(l => l.SKU == sku);
            
            if (line == null || line.Quantity < quantity)
            {
                // Projection not yet updated
                await Task.Delay(delayMs);
                continue;
            }
            
            // Projection updated
            return;
        }
        
        throw new Exception("Projection lag exceeded timeout");
    }
}
```

---

## 4. Transactional Outbox Implementation

### 4.1 Outbox Pattern with Marten

**Outbox Message Schema:**

```csharp
public class OutboxMessage
{
    public Guid MessageId { get; set; }
    public string AggregateType { get; set; }
    public Guid AggregateId { get; set; }
    public string EventType { get; set; }
    public string EventData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int RetryCount { get; set; }
    public string LastError { get; set; }
}
```

**Marten Configuration:**

```csharp
services.AddMarten(options =>
{
    // ... other configuration
    
    // Configure outbox
    options.Schema.For<OutboxMessage>()
        .Index(m => m.PublishedAt)
        .Index(m => m.CreatedAt);
});
```

---

### 4.2 Outbox Writer (Transactional)

**Write to Outbox Atomically with Aggregate:**

```csharp
public class StockLedgerCommandHandler : ICommandHandler<RecordStockMovementCommand, Result>
{
    private readonly IDocumentSession _session;
    private readonly IEventPublisher _eventPublisher;
    
    public async Task<Result> Handle(RecordStockMovementCommand command, CancellationToken ct)
    {
        // 1. Append event to stream
        var evt = new StockMovedEvent { /* ... */ };
        _session.Events.Append($"stock-ledger-{command.WarehouseId}", evt);
        
        // 2. Write to outbox (same transaction)
        var outboxMessage = new OutboxMessage
        {
            MessageId = Guid.NewGuid(),
            AggregateType = "StockLedger",
            AggregateId = Guid.Parse(command.WarehouseId),
            EventType = nameof(StockMovedEvent),
            EventData = JsonSerializer.Serialize(evt),
            CreatedAt = DateTime.UtcNow,
            PublishedAt = null,
            RetryCount = 0
        };
        
        _session.Store(outboxMessage);
        
        // 3. Commit transaction (atomic)
        await _session.SaveChangesAsync(ct);
        
        return Result.Ok();
    }
}
```

---

### 4.3 Outbox Processor (Background Service)

**Implementation:**

```csharp
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
    
    private async Task ProcessOutboxMessages(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        
        // Fetch unpublished messages
        var messages = await session.Query<OutboxMessage>()
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        
        foreach (var message in messages)
        {
            try
            {
                // Deserialize event
                var eventType = Type.GetType(message.EventType);
                var evt = JsonSerializer.Deserialize(message.EventData, eventType);
                
                // Publish to event bus
                await eventBus.Publish(evt, ct);
                
                // Mark as published
                message.PublishedAt = DateTime.UtcNow;
                session.Update(message);
                
                _logger.LogInformation(
                    "Published outbox message {MessageId} ({EventType})",
                    message.MessageId, message.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to publish outbox message {MessageId}. Retry count: {RetryCount}",
                    message.MessageId, message.RetryCount);
                
                message.RetryCount++;
                message.LastError = ex.Message;
                session.Update(message);
                
                // Exponential backoff
                if (message.RetryCount > 5)
                {
                    _logger.LogCritical(
                        "Outbox message {MessageId} exceeded max retries. Manual intervention required.",
                        message.MessageId);
                }
            }
        }
        
        await session.SaveChangesAsync(ct);
    }
}
```

---

### 4.4 Event Bus Integration (MassTransit)

**Configuration:**

```csharp
services.AddMassTransit(x =>
{
    // Configure consumers
    x.AddConsumer<StockMovedEventConsumer>();
    x.AddConsumer<ReservationCreatedEventConsumer>();
    
    // Configure RabbitMQ
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        
        // Configure endpoints
        cfg.ReceiveEndpoint("warehouse-stock-moved", e =>
        {
            e.ConfigureConsumer<StockMovedEventConsumer>(context);
            e.PrefetchCount = 16;
            e.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(5)));
        });
        
        cfg.ConfigureEndpoints(context);
    });
});
```

**Event Consumer Example:**

```csharp
public class StockMovedEventConsumer : IConsumer<StockMovedEvent>
{
    private readonly IDocumentSession _session;
    private readonly ILogger<StockMovedEventConsumer> _logger;
    
    public async Task Consume(ConsumeContext<StockMovedEvent> context)
    {
        var evt = context.Message;
        
        _logger.LogInformation(
            "Processing StockMoved event {MovementId} for HU {HandlingUnitId}",
            evt.MovementId, evt.HandlingUnitId);
        
        // Check idempotency
        var checkpoint = await _session.Query<EventProcessingCheckpoint>()
            .FirstOrDefaultAsync(c => c.HandlerName == nameof(StockMovedEventConsumer) 
                                   && c.EventId == evt.MovementId);
        
        if (checkpoint != null)
        {
            _logger.LogInformation("Event {MovementId} already processed. Skipping.", evt.MovementId);
            return;
        }
        
        // Update HandlingUnit projection
        if (evt.HandlingUnitId.HasValue)
        {
            var hu = await _session.LoadAsync<HandlingUnit>(evt.HandlingUnitId.Value);
            
            if (hu != null)
            {
                if (evt.FromLocation == hu.Location)
                {
                    hu.RemoveLine(evt.SKU, evt.Quantity);
                }
                
                if (evt.ToLocation != evt.FromLocation)
                {
                    hu.UpdateLocation(evt.ToLocation);
                }
                
                if (evt.ToLocation == hu.Location)
                {
                    hu.AddLine(evt.SKU, evt.Quantity);
                }
                
                _session.Update(hu);
            }
        }
        
        // Record checkpoint
        _session.Store(new EventProcessingCheckpoint
        {
            HandlerName = nameof(StockMovedEventConsumer),
            EventId = evt.MovementId,
            ProcessedAt = DateTime.UtcNow
        });
        
        await _session.SaveChangesAsync();
    }
}

public class EventProcessingCheckpoint
{
    public string HandlerName { get; set; }
    public Guid EventId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

---

## 5. Saga Runtime and Orchestration

### 5.1 Saga State Persistence

**Saga State Schema:**

```csharp
public class SagaState
{
    public Guid SagaId { get; set; }
    public string SagaType { get; set; }
    public int CurrentStep { get; set; }
    public Dictionary<int, string> StepResults { get; set; } = new();
    public SagaStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum SagaStatus
{
    RUNNING,
    COMPLETED,
    FAILED,
    COMPENSATING
}
```

**Marten Configuration:**

```csharp
services.AddMarten(options =>
{
    options.Schema.For<SagaState>()
        .Index(s => s.Status)
        .Index(s => s.SagaType);
});
```

---

### 5.2 Saga Base Class

**Implementation:**

```csharp
public abstract class Saga<TState> where TState : SagaState, new()
{
    protected TState State { get; private set; }
    protected IDocumentSession Session { get; private set; }
    protected IMediator Mediator { get; private set; }
    protected ILogger Logger { get; private set; }
    
    public async Task Initialize(Guid sagaId, IDocumentSession session, IMediator mediator, ILogger logger)
    {
        Session = session;
        Mediator = mediator;
        Logger = logger;
        
        // Load or create state
        State = await session.LoadAsync<TState>(sagaId);
        if (State == null)
        {
            State = new TState
            {
                SagaId = sagaId,
                SagaType = GetType().Name,
                CurrentStep = 0,
                Status = SagaStatus.RUNNING,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            session.Store(State);
        }
    }
    
    protected async Task<TResult> ExecuteStep<TResult>(int stepNumber, Func<Task<TResult>> stepFunction)
    {
        // Check if step already executed
        if (State.StepResults.ContainsKey(stepNumber))
        {
            Logger.LogInformation("Step {StepNumber} already executed. Returning cached result.", stepNumber);
            return JsonSerializer.Deserialize<TResult>(State.StepResults[stepNumber]);
        }
        
        try
        {
            // Execute step
            var result = await stepFunction();
            
            // Save step result
            State.StepResults[stepNumber] = JsonSerializer.Serialize(result);
            State.CurrentStep = stepNumber;
            State.UpdatedAt = DateTime.UtcNow;
            
            Session.Update(State);
            await Session.SaveChangesAsync();
            
            Logger.LogInformation("Step {StepNumber} completed successfully.", stepNumber);
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Step {StepNumber} failed.", stepNumber);
            
            State.Status = SagaStatus.FAILED;
            State.StepResults[stepNumber] = JsonSerializer.Serialize(new { error = ex.Message });
            State.UpdatedAt = DateTime.UtcNow;
            
            Session.Update(State);
            await Session.SaveChangesAsync();
            
            throw;
        }
    }
    
    protected async Task MarkAsComplete()
    {
        State.Status = SagaStatus.COMPLETED;
        State.UpdatedAt = DateTime.UtcNow;
        
        Session.Update(State);
        await Session.SaveChangesAsync();
        
        Logger.LogInformation("Saga {SagaId} completed successfully.", State.SagaId);
    }
    
    protected async Task MarkAsFailed(string reason)
    {
        State.Status = SagaStatus.FAILED;
        State.UpdatedAt = DateTime.UtcNow;
        
        Session.Update(State);
        await Session.SaveChangesAsync();
        
        Logger.LogError("Saga {SagaId} failed: {Reason}", State.SagaId, reason);
    }
}
```

---

### 5.3 PickStockSaga Implementation

**Complete Implementation (MITIGATION V-3 - Simplified):**

```csharp
public class PickStockSaga : Saga<PickStockSagaState>
{
    public async Task Handle(PickStockCommand command)
    {
        // Step 1: Validate reservation is PICKING (HARD locked)
        var reservation = await ExecuteStep(1, async () =>
        {
            var res = await Session.LoadAsync<Reservation>(command.ReservationId);
            if (res.Status != ReservationStatus.PICKING)
                throw new InvalidOperationException("Reservation must be in PICKING state");
            
            return res;
        });
        
        // Step 2: Validate HU is allocated to reservation
        await ExecuteStep(2, async () =>
        {
            var line = reservation.Lines.FirstOrDefault(l => l.SKU == command.SKU);
            if (line == null || !line.AllocatedHUs.Contains(command.HandlingUnitId))
                throw new InvalidOperationException("HU not allocated to reservation");
            
            return true;
        });
        
        // Step 3: Record StockMovement via StockLedger (CRITICAL - MUST BE FIRST)
        var movementResult = await ExecuteStep(3, async () =>
        {
            var result = await Mediator.Send(new RecordStockMovementCommand
            {
                CommandId = Guid.NewGuid(),
                WarehouseId = "main",
                SKU = command.SKU,
                Quantity = command.Quantity,
                FromLocation = command.FromLocation,
                ToLocation = "PRODUCTION",
                MovementType = MovementType.PICK,
                OperatorId = command.OperatorId,
                HandlingUnitId = command.HandlingUnitId,
                Reason = $"Pick for reservation {command.ReservationId}"
            });
            
            if (!result.IsSuccess)
                throw new InvalidOperationException($"StockMovement failed: {result.Error}");
            
            return result;
        });
        
        // Step 4: Update Reservation consumption
        // [MITIGATION V-3] Do NOT wait for HU projection - it updates asynchronously
        await ExecuteStep(4, async () =>
        {
            var result = await Mediator.Send(new ConsumeReservationCommand
            {
                CommandId = Guid.NewGuid(),
                ReservationId = command.ReservationId,
                SKU = command.SKU,
                Quantity = command.Quantity
            });
            
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Reservation consumption failed: {result.Error}");
            
            return result;
        });
        
        await MarkAsComplete();
        
        // Note: HandlingUnit projection processes StockMoved event asynchronously
        // This is eventual consistency - HU state updates independently
    }
}

public class PickStockSagaState : SagaState
{
    public Guid ReservationId { get; set; }
    public Guid HandlingUnitId { get; set; }
    public string SKU { get; set; }
    public decimal Quantity { get; set; }
}
```

---



### 5.4 StartPicking Command Handler (MITIGATION R-3)

**Purpose:** Atomically transition reservation from SOFT to HARD lock with balance re-validation and conflict detection.

**Implementation:**

```csharp
public class StartPickingCommandHandler : ICommandHandler<StartPickingCommand, Result>
{
    private readonly IDocumentSession _session;
    private readonly ILogger<StartPickingCommandHandler> _logger;
    
    public async Task<Result> Handle(StartPickingCommand command, CancellationToken ct)
    {
        var maxRetries = 3;
        var retryDelayMs = 100;
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Step 1: Load reservation state
                var streamId = $"reservation-{command.ReservationId}";
                var stream = await _session.Events.FetchStreamStateAsync(streamId);
                var expectedVersion = stream?.Version ?? 0;
                
                var reservation = await _session.Events.AggregateStreamAsync<Reservation>(streamId);
                
                if (reservation.Status != ReservationStatus.ALLOCATED)
                    return Result.Fail("Reservation must be ALLOCATED to start picking");
                
                if (reservation.LockType != ReservationLockType.SOFT)
                    return Result.Fail("Reservation must have SOFT lock to start picking");
                
                // Step 2: Re-validate balance from event stream (not projection)
                var balanceValid = await RevalidateBalanceFromEventStream(reservation);
                if (!balanceValid.IsSuccess)
                    return balanceValid;
                
                // Step 3: Check for HARD lock conflicts
                var conflictCheck = await CheckHardLockConflicts(reservation);
                if (!conflictCheck.IsSuccess)
                    return conflictCheck;
                
                // Step 4: Acquire HARD lock atomically with expected version
                var evt = new PickingStartedEvent
                {
                    ReservationId = command.ReservationId,
                    LockType = ReservationLockType.HARD,
                    Timestamp = DateTime.UtcNow
                };
                
                _session.Events.Append(streamId, expectedVersion, evt);
                
                // Step 5: Update ActiveHardLocks projection (inline, same transaction)
                foreach (var line in reservation.Lines)
                {
                    var hardLock = new ActiveHardLock
                    {
                        ReservationId = command.ReservationId,
                        Location = line.Location,
                        SKU = line.SKU,
                        HardLockedQty = line.RequestedQuantity,
                        StartedAt = DateTime.UtcNow
                    };
                    
                    _session.Store(hardLock);
                }
                
                await _session.SaveChangesAsync(ct);
                
                _logger.LogInformation(
                    "StartPicking succeeded for reservation {ReservationId}",
                    command.ReservationId);
                
                return Result.Ok();
            }
            catch (EventStreamUnexpectedMaxEventIdException ex)
            {
                // Concurrency conflict - retry with exponential backoff
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        "StartPicking concurrency conflict for reservation {ReservationId}. Retry {Attempt}/{MaxRetries}",
                        command.ReservationId, attempt + 1, maxRetries);
                    
                    await Task.Delay(retryDelayMs * (int)Math.Pow(2, attempt), ct);
                    continue;
                }
                
                // Retries exhausted
                _logger.LogError(
                    "StartPicking failed after {MaxRetries} retries for reservation {ReservationId}",
                    maxRetries, command.ReservationId);
                
                return Result.Fail($"Concurrency conflict after {maxRetries} retries. Please try again.");
            }
        }
        
        return Result.Fail("Unexpected error during StartPicking");
    }
    
    private async Task<Result> RevalidateBalanceFromEventStream(Reservation reservation)
    {
        // Query StockLedger event stream for current balance
        var stockLedgerStream = await _session.Events.FetchStreamAsync("stock-ledger-main");
        
        foreach (var line in reservation.Lines)
        {
            var balance = stockLedgerStream
                .OfType<StockMovedEvent>()
                .Where(e => e.SKU == line.SKU)
                .Sum(e => e.ToLocation == line.Location ? e.Quantity : 
                         e.FromLocation == line.Location ? -e.Quantity : 0);
            
            if (balance < line.RequestedQuantity)
            {
                return Result.Fail($"Insufficient balance for SKU {line.SKU} at {line.Location}. " +
                                 $"Required: {line.RequestedQuantity}, Available: {balance}");
            }
        }
        
        return Result.Ok();
    }
    
    private async Task<Result> CheckHardLockConflicts(Reservation reservation)
    {
        // Query ActiveHardLocks projection for conflicts
        foreach (var line in reservation.Lines)
        {
            var existingLocks = await _session.Query<ActiveHardLock>()
                .Where(l => l.Location == line.Location && l.SKU == line.SKU)
                .ToListAsync();
            
            var totalHardLocked = existingLocks.Sum(l => l.HardLockedQty);
            
            // Get physical balance
            var physicalBalance = await GetPhysicalBalance(line.Location, line.SKU);
            var availableForHardLock = physicalBalance - totalHardLocked;
            
            if (availableForHardLock < line.RequestedQuantity)
            {
                return Result.Fail($"HARD lock conflict for SKU {line.SKU} at {line.Location}. " +
                                 $"Required: {line.RequestedQuantity}, Available: {availableForHardLock}, " +
                                 $"Already locked: {totalHardLocked}");
            }
        }
        
        return Result.Ok();
    }
    
    private async Task<decimal> GetPhysicalBalance(string location, string sku)
    {
        var projection = await _session.Query<LocationBalanceProjection>()
            .FirstOrDefaultAsync(p => p.Location == location && p.SKU == sku);
        
        return projection?.Quantity ?? 0;
    }
}

public class ActiveHardLock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReservationId { get; set; }
    public string Location { get; set; }
    public string SKU { get; set; }
    public decimal HardLockedQty { get; set; }
    public DateTime StartedAt { get; set; }
}
```

---

### 5.5 ActiveHardLocks Projection (MITIGATION R-4)

**Purpose:** Maintain real-time view of active HARD locks for efficient conflict detection.

**Projection Logic:**

```csharp
// Event handler for PickingStarted
public class PickingStartedEventHandler : IConsumer<PickingStartedEvent>
{
    private readonly IDocumentSession _session;
    private readonly ILogger<PickingStartedEventHandler> _logger;
    
    public async Task Consume(ConsumeContext<PickingStartedEvent> context)
    {
        var evt = context.Message;
        
        // Check idempotency
        var checkpoint = await _session.Query<EventProcessingCheckpoint>()
            .FirstOrDefaultAsync(c => c.HandlerName == nameof(PickingStartedEventHandler) 
                                   && c.EventId == evt.EventId);
        
        if (checkpoint != null)
        {
            _logger.LogInformation("Event {EventId} already processed. Skipping.", evt.EventId);
            return;
        }
        
        // Load reservation to get allocated HUs and locations
        var reservation = await _session.Events.AggregateStreamAsync<Reservation>(
            $"reservation-{evt.ReservationId}");
        
        // Insert rows into ActiveHardLocks
        foreach (var line in reservation.Lines)
        {
            var hardLock = new ActiveHardLock
            {
                ReservationId = evt.ReservationId,
                Location = line.Location,
                SKU = line.SKU,
                HardLockedQty = line.RequestedQuantity,
                StartedAt = evt.Timestamp
            };
            
            _session.Store(hardLock);
        }
        
        // Record checkpoint
        _session.Store(new EventProcessingCheckpoint
        {
            HandlerName = nameof(PickingStartedEventHandler),
            EventId = evt.EventId,
            ProcessedAt = DateTime.UtcNow
        });
        
        await _session.SaveChangesAsync();
        
        _logger.LogInformation(
            "ActiveHardLocks updated for reservation {ReservationId}",
            evt.ReservationId);
    }
}

// Event handler for ReservationConsumed
public class ReservationConsumedEventHandler : IConsumer<ReservationConsumedEvent>
{
    private readonly IDocumentSession _session;
    private readonly ILogger<ReservationConsumedEventHandler> _logger;
    
    public async Task Consume(ConsumeContext<ReservationConsumedEvent> context)
    {
        var evt = context.Message;
        
        // Check idempotency
        var checkpoint = await _session.Query<EventProcessingCheckpoint>()
            .FirstOrDefaultAsync(c => c.HandlerName == nameof(ReservationConsumedEventHandler) 
                                   && c.EventId == evt.EventId);
        
        if (checkpoint != null)
        {
            _logger.LogInformation("Event {EventId} already processed. Skipping.", evt.EventId);
            return;
        }
        
        // Delete rows from ActiveHardLocks
        var locks = await _session.Query<ActiveHardLock>()
            .Where(l => l.ReservationId == evt.ReservationId)
            .ToListAsync();
        
        foreach (var lock in locks)
        {
            _session.Delete(lock);
        }
        
        // Record checkpoint
        _session.Store(new EventProcessingCheckpoint
        {
            HandlerName = nameof(ReservationConsumedEventHandler),
            EventId = evt.EventId,
            ProcessedAt = DateTime.UtcNow
        });
        
        await _session.SaveChangesAsync();
        
        _logger.LogInformation(
            "ActiveHardLocks cleared for reservation {ReservationId}",
            evt.ReservationId);
    }
}

// Event handler for ReservationCancelled
public class ReservationCancelledEventHandler : IConsumer<ReservationCancelledEvent>
{
    private readonly IDocumentSession _session;
    private readonly ILogger<ReservationCancelledEventHandler> _logger;
    
    public async Task Consume(ConsumeContext<ReservationCancelledEvent> context)
    {
        var evt = context.Message;
        
        // Check idempotency
        var checkpoint = await _session.Query<EventProcessingCheckpoint>()
            .FirstOrDefaultAsync(c => c.HandlerName == nameof(ReservationCancelledEventHandler) 
                                   && c.EventId == evt.EventId);
        
        if (checkpoint != null)
        {
            _logger.LogInformation("Event {EventId} already processed. Skipping.", evt.EventId);
            return;
        }
        
        // Delete rows from ActiveHardLocks (if reservation was PICKING)
        var locks = await _session.Query<ActiveHardLock>()
            .Where(l => l.ReservationId == evt.ReservationId)
            .ToListAsync();
        
        foreach (var lock in locks)
        {
            _session.Delete(lock);
        }
        
        // Record checkpoint
        _session.Store(new EventProcessingCheckpoint
        {
            HandlerName = nameof(ReservationCancelledEventHandler),
            EventId = evt.EventId,
            ProcessedAt = DateTime.UtcNow
        });
        
        await _session.SaveChangesAsync();
        
        _logger.LogInformation(
            "ActiveHardLocks cleared for cancelled reservation {ReservationId}",
            evt.ReservationId);
    }
}
```

**Marten Configuration:**

```csharp
services.AddMarten(options =>
{
    // ... other configuration
    
    // Configure ActiveHardLock as document
    options.Schema.For<ActiveHardLock>()
        .Index(l => l.ReservationId)
        .Index(l => l.Location)
        .Index(l => l.SKU)
        .Index(x => x.Location, x => x.SKU); // Composite index for conflict queries
});
```

---
