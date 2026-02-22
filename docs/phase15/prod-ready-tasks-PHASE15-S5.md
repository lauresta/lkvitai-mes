# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 5 (Execution Pack)

**Version:** 1.0
**Date:** February 12, 2026
**Sprint:** Phase 1.5 Sprint 5
**Source:** prod-ready-universe.md, prod-ready-tasks-progress.md
**Status:** Ready for Execution

---

## Sprint Overview

**Sprint Goal:** Harden system reliability, performance, and observability to production-grade standards with focus on idempotency gaps, concurrency safety, integration resilience, and operational maturity.

**Sprint Duration:** 2 weeks
**Total Tasks:** 20
**Estimated Effort:** 15.5 days

**Focus Areas:**
1. **Reliability Hardening:** Command handler idempotency audit, projection replay safety, saga checkpointing
2. **Performance Baseline:** Database indexing, query optimization, projection rebuild benchmarks
3. **Observability Maturity:** Structured logging, business metrics, alert tuning
4. **Integration Resilience:** Agnum export retry, label printer queue, ERP contract tests
5. **UI Quality:** Empty states, error handling, bulk operations, advanced search
6. **Security:** API rate limiting, sensitive data masking

**Dependencies:**
- Sprint 4 complete (PRD-1541 to PRD-1560)
- Operator end-to-end workflows validated

**Assumptions:**
- By end of Sprint 4, operator can execute full inbound/outbound workflows
- Sprint 5 focuses on production hardening, not new features

---

## Sprint 5 Task Index

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-1561 | Reliability | Command Handler Idempotency Audit | M | PRD-1501 | Backend/API | Universe §5.Idempotency |
| PRD-1562 | Reliability | Projection Replay Safety | M | PRD-1509,1513 | Projections | Universe §5.Projections |
| PRD-1563 | Reliability | Saga Step Checkpointing | M | PRD-1505,1508 | Backend/API | Universe §5.Sagas |
| PRD-1564 | Reliability | Aggregate Concurrency Tests | S | PRD-1536 | QA | Universe §5.Concurrency |
| PRD-1565 | Performance | Database Index Strategy | M | None | Infra/DevOps | Universe §5.Performance |
| PRD-1566 | Performance | Query Execution Plan Review | S | PRD-1565 | Backend/API | Universe §5.Performance |
| PRD-1567 | Performance | Projection Rebuild Benchmarks | S | PRD-1553 | QA | Universe §5.Performance |
| PRD-1568 | Performance | API Response Time SLAs | M | None | Backend/API | Universe §5.Performance |
| PRD-1569 | Observability | Structured Logging Enhancement | M | PRD-1539 | Infra/DevOps | Universe §5.Observability |
| PRD-1570 | Observability | Business Metrics Coverage | S | PRD-1545 | Backend/API | Universe §5.Observability |
| PRD-1571 | Observability | Alert Tuning & Escalation | S | PRD-1546 | Infra/DevOps | Universe §5.Monitoring |
| PRD-1572 | Integration | Agnum Export Retry Hardening | M | PRD-1514,1515 | Integration | Universe §4.Epic D |
| PRD-1573 | Integration | Label Printer Queue Resilience | M | PRD-1516 | Integration | Universe §4.Epic G |
| PRD-1574 | Integration | ERP Event Contract Tests | M | PRD-1556 | Integration | Universe §5.Integration |
| PRD-1575 | UI | Empty State & Error Handling UI | M | PRD-1523-1534 | UI | Universe §5.UX |
| PRD-1576 | UI | Bulk Operations (Multi-Select) | M | PRD-1528,1529 | UI | Universe §5.UX |
| PRD-1577 | UI | Advanced Search & Filters | M | PRD-1525,1528 | UI | Universe §5.UX |
| PRD-1578 | Security | API Rate Limiting | S | None | Backend/API | Universe §5.Security |
| PRD-1579 | Security | Sensitive Data Masking in Logs | S | PRD-1569 | Backend/API | Universe §5.Security |
| PRD-1580 | Testing | Load & Stress Testing Suite | L | All above | QA | Universe §5.Performance |

**Total Effort:** 15.5 days (1 developer, 3.1 weeks)

---

## Task PRD-1561: Command Handler Idempotency Audit

