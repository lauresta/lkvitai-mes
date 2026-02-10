# Operational Runbook: Projections Management

## Purpose

This runbook provides step-by-step procedures for operating and troubleshooting Marten projections in the Warehouse module. It addresses the known issue (`42P01: relation "mt_doc_locationbalanceview_shadow" does not exist`) and provides remediation steps.

---

## Known Issue: Shadow Table Missing (42P01)

### Error Details

**Symptom**: Projection rebuild fails with PostgreSQL error:
```
Npgsql.PostgresException (0x80004005): 42P01: relation "mt_doc_locationbalanceview_shadow" does not exist
```

**Affected Projections**: All projections using Marten's inline or async rebuild strategy.

**Impact**: 
- Projection rebuild operations fail
- New environments (staging, prod) cannot initialize projections
- Data corruption recovery blocked
- Schema migrations may fail

---

### Root Cause Analysis

**Marten Projection Lifecycle**:
1. Marten creates primary projection table: `mt_doc_<projection_name>`
2. During rebuild, Marten creates shadow table: `mt_doc_<projection_name>_shadow`
3. Rebuild populates shadow table
4. Atomic swap: shadow → primary (rename operation)
5. Shadow table dropped

**Failure Modes**:

**A. Schema Initialization Failure**
- **Cause**: Projection schema not initialized before rebuild
- **Trigger**: Fresh database, no prior projection runs
- **Reason**: Marten expects schema to exist, does not auto-create on rebuild
- **Solution**: Run `ApplyAllConfiguredChangesToDatabase()` before rebuild

**B. Migration Mismatch**
- **Cause**: EF Core migrations conflict with Marten schema
- **Trigger**: Both EF and Marten try to manage same database
- **Reason**: Schema ownership unclear (who creates/drops tables?)
- **Solution**: Separate schemas (EF: `public`, Marten: `warehouse_events`)

**C. Concurrent Rebuild**
- **Cause**: Multiple rebuild operations run simultaneously
- **Trigger**: Manual rebuild + scheduled rebuild + new deployment
- **Reason**: Shadow table creation locked by first rebuild
- **Solution**: Distributed lock on rebuild operations

**D. Incomplete Cleanup**
- **Cause**: Previous rebuild crashed, shadow table left behind
- **Trigger**: Server restart during rebuild, OOM, etc.
- **Reason**: Shadow table not dropped, blocks next rebuild
- **Solution**: Cleanup script to detect/drop orphaned shadow tables

---

### Immediate Remediation Steps

**Step 1: Verify Database State**

```sql
-- Check if projection table exists
SELECT schemaname, tablename 
FROM pg_tables 
WHERE tablename LIKE 'mt_doc_%';

-- Check for orphaned shadow tables
SELECT schemaname, tablename 
FROM pg_tables 
WHERE tablename LIKE 'mt_doc_%_shadow';

-- Check Marten event progression
SELECT name, last_seq_id 
FROM mt_event_progression;
```

**Expected Output**:
- Primary tables exist: `mt_doc_availablestock`, `mt_doc_locationbalance`, etc.
- NO shadow tables (if present → orphaned)
- Event progression records exist

---

**Step 2: Clean Orphaned Shadow Tables**

```sql
-- Drop all shadow tables (safe - they're temporary)
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (SELECT tablename FROM pg_tables WHERE tablename LIKE 'mt_doc_%_shadow')
    LOOP
        EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
        RAISE NOTICE 'Dropped shadow table: %', r.tablename;
    END LOOP;
END $$;
```

**Alternative (recommended in production)**:
```bash
curl -X POST http://localhost:5000/api/warehouse/v1/admin/projections/cleanup-shadows \
  -H "Authorization: Bearer <admin-token>"
```
The API skips cleanup if a rebuild lock is active and returns lock-holder details.

---

**Step 3: Initialize Marten Schema (Fresh Database)**

If database is fresh (no Marten tables), run this from application code:

```csharp
using var store = DocumentStore.For(options =>
{
    options.Connection(connectionString);
    options.DatabaseSchemaName = "warehouse_events";  // Separate from EF Core
    
    // Register projections
    options.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Inline);
    options.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Inline);
});

// Initialize schema (creates tables, indexes, functions)
await store.Schema.ApplyAllConfiguredChangesToDatabaseAsync();
```

