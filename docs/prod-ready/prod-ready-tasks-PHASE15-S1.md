# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 1 (Execution Pack)

**Version:** 1.0  
**Date:** February 10, 2026  
**Sprint:** Phase 1.5 Sprint 1  
**Source:** prod-ready-universe.md  
**Status:** Ready for Execution

---

## Sprint Overview

**Sprint Goal:** Establish foundation infrastructure and deliver core outbound/sales order functionality for production-ready B2B/B2C warehouse operations.

**Sprint Duration:** 2 weeks  
**Total Tasks:** 10  
**Estimated Effort:** 12 days (M=1d, L=2d, S=0.5d)

**Task Summary:**
- Foundation: 3 tasks (Idempotency, Event Versioning, Correlation/Trace)
- Sales Orders: 2 tasks (Customer + SalesOrder entities, APIs)
- Outbound/Shipment: 2 tasks (OutboundOrder + Shipment entities)
- Packing/Dispatch: 2 tasks (Packing MVP, Dispatch MVP)
- Projections/UI: 2 tasks (Projections, UI screens)

---

## Task PRD-1501: Foundation - Idempotency Completion

**Epic:** Foundation  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §5 (Idempotency Rules), Universe §4.Epic A (Commands/APIs)

### Context

- Phase 1 has basic idempotency via CommandId in some handlers
- Need comprehensive framework: all commands, events, saga steps, external API calls
- Prevent duplicate processing in distributed system (at-least-once delivery)
- Support replay scenarios (network retries, saga compensation)
- Critical for outbound/sales order workflows where duplicate shipments would be catastrophic

### Scope

**In Scope:**
- `processed_commands` table (CommandId, ProcessedAt, Result, CommandType, UserId)
- Idempotency middleware for all command handlers (MediatR pipeline behavior)
- Event processing checkpoints (event_processing_checkpoints table: HandlerName, StreamId, LastEventNumber)
- Saga step idempotency (store step results in SagaState, replay-safe)
- External API idempotency (include X-Idempotency-Key header in carrier/Agnum calls)
- Cleanup job (delete processed_commands older than 30 days)

**Out of Scope:**
- Query idempotency (read operations are naturally idempotent)
- UI-level deduplication (handled by API layer)
- Idempotency for non-critical operations (GET requests)

### Requirements

**Functional:**
1. All command handlers MUST check `processed_commands` before execution
2. If CommandId exists → return cached result (no re-execution)
3. If CommandId new → execute + store result atomically (same DB transaction)
4. Event handlers MUST check `event_processing_checkpoints` (StreamId, EventNumber)
5. Saga steps MUST store intermediate results (SagaState table via MassTransit)
6. External API calls MUST include `X-Idempotency-Key` header (value = CommandId or EventId)
7. Cleanup job runs daily at 02:00 UTC, deletes records older than 30 days

**Non-Functional:**
1. Idempotency check latency: < 10ms (indexed lookup on CommandId)
2. Storage overhead: < 1MB per 1000 commands (compact JSONB result storage)
3. Retention: 30 days for processed_commands (compliance + debugging window)
4. Thread-safe: handle concurrent duplicate requests (DB unique constraint on CommandId)
5. Transactional: command execution + result storage in single transaction (no partial state)

**Data Model:**
```sql
CREATE TABLE processed_commands (
  command_id UUID PRIMARY KEY,
  command_type VARCHAR(200) NOT NULL,
  processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  result_json JSONB,
  user_id UUID,
  correlation_id UUID,
  INDEX idx_processed_commands_processed_at (processed_at),
  INDEX idx_processed_commands_command_type (command_type)
);

CREATE TABLE event_processing_checkpoints (
  handler_name VARCHAR(200) NOT NULL,
  stream_id VARCHAR(500) NOT NULL,
  last_event_number BIGINT NOT NULL,
  processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (handler_name, stream_id),
  INDEX idx_event_checkpoints_processed_at (processed_at)
);
```

**API Changes:**
- All command DTOs MUST include `CommandId` property (GUID, required)
- Middleware: `IdempotencyMiddleware` (MediatR `IPipelineBehavior<TRequest, TResponse>`)
- Example command:
```csharp
public record CreateSalesOrderCommand(
  Guid CommandId, // REQUIRED
  Guid CustomerId,
  List<SalesOrderLineDto> Lines,
  DateTime RequestedDeliveryDate
) : IRequest<Result<CreateSalesOrderResult>>;
```

### Acceptance Criteria

