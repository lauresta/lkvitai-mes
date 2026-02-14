# Concurrency Bug Analysis - Stock Ledger Event Sourcing

## Issue Summary

**Stream:** `stock-ledger:RES:RECEIVING:RM-0001`
**Error:** Version conflict - expected version -1, but Marten expected -2
**Impact:** Transfer commands fail after 3 retry attempts
**Root Cause:** Incorrect version initialization - didn't understand Marten V-2 versioning scheme

⚠️ **IMPORTANT:** This document describes the FIRST (failed) fix attempt.
See `MARTEN_V2_VERSIONING_FIX.md` for the CORRECT fix using V-2 scheme.

---

## The Problem

### Error Message Breakdown

```
Unexpected starting version number for event stream 'stock-ledger:RES:RECEIVING:RM-0001',
expected -1 but was 0
```

**What this means:**
- Your code passes `expectedVersion: 0` to Marten
- Marten expects `-1` for appending to a stream with version 0
- This indicates the stream **already has 1 event** (version 0 = first event)

### Marten Version Semantics

| Stream State | Version | Expected Version for Append |
|--------------|---------|----------------------------|
| New stream (no events) | `null` | `-1` (or use `0` for "expect empty") |
| After 1st event | `0` | `0` (to append 2nd event) |
| After 2nd event | `1` | `1` (to append 3rd event) |
| After nth event | `n-1` | `n-1` (to append nth+1 event) |

---

## Root Cause Analysis

### File: `MartenStockLedgerRepository.cs:36`

```csharp
var state = await session.Events.FetchStreamStateAsync(streamId, ct);
var version = state?.Version ?? 0;  // ❌ BUG HERE
```