**What this does**:
- Creates `mt_events`, `mt_streams`, `mt_event_progression` tables
- Creates projection tables (`mt_doc_availablestock`, etc.)
- Creates PostgreSQL functions for event handling
- Creates indexes

---

**Step 4: Verify Schema Initialization**

```sql
-- Check Marten core tables
SELECT tablename FROM pg_tables WHERE schemaname = 'warehouse_events' AND tablename LIKE 'mt_%';

-- Expected tables:
-- mt_events
-- mt_streams
-- mt_event_progression
-- mt_doc_availablestock
-- mt_doc_locationbalance
-- mt_doc_activereservations
-- mt_doc_inboundshipmentsummary
-- mt_doc_adjustmenthistory
```

---

**Step 5: Rebuild Projections (Clean Rebuild)**

**Option A: Via Application Code**

```csharp
using var store = DocumentStore.For(connectionString);

// Reset projection progress (start from event 0)
await store.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(EventProgression));

// Rebuild projection
var daemon = await store.BuildProjectionDaemonAsync();
await daemon.RebuildProjectionAsync<AvailableStockProjection>(CancellationToken.None);

// Verify rebuild completed
var progress = await store.Advanced.FetchEventProgression();
Console.WriteLine($"Last processed event: {progress.LastSeqId}");
```

**Option B: Via Admin API**

```bash
# Trigger rebuild via HTTP endpoint
curl -X POST http://localhost:5000/api/warehouse/v1/admin/projections/rebuild \
  -H "Authorization: Bearer <token>" \
  -d '{"projectionName": "AvailableStock"}'
```

---

**Step 6: Monitor Rebuild Progress**

```sql
-- Check event count
SELECT COUNT(*) AS total_events FROM warehouse_events.mt_events;

-- Check projection progress
SELECT 
    name AS projection_name,
    last_seq_id AS events_processed,
    (SELECT MAX(seq_id) FROM warehouse_events.mt_events) AS total_events,
    (SELECT MAX(seq_id) FROM warehouse_events.mt_events) - last_seq_id AS events_remaining
FROM warehouse_events.mt_event_progression;
```

**Expected Timeline**:
- 10,000 events: ~30 seconds
- 100,000 events: ~5 minutes
- 1,000,000 events: ~30 minutes

---

### Preventing Recurrence

**Preventive Measure 1: Schema Separation**

**EF Core DbContext**:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("public");  // EF Core in public schema
}
```

**Marten Configuration**:
```csharp
services.AddMarten(options =>
{
    options.DatabaseSchemaName = "warehouse_events";  // Marten in separate schema
    options.Connection(connectionString);
});
```

**Benefits**:
- No table name conflicts
- Clear ownership (EF: master data, Marten: events)
- Independent migrations

---

**Preventive Measure 2: Pre-Deployment Schema Check**

**Add to CI/CD pipeline**:
```bash
#!/bin/bash
# Pre-deployment schema validation

# Check if Marten schema exists
psql $CONNECTION_STRING -c "SELECT schemaname FROM pg_namespace WHERE nspname = 'warehouse_events';" | grep -q warehouse_events

if [ $? -ne 0 ]; then
  echo "ERROR: Marten schema 'warehouse_events' does not exist. Run schema initialization first."
  exit 1
fi

# Check if projection tables exist
psql $CONNECTION_STRING -c "SELECT tablename FROM pg_tables WHERE schemaname = 'warehouse_events' AND tablename = 'mt_doc_availablestock';" | grep -q mt_doc_availablestock

if [ $? -ne 0 ]; then
  echo "ERROR: Projection table 'mt_doc_availablestock' does not exist. Run ApplyAllConfiguredChangesToDatabase."
  exit 1
fi

echo "Schema validation passed."
```

---

**Preventive Measure 3: Distributed Lock on Rebuild**

**Implementation** (using Redis):
```csharp
public async Task RebuildProjectionAsync<T>(CancellationToken ct)
{
    var lockKey = $"projection-rebuild:{typeof(T).Name}";
    var lockExpiry = TimeSpan.FromMinutes(30);
    
    // Acquire distributed lock
    if (!await _redisLock.AcquireAsync(lockKey, lockExpiry))
    {
        throw new InvalidOperationException($"Projection {typeof(T).Name} is already rebuilding. Lock held.");
    }
    
    try
    {
        // Perform rebuild
        var daemon = await _store.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync<T>(ct);
    }
    finally
    {
        // Release lock
        await _redisLock.ReleaseAsync(lockKey);
    }
}
```

---

**Preventive Measure 4: Startup Schema Validation**

**Add to application startup**:
```csharp
public class SchemaValidationService : IHostedService
{
    private readonly IDocumentStore _store;
    