```gherkin
Scenario: Duplicate command rejected with cached result
  Given command "AdjustStock" with CommandId "abc-123" already processed
  And cached result: { success: true, newQty: 100 }
  When same command submitted again with CommandId "abc-123"
  Then API returns cached result immediately
  And command handler NOT executed
  And response time < 50ms
  And response includes header: X-Idempotent-Replay: true

Scenario: Concurrent duplicate commands handled safely
  Given command "CreateSalesOrder" with CommandId "xyz-789"
  When 2 requests arrive simultaneously with same CommandId
  Then first request executes command
  And second request waits for DB lock (unique constraint)
  And second request returns cached result
  And only 1 sales order created
  And both responses identical

Scenario: Event handler idempotency
  Given event "StockMoved" (stream: stockledger-item-001, event #42)
  And AvailableStock projection already processed event #42
  When event replayed (e.g., projection rebuild)
  Then projection handler checks checkpoint table
  And skips event #42 (already processed)
  And processes event #43 onwards
  And checkpoint updated: last_event_number = 43

Scenario: Saga step idempotency
  Given AllocationSaga step "CreateReservation" completed
  And SagaState stores: { reservationId: "res-001", status: "allocated" }
  When saga replayed (e.g., after crash)
  Then step "CreateReservation" checks SagaState
  And skips re-execution (reservation already exists)
  And proceeds to next step "NotifyWarehouse"

Scenario: External API idempotency
  Given carrier API call "CreateShipment" with idempotency key "ship-001"
  When API call retried (network timeout)
  Then carrier API receives same X-Idempotency-Key header
  And carrier returns existing shipment (no duplicate)
  And tracking number matches original

Scenario: Cleanup job removes old records
  Given processed_commands table has 10,000 records
  And 3,000 records older than 30 days
  When cleanup job runs at 02:00 UTC
  Then 3,000 old records deleted
  And 7,000 recent records retained
  And job completes in < 5 seconds

Scenario: Command fails validation - no idempotency record
  Given command "CreateSalesOrder" with CommandId "fail-123"
  When command validation fails (customer not found)
  Then error returned: 400 Bad Request
  And NO record inserted into processed_commands
  And retry with same CommandId executes validation again
```

### Implementation Notes

- Use MediatR `IPipelineBehavior<TRequest, TResponse>` for command idempotency
- Store result as JSONB (flexible schema, queryable if needed)
- Cleanup job: Hangfire recurring job or cron-triggered endpoint
- For saga idempotency: use MassTransit's built-in saga state persistence (automatic)
- Transaction scope: wrap command execution + result storage in single EF Core transaction
- Error handling: if command fails, do NOT store in processed_commands (allow retry)

### Validation / Checks

**Local Testing:**
```bash
# Run idempotency tests
dotnet test --filter "Category=Idempotency"

# Check processed_commands table
psql -d warehouse -c "SELECT COUNT(*), command_type FROM processed_commands GROUP BY command_type;"

# Simulate duplicate command
curl -X POST http://localhost:5000/api/warehouse/v1/adjustments \
  -H "Content-Type: application/json" \
  -d '{"commandId":"test-123","itemId":"...","qty":10,"reason":"TEST"}'
# Repeat same curl → should return cached result with X-Idempotent-Replay: true header

# Test cleanup job
curl -X POST http://localhost:5000/api/admin/idempotency/cleanup
```

**Metrics:**
- `idempotency_cache_hits_total` (counter, labels: command_type)
- `idempotency_cache_misses_total` (counter, labels: command_type)
- `idempotency_check_duration_ms` (histogram)
- `idempotency_cleanup_records_deleted_total` (counter)

**Logs:**
- INFO: "Command {CommandId} already processed, returning cached result" (include command_type, user_id)
- WARN: "Concurrent duplicate command detected: {CommandId}, waiting for lock"
- INFO: "Idempotency cleanup completed: {DeletedCount} records removed"

**Backwards Compatibility:**
- New tables, no breaking changes to existing APIs
- Existing commands without CommandId: add CommandId field (breaking change, coordinate with clients)
- Migration path: generate CommandId server-side if missing (temporary fallback)

### Definition of Done

- [ ] `processed_commands` table created with migration (EF Core)
- [ ] `event_processing_checkpoints` table created with migration
- [ ] `IdempotencyMiddleware` implemented and registered in Program.cs
- [ ] All command DTOs updated to include CommandId property
- [ ] All command handlers wrapped with idempotency check
- [ ] Event handlers check checkpoints before processing
- [ ] Saga steps check SagaState before execution (MassTransit built-in)
- [ ] External API calls include X-Idempotency-Key header
- [ ] Cleanup job implemented (Hangfire recurring job, daily 02:00 UTC)
- [ ] Unit tests: 15+ scenarios (duplicate, concurrent, replay, cleanup)
- [ ] Integration tests: end-to-end command deduplication (API → DB → cache)
- [ ] Metrics exposed (cache hits/misses, latency, cleanup count)
- [ ] Documentation updated (ADR: Idempotency Strategy, API docs with CommandId requirement)
- [ ] Code review completed
- [ ] Manual testing: submit duplicate commands via Postman, verify cached response

---

## Task PRD-1502: Foundation - Event Schema Versioning

**Epic:** Foundation  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §5 (Event Schema Versioning), Universe §7.Appendix A (Event Catalog)

### Context

- Event sourcing requires immutable events (cannot change schema after published)
- Need versioning strategy for schema evolution (add fields, rename fields, change types)
- All events must include SchemaVersion field (e.g., "v1", "v2")
- Event upcasting: convert old events to new schema on read (transparent to consumers)
- Backwards compatibility: new code must handle old events without data loss