**Epic:** Reliability
**Phase:** 1.5
**Sprint:** 5
**Estimate:** M (1 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1501 (Idempotency foundation)
**SourceRefs:** Universe §5.Idempotency, codex-suspicions.md (idempotency gaps)

### Context

- PRD-1501 established idempotency infrastructure (processed_commands table, deduplication middleware)
- Need comprehensive audit of all command handlers to ensure idempotency compliance
- Some handlers may have side effects (external API calls, file writes) that are not replay-safe
- Goal: 100% of command handlers are idempotent and tested

### Scope

**In Scope:**
- Audit all command handlers in `src/LKvitai.MES.Api/Services/*CommandHandlers.cs`
- Identify non-idempotent operations (external API calls without deduplication, file writes)
- Add idempotency tests for critical handlers (SalesOrder, Outbound, Valuation)
- Document idempotency patterns in `docs/architecture/idempotency-patterns.md`
- Fix identified gaps (add request IDs to external calls, check-before-write for files)

**Out of Scope:**
- UI-level idempotency (button double-click prevention deferred to Phase 2)
- Distributed transaction coordination (saga-level idempotency covered in PRD-1563)

### Requirements

**Functional:**
1. Audit checklist: For each command handler, verify:
   - CommandId checked against processed_commands table
   - Database writes use upsert or check-before-insert patterns
   - External API calls include request ID for deduplication
   - File writes check existence before creating
   - Event publishing is transactional (outbox pattern)
2. Idempotency test pattern: Replay same command 3x, assert same result
3. Document findings in audit report (handler name, idempotent: yes/no, gaps, remediation)
4. Fix top 5 critical gaps (SalesOrder submit, Outbound pack, Shipment dispatch, Valuation adjust, Stock adjust)

**Non-Functional:**
1. Performance: Idempotency check adds < 10ms latency per command
2. Coverage: 100% of command handlers audited
3. Testing: Idempotency tests for 10 critical handlers

**Data Model:**
No schema changes (uses existing processed_commands table from PRD-1501)

**API:**
No new endpoints (internal audit task)

**Audit Report Format:**
```markdown
# Command Handler Idempotency Audit Report

## Summary
- Total handlers audited: 25
- Idempotent: 20
- Non-idempotent (gaps): 5
- Critical gaps fixed: 5

## Findings

### Handler: CreateSalesOrderHandler
- **Status:** ✅ Idempotent
- **Pattern:** CommandId checked, upsert to sales_orders table
- **Test:** CreateSalesOrderIdempotencyTest (3x replay, same result)

### Handler: PackOutboundOrderHandler
- **Status:** ⚠️ Gap identified
- **Issue:** Label printing API call lacks request ID
- **Impact:** Duplicate labels printed on replay
- **Remediation:** Add X-Request-ID header with CommandId
- **Fixed:** Yes (commit abc123)

...
```

### Acceptance Criteria

```gherkin
Feature: Command Handler Idempotency Audit

Scenario: Audit all command handlers
  Given 25 command handlers in codebase
  When idempotency audit executed
  Then audit report generated with status for each handler
  And report includes: handler name, idempotent status, gaps, remediation
  And report saved to docs/architecture/idempotency-audit-report.md

Scenario: Idempotency test for SalesOrder submit
  Given SalesOrder command with CommandId "cmd-001"
  When command executed 3 times
  Then only 1 SalesOrder created in database
  And all 3 responses return same OrderId
  And processed_commands table has 1 entry for "cmd-001"

Scenario: Fix label printing idempotency gap
  Given PackOutboundOrder command with CommandId "cmd-002"
  And label printing API called during pack
  When command replayed 2 times
  Then label printing API receives X-Request-ID: "cmd-002" on both calls
  And printer API deduplicates (only 1 label printed)

Scenario: External API call with request ID
  Given DispatchShipment command with CommandId "cmd-003"
  And carrier API called to generate tracking number
  When command replayed
  Then carrier API receives X-Request-ID: "cmd-003"
  And carrier returns same tracking number on replay

Scenario: File write idempotency
  Given AgnumExport command with CommandId "cmd-004"
  And export writes CSV file to blob storage
  When command replayed
  Then file existence checked before write
  And if file exists, write skipped (no duplicate)
```

### Validation / Checks

**Audit Execution:**
```bash
# Run audit script (manual review + automated checks)
dotnet run --project tools/IdempotencyAudit

# Output: docs/architecture/idempotency-audit-report.md

# Review report
cat docs/architecture/idempotency-audit-report.md

# Expected: 25 handlers audited, gaps identified, remediation plan
```

**Idempotency Tests:**
```bash
# Run idempotency test suite
dotnet test --filter "FullyQualifiedName~IdempotencyTests"

# Expected: 10 tests pass (3x replay for each critical handler)
```

**Manual Verification:**
```bash
# Test SalesOrder submit idempotency
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

COMMAND_ID="test-cmd-$(uuidgen)"

# Submit same command 3 times
for i in {1..3}; do
  curl -X POST http://localhost:5000/api/warehouse/v1/sales-orders \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -H "X-Command-ID: $COMMAND_ID" \
    -d '{
      "customerId": "cust-001",
      "lines": [{"itemId": 1, "qty": 10, "unitPrice": 100}]
    }'
done

# Expected: All 3 responses return same OrderId, only 1 row in sales_orders
```

**Database Check:**
```sql
-- Verify processed_commands entry
SELECT * FROM processed_commands WHERE command_id = 'test-cmd-...';
-- Expected: 1 row, processed_at timestamp

-- Verify only 1 SalesOrder created
SELECT COUNT(*) FROM sales_orders WHERE created_by = 'admin';
-- Expected: 1 (not 3)
```

### Definition of Done

- [ ] All 25 command handlers audited (checklist completed)
- [ ] Audit report generated: `docs/architecture/idempotency-audit-report.md`
- [ ] Top 5 critical gaps identified and fixed
- [ ] Idempotency tests added for 10 critical handlers
- [ ] All idempotency tests pass (3x replay, same result)
- [ ] External API calls include request ID headers
- [ ] File writes check existence before creating
- [ ] Documentation updated: `docs/architecture/idempotency-patterns.md`
- [ ] Code review approved (focus on replay safety)
- [ ] No regressions in existing tests

---

## Task PRD-1562: Projection Replay Safety

**Epic:** Reliability
**Phase:** 1.5
**Sprint:** 5
**Estimate:** M (1 day)
**OwnerType:** Projections
**Dependencies:** PRD-1509 (Outbound projections), PRD-1513 (OnHandValue projection)
**SourceRefs:** Universe §5.Projections, codex-suspicions.md (projection rebuild gaps)

### Context

- Projections must be rebuildable from event streams (disaster recovery, schema changes)
- Current projection rebuild (PRD-1553) may have gaps: non-idempotent updates, missing event handlers
- Need to ensure all projections are replay-safe (can rebuild from scratch without errors)
- Goal: 100% projection rebuild success rate

### Scope

**In Scope:**
- Audit all projection handlers in `src/LKvitai.MES.Infrastructure/Projections/`
- Identify non-idempotent projection updates (e.g., `qty += delta` without checking current state)
- Add replay safety tests: rebuild projection from events, compare to current state
- Fix identified gaps (use upsert patterns, handle out-of-order events)
- Document projection rebuild procedure in runbook

**Out of Scope:**
- Real-time projection updates (SignalR deferred to Phase 2)
- Projection versioning (schema evolution deferred to Phase 2)

### Requirements

**Functional:**
1. Audit checklist: For each projection, verify:
   - Projection handler is idempotent (can apply same event multiple times)
   - Projection uses upsert (INSERT ... ON CONFLICT UPDATE) or check-before-insert
   - Projection handles out-of-order events (event sequence number checked)
   - Projection rebuild clears existing data before replay
2. Replay safety test pattern: Rebuild projection from events, assert matches current state
3. Document findings in audit report
4. Fix top 5 critical gaps (AvailableStock, OnHandValue, OutboundOrderSummary, ShipmentSummary, LocationBalance)

**Non-Functional:**
1. Performance: Projection rebuild completes in < 5 minutes for 100k events
2. Coverage: 100% of projections audited
3. Testing: Replay safety tests for 5 critical projections

**Data Model:**
No schema changes (uses existing projection tables)

**API:**
Existing rebuild endpoint: `POST /api/admin/projections/rebuild` (from PRD-1553)

**Audit Report Format:**
```markdown
# Projection Replay Safety Audit Report

## Summary
- Total projections audited: 8
- Replay-safe: 6
- Gaps identified: 2
- Critical gaps fixed: 2

## Findings

### Projection: AvailableStockProjection
- **Status:** ✅ Replay-safe
- **Pattern:** Upsert on (item_id, location_id, lot_id)
- **Test:** RebuildAvailableStockTest (rebuild from 1000 events, matches current)

### Projection: OnHandValueProjection
- **Status:** ⚠️ Gap identified
- **Issue:** Qty update uses += without checking current state
- **Impact:** Replay doubles quantities
- **Remediation:** Change to SET qty = event.qty (absolute value)
- **Fixed:** Yes (commit def456)

...
```

### Acceptance Criteria

```gherkin
Feature: Projection Replay Safety

Scenario: Audit all projections
  Given 8 projections in codebase
  When replay safety audit executed
  Then audit report generated with status for each projection
  And report includes: projection name, replay-safe status, gaps, remediation
  And report saved to docs/architecture/projection-replay-audit.md

Scenario: Replay safety test for AvailableStock
  Given AvailableStock projection with 1000 events
  When projection rebuilt from scratch
  Then rebuilt projection matches current state
  And all stock balances correct (no duplicates, no missing items)

Scenario: Fix OnHandValue replay gap
  Given OnHandValue projection with qty update using +=
  When projection rebuilt from events
  Then qty update changed to SET (absolute value)
  And rebuilt projection matches current state

Scenario: Handle out-of-order events
  Given projection receives events: E1 (seq 1), E3 (seq 3), E2 (seq 2)
  When projection processes events
  Then projection applies events in sequence order
  And final state matches expected (E1 → E2 → E3)

Scenario: Projection rebuild clears existing data
  Given AvailableStock projection with existing data
  When rebuild triggered
  Then existing projection data deleted
  And projection rebuilt from event stream
  And final state matches event stream
```

### Validation / Checks

**Audit Execution:**
```bash
# Run audit script
dotnet run --project tools/ProjectionReplayAudit

# Output: docs/architecture/projection-replay-audit.md

# Review report
cat docs/architecture/projection-replay-audit.md
```

**Replay Safety Tests:**
```bash
# Run replay safety test suite
dotnet test --filter "FullyQualifiedName~ProjectionReplayTests"

# Expected: 5 tests pass (rebuild from events, matches current)
```

**Manual Rebuild Test:**
```bash
# Trigger projection rebuild
curl -X POST http://localhost:5000/api/admin/projections/rebuild \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"projectionName": "AvailableStock"}'

# Expected: 200 OK, rebuild completes in < 5 minutes

# Verify projection data
curl -X GET http://localhost:5000/api/warehouse/v1/stock/available \
  -H "Authorization: Bearer $TOKEN"

# Expected: Stock balances match pre-rebuild state
```

**Database Check:**
```sql
-- Compare projection data before/after rebuild
-- Before rebuild
SELECT item_id, location_id, SUM(on_hand_qty) FROM available_stock GROUP BY item_id, location_id;

-- Trigger rebuild (via API)

-- After rebuild
SELECT item_id, location_id, SUM(on_hand_qty) FROM available_stock GROUP BY item_id, location_id;

-- Expected: Same results (no data loss, no duplicates)
```

### Definition of Done

- [ ] All 8 projections audited (checklist completed)
- [ ] Audit report generated: `docs/architecture/projection-replay-audit.md`
- [ ] Top 5 critical gaps identified and fixed
- [ ] Replay safety tests added for 5 critical projections
- [ ] All replay safety tests pass (rebuild matches current)
- [ ] Projection handlers use upsert patterns
- [ ] Projection rebuild clears existing data before replay
- [ ] Documentation updated: `docs/ops/projection-rebuild-runbook.md`
- [ ] Code review approved (focus on idempotency)
- [ ] No regressions in existing tests

---

## Task PRD-1563: Saga Step Checkpointing

**Epic:** Reliability
**Phase:** 1.5
**Sprint:** 5
**Estimate:** M (1 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1505 (SalesOrder allocation saga), PRD-1508 (Shipment dispatch saga)
**SourceRefs:** Universe §5.Sagas, Universe §5.Idempotency

### Context

- Sagas coordinate multi-aggregate transactions (e.g., SalesOrder allocation: query stock → create reservation → update order status)
- Current sagas may not checkpoint step completion, causing duplicate work on retry
- Need saga step checkpointing to ensure each step executes exactly once
- Goal: Saga replay safety (can resume from last completed step)

### Scope

**In Scope:**
- Add saga_step_checkpoints table (saga_id, step_name, completed_at, result)
- Update saga handlers to check/record step completion
- Add saga replay tests: simulate failure mid-saga, resume from checkpoint
- Document saga patterns in architecture docs

**Out of Scope:**
- Saga compensation (rollback on failure deferred to Phase 2)
- Distributed saga coordination (single-database sagas only for Phase 1.5)

### Requirements

**Functional:**
1. Saga step checkpoint table:
   ```sql
   CREATE TABLE saga_step_checkpoints (
     id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
     saga_id UUID NOT NULL,
     step_name VARCHAR(100) NOT NULL,
     completed_at TIMESTAMP NOT NULL DEFAULT NOW(),
     result JSONB,
     UNIQUE (saga_id, step_name)
   );
   ```
2. Saga handler pattern:
   ```csharp
   public async Task ExecuteSaga(Guid sagaId)
   {
       // Step 1: Query stock
       if (!await IsStepCompleted(sagaId, "QueryStock"))
       {
           var stock = await QueryAvailableStock();
           await RecordStepCompletion(sagaId, "QueryStock", stock);
       }

       // Step 2: Create reservation
       if (!await IsStepCompleted(sagaId, "CreateReservation"))
       {
           var reservationId = await CreateReservation();
           await RecordStepCompletion(sagaId, "CreateReservation", reservationId);
       }

       // Step 3: Update order status
       if (!await IsStepCompleted(sagaId, "UpdateOrderStatus"))
       {
           await UpdateOrderStatus();
           await RecordStepCompletion(sagaId, "UpdateOrderStatus", null);
       }
   }
   ```
3. Saga replay test: Simulate failure after step 1, resume saga, verify step 1 not re-executed
4. Apply checkpointing to 3 critical sagas: SalesOrder allocation, Shipment dispatch, Agnum export

**Non-Functional:**
1. Performance: Checkpoint write adds < 5ms per step
2. Reliability: Saga can resume from any step on failure
3. Testing: Replay tests for 3 critical sagas

**Data Model:**
```csharp
public class SagaStepCheckpoint
{
    public Guid Id { get; set; }
    public Guid SagaId { get; set; }
    public string StepName { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Result { get; set; } // JSON
}
```

**API:**
No new endpoints (internal saga infrastructure)

### Acceptance Criteria

```gherkin
Feature: Saga Step Checkpointing

Scenario: Saga step checkpoint recorded
  Given SalesOrder allocation saga with sagaId "saga-001"
  When step "QueryStock" completes
  Then saga_step_checkpoints table has entry: sagaId="saga-001", stepName="QueryStock"
  And checkpoint includes result (available stock qty)

Scenario: Saga step skipped if already completed
  Given saga step "CreateReservation" already completed (checkpoint exists)
  When saga resumed
  Then step "CreateReservation" skipped (not re-executed)
  And saga proceeds to next step "UpdateOrderStatus"

Scenario: Saga replay after failure
  Given SalesOrder allocation saga fails after step 1 (QueryStock)
  When saga resumed
  Then step 1 skipped (checkpoint exists)
  And saga resumes from step 2 (CreateReservation)
  And saga completes successfully

Scenario: Saga checkpoint with result
  Given saga step "CreateReservation" completes with reservationId "res-001"
  When checkpoint recorded
  Then checkpoint result includes: {"reservationId": "res-001"}
  And subsequent steps can retrieve result from checkpoint

Scenario: Saga idempotency test
  Given SalesOrder allocation saga executed 3 times (same sagaId)
  When saga completes
  Then only 1 reservation created
  And all 3 executions return same reservationId
  And saga_step_checkpoints has 3 entries (1 per step)
```

### Validation / Checks

**Migration:**
```bash
# Generate migration
dotnet ef migrations add AddSagaStepCheckpoints --project src/LKvitai.MES.Infrastructure

# Apply migration
dotnet ef database update --project src/LKvitai.MES.Api

# Verify table created
psql -d warehouse -c "\d saga_step_checkpoints"
# Expected: Table with columns: id, saga_id, step_name, completed_at, result
```

**Saga Replay Test:**
```bash
# Run saga replay tests
dotnet test --filter "FullyQualifiedName~SagaReplayTests"

# Expected: 3 tests pass (1 per critical saga)
```

**Manual Test:**
```bash
# Trigger SalesOrder allocation saga
curl -X POST http://localhost:5000/api/warehouse/v1/sales-orders \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-001",
    "lines": [{"itemId": 1, "qty": 10, "unitPrice": 100}]
  }'

# Check saga checkpoints
psql -d warehouse -c "SELECT * FROM saga_step_checkpoints WHERE saga_id = (SELECT id FROM sales_orders ORDER BY created_at DESC LIMIT 1);"

# Expected: 3 rows (QueryStock, CreateReservation, UpdateOrderStatus)
```

### Definition of Done

- [ ] Migration created: AddSagaStepCheckpoints
- [ ] saga_step_checkpoints table created
- [ ] Saga handler pattern implemented (check/record step completion)
- [ ] Checkpointing applied to 3 critical sagas
- [ ] Saga replay tests added (3 tests)
- [ ] All saga replay tests pass
- [ ] Documentation updated: `docs/architecture/saga-patterns.md`
- [ ] Code review approved
- [ ] No regressions in existing tests

---

## Task PRD-1564: Aggregate Concurrency Tests

**Epic:** Reliability | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** S (0.5 day) | **OwnerType:** QA
**Dependencies:** PRD-1536 | **SourceRefs:** Universe §5.Concurrency

### Context
Aggregates use optimistic concurrency (version numbers) to prevent lost updates. Need tests to verify concurrent command handling doesn't cause data corruption.

### Scope
**In Scope:** Concurrency tests for 5 critical aggregates (StockLedger, Reservation, SalesOrder, OutboundOrder, Valuation), test pattern for concurrent commands, load test (100 concurrent commands)
**Out of Scope:** Distributed locking

### Requirements
**Functional:** 1) Concurrency test pattern (2 concurrent commands, verify only 1 succeeds), 2) Load test (100 concurrent commands, verify final state correct)
**Non-Functional:** Performance: Concurrency exception < 10ms, Reliability: No data corruption

