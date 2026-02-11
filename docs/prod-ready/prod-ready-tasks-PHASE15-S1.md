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

## Task PRD-1505: Sales Orders - APIs for CRUD/Import and Status Transitions

**Epic:** B - Sales Orders  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** L (2 days)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1504 (Customer + SalesOrder entities)  
**SourceRefs:** Universe §4.Epic B (Commands/APIs, Events, State Machine)

### Context

- PRD-1504 created Customer and SalesOrder entities with state machine
- Need API endpoints for full order lifecycle: Create → Submit → Approve → Allocate → Release → Cancel
- Commands trigger state transitions and emit events for downstream consumers
- Allocation saga listens to SalesOrderCreated event and auto-allocates stock
- Manager approval required if order exceeds customer credit limit
- Idempotency required (all commands include CommandId)

### Scope

**In Scope:**
- CreateSalesOrderCommand + handler (status: DRAFT)
- SubmitSalesOrderCommand + handler (DRAFT → PENDING_APPROVAL or ALLOCATED)
- ApproveSalesOrderCommand + handler (PENDING_APPROVAL → ALLOCATED)
- AllocateSalesOrderCommand + handler (manual allocation trigger)
- ReleaseSalesOrderCommand + handler (ALLOCATED → PICKING, SOFT → HARD lock)
- CancelSalesOrderCommand + handler (any status → CANCELLED)
- Query endpoints: GET /sales-orders (list), GET /sales-orders/{id} (details)
- Events: SalesOrderCreated, SalesOrderAllocated, SalesOrderReleased, SalesOrderCancelled
- Validation: customer exists, items exist, qty > 0, credit limit check
- Authorization: Sales Admin, Manager roles

**Out of Scope:**
- Pricing engine (manual unit price entry, deferred to Phase 2)
- Customer portal (API only, UI in PRD-1510)
- Backorder management (if insufficient stock → order stays PENDING_STOCK)
- Multi-currency (USD only)

### Requirements

**Functional:**
1. POST /api/warehouse/v1/sales-orders: Create order (status=DRAFT)
2. POST /api/warehouse/v1/sales-orders/{id}/submit: Submit for allocation
3. POST /api/warehouse/v1/sales-orders/{id}/approve: Manager approval (if > credit limit)
4. POST /api/warehouse/v1/sales-orders/{id}/allocate: Manual allocation trigger
5. POST /api/warehouse/v1/sales-orders/{id}/release: Release to picking (SOFT → HARD)
6. POST /api/warehouse/v1/sales-orders/{id}/cancel: Cancel order
7. GET /api/warehouse/v1/sales-orders: List orders (filters: status, customerId, dateRange)
8. GET /api/warehouse/v1/sales-orders/{id}: Get order details (with lines, customer, reservation)
9. All commands MUST validate state transitions (e.g., cannot release CANCELLED order)
10. All commands MUST emit events for saga/projection consumption

**Non-Functional:**
1. API latency: < 500ms (95th percentile) for create/submit/approve/release
2. Allocation latency: < 2 seconds (includes reservation creation)
3. Idempotency: all commands include CommandId (GUID)
4. Authorization: Sales Admin can create/submit/cancel, Manager can approve/release
5. Validation errors: return 400 Bad Request with detailed error messages
6. Concurrency: optimistic locking (EF Core RowVersion) to prevent lost updates

**Data Model (DTOs):**
```csharp
// Request DTOs
public record CreateSalesOrderRequest(
  Guid CommandId,
  Guid CustomerId,
  Address ShippingAddress, // Optional, defaults to customer default
  DateTime? RequestedDeliveryDate,
  List<SalesOrderLineDto> Lines
);

public record SalesOrderLineDto(
  Guid ItemId,
  decimal Qty,
  decimal UnitPrice // Manual entry, no pricing engine
);

public record SubmitSalesOrderRequest(Guid CommandId);
public record ApproveSalesOrderRequest(Guid CommandId);
public record AllocateSalesOrderRequest(Guid CommandId);
public record ReleaseSalesOrderRequest(Guid CommandId);
public record CancelSalesOrderRequest(Guid CommandId, string Reason);

// Response DTOs
public record SalesOrderResponse(
  Guid Id,
  string OrderNumber,
  Guid CustomerId,
  string CustomerName,
  Address ShippingAddress,
  SalesOrderStatus Status,
  DateTime OrderDate,
  DateTime? RequestedDeliveryDate,
  DateTime? AllocatedAt,
  DateTime? ShippedAt,
  List<SalesOrderLineResponse> Lines,
  Guid? ReservationId,
  Guid? OutboundOrderId,
  decimal TotalAmount
);

public record SalesOrderLineResponse(
  Guid Id,
  Guid ItemId,
  string ItemSku,
  string ItemDescription,
  decimal OrderedQty,
  decimal AllocatedQty,
  decimal PickedQty,
  decimal ShippedQty,
  decimal UnitPrice,
  decimal LineAmount
);
```

**API Contracts:**
```
POST /api/warehouse/v1/sales-orders
Request: CreateSalesOrderRequest
Response: 201 Created, SalesOrderResponse
Errors: 400 (validation), 404 (customer/item not found), 409 (duplicate CommandId)

POST /api/warehouse/v1/sales-orders/{id}/submit
Request: SubmitSalesOrderRequest
Response: 200 OK, SalesOrderResponse (status=PENDING_APPROVAL or ALLOCATED)
Errors: 400 (invalid state), 404 (order not found), 409 (insufficient stock)

POST /api/warehouse/v1/sales-orders/{id}/approve
Request: ApproveSalesOrderRequest
Response: 200 OK, SalesOrderResponse (status=ALLOCATED)
Errors: 400 (invalid state), 403 (not Manager), 404 (order not found)

POST /api/warehouse/v1/sales-orders/{id}/allocate
Request: AllocateSalesOrderRequest
Response: 200 OK, { reservationId: Guid }
Errors: 400 (invalid state), 404 (order not found), 409 (insufficient stock)

POST /api/warehouse/v1/sales-orders/{id}/release
Request: ReleaseSalesOrderRequest
Response: 200 OK, SalesOrderResponse (status=PICKING)
Errors: 400 (invalid state), 403 (not Manager), 404 (order not found)

POST /api/warehouse/v1/sales-orders/{id}/cancel
Request: CancelSalesOrderRequest
Response: 200 OK, SalesOrderResponse (status=CANCELLED)
Errors: 400 (invalid state), 404 (order not found)

GET /api/warehouse/v1/sales-orders?status=ALLOCATED&customerId={guid}&dateFrom=2026-01-01&dateTo=2026-12-31
Response: 200 OK, SalesOrderResponse[]

GET /api/warehouse/v1/sales-orders/{id}
Response: 200 OK, SalesOrderResponse
Errors: 404 (order not found)
```

**Events:**
```csharp
public record SalesOrderCreated(
  Guid Id,
  string OrderNumber,
  Guid CustomerId,
  List<SalesOrderLineDto> Lines,
  DateTime OrderDate,
  DateTime? RequestedDeliveryDate,
  string SchemaVersion = "v1"
);

public record SalesOrderAllocated(
  Guid Id,
  Guid ReservationId,
  DateTime AllocatedAt,
  string SchemaVersion = "v1"
);

public record SalesOrderReleased(
  Guid Id,
  Guid ReservationId,
  DateTime ReleasedAt,
  string SchemaVersion = "v1"
);

public record SalesOrderCancelled(
  Guid Id,
  string Reason,
  string CancelledBy,
  DateTime CancelledAt,
  string SchemaVersion = "v1"
);
```

### Acceptance Criteria

```gherkin
Scenario: Create sales order successfully
  Given customer "CUST-0001" exists with status ACTIVE
  And items "ITEM-001", "ITEM-002" exist
  When POST /api/warehouse/v1/sales-orders with:
    | customerId | CUST-0001 |
    | lines      | [{ itemId: ITEM-001, qty: 10, unitPrice: 5.00 }, { itemId: ITEM-002, qty: 5, unitPrice: 10.00 }] |
  Then response status: 201 Created
  And response body includes: orderNumber "SO-0001", status "DRAFT", totalAmount 100.00
  And SalesOrderCreated event emitted
  And order stored in sales_orders table

Scenario: Submit order for allocation (auto-allocate if sufficient stock)
  Given sales order "SO-0001" with status DRAFT
  And customer credit limit: 500.00, order total: 100.00 (within limit)
  And sufficient stock available for all items
  When POST /api/warehouse/v1/sales-orders/SO-0001/submit
  Then response status: 200 OK
  And response body: status "ALLOCATED"
  And SalesOrderAllocated event emitted
  And reservation created with SOFT lock
  And allocatedAt timestamp populated

Scenario: Submit order requires approval (exceeds credit limit)
  Given sales order "SO-0002" with status DRAFT
  And customer credit limit: 50.00, order total: 100.00 (exceeds limit)
  When POST /api/warehouse/v1/sales-orders/SO-0002/submit
  Then response status: 200 OK
  And response body: status "PENDING_APPROVAL"
  And NO allocation performed
  And NO SalesOrderAllocated event emitted

Scenario: Manager approves order
  Given sales order "SO-0002" with status PENDING_APPROVAL
  And user has Manager role
  When POST /api/warehouse/v1/sales-orders/SO-0002/approve
  Then response status: 200 OK
  And response body: status "ALLOCATED"
  And SalesOrderAllocated event emitted
  And reservation created

Scenario: Release order to picking (SOFT → HARD lock)
  Given sales order "SO-0001" with status ALLOCATED
  And reservation exists with SOFT lock
  And user has Manager role
  When POST /api/warehouse/v1/sales-orders/SO-0001/release
  Then response status: 200 OK
  And response body: status "PICKING"
  And SalesOrderReleased event emitted
  And reservation lock upgraded to HARD
  And outbound order created

Scenario: Cancel order and release stock
  Given sales order "SO-0001" with status ALLOCATED
  And reservation exists
  When POST /api/warehouse/v1/sales-orders/SO-0001/cancel with reason "Customer requested cancellation"
  Then response status: 200 OK
  And response body: status "CANCELLED"
  And SalesOrderCancelled event emitted
  And reservation released (stock available again)

Scenario: Validation failure - customer not found
  When POST /api/warehouse/v1/sales-orders with customerId "invalid-guid"
  Then response status: 400 Bad Request
  And error message: "Customer not found"
  And NO order created
  And NO event emitted

Scenario: Validation failure - insufficient stock
  Given sales order "SO-0003" with status DRAFT
  And item "ITEM-001" has available qty: 5
  And order line requests qty: 10
  When POST /api/warehouse/v1/sales-orders/SO-0003/submit
  Then response status: 409 Conflict
  And error message: "Insufficient stock for item ITEM-001: requested 10, available 5"
  And order status remains DRAFT
  And NO allocation performed
```