### Scope

**In Scope:**
- Event schema versioning convention (SchemaVersion field in all events, string type)
- Event upcasting framework (`IEventUpcaster<TFrom, TTo>` interface)
- Version registry (map event type → supported versions, loaded at startup)
- Marten configuration for event upcasting (register upcasters in DI)
- Documentation: schema evolution guide (when to upcast vs create new event type)
- Example upcaster: StockMovedV1 → StockMovedV2 (add MovementType field)

**Out of Scope:**
- Downcasting (new events → old schema, not needed for forward-only evolution)
- Cross-aggregate schema coordination (each aggregate owns its events independently)
- Event migration (rewriting event store, too risky and unnecessary with upcasting)

### Requirements

**Functional:**
1. All event records MUST include SchemaVersion property (string, e.g., "v1", "v2")
2. Event upcaster interface: `IEventUpcaster<TFrom, TTo>` with `TTo Upcast(TFrom oldEvent)` method
3. Marten MUST apply upcasters on event read (transparent to projection handlers)
4. Version registry MUST track: event type → list of supported versions (v1, v2, v3)
5. If event version not in registry → throw exception (unknown version, fail fast)
6. Upcasters MUST be idempotent (upcast(upcast(event)) = upcast(event))
7. Upcasting chain: v1 → v2 → v3 (apply multiple upcasters sequentially)

**Non-Functional:**
1. Upcasting overhead: < 5ms per event (in-memory transformation, no DB calls)
2. Version registry: loaded at startup (no runtime lookups, cached in memory)
3. Documentation: schema evolution guide with examples (when to upcast, when to create new event)
4. Testing: property-based tests (upcast random old events, verify new schema)

**Implementation Pattern:**
```csharp
// Event with versioning
public record StockMovedV1(
  Guid ItemId,
  decimal Qty,
  string From,
  string To,
  string SchemaVersion = "v1"
);

public record StockMovedV2(
  Guid ItemId,
  decimal Qty,
  string FromLocation,
  string ToLocation,
  StockMovementType Type, // NEW FIELD
  string SchemaVersion = "v2"
);

// Upcaster
public class StockMovedV1ToV2Upcaster : IEventUpcaster<StockMovedV1, StockMovedV2>
{
  public StockMovedV2 Upcast(StockMovedV1 old) => new StockMovedV2(
    old.ItemId,
    old.Qty,
    old.From, // Renamed field: From → FromLocation
    old.To,   // Renamed field: To → ToLocation
    StockMovementType.TRANSFER, // Default for old events (no Type field in v1)
    "v2"
  );
}

// Marten configuration (Program.cs)
services.AddMarten(opts => {
  opts.Events.Upcast<StockMovedV1, StockMovedV2>(new StockMovedV1ToV2Upcaster());
  // Register all upcasters here
});
```

### Acceptance Criteria

```gherkin
Scenario: Event with schema version
  Given new event StockMovedV1 created
  When event serialized to JSON
  Then JSON includes field: "schemaVersion": "v1"
  And field is at root level (not nested)
  And field is string type (not int)

Scenario: Upcast old event on read
  Given event store contains StockMovedV1 event (version v1)
  And upcaster registered: StockMovedV1ToV2Upcaster
  When projection reads event stream
  Then event automatically upcasted to StockMovedV2
  And projection receives StockMovedV2 (not V1)
  And Type field populated with default: TRANSFER
  And FromLocation = old From field
  And ToLocation = old To field

Scenario: Multiple version upcasting (v1 → v2 → v3)
  Given event store contains StockMovedV1 event
  And upcasters registered: V1ToV2, V2ToV3
  When projection reads event
  Then event upcasted through chain: V1 → V2 → V3
  And projection receives StockMovedV3
  And all transformations applied correctly

Scenario: Unknown version throws exception
  Given event store contains event with SchemaVersion="v99"
  And no upcaster registered for v99
  When projection reads event
  Then exception thrown: "Unknown event version: v99 for event type StockMoved"
  And error logged with event details (stream_id, event_number)
  And projection rebuild fails (fail fast, do not skip)

Scenario: Upcaster idempotency
  Given StockMovedV2 event (already latest version)
  When upcaster V1ToV2 applied (should not match)
  Then event unchanged (no transformation)
  And SchemaVersion remains "v2"
  And no errors thrown

Scenario: Upcasting performance
  Given event stream with 10,000 events (mix of v1, v2)
  When projection rebuilds from stream
  Then upcasting completes in < 50ms total (< 5ms per event)
  And projection receives all events in latest schema (v2)
```

### Implementation Notes

- Use Marten's built-in upcasting: `opts.Events.Upcast<TFrom, TTo>(upcaster)`
- Upcasters are singletons (registered at startup, reused for all events)
- For complex transformations: use async upcasters (query DB for lookup data, e.g., ItemId → Category)
- Version naming: use semantic versioning (v1, v2, v3) not dates (avoid "v2024-02-10")
- Breaking changes: create new event type (e.g., StockMovedV2 → StockTransferred) if schema incompatible
- Upcasting chain: Marten applies upcasters in registration order (v1→v2, then v2→v3)