### Acceptance Criteria
```gherkin
Scenario: Concurrent stock adjustments
  Given StockLedger with version 1
  When 2 concurrent AdjustStock commands submitted
  Then 1 succeeds (version 2), 1 fails with ConcurrencyException
  And final stock balance correct

Scenario: Load test concurrent commands
  Given StockLedger aggregate
  When 100 concurrent AdjustStock commands submitted
  Then all processed, final balance = sum of adjustments, no corruption
```

### Validation
```bash
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"
# Expected: 5 tests pass
```

### Definition of Done
- [ ] Concurrency tests for 5 aggregates
- [ ] Load test (100 concurrent commands)
- [ ] All tests pass
- [ ] Documentation: `docs/architecture/concurrency-testing.md`

---
## Task PRD-1565: Database Index Strategy

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** Infra/DevOps
**Dependencies:** None | **SourceRefs:** Universe §5.Performance

### Context
Current database has minimal indexes (primary keys only). Slow queries on stock lookups, order searches, projection queries. Need comprehensive index strategy.

### Scope
**In Scope:** Analyze slow queries (pg_stat_statements), add indexes for AvailableStock, SalesOrders, Events, composite indexes, index maintenance strategy
**Out of Scope:** Partitioning, materialized views

### Requirements
**Functional:** 1) Enable pg_stat_statements, identify top 10 slowest queries, 2) Create indexes: available_stock(item_id, location_id), sales_orders(customer_id, status), events(aggregate_id, timestamp), 3) Weekly REINDEX job
**Non-Functional:** Query time reduced by 50%, Index size < 20% of table size