### Implementation Notes

- Use MediatR for command handlers (CreateSalesOrderCommandHandler, etc.)
- State machine validation: use SalesOrder.Submit(), SalesOrder.Approve() methods (from PRD-1504)
- Allocation saga: triggered by SalesOrderCreated event (separate saga, not in this task)
- Credit limit check: compare order.TotalAmount with customer.CreditLimit (if not null)
- Reservation creation: call existing CreateReservationCommand (from Phase 1)
- Event publishing: use MassTransit IPublishEndpoint
- Authorization: use [Authorize(Policy = WarehousePolicies.SalesAdmin)] attribute
- Optimistic locking: add RowVersion column to sales_orders table (EF Core concurrency token)

### Validation / Checks

**Local Testing:**
```bash
# Create sales order
curl -X POST http://localhost:5000/api/warehouse/v1/sales-orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "commandId": "test-001",
    "customerId": "<guid>",
    "lines": [{ "itemId": "<guid>", "qty": 10, "unitPrice": 5.00 }]
  }'

# Submit order
curl -X POST http://localhost:5000/api/warehouse/v1/sales-orders/<id>/submit \
  -H "Content-Type: application/json" \
  -d '{ "commandId": "test-002" }'

# List orders
curl http://localhost:5000/api/warehouse/v1/sales-orders?status=ALLOCATED

# Run tests
dotnet test --filter "Category=SalesOrders"
```

**Metrics:**
- `sales_orders_created_total` (counter)
- `sales_orders_allocated_total` (counter)
- `sales_orders_cancelled_total` (counter, labels: reason)
- `sales_order_allocation_duration_ms` (histogram)
- `sales_order_api_errors_total` (counter, labels: endpoint, error_type)

**Logs:**
- INFO: "SalesOrder {OrderNumber} created by {UserId}, total {TotalAmount}"
- INFO: "SalesOrder {OrderNumber} allocated, reservation {ReservationId}"
- WARN: "SalesOrder {OrderNumber} requires approval, exceeds credit limit {CreditLimit}"
- ERROR: "SalesOrder allocation failed: {ErrorMessage}"

**Backwards Compatibility:**
- New API endpoints, no breaking changes
- New events, no consumers yet (projections in PRD-1509)

### Definition of Done

- [ ] CreateSalesOrderCommand + handler implemented
- [ ] SubmitSalesOrderCommand + handler implemented (with credit limit check)
- [ ] ApproveSalesOrderCommand + handler implemented
- [ ] AllocateSalesOrderCommand + handler implemented
- [ ] ReleaseSalesOrderCommand + handler implemented
- [ ] CancelSalesOrderCommand + handler implemented
- [ ] SalesOrdersController created with 8 endpoints
- [ ] DTOs defined (request/response)
- [ ] Events defined and published (SalesOrderCreated, Allocated, Released, Cancelled)
- [ ] State machine validation integrated (call entity methods)
- [ ] Authorization policies applied (Sales Admin, Manager)
- [ ] Idempotency middleware applied (CommandId check)
- [ ] Unit tests: 20+ scenarios (create, submit, approve, allocate, release, cancel, validation failures)
- [ ] Integration tests: end-to-end API calls (create → submit → allocate → release)
- [ ] Metrics exposed (counters, histograms)
- [ ] Logs added (INFO, WARN, ERROR with correlation IDs)
- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] Code review completed
- [ ] Manual testing: Postman collection with all endpoints

---
## Task PRD-1506: Outbound/Shipment - OutboundOrder + Shipment Entities + State Machine

**Epic:** A - Outbound/Shipment  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** L (2 days)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1504 (SalesOrder entities)  
**SourceRefs:** Universe §4.Epic A (Entities & Data Model, State Machine)

### Context

- Phase 1 has basic picking workflow but no shipment tracking
- Need OutboundOrder entity to represent warehouse fulfillment (decoupled from SalesOrder)
- Need Shipment entity to track carrier, tracking number, dispatch, delivery
- OutboundOrder created when SalesOrder released to picking (SOFT → HARD lock)
- Shipment created when order packed (consolidate items into shipping HU)
- State machines: OutboundOrder (DRAFT → ALLOCATED → PICKING → PACKED → SHIPPED → DELIVERED), Shipment (PACKING → PACKED → DISPATCHED → IN_TRANSIT → DELIVERED)

### Scope

**In Scope:**
- OutboundOrder entity (state-based, EF Core): OrderNumber, Type, Status, Lines, ReservationId, ShipmentId
- OutboundOrderLine entity: ItemId, Qty, PickedQty, ShippedQty
- Shipment entity (state-based, EF Core): ShipmentNumber, Carrier, TrackingNumber, Status, PackedAt, DispatchedAt, DeliveredAt
- ShipmentLine entity: ItemId, Qty, HandlingUnitId
- Enums: OutboundOrderType (SALES, TRANSFER, PRODUCTION_RETURN), OutboundOrderStatus, ShipmentStatus, Carrier
- Database schema (tables, indexes, constraints, migrations)
- EF Core configuration (entity mapping, relationships, value objects)
- State machine logic (status transitions with validation)

**Out of Scope:**
- Commands/handlers (separate tasks PRD-1507, PRD-1508)
- API endpoints (PRD-1507, PRD-1508)
- UI (PRD-1510)
- Carrier API integration (PRD-1508)
- Multi-parcel shipments (1 order = 1 shipment for Phase 1.5)

### Requirements

**Functional:**
1. OutboundOrder MUST have unique OrderNumber (auto-generated: OUT-0001, OUT-0002, ...)
2. OutboundOrder MUST link to Reservation (required)
3. OutboundOrder MUST link to Shipment (optional, populated after packing)
4. OutboundOrder Type: SALES (from SalesOrder), TRANSFER (inter-warehouse), PRODUCTION_RETURN (return to supplier)
5. OutboundOrderLine MUST track: Qty (ordered), PickedQty, ShippedQty
6. Shipment MUST have unique ShipmentNumber (auto-generated: SHIP-0001, SHIP-0002, ...)
7. Shipment MUST link to OutboundOrder (1:1 relationship for Phase 1.5)
8. Shipment Carrier: enum (FEDEX, UPS, DHL, USPS, OTHER)
9. Shipment TrackingNumber: nullable (populated after carrier API call or manual entry)
10. State machine transitions validated (e.g., cannot dispatch PACKING shipment)

**Non-Functional:**
1. OrderNumber generation: thread-safe (DB sequence or GUID-based)
2. ShipmentNumber generation: thread-safe (DB sequence or GUID-based)
3. Audit fields: CreatedBy, CreatedAt, UpdatedBy, UpdatedAt (all entities)
4. Soft delete: IsDeleted flag (retain for audit)
5. Indexes: status, order date, shipment date (for fast queries)
6. Relationships: OutboundOrder → Shipment (1:1), OutboundOrder → Reservation (1:1)

**Data Model:**
```csharp
// OutboundOrder (state-based aggregate)
public class OutboundOrder
{
  public Guid Id { get; set; }
  public string OrderNumber { get; set; } // Auto-generated: OUT-0001
  public OutboundOrderType Type { get; set; } // SALES, TRANSFER, PRODUCTION_RETURN
  public OutboundOrderStatus Status { get; set; }
  public DateTime OrderDate { get; set; }
  public DateTime? RequestedShipDate { get; set; }
  public DateTime? PickedAt { get; set; }
  public DateTime? PackedAt { get; set; }
  public DateTime? ShippedAt { get; set; }
  public DateTime? DeliveredAt { get; set; }
  public List<OutboundOrderLine> Lines { get; set; }
  public Guid ReservationId { get; set; } // Link to reservation (HARD lock)
  public Guid? ShipmentId { get; set; } // Link to shipment (populated after packing)
  public Guid? SalesOrderId { get; set; } // Optional: link to sales order (if Type=SALES)

  // Navigation
  public Shipment Shipment { get; set; }

  // Audit
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
  public string UpdatedBy { get; set; }
  public DateTime UpdatedAt { get; set; }
  public bool IsDeleted { get; set; }
  public byte[] RowVersion { get; set; } // Optimistic locking

  // State machine methods
  public Result StartPicking() { /* ALLOCATED → PICKING */ }
  public Result CompletePicking() { /* PICKING → PICKED */ }
  public Result Pack() { /* PICKED → PACKED */ }
  public Result Ship() { /* PACKED → SHIPPED */ }
  public Result ConfirmDelivery() { /* SHIPPED → DELIVERED */ }
  public Result Cancel() { /* any status → CANCELLED */ }
}

public class OutboundOrderLine
{
  public Guid Id { get; set; }
  public Guid OutboundOrderId { get; set; }
  public Guid ItemId { get; set; }
  public decimal Qty { get; set; } // Ordered qty
  public decimal PickedQty { get; set; } // Picked qty (updated during picking)
  public decimal ShippedQty { get; set; } // Shipped qty (updated after packing)

  // Navigation
  public OutboundOrder OutboundOrder { get; set; }
  public Item Item { get; set; }
}

// Shipment (state-based aggregate)
public class Shipment
{
  public Guid Id { get; set; }
  public string ShipmentNumber { get; set; } // Auto-generated: SHIP-0001
  public Guid OutboundOrderId { get; set; } // 1:1 relationship
  public Carrier Carrier { get; set; } // FEDEX, UPS, DHL, USPS, OTHER
  public string TrackingNumber { get; set; } // Nullable: populated after carrier API call
  public ShipmentStatus Status { get; set; }
  public DateTime? PackedAt { get; set; }
  public DateTime? DispatchedAt { get; set; }
  public DateTime? InTransitAt { get; set; }
  public DateTime? DeliveredAt { get; set; }
  public string DeliverySignature { get; set; } // Proof of delivery
  public string DeliveryPhotoUrl { get; set; } // Blob storage URL
  public List<ShipmentLine> Lines { get; set; }
  public Guid? ShippingHandlingUnitId { get; set; } // Link to shipping HU (box/pallet)

  // Navigation
  public OutboundOrder OutboundOrder { get; set; }

  // Audit
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
  public string UpdatedBy { get; set; }
  public DateTime UpdatedAt { get; set; }
  public bool IsDeleted { get; set; }
  public byte[] RowVersion { get; set; } // Optimistic locking

  // State machine methods
  public Result Pack() { /* PACKING → PACKED */ }
  public Result Dispatch() { /* PACKED → DISPATCHED */ }
  public Result MarkInTransit() { /* DISPATCHED → IN_TRANSIT */ }
  public Result ConfirmDelivery(string signature, string photoUrl) { /* IN_TRANSIT → DELIVERED */ }
  public Result Cancel() { /* any status → CANCELLED */ }
}

public class ShipmentLine
{
  public Guid Id { get; set; }
  public Guid ShipmentId { get; set; }
  public Guid ItemId { get; set; }
  public decimal Qty { get; set; }
  public Guid? HandlingUnitId { get; set; } // Link to HU (if packed in box/pallet)

  // Navigation
  public Shipment Shipment { get; set; }
  public Item Item { get; set; }
}

// Enums
public enum OutboundOrderType { SALES, TRANSFER, PRODUCTION_RETURN }
public enum OutboundOrderStatus {
  DRAFT,       // Order created, not yet allocated
  ALLOCATED,   // Stock allocated (HARD lock)
  PICKING,     // Picking in progress
  PICKED,      // All items picked
  PACKED,      // Packed, ready to ship
  SHIPPED,     // Dispatched to customer
  DELIVERED,   // Delivered to customer
  CANCELLED    // Order cancelled
}
public enum ShipmentStatus {
  PACKING,     // Packing in progress
  PACKED,      // Packed, ready to dispatch
  DISPATCHED,  // Dispatched to carrier
  IN_TRANSIT,  // In transit (carrier update)
  DELIVERED,   // Delivered to customer
  CANCELLED    // Shipment cancelled
}
public enum Carrier { FEDEX, UPS, DHL, USPS, OTHER }
```

