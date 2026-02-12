# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 9 (Execution Pack)

**Version:** 2.0
**Date:** February 12, 2026
**Sprint:** Phase 1.5 Sprint 9
**Source:** prod-ready-universe.md, prod-ready-tasks-progress.md
**Status:** Ready for Execution

---

## Sprint Overview

**Sprint Goal:** Final production readiness - performance optimization, monitoring & alerting, integration testing, and production deployment procedures.

**Sprint Duration:** 2 weeks
**Total Tasks:** 20
**Estimated Effort:** 19 days

**Focus Areas:**
1. **Performance Optimization:** Query optimization, caching strategy, connection pooling, async operations, load balancing
2. **Monitoring & Alerting:** APM integration, custom dashboards, alert escalation, SLA monitoring, capacity planning
3. **Integration Testing:** E2E test suite expansion, chaos engineering, failover testing, data migration tests, rollback procedures
4. **Production Deployment:** Blue-green deployment, canary releases, feature flags, production runbook, go-live checklist

**Dependencies:**
- Sprint 8 complete (PRD-1621 to PRD-1640)

---

## Sprint 9 Task Index

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-1641 | Performance | Query Optimization | M | PRD-1565 | Backend/API | Universe §5.Performance |
| PRD-1642 | Performance | Caching Strategy | M | None | Backend/API | Universe §5.Performance |
| PRD-1643 | Performance | Connection Pooling | M | None | Backend/API | Universe §5.Performance |
| PRD-1644 | Performance | Async Operations | M | None | Backend/API | Universe §5.Performance |
| PRD-1645 | Performance | Load Balancing | M | None | Infra/DevOps | Universe §5.Performance |
| PRD-1646 | Monitoring | APM Integration | M | PRD-1545 | Infra/DevOps | Universe §5.Observability |
| PRD-1647 | Monitoring | Custom Dashboards | M | PRD-1545 | Infra/DevOps | Universe §5.Observability |
| PRD-1648 | Monitoring | Alert Escalation | M | PRD-1546 | Infra/DevOps | Universe §5.Observability |
| PRD-1649 | Monitoring | SLA Monitoring | M | PRD-1568 | Backend/API | Universe §5.Observability |
| PRD-1650 | Monitoring | Capacity Planning | M | PRD-1649 | Infra/DevOps | Universe §5.Observability |
| PRD-1651 | Integration Testing | E2E Test Suite Expansion | L | PRD-1540 | QA | Universe §5.Testing |
| PRD-1652 | Integration Testing | Chaos Engineering | M | None | QA | Universe §5.Testing |
| PRD-1653 | Integration Testing | Failover Testing | M | PRD-1640 | QA | Universe §5.Testing |
| PRD-1654 | Integration Testing | Data Migration Tests | M | None | QA | Universe §5.Testing |
| PRD-1655 | Integration Testing | Rollback Procedures | M | PRD-1654 | Infra/DevOps | Universe §5.Testing |
| PRD-1656 | Deployment | Blue-Green Deployment | M | None | Infra/DevOps | Universe §5.Deployment |
| PRD-1657 | Deployment | Canary Releases | M | PRD-1656 | Infra/DevOps | Universe §5.Deployment |
| PRD-1658 | Deployment | Feature Flags | M | None | Backend/API | Universe §5.Deployment |
| PRD-1659 | Deployment | Production Runbook | M | PRD-1558 | Infra/DevOps | Universe §5.Deployment |
| PRD-1660 | Deployment | Go-Live Checklist | M | PRD-1560 | QA | Universe §5.Deployment |

**Total Effort:** 19 days

---
## Task PRD-1641: Query Optimization

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1565 | **SourceRefs:** Universe §5.Performance

### Context
Analyze and optimize slow database queries to meet performance SLAs. Target: API response time < 500ms (p95), projection queries < 100ms, report generation < 3 seconds.

### Scope
**In Scope:**
- Analyze slow query log (queries > 100ms)
- Add missing indexes on frequently queried columns
- Optimize N+1 query patterns (use eager loading)
- Review and optimize query plans (EXPLAIN ANALYZE)
- Benchmark query performance before/after optimization
- Document index strategy and query patterns

**Out of Scope:**
- Database sharding (single-instance optimization only)
- Read replicas (Phase 2)
- Query result caching (covered in PRD-1642)

### Requirements

**Functional Requirements:**
1. Identify all queries > 100ms from application logs and PostgreSQL slow query log
2. Add indexes for:
   - `items` table: `idx_items_category_id`, `idx_items_supplier_id`, `idx_items_barcode`
   - `sales_orders` table: `idx_sales_orders_customer_id_status`, `idx_sales_orders_order_date`
   - `outbound_orders` table: `idx_outbound_orders_status_requested_ship_date`
   - `shipments` table: `idx_shipments_tracking_number`, `idx_shipments_dispatched_at`
   - `available_stock_view` (materialized view): `idx_available_stock_item_location`
   - `on_hand_value_view`: `idx_on_hand_value_category_id`
3. Optimize EF Core queries:
   - Replace `.ToList().Where()` with `.Where().ToList()` (server-side filtering)
   - Use `.Include()` for related entities (avoid N+1)
   - Use `.AsNoTracking()` for read-only queries
   - Use `.Select()` projections instead of loading full entities
4. Optimize Marten event queries:
   - Add indexes on `mt_events.stream_id`, `mt_events.type`, `mt_events.timestamp`
   - Use compiled queries for frequently accessed event streams
5. Benchmark all optimized queries (before/after comparison)

**Non-Functional Requirements:**
- API endpoints: < 500ms response time (p95)
- Projection queries: < 100ms
- Report generation: < 3 seconds (10k rows)
- Index creation: zero downtime (CREATE INDEX CONCURRENTLY)
- Query plan documentation: stored in `docs/performance/query-plans.md`

**Data Model Changes:**
- Migration: `20260212_AddPerformanceIndexes.cs`
- Indexes added (see functional requirements)
- No schema changes (indexes only)

**API Changes:**
- No new endpoints
- Existing endpoints performance improved

### Acceptance Criteria

```gherkin
Feature: Query Optimization

Scenario: Slow query identification
  Given application running with query logging enabled
  When queries executed over 1 hour period
  Then slow query log contains all queries > 100ms
  And log includes query text, execution time, and call stack
  And queries sorted by total time (frequency × duration)

Scenario: Index creation for items table
  Given items table with 10,000 rows
  And no index on category_id
  When query "SELECT * FROM items WHERE category_id = 5" executed
  Then query takes > 50ms (sequential scan)
  When index idx_items_category_id created
  And query re-executed
  Then query takes < 5ms (index scan)
  And EXPLAIN ANALYZE shows "Index Scan using idx_items_category_id"

Scenario: N+1 query elimination in sales orders
  Given 100 sales orders with 5 lines each
  When GET /api/warehouse/v1/sales-orders executed without Include
  Then 101 queries executed (1 for orders + 100 for lines)
  And response time > 500ms
  When query optimized with .Include(o => o.Lines)
  And endpoint re-executed
  Then 1 query executed (with JOIN)
  And response time < 100ms

Scenario: Projection query optimization
  Given AvailableStockView with 50,000 rows
  When query "SELECT * FROM available_stock_view WHERE item_id = 123" executed without index
  Then query takes > 200ms
  When materialized view refreshed with index idx_available_stock_item_location
  And query re-executed
  Then query takes < 10ms

Scenario: Report generation performance
  Given on_hand_value_view with 10,000 rows
  When GET /api/warehouse/v1/reports/on-hand-value?category=Textile executed
  Then query filtered by category_id
  And index idx_on_hand_value_category_id used
  And response time < 2 seconds
  And CSV export generated with 2,500 rows

Scenario: Query plan documentation
  Given all optimized queries
  When EXPLAIN ANALYZE executed for each query
  Then query plans saved to docs/performance/query-plans.md
  And plans include: query text, execution time, index usage, row estimates
  And plans reviewed by DBA or senior developer
```

### Validation

```bash
# Enable slow query logging in PostgreSQL
psql -d warehouse -c "ALTER SYSTEM SET log_min_duration_statement = 100;"
psql -d warehouse -c "SELECT pg_reload_conf();"

# Run application under load (use k6 or similar)
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 50

# Analyze slow query log
psql -d warehouse -c "SELECT query, calls, total_time, mean_time FROM pg_stat_statements ORDER BY total_time DESC LIMIT 20;"

# Apply migration with indexes
cd src/LKvitai.MES.Infrastructure
dotnet ef migrations add AddPerformanceIndexes
dotnet ef database update

# Verify indexes created
psql -d warehouse -c "\d+ items"
psql -d warehouse -c "\d+ sales_orders"
psql -d warehouse -c "\d+ available_stock_view"

# Benchmark specific query (before/after)
psql -d warehouse -c "EXPLAIN ANALYZE SELECT * FROM items WHERE category_id = 5;"

# Run integration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~QueryPerformanceTests"

# Verify API response times
curl -w "@curl-format.txt" -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/warehouse/v1/sales-orders?status=ALLOCATED

# Expected output: time_total < 0.5s
```

### Definition of Done

- [ ] Slow query log analyzed (all queries > 100ms identified)
- [ ] Migration created with 10+ performance indexes
- [ ] Migration applied to dev/staging databases (zero downtime)
- [ ] EF Core queries optimized (N+1 patterns eliminated)
- [ ] Marten event queries optimized (compiled queries used)
- [ ] Query plans documented in `docs/performance/query-plans.md`
- [ ] Benchmark results recorded (before/after comparison)
- [ ] Integration tests added: `QueryPerformanceTests.cs` (5+ test cases)
- [ ] Load testing executed (k6 script, 50 VUs, 5 minutes)
- [ ] API response times validated: p95 < 500ms, p99 < 1s
- [ ] Projection queries validated: < 100ms
- [ ] Report generation validated: < 3 seconds
- [ ] Code review completed
- [ ] Documentation updated (performance tuning guide)

---
## Task PRD-1642: Caching Strategy

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Performance

### Context
Implement Redis-based caching to reduce database load and improve API response times. Target: 80%+ cache hit rate for read-heavy endpoints, cache latency < 5ms.

### Scope
**In Scope:**
- Redis integration (StackExchange.Redis)
- Cache-aside pattern implementation
- Cache invalidation on write operations
- TTL configuration per entity type
- Cache key naming strategy
- Cache hit/miss metrics

**Out of Scope:**
- Write-through caching (cache-aside only)
- Distributed cache coordination (single Redis instance)
- Cache warming strategies (Phase 2)

### Requirements

**Functional Requirements:**
1. Install and configure Redis:
   - Docker Compose: Redis 7.2 (single instance, 2GB memory limit)
   - Connection string: `localhost:6379` (dev), configurable via appsettings
   - Connection pooling: min=10, max=100 connections
2. Implement `ICacheService` interface:
   - `Task<T> GetAsync<T>(string key)`
   - `Task SetAsync<T>(string key, T value, TimeSpan ttl)`
   - `Task RemoveAsync(string key)`
   - `Task RemoveByPrefixAsync(string prefix)`
3. Cache the following entities:
   - Items: TTL=1 hour, key=`item:{id}`, invalidate on update/delete
   - Customers: TTL=30 minutes, key=`customer:{id}`, invalidate on update
   - Locations: TTL=2 hours, key=`location:{code}`, invalidate on update
   - AvailableStock projection: TTL=30 seconds, key=`stock:{itemId}:{locationId}`, invalidate on StockMoved
   - OnHandValue projection: TTL=5 minutes, key=`value:{itemId}`, invalidate on CostAdjusted
4. Implement cache invalidation:
   - On entity update: remove specific key
   - On StockMoved event: remove `stock:*` keys for affected item/location
   - On CostAdjusted event: remove `value:{itemId}` key
