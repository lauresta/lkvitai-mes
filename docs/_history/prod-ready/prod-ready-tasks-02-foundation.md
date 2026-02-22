# Production-Ready Warehouse Tasks - Part 2: Foundation Tasks

**Version:** 1.0  
**Date:** February 10, 2026  
**Source:** prod-ready-universe.md

---

## Task PRD-0001: Idempotency Framework Completion

**Epic:** Foundation  
**Phase:** 1.5  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §5 (Idempotency Rules)

### Context

- Phase 1 has basic idempotency via CommandId in some handlers
- Need comprehensive framework: all commands, events, saga steps, external API calls
- Prevent duplicate processing in distributed system (at-least-once delivery)
- Support replay scenarios (network retries, saga compensation)

### Scope

**In Scope:**
- `processed_commands` table (CommandId, ProcessedAt, Result)
- Idempotency middleware for all command handlers
- Event processing checkpoints (event_processing_checkpoints table)
- Saga step idempotency (store step results, replay-safe)
- External API idempotency (include request ID in headers)

**Out of Scope:**
- Query idempotency (read operations are naturally idempotent)
- UI-level deduplication (handled by API layer)

### Requirements

**Functional:**
1. All command handlers MUST check `processed_commands` before execution
2. If CommandId exists → return cached result (no re-execution)
3. If CommandId new → execute + store result atomically (same transaction)
4. Event handlers MUST check `event_processing_checkpoints` (StreamId, EventNumber)
5. Saga steps MUST store intermediate results (SagaState table)
6. External API calls MUST include `X-Idempotency-Key` header

**Non-Functional:**
1. Idempotency check latency: < 10ms (indexed lookup)
2. Storage overhead: < 1MB per 1000 commands (compact result storage)
3. Retention: 30 days for processed_commands (cleanup job)
4. Thread-safe: handle concurrent duplicate requests (DB unique constraint)

**Data Model:**
```sql
CREATE TABLE processed_commands (
  command_id UUID PRIMARY KEY,
  command_type VARCHAR(200) NOT NULL,
  processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  result_json JSONB,
  user_id UUID,
  INDEX idx_processed_commands_processed_at (processed_at)
);

CREATE TABLE event_processing_checkpoints (
  handler_name VARCHAR(200) NOT NULL,
  stream_id VARCHAR(500) NOT NULL,
  last_event_number BIGINT NOT NULL,
  processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (handler_name, stream_id)
);
```

**API Changes:**
- All command DTOs MUST include `CommandId` property (GUID)
- Middleware: `IdempotencyMiddleware` (MediatR pipeline behavior)

### Acceptance Criteria

```gherkin
Scenario: Duplicate command rejected with cached result
  Given command "AdjustStock" with CommandId "abc-123" already processed
  And cached result: { success: true, newQty: 100 }
  When same command submitted again with CommandId "abc-123"
  Then API returns cached result immediately
  And command handler NOT executed
  And response time < 50ms

Scenario: Concurrent duplicate commands handled safely
  Given command "CreateSalesOrder" with CommandId "xyz-789"
  When 2 requests arrive simultaneously with same CommandId
  Then first request executes command
  And second request waits for DB lock
  And second request returns cached result
  And only 1 sales order created

Scenario: Event handler idempotency
  Given event "StockMoved" (stream: stockledger-item-001, event #42)
  And AvailableStock projection already processed event #42
  When event replayed (e.g., projection rebuild)
  Then projection handler checks checkpoint
  And skips event #42 (already processed)
  And processes event #43 onwards

Scenario: Saga step idempotency
  Given AllocationSaga step "CreateReservation" completed
  And SagaState stores: { reservationId: "res-001", status: "allocated" }
  When saga replayed (e.g., after crash)
  Then step "CreateReservation" checks SagaState
  And skips re-execution (reservation already exists)
  And proceeds to next step

Scenario: External API idempotency
  Given carrier API call "CreateShipment" with idempotency key "ship-001"
  When API call retried (network timeout)
  Then carrier API receives same idempotency key
  And carrier returns existing shipment (no duplicate)
```

### Implementation Notes

- Use MediatR `IPipelineBehavior<TRequest, TResponse>` for command idempotency
- Store result as JSONB (flexible schema, queryable)
- Cleanup job: DELETE FROM processed_commands WHERE processed_at < NOW() - INTERVAL '30 days'
- For saga idempotency: use MassTransit's built-in saga state persistence

### Validation / Checks

**Local Testing:**
```bash
# Run idempotency tests
dotnet test --filter "Category=Idempotency"

# Check processed_commands table
psql -d warehouse -c "SELECT COUNT(*) FROM processed_commands;"

# Simulate duplicate command
curl -X POST /api/warehouse/v1/adjustments \
  -H "Content-Type: application/json" \
  -d '{"commandId":"test-123","itemId":"...","qty":10,"reason":"TEST"}'
# Repeat same curl → should return cached result
```

**Metrics:**
- `idempotency_cache_hits_total` (counter)
- `idempotency_cache_misses_total` (counter)
- `idempotency_check_duration_ms` (histogram)