**Database Schema:**
```sql
CREATE TABLE outbound_orders (
  id UUID PRIMARY KEY,
  order_number VARCHAR(50) NOT NULL UNIQUE,
  type VARCHAR(50) NOT NULL, -- SALES, TRANSFER, PRODUCTION_RETURN
  status VARCHAR(50) NOT NULL,
  order_date TIMESTAMPTZ NOT NULL,
  requested_ship_date TIMESTAMPTZ,
  picked_at TIMESTAMPTZ,
  packed_at TIMESTAMPTZ,
  shipped_at TIMESTAMPTZ,
  delivered_at TIMESTAMPTZ,
  reservation_id UUID NOT NULL,
  shipment_id UUID,
  sales_order_id UUID,
  created_by VARCHAR(200) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by VARCHAR(200) NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  row_version BYTEA NOT NULL,
  INDEX idx_outbound_orders_status (status),
  INDEX idx_outbound_orders_order_date (order_date),
  INDEX idx_outbound_orders_reservation_id (reservation_id),
  INDEX idx_outbound_orders_sales_order_id (sales_order_id)
);

CREATE TABLE outbound_order_lines (
  id UUID PRIMARY KEY,
  outbound_order_id UUID NOT NULL REFERENCES outbound_orders(id),
  item_id UUID NOT NULL,
  qty DECIMAL(18,3) NOT NULL,
  picked_qty DECIMAL(18,3) NOT NULL DEFAULT 0,
  shipped_qty DECIMAL(18,3) NOT NULL DEFAULT 0,
  INDEX idx_outbound_order_lines_outbound_order_id (outbound_order_id)
);

CREATE TABLE shipments (
  id UUID PRIMARY KEY,
  shipment_number VARCHAR(50) NOT NULL UNIQUE,
  outbound_order_id UUID NOT NULL REFERENCES outbound_orders(id),
  carrier VARCHAR(50) NOT NULL, -- FEDEX, UPS, DHL, USPS, OTHER
  tracking_number VARCHAR(200),
  status VARCHAR(50) NOT NULL,
  packed_at TIMESTAMPTZ,
  dispatched_at TIMESTAMPTZ,
  in_transit_at TIMESTAMPTZ,
  delivered_at TIMESTAMPTZ,
  delivery_signature VARCHAR(500),
  delivery_photo_url VARCHAR(1000),
  shipping_handling_unit_id UUID,
  created_by VARCHAR(200) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by VARCHAR(200) NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  row_version BYTEA NOT NULL,
  INDEX idx_shipments_status (status),
  INDEX idx_shipments_outbound_order_id (outbound_order_id),
  INDEX idx_shipments_tracking_number (tracking_number)
);

CREATE TABLE shipment_lines (
  id UUID PRIMARY KEY,
  shipment_id UUID NOT NULL REFERENCES shipments(id),
  item_id UUID NOT NULL,
  qty DECIMAL(18,3) NOT NULL,
  handling_unit_id UUID,
  INDEX idx_shipment_lines_shipment_id (shipment_id)
);
```

### Acceptance Criteria

```gherkin
Scenario: Create outbound order from sales order
  Given sales order "SO-0001" with status ALLOCATED
  And reservation "RES-0001" exists with HARD lock
  When outbound order created with type SALES
  Then OutboundOrder "OUT-0001" created with status ALLOCATED
  And OutboundOrder.ReservationId = RES-0001
  And OutboundOrder.SalesOrderId = SO-0001
  And OutboundOrderLines match SalesOrderLines

Scenario: State transition - start picking
  Given OutboundOrder "OUT-0001" with status ALLOCATED
  When StartPicking() called
  Then status updated to PICKING
  And PickedAt timestamp populated
  And state transition logged

Scenario: State transition - pack order
  Given OutboundOrder "OUT-0001" with status PICKED
  When Pack() called
  Then status updated to PACKED
  And PackedAt timestamp populated
  And Shipment "SHIP-0001" created with status PACKED

Scenario: State transition - dispatch shipment
  Given Shipment "SHIP-0001" with status PACKED
  When Dispatch() called
  Then status updated to DISPATCHED
  And DispatchedAt timestamp populated
  And OutboundOrder.Status updated to SHIPPED

Scenario: State transition - confirm delivery
  Given Shipment "SHIP-0001" with status IN_TRANSIT
  When ConfirmDelivery(signature, photoUrl) called
  Then status updated to DELIVERED
  And DeliveredAt timestamp populated
  And DeliverySignature and DeliveryPhotoUrl stored
  And OutboundOrder.Status updated to DELIVERED

Scenario: Invalid state transition rejected
  Given OutboundOrder "OUT-0001" with status CANCELLED
  When StartPicking() called
  Then error returned: "Cannot start picking cancelled order"
  And status remains CANCELLED
  And NO state change logged
```

### Implementation Notes

- Use EF Core for entity mapping (OutboundOrderConfiguration, ShipmentConfiguration)
- State machine methods: validate current status before transition (throw exception if invalid)
- OrderNumber/ShipmentNumber generation: use DB sequence or GUID-based (e.g., OUT-{timestamp}-{random})
- Optimistic locking: add RowVersion column (EF Core concurrency token)
- Soft delete: global query filter (IsDeleted = false)
- Audit fields: auto-populated via SaveChanges interceptor (from Phase 1)
- Relationships: OutboundOrder → Shipment (1:1), OutboundOrder → Reservation (1:1)

### Validation / Checks

**Local Testing:**
```bash
# Generate migration
dotnet ef migrations add AddOutboundOrderAndShipment --project src/LKvitai.MES.Api

# Apply migration
dotnet ef database update --project src/LKvitai.MES.Api

# Verify schema
psql -d warehouse -c "\d outbound_orders"
psql -d warehouse -c "\d shipments"

# Run tests
dotnet test --filter "Category=OutboundOrders"
```

**Metrics:**
- N/A (entity layer, no metrics needed)

**Logs:**
- INFO: "OutboundOrder {OrderNumber} created, type {Type}, reservation {ReservationId}"
- INFO: "OutboundOrder {OrderNumber} state transition: {FromStatus} → {ToStatus}"
- INFO: "Shipment {ShipmentNumber} created for order {OrderNumber}"

**Backwards Compatibility:**
- New tables, no breaking changes
- New entities, no impact on existing code

### Definition of Done

- [ ] OutboundOrder entity class created (OutboundOrder.cs)
- [ ] OutboundOrderLine entity class created
- [ ] Shipment entity class created (Shipment.cs)
- [ ] ShipmentLine entity class created
- [ ] Enums defined (OutboundOrderType, OutboundOrderStatus, ShipmentStatus, Carrier)
- [ ] EF Core configuration created (OutboundOrderConfiguration.cs, ShipmentConfiguration.cs)
- [ ] State machine methods implemented (StartPicking, CompletePicking, Pack, Ship, ConfirmDelivery, Cancel)
- [ ] Migration generated (AddOutboundOrderAndShipment)
- [ ] Migration applied to local DB
- [ ] Schema verified (tables, indexes, constraints)
- [ ] Unit tests: entity creation, validation, relationships, state machine transitions (20+ tests)
- [ ] Soft delete tested (global query filter)
- [ ] Audit fields tested (auto-populated)
- [ ] Optimistic locking tested (concurrency conflicts)
- [ ] Code review completed
- [ ] Documentation: entity diagram added to docs/

---
## Task PRD-1507: Packing MVP - Consolidate from SHIPPING to Shipment/HUs + ShipmentPacked Event

**Epic:** A - Outbound/Shipment  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1506 (OutboundOrder + Shipment entities)  
**SourceRefs:** Universe §4.Epic A (Packing Station, Commands/APIs)

### Context

- Phase 1 has picking workflow but no packing step
- Need packing station workflow: scan order → verify items → select packaging → pack → generate label
- Packing creates Shipment entity and shipping HandlingUnit (consolidate picked items)
- Stock moved from PICKING_STAGING → SHIPPING location
- ShipmentPacked event emitted for downstream consumers (label printing, dispatch)
- Barcode validation: ensure scanned items match order lines

### Scope

**In Scope:**
- PackOrderCommand + handler (validate items, create shipment, create shipping HU, emit events)
- API endpoint: POST /api/warehouse/v1/outbound/orders/{id}/pack
- Validation: all order items scanned, barcode match, quantity match
- Create Shipment (status: PACKED)
- Create shipping HandlingUnit (type: BOX or PALLET)
- Emit StockMoved events (PICKING_STAGING → SHIPPING)
- Emit ShipmentPacked event
- Update OutboundOrder.Status = PACKED
- Idempotency (CommandId)

**Out of Scope:**
- Label printing (separate task, listens to ShipmentPacked event)
- Carrier API integration (PRD-1508)
- Multi-parcel shipments (1 order = 1 shipment)
- Packing UI (PRD-1510)

### Requirements