5. Add cache metrics:
   - Cache hit rate (gauge)
   - Cache miss rate (gauge)
   - Cache latency (histogram)
   - Cache size (gauge)

**Non-Functional Requirements:**
- Cache hit rate: > 80% for read-heavy endpoints
- Cache latency: < 5ms (p95)
- Redis memory usage: < 2GB
- Cache invalidation latency: < 100ms
- Fallback: if Redis unavailable, bypass cache (degrade gracefully)

**Data Model Changes:**
- No database schema changes
- Redis data structures: strings (JSON serialized entities)

**API Changes:**
- No new endpoints
- Existing endpoints use caching transparently

### Acceptance Criteria

```gherkin
Feature: Redis Caching

Scenario: Cache miss on first request
  Given Redis cache is empty
  When GET /api/warehouse/v1/items/123 executed
  Then item fetched from database
  And item stored in Redis with key "item:123"
  And TTL set to 1 hour
  And cache miss metric incremented

Scenario: Cache hit on subsequent request
  Given item 123 cached in Redis
  When GET /api/warehouse/v1/items/123 executed
  Then item fetched from Redis (no database query)
  And response time < 50ms
  And cache hit metric incremented

Scenario: Cache invalidation on item update
  Given item 123 cached in Redis
  When PUT /api/warehouse/v1/items/123 executed with updated data
  Then item updated in database
  And cache key "item:123" removed from Redis
  And next GET request fetches from database (cache miss)

Scenario: Stock projection cache invalidation
  Given AvailableStock cached for item 456, location A1-B1
  When StockMoved event published (item 456, from A1-B1 to A2-B2)
  Then cache keys removed: "stock:456:A1-B1", "stock:456:A2-B2"
  And next query fetches fresh data from projection

Scenario: Redis unavailable fallback
  Given Redis server is stopped
  When GET /api/warehouse/v1/items/123 executed
  Then cache bypassed (no exception thrown)
  And item fetched from database
  And response time < 200ms (acceptable degradation)
  And warning logged: "Redis unavailable, cache bypassed"

Scenario: Cache hit rate monitoring
  Given 100 requests to GET /api/warehouse/v1/items/{id}
  And 80 items already cached
  When requests executed
  Then cache hit rate = 80%
  And Prometheus metric cache_hit_rate{endpoint="items"} = 0.80
```

### Validation

```bash
# Start Redis via Docker Compose
docker-compose up -d redis

# Verify Redis running
redis-cli ping
# Expected: PONG

# Run application
cd src/LKvitai.MES.Api
dotnet run

# Test cache miss (first request)
time curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items/1
# Expected: ~100ms (database query)

# Verify item cached in Redis
redis-cli GET "item:1"
# Expected: JSON object

# Test cache hit (second request)
time curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items/1
# Expected: ~10ms (Redis fetch)

# Test cache invalidation
curl -X PUT -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Updated Item"}' http://localhost:5000/api/warehouse/v1/items/1

# Verify cache key removed
redis-cli GET "item:1"
# Expected: (nil)

# Check cache metrics
curl http://localhost:5000/metrics | grep cache_hit_rate

# Run integration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~CachingTests"

# Load test with caching enabled
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 100

# Monitor Redis memory usage
redis-cli INFO memory | grep used_memory_human
```

### Definition of Done

- [ ] Redis Docker Compose service added to `docker-compose.yml`
- [ ] StackExchange.Redis NuGet package installed
- [ ] `ICacheService` interface implemented in `src/LKvitai.MES.Infrastructure/Caching/RedisCacheService.cs`
- [ ] Cache-aside pattern applied to: Items, Customers, Locations, AvailableStock, OnHandValue
- [ ] Cache invalidation implemented for all write operations
- [ ] TTL configured per entity type (documented in code comments)
- [ ] Cache metrics exposed via Prometheus endpoint
- [ ] Integration tests added: `CachingTests.cs` (6+ test cases)
- [ ] Load testing executed with cache hit rate > 80%
- [ ] Redis memory usage validated < 2GB
- [ ] Fallback behavior tested (Redis unavailable)
- [ ] Code review completed
- [ ] Documentation updated: `docs/architecture/caching-strategy.md`

---
## Task PRD-1643: Connection Pooling

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Performance

### Context
Optimize PostgreSQL connection pooling to handle 1000+ concurrent users without connection exhaustion. Target: connection acquisition < 10ms, zero connection leaks.

### Scope
**In Scope:**
- Npgsql connection pooling configuration
- Connection pool size tuning (min/max)
- Connection leak detection and monitoring
- Connection lifetime management
- Pool exhaustion handling

**Out of Scope:**
- External connection poolers (PgBouncer) - Phase 2
- Connection multiplexing

### Requirements

**Functional Requirements:**
1. Configure Npgsql connection string:
   - `Minimum Pool Size=10` (keep warm connections)
   - `Maximum Pool Size=100` (prevent exhaustion)
   - `Connection Lifetime=300` (5 minutes, prevent stale connections)
   - `Connection Idle Lifetime=60` (1 minute, release idle connections)
   - `Timeout=30` (connection acquisition timeout)
2. Implement connection monitoring:
   - Active connections (gauge)
   - Idle connections (gauge)
   - Connection wait time (histogram)
   - Connection errors (counter)
3. Add connection leak detection:
   - Log warning if connection held > 30 seconds
   - Track connection stack traces in debug mode
4. Configure EF Core DbContext:
   - Scoped lifetime (one per HTTP request)
   - Dispose after request completion
   - No long-lived DbContext instances
5. Configure Marten DocumentStore:
   - Lightweight sessions (dispose after use)
   - No session caching

**Non-Functional Requirements:**
- Connection acquisition: < 10ms (p95)
- Pool exhaustion: never (under 1000 concurrent users)
- Connection leaks: zero (monitored via metrics)
- Connection reuse rate: > 90%

**Data Model Changes:**
- No schema changes
- Connection string updates in appsettings.json

**API Changes:**
- No new endpoints
- Improved connection handling in existing endpoints

### Acceptance Criteria

```gherkin
Feature: Connection Pooling

Scenario: Connection pool warm-up on startup
  Given application starting
  When DbContext first accessed
  Then 10 connections opened (Minimum Pool Size)
  And connections kept alive in pool
  And metric active_connections = 10

Scenario: Connection reuse under load
  Given 100 concurrent requests to API
  When requests execute database queries
  Then connections acquired from pool (< 10ms)
  And max 100 connections opened (Maximum Pool Size)
  And connections returned to pool after request
  And connection reuse rate > 90%

Scenario: Connection pool exhaustion prevention
  Given 150 concurrent requests (exceeds max pool size)
  When requests execute database queries
  Then first 100 requests acquire connections immediately
  And remaining 50 requests wait (up to 30s timeout)
  And no "connection pool exhausted" errors
  And requests complete when connections released

Scenario: Connection leak detection
  Given DbContext not disposed in controller
  When request completes
  Then connection held > 30 seconds
  And warning logged: "Potential connection leak detected"
  And stack trace logged (debug mode)

Scenario: Idle connection cleanup
  Given 50 connections in pool (idle for 2 minutes)
  When Connection Idle Lifetime expires
  Then idle connections closed (keep only Minimum Pool Size=10)
  And metric idle_connections = 10
```

### Validation

```bash
# Update connection string in appsettings.json
cat src/LKvitai.MES.Api/appsettings.Development.json
# Verify: "Minimum Pool Size=10;Maximum Pool Size=100;Connection Lifetime=300;..."

# Run application
cd src/LKvitai.MES.Api
dotnet run

# Monitor connection pool metrics
curl http://localhost:5000/metrics | grep npgsql_connection

# Load test (1000 concurrent users)
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 1000

# Check PostgreSQL active connections
psql -d warehouse -c "SELECT count(*) FROM pg_stat_activity WHERE datname='warehouse';"
# Expected: <= 100 (max pool size)

# Test connection leak detection (inject leak in test controller)
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/test/connection-leak
# Check logs for warning: "Potential connection leak detected"

# Run integration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~ConnectionPoolingTests"
```

### Definition of Done

- [ ] Connection string updated with pooling parameters
- [ ] Connection monitoring metrics added (active, idle, wait time, errors)
- [ ] Connection leak detection implemented (log warnings)
- [ ] EF Core DbContext lifetime verified (scoped, disposed)
- [ ] Marten session lifetime verified (lightweight, disposed)
- [ ] Integration tests added: `ConnectionPoolingTests.cs` (5+ test cases)
- [ ] Load testing executed (1000 VUs, zero pool exhaustion errors)
- [ ] Connection metrics validated via Prometheus
- [ ] Code review completed
- [ ] Documentation updated: `docs/performance/connection-pooling.md`

---

## Task PRD-1644: Async Operations

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Performance

### Context
Convert synchronous I/O-bound operations to async/await to improve thread pool utilization and scalability. Target: support 1000+ concurrent requests without thread starvation.

### Scope
**In Scope:**
- Convert sync database calls to async (EF Core, Marten)
- Convert sync HTTP calls to async (HttpClient)
- Convert sync file I/O to async
- Async/await best practices (ConfigureAwait, cancellation tokens)
- Deadlock prevention

**Out of Scope:**
- CPU-bound async operations (use Task.Run sparingly)
- Parallel processing (PLINQ, Parallel.ForEach) - Phase 2

### Requirements

**Functional Requirements:**
1. Audit all synchronous I/O operations:
   - Database: `.ToList()` → `.ToListAsync()`, `.FirstOrDefault()` → `.FirstOrDefaultAsync()`
   - HTTP: `HttpClient.Send()` → `HttpClient.SendAsync()`
   - File I/O: `File.ReadAllText()` → `File.ReadAllTextAsync()`
   - Marten: `.Query()` → `.QueryAsync()`
2. Update controller actions:
   - All actions return `Task<IActionResult>` or `Task<ActionResult<T>>`
   - Use `async`/`await` keywords
   - Pass `CancellationToken` from HTTP context
3. Update command handlers (MediatR):
   - All handlers implement `IRequestHandler<TRequest, Task<TResponse>>`
   - Use async database operations
4. Update saga steps (MassTransit):
   - All saga activities async
   - Use `ConsumeContext.CancellationToken`
5. Async best practices:
   - Use `ConfigureAwait(false)` in library code (not in ASP.NET Core controllers)
   - Avoid `async void` (except event handlers)
   - Avoid `.Result` or `.Wait()` (causes deadlocks)
   - Use `ValueTask<T>` for hot paths (if applicable)

**Non-Functional Requirements:**
- Thread pool utilization: < 50% under load
- Request throughput: 2x improvement (sync vs async)
- No deadlocks (validated via stress testing)
- Cancellation token propagation: 100% of async operations

**Data Model Changes:**
- No schema changes

**API Changes:**
- All endpoints async (transparent to clients)

### Acceptance Criteria

```gherkin
Feature: Async Operations

Scenario: Async database query
  Given controller action GetItemById
  When action invoked with id=123
  Then DbContext.Items.FindAsync(123) called (not Find)
  And thread released while waiting for database
  And response returned when query completes

Scenario: Async HTTP call to external API
  Given FedExApiService.GetTrackingAsync method
  When method invoked with tracking number
  Then HttpClient.SendAsync used (not Send)
  And thread released while waiting for HTTP response
  And result returned when response received

Scenario: Cancellation token propagation
  Given async operation in progress
  When HTTP request cancelled by client
  Then CancellationToken.IsCancellationRequested = true
  And database query cancelled
  And operation aborted gracefully

Scenario: No deadlocks under load
  Given 1000 concurrent requests
  When all requests execute async operations
  Then no thread pool starvation
  And no deadlocks detected
  And all requests complete successfully

Scenario: Thread pool utilization improvement
  Given 500 concurrent requests (sync implementation)
  When requests execute
  Then thread pool utilization = 90% (threads blocked on I/O)
  Given 500 concurrent requests (async implementation)
  When requests execute
  Then thread pool utilization = 40% (threads released during I/O)
  And throughput increased by 2x
```

