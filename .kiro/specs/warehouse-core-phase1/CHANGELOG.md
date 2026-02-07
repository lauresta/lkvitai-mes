# Architectural Mitigation Changelog

**Date:** 2026-02-08  
**Type:** Architecture Patch  
**Scope:** Concurrency and Consistency Mitigations

---

## Overview

This changelog documents the integration of 5 critical architectural mitigations identified in the Claude Architectural Mitigation Report. These mitigations address concurrency race conditions, saga correctness, and projection determinism without redesigning the core system.

**Mitigation Summary:**
- **V-2:** Atomic Balance Validation (StockLedger Concurrency)
- **V-3:** PickStock Saga Fix (Remove Projection Wait)
- **R-3:** StartPicking Orchestration (HARD Lock Acquisition)
- **R-4:** ActiveHardLocks Projection (New Read Model)
- **V-5:** Deterministic Projection Rebuild (Rebuild Contract)

---

## Mitigation V-2: Atomic Balance Validation (StockLedger Concurrency)

### Problem
TOCTOU (Time-of-Check-Time-of-Use) race condition in StockLedger: balance validation at T1, event append at T2 allows concurrent overdraw.

### Solution
- Use Marten's expected-version append for StockLedger stream
- Document retry policy (max 3 retries with exponential backoff)
- Define StockLedger stream partition key rules explicitly
- Define serialization boundary clearly

### Changes Made

**requirements.md:**
- Added new acceptance criterion to Requirement 1 (Stock Movement Ledger):
  - 1.9: Balance validation and movement append must be atomic using optimistic concurrency control

**design.md:**
- Updated StockLedger aggregate section with concurrency enforcement details
- Added invariant: "Balance validation and movement append must be atomic within StockLedger concurrency boundary using optimistic concurrency control"
- Added retry policy documentation

**implementation-blueprint.md:**
- Added expected-version append code pattern with retry logic
- Added concurrency conflict handling example
- Documented stream version tracking

**tasks.md:**
- Added sub-task 2.6: Implement StockLedger concurrency enforcement with expected-version append

---

## Mitigation V-3: PickStock Saga Fix (Remove Projection Wait)

### Problem
PickStock saga incorrectly waits for HU projection completion, creating coupling and fragility.

### Solution
- Remove saga dependency on projection completion
- Define PickStock saga as two-step saga:
  1. Record StockMovement (commits, publishes via outbox)
  2. Consume Reservation
- Clarify: HU projection is asynchronous read model only, NOT a saga step

### Changes Made

**requirements.md:**
- Updated Requirement 4 (Transaction Ordering) to clarify projection is async
- Updated Requirement 17 (Pick Workflow) to remove projection wait requirement

**design.md:**
- Updated PickStockSaga section to remove projection wait step
- Clarified HU projection is async read model, not saga coordination point
- Updated saga state machine diagram

**implementation-blueprint.md:**
- Removed WaitForProjection method from PickStockSaga implementation
- Updated saga to two-step process (StockMovement → Reservation consumption)
- Added note that HU projection updates asynchronously

**tasks.md:**
- Updated task 16.1 (PickStockSaga) to remove projection wait step

---

## Mitigation R-3: StartPicking Orchestration (HARD Lock Acquisition)

### Problem
StartPicking re-validates balance but doesn't prevent concurrent HARD lock conflicts.

### Solution
- Introduce explicit StartPicking workflow with:
  - Balance re-validation using event stream (not projection)
  - HARD lock acquisition using optimistic concurrency on Reservation stream
  - Retry on concurrency conflict
  - Define serialization boundary on reservation stream

### Changes Made

**requirements.md:**
- Added new acceptance criteria to Requirement 3 (Reservation System):
  - 3.13: StartPicking must re-validate balance from event stream
  - 3.14: StartPicking must acquire HARD lock atomically using optimistic concurrency control
  - 3.15: StartPicking must retry on concurrency conflict (max 3 retries)

**design.md:**
- Added new section: "StartPicking Workflow" with detailed orchestration
- Added invariant: "StartPicking must re-validate balance from event stream and acquire HARD lock atomically using optimistic concurrency control"
- Added correctness property for StartPicking atomicity

**implementation-blueprint.md:**
- Added StartPicking command handler implementation
- Added retry logic with exponential backoff
- Added expected-version append for Reservation stream