**Functional:**
1. POST /api/warehouse/v1/outbound/orders/{id}/pack: Pack order
2. Request includes: CommandId, scanned items (barcode, qty), packaging type (BOX, PALLET)
3. Validate: all order lines scanned, barcode matches item, qty matches order line
4. Create Shipment (status: PACKED, PackedAt timestamp)
5. Create shipping HandlingUnit (type: BOX or PALLET, location: SHIPPING)
6. Emit StockMoved events (PICKING_STAGING → SHIPPING) for each item
7. Emit ShipmentPacked event (includes shipment ID, order ID, items)
8. Update OutboundOrder.Status = PACKED, PackedAt timestamp
9. Return shipment details (shipment number, HU ID, label preview URL)

**Non-Functional:**
1. API latency: < 2 seconds (95th percentile)
2. Idempotency: duplicate CommandId returns cached result
3. Validation errors: return 400 Bad Request with detailed error messages
4. Authorization: Packing Operator role required
5. Transactional: all DB changes in single transaction (shipment, HU, stock moves, order update)

**Data Model (DTOs):**
```csharp
// Request DTO
public record PackOrderRequest(
  Guid CommandId,
  List<ScannedItemDto> ScannedItems,
  PackagingType PackagingType // BOX, PALLET
);

public record ScannedItemDto(
  string Barcode,
  decimal Qty
);

public enum PackagingType { BOX, PALLET }

// Response DTO
public record PackOrderResponse(
  Guid ShipmentId,
  string ShipmentNumber,
  Guid HandlingUnitId,
  string HandlingUnitCode,
  string LabelPreviewUrl // URL to label PDF (generated by label service)
);
```

**API Contract:**
```
POST /api/warehouse/v1/outbound/orders/{id}/pack
Request: PackOrderRequest
Response: 200 OK, PackOrderResponse
Errors:
  - 400 Bad Request: validation failure (missing items, barcode mismatch, qty mismatch)
  - 404 Not Found: order not found
  - 409 Conflict: order already packed (idempotency check)
  - 500 Internal Server Error: stock move failed, HU creation failed
```

**Events:**
```csharp
public record ShipmentPacked(
  Guid ShipmentId,
  string ShipmentNumber,
  Guid OutboundOrderId,
  string OutboundOrderNumber,
  Guid HandlingUnitId,
  string HandlingUnitCode,
  PackagingType PackagingType,
  List<ShipmentLineDto> Lines,
  DateTime PackedAt,
  string PackedBy,
  string SchemaVersion = "v1"
);

public record ShipmentLineDto(
  Guid ItemId,
  string ItemSku,
  decimal Qty
);
```

### Acceptance Criteria

```gherkin
Scenario: Pack order successfully
  Given OutboundOrder "OUT-0001" with status PICKED
  And order lines: [{ itemId: ITEM-001, qty: 10 }, { itemId: ITEM-002, qty: 5 }]
  And items in PICKING_STAGING location
  When POST /api/warehouse/v1/outbound/orders/OUT-0001/pack with:
    | scannedItems | [{ barcode: "BC-001", qty: 10 }, { barcode: "BC-002", qty: 5 }] |
    | packagingType | BOX |
  Then response status: 200 OK
  And response body includes: shipmentNumber "SHIP-0001", handlingUnitCode "HU-SHIP-0001"
  And Shipment created with status PACKED
  And HandlingUnit created with type BOX, location SHIPPING
  And StockMoved events emitted (PICKING_STAGING → SHIPPING) for ITEM-001, ITEM-002
  And ShipmentPacked event emitted
  And OutboundOrder.Status updated to PACKED

Scenario: Validation failure - missing item
  Given OutboundOrder "OUT-0001" with 2 order lines
  When POST /api/warehouse/v1/outbound/orders/OUT-0001/pack with only 1 scanned item
  Then response status: 400 Bad Request
  And error message: "Missing items: ITEM-002 not scanned"
  And NO shipment created
  And NO events emitted
  And OutboundOrder.Status remains PICKED

Scenario: Validation failure - barcode mismatch
  Given OutboundOrder "OUT-0001" with order line: { itemId: ITEM-001, qty: 10 }
  And item ITEM-001 has barcode "BC-001"
  When POST /api/warehouse/v1/outbound/orders/OUT-0001/pack with scanned barcode "BC-999"
  Then response status: 400 Bad Request
  And error message: "Barcode BC-999 does not match any order item"
  And NO shipment created

Scenario: Validation failure - quantity mismatch
  Given OutboundOrder "OUT-0001" with order line: { itemId: ITEM-001, qty: 10 }
  When POST /api/warehouse/v1/outbound/orders/OUT-0001/pack with scanned qty: 5
  Then response status: 400 Bad Request
  And error message: "Quantity mismatch for ITEM-001: expected 10, scanned 5"
  And NO shipment created

Scenario: Idempotency - duplicate pack request
  Given OutboundOrder "OUT-0001" already packed with CommandId "cmd-123"
  And Shipment "SHIP-0001" exists
  When POST /api/warehouse/v1/outbound/orders/OUT-0001/pack with same CommandId "cmd-123"
  Then response status: 200 OK
  And response body includes: shipmentNumber "SHIP-0001" (cached result)
  And NO new shipment created
  And NO events emitted
  And response header: X-Idempotent-Replay: true

Scenario: Invalid state - order not picked
  Given OutboundOrder "OUT-0001" with status ALLOCATED (not PICKED)
  When POST /api/warehouse/v1/outbound/orders/OUT-0001/pack
  Then response status: 400 Bad Request
  And error message: "Cannot pack order in status ALLOCATED, must be PICKED"
  And NO shipment created
```

### Implementation Notes

- Use MediatR for PackOrderCommandHandler
- Validation: compare scanned items with OutboundOrderLines (barcode → ItemId lookup)
- Barcode lookup: query items table (item.Barcode = scannedItem.Barcode)
- Create Shipment: use OutboundOrder.Pack() method (from PRD-1506)
- Create HandlingUnit: call CreateHandlingUnitCommand (from Phase 1)
- Stock moves: emit StockMoved events (PICKING_STAGING → SHIPPING) via event sourcing
- Event publishing: use MassTransit IPublishEndpoint
- Transactional: wrap all DB changes in single EF Core transaction
- Label preview URL: placeholder for now (label service listens to ShipmentPacked event)

### Validation / Checks

**Local Testing:**
```bash
# Pack order
curl -X POST http://localhost:5000/api/warehouse/v1/outbound/orders/<id>/pack \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "commandId": "test-pack-001",
    "scannedItems": [
      { "barcode": "BC-001", "qty": 10 },
      { "barcode": "BC-002", "qty": 5 }
    ],
    "packagingType": "BOX"
  }'

# Verify shipment created
psql -d warehouse -c "SELECT * FROM shipments WHERE outbound_order_id = '<id>';"

# Verify HU created
psql -d warehouse -c "SELECT * FROM handling_units WHERE type = 'BOX' AND location_code = 'SHIPPING';"

# Run tests
dotnet test --filter "Category=Packing"
```

**Metrics:**
- `packing_operations_total` (counter, labels: packaging_type)
- `packing_duration_ms` (histogram)
- `packing_validation_errors_total` (counter, labels: error_type)
- `shipments_packed_total` (counter)

**Logs:**
- INFO: "Packing order {OrderNumber}, scanned {ItemCount} items"
- INFO: "Shipment {ShipmentNumber} created, HU {HandlingUnitCode}, packaging {PackagingType}"
- WARN: "Packing validation failed: {ErrorMessage}"
- ERROR: "Packing failed: {Exception}"

**Backwards Compatibility:**
- New API endpoint, no breaking changes
- New event (ShipmentPacked), no existing consumers

### Definition of Done

- [ ] PackOrderCommand + handler implemented
- [ ] PackingController created with POST /pack endpoint
- [ ] DTOs defined (PackOrderRequest, PackOrderResponse, ScannedItemDto)
- [ ] Validation logic implemented (barcode match, qty match, all items scanned)
- [ ] Shipment creation integrated (call OutboundOrder.Pack())
- [ ] HandlingUnit creation integrated (call CreateHandlingUnitCommand)
- [ ] StockMoved events emitted (PICKING_STAGING → SHIPPING)
- [ ] ShipmentPacked event defined and published
- [ ] OutboundOrder.Status updated to PACKED
- [ ] Idempotency middleware applied (CommandId check)
- [ ] Authorization policy applied (Packing Operator role)
- [ ] Unit tests: 15+ scenarios (pack success, validation failures, idempotency)
- [ ] Integration tests: end-to-end pack workflow (API → DB → events)
- [ ] Metrics exposed (counters, histograms)
- [ ] Logs added (INFO, WARN, ERROR with correlation IDs)
- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] Code review completed
- [ ] Manual testing: Postman collection with pack endpoint

---
## Task PRD-1508: Dispatch MVP - Mark Shipment as Dispatched + ShipmentDispatched Event + Audit

**Epic:** A - Outbound/Shipment  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1507 (Packing MVP)  
**SourceRefs:** Universe §4.Epic A (Dispatch Confirmation, Carrier API Integration)

### Context

- PRD-1507 created Shipment entity with status PACKED
- Need dispatch workflow: confirm shipment loaded onto carrier vehicle
- Dispatch updates Shipment.Status = DISPATCHED, DispatchedAt timestamp
- Optionally integrate with carrier API (FedEx, UPS) to generate tracking number
- ShipmentDispatched event emitted for downstream consumers (ERP, customer notification)
- Audit trail: who dispatched, when, carrier, vehicle ID

### Scope

**In Scope:**
- DispatchShipmentCommand + handler (validate status, update shipment, emit event)
- API endpoint: POST /api/warehouse/v1/shipments/{id}/dispatch
- Validation: Shipment.Status = PACKED
- Update Shipment.Status = DISPATCHED, DispatchedAt timestamp
- Carrier API integration (optional, with retry logic and manual fallback)
- Emit ShipmentDispatched event
- Update OutboundOrder.Status = SHIPPED
- Audit: DispatchedBy, Carrier, VehicleId, DispatchedAt
- Idempotency (CommandId)

**Out of Scope:**
- Label printing (handled by label service, listens to ShipmentPacked event)
- Proof of delivery (separate task, carrier webhook)
- Multi-carrier support (FedEx only for Phase 1.5, others in Phase 2)
- Dispatch UI (PRD-1510)

### Requirements

**Functional:**
1. POST /api/warehouse/v1/shipments/{id}/dispatch: Dispatch shipment
2. Request includes: CommandId, Carrier, VehicleId (optional), DispatchTime (default: now)
3. Validate: Shipment.Status = PACKED
4. If carrier API enabled: call carrier API to generate tracking number (with retry)
5. If carrier API fails: allow manual tracking number entry (fallback)
6. Update Shipment.Status = DISPATCHED, DispatchedAt timestamp, Carrier, TrackingNumber
7. Emit ShipmentDispatched event (includes shipment ID, tracking number, carrier)
8. Update OutboundOrder.Status = SHIPPED, ShippedAt timestamp
9. Return shipment details (shipment number, tracking number, carrier)