### Validation

```bash
# Audit sync operations (find .Result, .Wait(), .ToList())
cd src/LKvitai.MES.Api
grep -r "\.Result\|\.Wait()\|\.ToList()" --include="*.cs" | grep -v "Async"
# Expected: zero occurrences (all converted to async)

# Run application
dotnet run

# Monitor thread pool metrics
curl http://localhost:5000/metrics | grep threadpool

# Load test (async vs sync comparison)
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 500

# Check thread pool utilization
dotnet-counters monitor --process-id $(pgrep -f LKvitai.MES.Api) \
  --counters System.Runtime[threadpool-thread-count,threadpool-queue-length]

# Test cancellation token propagation
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items &
# Kill request after 1 second
sleep 1 && pkill curl
# Check logs: "Operation cancelled" (no exception)

# Run integration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~AsyncOperationTests"
```

### Definition of Done

- [ ] All database operations converted to async (EF Core, Marten)
- [ ] All HTTP calls converted to async (HttpClient)
- [ ] All file I/O converted to async
- [ ] All controller actions async
- [ ] All MediatR handlers async
- [ ] All MassTransit saga activities async
- [ ] Cancellation tokens propagated (100% of async operations)
- [ ] Async best practices applied (ConfigureAwait, no .Result/.Wait())
- [ ] Integration tests added: `AsyncOperationTests.cs` (5+ test cases)
- [ ] Load testing executed (2x throughput improvement validated)
- [ ] Thread pool utilization validated (< 50% under load)
- [ ] Code review completed (focus on deadlock prevention)
- [ ] Documentation updated: `docs/performance/async-patterns.md`

---
## Task PRD-1645: Load Balancing

**Epic:** Performance | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** None | **SourceRefs:** Universe §5.Performance

### Context
Configure Nginx load balancer for horizontal scaling and high availability. Target: support 2000+ concurrent users, zero downtime deployments.

### Scope
**In Scope:**
- Nginx reverse proxy configuration
- Round-robin load balancing across 3 API instances
- Health check endpoints
- Session affinity (sticky sessions) for SignalR
- Horizontal scaling validation

**Out of Scope:**
- Auto-scaling (Kubernetes HPA) - Phase 2
- Global load balancing (multi-region) - Phase 2

### Requirements

**Functional Requirements:**
1. Nginx configuration (`/etc/nginx/nginx.conf`):
   - Upstream block with 3 API instances (localhost:5001, 5002, 5003)
   - Round-robin load balancing algorithm
   - Health checks: `max_fails=3`, `fail_timeout=30s`
   - Keepalive connections: `keepalive 32`
2. Health check endpoint: `GET /health` (returns 200 OK if healthy)
3. Session affinity for SignalR:
   - Use `ip_hash` directive for `/hubs/*` paths
   - Ensures WebSocket connections stick to same instance
4. Docker Compose configuration:
   - 3 API containers (api-1, api-2, api-3)
   - 1 Nginx container (load balancer)
   - Shared PostgreSQL and Redis instances
5. Horizontal scaling test:
   - Start with 1 instance, add 2 more
   - Verify traffic distributed evenly
   - Verify zero dropped requests during scale-up

**Non-Functional Requirements:**
- Load distribution: ±10% variance across instances
- Health check interval: 10 seconds
- Failover time: < 30 seconds (when instance fails)
- Zero downtime during rolling deployments

**Infrastructure Changes:**
- `docker-compose.yml`: add api-1, api-2, api-3, nginx services
- `nginx.conf`: load balancer configuration

**API Changes:**
- Health check endpoint: `GET /health` (returns `{ "status": "healthy", "timestamp": "..." }`)

### Acceptance Criteria

```gherkin
Feature: Load Balancing

Scenario: Round-robin distribution
  Given 3 API instances running (api-1, api-2, api-3)
  And Nginx load balancer configured
  When 300 requests sent to http://localhost/api/warehouse/v1/items
  Then requests distributed: api-1=100, api-2=100, api-3=100 (±10)
  And all requests return 200 OK

Scenario: Health check and failover
  Given 3 API instances running
  When api-2 stopped (simulated failure)
  Then Nginx detects failure after 3 failed health checks (30 seconds)
  And traffic routed only to api-1 and api-3
  And zero 502 Bad Gateway errors after failover

Scenario: Instance recovery
  Given api-2 failed and removed from pool
  When api-2 restarted
  Then Nginx detects healthy status after 1 successful health check
  And traffic routed to all 3 instances again

Scenario: Session affinity for SignalR
  Given client connects to WebSocket at /hubs/warehouse
  When connection established to api-1
  Then subsequent messages routed to api-1 (same instance)
  And connection not dropped during load balancing

Scenario: Zero downtime deployment
  Given 3 API instances running under load (100 req/s)
  When rolling deployment executed (stop api-1, deploy, start api-1, repeat for api-2, api-3)
  Then zero 502 errors during deployment
  And all requests return 200 OK or 503 (graceful degradation)
```

### Validation

```bash
# Build and start all services
docker-compose up -d --scale api=3

# Verify 3 API instances running
docker-compose ps | grep api
# Expected: api-1, api-2, api-3 (all "Up")

# Verify Nginx running
curl http://localhost/health
# Expected: 200 OK

# Test load distribution
for i in {1..300}; do
  curl -s http://localhost/api/warehouse/v1/items | grep -o "api-[0-9]" >> /tmp/distribution.txt
done
sort /tmp/distribution.txt | uniq -c
# Expected: ~100 requests per instance

# Test health check and failover
docker-compose stop api-2
sleep 35  # Wait for health check to detect failure
curl http://localhost/api/warehouse/v1/items
# Expected: 200 OK (routed to api-1 or api-3)

# Restart api-2
docker-compose start api-2
sleep 15  # Wait for health check to detect recovery
curl http://localhost/api/warehouse/v1/items
# Expected: 200 OK (may route to api-2)

# Load test with load balancer
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 500

# Monitor Nginx access logs
docker-compose logs -f nginx | grep "upstream"
```

### Definition of Done

- [ ] Nginx configuration file created: `nginx.conf`
- [ ] Docker Compose updated with 3 API instances + Nginx
- [ ] Health check endpoint implemented: `GET /health`
- [ ] Round-robin load balancing validated (±10% variance)
- [ ] Health check and failover tested (< 30s failover time)
- [ ] Session affinity validated for SignalR
- [ ] Zero downtime deployment tested (rolling update)
- [ ] Load testing executed (500 VUs, 5 minutes, zero errors)
- [ ] Nginx access logs reviewed (traffic distribution confirmed)
- [ ] Code review completed
- [ ] Documentation updated: `docs/deployment/load-balancing.md`

---

## Task PRD-1646: APM Integration

**Epic:** Monitoring | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1545 | **SourceRefs:** Universe §5.Observability

### Context
Integrate Application Performance Monitoring (APM) for distributed tracing, performance profiling, and error tracking. Target: 100% request tracing, < 5% overhead.

### Scope
**In Scope:**
- Application Insights or New Relic integration
- Distributed tracing (OpenTelemetry)
- Performance profiling (slow requests, database queries)
- Error tracking and alerting
- Custom telemetry (business metrics)

**Out of Scope:**
- Real User Monitoring (RUM) - Phase 2
- Synthetic monitoring - Phase 2

### Requirements

**Functional Requirements:**
1. Install APM SDK:
   - Application Insights: `Microsoft.ApplicationInsights.AspNetCore` NuGet package
   - OR New Relic: `NewRelic.Agent` NuGet package
2. Configure instrumentation key (appsettings.json):
   - `ApplicationInsights:InstrumentationKey` (from Azure portal)
   - OR `NewRelic:LicenseKey` (from New Relic account)
3. Enable automatic instrumentation:
   - HTTP requests (incoming/outgoing)
   - Database queries (EF Core, Marten)
   - Exceptions and errors
   - Dependencies (Redis, external APIs)
4. Add custom telemetry:
   - Business events: `OrderCreated`, `ShipmentDispatched`, `StockAdjusted`
   - Custom metrics: `OrdersPerHour`, `PickingDuration`, `PackingDuration`
   - Custom dimensions: `UserId`, `WarehouseCode`, `OrderType`
5. Configure sampling:
   - 100% sampling for errors
   - 10% sampling for successful requests (reduce cost)
   - Adaptive sampling based on traffic volume
6. Distributed tracing:
   - Propagate correlation ID across services
   - Trace saga execution (MassTransit integration)
   - Trace external API calls (FedEx, Agnum)

**Non-Functional Requirements:**
- APM overhead: < 5% (CPU, memory, latency)
- Trace retention: 30 days
- Error alerting: < 1 minute (from error to alert)
- Dashboard load time: < 3 seconds

**Infrastructure Changes:**
- Application Insights workspace (Azure) OR New Relic account
- APM SDK installed in API project

**API Changes:**
- No new endpoints
- Telemetry added to existing endpoints

### Acceptance Criteria

```gherkin
Feature: APM Integration

Scenario: Automatic request tracing
  Given Application Insights configured
  When GET /api/warehouse/v1/items/123 executed
  Then request traced with:
    - Request ID (correlation ID)
    - Duration (ms)
    - Response code (200)
    - User ID (from auth token)
  And trace visible in Application Insights portal within 1 minute

Scenario: Database query tracing
  Given request traced
  When EF Core query executed (SELECT * FROM items WHERE id=123)
  Then query traced as dependency with:
    - Query text (parameterized)
    - Duration (ms)
    - Success/failure status
  And query linked to parent request trace

Scenario: Exception tracking
  Given request execution
  When unhandled exception thrown (NullReferenceException)
  Then exception logged to Application Insights with:
    - Exception type and message
    - Stack trace
    - Request context (URL, user, timestamp)
  And alert triggered (if configured)

Scenario: Custom business event
  Given sales order created
  When OrderCreated event published
  Then custom telemetry sent to Application Insights:
    - Event name: "OrderCreated"
    - Properties: { OrderId, CustomerId, TotalAmount, OrderDate }
  And event visible in custom events dashboard

Scenario: Distributed tracing across saga
  Given sales order allocation saga
  When saga executes (create reservation, allocate stock, update order)
  Then all saga steps traced with same correlation ID
  And trace shows: API request → Saga start → Reservation created → Stock allocated → Order updated
  And end-to-end duration calculated

Scenario: Performance profiling
  Given slow request (> 1 second)
  When request traced
  Then Application Insights shows:
    - Total duration: 1.2s
    - Database query: 0.8s (slow query identified)
    - External API call: 0.3s
    - Application code: 0.1s
  And slow query flagged for optimization
```

### Validation

```bash
# Install Application Insights SDK
cd src/LKvitai.MES.Api
dotnet add package Microsoft.ApplicationInsights.AspNetCore

# Configure instrumentation key in appsettings.json
cat appsettings.json | grep ApplicationInsights
# Expected: "InstrumentationKey": "your-key-here"

# Run application
dotnet run

# Execute test requests
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items/1

# Verify telemetry in Application Insights portal
# Navigate to: Azure Portal → Application Insights → Transaction search
# Filter: Last 30 minutes, Operation name: "GET /api/warehouse/v1/items/{id}"
# Expected: Request trace with duration, dependencies, custom properties

# Trigger exception
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/test/throw-exception

# Verify exception in Application Insights
# Navigate to: Failures → Exceptions
# Expected: NullReferenceException with stack trace

# Run load test
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 100

# Check APM overhead
# Compare: CPU/memory usage with APM enabled vs disabled
# Expected: < 5% overhead

# Run integration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~APMIntegrationTests"
```