### Validation / Checks

**Local Testing:**
```bash
# Run event versioning tests
dotnet test --filter "Category=EventVersioning"

# Verify event schema in DB
psql -d warehouse -c "SELECT data->>'SchemaVersion' as version, COUNT(*) FROM mt_events GROUP BY version;"

# Test upcasting (insert old event, read with projection)
dotnet test --filter "FullyQualifiedName~EventUpcastingTests"

# Performance test (upcast 10k events)
dotnet test --filter "FullyQualifiedName~EventUpcastingPerformanceTests"
```

**Metrics:**
- `event_upcasting_duration_ms` (histogram, labels: from_version, to_version, event_type)
- `event_upcasting_errors_total` (counter, labels: event_type, error_type)
- `event_upcasting_applied_total` (counter, labels: from_version, to_version)

**Logs:**
- INFO: "Event upcaster registered: {FromType} v{FromVersion} → {ToType} v{ToVersion}"
- WARN: "Event version mismatch: expected {ExpectedVersion}, got {ActualVersion}, applying upcaster"
- ERROR: "Event upcasting failed: {EventType}, {Exception}"

**Backwards Compatibility:**
- All existing events: add SchemaVersion="v1" via migration script (update mt_events table)
- New events: include SchemaVersion in constructor (default parameter)
- Projection handlers: no changes required (upcasting transparent)

### Definition of Done

- [ ] IEventUpcaster<TFrom, TTo> interface defined
- [ ] Marten upcasting configuration added to Program.cs
- [ ] Version registry implemented (event type → versions map, loaded at startup)
- [ ] All existing events updated with SchemaVersion="v1" (migration script)
- [ ] Example upcaster implemented (StockMovedV1ToV2)
- [ ] Unit tests: 15+ scenarios (upcast, chain, idempotency, unknown version, performance)
- [ ] Property-based tests: upcast random old events, verify schema correctness
- [ ] Documentation: schema evolution guide (docs/adr/002-event-schema-versioning.md)
- [ ] Code review completed
- [ ] Manual testing: insert old event, verify projection receives upcasted version

---

## Task PRD-1503: Foundation - Correlation/Trace Propagation

**Epic:** Foundation  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** S (0.5 day)  
**OwnerType:** Infra/DevOps  
**Dependencies:** None  
**SourceRefs:** Universe §5 (Observability > Traces)

### Context

- Distributed system: API → Command Handler → Event Bus → Saga → External API
- Need to trace requests across boundaries (correlation ID for debugging)
- OpenTelemetry standard for distributed tracing (W3C Trace Context)
- Correlation ID: unique per request (GUID), propagated in headers/logs/events
- Trace ID: OpenTelemetry trace identifier (spans linked across services)

### Scope

**In Scope:**
- Correlation ID generation (per HTTP request, middleware)
- Correlation ID propagation (HTTP headers, MassTransit messages, Marten events, logs)
- OpenTelemetry integration (ASP.NET Core, MassTransit, Marten instrumentation)
- Trace context propagation (W3C Trace Context standard: traceparent, tracestate headers)
- Structured logging with correlation ID (Serilog enricher)

**Out of Scope:**
- Trace visualization (use external tool: Jaeger, Zipkin, Application Insights)
- Span creation (basic auto-instrumentation only, detailed spans in Phase 2)
- Metrics correlation (separate concern, handled by Prometheus labels)

### Requirements

**Functional:**
1. Every HTTP request MUST generate correlation ID (GUID) if not provided in header
2. Correlation ID MUST be in response header: `X-Correlation-ID`
3. Correlation ID MUST be in all log messages (structured field: `CorrelationId`)
4. Correlation ID MUST be in all events (field: `CorrelationId` in event metadata)
5. Correlation ID MUST be in all MassTransit messages (header: `CorrelationId`)
6. OpenTelemetry trace context MUST be propagated (W3C Trace Context headers: traceparent, tracestate)
7. If client provides X-Correlation-ID header, use it (preserve client correlation)

**Non-Functional:**
1. Overhead: < 1ms per request (correlation ID generation + propagation)
2. Log format: JSON with correlation ID at root level (not nested)
3. Trace sampling: 100% in dev/staging, 10% in production (configurable via appsettings)
4. Trace export: Jaeger exporter in dev/staging, OTLP exporter in production

**Implementation Pattern:**
```csharp
// Middleware: Correlation ID
public class CorrelationIdMiddleware
{
  public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
  {
    var correlationId = ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault()
      ?? Guid.NewGuid().ToString();

    ctx.Items["CorrelationId"] = correlationId;
    ctx.Response.Headers.Add("X-Correlation-ID", correlationId);

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
      await next(ctx);
    }
  }
}

// OpenTelemetry setup (Program.cs)
services.AddOpenTelemetry()
  .WithTracing(builder => builder
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddSource("MassTransit")
    .AddJaegerExporter(opts => opts.Endpoint = new Uri("http://jaeger:14268"))
  );

// MassTransit: propagate correlation ID
cfg.ConfigurePublish(p => p.UseExecute(ctx => {
  if (ctx.CorrelationId == null)
    ctx.CorrelationId = Guid.NewGuid();
}));
```