**Non-Functional:**
1. API latency: < 1 second (95th percentile, excluding carrier API call)
2. Carrier API latency: < 5 seconds (with 3 retries)
3. Idempotency: duplicate CommandId returns cached result
4. Validation errors: return 400 Bad Request with detailed error messages
5. Authorization: Dispatch Clerk role required
6. Transactional: all DB changes in single transaction (shipment, order update)

**Data Model (DTOs):**
```csharp
// Request DTO
public record DispatchShipmentRequest(
  Guid CommandId,
  Carrier Carrier, // FEDEX, UPS, DHL, USPS, OTHER
  string VehicleId, // Optional: truck plate or van number
  DateTime? DispatchTime, // Optional: default now
  string ManualTrackingNumber // Optional: manual entry if carrier API fails
);

// Response DTO
public record DispatchShipmentResponse(
  Guid ShipmentId,
  string ShipmentNumber,
  Carrier Carrier,
  string TrackingNumber,
  DateTime DispatchedAt,
  string DispatchedBy
);
```

**API Contract:**
```
POST /api/warehouse/v1/shipments/{id}/dispatch
Request: DispatchShipmentRequest
Response: 200 OK, DispatchShipmentResponse
Errors:
  - 400 Bad Request: validation failure (invalid status, carrier required)
  - 404 Not Found: shipment not found
  - 409 Conflict: shipment already dispatched (idempotency check)
  - 500 Internal Server Error: carrier API failed (after retries)
```

**Events:**
```csharp
public record ShipmentDispatched(
  Guid ShipmentId,
  string ShipmentNumber,
  Guid OutboundOrderId,
  string OutboundOrderNumber,
  Carrier Carrier,
  string TrackingNumber,
  string VehicleId,
  DateTime DispatchedAt,
  string DispatchedBy,
  bool ManualTracking, // True if tracking number entered manually
  string SchemaVersion = "v1"
);
```

**Carrier API Integration (Optional):**
```csharp
// Carrier API service interface
public interface ICarrierApiService
{
  Task<Result<string>> GenerateTrackingNumberAsync(
    Guid shipmentId,
    Carrier carrier,
    Address destinationAddress,
    List<ShipmentLineDto> items,
    CancellationToken cancellationToken
  );
}

// FedEx API implementation (example)
public class FedExApiService : ICarrierApiService
{
  public async Task<Result<string>> GenerateTrackingNumberAsync(...)
  {
    // Call FedEx API: POST /shipments/create
    // Include idempotency key: shipmentId
    // Retry 3x with exponential backoff (1s, 2s, 4s)
    // Return tracking number or error
  }
}
```

### Acceptance Criteria

```gherkin
Scenario: Dispatch shipment successfully with carrier API
  Given Shipment "SHIP-0001" with status PACKED
  And carrier API (FedEx) is available
  When POST /api/warehouse/v1/shipments/SHIP-0001/dispatch with:
    | carrier | FEDEX |
    | vehicleId | VAN-042 |
  Then response status: 200 OK
  And response body includes: trackingNumber "1Z999AA1234567890" (from carrier API)
  And Shipment.Status updated to DISPATCHED
  And Shipment.DispatchedAt timestamp populated
  And Shipment.TrackingNumber = "1Z999AA1234567890"
  And ShipmentDispatched event emitted
  And OutboundOrder.Status updated to SHIPPED

Scenario: Dispatch shipment with manual tracking (carrier API unavailable)
  Given Shipment "SHIP-0002" with status PACKED
  And carrier API (FedEx) is unavailable (returns 503)
  When POST /api/warehouse/v1/shipments/SHIP-0002/dispatch with:
    | carrier | FEDEX |
    | manualTrackingNumber | 1Z999AA9876543210 |
  Then response status: 200 OK
  And response body includes: trackingNumber "1Z999AA9876543210" (manual entry)
  And Shipment.Status updated to DISPATCHED
  And ShipmentDispatched event emitted with ManualTracking=true
  And OutboundOrder.Status updated to SHIPPED

Scenario: Carrier API retry logic
  Given Shipment "SHIP-0003" with status PACKED
  And carrier API (FedEx) returns 503 on first 2 calls, 200 on 3rd call
  When POST /api/warehouse/v1/shipments/SHIP-0003/dispatch
  Then system retries 3 times (1s, 2s, 4s delays)
  And 3rd call succeeds
  And response status: 200 OK
  And tracking number returned from carrier API

Scenario: Validation failure - invalid status
  Given Shipment "SHIP-0001" with status DISPATCHED (already dispatched)
  When POST /api/warehouse/v1/shipments/SHIP-0001/dispatch
  Then response status: 400 Bad Request
  And error message: "Cannot dispatch shipment in status DISPATCHED, must be PACKED"
  And NO events emitted

Scenario: Idempotency - duplicate dispatch request
  Given Shipment "SHIP-0001" already dispatched with CommandId "cmd-456"
  When POST /api/warehouse/v1/shipments/SHIP-0001/dispatch with same CommandId "cmd-456"
  Then response status: 200 OK
  And response body includes: trackingNumber (cached result)
  And NO new dispatch performed
  And NO events emitted
  And response header: X-Idempotent-Replay: true

Scenario: Audit trail captured
  Given Shipment "SHIP-0001" with status PACKED
  When POST /api/warehouse/v1/shipments/SHIP-0001/dispatch by user "john.doe"
  Then Shipment.DispatchedBy = "john.doe"
  And Shipment.DispatchedAt = current timestamp
  And ShipmentDispatched event includes: DispatchedBy="john.doe", DispatchedAt
  And audit log entry created
```

### Implementation Notes

- Use MediatR for DispatchShipmentCommandHandler
- Validation: check Shipment.Status = PACKED (use Shipment.Dispatch() method from PRD-1506)
- Carrier API: inject ICarrierApiService (FedExApiService for Phase 1.5)
- Retry logic: use Polly library (3 retries, exponential backoff: 1s, 2s, 4s)
- Manual fallback: if carrier API fails after retries, use ManualTrackingNumber from request
- Event publishing: use MassTransit IPublishEndpoint
- Transactional: wrap all DB changes in single EF Core transaction
- Authorization: use [Authorize(Policy = WarehousePolicies.DispatchClerk)] attribute

### Validation / Checks

**Local Testing:**
```bash
# Dispatch shipment
curl -X POST http://localhost:5000/api/warehouse/v1/shipments/<id>/dispatch \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "commandId": "test-dispatch-001",
    "carrier": "FEDEX",
    "vehicleId": "VAN-042"
  }'

# Verify shipment dispatched
psql -d warehouse -c "SELECT * FROM shipments WHERE id = '<id>';"

# Verify OutboundOrder updated
psql -d warehouse -c "SELECT status FROM outbound_orders WHERE shipment_id = '<id>';"

# Run tests
dotnet test --filter "Category=Dispatch"
```

**Metrics:**
- `dispatch_operations_total` (counter, labels: carrier)
- `dispatch_duration_ms` (histogram)
- `carrier_api_calls_total` (counter, labels: carrier, status)
- `carrier_api_latency_ms` (histogram, labels: carrier)
- `carrier_api_errors_total` (counter, labels: carrier, error_type)
- `manual_tracking_entries_total` (counter)

**Logs:**
- INFO: "Dispatching shipment {ShipmentNumber}, carrier {Carrier}, vehicle {VehicleId}"
- INFO: "Carrier API call succeeded: tracking {TrackingNumber}"
- WARN: "Carrier API call failed (attempt {AttemptNumber}/3): {ErrorMessage}"
- WARN: "Carrier API unavailable after 3 retries, using manual tracking"
- ERROR: "Dispatch failed: {Exception}"

**Backwards Compatibility:**
- New API endpoint, no breaking changes
- New event (ShipmentDispatched), no existing consumers

### Definition of Done

- [ ] DispatchShipmentCommand + handler implemented
- [ ] ShipmentsController created with POST /dispatch endpoint
- [ ] DTOs defined (DispatchShipmentRequest, DispatchShipmentResponse)
- [ ] Validation logic implemented (status check)
- [ ] ICarrierApiService interface defined
- [ ] FedExApiService implemented (with retry logic using Polly)
- [ ] Manual tracking fallback implemented
- [ ] Shipment.Status updated to DISPATCHED
- [ ] OutboundOrder.Status updated to SHIPPED
- [ ] ShipmentDispatched event defined and published
- [ ] Idempotency middleware applied (CommandId check)
- [ ] Authorization policy applied (Dispatch Clerk role)
- [ ] Unit tests: 15+ scenarios (dispatch success, carrier API failure, retry, manual fallback, idempotency)
- [ ] Integration tests: end-to-end dispatch workflow (API → carrier API → DB → events)
- [ ] Metrics exposed (counters, histograms for carrier API)
- [ ] Logs added (INFO, WARN, ERROR with correlation IDs)
- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] Code review completed
- [ ] Manual testing: Postman collection with dispatch endpoint

---
## Task PRD-1509: Projections - OutboundOrderSummary + ShipmentSummary + DispatchHistory

**Epic:** A - Outbound/Shipment  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-1507 (Packing), PRD-1508 (Dispatch)  
**SourceRefs:** Universe §4.Epic A (Reporting & Exports)

### Context

- PRD-1507 and PRD-1508 emit events (ShipmentPacked, ShipmentDispatched)
- Need read-optimized projections for UI and reporting
- OutboundOrderSummary: aggregated view (order count by status, customer, date range)
- ShipmentSummary: shipment details with tracking info (for dispatch dashboard)
- DispatchHistory: audit log (who dispatched, when, carrier, vehicle)
- Projections consume events and update denormalized tables

### Scope

**In Scope:**
- OutboundOrderSummary projection (table: outbound_order_summary)
- ShipmentSummary projection (table: shipment_summary)
- DispatchHistory projection (table: dispatch_history)
- Event handlers: OutboundOrderCreated, ShipmentPacked, ShipmentDispatched
- Query endpoints: GET /outbound/orders/summary, GET /shipments/summary, GET /dispatch/history
- Projection rebuild support (replay events from event store)
- Projection lag monitoring (metrics)

**Out of Scope:**
- Complex analytics (deferred to Phase 2, use BI tool)
- Real-time dashboards (projections updated within 5 seconds, not real-time)
- Export to CSV/PDF (separate task)