### Definition of Done

- [ ] Application Insights SDK installed and configured
- [ ] Instrumentation key configured (appsettings.json)
- [ ] Automatic instrumentation enabled (HTTP, database, exceptions)
- [ ] Custom telemetry added (business events, custom metrics)
- [ ] Distributed tracing validated (correlation ID propagation)
- [ ] Sampling configured (100% errors, 10% success)
- [ ] APM overhead validated (< 5%)
- [ ] Integration tests added: `APMIntegrationTests.cs` (6+ test cases)
- [ ] Load testing executed (telemetry verified in portal)
- [ ] Dashboard created in Application Insights (requests, dependencies, exceptions)
- [ ] Alerts configured (error rate > 1%, response time > 1s)
- [ ] Code review completed
- [ ] Documentation updated: `docs/observability/apm-integration.md`

---
## Task PRD-1647: Custom Dashboards

**Epic:** Monitoring | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1545 | **SourceRefs:** Universe §5.Observability

### Context
Create Grafana dashboards for business metrics, SLAs, and operational monitoring. Target: 5 dashboards covering all critical metrics.

### Scope
**In Scope:** Grafana dashboards (business metrics, SLA tracking, error rates, system health, capacity planning), dashboard templates, sharing/export

**Out of Scope:** Custom Grafana plugins, advanced alerting (covered in PRD-1648)

### Requirements
**Functional:**
1. Business Metrics Dashboard: Orders/hour, picks/hour, shipments/hour, on-hand value, stock movements/day
2. SLA Dashboard: API response time (p50/p95/p99), uptime %, error rate, projection lag
3. System Health Dashboard: CPU/memory/disk usage, database connections, Redis hit rate, queue depth
4. Error Dashboard: Error count by type, error rate trend, top 10 errors, failed saga count
5. Capacity Planning Dashboard: Growth trends (30/60/90 days), resource utilization forecast, storage capacity

**Non-Functional:** Dashboard load time < 3s, auto-refresh every 30s, 30-day data retention

**Acceptance Criteria:**
```gherkin
Scenario: Business metrics dashboard displays real-time data
  Given Grafana dashboard "Business Metrics" loaded
  When data refreshed
  Then panels show: Orders created (last hour)=45, Picks completed=120, Shipments dispatched=38
  And graphs show trends (last 24 hours)
  And data refreshed every 30 seconds

Scenario: SLA dashboard shows performance metrics
  Given SLA dashboard loaded
  Then API response time panel shows: p50=120ms, p95=450ms, p99=890ms
  And uptime panel shows: 99.95% (last 30 days)
  And error rate panel shows: 0.12% (last hour)
  And all metrics color-coded (green < SLA, red > SLA)

Scenario: Alert triggered from dashboard
  Given error rate > 1% threshold
  When dashboard refreshed
  Then error rate panel shows red
  And alert annotation visible on graph
  And PagerDuty notification sent (if configured)
```

**Validation:**
```bash
# Start Grafana
docker-compose up -d grafana

# Import dashboards
curl -X POST http://admin:admin@localhost:3000/api/dashboards/db \
  -H "Content-Type: application/json" \
  -d @grafana/dashboards/business-metrics.json

# Verify dashboards
curl http://admin:admin@localhost:3000/api/dashboards | jq '.[] | .title'
# Expected: Business Metrics, SLA Tracking, System Health, Errors, Capacity Planning

# Load test to generate metrics
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 100

# View dashboards in browser
open http://localhost:3000/d/business-metrics
```

**Definition of Done:**
- [ ] 5 Grafana dashboards created (JSON files in `grafana/dashboards/`)
- [ ] Dashboards imported to Grafana instance
- [ ] All panels configured with Prometheus data sources
- [ ] Auto-refresh enabled (30s interval)
- [ ] Color thresholds configured (green/yellow/red)
- [ ] Dashboard templates exported for reuse
- [ ] Load testing executed (dashboards display real-time data)
- [ ] Screenshots captured for documentation
- [ ] Code review completed
- [ ] Documentation updated: `docs/observability/grafana-dashboards.md`

---

## Task PRD-1648: Alert Escalation

**Epic:** Monitoring | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1546 | **SourceRefs:** Universe §5.Observability

### Context
Configure PagerDuty/Opsgenie for alert escalation and on-call management. Target: < 5 minute alert response time.

### Scope
**In Scope:** PagerDuty integration, escalation policies, on-call schedules, alert routing, incident management

**Out of Scope:** Custom incident response workflows (Phase 2)

### Requirements
**Functional:**
1. PagerDuty service integration (API key in appsettings.json)
2. Alert routing rules: Critical → Page on-call engineer, Warning → Email team, Info → Slack channel
3. Escalation policy: L1 (5 min) → L2 (15 min) → L3 Manager (30 min)
4. On-call schedule: 24/7 rotation (weekly shifts)
5. Alert deduplication (same alert within 5 minutes = single incident)

**Non-Functional:** Alert delivery < 1 minute, escalation timing accurate ±30 seconds, 99.9% delivery reliability

**Acceptance Criteria:**
```gherkin
Scenario: Critical alert triggers page
  Given error rate > 5% (critical threshold)
  When alert fired from Prometheus
  Then PagerDuty incident created (severity=critical)
  And on-call engineer paged (SMS + phone call)
  And incident visible in PagerDuty dashboard within 1 minute

Scenario: Escalation to L2 if no acknowledgment
  Given critical incident created
  When L1 engineer does not acknowledge within 5 minutes
  Then incident escalated to L2 engineer
  And L2 engineer paged
  And escalation logged in incident timeline

Scenario: Alert deduplication
  Given error rate > 5% alert fired at 10:00:00
  When same alert fired again at 10:02:00
  Then no new incident created (deduplicated)
  And existing incident updated with new occurrence count

Scenario: Alert resolution
  Given critical incident open
  When error rate drops below 5%
  Then auto-resolve alert sent to PagerDuty
  And incident status changed to "Resolved"
  And on-call engineer notified of resolution
```

**Validation:**
```bash
# Configure PagerDuty integration key
cat src/LKvitai.MES.Api/appsettings.json | grep PagerDuty
# Expected: "ApiKey": "your-pagerduty-key"

# Test alert (trigger critical error rate)
curl -X POST http://localhost:9090/api/v1/alerts \
  -d '[{"labels":{"alertname":"HighErrorRate","severity":"critical"},"annotations":{"summary":"Error rate > 5%"}}]'

# Verify incident in PagerDuty
curl -H "Authorization: Token token=YOUR_API_KEY" \
  https://api.pagerduty.com/incidents?statuses[]=triggered
# Expected: Incident with title "HighErrorRate"

# Test escalation (wait 5 minutes without acknowledgment)
# Verify L2 engineer paged

# Test alert resolution
curl -X POST http://localhost:9090/api/v1/alerts \
  -d '[{"labels":{"alertname":"HighErrorRate","severity":"critical"},"endsAt":"2026-02-12T10:00:00Z"}]'

# Verify incident resolved in PagerDuty
```

**Definition of Done:**
- [ ] PagerDuty service created and API key configured
- [ ] Alert routing rules configured (critical/warning/info)
- [ ] Escalation policy created (L1 → L2 → L3)
- [ ] On-call schedule configured (24/7 rotation)
- [ ] Alert deduplication enabled (5-minute window)
- [ ] Integration tested (alert → incident → escalation → resolution)
- [ ] On-call team trained on incident response
- [ ] Runbook links added to alert annotations
- [ ] Code review completed
- [ ] Documentation updated: `docs/observability/alert-escalation.md`

---

## Task PRD-1649: SLA Monitoring

**Epic:** Monitoring | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** PRD-1568 | **SourceRefs:** Universe §5.Observability

### Context
Define and monitor SLAs for API response time, uptime, and business operations. Target: 99.9% uptime, API p95 < 500ms.

### Scope
**In Scope:** SLA definitions, SLA tracking metrics, breach alerting, SLA reports

**Out of Scope:** SLA credits/penalties (business process)

### Requirements
**Functional:**
1. SLA definitions:
   - API uptime: 99.9% (monthly)
   - API response time: p95 < 500ms, p99 < 1s
   - Projection lag: < 1 second
   - Order fulfillment: 95% shipped within 24 hours
2. SLA tracking metrics (Prometheus):
   - `sla_uptime_percentage` (gauge)
   - `sla_api_response_time_p95` (gauge)
   - `sla_projection_lag_seconds` (gauge)
   - `sla_order_fulfillment_rate` (gauge)
3. SLA breach alerting:
   - Uptime < 99.9% → Critical alert
   - API p95 > 500ms → Warning alert
   - Projection lag > 5s → Warning alert
4. SLA reports:
   - Monthly SLA report (PDF): uptime %, API performance, breach incidents
   - SLA dashboard (Grafana): real-time SLA status

**Non-Functional:** SLA calculation accuracy 100%, report generation < 10 seconds

**Acceptance Criteria:**
```gherkin
Scenario: SLA uptime tracking
  Given API running for 30 days
  And total downtime = 43 minutes (0.1% of month)
  When SLA uptime calculated
  Then sla_uptime_percentage = 99.9%
  And SLA met (no breach)

Scenario: SLA breach alert for API response time
  Given API p95 response time = 650ms (exceeds 500ms SLA)
  When SLA check executed
  Then sla_api_response_time_p95 = 650
  And alert fired: "SLA breach: API response time p95 > 500ms"
  And incident created in PagerDuty

Scenario: SLA report generation
  Given month ended (January 2026)
  When SLA report generated
  Then PDF report created with:
    - Uptime: 99.95% (SLA met)
    - API p95: 420ms (SLA met)
    - Projection lag: 0.8s (SLA met)
    - Breach incidents: 2 (details included)
  And report emailed to stakeholders

Scenario: Order fulfillment SLA tracking
  Given 100 orders created on 2026-02-10
  And 96 orders shipped within 24 hours
  When SLA calculated
  Then sla_order_fulfillment_rate = 0.96 (96%)
  And SLA met (> 95% threshold)
```

**Validation:**
```bash
# Verify SLA metrics exposed
curl http://localhost:5000/metrics | grep sla_
# Expected: sla_uptime_percentage, sla_api_response_time_p95, etc.

# Run load test
k6 run scripts/load/warehouse-load-smoke.js --duration 1h --vus 100

# Check SLA metrics
curl http://localhost:5000/metrics | grep sla_api_response_time_p95
# Expected: value < 500

# Generate SLA report
curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/admin/sla/report?month=2026-01 \
  -o sla-report-2026-01.pdf

# Verify report contents
pdftotext sla-report-2026-01.pdf - | grep "Uptime:"
# Expected: "Uptime: 99.95%"

# Run integration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~SLAMonitoringTests"
```

**Definition of Done:**
- [ ] SLA definitions documented in `docs/sla/sla-definitions.md`
- [ ] SLA tracking metrics implemented (4+ metrics)
- [ ] SLA breach alerts configured (Prometheus alert rules)
- [ ] SLA report generation endpoint implemented
- [ ] SLA dashboard created in Grafana
- [ ] Integration tests added: `SLAMonitoringTests.cs` (4+ test cases)
- [ ] Load testing executed (SLA metrics validated)
- [ ] Monthly SLA report generated and reviewed
- [ ] Code review completed
- [ ] Documentation updated: `docs/observability/sla-monitoring.md`

---

## Task PRD-1650: Capacity Planning

**Epic:** Monitoring | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1649 | **SourceRefs:** Universe §5.Observability

