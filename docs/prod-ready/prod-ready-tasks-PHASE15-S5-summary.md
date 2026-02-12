# Phase 1.5 Sprint 5 - Task Summary

**Sprint Goal:** Production hardening - reliability, performance, observability maturity

**Total Tasks:** 20 (PRD-1561 to PRD-1580)
**Estimated Effort:** 15.5 days
**Sprint Duration:** 2 weeks

---

## Task List

### Reliability Hardening (4 tasks, 3.5 days)

**PRD-1561 - Command Handler Idempotency Audit (M, 1 day)**
- Audit all 25 command handlers for idempotency compliance
- Fix top 5 critical gaps (SalesOrder, Outbound, Valuation handlers)
- Add idempotency tests (3x replay, same result)
- Document patterns in architecture docs

**PRD-1562 - Projection Replay Safety (M, 1 day)**
- Audit all 8 projections for replay safety
- Fix non-idempotent updates (use upsert patterns)
- Add replay tests (rebuild from events, matches current)
- Document rebuild procedure in runbook

**PRD-1563 - Saga Step Checkpointing (M, 1 day)**
- Add saga_step_checkpoints table
- Implement check/record pattern for saga steps
- Apply to 3 critical sagas (allocation, dispatch, export)
- Add saga replay tests (resume from checkpoint)

**PRD-1564 - Aggregate Concurrency Tests (S, 0.5 day)**
- Add optimistic locking tests for SalesOrder, OutboundOrder
- Test concurrent updates (expect 409 conflict)
- Verify RowVersion increments on each update
- Document concurrency patterns

### Performance Baseline (4 tasks, 3 days)

**PRD-1565 - Database Index Strategy (M, 1 day)**
- Analyze slow queries from Sprint 4 logs
- Add indexes: sales_orders(status, customer_id), outbound_orders(status), shipments(tracking_number)
- Add composite indexes for common joins
- Benchmark query performance (before/after)

**PRD-1566 - Query Execution Plan Review (S, 0.5 day)**
- Review EXPLAIN ANALYZE for top 10 queries
- Identify missing indexes, sequential scans
- Optimize N+1 queries (add eager loading)
- Document query optimization patterns

**PRD-1567 - Projection Rebuild Benchmarks (S, 0.5 day)**
- Benchmark projection rebuild time (100k events)
- Target: < 5 minutes for all projections
- Identify bottlenecks (slow queries, missing indexes)
- Document rebuild performance baseline

**PRD-1568 - API Response Time SLAs (M, 1 day)**
- Define SLAs: GET < 500ms (p95), POST < 2s (p95)
- Add response time middleware (log slow requests)
- Add Grafana dashboard for API latency
- Alert on SLA violations (> 5% requests slow)

### Observability Maturity (3 tasks, 2 days)

**PRD-1569 - Structured Logging Enhancement (M, 1 day)**
- Standardize log format (JSON, correlation ID, user ID, operation)
- Add business event logging (order created, shipment dispatched)
- Add error context (stack trace, request payload)
- Configure log levels (Debug for dev, Info for prod)

**PRD-1570 - Business Metrics Coverage (S, 0.5 day)**
- Add metrics: orders_created, shipments_dispatched, stock_adjusted
- Add gauges: available_stock_value, pending_orders_count
- Add histograms: pick_time_seconds, pack_time_seconds
- Update Grafana dashboards with business metrics

**PRD-1571 - Alert Tuning & Escalation (S, 0.5 day)**
- Review Sprint 4 alerts (false positives, missed incidents)
- Tune thresholds (projection lag, saga failures, API errors)
- Add escalation rules (page on-call after 3 failures)
- Document alert runbook (symptoms, diagnosis, remediation)

### Integration Resilience (3 tasks, 3 days)

**PRD-1572 - Agnum Export Retry Hardening (M, 1 day)**
- Add exponential backoff retry (1h, 2h, 4h)
- Add circuit breaker (open after 3 failures, half-open after 1h)
- Add manual fallback (download CSV, upload to Agnum manually)
- Add export failure alerts (email accountant)

**PRD-1573 - Label Printer Queue Resilience (M, 1 day)**
- Add print queue table (job_id, label_data, status, retry_count)
- Add retry logic (3x with 10s delay)
- Add manual reprint endpoint (POST /api/labels/{id}/reprint)
- Add printer offline detection (health check)

