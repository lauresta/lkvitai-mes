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