### Context
Implement capacity planning metrics and forecasting to prevent resource exhaustion. Target: 6-month growth projection, alerts at 80% capacity.

### Scope
**In Scope:** Resource utilization trends, growth projections, capacity alerts, scaling recommendations

**Out of Scope:** Auto-scaling implementation (Phase 2)

### Requirements
**Functional:**
1. Capacity metrics:
   - Database size growth (GB/month)
   - Event store growth (events/day)
   - API request volume growth (requests/hour)
   - Storage capacity (warehouse locations utilization %)
2. Growth projections (linear regression):
   - 30-day trend → 6-month forecast
   - Database: current 50GB → projected 120GB (6 months)
   - Events: current 10k/day → projected 25k/day
3. Capacity alerts:
   - Database size > 80% of allocated storage → Warning
   - Location utilization > 90% → Critical
   - Event store > 1M events/day → Warning (performance impact)
4. Scaling recommendations:
   - Database: upgrade to next tier when > 80% capacity
   - API: add instance when CPU > 70% sustained
   - Storage: add locations when utilization > 85%

**Non-Functional:** Forecast accuracy ±20%, alert latency < 5 minutes

**Acceptance Criteria:**
```gherkin
Scenario: Database growth projection
  Given database size history (last 30 days): 45GB → 50GB
  When capacity forecast calculated
  Then projected size (6 months) = 120GB
  And growth rate = 5GB/month
  And recommendation: "Upgrade to 150GB tier in 4 months"

Scenario: Capacity alert for location utilization
  Given warehouse has 1000 locations
  And 920 locations occupied (92% utilization)
  When capacity check executed
  Then alert fired: "Location capacity > 90%"
  And recommendation: "Add 200 locations or optimize putaway"

Scenario: Event store growth monitoring
  Given event store contains 5M events
  And growth rate = 15k events/day
  When forecast calculated
  Then projected events (6 months) = 7.7M
  And storage impact = +2.5GB
  And no action required (within capacity)

Scenario: API scaling recommendation
  Given API CPU usage = 75% (sustained for 1 hour)
  When capacity check executed
  Then alert fired: "API CPU > 70% sustained"
  And recommendation: "Add 1 API instance (current: 3, recommended: 4)"
```

**Validation:**
```bash
# Check capacity metrics
curl http://localhost:5000/metrics | grep capacity_
# Expected: capacity_database_size_gb, capacity_event_count, capacity_location_utilization_percent

# Generate capacity report
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/admin/capacity/report \
  -o capacity-report.json

# Verify forecast
cat capacity-report.json | jq '.forecasts'
# Expected: { "database_size_6m": 120, "events_per_day_6m": 25000, ... }

# Test capacity alert
# Simulate high location utilization (occupy 920/1000 locations)
curl -X POST -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/test/simulate-capacity-alert?type=location&utilization=92

# Verify alert fired
curl http://localhost:9090/api/v1/alerts | jq '.data.alerts[] | select(.labels.alertname=="HighLocationUtilization")'

# Run integration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~CapacityPlanningTests"
```

**Definition of Done:**
- [ ] Capacity metrics implemented (database, events, API, storage)
- [ ] Growth projection algorithm implemented (linear regression)
- [ ] Capacity alerts configured (80% warning, 90% critical)
- [ ] Scaling recommendations documented
- [ ] Capacity report endpoint implemented
- [ ] Capacity dashboard created in Grafana
- [ ] Integration tests added: `CapacityPlanningTests.cs` (4+ test cases)
- [ ] 30-day historical data collected (for accurate forecasts)
- [ ] Capacity report generated and reviewed
- [ ] Code review completed
- [ ] Documentation updated: `docs/observability/capacity-planning.md`

---
## Task PRD-1651: E2E Test Suite Expansion

**Epic:** Integration Testing | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** L (2 days)
**OwnerType:** QA | **Dependencies:** PRD-1540 | **SourceRefs:** Universe §5.Testing

### Context
Expand E2E test coverage to all workflows with data-driven tests and parallel execution. Target: 90%+ workflow coverage, < 10 minute test suite execution.

### Scope
**In Scope:** E2E tests for all workflows (inbound, outbound, valuation, cycle count, transfers), data-driven test framework, parallel execution, CI integration

**Out of Scope:** UI automation tests (Selenium) - Phase 2

### Requirements
**Functional:**
1. E2E test scenarios (xUnit):
   - Inbound workflow: Create shipment → Receive → QC → Putaway (5 scenarios)
   - Outbound workflow: Create order → Allocate → Pick → Pack → Dispatch (7 scenarios)
   - Valuation workflow: Adjust cost → Allocate landed cost → Write-down (4 scenarios)
   - Cycle count workflow: Schedule → Execute → Resolve discrepancy (3 scenarios)
   - Transfer workflow: Create → Approve → Execute (3 scenarios)
2. Data-driven tests:
   - Test data in JSON files (`tests/data/inbound-scenarios.json`)
   - Parameterized tests (Theory attribute)
   - 50+ test cases from 20 test methods
3. Parallel execution:
   - xUnit parallel test execution (max 4 threads)
   - Isolated test databases (one per thread)
4. CI integration:
   - GitHub Actions workflow (`.github/workflows/e2e-tests.yml`)
   - Run on every PR and main branch push
   - Fail build if tests fail

**Non-Functional:** Test suite execution < 10 minutes, 90%+ workflow coverage, zero flaky tests

**Acceptance Criteria:**
```gherkin
Scenario: Inbound E2E test with data-driven scenarios
  Given test data file "inbound-scenarios.json" with 10 scenarios
  When E2E test "InboundWorkflowTests.ReceiveAndPutaway" executed
  Then all 10 scenarios executed (Theory attribute)
  And each scenario: Create shipment → Receive → QC → Putaway
  And all assertions pass (stock moved, HU created, projection updated)

Scenario: Parallel test execution
  Given 50 E2E tests in test suite
  When tests executed with xUnit parallel runner (4 threads)
  Then tests run in parallel (4 concurrent tests)
  And each test uses isolated database (test-db-1, test-db-2, test-db-3, test-db-4)
  And total execution time < 10 minutes (vs 30 minutes sequential)

Scenario: CI integration
  Given PR created with code changes
  When GitHub Actions workflow triggered
  Then E2E test suite executed in CI environment
  And test results reported in PR (pass/fail)
  And PR blocked if tests fail
```

**Validation:**
```bash
# Run E2E test suite locally
cd src/LKvitai.MES.Tests.E2E
dotnet test --logger "console;verbosity=detailed"

# Run with parallel execution
dotnet test --logger "console;verbosity=detailed" -- xUnit.ParallelizeTestCollections=true xUnit.MaxParallelThreads=4

# Measure execution time
time dotnet test

# Run specific workflow tests
dotnet test --filter "FullyQualifiedName~InboundWorkflowTests"
dotnet test --filter "FullyQualifiedName~OutboundWorkflowTests"

# Verify test coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=opencover
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage-report
open coverage-report/index.html
# Expected: > 90% workflow coverage

# Run in CI (GitHub Actions)
git push origin feature/e2e-tests
# Check: https://github.com/your-repo/actions
```

**Definition of Done:**
- [ ] E2E test project created: `src/LKvitai.MES.Tests.E2E`
- [ ] 20+ test methods covering all workflows
- [ ] Data-driven test framework implemented (JSON test data)
- [ ] 50+ test scenarios executed via Theory attribute
- [ ] Parallel execution configured (4 threads, isolated databases)
- [ ] Test execution time < 10 minutes
- [ ] Workflow coverage > 90% (measured via code coverage tool)
- [ ] CI workflow configured (`.github/workflows/e2e-tests.yml`)
- [ ] All tests passing in CI
- [ ] Zero flaky tests (3 consecutive runs, 100% pass rate)
- [ ] Code review completed
- [ ] Documentation updated: `docs/testing/e2e-test-suite.md`

---

## Task PRD-1652: Chaos Engineering

**Epic:** Integration Testing | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** QA | **Dependencies:** None | **SourceRefs:** Universe §5.Testing

### Context
Implement chaos engineering tests to validate system resilience under failure conditions. Target: zero data loss, graceful degradation.

### Scope
**In Scope:** Chaos Monkey integration, failure injection (network, database, Redis), resilience validation

**Out of Scope:** Production chaos testing (staging only)

### Requirements
**Functional:**
1. Chaos testing scenarios:
   - Database connection failure (kill PostgreSQL)
   - Redis cache failure (kill Redis)
   - Network partition (block API → database traffic)
   - High latency injection (add 500ms delay to database queries)
   - Message queue failure (kill RabbitMQ)
2. Resilience validation:
   - API returns 503 Service Unavailable (not 500 Internal Server Error)
   - Retry logic executes (exponential backoff)
   - Circuit breaker opens after 3 failures
   - Graceful degradation (cache miss → database fallback)
   - Zero data loss (transactions rolled back on failure)
3. Chaos testing framework:
   - Simmy (Polly chaos extension) for .NET
   - Chaos policies: fault injection, latency injection, result injection
4. Automated chaos tests (xUnit):
   - `ChaosTests.DatabaseFailure`
   - `ChaosTests.RedisFailure`
   - `ChaosTests.NetworkPartition`
   - `ChaosTests.HighLatency`

**Non-Functional:** Zero data corruption, graceful degradation, recovery time < 30 seconds

**Acceptance Criteria:**
```gherkin
Scenario: Database failure with graceful degradation
  Given API running with database connection
  When PostgreSQL killed (simulated failure)
  Then API returns 503 Service Unavailable
  And error logged: "Database connection failed"
  And retry logic executes (3 attempts with exponential backoff)
  And circuit breaker opens after 3 failures
  And subsequent requests fail fast (no retry)
  When PostgreSQL restarted
  Then circuit breaker closes after 1 successful health check
  And API resumes normal operation

Scenario: Redis failure with cache bypass
  Given API using Redis cache
  When Redis killed
  Then cache operations fail gracefully
  And API falls back to database queries
  And response time increases (acceptable degradation)
  And zero 500 errors (all requests return 200 or 503)

Scenario: High latency injection
  Given chaos policy injecting 500ms latency to database queries
  When API request executed
  Then query takes 500ms longer than normal
  And request timeout not exceeded (30s timeout)
  And response returned successfully

Scenario: Zero data loss validation
  Given transaction in progress (create sales order)
  When database connection lost mid-transaction
  Then transaction rolled back
  And sales order not created (database consistent)
  And idempotency key preserved (retry safe)
```

**Validation:**
```bash
# Install Simmy (Polly chaos extension)
cd src/LKvitai.MES.Api
dotnet add package Polly.Contrib.Simmy

# Run chaos tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~ChaosTests"

# Manual chaos test: Kill database
docker-compose stop postgres
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items
# Expected: 503 Service Unavailable

# Restart database
docker-compose start postgres
sleep 10
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items
# Expected: 200 OK (recovered)

# Manual chaos test: Kill Redis
docker-compose stop redis
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items/1
# Expected: 200 OK (cache bypassed, database fallback)

# Verify zero data loss
# Create order during database failure
curl -X POST -H "Authorization: Bearer $TOKEN" \
  -d '{"customerId":1,"lines":[{"itemId":1,"qty":10}]}' \
  http://localhost:5000/api/warehouse/v1/sales-orders
# Expected: 503 (transaction rolled back)

# Verify order not created
psql -d warehouse -c "SELECT count(*) FROM sales_orders;"
# Expected: count unchanged
```