**Logs:**
- INFO: "Command {CommandId} already processed, returning cached result"
- WARN: "Concurrent duplicate command detected: {CommandId}"

### Definition of Done

- [ ] `processed_commands` table created with migration
- [ ] `event_processing_checkpoints` table created
- [ ] `IdempotencyMiddleware` implemented and registered
- [ ] All command handlers include CommandId validation
- [ ] Event handlers check checkpoints before processing
- [ ] Saga steps check SagaState before execution
- [ ] External API calls include idempotency headers
- [ ] Unit tests: 10+ scenarios (duplicate, concurrent, replay)
- [ ] Integration tests: end-to-end command deduplication
- [ ] Cleanup job scheduled (daily, delete old records)
- [ ] Metrics exposed (cache hits/misses, latency)
- [ ] Documentation updated (ADR: Idempotency Strategy)

---


## Task PRD-0002: Event Schema Versioning

**Epic:** Foundation  
**Phase:** 1.5  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** None  
**SourceRefs:** Universe §5 (Cross-Cutting Architecture > Event Schema Versioning), Universe §7.Appendix A (Event Catalog, lines 2440-2520)

### Context

- Event sourcing requires immutable events (cannot change schema after published)
- Need versioning strategy for schema evolution (add fields, rename fields, change types)
- All events must include SchemaVersion field (e.g., "v1", "v2")
- Event upcasting: convert old events to new schema on read
- Backwards compatibility: new code must handle old events

### Scope

**In Scope:**
- Event schema versioning convention (SchemaVersion field in all events)
- Event upcasting framework (IEventUpcaster<TFrom, TTo>)
- Version registry (map event type → supported versions)
- Marten configuration for event upcasting
- Documentation: schema evolution guide

**Out of Scope:**
- Downcasting (new events → old schema, not needed)
- Cross-aggregate schema coordination (each aggregate owns its events)
- Event migration (rewriting event store, too risky)

### Requirements

**Functional:**
1. All event records MUST include SchemaVersion property (string, e.g., "v1")
2. Event upcaster interface: `IEventUpcaster<TFrom, TTo>` with `TTo Upcast(TFrom oldEvent)` method
3. Marten MUST apply upcasters on event read (transparent to consumers)
4. Version registry MUST track: event type → list of versions (v1, v2, v3)
5. If event version not in registry → throw exception (unknown version)
6. Upcasters MUST be idempotent (upcast(upcast(event)) = upcast(event))

**Non-Functional:**
1. Upcasting overhead: < 5ms per event (in-memory transformation)
2. Version registry: loaded at startup (no runtime lookups)
3. Documentation: schema evolution guide with examples
4. Testing: property-based tests (upcast old events, verify new schema)

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
    old.From,
    old.To,
    StockMovementType.TRANSFER, // Default for old events
    "v2"
  );
}

// Marten configuration
services.AddMarten(opts => {
  opts.Events.Upcast<StockMovedV1, StockMovedV2>(new StockMovedV1ToV2Upcaster());
});
```

### Acceptance Criteria

```gherkin
Scenario: Event with schema version
  Given new event StockMovedV1 created
  When event serialized to JSON
  Then JSON includes field: "schemaVersion": "v1"
  And field is at root level (not nested)

Scenario: Upcast old event on read
  Given event store contains StockMovedV1 event (version v1)
  And upcaster registered: StockMovedV1ToV2Upcaster
  When projection reads event stream
  Then event automatically upcasted to StockMovedV2
  And projection receives StockMovedV2 (not V1)
  And Type field populated with default: TRANSFER

Scenario: Multiple version upcasting (v1 → v2 → v3)
  Given event store contains StockMovedV1 event
  And upcasters registered: V1ToV2, V2ToV3
  When projection reads event
  Then event upcasted through chain: V1 → V2 → V3
  And projection receives StockMovedV3

Scenario: Unknown version throws exception
  Given event store contains event with SchemaVersion="v99"
  And no upcaster registered for v99
  When projection reads event
  Then exception thrown: "Unknown event version: v99"
  And error logged with event details

Scenario: Upcaster idempotency
  Given StockMovedV2 event (already latest version)
  When upcaster applied (V1ToV2)
  Then event unchanged (no transformation)
  And SchemaVersion remains "v2"
```

### Implementation Notes

- Use Marten's built-in upcasting: `opts.Events.Upcast<TFrom, TTo>(upcaster)`
- Upcasters are singletons (registered at startup)
- For complex transformations: use async upcasters (query DB for lookup data)
- Version naming: use semantic versioning (v1, v2, v3) not dates
- Breaking changes: create new event type (e.g., StockMovedV2 → StockTransferred)

### Validation / Checks

**Local Testing:**
```bash
# Run event versioning tests
dotnet test --filter "Category=EventVersioning"

# Verify event schema in DB
psql -d warehouse -c "SELECT data->>'SchemaVersion' as version, COUNT(*) FROM mt_events GROUP BY version;"