**PRD-1574 - ERP Event Contract Tests (M, 1 day)**
- Add contract tests for ERP events (SalesOrderShipped, MaterialIssued)
- Verify event schema matches ERP expectations
- Add schema versioning tests (v1 → v2 compatibility)
- Document event contracts in API docs

### UI Quality (3 tasks, 3 days)

**PRD-1575 - Empty State & Error Handling UI (M, 1 day)**
- Add empty states for all list pages (orders, shipments, stock)
- Add error states (network error, 403, 500)
- Add retry buttons on errors
- Add loading skeletons (avoid blank screens)

**PRD-1576 - Bulk Operations (Multi-Select) (M, 1 day)**
- Add multi-select checkboxes to orders list, shipments list
- Add bulk actions: Cancel orders, Print labels, Export CSV
- Add confirmation dialogs (bulk cancel: "Cancel 5 orders?")
- Add progress indicators (bulk operation in progress)

**PRD-1577 - Advanced Search & Filters (M, 1 day)**
- Add search to stock dashboard (by SKU, description, location)
- Add filters to orders list (status, customer, date range)
- Add saved filters (save common searches)
- Add filter chips (show active filters, click to remove)

### Security (2 tasks, 1 day)

**PRD-1578 - API Rate Limiting (S, 0.5 day)**
- Add rate limiting middleware (100 req/min per user)
- Add rate limit headers (X-RateLimit-Remaining)
- Add 429 Too Many Requests response
- Exempt health check endpoint

**PRD-1579 - Sensitive Data Masking in Logs (S, 0.5 day)**
- Mask PII in logs (customer email, phone, address)
- Mask credentials (API keys, passwords)
- Add log scrubbing middleware
- Document sensitive data handling policy

### Testing (1 task, 2 days)

**PRD-1580 - Load & Stress Testing Suite (L, 2 days)**
- Add load tests: 100 concurrent users, 1000 req/min
- Test scenarios: Create orders, Pick items, Pack shipments
- Measure: Response time (p50, p95, p99), Error rate, Throughput
- Identify bottlenecks (database, CPU, memory)
- Document load testing results and capacity planning

---

## Critical Path

1. **Week 1 (Days 1-5):**
   - PRD-1561 (Idempotency audit) - Day 1
   - PRD-1562 (Projection replay) - Day 2
   - PRD-1563 (Saga checkpointing) - Day 3
   - PRD-1565 (Database indexes) - Day 4
   - PRD-1569 (Structured logging) - Day 5

2. **Week 2 (Days 6-10):**
   - PRD-1572 (Agnum retry) - Day 6
   - PRD-1573 (Label printer queue) - Day 7
   - PRD-1575 (Empty states) - Day 8
   - PRD-1576 (Bulk operations) - Day 9
   - PRD-1580 (Load testing) - Days 10-11

3. **Parallel (can overlap):**
   - PRD-1564, 1566, 1567, 1568 (Performance tests)
   - PRD-1570, 1571 (Observability)
   - PRD-1574 (ERP contracts)
   - PRD-1577 (Advanced search)
   - PRD-1578, 1579 (Security)

---

## Known Risks

1. **Idempotency gaps:** May discover more gaps than expected (extend PRD-1561 to 1.5 days)
2. **Performance bottlenecks:** Load testing may reveal database/CPU limits (require infrastructure scaling)
3. **Integration failures:** Agnum/Label printer APIs may be unreliable (require manual fallbacks)

---

## Success Criteria

After Sprint 5:
- ✅ All command handlers idempotent (100% audit coverage)
- ✅ All projections replay-safe (rebuild from events works)
- ✅ Sagas resume from checkpoints (no duplicate work)
- ✅ API response times meet SLAs (p95 < 2s)
- ✅ Database indexes optimized (slow queries eliminated)
- ✅ Structured logging operational (JSON format, correlation IDs)
- ✅ Integration retries hardened (Agnum, label printer)
- ✅ UI quality improved (empty states, bulk operations, search)
- ✅ Load testing complete (capacity baseline established)

---

## Dependencies

- Sprint 4 complete (PRD-1541 to PRD-1560)
- Operator workflows validated end-to-end
- Grafana dashboards operational (from PRD-1545)
- Health checks operational (from PRD-1544)

---

## Handoff to Sprint 6

Sprint 5 delivers production-grade reliability and performance. Sprint 6 will focus on advanced warehouse features (wave picking, cross-docking, RMA, QC enhancements, HU hierarchy, serial tracking, analytics).