**Definition of Done:**
- [ ] Simmy (Polly chaos extension) installed
- [ ] Chaos policies configured (fault, latency, result injection)
- [ ] 4+ chaos test scenarios implemented
- [ ] Resilience validation: 503 errors (not 500), retry logic, circuit breaker
- [ ] Zero data loss validated (transaction rollback)
- [ ] Graceful degradation validated (cache bypass, database fallback)
- [ ] Recovery time validated (< 30 seconds)
- [ ] Integration tests added: `ChaosTests.cs` (4+ test cases)
- [ ] Manual chaos testing executed (kill database, Redis, RabbitMQ)
- [ ] Code review completed
- [ ] Documentation updated: `docs/testing/chaos-engineering.md`

---

## Task PRD-1653: Failover Testing

**Epic:** Integration Testing | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** QA | **Dependencies:** PRD-1640 | **SourceRefs:** Universe §5.Testing

### Context
Test database and API failover scenarios to validate high availability. Target: RTO < 4 hours, RPO < 1 hour.

### Scope
**In Scope:** Database failover tests, API failover tests, recovery time measurement, automated failover

**Out of Scope:** Multi-region failover (Phase 2)

### Requirements
**Functional:**
1. Database failover test:
   - Primary database failure → promote standby to primary
   - Measure recovery time (RTO)
   - Verify data loss (RPO)
2. API failover test:
   - API instance failure → load balancer routes to healthy instances
   - Measure failover time
   - Verify zero dropped requests
3. Automated failover:
   - Health check detects failure → automatic failover
   - No manual intervention required
4. Failover validation:
   - Data integrity (no corruption)
   - Transaction consistency (no partial commits)
   - Projection consistency (rebuild if needed)

**Non-Functional:** RTO < 4 hours, RPO < 1 hour, zero data corruption

**Acceptance Criteria:**
```gherkin
Scenario: Database failover with standby promotion
  Given primary database running with streaming replication to standby
  When primary database killed
  Then standby promoted to primary within 2 minutes
  And API reconnects to new primary
  And all queries execute successfully
  And RTO = 2 minutes, RPO = 0 (synchronous replication)

Scenario: API instance failover
  Given 3 API instances behind load balancer
  When api-1 killed
  Then load balancer detects failure within 30 seconds
  And traffic routed to api-2 and api-3
  And zero 502 errors after failover
  And failover time < 30 seconds

Scenario: Data integrity after failover
  Given database failover completed
  When data integrity check executed
  Then all tables consistent (no corruption)
  And all foreign keys valid
  And all projections consistent with event store
```

**Validation:**
```bash
# Setup database replication (primary + standby)
# See: docs/deployment/database-replication.md

# Test database failover
docker-compose stop postgres-primary
# Promote standby to primary
docker exec postgres-standby pg_ctl promote

# Verify API reconnects
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items
# Expected: 200 OK (after ~2 minutes)

# Measure RTO
# Start time: when primary killed
# End time: when API responds 200 OK
# Expected: < 4 hours (target: < 2 minutes with automated failover)

# Test API failover
docker-compose stop api-1
sleep 35  # Wait for health check
curl -H "Authorization: Bearer $TOKEN" http://localhost/api/warehouse/v1/items
# Expected: 200 OK (routed to api-2 or api-3)

# Verify zero dropped requests during failover
k6 run scripts/load/warehouse-load-smoke.js --duration 2m --vus 100 &
sleep 30
docker-compose stop api-1
wait
# Check k6 results: http_req_failed rate = 0%

# Run failover tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~FailoverTests"
```

**Definition of Done:**
- [ ] Database replication configured (primary + standby)
- [ ] Automated failover script created (`scripts/failover/promote-standby.sh`)
- [ ] Database failover tested (RTO < 4 hours validated)
- [ ] API failover tested (zero dropped requests)
- [ ] Data integrity validated after failover
- [ ] Recovery time measured and documented
- [ ] Integration tests added: `FailoverTests.cs` (3+ test cases)
- [ ] Failover runbook created (`docs/operations/failover-runbook.md`)
- [ ] Code review completed
- [ ] Documentation updated: `docs/deployment/high-availability.md`

---

## Task PRD-1654: Data Migration Tests

**Epic:** Integration Testing | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** QA | **Dependencies:** None | **SourceRefs:** Universe §5.Testing

### Context
Test database schema migrations and data migrations to ensure zero downtime and data integrity. Target: zero data loss, < 5 minute migration time.

### Scope
**In Scope:** Migration test suite (schema changes), rollback tests, data integrity validation

**Out of Scope:** Large-scale data migrations (> 1M rows) - Phase 2

### Requirements
**Functional:**
1. Migration test scenarios:
   - Add column (nullable, with default value)
   - Add index (concurrent, zero downtime)
   - Add table (new entity)
   - Rename column (with data migration)
   - Drop column (after deprecation period)
2. Rollback tests:
   - Apply migration → rollback → verify data intact
   - Test rollback for last 5 migrations
3. Data integrity validation:
   - Foreign key constraints valid
   - Check constraints valid
   - Unique constraints valid
   - Data types correct
4. Zero downtime validation:
   - API remains available during migration
   - Queries execute successfully during migration

**Non-Functional:** Migration time < 5 minutes, zero data loss, zero downtime

**Acceptance Criteria:**
```gherkin
Scenario: Add column migration with default value
  Given table "items" with 10,000 rows
  When migration adds column "is_active" (boolean, default=true)
  Then column added successfully
  And all existing rows have is_active=true
  And new rows can set is_active explicitly
  And migration time < 30 seconds

Scenario: Add index concurrently (zero downtime)
  Given table "sales_orders" with 50,000 rows
  When migration adds index on "customer_id" (CREATE INDEX CONCURRENTLY)
  Then index created without locking table
  And API queries execute during migration (no blocking)
  And migration time < 2 minutes

Scenario: Migration rollback
  Given migration applied (add column "notes" to items)
  When rollback executed (dotnet ef database update PreviousMigration)
  Then column "notes" removed
  And data intact (no rows lost)
  And foreign keys valid

Scenario: Data integrity after migration
  Given migration completed (rename column "qty" to "quantity")
  When data integrity check executed
  Then all foreign keys valid
  And all check constraints valid
  And all unique constraints valid
  And data types correct
```

**Validation:**
```bash
# Create test migration
cd src/LKvitai.MES.Infrastructure
dotnet ef migrations add TestMigration_AddIsActiveColumn

# Apply migration
dotnet ef database update

# Verify column added
psql -d warehouse -c "\d items"
# Expected: is_active column present

# Rollback migration
dotnet ef database update PreviousMigration

# Verify column removed
psql -d warehouse -c "\d items"
# Expected: is_active column absent

# Test zero downtime migration (concurrent index)
# Start load test
k6 run scripts/load/warehouse-load-smoke.js --duration 5m --vus 100 &

# Apply migration (add index)
dotnet ef database update

# Verify zero errors during migration
wait
# Check k6 results: http_req_failed rate = 0%

# Run migration tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~MigrationTests"
```

**Definition of Done:**
- [ ] Migration test suite created: `MigrationTests.cs` (5+ test cases)
- [ ] Test scenarios cover: add column, add index, add table, rename column, drop column
- [ ] Rollback tests implemented (last 5 migrations)
- [ ] Data integrity validation implemented
- [ ] Zero downtime validated (concurrent index creation)
- [ ] Migration time measured (< 5 minutes for all migrations)
- [ ] Migration runbook created (`docs/operations/migration-runbook.md`)
- [ ] Code review completed
- [ ] Documentation updated: `docs/deployment/database-migrations.md`

---

## Task PRD-1655: Rollback Procedures

**Epic:** Integration Testing | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1654 | **SourceRefs:** Universe §5.Testing

### Context
Document and test rollback procedures for deployments and migrations. Target: < 10 minute rollback time, zero data loss.

### Scope
**In Scope:** Rollback runbook, automated rollback scripts, rollback testing, version pinning

**Out of Scope:** Automated rollback triggers (Phase 2)

### Requirements
**Functional:**
1. Rollback runbook (`docs/operations/rollback-runbook.md`):
   - When to rollback (criteria: error rate > 5%, critical bug, data corruption)
   - How to rollback (step-by-step instructions)
   - Rollback validation (health checks, smoke tests)
2. Automated rollback scripts:
   - `scripts/rollback/rollback-api.sh` (revert to previous Docker image)
   - `scripts/rollback/rollback-database.sh` (revert to previous migration)
   - `scripts/rollback/rollback-full.sh` (revert API + database)
3. Version pinning:
   - Docker images tagged with version (e.g., `warehouse-api:v1.2.3`)
   - Database migrations tagged with version
   - Rollback to specific version (not just "previous")
4. Rollback testing:
   - Deploy v1.2.3 → deploy v1.2.4 → rollback to v1.2.3
   - Verify data intact, API functional, projections consistent

**Non-Functional:** Rollback time < 10 minutes, zero data loss, zero downtime (blue-green deployment)

**Acceptance Criteria:**
```gherkin
Scenario: API rollback to previous version
  Given API v1.2.4 deployed with critical bug
  When rollback script executed (rollback-api.sh v1.2.3)
  Then Docker containers restarted with image warehouse-api:v1.2.3
  And API responds with version header: "X-API-Version: 1.2.3"
  And rollback time < 5 minutes

Scenario: Database migration rollback
  Given migration 20260212_AddIsActiveColumn applied
  When rollback script executed (rollback-database.sh 20260211_PreviousMigration)
  Then migration reverted (dotnet ef database update 20260211_PreviousMigration)
  And column "is_active" removed
  And data intact (no rows lost)
  And rollback time < 2 minutes

Scenario: Full rollback (API + database)
  Given v1.2.4 deployed (API + migrations)
  When full rollback executed (rollback-full.sh v1.2.3)
  Then API rolled back to v1.2.3
  And database rolled back to v1.2.3 migrations
  And smoke tests pass
  And rollback time < 10 minutes

Scenario: Rollback validation
  Given rollback completed
  When validation checks executed
  Then health check returns 200 OK
  And smoke tests pass (create item, create order, pick stock)
  And error rate < 1%
  And no data corruption detected
```

**Validation:**
```bash
# Test API rollback
./scripts/rollback/rollback-api.sh v1.2.3

# Verify version
curl -I http://localhost:5000/health | grep X-API-Version
# Expected: X-API-Version: 1.2.3

# Test database rollback
./scripts/rollback/rollback-database.sh 20260211_PreviousMigration

# Verify migration reverted
dotnet ef migrations list
# Expected: 20260212_AddIsActiveColumn not applied

# Test full rollback
./scripts/rollback/rollback-full.sh v1.2.3

# Run smoke tests
./scripts/smoke-tests.sh
# Expected: All tests pass

# Measure rollback time
time ./scripts/rollback/rollback-full.sh v1.2.3
# Expected: < 10 minutes

# Run rollback tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~RollbackTests"
```

**Definition of Done:**
- [ ] Rollback runbook created (`docs/operations/rollback-runbook.md`)
- [ ] Automated rollback scripts created (API, database, full)
- [ ] Version pinning implemented (Docker tags, migration tags)
- [ ] Rollback testing executed (v1.2.4 → v1.2.3)
- [ ] Rollback time validated (< 10 minutes)
- [ ] Data integrity validated after rollback
- [ ] Smoke tests pass after rollback
- [ ] Integration tests added: `RollbackTests.cs` (4+ test cases)
- [ ] Code review completed
- [ ] Documentation updated: `docs/operations/rollback-procedures.md`

---
## Task PRD-1656: Blue-Green Deployment

**Epic:** Deployment | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** None | **SourceRefs:** Universe §5.Deployment

### Context
Implement blue-green deployment strategy for zero-downtime releases. Target: < 1 minute switchover time, instant rollback capability.

### Scope
**In Scope:** Blue-green infrastructure (Kubernetes/Docker), traffic switching, smoke tests, rollback

**Out of Scope:** Multi-region blue-green (Phase 2)