### Acceptance Criteria
```gherkin
Scenario: Add indexes for AvailableStock
  Given query: SELECT * FROM available_stock WHERE item_id = 1 AND location_id = 2
  When index created on (item_id, location_id)
  Then query time reduced from 500ms to 10ms

Scenario: Composite index for order search
  Given query with customer_id AND status
  When composite index created
  Then query uses Index Scan (EXPLAIN shows)
```

### Validation
```bash
psql -d warehouse -c "CREATE EXTENSION IF NOT EXISTS pg_stat_statements;"
psql -d warehouse -c "SELECT query, mean_exec_time FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;"
dotnet ef migrations add AddPerformanceIndexes
dotnet ef database update
psql -d warehouse -c "\di"  # Verify indexes
```

### Definition of Done
- [ ] pg_stat_statements enabled
- [ ] Slow query analysis completed
- [ ] Migration: AddPerformanceIndexes (10+ indexes)
- [ ] Query performance improved by 50%
- [ ] Weekly REINDEX job scheduled
- [ ] Documentation: `docs/database-indexes.md`

---
## Task PRD-1566: Query Execution Plan Review

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** S (0.5 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1565 | **SourceRefs:** Universe §5.Performance

### Context
After adding indexes, need to verify queries use them correctly. Review execution plans for critical queries.

### Scope
**In Scope:** EXPLAIN ANALYZE for 20 critical queries, identify missing indexes or inefficient queries, optimize query patterns
**Out of Scope:** Query rewriting (major refactors deferred)

### Requirements
**Functional:** 1) Run EXPLAIN ANALYZE on critical queries, 2) Verify Index Scan (not Seq Scan), 3) Document query patterns in `docs/query-optimization.md`
**Non-Functional:** All critical queries use indexes, Query time < 100ms

### Acceptance Criteria
```gherkin
Scenario: Verify AvailableStock query uses index
  Given query: SELECT * FROM available_stock WHERE item_id = 1
  When EXPLAIN ANALYZE executed
  Then plan shows "Index Scan using idx_available_stock_item_location"
  And execution time < 10ms

Scenario: Identify sequential scans
  Given 20 critical queries
  When execution plans reviewed
  Then no Seq Scan on large tables (> 10k rows)
```