### Acceptance Criteria

```gherkin
Scenario: Correlation ID generated per request
  Given HTTP request to /api/warehouse/v1/items
  When request processed
  Then response header includes: X-Correlation-ID: <guid>
  And all logs for this request include CorrelationId: <guid>
  And correlation ID is valid GUID format

Scenario: Correlation ID propagated to events
  Given HTTP request with X-Correlation-ID: abc-123
  When command emits event StockMoved
  Then event includes field: CorrelationId: abc-123
  And event stored in mt_events with correlation ID in metadata
  And event consumers receive correlation ID

Scenario: Correlation ID propagated to MassTransit
  Given HTTP request with X-Correlation-ID: xyz-789
  When saga publishes message to queue
  Then message header includes: CorrelationId: xyz-789
  And downstream consumer receives correlation ID
  And consumer logs include CorrelationId: xyz-789

Scenario: OpenTelemetry trace context propagated
  Given HTTP request with traceparent header (W3C Trace Context)
  When request calls external API (Agnum)
  Then external API call includes traceparent header
  And trace spans linked (parent → child)
  And trace visible in Jaeger UI

Scenario: Logs include correlation ID
  Given HTTP request with X-Correlation-ID: test-456
  When any log message written (INFO, WARN, ERROR)
  Then log JSON includes: "CorrelationId": "test-456"
  And correlation ID at root level (not nested in properties)
  And log searchable by correlation ID in log aggregator

Scenario: Client-provided correlation ID preserved
  Given HTTP request with X-Correlation-ID: client-999
  When request processed
  Then response header includes: X-Correlation-ID: client-999
  And all logs include CorrelationId: client-999
  And no new correlation ID generated
```

### Implementation Notes

- Use Serilog's `LogContext.PushProperty()` for correlation ID in logs
- MassTransit auto-propagates correlation ID (built-in, no custom code needed)
- Marten: add correlation ID to event metadata (custom projection or event enricher)
- OpenTelemetry: use auto-instrumentation (ASP.NET Core, HttpClient, MassTransit)
- Jaeger exporter: for dev/staging (local Docker container)
- OTLP exporter: for production (send to Application Insights or Datadog)

### Validation / Checks

**Local Testing:**
```bash
# Test correlation ID in response
curl -v http://localhost:5000/api/warehouse/v1/items | grep X-Correlation-ID

# Test correlation ID in logs
dotnet run
# Make request, check logs for CorrelationId field
cat logs/warehouse-*.log | jq '.CorrelationId'

# Test OpenTelemetry export
# Start Jaeger: docker run -d -p 16686:16686 -p 14268:14268 jaegertracing/all-in-one
# Make request, open http://localhost:16686, search for trace

# Test client-provided correlation ID
curl -H "X-Correlation-ID: test-123" http://localhost:5000/api/warehouse/v1/items
# Verify response header and logs include test-123
```

**Metrics:**
- N/A (tracing overhead tracked by OpenTelemetry, no custom metrics needed)

**Logs:**
- All logs MUST include CorrelationId field
- Example: `{"Timestamp":"2026-02-10T10:00:00Z","Level":"INFO","Message":"Item created","CorrelationId":"abc-123","ItemId":"..."}`

**Backwards Compatibility:**
- New middleware, no breaking changes
- Existing logs: add CorrelationId field (enricher applies to all logs)
- Existing events: add CorrelationId to metadata (migration script or event enricher)

### Definition of Done

- [ ] CorrelationIdMiddleware implemented and registered in Program.cs
- [ ] OpenTelemetry configured (ASP.NET Core, HttpClient, MassTransit instrumentation)
- [ ] Serilog configured with correlation ID enricher (LogContext.PushProperty)
- [ ] MassTransit correlation ID propagation verified (built-in, no custom code)
- [ ] Marten event metadata includes correlation ID (event enricher or projection)
- [ ] Unit tests: correlation ID generation, propagation, client-provided ID
- [ ] Integration tests: end-to-end trace (HTTP → Event → Saga → External API)
- [ ] Jaeger/Zipkin exporter configured (dev/staging)
- [ ] Documentation: how to trace requests across services (docs/observability.md)
- [ ] Code review completed
- [ ] Manual testing: submit request, verify correlation ID in response/logs/events/traces

---

## Task PRD-1504: Sales Orders - Customer Entity + SalesOrder Aggregate + State Machine

**Epic:** B - Sales Orders  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** L (2 days)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1501 (Idempotency)  
**SourceRefs:** Universe §4.Epic B (Entities & Data Model, State Machine)

### Context