### Requirements
**Functional:**
1. Blue-green infrastructure:
   - Blue environment: current production (v1.2.3)
   - Green environment: new version (v1.2.4)
   - Both environments run simultaneously during deployment
2. Traffic switching:
   - Load balancer routes 100% traffic to blue
   - Deploy green, run smoke tests
   - Switch load balancer to route 100% traffic to green
   - Keep blue running for 1 hour (instant rollback if needed)
3. Smoke tests (automated):
   - Health check: GET /health returns 200 OK
   - Create item: POST /api/warehouse/v1/items
   - Create order: POST /api/warehouse/v1/sales-orders
   - Pick stock: POST /api/warehouse/v1/picks/execute
4. Rollback:
   - If smoke tests fail → switch traffic back to blue
   - If production issues detected → instant rollback (< 1 minute)

**Non-Functional:** Switchover time < 1 minute, zero downtime, instant rollback

**Acceptance Criteria:**
```gherkin
Scenario: Blue-green deployment with traffic switch
  Given blue environment running (v1.2.3, 100% traffic)
  When green environment deployed (v1.2.4)
  Then green environment starts successfully
  And smoke tests executed on green
  And all smoke tests pass
  When traffic switched to green (load balancer config updated)
  Then 100% traffic routed to green
  And blue environment kept running (1 hour)
  And switchover time < 1 minute

Scenario: Smoke test failure triggers rollback
  Given green environment deployed (v1.2.4)
  When smoke tests executed
  And smoke test fails (create order returns 500)
  Then traffic remains on blue (no switch)
  And green environment shut down
  And alert sent: "Deployment failed, smoke tests failed"

Scenario: Instant rollback after traffic switch
  Given traffic switched to green (v1.2.4)
  And production error rate > 5%
  When rollback triggered
  Then traffic switched back to blue (v1.2.3)
  And rollback time < 1 minute
  And green environment shut down

Scenario: Blue environment cleanup
  Given traffic on green for 1 hour
  And no rollback triggered
  When cleanup executed
  Then blue environment shut down
  And resources released
```

**Validation:**
```bash
# Deploy green environment
docker-compose -f docker-compose.blue-green.yml up -d green

# Verify green running
curl http://localhost:5001/health
# Expected: 200 OK, version: v1.2.4

# Run smoke tests on green
./scripts/smoke-tests.sh http://localhost:5001
# Expected: All tests pass

# Switch traffic to green
./scripts/blue-green/switch-to-green.sh

# Verify traffic on green
curl http://localhost/health | grep X-API-Version
# Expected: v1.2.4

# Test rollback
./scripts/blue-green/rollback-to-blue.sh

# Verify traffic on blue
curl http://localhost/health | grep X-API-Version
# Expected: v1.2.3

# Measure switchover time
time ./scripts/blue-green/switch-to-green.sh
# Expected: < 1 minute

# Run blue-green deployment tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~BlueGreenDeploymentTests"
```

**Definition of Done:**
- [ ] Blue-green Docker Compose configuration created
- [ ] Traffic switching scripts created (`switch-to-green.sh`, `rollback-to-blue.sh`)
- [ ] Smoke test suite automated (`scripts/smoke-tests.sh`)
- [ ] Blue-green deployment tested (v1.2.3 → v1.2.4)
- [ ] Switchover time validated (< 1 minute)
- [ ] Rollback tested (instant rollback < 1 minute)
- [ ] Zero downtime validated (load test during deployment)
- [ ] Integration tests added: `BlueGreenDeploymentTests.cs` (4+ test cases)
- [ ] Code review completed
- [ ] Documentation updated: `docs/deployment/blue-green-deployment.md`

---

## Task PRD-1657: Canary Releases

**Epic:** Deployment | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1656 | **SourceRefs:** Universe §5.Deployment

### Context
Implement canary release strategy for gradual rollout with automatic rollback. Target: 10% → 50% → 100% traffic progression, auto-rollback on errors.

### Scope
**In Scope:** Canary deployment strategy, traffic splitting (10%/50%/100%), metrics monitoring, auto-rollback

**Out of Scope:** A/B testing (Phase 2)

### Requirements
**Functional:**
1. Canary deployment phases:
   - Phase 1: Deploy canary, route 10% traffic (5 minutes)
   - Phase 2: If metrics healthy, route 50% traffic (10 minutes)
   - Phase 3: If metrics healthy, route 100% traffic (full rollout)
2. Health metrics monitoring:
   - Error rate < 1%
   - API response time p95 < 500ms
   - No critical exceptions
3. Auto-rollback triggers:
   - Error rate > 5% → immediate rollback
   - API response time p95 > 1s → rollback
   - Critical exception count > 10 → rollback
4. Traffic splitting (Nginx):
   - `split_clients` directive based on request ID
   - 10% traffic: 1 canary instance, 9 stable instances
   - 50% traffic: 5 canary instances, 5 stable instances
   - 100% traffic: 10 canary instances, 0 stable instances

**Non-Functional:** Rollout time 15-30 minutes (if healthy), rollback time < 1 minute

**Acceptance Criteria:**
```gherkin
Scenario: Canary deployment with gradual rollout
  Given stable version v1.2.3 running (100% traffic)
  When canary v1.2.4 deployed
  Then 10% traffic routed to canary
  And metrics monitored for 5 minutes
  And error rate < 1%, p95 < 500ms
  When Phase 1 healthy
  Then 50% traffic routed to canary
  And metrics monitored for 10 minutes
  When Phase 2 healthy
  Then 100% traffic routed to canary
  And stable version shut down

Scenario: Auto-rollback on high error rate
  Given canary v1.2.4 deployed (10% traffic)
  When error rate > 5% detected
  Then auto-rollback triggered
  And 100% traffic routed back to stable v1.2.3
  And canary shut down
  And alert sent: "Canary rollback: high error rate"

Scenario: Manual rollback during canary
  Given canary v1.2.4 deployed (50% traffic)
  When manual rollback triggered
  Then 100% traffic routed to stable v1.2.3
  And canary shut down
  And rollback time < 1 minute

Scenario: Traffic splitting validation
  Given canary deployed (10% traffic)
  When 1000 requests sent
  Then ~100 requests routed to canary (10%)
  And ~900 requests routed to stable (90%)
  And variance < 5%
```

**Validation:**
```bash
# Deploy canary (10% traffic)
./scripts/canary/deploy-canary.sh v1.2.4 10

# Verify traffic split
for i in {1..1000}; do
  curl -s http://localhost/health | grep X-API-Version >> /tmp/canary-traffic.txt
done
grep "v1.2.4" /tmp/canary-traffic.txt | wc -l
# Expected: ~100 (10% of 1000)

# Monitor metrics
curl http://localhost:9090/api/v1/query?query=rate(http_requests_total{code=~"5.."}[5m])
# Expected: < 0.01 (1% error rate)

# Progress to 50% traffic
./scripts/canary/progress-canary.sh 50

# Verify traffic split
# Expected: ~500 requests to canary

# Progress to 100% traffic
./scripts/canary/progress-canary.sh 100

# Test auto-rollback (inject errors)
./scripts/canary/inject-errors.sh
# Wait for auto-rollback
# Expected: Traffic back to stable within 1 minute

# Run canary deployment tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~CanaryDeploymentTests"
```

**Definition of Done:**
- [ ] Canary deployment scripts created (`deploy-canary.sh`, `progress-canary.sh`, `rollback-canary.sh`)
- [ ] Traffic splitting configured (Nginx split_clients)
- [ ] Metrics monitoring implemented (error rate, response time, exceptions)
- [ ] Auto-rollback logic implemented (Prometheus alerts → rollback script)
- [ ] Canary deployment tested (10% → 50% → 100%)
- [ ] Auto-rollback tested (high error rate triggers rollback)
- [ ] Traffic splitting validated (±5% variance)
- [ ] Integration tests added: `CanaryDeploymentTests.cs` (4+ test cases)
- [ ] Code review completed
- [ ] Documentation updated: `docs/deployment/canary-releases.md`

---

## Task PRD-1658: Feature Flags

**Epic:** Deployment | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Backend/API | **Dependencies:** None | **SourceRefs:** Universe §5.Deployment

### Context
Implement feature flag system for gradual rollout and kill switches. Target: toggle features without deployment, < 10ms flag evaluation.

### Scope
**In Scope:** Feature flag library (LaunchDarkly/Unleash), flag management UI, gradual rollout, kill switches

**Out of Scope:** A/B testing, multivariate testing (Phase 2)

### Requirements
**Functional:**
1. Feature flag library:
   - LaunchDarkly SDK OR Unleash SDK (open-source)
   - Flag evaluation: `IFeatureFlagService.IsEnabled(string flagKey, User user)`
2. Feature flags:
   - `enable_3d_visualization` (boolean, default=false)
   - `enable_wave_picking` (boolean, default=false)
   - `enable_agnum_export` (boolean, default=true)
   - `max_order_lines` (number, default=100)
3. Gradual rollout:
   - Percentage rollout: 10% → 50% → 100%
   - User targeting: enable for specific users (beta testers)
   - Rule-based: enable for users with role=Admin
4. Kill switches:
   - Disable feature instantly (no deployment)
   - Use case: critical bug in new feature → disable via flag
5. Flag management UI:
   - LaunchDarkly dashboard OR Unleash admin UI
   - Toggle flags, set rollout percentage, define targeting rules

**Non-Functional:** Flag evaluation < 10ms, flag cache TTL=30 seconds, 99.9% availability

**Acceptance Criteria:**
```gherkin
Scenario: Feature flag enabled for user
  Given feature flag "enable_3d_visualization" = true
  When user navigates to /warehouse/visualization/3d
  Then 3D visualization page rendered
  And flag evaluation time < 10ms

Scenario: Feature flag disabled for user
  Given feature flag "enable_wave_picking" = false
  When user attempts to create wave
  Then API returns 403 Forbidden
  And message: "Feature not enabled"

Scenario: Gradual rollout (percentage)
  Given feature flag "enable_agnum_export" rollout = 50%
  When 100 users access Agnum export feature
  Then ~50 users see feature enabled
  And ~50 users see feature disabled
  And variance < 10%

Scenario: User targeting
  Given feature flag "enable_wave_picking" targeting = [user123, user456]
  When user123 accesses wave picking
  Then feature enabled
  When user789 accesses wave picking
  Then feature disabled (not in targeting list)

Scenario: Kill switch activation
  Given feature "enable_3d_visualization" enabled in production
  And critical bug discovered in 3D rendering
  When admin disables flag in LaunchDarkly dashboard
  Then flag evaluation returns false within 30 seconds (cache TTL)
  And all users see feature disabled
  And no deployment required
```

**Validation:**
```bash
# Install LaunchDarkly SDK
cd src/LKvitai.MES.Api
dotnet add package LaunchDarkly.ServerSdk

# Configure SDK key in appsettings.json
cat appsettings.json | grep LaunchDarkly
# Expected: "SdkKey": "your-sdk-key"

# Run application
dotnet run

# Test feature flag evaluation
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/warehouse/v1/features/enable_3d_visualization
# Expected: { "enabled": true }

# Toggle flag in LaunchDarkly dashboard
# Navigate to: LaunchDarkly → Flags → enable_3d_visualization → Toggle OFF

# Wait 30 seconds (cache TTL)
sleep 30

# Re-test flag evaluation
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/warehouse/v1/features/enable_3d_visualization
# Expected: { "enabled": false }

# Test gradual rollout
# Set rollout to 50% in LaunchDarkly
for i in {1..100}; do
  curl -s -H "Authorization: Bearer user$i" \
    http://localhost:5000/api/warehouse/v1/features/enable_agnum_export \
    | jq '.enabled' >> /tmp/rollout.txt
done
grep "true" /tmp/rollout.txt | wc -l
# Expected: ~50 (50% rollout)

# Run feature flag tests
cd src/LKvitai.MES.Tests.Integration
dotnet test --filter "FullyQualifiedName~FeatureFlagTests"
```