# Test upcasting
dotnet test --filter "FullyQualifiedName~EventUpcastingTests"
```

**Metrics:**
- `event_upcasting_duration_ms` (histogram, labels: from_version, to_version)
- `event_upcasting_errors_total` (counter, labels: event_type, error_type)

**Logs:**
- INFO: "Event upcaster registered: {FromType} → {ToType}"
- WARN: "Event version mismatch: expected {ExpectedVersion}, got {ActualVersion}"
- ERROR: "Event upcasting failed: {EventType}, {Exception}"

### Definition of Done

- [ ] IEventUpcaster<TFrom, TTo> interface defined
- [ ] Marten upcasting configuration added
- [ ] Version registry implemented (event type → versions map)
- [ ] All existing events updated with SchemaVersion field
- [ ] Example upcaster implemented (StockMovedV1ToV2)
- [ ] Unit tests: 10+ scenarios (upcast, chain, idempotency, unknown version)
- [ ] Property-based tests: upcast random old events, verify schema
- [ ] Documentation: schema evolution guide (when to upcast vs new event type)
- [ ] Code review completed

---

## Task PRD-0003: Correlation/Trace Propagation

**Epic:** Foundation  
**Phase:** 1.5  
**Estimate:** S (0.5 day)  
**OwnerType:** Infra/DevOps  
**Dependencies:** None  
**SourceRefs:** Universe §5 (Cross-Cutting Architecture > Observability > Traces)

### Context

- Distributed system: API → Command Handler → Event Bus → Saga → External API
- Need to trace requests across boundaries (correlation ID)
- OpenTelemetry standard for distributed tracing
- Correlation ID: unique per request (GUID), propagated in headers/logs/events
- Trace ID: OpenTelemetry trace identifier (W3C Trace Context)

### Scope

**In Scope:**
- Correlation ID generation (per HTTP request)
- Correlation ID propagation (HTTP headers, MassTransit messages, Marten events, logs)
- OpenTelemetry integration (ASP.NET Core, MassTransit, Marten)
- Trace context propagation (W3C Trace Context standard)
- Structured logging with correlation ID

**Out of Scope:**
- Trace visualization (use external tool: Jaeger, Zipkin, Application Insights)
- Span creation (basic auto-instrumentation only, detailed spans in Phase 2)
- Metrics correlation (separate concern)

### Requirements

**Functional:**
1. Every HTTP request MUST generate correlation ID (GUID)
2. Correlation ID MUST be in response header: `X-Correlation-ID`
3. Correlation ID MUST be in all log messages (structured field: `CorrelationId`)
4. Correlation ID MUST be in all events (field: `CorrelationId`)
5. Correlation ID MUST be in all MassTransit messages (header: `CorrelationId`)
6. OpenTelemetry trace context MUST be propagated (W3C Trace Context headers)

**Non-Functional:**
1. Overhead: < 1ms per request (correlation ID generation + propagation)
2. Log format: JSON with correlation ID at root level
3. Trace sampling: 100% in dev/staging, 10% in production (configurable)

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

// OpenTelemetry setup
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

Scenario: Correlation ID propagated to events
  Given HTTP request with X-Correlation-ID: abc-123
  When command emits event StockMoved
  Then event includes field: CorrelationId: abc-123
  And event stored in mt_events with correlation ID

Scenario: Correlation ID propagated to MassTransit
  Given HTTP request with X-Correlation-ID: xyz-789
  When saga publishes message to queue
  Then message header includes: CorrelationId: xyz-789
  And downstream consumer receives correlation ID

Scenario: OpenTelemetry trace context propagated
  Given HTTP request with traceparent header (W3C Trace Context)
  When request calls external API (Agnum)
  Then external API call includes traceparent header
  And trace spans linked (parent → child)

Scenario: Logs include correlation ID
  Given HTTP request with X-Correlation-ID: test-456
  When any log message written (INFO, WARN, ERROR)
  Then log JSON includes: "CorrelationId": "test-456"
  And correlation ID at root level (not nested)
```

### Implementation Notes

- Use Serilog's `LogContext.PushProperty()` for correlation ID in logs
- MassTransit auto-propagates correlation ID (built-in)
- Marten: add correlation ID to event metadata (custom projection)
- OpenTelemetry: use auto-instrumentation (ASP.NET Core, HttpClient, MassTransit)

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
```

**Metrics:**
- N/A (tracing overhead tracked by OpenTelemetry)

**Logs:**
- All logs MUST include CorrelationId field
- Example: `{"Timestamp":"2026-02-10T10:00:00Z","Level":"INFO","Message":"Item created","CorrelationId":"abc-123"}`

### Definition of Done

- [ ] CorrelationIdMiddleware implemented and registered
- [ ] OpenTelemetry configured (ASP.NET Core, HttpClient, MassTransit)
- [ ] Serilog configured with correlation ID enricher
- [ ] MassTransit correlation ID propagation verified
- [ ] Marten event metadata includes correlation ID
- [ ] Unit tests: correlation ID generation, propagation
- [ ] Integration tests: end-to-end trace (HTTP → Event → Saga → External API)
- [ ] Jaeger/Zipkin exporter configured (dev/staging)
- [ ] Documentation: how to trace requests across services
- [ ] Code review completed

---