- Phase 1 only supports production picking (material requests), no customer order management
- Need full sales order lifecycle: Create order → Allocate stock → Pick → Pack → Ship → Invoice
- Customer entity: master data (name, address, contact, payment terms)
- SalesOrder entity: order header + lines, status lifecycle
- State machine: DRAFT → ALLOCATED → PICKING → PACKED → SHIPPED → DELIVERED → INVOICED

### Scope

**In Scope:**
- Customer entity (state-based, EF Core): CustomerCode, Name, Email, Phone, BillingAddress, DefaultShippingAddress, PaymentTerms, CreditLimit, Status
- SalesOrder entity (state-based, EF Core): OrderNumber, CustomerId, ShippingAddress, Status, OrderDate, RequestedDeliveryDate, Lines, ReservationId, OutboundOrderId
- SalesOrderLine entity: OrderedQty, AllocatedQty, PickedQty, ShippedQty, UnitPrice, LineAmount
- Enums: CustomerStatus (ACTIVE, ON_HOLD, INACTIVE), PaymentTerms (NET30, NET60, COD, PREPAID), SalesOrderStatus (DRAFT, PENDING_APPROVAL, PENDING_STOCK, ALLOCATED, PICKING, PACKED, SHIPPED, DELIVERED, INVOICED, CANCELLED)
- Database schema (tables, indexes, constraints)
- EF Core configuration (entity mapping, relationships, value objects)
- State machine logic (status transitions with validation)

**Out of Scope:**
- Commands/handlers (separate task PRD-1505)
- API endpoints (PRD-1505)
- UI (separate tasks)
- Allocation saga (PRD-1505)
- Pricing engine (deferred to Phase 2)

### Requirements

**Functional:**
1. Customer MUST have unique CustomerCode (auto-generated: CUST-0001, CUST-0002, ...)
2. Customer MUST have BillingAddress (required) and DefaultShippingAddress (optional)
3. Customer CreditLimit nullable (null = no limit)
4. SalesOrder MUST have unique OrderNumber (auto-generated: SO-0001, SO-0002, ...)
5. SalesOrder MUST link to Customer (required)
6. SalesOrder ShippingAddress can override customer default
7. SalesOrder MUST have Status lifecycle (state machine validation)
8. SalesOrderLine MUST track: OrderedQty, AllocatedQty, PickedQty, ShippedQty
9. SalesOrder TotalAmount computed (sum of line amounts)
10. State machine transitions validated (e.g., cannot go from DRAFT to SHIPPED directly)