### Requirements

**Functional:**
1. OutboundOrderSummary: count by status, customer, date range (for dashboard widgets)
2. ShipmentSummary: shipment details (order number, customer, carrier, tracking, status, timestamps)
3. DispatchHistory: audit log (shipment number, dispatched by, dispatched at, carrier, vehicle)
4. Event handlers consume events and update projection tables
5. Query endpoints return projection data (with filters: status, date range, customer)
6. Projection rebuild: replay events from event store (for schema changes or data fixes)
7. Projection lag: < 5 seconds (95th percentile)

**Non-Functional:**
1. Query latency: < 100ms (95th percentile, indexed queries)
2. Projection update latency: < 500ms per event
3. Projection rebuild time: < 5 minutes for 100k events
4. Idempotency: event handlers check event number (skip duplicates)
5. Error handling: failed events logged, retried (with exponential backoff)

**Data Model (Projections):**
```csharp
// OutboundOrderSummary (denormalized, read-optimized)
public class OutboundOrderSummary
{
  public Guid Id { get; set; }
  public string OrderNumber { get; set; }
  public OutboundOrderType Type { get; set; }
  public OutboundOrderStatus Status { get; set; }
  public string CustomerName { get; set; }
  public int ItemCount { get; set; }
  public DateTime OrderDate { get; set; }
  public DateTime? RequestedShipDate { get; set; }
  public DateTime? PackedAt { get; set; }
  public DateTime? ShippedAt { get; set; }
  public Guid? ShipmentId { get; set; }
  public string ShipmentNumber { get; set; }
  public string TrackingNumber { get; set; }
}

// ShipmentSummary (denormalized, read-optimized)
public class ShipmentSummary
{
  public Guid Id { get; set; }
  public string ShipmentNumber { get; set; }
  public Guid OutboundOrderId { get; set; }
  public string OutboundOrderNumber { get; set; }
  public string CustomerName { get; set; }
  public Carrier Carrier { get; set; }
  public string TrackingNumber { get; set; }
  public ShipmentStatus Status { get; set; }
  public DateTime? PackedAt { get; set; }
  public DateTime? DispatchedAt { get; set; }
  public DateTime? DeliveredAt { get; set; }
  public string PackedBy { get; set; }
  public string DispatchedBy { get; set; }
}

// DispatchHistory (audit log)
public class DispatchHistory
{
  public Guid Id { get; set; }
  public Guid ShipmentId { get; set; }
  public string ShipmentNumber { get; set; }
  public string OutboundOrderNumber { get; set; }
  public Carrier Carrier { get; set; }
  public string TrackingNumber { get; set; }
  public string VehicleId { get; set; }
  public DateTime DispatchedAt { get; set; }
  public string DispatchedBy { get; set; }
  public bool ManualTracking { get; set; }
}
```

**Database Schema:**
```sql
CREATE TABLE outbound_order_summary (
  id UUID PRIMARY KEY,
  order_number VARCHAR(50) NOT NULL,
  type VARCHAR(50) NOT NULL,
  status VARCHAR(50) NOT NULL,
  customer_name VARCHAR(200),
  item_count INT NOT NULL,
  order_date TIMESTAMPTZ NOT NULL,
  requested_ship_date TIMESTAMPTZ,
  packed_at TIMESTAMPTZ,
  shipped_at TIMESTAMPTZ,
  shipment_id UUID,
  shipment_number VARCHAR(50),
  tracking_number VARCHAR(200),
  INDEX idx_outbound_order_summary_status (status),
  INDEX idx_outbound_order_summary_order_date (order_date),
  INDEX idx_outbound_order_summary_customer_name (customer_name)
);

CREATE TABLE shipment_summary (
  id UUID PRIMARY KEY,
  shipment_number VARCHAR(50) NOT NULL,
  outbound_order_id UUID NOT NULL,
  outbound_order_number VARCHAR(50) NOT NULL,
  customer_name VARCHAR(200),
  carrier VARCHAR(50) NOT NULL,
  tracking_number VARCHAR(200),
  status VARCHAR(50) NOT NULL,
  packed_at TIMESTAMPTZ,
  dispatched_at TIMESTAMPTZ,
  delivered_at TIMESTAMPTZ,
  packed_by VARCHAR(200),
  dispatched_by VARCHAR(200),
  INDEX idx_shipment_summary_status (status),
  INDEX idx_shipment_summary_dispatched_at (dispatched_at),
  INDEX idx_shipment_summary_tracking_number (tracking_number)
);

CREATE TABLE dispatch_history (
  id UUID PRIMARY KEY,
  shipment_id UUID NOT NULL,
  shipment_number VARCHAR(50) NOT NULL,
  outbound_order_number VARCHAR(50) NOT NULL,
  carrier VARCHAR(50) NOT NULL,
  tracking_number VARCHAR(200),
  vehicle_id VARCHAR(100),
  dispatched_at TIMESTAMPTZ NOT NULL,
  dispatched_by VARCHAR(200) NOT NULL,
  manual_tracking BOOLEAN NOT NULL DEFAULT FALSE,
  INDEX idx_dispatch_history_dispatched_at (dispatched_at),
  INDEX idx_dispatch_history_shipment_id (shipment_id)
);
```

**Event Handlers:**
```csharp
// OutboundOrderSummary projection handler
public class OutboundOrderSummaryProjection :
  IConsumer<OutboundOrderCreated>,
  IConsumer<ShipmentPacked>,
  IConsumer<ShipmentDispatched>
{
  public async Task Consume(ConsumeContext<OutboundOrderCreated> context)
  {
    // Insert new row in outbound_order_summary
    var summary = new OutboundOrderSummary
    {
      Id = context.Message.Id,
      OrderNumber = context.Message.OrderNumber,
      Type = context.Message.Type,
      Status = OutboundOrderStatus.ALLOCATED,
      CustomerName = context.Message.CustomerName,
      ItemCount = context.Message.Lines.Count,
      OrderDate = context.Message.OrderDate,
      RequestedShipDate = context.Message.RequestedShipDate
    };
    await _dbContext.OutboundOrderSummaries.AddAsync(summary);
    await _dbContext.SaveChangesAsync();
  }

  public async Task Consume(ConsumeContext<ShipmentPacked> context)
  {
    // Update outbound_order_summary: status=PACKED, shipment info
    var summary = await _dbContext.OutboundOrderSummaries
      .FirstOrDefaultAsync(x => x.Id == context.Message.OutboundOrderId);
    if (summary != null)
    {
      summary.Status = OutboundOrderStatus.PACKED;
      summary.PackedAt = context.Message.PackedAt;
      summary.ShipmentId = context.Message.ShipmentId;
      summary.ShipmentNumber = context.Message.ShipmentNumber;
      await _dbContext.SaveChangesAsync();
    }
  }

  public async Task Consume(ConsumeContext<ShipmentDispatched> context)
  {
    // Update outbound_order_summary: status=SHIPPED, tracking number
    var summary = await _dbContext.OutboundOrderSummaries
      .FirstOrDefaultAsync(x => x.ShipmentId == context.Message.ShipmentId);
    if (summary != null)
    {
      summary.Status = OutboundOrderStatus.SHIPPED;
      summary.ShippedAt = context.Message.DispatchedAt;
      summary.TrackingNumber = context.Message.TrackingNumber;
      await _dbContext.SaveChangesAsync();
    }
  }
}

// ShipmentSummary projection handler
public class ShipmentSummaryProjection :
  IConsumer<ShipmentPacked>,
  IConsumer<ShipmentDispatched>
{
  public async Task Consume(ConsumeContext<ShipmentPacked> context)
  {
    // Insert new row in shipment_summary
    var summary = new ShipmentSummary
    {
      Id = context.Message.ShipmentId,
      ShipmentNumber = context.Message.ShipmentNumber,
      OutboundOrderId = context.Message.OutboundOrderId,
      OutboundOrderNumber = context.Message.OutboundOrderNumber,
      Status = ShipmentStatus.PACKED,
      PackedAt = context.Message.PackedAt,
      PackedBy = context.Message.PackedBy
    };
    await _dbContext.ShipmentSummaries.AddAsync(summary);
    await _dbContext.SaveChangesAsync();
  }

  public async Task Consume(ConsumeContext<ShipmentDispatched> context)
  {
    // Update shipment_summary: status=DISPATCHED, carrier, tracking
    var summary = await _dbContext.ShipmentSummaries
      .FirstOrDefaultAsync(x => x.Id == context.Message.ShipmentId);
    if (summary != null)
    {
      summary.Status = ShipmentStatus.DISPATCHED;
      summary.Carrier = context.Message.Carrier;
      summary.TrackingNumber = context.Message.TrackingNumber;
      summary.DispatchedAt = context.Message.DispatchedAt;
      summary.DispatchedBy = context.Message.DispatchedBy;
      await _dbContext.SaveChangesAsync();
    }
  }
}

// DispatchHistory projection handler
public class DispatchHistoryProjection : IConsumer<ShipmentDispatched>
{
  public async Task Consume(ConsumeContext<ShipmentDispatched> context)
  {
    // Insert new row in dispatch_history (audit log)
    var history = new DispatchHistory
    {
      Id = Guid.NewGuid(),
      ShipmentId = context.Message.ShipmentId,
      ShipmentNumber = context.Message.ShipmentNumber,
      OutboundOrderNumber = context.Message.OutboundOrderNumber,
      Carrier = context.Message.Carrier,
      TrackingNumber = context.Message.TrackingNumber,
      VehicleId = context.Message.VehicleId,
      DispatchedAt = context.Message.DispatchedAt,
      DispatchedBy = context.Message.DispatchedBy,
      ManualTracking = context.Message.ManualTracking
    };
    await _dbContext.DispatchHistories.AddAsync(history);
    await _dbContext.SaveChangesAsync();
  }
}
```

**Query Endpoints:**
```
GET /api/warehouse/v1/outbound/orders/summary?status=PACKED&dateFrom=2026-01-01&dateTo=2026-12-31
Response: 200 OK, OutboundOrderSummary[]

GET /api/warehouse/v1/shipments/summary?status=DISPATCHED&dateFrom=2026-01-01
Response: 200 OK, ShipmentSummary[]

GET /api/warehouse/v1/dispatch/history?dateFrom=2026-01-01&dateTo=2026-12-31
Response: 200 OK, DispatchHistory[]
```

### Acceptance Criteria