**The Bug:**
- When `state` is `null` (stream doesn't exist), version is set to `0`
- When appending with `expectedVersion: 0`, Marten interprets this as "I expect the stream to have 1 event already"
- But the stream is empty, so Marten expects `-1`

**Why retries don't help:**
- Retry loop re-loads the stream version
- Gets `version = 0` (now the stream exists with 1 event from a concurrent operation)
- Tries to append with `expectedVersion: 0`
- But another concurrent operation already appended, so actual version is now `1`
- Conflict repeats

---

## The Fix

### Option 1: Use -1 for new streams (Recommended)

```csharp
// MartenStockLedgerRepository.cs:36
var state = await session.Events.FetchStreamStateAsync(streamId, ct);
var version = state?.Version ?? -1;  // ✅ FIX: -1 for new streams
```

**Why this works:**
- New stream: `state = null` → `version = -1` → Marten expects empty stream ✅
- Existing stream: `state.Version = 0` → `version = 0` → Marten expects 1 event ✅

### Option 2: Use Marten's built-in stream version tracking

```csharp
public async Task<(StockLedger Ledger, long Version)> LoadAsync(
    string streamId, CancellationToken ct)
{
    await using var session = _store.LightweightSession();

    var ledger = await session.Events.AggregateStreamAsync<StockLedger>(
        streamId, token: ct) ?? new StockLedger();

    var state = await session.Events.FetchStreamStateAsync(streamId, ct);

    // Use -1 for non-existent streams, otherwise use actual version
    var version = state?.Version ?? -1;

    return (ledger, version);
}
```

### Option 3: Let Marten auto-detect (if you don't need optimistic concurrency)

```csharp
// If you don't care about version conflicts, just append without version check
session.Events.Append(streamId, evt);  // No expectedVersion
await session.SaveChangesAsync(ct);
```

**⚠️ Warning:** This disables optimistic concurrency control - NOT recommended for financial/inventory systems.

---

## Why This Bug Manifests

### Concurrent Operations Scenario

1. **Request A** arrives: Transfer RM-0001 from RECEIVING → STORAGE
   - Loads stream → `version = 0` (stream doesn't exist yet)
   - Appends event with `expectedVersion: 0`
   - **Marten rejects:** "You said version 0, but stream is new (expected -1)"

2. **Request B** (concurrent): Another operation on same stream
   - Creates the stream first
   - Now stream exists with version `0`

3. **Request A retries:**
   - Loads stream → `version = 0` (stream now exists with 1 event)
   - Appends event with `expectedVersion: 0`
   - **Marten rejects:** "You said version 0, but actual version is 1 (someone else appended)"

4. **Retry loop exhausted** → Error logged

---

## Testing the Fix

### Unit Test (Property-Based)

```csharp
[Fact]
public async Task AppendEvent_ToNewStream_ShouldSucceed()
{
    // Arrange
    var streamId = "test-stream-" + Guid.NewGuid();
    var evt = new StockMovedEvent(/* ... */);

    // Act
    var (_, version) = await _repository.LoadAsync(streamId, CancellationToken.None);

    // Assert - version should be -1 for new stream
    version.Should().Be(-1);

    // Act - append should succeed
    await _repository.AppendEventAsync(streamId, evt, version, CancellationToken.None);

    // Assert
    var (ledger, newVersion) = await _repository.LoadAsync(streamId, CancellationToken.None);
    newVersion.Should().Be(0); // First event at version 0
}

[Fact]
public async Task AppendEvent_Concurrent_ShouldRetryAndSucceed()
{
    // Arrange
    var streamId = "test-stream-" + Guid.NewGuid();
    var evt1 = new StockMovedEvent(/* ... */);
    var evt2 = new StockMovedEvent(/* ... */);

    // Act - simulate concurrent appends
    var task1 = AppendWithRetryAsync(streamId, evt1);
    var task2 = AppendWithRetryAsync(streamId, evt2);

    await Task.WhenAll(task1, task2);

    // Assert - both events should be persisted
    var (ledger, version) = await _repository.LoadAsync(streamId, CancellationToken.None);
    version.Should().Be(1); // Two events (versions 0 and 1)
}
```

### Integration Test (Verify Fix)

```bash
# Before fix: This will fail
curl -X POST http://localhost:5000/api/transfers/execute \
  -H "Content-Type: application/json" \
  -d '{
    "transferId": "...",
    "warehouseId": "RES",
    "location": "RECEIVING",
    "sku": "RM-0001"
  }'

# After fix: Should succeed
# Expected: 200 OK with transfer executed
```

---

## Impact Assessment

### Affected Operations

All operations that append to stock ledger streams:
- ✅ **ReceiveStock** - Creates new streams (most affected)
- ✅ **TransferStock** - Appends to existing streams
- ✅ **PickStock** - Appends to existing streams
- ✅ **AdjustStock** - Appends to existing streams
- ✅ **ReturnStock** - Appends to existing streams

### Severity

**HIGH** - This bug blocks all stock movements when concurrent operations target the same location+SKU combination.

### Frequency

- High in production environments with:
  - Multiple users performing stock movements
  - Batch/automated stock operations
  - High-volume receiving operations

---

## Recommended Actions

1. ✅ **Apply Fix:** Change `?? 0` to `?? -1` in `MartenStockLedgerRepository.cs:36`
2. ✅ **Add Unit Tests:** Verify new stream behavior
3. ✅ **Add Integration Tests:** Verify concurrent append scenarios
4. ✅ **Review Retry Logic:** Consider exponential backoff (currently linear: 50ms, 100ms, 150ms)
5. ✅ **Monitor Logs:** Track concurrency conflict frequency after fix
6. ⚠️ **Update Memory:** Document this pattern for other event-sourced aggregates

---

## Related Code Locations

### ✅ Fixed Files

| File | Line | Status | Fix Applied |
|------|------|--------|-------------|
| `MartenStockLedgerRepository.cs` | 36 | ✅ FIXED | `?? 0` → `?? -1` |
| `MartenReservationRepository.cs` | 33 | ✅ FIXED | `?? 0` → `?? -1` |
| `MartenReceiveGoodsOrchestration.cs` | 75 | ✅ FIXED | `?? 0` → `?? -1` |
| `MartenStartPickingOrchestration.cs` | 185 | ✅ FIXED | `?? 0` → `?? -1` |
| `MartenAllocateReservationOrchestration.cs` | 113 | ✅ FIXED | `?? 0` → `?? -1` |
| `MartenPickStockOrchestration.cs` | 152 | ✅ FIXED | `?? 0` → `?? -1` |
| `MartenPickStockOrchestration.cs` | 235 | ✅ FIXED | `?? 0` → `?? -1` |

### 🧪 Test Coverage

| File | Status |
|------|--------|
| `EventSourcingVersionInitializationTests.cs` | ✅ Created - Unit tests for version semantics |

### 🔍 Call Sites (Retry Logic)

| File | Line | Description |
|------|------|-------------|
| `TransferServices.cs` | 574-615 | Retry loop for stock ledger append (3 attempts) |

---

## Architecture Considerations (from Doc 04)

This bug violates **Decision 1**:
> "StockLedger is the sole owner of StockMovement events"

The retry mechanism is working as designed, but the version initialization prevents proper optimistic concurrency control. This fix restores the intended behavior:

- **Optimistic Concurrency:** Detect conflicts and retry
- **Event Ordering:** Ensure sequential append to stream
- **Invariant Protection:** "No negative stock" requires version control

---

## References

- **Marten Docs:** [Event Store Optimistic Concurrency](https://martendb.io/events/versioning.html)
- **Project Docs:** `docs/04-system-architecture.md` - Decision 1
- **Implementation Blueprint:** `.kiro/.../implementation-blueprint.md` - Section 2.2