**Non-Functional:**
1. CustomerCode generation: thread-safe (DB sequence or GUID-based)
2. OrderNumber generation: thread-safe (DB sequence or GUID-based)
3. Audit fields: CreatedBy, CreatedAt, UpdatedBy, UpdatedAt (all entities)
4. Soft delete: IsDeleted flag (retain for audit, don't hard delete)
5. Indexes: status, customer name, order date (for fast queries)
6. Address value object: owned entity (no separate table)

**Data Model:**
```csharp
// Customer (master data)
public class Customer
{
  public Guid Id { get; set; }
  public string CustomerCode { get; set; } // Auto-generated: CUST-0001
  public string Name { get; set; }
  public string Email { get; set; }
  public string Phone { get; set; }
  public Address BillingAddress { get; set; } // Value object
  public Address DefaultShippingAddress { get; set; } // Value object
  public PaymentTerms PaymentTerms { get; set; }
  public CustomerStatus Status { get; set; }
  public decimal? CreditLimit { get; set; } // Nullable: no limit if null

  // Audit
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
  public string UpdatedBy { get; set; }
  public DateTime UpdatedAt { get; set; }
  public bool IsDeleted { get; set; }
}

// SalesOrder (state-based aggregate)
public class SalesOrder
{
  public Guid Id { get; set; }
  public string OrderNumber { get; set; } // Auto-generated: SO-0001
  public Guid CustomerId { get; set; }
  public Address ShippingAddress { get; set; } // Can override customer default
  public SalesOrderStatus Status { get; set; }
  public DateTime OrderDate { get; set; }
  public DateTime? RequestedDeliveryDate { get; set; }
  public DateTime? AllocatedAt { get; set; }
  public DateTime? ShippedAt { get; set; }
  public DateTime? DeliveredAt { get; set; }
  public DateTime? InvoicedAt { get; set; }
  public List<SalesOrderLine> Lines { get; set; }
  public Guid? ReservationId { get; set; } // Link to reservation
  public Guid? OutboundOrderId { get; set; } // Link to outbound order
  public decimal TotalAmount { get; set; } // Sum of line amounts

  // Navigation
  public Customer Customer { get; set; }

  // Audit
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
  public string UpdatedBy { get; set; }
  public DateTime UpdatedAt { get; set; }
  public bool IsDeleted { get; set; }

  // State machine methods
  public Result Submit() { /* DRAFT → PENDING_APPROVAL or ALLOCATED */ }
  public Result Approve() { /* PENDING_APPROVAL → ALLOCATED */ }
  public Result Allocate(Guid reservationId) { /* DRAFT/PENDING_STOCK → ALLOCATED */ }
  public Result Release() { /* ALLOCATED → PICKING */ }
  public Result Pack(Guid outboundOrderId) { /* PICKING → PACKED */ }
  public Result Ship(DateTime shippedAt) { /* PACKED → SHIPPED */ }
  public Result ConfirmDelivery(DateTime deliveredAt) { /* SHIPPED → DELIVERED */ }
  public Result Invoice(DateTime invoicedAt) { /* DELIVERED → INVOICED */ }
  public Result Cancel(string reason) { /* Any status → CANCELLED */ }
}

public class SalesOrderLine
{
  public Guid Id { get; set; }
  public Guid SalesOrderId { get; set; }
  public Guid ItemId { get; set; }
  public decimal OrderedQty { get; set; }
  public decimal AllocatedQty { get; set; }
  public decimal PickedQty { get; set; }
  public decimal ShippedQty { get; set; }
  public decimal UnitPrice { get; set; } // If pricing enabled, else 0
  public decimal LineAmount { get; set; } // OrderedQty * UnitPrice

  // Navigation
  public SalesOrder SalesOrder { get; set; }
  public Item Item { get; set; }
}

// Value Object: Address
public class Address
{
  public string Street { get; set; }
  public string City { get; set; }
  public string State { get; set; }
  public string ZipCode { get; set; }
  public string Country { get; set; }
}

// Enums
public enum CustomerStatus { ACTIVE, ON_HOLD, INACTIVE }
public enum PaymentTerms { NET30, NET60, COD, PREPAID, CREDIT_CARD }
public enum SalesOrderStatus
{
  DRAFT,              // Order created, not yet submitted
  PENDING_APPROVAL,   // Awaiting manager approval (if > credit limit)
  PENDING_STOCK,      // Insufficient stock, awaiting inventory
  ALLOCATED,          // Stock allocated (SOFT lock)
  PICKING,            // Picking in progress (HARD lock)
  PACKED,             // Packed, ready to ship
  SHIPPED,            // Dispatched to customer
  DELIVERED,          // Delivered to customer
  INVOICED,           // Invoice sent to customer
  CANCELLED           // Order cancelled
}
```

**Database Schema (SQL):**
```sql
CREATE TABLE customers (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  customer_code VARCHAR(50) NOT NULL UNIQUE,
  name VARCHAR(200) NOT NULL,
  email VARCHAR(200) NOT NULL,
  phone VARCHAR(50),
  billing_address_street VARCHAR(200),
  billing_address_city VARCHAR(100),
  billing_address_state VARCHAR(50),
  billing_address_zip_code VARCHAR(20),
  billing_address_country VARCHAR(100),
  default_shipping_address_street VARCHAR(200),
  default_shipping_address_city VARCHAR(100),
  default_shipping_address_state VARCHAR(50),
  default_shipping_address_zip_code VARCHAR(20),
  default_shipping_address_country VARCHAR(100),
  payment_terms VARCHAR(50) NOT NULL,
  status VARCHAR(50) NOT NULL,
  credit_limit DECIMAL(18,2) NULL,
  created_by VARCHAR(200) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by VARCHAR(200) NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX idx_customers_code ON customers(customer_code);
CREATE INDEX idx_customers_status ON customers(status) WHERE is_deleted = FALSE;
CREATE INDEX idx_customers_name ON customers(name) WHERE is_deleted = FALSE;

CREATE TABLE sales_orders (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  order_number VARCHAR(50) NOT NULL UNIQUE,
  customer_id UUID NOT NULL REFERENCES customers(id),
  shipping_address_street VARCHAR(200),
  shipping_address_city VARCHAR(100),
  shipping_address_state VARCHAR(50),
  shipping_address_zip_code VARCHAR(20),
  shipping_address_country VARCHAR(100),
  status VARCHAR(50) NOT NULL,
  order_date DATE NOT NULL,
  requested_delivery_date DATE NULL,
  allocated_at TIMESTAMPTZ NULL,
  shipped_at TIMESTAMPTZ NULL,
  delivered_at TIMESTAMPTZ NULL,
  invoiced_at TIMESTAMPTZ NULL,
  reservation_id UUID NULL,
  outbound_order_id UUID NULL,
  total_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
  created_by VARCHAR(200) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by VARCHAR(200) NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX idx_sales_orders_status ON sales_orders(status) WHERE is_deleted = FALSE;
CREATE INDEX idx_sales_orders_customer_id ON sales_orders(customer_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_sales_orders_order_number ON sales_orders(order_number);
CREATE INDEX idx_sales_orders_order_date ON sales_orders(order_date) WHERE is_deleted = FALSE;

CREATE TABLE sales_order_lines (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  sales_order_id UUID NOT NULL REFERENCES sales_orders(id) ON DELETE CASCADE,
  item_id UUID NOT NULL REFERENCES items(id),
  ordered_qty DECIMAL(18,4) NOT NULL,
  allocated_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  picked_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  shipped_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  unit_price DECIMAL(18,2) NOT NULL DEFAULT 0,
  line_amount DECIMAL(18,2) NOT NULL DEFAULT 0
);

CREATE INDEX idx_sales_order_lines_sales_order_id ON sales_order_lines(sales_order_id);
CREATE INDEX idx_sales_order_lines_item_id ON sales_order_lines(item_id);
```

### Acceptance Criteria

```gherkin
Scenario: Create Customer entity
  Given Customer entity defined with all properties
  When EF Core migration generated
  Then customers table created
  And indexes created (customer_code, status, name)
  And foreign keys enforced (none for Customer)
  And CustomerCode unique constraint enforced

Scenario: CustomerCode auto-generation
  Given Customer created without CustomerCode
  When entity saved to database
  Then CustomerCode generated: CUST-0001
  And subsequent customers: CUST-0002, CUST-0003, ...
  And CustomerCode is unique (DB constraint)

Scenario: Create SalesOrder entity
  Given SalesOrder entity defined with all properties
  When EF Core migration generated
  Then sales_orders table created
  And sales_order_lines table created
  And indexes created (status, customer_id, order_number, order_date)
  And foreign keys enforced (customer_id, item_id)

Scenario: OrderNumber auto-generation
  Given SalesOrder created without OrderNumber
  When entity saved to database
  Then OrderNumber generated: SO-0001
  And subsequent orders: SO-0002, SO-0003, ...
  And OrderNumber is unique (DB constraint)

Scenario: Address value object mapping
  Given Customer with BillingAddress and DefaultShippingAddress
  When entity saved
  Then addresses stored in 10 columns (5 per address)
  And addresses retrieved as Address objects (not separate entities)

Scenario: SalesOrder state machine - valid transition
  Given SalesOrder with status DRAFT
  When Submit() called
  And customer credit limit not exceeded
  Then status updated to ALLOCATED
  And AllocatedAt timestamp set
  And no errors thrown

Scenario: SalesOrder state machine - invalid transition
  Given SalesOrder with status DRAFT
  When Ship() called (skip ALLOCATED, PICKING, PACKED)
  Then error returned: "Invalid status transition: DRAFT → SHIPPED"
  And status unchanged (DRAFT)
  And no database changes

Scenario: Soft delete behavior
  Given Customer with IsDeleted = true
  When querying customers
  Then soft-deleted customers NOT returned (global query filter)
  And can query with IgnoreQueryFilters() to see deleted

Scenario: Audit fields auto-populated
  Given SalesOrder created by user "john@example.com"
  When entity saved
  Then CreatedBy = "john@example.com"
  And CreatedAt = current timestamp
  And UpdatedBy = "john@example.com"
  And UpdatedAt = current timestamp
```

### Implementation Notes

- Use EF Core value converters for enums (store as strings, not ints)
- Address is value object (owned entity, no separate table)
- CustomerCode/OrderNumber generation: use DB sequence or GUID-based (thread-safe)
- Soft delete: global query filter prevents accidental hard deletes
- Audit fields: populate via SaveChanges interceptor (see Phase 1 implementation)
- State machine: implement as methods on SalesOrder entity (validate transitions)

### Validation / Checks

**Local Testing:**
```bash
# Generate migration
dotnet ef migrations add AddCustomerAndSalesOrder --project src/LKvitai.MES.Infrastructure

# Apply migration
dotnet ef database update --project src/LKvitai.MES.Api

# Verify schema
psql -d warehouse -c "\d customers"
psql -d warehouse -c "\d sales_orders"
psql -d warehouse -c "\d sales_order_lines"
psql -d warehouse -c "\di customers*"
psql -d warehouse -c "\di sales_orders*"

# Test entity creation
dotnet test --filter "FullyQualifiedName~CustomerTests"
dotnet test --filter "FullyQualifiedName~SalesOrderTests"
```

**Metrics:**
- N/A (entity definition, no runtime metrics)

**Logs:**
- N/A (entity definition, no logs)

**Backwards Compatibility:**
- New tables, no breaking changes
- Ensure migration is reversible (Down() method)

### Definition of Done

- [ ] Customer entity class created (Customer.cs)
- [ ] SalesOrder entity class created (SalesOrder.cs)
- [ ] SalesOrderLine entity class created
- [ ] Enums defined (CustomerStatus, PaymentTerms, SalesOrderStatus)
- [ ] Address value object defined
- [ ] EF Core configuration created (CustomerConfiguration.cs, SalesOrderConfiguration.cs)
- [ ] State machine methods implemented on SalesOrder (Submit, Approve, Allocate, Release, Pack, Ship, ConfirmDelivery, Invoice, Cancel)
- [ ] Migration generated (AddCustomerAndSalesOrder)
- [ ] Migration applied to local DB
- [ ] Schema verified (tables, indexes, constraints)
- [ ] Unit tests: entity creation, validation, relationships, state machine transitions (20+ tests)
- [ ] Soft delete tested (global query filter)
- [ ] Audit fields tested (auto-populated)
- [ ] Code review completed
- [ ] Documentation: entity diagram added to docs/

---