```gherkin
Scenario: OutboundOrderSummary projection updated on OutboundOrderCreated
  Given OutboundOrderCreated event emitted with:
    | id | OUT-001 |
    | orderNumber | OUT-0001 |
    | customerName | Acme Corp |
    | itemCount | 3 |
  When projection handler consumes event
  Then new row inserted in outbound_order_summary table
  And row includes: orderNumber "OUT-0001", status "ALLOCATED", customerName "Acme Corp"

Scenario: OutboundOrderSummary projection updated on ShipmentPacked
  Given OutboundOrderSummary row exists for order "OUT-0001"
  And ShipmentPacked event emitted with:
    | outboundOrderId | OUT-001 |
    | shipmentNumber | SHIP-0001 |
    | packedAt | 2026-02-10T10:00:00Z |
  When projection handler consumes event
  Then outbound_order_summary row updated: status "PACKED", shipmentNumber "SHIP-0001", packedAt timestamp

Scenario: ShipmentSummary projection created on ShipmentPacked
  Given ShipmentPacked event emitted
  When projection handler consumes event
  Then new row inserted in shipment_summary table
  And row includes: shipmentNumber, status "PACKED", packedAt, packedBy

Scenario: DispatchHistory projection created on ShipmentDispatched
  Given ShipmentDispatched event emitted with:
    | shipmentNumber | SHIP-0001 |
    | carrier | FEDEX |
    | trackingNumber | 1Z999AA1234567890 |
    | dispatchedBy | john.doe |
  When projection handler consumes event
  Then new row inserted in dispatch_history table
  And row includes: shipmentNumber "SHIP-0001", carrier "FEDEX", trackingNumber, dispatchedBy "john.doe"

Scenario: Projection lag monitoring
  Given ShipmentDispatched event emitted at T0
  When projection handler consumes event at T1
  Then projection lag = T1 - T0
  And lag metric recorded (histogram)
  And if lag > 5 seconds → alert triggered
```

### Implementation Notes

- Use MassTransit consumers for event handlers (IConsumer<TEvent>)
- Projection tables: separate DbContext (ProjectionsDbContext) or same as main DbContext
- Idempotency: MassTransit handles duplicate event detection (message deduplication)
- Projection rebuild: use Marten projection rebuild API (replay events from event store)
- Projection lag: measure time between event timestamp and projection update timestamp
- Error handling: if projection update fails, log error and retry (MassTransit retry policy)

### Validation / Checks

**Local Testing:**
```bash
# Emit test event (via API or test harness)
curl -X POST http://localhost:5000/api/warehouse/v1/outbound/orders/<id>/pack

# Verify projection updated
psql -d warehouse -c "SELECT * FROM outbound_order_summary WHERE order_number = 'OUT-0001';"
psql -d warehouse -c "SELECT * FROM shipment_summary WHERE shipment_number = 'SHIP-0001';"
psql -d warehouse -c "SELECT * FROM dispatch_history WHERE shipment_number = 'SHIP-0001';"

# Query projection endpoints
curl http://localhost:5000/api/warehouse/v1/outbound/orders/summary?status=PACKED
curl http://localhost:5000/api/warehouse/v1/shipments/summary?status=DISPATCHED
curl http://localhost:5000/api/warehouse/v1/dispatch/history

# Run tests
dotnet test --filter "Category=Projections"
```

**Metrics:**
- `projection_updates_total` (counter, labels: projection_name, event_type)
- `projection_update_duration_ms` (histogram, labels: projection_name)
- `projection_lag_seconds` (histogram, labels: projection_name)
- `projection_errors_total` (counter, labels: projection_name, error_type)

**Logs:**
- INFO: "Projection {ProjectionName} updated for event {EventType}, entity {EntityId}"
- WARN: "Projection lag high: {LagSeconds}s for {ProjectionName}"
- ERROR: "Projection update failed: {Exception}"

**Backwards Compatibility:**
- New projection tables, no breaking changes
- New query endpoints, no impact on existing APIs

### Definition of Done

- [ ] OutboundOrderSummary projection class created
- [ ] ShipmentSummary projection class created
- [ ] DispatchHistory projection class created
- [ ] Event handlers implemented (OutboundOrderSummaryProjection, ShipmentSummaryProjection, DispatchHistoryProjection)
- [ ] MassTransit consumers registered in Program.cs
- [ ] Projection tables created (migration: AddProjectionTables)
- [ ] Query endpoints implemented (GET /summary, GET /history)
- [ ] Projection rebuild support implemented (replay events)
- [ ] Unit tests: 15+ scenarios (projection updates, event handling, idempotency)
- [ ] Integration tests: end-to-end event → projection update
- [ ] Metrics exposed (counters, histograms for lag)
- [ ] Logs added (INFO, WARN, ERROR with correlation IDs)
- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] Code review completed
- [ ] Manual testing: emit events, verify projections updated

---
## Task PRD-1510: UI - Outbound Orders List + Order Detail + Packing Station + Dispatch Confirmation

**Epic:** A - Outbound/Shipment  
**Phase:** 1.5  
**Sprint:** 1  
**Estimate:** L (2 days)  
**OwnerType:** Frontend/UI  
**Dependencies:** PRD-1505 (SalesOrder APIs), PRD-1507 (Packing), PRD-1508 (Dispatch), PRD-1509 (Projections)  
**SourceRefs:** Universe §4.Epic A (UI/UX Pages), Universe §4.Epic B (UI/UX Pages)

### Context

- PRD-1505 to PRD-1509 created backend APIs and projections
- Need UI screens for outbound order management and fulfillment
- Outbound Orders List: view all orders, filter by status, customer, date
- Order Detail: view order header, lines, reservation, shipment info
- Packing Station: scan order, verify items, pack, generate label
- Dispatch Confirmation: confirm shipments loaded onto carrier vehicle
- UI framework: React (existing Phase 1 UI)

### Scope

**In Scope:**
- Outbound Orders List screen (`/warehouse/outbound/orders`)
- Order Detail screen (`/warehouse/outbound/orders/{id}`)
- Packing Station screen (`/warehouse/outbound/pack/{orderId}`)
- Dispatch Confirmation screen (`/warehouse/outbound/dispatch`)
- API integration (fetch orders, pack, dispatch)
- Form validation (client-side)
- Error handling (network errors, validation errors)
- Loading states (spinners, skeletons)

**Out of Scope:**
- Sales Orders UI (separate task, similar to Outbound Orders)
- Customer management UI (separate task)
- Label printing UI (label auto-prints, no preview UI for Phase 1.5)
- Real-time updates (polling every 30 seconds, no WebSockets)

### Requirements

**Functional:**
1. Outbound Orders List: table with filters (status, customer, date range), pagination
2. Order Detail: header (order number, customer, status, dates), lines table, actions (release, cancel)
3. Packing Station: scan order barcode, scan item barcodes, verify, select packaging, pack button
4. Dispatch Confirmation: table of packed shipments, dispatch modal (carrier, vehicle, dispatch button)
5. All screens: error handling (network errors, validation errors), loading states
6. All screens: responsive design (desktop, tablet)

**Non-Functional:**
1. Page load time: < 2 seconds (95th percentile)
2. API call latency: < 500ms (95th percentile)
3. Accessibility: WCAG 2.1 AA compliance (keyboard navigation, screen reader support)
4. Browser support: Chrome, Firefox, Safari, Edge (latest 2 versions)
5. Mobile support: tablet (iPad), not phone (deferred to Phase 2)

**UI Components (React):**

**1. Outbound Orders List (`/warehouse/outbound/orders`)**
```tsx
// OutboundOrdersListPage.tsx
export const OutboundOrdersListPage = () => {
  const [orders, setOrders] = useState<OutboundOrderSummary[]>([]);
  const [filters, setFilters] = useState({ status: '', customerId: '', dateFrom: '', dateTo: '' });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    fetchOrders();
  }, [filters]);

  const fetchOrders = async () => {
    setLoading(true);
    const response = await api.get('/outbound/orders/summary', { params: filters });
    setOrders(response.data);
    setLoading(false);
  };

  return (
    <div>
      <h1>Outbound Orders</h1>
      <Filters filters={filters} onChange={setFilters} />
      {loading ? <Spinner /> : <OrdersTable orders={orders} />}
    </div>
  );
};

// OrdersTable.tsx
const OrdersTable = ({ orders }) => (
  <table>
    <thead>
      <tr>
        <th>Order Number</th>
        <th>Customer</th>
        <th>Status</th>
        <th>Items</th>
        <th>Requested Ship Date</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      {orders.map(order => (
        <tr key={order.id}>
          <td><Link to={`/warehouse/outbound/orders/${order.id}`}>{order.orderNumber}</Link></td>
          <td>{order.customerName}</td>
          <td><StatusBadge status={order.status} /></td>
          <td>{order.itemCount}</td>
          <td>{formatDate(order.requestedShipDate)}</td>
          <td>
            <Button onClick={() => navigate(`/warehouse/outbound/orders/${order.id}`)}>View</Button>
            {order.status === 'ALLOCATED' && <Button onClick={() => releaseOrder(order.id)}>Release</Button>}
            {order.status !== 'SHIPPED' && <Button onClick={() => cancelOrder(order.id)}>Cancel</Button>}
          </td>
        </tr>
      ))}
    </tbody>
  </table>
);
```

**2. Order Detail (`/warehouse/outbound/orders/{id}`)**
```tsx
// OrderDetailPage.tsx
export const OrderDetailPage = () => {
  const { id } = useParams();
  const [order, setOrder] = useState<OutboundOrderSummary | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchOrder();
  }, [id]);

  const fetchOrder = async () => {
    const response = await api.get(`/outbound/orders/${id}`);
    setOrder(response.data);
    setLoading(false);
  };

  if (loading) return <Spinner />;
  if (!order) return <div>Order not found</div>;

  return (
    <div>
      <h1>Order {order.orderNumber}</h1>
      <OrderHeader order={order} />
      <OrderLinesTable lines={order.lines} />
      <OrderActions order={order} onRelease={releaseOrder} onCancel={cancelOrder} />
    </div>
  );
};

// OrderHeader.tsx
const OrderHeader = ({ order }) => (
  <div>
    <p><strong>Customer:</strong> {order.customerName}</p>
    <p><strong>Status:</strong> <StatusBadge status={order.status} /></p>
    <p><strong>Order Date:</strong> {formatDate(order.orderDate)}</p>
    <p><strong>Requested Ship Date:</strong> {formatDate(order.requestedShipDate)}</p>
    {order.shipmentNumber && <p><strong>Shipment:</strong> {order.shipmentNumber}</p>}
    {order.trackingNumber && <p><strong>Tracking:</strong> {order.trackingNumber}</p>}
  </div>
);
```