    public async Task StartAsync(CancellationToken ct)
    {
        // Verify Marten schema exists
        var schemaExists = await _store.Advanced.SchemaExistsAsync(ct);
        
        if (!schemaExists)
        {
            _logger.LogCritical("Marten schema does not exist. Run ApplyAllConfiguredChangesToDatabase.");
            throw new InvalidOperationException("Marten schema not initialized.");
        }
        
        // Verify projection tables exist
        var projections = new[] { "mt_doc_availablestock", "mt_doc_locationbalance" };
        foreach (var projection in projections)
        {
            var tableExists = await _store.Advanced.TableExistsAsync(projection, ct);
            if (!tableExists)
            {
                _logger.LogCritical($"Projection table {projection} does not exist.");
                throw new InvalidOperationException($"Projection {projection} not initialized.");
            }
        }
        
        _logger.LogInformation("Schema validation passed.");
    }
    
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

---

## Projection Operations

### Operation 1: Check Projection Health

**Command**:
```bash
curl http://localhost:5000/api/warehouse/v1/health
```

**Expected Response** (healthy):
```json
{
  "status": "Healthy",
  "projectionStatus": {
    "availableStock": {
      "lastUpdated": "2026-02-09T16:20:00Z",
      "lagSeconds": 0.5,
      "status": "Healthy"
    },
    "locationBalance": {
      "lastUpdated": "2026-02-09T16:20:00Z",
      "lagSeconds": 0.5,
      "status": "Healthy"
    }
  }
}
```

**Unhealthy Response** (projection lag):
```json
{
  "status": "Degraded",
  "projectionStatus": {
    "availableStock": {
      "lastUpdated": "2026-02-09T16:10:00Z",
      "lagSeconds": 600,
      "status": "Unhealthy"
    }
  }
}
```

**Action**: If lag > 60 seconds, investigate (see Troubleshooting section).

---

### Operation 2: Rebuild Single Projection

**When to Use**:
- Projection data corrupted
- Projection logic changed (code update)
- Data quality issues detected

**Procedure**:
```bash
# Stop projection daemon (prevent concurrent updates)
systemctl stop warehouse-projection-daemon

# Backup current projection data (optional)
pg_dump -U postgres -d warehouse -t warehouse_events.mt_doc_availablestock > availablestock_backup.sql

# Rebuild projection via API
curl -X POST http://localhost:5000/api/warehouse/v1/admin/projections/rebuild \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"projectionName": "AvailableStock", "resetProgress": true}'

# Monitor rebuild progress
watch -n 5 'psql -U postgres -d warehouse -c "SELECT * FROM warehouse_events.mt_event_progression"'

# Restart projection daemon
systemctl start warehouse-projection-daemon
```

**Expected Duration**:
- 10k events: ~30 seconds
- 100k events: ~5 minutes
- 1M events: ~30 minutes

---

### Operation 3: Rebuild All Projections

**When to Use**:
- Fresh environment setup (staging, prod)
- Mass data corruption
- Schema changes affecting all projections

**Procedure**:
```bash
# Stop all projection workers
systemctl stop warehouse-projection-daemon

# Drop all projection tables (data loss - ensure backup exists!)
psql -U postgres -d warehouse <<EOF
DROP TABLE IF EXISTS warehouse_events.mt_doc_availablestock CASCADE;
DROP TABLE IF EXISTS warehouse_events.mt_doc_locationbalance CASCADE;
DROP TABLE IF EXISTS warehouse_events.mt_doc_activereservations CASCADE;
DROP TABLE IF EXISTS warehouse_events.mt_doc_inboundshipmentsummary CASCADE;
DROP TABLE IF EXISTS warehouse_events.mt_doc_adjustmenthistory CASCADE;

-- Reset event progression
DELETE FROM warehouse_events.mt_event_progression;
EOF

# Reinitialize schema
dotnet run --project Warehouse.Admin -- initialize-schema

# Rebuild all projections
dotnet run --project Warehouse.Admin -- rebuild-projections --all

# Restart daemon
systemctl start warehouse-projection-daemon
```

---

### Operation 4: Verify Projection Accuracy

**Procedure**:
```sql
-- Compare projection with raw events (smoke test)

-- Test 1: Total stock across all locations should match event sum
WITH event_stock AS (
    SELECT 
        (data->>'itemId')::int AS item_id,
        SUM(CASE WHEN type = 'GoodsReceived' THEN (data->>'receivedQty')::decimal ELSE 0 END) AS received,
        SUM(CASE WHEN type = 'StockMoved' THEN (data->>'qty')::decimal ELSE 0 END) AS moved,
        SUM(CASE WHEN type = 'PickCompleted' THEN (data->>'pickedQty')::decimal ELSE 0 END) AS picked
    FROM warehouse_events.mt_events
    GROUP BY item_id
),
projection_stock AS (
    SELECT item_id, SUM(qty) AS total_qty
    FROM warehouse_events.mt_doc_availablestock
    GROUP BY item_id
)
SELECT 
    e.item_id,
    e.received - e.picked AS expected_qty,
    p.total_qty AS projection_qty,
    (e.received - e.picked) - p.total_qty AS variance
FROM event_stock e
LEFT JOIN projection_stock p ON e.item_id = p.item_id
WHERE ABS((e.received - e.picked) - COALESCE(p.total_qty, 0)) > 0.01;

-- Expected: 0 rows (no variance)
-- If variance found → projection rebuild needed
```

---

## Troubleshooting Guide

### Issue 1: Projection Lag Increasing

**Symptoms**:
- Health check shows lag > 10 seconds
- Projection timestamp not updating
- Stock visibility stale

**Diagnosis**:
```sql
-- Check event append rate
SELECT 
    DATE_TRUNC('minute', timestamp) AS minute,
    COUNT(*) AS events_per_minute
FROM warehouse_events.mt_events
WHERE timestamp > NOW() - INTERVAL '1 hour'
GROUP BY minute
ORDER BY minute DESC;

-- Check projection worker status
SELECT 
    pid, 
    application_name, 
    state, 
    query_start,
    NOW() - query_start AS duration
FROM pg_stat_activity
WHERE application_name LIKE '%Marten%';
```

**Resolution**:
1. **If event rate > 1000/min**: Add more projection workers (scale horizontally)
   ```csharp
   options.Projections.Add<AvailableStockProjection>(
       ProjectionLifecycle.Async,
       asyncConfiguration: async => { async.Workers = 8; });
   ```

2. **If worker stuck (query duration > 5 min)**: Kill and restart
   ```sql
   SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE application_name LIKE '%Marten%';
   ```

3. **If database CPU > 90%**: Optimize projection queries (add indexes)

---

### Issue 2: Projection Not Updating After Event Append

**Symptoms**:
- Event appended successfully (eventId returned)
- Projection not updated after 10+ seconds
- No errors in logs

**Diagnosis**:
```sql
-- Check if event exists
SELECT * FROM warehouse_events.mt_events WHERE id = '<eventId>';

-- Check event progression (did projection process this event?)
SELECT 
    name,
    last_seq_id,
    (SELECT seq_id FROM warehouse_events.mt_events WHERE id = '<eventId>') AS target_seq
FROM warehouse_events.mt_event_progression;
```

**Resolution**:
1. **If event seq_id > last_seq_id**: Projection worker behind (wait or restart)
2. **If event not in mt_events**: Transaction rolled back (check application logs)
3. **If worker not running**: Start projection daemon
   ```bash
   systemctl start warehouse-projection-daemon
   ```

---

### Issue 3: Duplicate Projection Records

**Symptoms**:
- Same (itemId, locationId, lotId) appears twice in projection
- AvailableStock shows inflated qty

**Diagnosis**:
```sql
-- Find duplicates
SELECT item_id, location_id, lot_id, COUNT(*)
FROM warehouse_events.mt_doc_availablestock
GROUP BY item_id, location_id, lot_id
HAVING COUNT(*) > 1;
```

**Cause**: Race condition in projection update (concurrent events)

**Resolution**:
```sql
-- Delete duplicates (keep most recent)
DELETE FROM warehouse_events.mt_doc_availablestock a
USING warehouse_events.mt_doc_availablestock b
WHERE a.id < b.id
  AND a.item_id = b.item_id
  AND a.location_id = b.location_id
  AND COALESCE(a.lot_id, '00000000-0000-0000-0000-000000000000'::uuid) = 
      COALESCE(b.lot_id, '00000000-0000-0000-0000-000000000000'::uuid);

-- Add unique constraint to prevent recurrence
ALTER TABLE warehouse_events.mt_doc_availablestock
ADD CONSTRAINT uk_availablestock_item_location_lot 
UNIQUE (item_id, location_id, COALESCE(lot_id, '00000000-0000-0000-0000-000000000000'::uuid));
```

---

### Issue 4: Negative Stock in Projection

**Symptoms**:
- AvailableStock shows qty < 0
- Business rule violation (cannot pick more than available)

**Diagnosis**:
```sql
-- Find negative stock
SELECT * FROM warehouse_events.mt_doc_availablestock WHERE qty < 0;

-- Trace events for this item/location
SELECT * FROM warehouse_events.mt_events
WHERE (data->>'itemId')::int = <itemId>
  AND (data->>'locationId')::int = <locationId>
ORDER BY seq_id;
```

**Cause**: Events applied out of order OR insufficient stock check bypassed

**Resolution**:
1. **Immediate**: Adjust stock to 0 (manual adjustment event)
2. **Root cause**: Review event order, fix business logic to prevent picking when stock insufficient
3. **Rebuild projection** to ensure consistency

---

## Monitoring & Alerts

### Metrics to Track

| Metric | Target | Warning | Critical |
|--------|--------|---------|----------|
| Projection Lag (seconds) | <1 | >10 | >60 |
| Event Append Rate (events/sec) | <100 | >500 | >1000 |
| Projection Rebuild Duration (min) | <5 | >15 | >30 |
| Database CPU (%) | <50 | >80 | >90 |
| Projection Worker Status | Running | Stopped | Crashed |

### Alert Setup (Prometheus + Grafana)

**Metrics Endpoint**:
```csharp
// Expose Prometheus metrics
app.MapMetrics("/metrics");

// Track projection lag
Metrics.CreateGauge("warehouse_projection_lag_seconds", 
    "Projection lag in seconds", 
    new GaugeConfiguration { LabelNames = new[] { "projection_name" } });
```

**Alert Rule** (Prometheus):
```yaml
groups:
- name: warehouse_projections
  rules:
  - alert: ProjectionLagHigh
    expr: warehouse_projection_lag_seconds > 60
    for: 5m
    labels:
      severity: critical
    annotations:
      summary: "Projection lag exceeds 60 seconds for {{ $labels.projection_name }}"
```

---

## Disaster Recovery

### Scenario: Complete Projection Data Loss

**Recovery Steps**:
1. **Stop all writes** (read-only mode)
2. **Restore from backup** (if available):
   ```bash
   psql -U postgres -d warehouse < projection_backup.sql
   ```
3. **If no backup**: Rebuild from events (see Operation 3)
4. **Verify accuracy** (see Operation 4)
5. **Resume writes**

**RTO**: 30 minutes (for 100k events)  
**RPO**: 0 (events are source of truth)

---

## Maintenance Tasks

### Weekly

- [ ] Check projection lag (should be <1 second)
- [ ] Review error logs (projection worker exceptions)
- [ ] Verify projection record counts (no unexpected spikes/drops)

### Monthly

- [ ] Vacuum projection tables (reclaim disk space)
  ```sql
  VACUUM ANALYZE warehouse_events.mt_doc_availablestock;
  ```
- [ ] Review slow queries (pg_stat_statements)
- [ ] Test projection rebuild on staging (dry run)

### Quarterly

- [ ] Archive old events (>2 years) to cold storage
- [ ] Review projection index usage (unused indexes → drop)
- [ ] Load test (simulate 1000 concurrent event appends)

---

## Summary

The known 42P01 error is caused by **missing schema initialization** or **orphaned shadow tables**. Remediation involves: (1) cleaning shadow tables, (2) running `ApplyAllConfiguredChangesToDatabase`, (3) rebuilding projections. **Prevention**: separate EF/Marten schemas, pre-deployment validation, distributed rebuild locks, startup schema checks. All operational procedures (rebuild, verify, monitor) are documented above. **Target SLA**: Projection lag <1 second, rebuild duration <5 minutes for 100k events.