**tasks.md:**
- Added new task group 7.5: Implement StartPicking orchestration with concurrency enforcement

---

## Mitigation R-4: ActiveHardLocks Projection (New Read Model)

### Problem
No efficient way to query active HARD locks for conflict detection during StartPicking.

### Solution
- Add new projection: ActiveHardLocks
- Requirements:
  - Inline projection in Marten (same-transaction update)
  - Updated atomically with PickingStarted / Consumed / Cancelled events
  - Provide query contract for HARD lock conflict detection
- Schema: (location, sku, reservation_id, hard_locked_qty, started_at)

### Changes Made

**requirements.md:**
- Added new Requirement 19: ActiveHardLocks Read Model
  - Acceptance criteria for inline projection, atomic updates, query contract

**design.md:**
- Added ActiveHardLocks projection section in read models
- Added data model schema for active_hard_locks table
- Added projection logic (INSERT on PickingStarted, DELETE on Consumed/Cancelled)
- Added query contract for conflict detection

**implementation-blueprint.md:**
- Added ActiveHardLocks inline projection implementation
- Added projection update logic in event handlers
- Added query methods for conflict detection

**tasks.md:**
- Added new task 11.3: Implement ActiveHardLocks projection

---

## Mitigation V-5: Deterministic Projection Rebuild (Rebuild Contract)

### Problem
Projection rebuild may produce different results than live processing due to non-determinism.

### Solution
- Add projection rebuild contract with three rules:
  - Rule A: Stream-ordered replay (by stream position, not timestamp)
  - Rule B: Self-contained event data (no external queries during projection)
  - Rule C: Rebuild verification gate (shadow table + checksum before swap)
- Audit existing projections for Rule B compliance
- Add rebuild tooling with shadow table approach

### Changes Made

**requirements.md:**
- Updated Requirement 6 (Read Model Projections) with rebuild contract:
  - 6.13: Stream-ordered replay rule
  - 6.14: Self-contained event data rule
  - 6.15: Rebuild verification gate rule

**design.md:**
- Added new section: "Projection Rebuild Contract"
- Added invariant: "All projections must be deterministic - rebuilding from event stream produces identical results to live processing"
- Added rebuild verification process with shadow tables
- Added correctness property for projection rebuild determinism

**implementation-blueprint.md:**
- Added projection rebuild tooling section
- Added shadow table pattern implementation
- Added checksum verification before swap
- Added rebuild verification process

**tasks.md:**
- Added new task group 25.3: Implement projection rebuild tooling with verification

---

## Impact Summary

### Files Modified
- `requirements.md` - Added 9 new acceptance criteria, 1 new requirement
- `design.md` - Added 3 new sections, 5 new invariants, 3 new correctness properties
- `implementation-blueprint.md` - Added 4 new implementation sections with code examples
- `implementation-blueprint-part2.md` - Updated projection runtime section
- `tasks.md` - Added 5 new task groups

### Architectural Guarantees Added
1. **Atomicity:** Balance validation and event append are now atomic (V-2)
2. **Decoupling:** PickStock saga no longer waits for projections (V-3)
3. **Conflict Prevention:** StartPicking prevents concurrent HARD lock conflicts (R-3)
4. **Efficient Queries:** ActiveHardLocks enables fast conflict detection (R-4)
5. **Determinism:** Projection rebuilds produce identical results (V-5)

### Backward Compatibility
- All changes are additive or refinements
- No breaking changes to existing APIs
- Existing event schemas unchanged
- Existing aggregate boundaries preserved

---

## Testing Requirements

### New Property Tests Required
- Property 50: StockLedger concurrency enforcement (V-2)
- Property 51: StartPicking atomicity (R-3)
- Property 52: ActiveHardLocks consistency (R-4)
- Property 53: Projection rebuild determinism (V-5)

### New Unit Tests Required
- StockLedger retry logic on concurrency conflict
- StartPicking retry logic on concurrency conflict
- ActiveHardLocks projection update logic
- Projection rebuild verification process

---

## Implementation Priority

**Phase 1 (Critical):**
1. V-2: StockLedger concurrency enforcement
2. R-3: StartPicking orchestration
3. R-4: ActiveHardLocks projection

**Phase 2 (Important):**
4. V-3: PickStock saga simplification
5. V-5: Projection rebuild tooling

---

**End of Changelog**