### Validation
```bash
psql -d warehouse -c "EXPLAIN ANALYZE SELECT * FROM available_stock WHERE item_id = 1;"
# Expected: Index Scan, time < 10ms
```

### Definition of Done
- [ ] EXPLAIN ANALYZE for 20 critical queries
- [ ] All queries use indexes (no Seq Scan on large tables)
- [ ] Query optimization report generated
- [ ] Documentation: `docs/query-optimization.md`

---
## Task PRD-1567: Projection Rebuild Benchmarks

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** S (0.5 day) | **OwnerType:** QA
**Dependencies:** PRD-1553 | **SourceRefs:** Universe §5.Performance

### Context
Projection rebuild must complete in reasonable time for disaster recovery. Need benchmarks for 100k events.

### Scope
**In Scope:** Benchmark projection rebuild for 5 projections (AvailableStock, OnHandValue, OutboundOrderSummary, ShipmentSummary, LocationBalance), measure time and memory usage
**Out of Scope:** Real-time projection updates

### Requirements
**Functional:** 1) Generate 100k test events, 2) Rebuild each projection, measure time, 3) Target: < 5 minutes per projection
**Non-Functional:** Memory usage < 2GB during rebuild

### Acceptance Criteria
```gherkin
Scenario: Benchmark AvailableStock rebuild
  Given 100k StockMoved events
  When AvailableStock projection rebuilt
  Then rebuild completes in < 5 minutes
  And memory usage < 2GB
  And final projection matches expected state

Scenario: Benchmark all projections
  Given 100k events across all aggregates
  When all 5 projections rebuilt
  Then total time < 25 minutes (5 min × 5 projections)
```

### Validation
```bash
# Generate test events
dotnet run --project tools/EventGenerator -- --count 100000

# Benchmark rebuild
time curl -X POST http://localhost:5000/api/admin/projections/rebuild \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"projectionName": "AvailableStock"}'

# Expected: < 5 minutes
```

### Definition of Done
- [ ] Test event generator (100k events)
- [ ] Benchmark tests for 5 projections
- [ ] All projections rebuild in < 5 minutes
- [ ] Benchmark report: `docs/projection-rebuild-benchmarks.md`

---
## Task PRD-1568: API Response Time SLAs

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** Backend/API
**Dependencies:** None | **SourceRefs:** Universe §5.Performance

### Context
Need defined SLAs for API response times. Monitor and alert on SLA violations.

### Scope
**In Scope:** Define SLAs (p95 < 200ms, p99 < 500ms), add response time tracking, alert on violations
**Out of Scope:** Auto-scaling (deferred to Phase 2)

### Requirements
**Functional:** 1) SLA targets: p50 < 100ms, p95 < 200ms, p99 < 500ms, 2) Track response times per endpoint, 3) Alert if p95 > 200ms for 5 minutes
**Non-Functional:** SLA compliance > 99%

### Acceptance Criteria
```gherkin
Scenario: Track API response times
  Given API receiving requests
  When metrics collected
  Then response time histogram available (p50, p95, p99)
  And metrics grouped by endpoint

Scenario: Alert on SLA violation
  Given p95 response time > 200ms for 5 minutes
  When alert rule evaluated
  Then alert fired to Slack/email
  And alert includes: endpoint, current p95, threshold
```

### Validation
```bash
# Generate load
for i in {1..1000}; do
  curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items &
done
wait

# Check metrics
curl http://localhost:9090/api/v1/query?query=histogram_quantile\(0.95,rate\(http_request_duration_ms_bucket\[5m\]\)\)
# Expected: p95 < 200ms
```

### Definition of Done
- [ ] SLA targets defined (p50/p95/p99)
- [ ] Response time tracking per endpoint
- [ ] Alert rule for SLA violations
- [ ] SLA compliance dashboard
- [ ] Documentation: `docs/api-slas.md`

---
## Task PRD-1569: Structured Logging Enhancement

**Epic:** Observability | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** Infra/DevOps
**Dependencies:** PRD-1539 | **SourceRefs:** Universe §5.Observability

### Context
Current logging uses string interpolation. Need structured logging (JSON) for better querying and analysis.

### Scope
**In Scope:** Convert to structured logging (Serilog), add correlation IDs, log enrichment (user, tenant, request ID), log aggregation (Seq or ELK)
**Out of Scope:** Log retention policies (deferred)

### Requirements
**Functional:** 1) Use Serilog with JSON formatter, 2) Add correlation ID to all logs, 3) Enrich logs with: UserId, TenantId, RequestId, 4) Ship logs to Seq
**Non-Functional:** Log query time < 1 second

### Acceptance Criteria
```gherkin
Scenario: Structured log format
  Given API request processed
  When logs written
  Then log format is JSON with fields: timestamp, level, message, correlationId, userId, requestId

Scenario: Query logs by correlation ID
  Given request with correlationId "abc-123"
  When query logs in Seq: correlationId = "abc-123"
  Then all logs for request returned (across services)
```

### Validation
```bash
# Check log format
tail -f logs/warehouse-api.log
# Expected: JSON format with structured fields

# Query logs in Seq
open http://localhost:5341
# Search: correlationId = "abc-123"
```

### Definition of Done
- [ ] Serilog configured with JSON formatter
- [ ] Correlation ID middleware added
- [ ] Log enrichment (user, tenant, request ID)
- [ ] Seq Docker Compose configuration
- [ ] Logs shipped to Seq
- [ ] Documentation: `docs/structured-logging.md`

---
## Task PRD-1570: Business Metrics Coverage

**Epic:** Observability | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** S (0.5 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1545 | **SourceRefs:** Universe §5.Observability

### Context
Need business metrics (not just technical metrics) for operational visibility: orders created, picks completed, stock movements.

### Scope
**In Scope:** Add business metrics: orders_created_total, picks_completed_total, stock_movements_total, on_hand_value_gauge
**Out of Scope:** Real-time dashboards (covered in PRD-1545)

### Requirements
**Functional:** 1) Emit business metrics on domain events, 2) Metrics: orders_created, picks_completed, stock_movements, on_hand_value, 3) Expose via /metrics endpoint
**Non-Functional:** Metric emission < 1ms overhead

### Acceptance Criteria
```gherkin
Scenario: Track orders created
  Given SalesOrder created
  When OrderCreated event published
  Then orders_created_total counter incremented
  And metric tagged with: customer_id, order_type

Scenario: Track on-hand value
  Given stock valuation updated
  When ValuationAdjusted event published
  Then on_hand_value_gauge updated
  And metric shows current total value
```