**3. Packing Station (`/warehouse/outbound/pack/{orderId}`)**
```tsx
// PackingStationPage.tsx
export const PackingStationPage = () => {
  const { orderId } = useParams();
  const [order, setOrder] = useState<OutboundOrder | null>(null);
  const [scannedItems, setScannedItems] = useState<ScannedItem[]>([]);
  const [packagingType, setPackagingType] = useState<PackagingType>('BOX');
  const [barcodeInput, setBarcodeInput] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    fetchOrder();
  }, [orderId]);

  const handleBarcodeScan = (barcode: string) => {
    const item = order.lines.find(line => line.item.barcode === barcode);
    if (!item) {
      setError(`Barcode ${barcode} not found in order`);
      return;
    }
    setScannedItems([...scannedItems, { barcode, qty: 1 }]);
    setBarcodeInput('');
  };

  const handlePack = async () => {
    try {
      const response = await api.post(`/outbound/orders/${orderId}/pack`, {
        commandId: uuidv4(),
        scannedItems,
        packagingType
      });
      navigate(`/warehouse/outbound/dispatch`);
    } catch (err) {
      setError(err.response.data.message);
    }
  };

  const allItemsScanned = order?.lines.every(line =>
    scannedItems.some(scanned => scanned.barcode === line.item.barcode)
  );

  return (
    <div>
      <h1>Pack Order {order?.orderNumber}</h1>
      <div style={{ display: 'flex' }}>
        <div style={{ flex: 1 }}>
          <h2>Order Items</h2>
          <OrderLinesTable lines={order?.lines} scannedItems={scannedItems} />
        </div>
        <div style={{ flex: 1 }}>
          <h2>Packing Progress</h2>
          <input
            type="text"
            placeholder="Scan barcode"
            value={barcodeInput}
            onChange={e => setBarcodeInput(e.target.value)}
            onKeyPress={e => e.key === 'Enter' && handleBarcodeScan(barcodeInput)}
            autoFocus
          />
          <ScannedItemsList items={scannedItems} />
          <select value={packagingType} onChange={e => setPackagingType(e.target.value)}>
            <option value="BOX">Box</option>
            <option value="PALLET">Pallet</option>
          </select>
          <Button onClick={handlePack} disabled={!allItemsScanned}>Pack</Button>
          {error && <ErrorMessage message={error} />}
        </div>
      </div>
    </div>
  );
};
```

**4. Dispatch Confirmation (`/warehouse/outbound/dispatch`)**
```tsx
// DispatchConfirmationPage.tsx
export const DispatchConfirmationPage = () => {
  const [shipments, setShipments] = useState<ShipmentSummary[]>([]);
  const [selectedShipment, setSelectedShipment] = useState<ShipmentSummary | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [carrier, setCarrier] = useState<Carrier>('FEDEX');
  const [vehicleId, setVehicleId] = useState('');

  useEffect(() => {
    fetchPackedShipments();
  }, []);

  const fetchPackedShipments = async () => {
    const response = await api.get('/shipments/summary', { params: { status: 'PACKED' } });
    setShipments(response.data);
  };

  const handleDispatch = async () => {
    try {
      await api.post(`/shipments/${selectedShipment.id}/dispatch`, {
        commandId: uuidv4(),
        carrier,
        vehicleId
      });
      setShowModal(false);
      fetchPackedShipments(); // Refresh list
    } catch (err) {
      alert(err.response.data.message);
    }
  };

  return (
    <div>
      <h1>Dispatch Confirmation</h1>
      <table>
        <thead>
          <tr>
            <th>Shipment Number</th>
            <th>Order Number</th>
            <th>Customer</th>
            <th>Packed At</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {shipments.map(shipment => (
            <tr key={shipment.id}>
              <td>{shipment.shipmentNumber}</td>
              <td>{shipment.outboundOrderNumber}</td>
              <td>{shipment.customerName}</td>
              <td>{formatDate(shipment.packedAt)}</td>
              <td>
                <Button onClick={() => { setSelectedShipment(shipment); setShowModal(true); }}>
                  Dispatch
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {showModal && (
        <Modal onClose={() => setShowModal(false)}>
          <h2>Dispatch Shipment {selectedShipment.shipmentNumber}</h2>
          <label>Carrier:</label>
          <select value={carrier} onChange={e => setCarrier(e.target.value)}>
            <option value="FEDEX">FedEx</option>
            <option value="UPS">UPS</option>
            <option value="DHL">DHL</option>
            <option value="USPS">USPS</option>
            <option value="OTHER">Other</option>
          </select>
          <label>Vehicle ID:</label>
          <input type="text" value={vehicleId} onChange={e => setVehicleId(e.target.value)} />
          <Button onClick={handleDispatch}>Confirm Dispatch</Button>
        </Modal>
      )}
    </div>
  );
};
```

### Acceptance Criteria

```gherkin
Scenario: View outbound orders list
  Given user navigates to /warehouse/outbound/orders
  When page loads
  Then orders table displayed with columns: Order Number, Customer, Status, Items, Requested Ship Date, Actions
  And filters displayed: Status dropdown, Customer search, Date range
  And pagination controls displayed

Scenario: Filter orders by status
  Given user on outbound orders list page
  When user selects status "PACKED" from filter dropdown
  Then API called: GET /outbound/orders/summary?status=PACKED
  And table displays only PACKED orders

Scenario: View order detail
  Given user on outbound orders list page
  When user clicks order number "OUT-0001"
  Then navigates to /warehouse/outbound/orders/OUT-0001
  And order header displayed (customer, status, dates)
  And order lines table displayed (item, qty, picked qty, shipped qty)
  And actions displayed (Release, Cancel)

Scenario: Pack order successfully
  Given user navigates to /warehouse/outbound/pack/OUT-0001
  And order has 2 items: ITEM-001 (qty 10), ITEM-002 (qty 5)
  When user scans barcode "BC-001" (ITEM-001)
  Then item highlighted green in order lines table
  When user scans barcode "BC-002" (ITEM-002)
  Then all items scanned, "Pack" button enabled
  When user selects packaging type "BOX"
  And clicks "Pack" button
  Then API called: POST /outbound/orders/OUT-0001/pack
  And navigates to /warehouse/outbound/dispatch
  And success toast displayed: "Order packed successfully"

Scenario: Packing validation error - barcode mismatch
  Given user on packing station page
  When user scans barcode "BC-999" (not in order)
  Then error message displayed: "Barcode BC-999 not found in order"
  And item NOT added to scanned items list
  And "Pack" button remains disabled

Scenario: Dispatch shipment
  Given user navigates to /warehouse/outbound/dispatch
  And shipment "SHIP-0001" with status PACKED displayed in table
  When user clicks "Dispatch" button
  Then dispatch modal displayed
  When user selects carrier "FEDEX"
  And enters vehicle ID "VAN-042"
  And clicks "Confirm Dispatch" button
  Then API called: POST /shipments/SHIP-0001/dispatch
  And modal closed
  And shipment removed from table (status changed to DISPATCHED)
  And success toast displayed: "Shipment dispatched successfully"

Scenario: Network error handling
  Given user on outbound orders list page
  And API returns 500 Internal Server Error
  When page loads
  Then error message displayed: "Failed to load orders. Please try again."
  And retry button displayed
  When user clicks retry button
  Then API called again
```

### Implementation Notes

- Use React Router for navigation
- Use Axios for API calls (with interceptors for auth, error handling)
- Use React Query for data fetching (caching, refetching)
- Use Formik for form validation (packing station, dispatch modal)
- Use Tailwind CSS for styling (existing Phase 1 UI)
- Use React Toastify for success/error toasts
- Barcode scanning: use keyboard input (barcode scanner acts as keyboard)
- Polling: refetch orders every 30 seconds (React Query refetchInterval)

### Validation / Checks

**Local Testing:**
```bash
# Start frontend dev server
cd src/LKvitai.MES.UI
npm run dev

# Navigate to pages
http://localhost:3000/warehouse/outbound/orders
http://localhost:3000/warehouse/outbound/orders/<id>
http://localhost:3000/warehouse/outbound/pack/<id>
http://localhost:3000/warehouse/outbound/dispatch

# Test barcode scanning (use keyboard input)
# Test filters, pagination, actions
# Test error states (disconnect network, check error messages)
```

**Metrics:**
- N/A (frontend, no backend metrics)

**Logs:**
- Browser console logs for errors (React error boundaries)

**Backwards Compatibility:**
- New UI screens, no breaking changes
- Existing Phase 1 UI unaffected

### Definition of Done

- [ ] OutboundOrdersListPage component created
- [ ] OrderDetailPage component created
- [ ] PackingStationPage component created
- [ ] DispatchConfirmationPage component created
- [ ] React Router routes configured
- [ ] API integration implemented (Axios, React Query)
- [ ] Form validation implemented (Formik)
- [ ] Error handling implemented (error boundaries, toasts)
- [ ] Loading states implemented (spinners, skeletons)
- [ ] Responsive design implemented (desktop, tablet)
- [ ] Accessibility tested (keyboard navigation, screen reader)
- [ ] Unit tests: 10+ scenarios (component rendering, user interactions)
- [ ] Integration tests: end-to-end workflows (list → detail → pack → dispatch)
- [ ] Manual testing: all screens, all workflows, error states
- [ ] Code review completed
- [ ] Documentation: UI screenshots added to docs/

---

## Sprint 1 Summary

**Total Tasks:** 10  
**Total Effort:** 13.5 days  
**Status:** ✅ All tasks fully elaborated and ready for execution

**Task Breakdown:**
- Foundation (3 tasks, 2.5 days): Idempotency, Event Versioning, Correlation/Trace
- Sales Orders (2 tasks, 4 days): Entities, APIs
- Outbound/Shipment (2 tasks, 4 days): Entities, State Machines
- Packing/Dispatch (2 tasks, 2 days): Packing MVP, Dispatch MVP
- Projections/UI (2 tasks, 3 days): Projections, UI Screens

**Next Steps:**
1. Review this execution pack with team
2. Begin Sprint 1 execution with PRD-1501 (Idempotency)
3. Follow task order: PRD-1501 → PRD-1502 → PRD-1503 → PRD-1504 → PRD-1505 → PRD-1506 → PRD-1507 → PRD-1508 → PRD-1509 → PRD-1510
4. Update progress ledger after each task completion
5. Conduct sprint review after all tasks complete

**Files:**
- Main file: `prod-ready-tasks-PHASE15-S1.md` (this file)
- Summary file: `prod-ready-tasks-PHASE15-S1-summary.md`
- Progress ledger: `prod-ready-tasks-progress.md`