**Definition of Done:**
- [ ] LaunchDarkly SDK installed and configured
- [ ] `IFeatureFlagService` interface implemented
- [ ] 4+ feature flags defined (3D viz, wave picking, Agnum export, max order lines)
- [ ] Gradual rollout tested (10% → 50% → 100%)
- [ ] User targeting tested (specific users, role-based)
- [ ] Kill switch tested (disable feature instantly)
- [ ] Flag evaluation performance validated (< 10ms)
- [ ] Integration tests added: `FeatureFlagTests.cs` (5+ test cases)
- [ ] LaunchDarkly dashboard configured (or Unleash admin UI)
- [ ] Code review completed
- [ ] Documentation updated: `docs/deployment/feature-flags.md`

---

## Task PRD-1659: Production Runbook

**Epic:** Deployment | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** Infra/DevOps | **Dependencies:** PRD-1558 | **SourceRefs:** Universe §5.Deployment

### Context
Create comprehensive production runbook covering deployment, monitoring, troubleshooting, and rollback. Target: 100% operational procedures documented.

### Scope
**In Scope:** Comprehensive runbook (deployment, monitoring, troubleshooting, rollback), runbook testing

**Out of Scope:** Automated runbook execution (Phase 2)

### Requirements
**Functional:**
1. Runbook sections:
   - Deployment procedures (blue-green, canary, rollback)
   - Monitoring and alerting (dashboards, alert response)
   - Troubleshooting guides (common issues, resolution steps)
   - Incident response (severity levels, escalation)
   - Disaster recovery (backup restore, failover)
   - Maintenance procedures (database maintenance, log rotation)
2. Runbook format:
   - Markdown files in `docs/operations/runbook/`
   - Step-by-step instructions with commands
   - Screenshots and diagrams
   - Links to related documentation
3. Runbook testing:
   - Execute all procedures in staging environment
   - Verify commands work as documented
   - Measure execution time for time-sensitive procedures
4. Runbook accessibility:
   - Hosted on internal wiki (Confluence, Notion)
   - Searchable (full-text search)
   - Version controlled (Git)

**Non-Functional:** Runbook completeness 100%, accuracy 100%, accessibility 24/7

**Acceptance Criteria:**
```gherkin
Scenario: Deployment procedure execution
  Given runbook section "Blue-Green Deployment"
  When operator follows step-by-step instructions
  Then deployment completes successfully
  And all commands execute without errors
  And deployment time matches documented estimate (< 10 minutes)

Scenario: Troubleshooting guide usage
  Given production issue: "High API response time"
  When operator searches runbook for "high response time"
  Then troubleshooting guide found
  And guide includes: symptoms, root causes, resolution steps
  And operator resolves issue following guide

Scenario: Incident response procedure
  Given critical alert: "Database connection pool exhausted"
  When on-call engineer follows incident response runbook
  Then severity level determined (Critical)
  And escalation policy followed (page L2 if not resolved in 15 minutes)
  And incident resolved following documented steps

Scenario: Disaster recovery execution
  Given database failure (data corruption)
  When operator follows disaster recovery runbook
  Then backup restored from last snapshot
  And data integrity validated
  And RTO < 4 hours (as documented)
```

**Validation:**
```bash
# Test deployment procedure
# Follow: docs/operations/runbook/deployment/blue-green-deployment.md
./scripts/blue-green/deploy-green.sh v1.2.4
./scripts/blue-green/switch-to-green.sh
# Expected: Deployment successful, < 10 minutes

# Test troubleshooting guide
# Simulate high API response time
./scripts/test/simulate-high-latency.sh
# Follow: docs/operations/runbook/troubleshooting/high-api-response-time.md
# Expected: Issue resolved

# Test incident response
# Simulate critical alert
./scripts/test/trigger-critical-alert.sh
# Follow: docs/operations/runbook/incident-response/critical-alerts.md
# Expected: Incident resolved, escalation followed

# Test disaster recovery
# Simulate database failure
docker-compose stop postgres
# Follow: docs/operations/runbook/disaster-recovery/database-restore.md
./scripts/backup/restore-latest.sh
# Expected: Database restored, RTO < 4 hours

# Verify runbook completeness
ls docs/operations/runbook/
# Expected: deployment/, monitoring/, troubleshooting/, incident-response/, disaster-recovery/, maintenance/
```

**Definition of Done:**
- [ ] Runbook created with 6 sections (deployment, monitoring, troubleshooting, incident response, disaster recovery, maintenance)
- [ ] 20+ procedures documented (step-by-step instructions)
- [ ] All procedures tested in staging environment
- [ ] Screenshots and diagrams added (10+ visuals)
- [ ] Runbook hosted on internal wiki (searchable)
- [ ] Runbook reviewed by operations team
- [ ] Runbook accuracy validated (100% commands work)
- [ ] Runbook completeness validated (100% operational procedures covered)
- [ ] Code review completed
- [ ] Documentation updated: `docs/operations/runbook/README.md`

---

## Task PRD-1660: Go-Live Checklist

**Epic:** Deployment | **Phase:** 1.5 | **Sprint:** 9 | **Estimate:** M (1 day)
**OwnerType:** QA | **Dependencies:** PRD-1560 | **SourceRefs:** Universe §5.Deployment

### Context
Create and execute 100-item go-live checklist covering infrastructure, security, compliance, performance, monitoring, testing, deployment, documentation, operations, and business readiness.

### Scope
**In Scope:** Production readiness checklist (100+ items), sign-off process, go/no-go criteria, launch plan

**Out of Scope:** Post-launch support plan (Phase 2)

### Requirements
**Functional:**
1. Go-live checklist categories (10 categories, 10 items each):
   - Infrastructure (load balancer, database replication, backups, DR, SSL, firewall, DDoS, CDN, monitoring, health checks)
   - Security (SSO, MFA, API keys, RBAC, audit log, PII encryption, penetration testing, vulnerability scanning, incident response, security training)
   - Compliance (transaction log, traceability, FDA 21 CFR Part 11, GDPR, retention policies, compliance reports, audit trail, electronic signatures, record retention, compliance training)
   - Performance (load testing, stress testing, query optimization, caching, async operations, connection pooling, indexes, projection rebuild, API SLAs, capacity planning)
   - Monitoring (APM, dashboards, alerts, SLA monitoring, capacity alerts, error rate alerts, on-call schedule, runbook links, alert tuning, incident postmortem)
   - Testing (E2E tests, chaos tests, failover tests, migration tests, rollback tests, performance regression, contract tests, security testing, accessibility testing, UAT)
   - Deployment (blue-green, canary, feature flags, runbook, rollback procedures, deployment automation, smoke tests, database migrations, configuration management, deployment checklist)
   - Documentation (API docs, operator training, admin guide, troubleshooting guide, architecture docs, runbook, disaster recovery plan, compliance docs, security docs, release notes)
   - Operations (on-call rotation, escalation procedures, incident response, change management, maintenance windows, communication plan, support ticketing, knowledge base, training, go-live communication)
   - Business Readiness (stakeholder sign-off, user training, data migration, legacy cutover, parallel run, go/no-go meeting, launch announcement, customer communication, success metrics, post-launch support)
2. Sign-off process:
   - Each category requires sign-off from responsible party
   - Infrastructure: DevOps Lead
   - Security: Security Officer
   - Compliance: Compliance Officer
   - Performance: Engineering Lead
   - Monitoring: SRE Lead
   - Testing: QA Lead
   - Deployment: DevOps Lead
   - Documentation: Technical Writer
   - Operations: Operations Manager
   - Business Readiness: Product Manager
3. Go/no-go criteria:
   - All 100 items checked (100% completion)
   - All sign-offs obtained (10/10 categories)
   - Zero critical bugs (P0/P1)
   - Performance SLAs met (API p95 < 500ms, uptime > 99.9%)
   - Security audit passed (no critical vulnerabilities)
4. Launch plan:
   - Go-live date: 4 weeks after Sprint 9 completion
   - Launch window: Saturday 2:00 AM - 6:00 AM (low traffic)
   - Deployment: Blue-green deployment (zero downtime)
   - Monitoring: 24/7 on-call team (48 hours post-launch)
   - Communication: Email to all users (launch announcement)

**Non-Functional:** Checklist completion 100%, sign-off 100%, go-live success rate 100%

**Acceptance Criteria:**
```gherkin
Scenario: Go-live checklist execution
  Given go-live checklist with 100 items
  When checklist executed
  Then all 100 items checked
  And all sign-offs obtained (10/10 categories)
  And go/no-go criteria met

Scenario: Go/no-go meeting
  Given go-live checklist 100% complete
  When go/no-go meeting conducted
  Then decision: GO (all criteria met)
  And launch plan approved
  And go-live date confirmed

Scenario: Launch execution
  Given go-live date: 2026-03-15 02:00 AM
  When deployment executed (blue-green)
  Then deployment completes successfully
  And zero downtime
  And smoke tests pass
  And monitoring active (24/7 on-call)
  And launch announcement sent

Scenario: Post-launch monitoring
  Given system launched
  When 48 hours elapsed
  Then uptime = 100%
  And error rate < 0.5%
  And API p95 < 400ms
  And zero critical incidents
  And launch declared successful
```

**Validation:**
```bash
# Execute go-live checklist
cat docs/operations/go-live-checklist.md
# Verify: 100 items, 10 categories

# Check completion status
grep -c "\[x\]" docs/operations/go-live-checklist.md
# Expected: 100 (all items checked)

# Verify sign-offs
grep "Sign-off:" docs/operations/go-live-checklist.md
# Expected: 10 sign-offs (one per category)

# Verify go/no-go criteria
./scripts/go-live/check-criteria.sh
# Expected: All criteria met (GO decision)

# Execute launch plan
./scripts/go-live/launch.sh
# Expected: Deployment successful, zero downtime

# Monitor post-launch
curl http://localhost:5000/metrics | grep uptime
# Expected: uptime = 1.0 (100%)

# Run post-launch validation
./scripts/go-live/post-launch-validation.sh
# Expected: All checks pass
```

**Definition of Done:**
- [ ] Go-live checklist created with 100 items (10 categories × 10 items)
- [ ] Checklist executed (100% completion)
- [ ] Sign-offs obtained (10/10 categories)
- [ ] Go/no-go criteria validated (all met)
- [ ] Go/no-go meeting conducted (GO decision)
- [ ] Launch plan created and approved
- [ ] Deployment executed (blue-green, zero downtime)
- [ ] Smoke tests passed post-launch
- [ ] 48-hour monitoring completed (zero critical incidents)
- [ ] Launch declared successful
- [ ] Post-launch retrospective conducted
- [ ] Code review completed
- [ ] Documentation updated: `docs/operations/go-live-checklist.md`

---

## Sprint 9 Complete

**Total Tasks:** 20 (PRD-1641 to PRD-1660)
**Total Effort:** 19 days
**Placeholder Count:** 0 ✅

All Sprint 9 tasks are now fully specified with concrete requirements, data models, APIs, Gherkin scenarios, validation commands, and Definition of Done checklists.

**Next Steps:**
1. Execute Sprint 9 tasks (PRD-1641 to PRD-1660)
2. Validate all performance, monitoring, testing, and deployment requirements
3. Complete go-live checklist (100 items)
4. Conduct go/no-go meeting
5. Execute production launch

**BATON:** 2026-02-12T16:00:00Z-PHASE15-S9-SPEC-COMPLETE-0-PLACEHOLDERS