### Validation
```bash
curl http://localhost:5000/metrics | grep orders_created_total
# Expected: orders_created_total{customer_id="cust-001"} 5
```

### Definition of Done
- [ ] Business metrics added (4 metrics)
- [ ] Metrics emitted on domain events
- [ ] Metrics visible in /metrics endpoint
- [ ] Grafana dashboard updated with business metrics

---
## Task PRD-1571: Alert Tuning & Escalation

**Epic:** Observability | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** S (0.5 day) | **OwnerType:** Infra/DevOps
**Dependencies:** PRD-1546 | **SourceRefs:** Universe §5.Monitoring

### Context
Initial alerts may be too noisy or miss critical issues. Need tuning and escalation policies.

### Scope
**In Scope:** Review alert thresholds, add escalation (email → Slack → PagerDuty), alert grouping, throttling
**Out of Scope:** On-call rotation (manual for Phase 1.5)

### Requirements
**Functional:** 1) Tune alert thresholds (reduce false positives), 2) Escalation: email (5 min) → Slack (10 min) → PagerDuty (15 min), 3) Group alerts by service, 4) Throttle: max 1 alert per 5 minutes
**Non-Functional:** Alert fatigue < 5 alerts/day

### Acceptance Criteria
```gherkin
Scenario: Alert escalation
  Given API error rate > 5% for 5 minutes
  When alert fired
  Then email sent immediately
  And if not acknowledged in 5 minutes, Slack notification sent
  And if not acknowledged in 10 minutes, PagerDuty page sent

Scenario: Alert grouping
  Given 10 alerts for same service in 1 minute
  When alerts grouped
  Then 1 notification sent with "10 alerts for warehouse-api"
```

### Validation
```bash
# Trigger test alert
curl -X POST http://localhost:9093/api/v1/alerts \
  -d '[{"labels":{"alertname":"HighErrorRate","severity":"critical"}}]'

# Verify escalation in Alertmanager UI
open http://localhost:9093
```

### Definition of Done
- [ ] Alert thresholds tuned (< 5 false positives/day)
- [ ] Escalation policy configured
- [ ] Alert grouping enabled
- [ ] Throttling configured
- [ ] Documentation: `docs/alert-escalation.md`

---
## Task PRD-1572: Agnum Export Retry Hardening

**Epic:** Integration | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** Integration
**Dependencies:** PRD-1514, PRD-1515 | **SourceRefs:** Universe §4.Epic D

### Context
Agnum export may fail due to network issues, API downtime. Need retry logic with exponential backoff.

### Scope
**In Scope:** Retry policy (3 attempts, exponential backoff), dead letter queue for failed exports, manual retry UI
**Out of Scope:** Real-time export (daily batch only)

### Requirements
**Functional:** 1) Retry policy: 3 attempts with backoff (1 min, 5 min, 15 min), 2) Dead letter queue for failed exports, 3) Manual retry button in admin UI, 4) Alert on 3 consecutive failures
**Non-Functional:** Export success rate > 99%

### Acceptance Criteria
```gherkin
Scenario: Retry on transient failure
  Given Agnum export fails with network error
  When retry policy applied
  Then export retried after 1 minute
  And if fails again, retried after 5 minutes
  And if fails 3 times, moved to dead letter queue

Scenario: Manual retry from UI
  Given failed export in dead letter queue
  When admin clicks "Retry Export"
  Then export re-attempted immediately
  And if succeeds, removed from dead letter queue
```

### Validation
```bash
# Simulate Agnum API failure
# (Stop Agnum mock service)

# Trigger export
curl -X POST http://localhost:5000/api/admin/agnum/export \
  -H "Authorization: Bearer $TOKEN"

# Verify retry attempts in logs
tail -f logs/warehouse-api.log | grep "Agnum export retry"

# Check dead letter queue
curl http://localhost:5000/api/admin/agnum/failed-exports \
  -H "Authorization: Bearer $TOKEN"
```

### Definition of Done
- [ ] Retry policy implemented (3 attempts, exponential backoff)
- [ ] Dead letter queue for failed exports
- [ ] Manual retry UI
- [ ] Alert on 3 consecutive failures
- [ ] Export success rate > 99%

---
## Task PRD-1573: Label Printer Queue Resilience

**Epic:** Integration | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** Integration
**Dependencies:** PRD-1516 | **SourceRefs:** Universe §4.Epic G

### Context
Label printing may fail if printer offline. Need queue with retry and fallback to PDF.

### Scope
**In Scope:** Print queue (pending, printing, failed), retry logic, fallback to PDF generation, printer status monitoring
**Out of Scope:** Multiple printer support (single printer only)

### Requirements
**Functional:** 1) Print queue table (job_id, label_data, status, attempts), 2) Retry: 3 attempts with 30 sec delay, 3) Fallback: Generate PDF if printer offline, 4) Printer health check (ping TCP 9100)
**Non-Functional:** Print success rate > 95%

### Acceptance Criteria
```gherkin
Scenario: Retry on printer offline
  Given label print job submitted
  And printer offline
  When print attempted
  Then job status = FAILED
  And retry after 30 seconds
  And if fails 3 times, generate PDF fallback

Scenario: Printer health check
  Given printer health check runs every 1 minute
  When printer offline
  Then alert fired: "Label printer offline"
  And all print jobs queued (not attempted)
```

### Validation
```bash
# Submit print job
curl -X POST http://localhost:5000/api/warehouse/v1/labels/print \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"labelType": "PALLET", "data": {...}}'

# Check print queue
curl http://localhost:5000/api/admin/labels/queue \
  -H "Authorization: Bearer $TOKEN"

# Simulate printer offline
# (Disconnect printer or block TCP 9100)

# Verify retry and PDF fallback
```

### Definition of Done
- [ ] Print queue table created
- [ ] Retry logic (3 attempts, 30 sec delay)
- [ ] PDF fallback generation
- [ ] Printer health check (TCP 9100 ping)
- [ ] Alert on printer offline
- [ ] Print success rate > 95%

---
## Task PRD-1574: ERP Event Contract Tests

**Epic:** Integration | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** Integration
**Dependencies:** PRD-1556 | **SourceRefs:** Universe §5.Integration

### Context
ERP integration relies on event schemas. Need contract tests to detect breaking changes.

### Scope
**In Scope:** Contract tests for 10 event types (OrderCreated, StockMoved, etc.), schema validation, version compatibility tests
**Out of Scope:** Consumer-driven contracts (Pact deferred)

### Requirements
**Functional:** 1) Contract tests for 10 event types, 2) JSON schema validation, 3) Version compatibility tests (v1 → v2), 4) Fail build on breaking changes
**Non-Functional:** Contract tests run in < 30 seconds

### Acceptance Criteria
```gherkin
Scenario: Validate event schema
  Given OrderCreated event published
  When contract test runs
  Then event matches JSON schema
  And all required fields present (orderId, customerId, lines)

Scenario: Detect breaking change
  Given OrderCreated schema v1 has field "customerId"
  When field renamed to "clientId" in v2
  Then contract test fails
  And build blocked
```

### Validation
```bash
dotnet test --filter "FullyQualifiedName~ContractTests"
# Expected: 10 tests pass (1 per event type)
```

### Definition of Done
- [ ] Contract tests for 10 event types
- [ ] JSON schema validation
- [ ] Version compatibility tests
- [ ] CI pipeline fails on breaking changes
- [ ] Documentation: `docs/event-contracts.md`

---
## Task PRD-1575: Empty State & Error Handling UI

**Epic:** UI | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** UI
**Dependencies:** PRD-1523-1534 | **SourceRefs:** Universe §5.UX

### Context
Current UI shows blank pages when no data. Need empty states with helpful messages and actions.

### Scope
**In Scope:** Empty states for 10 pages (Items, Orders, Stock, etc.), error messages, retry buttons, loading states
**Out of Scope:** Skeleton loaders (deferred)

### Requirements
**Functional:** 1) Empty state component (icon, message, action button), 2) Error messages (user-friendly, not stack traces), 3) Retry button on errors, 4) Loading spinners
**Non-Functional:** UX: Clear guidance on next steps

### Acceptance Criteria
```gherkin
Scenario: Empty state for Items page
  Given no items in database
  When navigate to /warehouse/items
  Then see empty state: "No items yet. Add your first item to get started."
  And "Add Item" button visible

Scenario: Error handling
  Given API returns 500 error
  When page loads
  Then see error message: "Something went wrong. Please try again."
  And "Retry" button visible
  And clicking Retry reloads page
```

### Validation
```bash
# Manual UI test
# 1. Clear database
# 2. Navigate to /warehouse/items
# 3. Verify empty state shown
# 4. Simulate API error (stop API)
# 5. Verify error message and retry button
```

### Definition of Done
- [ ] Empty state component created
- [ ] Empty states for 10 pages
- [ ] Error messages (user-friendly)
- [ ] Retry buttons on errors
- [ ] Loading spinners
- [ ] Manual testing complete

---
## Task PRD-1576: Bulk Operations (Multi-Select)

**Epic:** UI | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** UI
**Dependencies:** PRD-1528, PRD-1529 | **SourceRefs:** Universe §5.UX

### Context
Operators need to perform bulk actions (delete multiple items, approve multiple orders). Current UI requires one-by-one actions.

### Scope
**In Scope:** Multi-select checkboxes, bulk actions (delete, approve, export), confirmation dialogs
**Out of Scope:** Bulk edit (deferred)

### Requirements
**Functional:** 1) Multi-select checkboxes on list pages, 2) Bulk actions: Delete, Approve, Export to CSV, 3) Confirmation dialog ("Delete 5 items?"), 4) Progress indicator for bulk operations
**Non-Functional:** Bulk operations complete in < 5 seconds for 100 items

### Acceptance Criteria
```gherkin
Scenario: Bulk delete items
  Given Items list page with 10 items
  When select 5 items (checkboxes)
  And click "Delete Selected"
  Then confirmation dialog: "Delete 5 items?"
  And clicking "Confirm" deletes 5 items
  And success message: "5 items deleted"

Scenario: Bulk export to CSV
  Given Orders list with 20 orders
  When select 10 orders
  And click "Export to CSV"
  Then CSV file downloaded with 10 orders
```

### Validation
```bash
# Manual UI test
# 1. Navigate to /warehouse/items
# 2. Select 5 items (checkboxes)
# 3. Click "Delete Selected"
# 4. Verify confirmation dialog
# 5. Confirm deletion
# 6. Verify 5 items deleted
```

### Definition of Done
- [ ] Multi-select checkboxes on 5 list pages
- [ ] Bulk actions: Delete, Approve, Export
- [ ] Confirmation dialogs
- [ ] Progress indicators
- [ ] Manual testing complete

---
## Task PRD-1577: Advanced Search & Filters

**Epic:** UI | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** M (1 day) | **OwnerType:** UI
**Dependencies:** PRD-1525, PRD-1528 | **SourceRefs:** Universe §5.UX

### Context
Current search is basic (single field). Need advanced filters (date range, status, category, etc.).

### Scope
**In Scope:** Filter panel, multi-field search, date range picker, status filters, category filters, save filter presets
**Out of Scope:** Full-text search (deferred)

### Requirements
**Functional:** 1) Filter panel (collapsible), 2) Filters: Date range, Status, Category, Supplier, Location, 3) Save filter presets ("My Orders", "Pending QC"), 4) Clear filters button
**Non-Functional:** Filter results update in < 500ms

### Acceptance Criteria
```gherkin
Scenario: Filter orders by date range
  Given Orders list page
  When select date range: 2026-02-01 to 2026-02-10
  And click "Apply Filters"
  Then only orders in date range shown

Scenario: Filter by multiple criteria
  Given Orders list
  When filter by: Status = ALLOCATED, Customer = "cust-001"
  Then only orders matching both criteria shown

Scenario: Save filter preset
  Given filters applied: Status = PENDING, Date = Last 7 days
  When click "Save Preset" and name it "Recent Pending"
  Then preset saved
  And clicking "Recent Pending" applies filters
```

### Validation
```bash
# Manual UI test
# 1. Navigate to /warehouse/orders
# 2. Open filter panel
# 3. Select date range, status
# 4. Click "Apply Filters"
# 5. Verify filtered results
# 6. Save preset
# 7. Clear filters
# 8. Load preset
# 9. Verify filters re-applied
```

### Definition of Done
- [ ] Filter panel component
- [ ] Filters: Date range, Status, Category, Supplier, Location
- [ ] Save filter presets
- [ ] Clear filters button
- [ ] Filter results update < 500ms
- [ ] Manual testing complete

---
## Task PRD-1578: API Rate Limiting

**Epic:** Security | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** S (0.5 day) | **OwnerType:** Backend/API
**Dependencies:** None | **SourceRefs:** Universe §5.Security

### Context
API has no rate limiting. Vulnerable to abuse and DoS attacks. Need rate limiting per user/IP.

### Scope
**In Scope:** Rate limiting middleware (100 req/min per user, 1000 req/min per IP), 429 responses, rate limit headers
**Out of Scope:** Distributed rate limiting (Redis deferred)

### Requirements
**Functional:** 1) Rate limit: 100 req/min per user, 1000 req/min per IP, 2) Return 429 Too Many Requests with Retry-After header, 3) Exempt admin endpoints from rate limiting
**Non-Functional:** Rate limiting overhead < 1ms per request

### Acceptance Criteria
```gherkin
Scenario: Rate limit per user
  Given user "operator-01"
  When 101 requests sent in 1 minute
  Then first 100 requests succeed (200 OK)
  And 101st request returns 429 Too Many Requests
  And response includes Retry-After header

Scenario: Rate limit headers
  Given request within rate limit
  When response received
  Then headers include: X-RateLimit-Limit: 100, X-RateLimit-Remaining: 95, X-RateLimit-Reset: 1234567890
```

### Validation
```bash
# Test rate limiting
for i in {1..101}; do
  curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items
done

# Expected: First 100 succeed, 101st returns 429
```

### Definition of Done
- [ ] Rate limiting middleware implemented
- [ ] Rate limits: 100 req/min per user, 1000 req/min per IP
- [ ] 429 responses with Retry-After header
- [ ] Rate limit headers (X-RateLimit-*)
- [ ] Admin endpoints exempted
- [ ] Tests pass

---
## Task PRD-1579: Sensitive Data Masking in Logs

**Epic:** Security | **Phase:** 1.5 | **Sprint:** 5 | **Estimate:** S (0.5 day) | **OwnerType:** Backend/API
**Dependencies:** PRD-1569 | **SourceRefs:** Universe §5.Security

### Context
Logs may contain sensitive data (passwords, tokens, PII). Need automatic masking.

### Scope
**In Scope:** Log masking for passwords, tokens, credit cards, emails, phone numbers
**Out of Scope:** Encryption at rest (deferred)

### Requirements
**Functional:** 1) Mask sensitive fields in logs: password, token, creditCard, email, phone, 2) Replace with "***MASKED***", 3) Apply to all log levels
**Non-Functional:** Masking overhead < 1ms per log entry

### Acceptance Criteria
```gherkin
Scenario: Mask password in logs
  Given log entry: "User login: {username: 'admin', password: 'secret123'}"
  When log written
  Then log shows: "User login: {username: 'admin', password: '***MASKED***'}"

Scenario: Mask credit card
  Given log entry contains credit card: "4111111111111111"
  When log written
  Then credit card masked: "***MASKED***"
```

### Validation
```bash
# Trigger log with sensitive data
curl -X POST http://localhost:5000/api/auth/login \
  -d '{"username":"admin","password":"secret123"}'

# Check logs
tail -f logs/warehouse-api.log | grep password
# Expected: password: "***MASKED***"
```

### Definition of Done
- [ ] Log masking middleware implemented
- [ ] Mask: password, token, creditCard, email, phone
- [ ] Applied to all log levels
- [ ] Tests pass
- [ ] Documentation: `docs/log-masking.md`

---
## Task PRD-1580: Load & Stress Testing Suite

**Epic:** Testing | **Phase:** 1.5 | **Sprint:** 5
**Estimate:** L (2 days)
**OwnerType:** QA
**Dependencies:** All above
**SourceRefs:** Universe §5.Performance

### Context

Need comprehensive load and stress tests to validate system performance under production load. Establish performance baselines.

### Scope

**In Scope:**
- Load testing tool (k6 or JMeter)
- Test scenarios: Create 100 orders, Pick 1000 items, Pack 50 shipments, Process 10k events
- Performance baselines (response time, throughput, error rate)
- Stress test (find breaking point)

**Out of Scope:**
- Chaos engineering (deferred to Phase 2)

### Requirements

**Functional:**
1. Load test scenarios:
   - Scenario 1: Create 100 SalesOrders (10 req/sec for 10 sec)
   - Scenario 2: Pick 1000 items (50 req/sec for 20 sec)
   - Scenario 3: Pack 50 shipments (5 req/sec for 10 sec)
   - Scenario 4: Process 10k events (projection lag < 1 sec)
2. Performance baselines:
   - p95 response time < 200ms
   - Throughput > 100 req/sec
   - Error rate < 0.1%
3. Stress test: Increase load until system fails (find breaking point)

**Non-Functional:**
1. Repeatability: Tests produce consistent results (±5%)
2. CI Integration: Load tests run nightly

### Acceptance Criteria

```gherkin
Feature: Load & Stress Testing

Scenario: Load test - Create 100 orders
  Given k6 load test script
  When 100 SalesOrders created (10 req/sec)
  Then p95 response time < 200ms
  And error rate < 0.1%
  And all orders created successfully

Scenario: Load test - Pick 1000 items
  Given 1000 pick tasks
  When picked at 50 req/sec
  Then p95 response time < 200ms
  And throughput > 50 req/sec
  And all picks completed

Scenario: Stress test - Find breaking point
  Given load test with increasing RPS (10, 50, 100, 200, 500)
  When system reaches capacity
  Then breaking point identified (e.g., 300 RPS)
  And error rate > 1% at breaking point
  And system recovers when load reduced

Scenario: Projection lag under load
  Given 10k events published
  When projections process events
  Then projection lag < 1 second
  And all projections eventually consistent
```

### Validation / Checks

**Install k6:**
```bash
brew install k6  # macOS
# or
curl https://github.com/grafana/k6/releases/download/v0.48.0/k6-v0.48.0-linux-amd64.tar.gz -L | tar xvz
```

**Run Load Tests:**
```bash
# Scenario 1: Create orders
k6 run tests/load/create-orders.js

# Scenario 2: Pick items
k6 run tests/load/pick-items.js

# Scenario 3: Pack shipments
k6 run tests/load/pack-shipments.js

# Scenario 4: Event processing
k6 run tests/load/event-processing.js

# Expected: All tests pass, p95 < 200ms, error rate < 0.1%
```

**Stress Test:**
```bash
k6 run --vus 10 --duration 30s tests/load/stress-test.js
k6 run --vus 50 --duration 30s tests/load/stress-test.js
k6 run --vus 100 --duration 30s tests/load/stress-test.js
# Continue until system fails
```

**k6 Script Example:**
```javascript
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  vus: 10,
  duration: '10s',
  thresholds: {
    http_req_duration: ['p(95)<200'],
    http_req_failed: ['rate<0.001'],
  },
};

export default function () {
  const payload = JSON.stringify({
    customerId: 'cust-001',
    lines: [{ itemId: 1, qty: 10, unitPrice: 100 }],
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ' + __ENV.TOKEN,
    },
  };

  const res = http.post('http://localhost:5000/api/warehouse/v1/sales-orders', payload, params);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 200ms': (r) => r.timings.duration < 200,
  });
}
```

### Definition of Done

- [ ] k6 installed and configured
- [ ] 4 load test scenarios implemented
- [ ] Performance baselines established (p95 < 200ms, throughput > 100 req/sec, error rate < 0.1%)
- [ ] Stress test identifies breaking point
- [ ] Load tests integrated into CI (nightly runs)
- [ ] Performance report generated: `docs/load-test-results.md`
- [ ] Code review approved
- [ ] All tests pass

---
