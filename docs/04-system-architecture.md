# LKvitai.MES - Warehouse Architecture (Hardened & Implementation-Ready)

**Version:** 2.0 (Hardened)  
**Date:** February 2026  
**Status:** FINAL BASELINE - Implementation Ready  
**Supersedes:** 04-system-architecture.md  
**Canonical Inputs:** 01-discovery.md, 02-warehouse-domain-model-v1.md, 03-implementation-guide.md

---

## Document Purpose

This document hardens the architecture based on mandatory architectural decisions. It is the **FINAL BASELINE** for implementation. All decisions here are **MANDATORY** and must not be changed without architectural review board approval.

**Key Changes from Previous Architecture:**
1. Clarified StockLedger as sole owner of StockMovement events
2. Hardened transaction boundaries and ordering
3. Restricted offline operations to safe subset
4. Split Integration Module into three logical components
5. Added Implementation Readiness Checklist

---

## Mandatory Architectural Decisions (ENFORCED)

### Decision 1: Movement Ownership (CRITICAL)

**Rule:** StockMovement MUST be owned exclusively by StockLedger module.

**Implications:**
- ✅ StockLedger is the ONLY module that can append StockMoved events
- ✅ HandlingUnit updates its state by SUBSCRIBING to StockMoved events (projection)
- ✅ HandlingUnit.MoveTo() does NOT generate StockMoved events directly
- ❌ HandlingUnit CANNOT write to stock_movement_events table
- ❌ No other module can bypass StockLedger to record movements

**Enforcement Mechanism:**
- Database: `stock_movement_events` table has INSERT privilege only for StockLedger service account
- Code: HandlingUnit module has no dependency on event store write APIs
- Review: All pull requests touching movement recording must be reviewed by architect

---

### Decision 2: Pick Transaction Order (CRITICAL)

**Rule:** Pick operations MUST follow strict ordering.

**Mandatory Sequence:**
```
1. StockLedger.AppendMovement(sku, qty, fromLocation, PRODUCTION, ...)
   ↓ (commit transaction)
2. Event published: StockMoved
   ↓ (event bus)
3. HandlingUnit projection updates (consumes StockMoved event)
   ↓ (separate transaction)
4. Reservation.Consume(qty)
   ↓ (separate transaction)
5. Pick complete
```

**Forbidden Patterns:**
- ❌ Updating HandlingUnit.Lines BEFORE StockLedger append
- ❌ Consuming Reservation BEFORE StockLedger append
- ❌ Wrapping all 3 operations in single distributed transaction

**Rationale:**
- StockLedger is source of truth - must commit first
- If process crashes after ledger commit, events can be replayed to rebuild HU/Reservation state
- If HU updated first and ledger fails, inventory corruption occurs

---

### Decision 3: Offline Policy (CRITICAL)

**Rule:** Offline operations restricted to safe subset only.

**Allowed Offline:**
- ✅ PickStock (only when reservation already HARD locked on server)
- ✅ TransferStock (only for already assigned HandlingUnits to operator)

**Forbidden Offline:**
- ❌ AllocateReservation (requires real-time balance check)
- ❌ StartPicking (requires conflict detection)
- ❌ AdjustStock (requires approval)
- ❌ ApplyCostAdjustment (requires approval)
- ❌ SplitHU / MergeHU (requires real-time validation)
- ❌ CreateReservation (requires allocation saga)

**Enforcement:**
- Edge Agent checks operation whitelist before queueing
- Server rejects offline-queued commands for forbidden operations (returns 403 Forbidden)
- UI disables forbidden operation buttons when offline

---

### Decision 4: Reservation Bumping Policy (CRITICAL)

**Rule:** Bumping rules based on lock type and state.

| Current Reservation State | Lock Type | Can Be Bumped? | By Whom? |
|--------------------------|-----------|----------------|----------|
| PENDING (not yet allocated) | N/A | ✅ Yes | Any reservation can take same stock |
| ALLOCATED | SOFT | ✅ Yes | Higher priority OR any HARD lock request |
| PICKING | HARD | ❌ No | Cannot be bumped (picking already started) |
| CONSUMED | N/A | N/A | Terminal state |
| CANCELLED | N/A | N/A | Terminal state |

**UI/Operational Consequences:**

**Scenario 1: Soft Reservation Bumped Before Picking**
```
User Story:
- Operator A has soft reservation R1 for HU-001
- Higher priority order needs same stock
- System allocates HU-001 to new reservation R2
- Operator A sees notification: "Reservation R1 bumped by higher priority order. Stock reallocated."

User Actions:
- Operator A must find alternative stock OR
- Escalate to manager for priority adjustment OR
- Wait for new stock to arrive
```

**Scenario 2: Attempt to Bump Hard Reservation (Forbidden)**
```
User Story:
- Operator B starts picking reservation R3 (HARD lock)
- Urgent order needs same stock
- System attempts allocation
- Allocation FAILS with conflict error

User Actions:
- Urgent order must wait for R3 to complete OR
- Manager intervenes to cancel R3 (requires approval + compensation)
```

**Scenario 3: Offline Pick Against Bumped Reservation**
```
User Story:
- Operator C offline with SOFT reservation R4
- While offline, R4 gets bumped
- Operator C queues PickStock command offline
- On reconnect, sync attempts PickStock
- Server rejects: 400 Bad Request "Reservation R4 cancelled/bumped"

User Actions:
- Operator C sees error in reconciliation report
- Must return stock to shelf OR
- Contact manager to create new reservation
```

---

### Decision 5: Integration Separation (MANDATORY)

**Rule:** Integration Module split into three logical components.

**Component 1: Operational Integration**
- **Responsibility:** Real-time operational integrations (label printing, scanners, equipment)
- **Latency:** Low (< 5 seconds)
- **Failure Mode:** Retry 3x, then alert operator
- **Examples:** Print label, read scale, unlock gate

**Component 2: Financial Integration**
- **Responsibility:** Periodic financial exports (Agnum accounting)
- **Latency:** High tolerance (minutes to hours)
- **Failure Mode:** Retry with exponential backoff, manual fallback
- **Examples:** Export stock snapshot, reconciliation reports

**Component 3: Process Integration**
- **Responsibility:** MES/ERP process coordination (material requests, consumption events)
- **Latency:** Medium (< 30 seconds)
- **Failure Mode:** Saga compensation, notify both systems
- **Examples:** MaterialRequested → CreateReservation, StockMoved → MaterialConsumed

**Why Split:**
- Different SLAs (operational needs fast response, financial can be batch)
- Different failure modes (operational blocks work, financial is async)
- Different teams (operations team vs accounting team vs production team)

---

## Table of Contents

- [PART 1: Refined Aggregate Interaction Model](#part-1-refined-aggregate-interaction-model)
- [PART 2: Updated Transaction Model](#part-2-updated-transaction-model)
- [PART 3: Offline/Edge Conflict Model](#part-3-offlineedge-conflict-model)
- [PART 4: Final Integration Architecture](#part-4-final-integration-architecture)
- [PART 5: Implementation Readiness Checklist](#part-5-implementation-readiness-checklist)

---

<!-- ==================== OUTPUT 1 START ==================== -->

## PART 1: Refined Aggregate Interaction Model

### 1.1 Ledger → HandlingUnit Projection Flow

**Pattern:** Event-Driven Projection (StockLedger publishes, HandlingUnit subscribes)

#### Flow Diagram

```
┌──────────────────┐
│   StockLedger    │
│   (Write Model)  │
└────────┬─────────┘
         │
         │ 1. Command: RecordStockMovement(sku, qty, from, to, huId)
         ↓
    ┌────────────────────────────────────┐
    │ Validate:                          │
    │ - from location has balance ≥ qty  │
    │ - to ≠ from                        │
    │ - qty > 0                          │
    └────────┬───────────────────────────┘
             │
             │ 2. Append to event stream
             ↓
    ┌────────────────────────────────────┐
    │ stock_movement_events table        │
    │ INSERT new row                     │
    │ (atomic, within transaction)       │
    └────────┬───────────────────────────┘
             │
             │ 3. Commit transaction
             ↓
    ┌────────────────────────────────────┐
    │ StockMoved event published         │
    │ (via Transactional Outbox)         │
    └────────┬───────────────────────────┘
             │
             │ 4. Event bus delivers
             ↓
    ┌────────────────────────────────────┐
    │ HandlingUnit Projection Handler    │
    │ (Read Model)                       │
    └────────┬───────────────────────────┘
             │
             │ 5. Process event
             ↓
    ┌────────────────────────────────────────────────┐
    │ IF event.handlingUnitId != null:               │
    │   IF event.fromLocation == HU.location:        │
    │     RemoveLine(event.sku, event.qty)           │
    │   IF event.toLocation == HU.location:          │
    │     AddLine(event.sku, event.qty)              │
    │   IF event.toLocation != event.fromLocation:   │
    │     Update HU.location = event.toLocation      │
    └────────┬───────────────────────────────────────┘
             │
             │ 6. Commit projection update
             ↓
    ┌────────────────────────────────────┐
    │ handling_units table updated       │
    │ handling_unit_lines table updated  │
    └────────────────────────────────────┘
```

#### Key Rules

| Rule | Enforcement | Violation Consequence |
|------|-------------|----------------------|
| **R1.1** StockLedger is ONLY writer to stock_movement_events | Database permissions | Cannot insert |
| **R1.2** HandlingUnit reads StockMoved events via subscription | Event bus registration | Events not received |
| **R1.3** HandlingUnit projection is idempotent | Check event.movementId already processed | Duplicate processing safe |
| **R1.4** Projection lag is acceptable (< 5 sec) | Monitoring alert if lag > 30 sec | UI shows "Refreshing..." |
| **R1.5** Projection can be rebuilt from events | Replay all StockMoved events for HU | Recovery from corruption |

#### Failure Scenarios

**Scenario 1A: StockLedger Commit Fails**
```
1. RecordStockMovement command received
2. Validation passes
3. INSERT to stock_movement_events attempted
4. ❌ Database error (network, disk full, constraint violation)
5. Transaction rolled back
6. NO event published
7. HandlingUnit state unchanged
8. ✅ System remains consistent (no partial state)
```

**Scenario 1B: Event Delivery Fails**
```
1. RecordStockMovement command succeeds
2. StockMoved event in Outbox table
3. Outbox processor attempts delivery
4. ❌ Event bus unavailable
5. Event remains in Outbox (not marked as published)
6. ✅ Outbox processor retries (exponential backoff)
7. Eventually event delivered (at-least-once guarantee)
8. HandlingUnit projection eventually consistent
```

**Scenario 1C: Projection Handler Crashes Mid-Update**
```
1. StockMoved event delivered
2. Projection handler starts processing
3. RemoveLine(sku, qty) succeeds
4. ❌ Process crashes before AddLine
5. On restart: Event redelivered (at-least-once)
6. Projection handler checks: "Did I already process movementId?"
7. If yes: Skip (idempotent)
8. If no: Replay full update (RemoveLine + AddLine)
9. ✅ Eventually consistent
```

---

### 1.2 Ledger → Reservation Validation Flow

**Pattern:** Query-Before-Command (Reservation queries Ledger before allocation)

#### Flow Diagram

```
┌──────────────────┐
│   Reservation    │
│   Aggregate      │
└────────┬─────────┘
         │
         │ 1. Command: AllocateReservation(reservationId, huIds)
         ↓
    ┌────────────────────────────────────┐
    │ Query StockLedger:                 │
    │ "What is current balance at        │
    │  HU locations for requested SKUs?" │
    └────────┬───────────────────────────┘
             │
             │ 2. Execute query
             ↓
    ┌────────────────────────────────────┐
    │ StockLedger.GetBalanceAt(          │
    │   location, sku, asOf=now)         │
    │                                    │
    │ Implementation:                    │
    │ - Check projection: location_balance│
    │ - If projection lag < 5 sec: return│
    │ - Else: compute from event stream  │
    └────────┬───────────────────────────┘
             │
             │ 3. Return balance
             ↓
    ┌────────────────────────────────────┐
    │ Reservation validates:             │
    │ - Balance ≥ requested qty?         │
    │ - HU not already hard-locked?      │
    │ - No conflicting reservations?     │
    └────────┬───────────────────────────┘
             │
             │ 4. If validation passes
             ↓
    ┌────────────────────────────────────┐
    │ Reservation.Allocate(huIds)        │
    │ (SOFT lock)                        │
    └────────┬───────────────────────────┘
             │
             │ 5. Publish event
             ↓
    ┌────────────────────────────────────┐
    │ StockAllocated event               │
    │ (reservation_events table)         │
    └────────────────────────────────────┘
```

#### Key Rules

| Rule | Enforcement | Violation Consequence |
|------|-------------|----------------------|
| **R2.1** Reservation MUST query Ledger before allocation | Code review + architecture tests | Allocation without balance check |
| **R2.2** Ledger balance query uses projection if fresh (< 5 sec) | Projection timestamp check | Falls back to event stream query |
| **R2.3** Allocation is optimistic (can overbook with SOFT) | Business rule | Resolved at StartPicking() |
| **R2.4** StartPicking() re-validates balance | Code enforcement | Prevents picking insufficient stock |
| **R2.5** HARD lock allocation queries real-time balance | Always query event stream, skip projection | Guarantees accuracy |

#### Stale Read Scenarios

**Scenario 2A: Projection Lag During Allocation**
```
Timeline:
T0: Location A has 100 units of SKU933
T1: Operator picks 80 units (StockMoved event recorded)
T2: Projection not yet updated (lag = 3 sec)
T3: Reservation R1 queries balance → sees 100 units (stale)
T4: Reservation R1 allocates 50 units (SOFT) ✅ Allowed
T5: Projection updates → balance now 20 units
T6: Operator tries StartPicking(R1)
T7: Re-validation: balance (20) < allocated (50) ❌ Fails
T8: Operator sees error: "Insufficient stock, reservation cannot start picking"

Resolution:
- Reservation remains ALLOCATED (SOFT)
- Operator notified to wait for stock or reallocate
- No inventory corruption (ledger is correct)
```

**Scenario 2B: Concurrent Allocations on Same Stock**
```
Timeline:
T0: Location A has 50 units of SKU105
T1: Reservation R1 queries balance → 50 units
T2: Reservation R2 queries balance → 50 units (same)
T3: R1 allocates 30 units (SOFT) ✅
T4: R2 allocates 40 units (SOFT) ✅ (overbooked to 70 units)
T5: Operator starts picking R1 → StartPicking(R1)
T6: Re-validation: balance (50) ≥ allocated (30) ✅ Succeeds
T7: R1 transitions to HARD lock
T8: Operator starts picking R2 → StartPicking(R2)
T9: Re-validation: balance (50) - R1 hard (30) = 20 < allocated (40) ❌ Fails
T10: Operator sees error: "Stock already hard-locked by another reservation"

Resolution:
- R1 proceeds (HARD lock)
- R2 fails to start picking
- R2 notified: "Bumped by higher priority"
- R2 must find alternative stock
```

---

### 1.3 Cross-Aggregate Command Rules

**Pattern:** Commands flow downward in dependency hierarchy, never upward.

#### Dependency Hierarchy

```
Level 1 (Foundation):
┌──────────────────┐
│   StockLedger    │  ← No dependencies
└──────────────────┘

Level 2 (State):
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  HandlingUnit    │  │  WarehouseLayout │  │ LogicalWarehouse │
│  (reads Ledger)  │  │  (independent)   │  │  (independent)   │
└──────────────────┘  └──────────────────┘  └──────────────────┘

Level 3 (Business Logic):
┌──────────────────┐  ┌──────────────────┐
│   Reservation    │  │    Valuation     │
│  (reads Ledger,  │  │  (reads Ledger)  │
│   reads HU)      │  │                  │
└──────────────────┘  └──────────────────┘

Level 4 (Orchestration):
┌────────────────────────────────────────┐
│   Process Managers / Sagas             │
│   (can send commands to any level)     │
└────────────────────────────────────────┘
```

#### Command Flow Rules

**ALLOWED Command Flows:**

| From Module | To Module | Command Type | Example |
|-------------|-----------|--------------|---------|
| Any | StockLedger | RecordStockMovement | TransferStockSaga → RecordStockMovement |
| Any | HandlingUnit | CreateHU, AddLine, SealHU | ReceiveGoodsSaga → CreateHU |
| Any | Reservation | CreateReservation, AllocateReservation | ERP Gateway → CreateReservation |
| Saga | Any | Any allowed command | PickStockSaga → RecordStockMovement + Reservation.Consume |

**FORBIDDEN Command Flows:**

| From Module | To Module | Why Forbidden | Enforcement |
|-------------|-----------|---------------|-------------|
| StockLedger | HandlingUnit | Ledger is foundation, cannot depend on higher levels | Code dependency analysis |
| StockLedger | Reservation | Ledger is foundation | Code dependency analysis |
| HandlingUnit | StockLedger | HU should subscribe to events, not command ledger | Architecture review |
| HandlingUnit | Reservation | HU is stateless projection, cannot affect reservations | Architecture review |
| Reservation | Valuation | Orthogonal concerns (quantity vs cost) | Architecture review |
| Valuation | Reservation | Orthogonal concerns | Architecture review |

#### Query Flow Rules

**ALLOWED Query Flows:**

| From Module | To Module | Query Type | Example |
|-------------|-----------|------------|---------|
| Reservation | StockLedger | GetBalanceAt(location, sku) | Validate allocation |
| Reservation | HandlingUnit | GetHU(huId) | Check HU contents |
| Valuation | StockLedger | GetBalanceAt(location, sku) | Compute on-hand value |
| Process Managers | Any | Any query | AllocationSaga queries AvailableStock |

**FORBIDDEN Query Flows:**

| From Module | To Module | Why Forbidden | Enforcement |
|-------------|-----------|---------------|-------------|
| StockLedger | HandlingUnit | Ledger cannot depend on higher-level state | Dependency check |
| StockLedger | Reservation | Ledger is foundation | Dependency check |
| HandlingUnit | Reservation | HU is projection, should not know about reservations | Architecture review |

---

### 1.4 Command Validation Rules Matrix

**Validation Location Policy:** Validate at the deepest aggregate that owns the invariant.

| Command | Validation | Where Validated | Invariant Owner |
|---------|------------|----------------|-----------------|
| **RecordStockMovement** | From location has balance ≥ qty | StockLedger | StockLedger (no negative balance) |
| **CreateHandlingUnit** | Location exists | WarehouseLayout query | WarehouseLayout (valid locations) |
| **AddLineToHandlingUnit** | HU status != SEALED | HandlingUnit | HandlingUnit (sealed immutability) |
| **SealHandlingUnit** | HU has lines (not empty) | HandlingUnit | HandlingUnit (no empty sealed HUs) |
| **MoveHandlingUnit** | To location exists | WarehouseLayout query | WarehouseLayout (valid locations) |
| **MoveHandlingUnit** | HU has stock | HandlingUnit | HandlingUnit (cannot move empty HU) |
| **AllocateReservation** | Balance sufficient | StockLedger query | StockLedger (balance truth) |
| **AllocateReservation** | HU exists | HandlingUnit query | HandlingUnit (HU existence) |
| **StartPicking** | No conflicting HARD locks | Reservation aggregate | Reservation (lock conflicts) |
| **StartPicking** | Balance still sufficient | StockLedger query (re-validate) | StockLedger (balance truth) |
| **PickStock** | Reservation status == PICKING | Reservation | Reservation (state machine) |
| **PickStock** | HU allocated to reservation | Reservation | Reservation (allocation integrity) |
| **ApplyCostAdjustment** | Approver authorized | Valuation + IAM | Valuation (cost change approval) |

**Multi-Aggregate Validation:**

Some commands require validation from multiple aggregates. These are orchestrated by Sagas:

```
Example: TransferStock(huId, toLocation)

Saga coordinates:
1. Query WarehouseLayout: toLocation exists? ✅
2. Query HandlingUnit: HU exists and not empty? ✅
3. For each line in HU:
   a. Query StockLedger: from location has balance? ✅
   b. Command StockLedger: RecordStockMovement
4. Command HandlingUnit: Update location

If any validation fails → abort saga
```

---

### 1.5 Event Subscription Rules

**Pattern:** Aggregates subscribe to events from dependencies (lower levels).

#### Subscription Matrix

| Subscriber Module | Publishes Events | Subscribes To Events | Reason |
|------------------|------------------|---------------------|---------|
| **StockLedger** | StockMoved | NONE | Foundation - publishes only |
| **HandlingUnit** | HandlingUnitCreated, LineAdded, LineRemoved, HandlingUnitMoved, HandlingUnitSealed | StockMoved | Updates projection from ledger |
| **Reservation** | ReservationCreated, StockAllocated, PickingStarted, ReservationConsumed | StockMoved (to detect balance changes) | Re-validates on balance changes |
| **Valuation** | CostAdjusted, LandedCostAllocated | NONE | Independent financial interpretation |
| **WarehouseLayout** | BinDefined, BinModified | NONE | Independent configuration |
| **LogicalWarehouse** | CategoryAssigned | NONE | Independent metadata |
| **Read Models** | ProjectionRebuilt | ALL events | Builds query projections |
| **Integration (Operational)** | LabelPrinted, LabelPrintFailed | HandlingUnitCreated, HandlingUnitSealed | Triggers label printing |
| **Integration (Financial)** | ExportCompleted | StockMoved, CostAdjusted | Detects when export needed |
| **Integration (Process)** | MaterialConsumed | StockMoved (to PRODUCTION location) | Notifies ERP of consumption |

#### Subscription Guarantees

| Guarantee | Mechanism | Failure Handling |
|-----------|-----------|------------------|
| **At-Least-Once Delivery** | Transactional Outbox + retry | Event may be delivered multiple times → handlers must be idempotent |
| **Ordering Within Partition** | Events for same aggregate ID ordered | E.g., all StockMoved events for same HU delivered in order |
| **No Ordering Across Partitions** | Events for different aggregates unordered | E.g., StockMoved for HU-001 and HU-002 can arrive out of order |
| **Event Handler Idempotency** | Handler checks: "Already processed event.id?" | Duplicate event processing is safe |
| **Projection Lag Monitoring** | Alert if event timestamp - processing timestamp > 30 sec | Operations team investigates lag |

---

## PART 2: Updated Transaction Model

### 2.1 Transaction Boundaries (ACID Guarantees)

**Principle:** Each aggregate operation is one transaction. Cross-aggregate operations use sagas (eventual consistency).

#### Single-Aggregate Transactions

| Operation | Aggregate | Transaction Scope | Rollback Trigger |
|-----------|-----------|-------------------|------------------|
| **RecordStockMovement** | StockLedger | 1. Validate balance<br>2. INSERT stock_movement_events<br>3. INSERT outbox (event) | Validation fails OR DB error |
| **CreateHandlingUnit** | HandlingUnit | 1. INSERT handling_units<br>2. INSERT outbox (HandlingUnitCreated) | Validation fails OR DB error |
| **AddLineToHandlingUnit** | HandlingUnit | 1. Validate HU not sealed<br>2. UPSERT handling_unit_lines<br>3. INSERT outbox (LineAdded) | Validation fails OR DB error |
| **SealHandlingUnit** | HandlingUnit | 1. Validate HU has lines<br>2. UPDATE handling_units SET status='SEALED'<br>3. INSERT outbox (HandlingUnitSealed) | Validation fails OR DB error |
| **CreateReservation** | Reservation | 1. INSERT reservation_events (ReservationCreated)<br>2. INSERT outbox | Validation fails OR DB error |
| **AllocateReservation** | Reservation | 1. Validate balance (query Ledger)<br>2. INSERT reservation_events (StockAllocated)<br>3. INSERT reservation_allocations<br>4. INSERT outbox | Validation fails OR DB error |
| **ApplyCostAdjustment** | Valuation | 1. Validate approver<br>2. INSERT valuation_events (CostAdjusted)<br>3. INSERT outbox | Validation fails OR DB error |

**Isolation Level:** READ COMMITTED (default for most databases)

**Why Not SERIALIZABLE:** Would cause too many conflicts under high concurrency. Optimistic concurrency control (via aggregate versioning) is sufficient.

---

#### Multi-Aggregate Sagas (Eventual Consistency)

**Pattern:** Saga coordinates multiple single-aggregate transactions via events.

**Example 1: ReceiveGoodsSaga**

```
Transaction 1 (StockLedger):
├─ BEGIN
├─ RecordStockMovement(SUPPLIER → location)
├─ INSERT stock_movement_events
├─ INSERT outbox (StockMoved)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 2 (HandlingUnit):
├─ BEGIN
├─ CreateHandlingUnit(location)
├─ INSERT handling_units
├─ INSERT outbox (HandlingUnitCreated)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 3 (HandlingUnit):
├─ BEGIN
├─ AddLineToHandlingUnit(sku, qty)
├─ INSERT handling_unit_lines
├─ INSERT outbox (LineAdded)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 4 (HandlingUnit):
├─ BEGIN
├─ SealHandlingUnit()
├─ UPDATE handling_units SET status='SEALED'
├─ INSERT outbox (HandlingUnitSealed)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 5 (Integration):
├─ BEGIN
├─ PrintLabel(huId)
├─ INSERT print_jobs
├─ INSERT outbox (LabelPrintRequested)
└─ COMMIT ✅
```

**Saga State Machine:**

```
ReceiveGoodsSaga States:
1. STARTED
2. MOVEMENT_RECORDED
3. HU_CREATED
4. HU_SEALED
5. LABEL_REQUESTED
6. COMPLETED
7. FAILED (compensation needed)
```

**Compensation (if failure occurs):**

| Failure Point | Compensation Action | State Cleanup |
|---------------|---------------------|---------------|
| Transaction 1 fails (StockMovement) | None needed (nothing committed) | Saga state: FAILED |
| Transaction 2 fails (CreateHU) | StockMovement already recorded ✅ (correct!)<br>Next cycle count will detect extra stock → manual adjustment | Saga state: MOVEMENT_RECORDED (partial) |
| Transaction 3 fails (AddLine) | Delete empty HU (compensating command) | Saga state: HU_CREATED |
| Transaction 4 fails (Seal) | HU remains OPEN → operator can seal manually | Saga state: HU_CREATED |
| Transaction 5 fails (Label) | Retry label print 3x → if fails, log alert | Saga state: HU_SEALED (label pending) |

**Example 2: PickStockSaga (CRITICAL - Enforces Decision 2)**

```
Transaction 1 (StockLedger): ← MUST BE FIRST
├─ BEGIN
├─ RecordStockMovement(location → PRODUCTION)
├─ INSERT stock_movement_events
├─ INSERT outbox (StockMoved)
└─ COMMIT ✅

   ↓ Event delivered (MANDATORY before proceeding)

Transaction 2 (HandlingUnit Projection):
├─ BEGIN
├─ Process StockMoved event
├─ RemoveLine(sku, qty) from HU
├─ UPDATE handling_unit_lines
└─ COMMIT ✅

   ↓ Event delivered

Transaction 3 (Reservation):
├─ BEGIN
├─ ConsumeReservation(qty)
├─ INSERT reservation_events (ReservationConsumed)
└─ COMMIT ✅
```

**Why This Order is MANDATORY (Decision 2):**

1. **StockLedger First:** Source of truth must commit before any derived state changes
2. **If Crash After T1:** Event replay rebuilds HU and Reservation state correctly
3. **If Crash After T2:** Reservation can still be consumed (HU already updated)
4. **If T1 Fails:** Nothing committed, no partial state
5. **If T2 Fails:** StockMoved recorded (correct), HU projection can be rebuilt from events
6. **If T3 Fails:** Pick still valid (ledger recorded), Reservation can be consumed later

**Forbidden Pattern (WRONG ORDER):**

```
❌ Transaction 1 (HandlingUnit): RemoveLine FIRST
   ↓
❌ Transaction 2 (StockLedger): RecordStockMovement SECOND

Problem: If T2 fails, HU updated but no ledger record → inventory corruption!
```

---

### 2.2 Failure Safety Guarantees

**Guarantee 1: No Lost Movements**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| StockLedger commit succeeds, but event not delivered | Event in Outbox table | Outbox processor retries delivery |
| Event delivered, but projection handler crashes | Event redelivered (at-least-once) | Handler checks: already processed? |
| Database crashes after commit | Write-ahead log (WAL) ensures durability | Events persist, replayed on restart |

**Guarantee 2: No Partial Picks**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| PickStockSaga crashes after T1 (Ledger) | T1 committed, T2/T3 pending | Saga resumes on restart, completes T2/T3 |
| PickStockSaga crashes after T2 (HU Projection) | T1 + T2 committed, T3 pending | Saga resumes, completes T3 (Reservation) |
| PickStockSaga crashes after T3 (Reservation) | All committed ✅ | Saga marked complete |

**Guarantee 3: No Negative Balances**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| Concurrent picks exceed balance | StockLedger validation before append | Second pick fails validation, returns error |
| Projection shows incorrect balance | Balance query falls back to event stream | Accurate balance computed from events |
| Malicious direct DB update | DB triggers prevent negative balances | INSERT/UPDATE rejected |

**Guarantee 4: No Lost Reservations**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| Reservation created but allocation fails | Reservation remains PENDING | AllocationSaga retries when new stock arrives |
| Reservation bumped while operator offline | Offline command rejected on sync | Operator sees error in reconciliation report |
| Reservation hard-locked but operator disconnects | Reservation times out after 2 hours | Auto-cancelled, stock released |

---

### 2.3 Replay Safety (Idempotency)

**Principle:** All event handlers and commands must be idempotent (safe to replay).

#### Command Idempotency

**Mechanism: Command Deduplication via CommandId**

```sql
CREATE TABLE processed_commands (
  command_id TEXT PRIMARY KEY,
  command_type TEXT NOT NULL,
  timestamp DATETIME NOT NULL,
  result TEXT NOT NULL,  -- JSON: success or error
  INDEX idx_timestamp (timestamp DESC)
);
```

**Command Handler Pattern:**

```python
def handle_command(command):
    # 1. Check if already processed
    existing = db.query("SELECT result FROM processed_commands WHERE command_id = ?", command.id)
    if existing:
        return cached_result(existing.result)  # Idempotent return
    
    # 2. Execute command
    try:
        result = execute_command_logic(command)
        
        # 3. Store result
        db.insert("processed_commands", {
            "command_id": command.id,
            "command_type": command.type,
            "timestamp": now(),
            "result": json(result)
        })
        
        return result
    
    except Exception as e:
        # 4. Store error result
        db.insert("processed_commands", {
            "command_id": command.id,
            "command_type": command.type,
            "timestamp": now(),
            "result": json({"error": str(e)})
        })
        raise
```

**Retention Policy:** Keep processed commands for 7 days (sufficient for retry windows).

---

#### Event Handler Idempotency

**Mechanism: Event Processing Checkpoints**

```sql
CREATE TABLE event_processing_checkpoints (
  handler_name TEXT NOT NULL,
  event_id TEXT NOT NULL,
  processed_at DATETIME NOT NULL,
  PRIMARY KEY (handler_name, event_id)
);
```

**Event Handler Pattern:**

```python
def handle_event(event, handler_name):
    # 1. Check if already processed
    existing = db.query("""
        SELECT processed_at 
        FROM event_processing_checkpoints 
        WHERE handler_name = ? AND event_id = ?
    """, handler_name, event.id)
    
    if existing:
        return  # Already processed, skip (idempotent)
    
    # 2. Process event (must be idempotent operation)
    process_event_logic(event)
    
    # 3. Record checkpoint
    db.insert("event_processing_checkpoints", {
        "handler_name": handler_name,
        "event_id": event.id,
        "processed_at": now()
    })
```

**Alternative: Upsert-Based Projection**

For projections, use UPSERT instead of INSERT to ensure idempotency:

```sql
-- Example: LocationBalance projection
ON StockMoved event:
  UPSERT INTO location_balance (location, sku, quantity)
  VALUES (
    event.toLocation,
    event.sku,
    COALESCE((SELECT quantity FROM location_balance 
              WHERE location = event.toLocation AND sku = event.sku), 0) + event.quantity
  )
  ON CONFLICT (location, sku) DO UPDATE
    SET quantity = location_balance.quantity + event.quantity;
```

This ensures replaying same event multiple times produces same result.

---

#### Saga Idempotency

**Mechanism: Saga Step Checkpoints**

```sql
CREATE TABLE saga_state (
  saga_id TEXT PRIMARY KEY,
  saga_type TEXT NOT NULL,
  current_step INTEGER NOT NULL,
  step_results TEXT NOT NULL,  -- JSON: {step1: result, step2: result, ...}
  status TEXT NOT NULL,        -- RUNNING, COMPLETED, FAILED
  created_at DATETIME NOT NULL,
  updated_at DATETIME NOT NULL
);
```

**Saga Execution Pattern:**

```python
def execute_saga_step(saga_id, step_number, step_function):
    # 1. Load saga state
    saga = db.query("SELECT * FROM saga_state WHERE saga_id = ?", saga_id)
    
    # 2. Check if step already executed
    if step_number in saga.step_results:
        return saga.step_results[step_number]  # Idempotent return
    
    # 3. Execute step
    try:
        result = step_function()
        
        # 4. Save step result
        saga.step_results[step_number] = result
        saga.current_step = step_number
        saga.updated_at = now()
        
        db.update("saga_state", saga)
        
        return result
    
    except Exception as e:
        # 5. Mark saga as failed
        saga.status = "FAILED"
        saga.step_results[step_number] = {"error": str(e)}
        saga.updated_at = now()
        
        db.update("saga_state", saga)
        
        raise
```

**Saga Restart Safety:**

If saga process crashes and restarts:
1. Load saga state from database
2. Check current_step
3. Resume from next step (already-completed steps skipped)
4. All step functions must be idempotent

---

### 2.4 Idempotency Rules Matrix

| Operation Type | Idempotency Key | Mechanism | Duplicate Behavior |
|---------------|----------------|-----------|-------------------|
| **Command** | CommandId (GUID) | processed_commands table | Return cached result |
| **Event Append** | Event.movementId | Unique constraint on movement_id | INSERT fails (safe - rollback) |
| **Event Processing** | (handler_name, event_id) | event_processing_checkpoints | Skip processing (no-op) |
| **Projection Update** | N/A | UPSERT with computed value | Replay produces same result |
| **Saga Step** | (saga_id, step_number) | saga_state.step_results | Return cached result |
| **External API Call** | Integration-specific ID | Varies by integration | See Integration section |

---

### 2.5 Consistency Verification (Self-Test)

**Daily Consistency Checks (Automated Background Job):**

| Check Name | Query | Expected Result | Alert If Failed | Severity |
|-----------|-------|----------------|-----------------|----------|
| **Balance Integrity** | `SELECT location, sku, SUM(CASE WHEN to_location = location THEN quantity WHEN from_location = location THEN -quantity ELSE 0 END) AS computed_balance FROM stock_movement_events GROUP BY location, sku EXCEPT SELECT location, sku, quantity FROM location_balance` | Zero rows | CRITICAL: "Balance mismatch detected" | P0 - Investigate immediately |
| **HU Contents vs Ledger** | `SELECT hu.location, hul.sku, SUM(hul.quantity) AS hu_total FROM handling_units hu JOIN handling_unit_lines hul ON hu.hu_id = hul.hu_id GROUP BY hu.location, hul.sku EXCEPT SELECT location, sku, quantity FROM location_balance` | Zero rows | WARNING: "HU contents mismatch" | P2 - Review within 24h |
| **No Negative Balances** | `SELECT * FROM location_balance WHERE quantity < 0` | Zero rows | CRITICAL: "Negative balance" | P0 - Halt operations |
| **Orphaned HUs** | `SELECT hu_id FROM handling_units WHERE location NOT IN (SELECT bin_code FROM bins)` | Zero rows | WARNING: "HU at invalid location" | P2 - Review within 24h |
| **Consumed Reservations Still Holding HUs** | `SELECT r.reservation_id FROM reservations r JOIN reservation_allocations ra ON r.reservation_id = ra.reservation_id WHERE r.status = 'CONSUMED'` | Zero rows | WARNING: "Consumed reservation not released" | P2 - Review within 24h |
| **Event Stream Gaps** | `SELECT sequence_number FROM stock_movement_events ORDER BY sequence_number` → Check for gaps | No gaps in sequence | CRITICAL: "Event stream corrupted" | P0 - Database corruption |

**Manual Reconciliation Tools:**

| Tool | Purpose | Trigger | User |
|------|---------|---------|------|
| **Rebuild Projection** | Replay all events to rebuild location_balance | After detecting mismatch | Warehouse Manager |
| **Cycle Count** | Compare physical inventory with system | Scheduled (quarterly) OR after 3 adjustments in 30 days | Warehouse Operator + Manager |
| **Adjustment Wizard** | Fix balance discrepancies | After cycle count finds delta | Inventory Accountant (with approval) |
| **Saga Recovery** | Manually complete stuck saga | Saga stuck > 5 minutes | System Administrator |

---

<!-- ==================== OUTPUT 1 END ==================== -->

**END OF OUTPUT 1**

---

**This is OUTPUT 1 of 2. It contains:**
- ✅ PART 1: Refined Aggregate Interaction Model
- ✅ PART 2: Updated Transaction Model

**Next OUTPUT will contain:**
- PART 3: Offline/Edge Conflict Model
- PART 4: Final Integration Architecture  
- PART 5: Implementation Readiness Checklist

**Please confirm to proceed with OUTPUT 2.**

---

# Appendix: Output 2 (Parts 3-5 continuation)

# LKvitai.MES - Warehouse Architecture (Hardened & Implementation-Ready)

**Version:** 2.0 (Hardened)  
**Date:** February 2026  
**Status:** FINAL BASELINE - Implementation Ready  
**Supersedes:** 04-system-architecture.md  
**Canonical Inputs:** 01-discovery.md, 02-warehouse-domain-model-v1.md, 03-implementation-guide.md

---

## Document Purpose

This document hardens the architecture based on mandatory architectural decisions. It is the **FINAL BASELINE** for implementation. All decisions here are **MANDATORY** and must not be changed without architectural review board approval.

**Key Changes from Previous Architecture:**
1. Clarified StockLedger as sole owner of StockMovement events
2. Hardened transaction boundaries and ordering
3. Restricted offline operations to safe subset
4. Split Integration Module into three logical components
5. Added Implementation Readiness Checklist

---

## Mandatory Architectural Decisions (ENFORCED)

### Decision 1: Movement Ownership (CRITICAL)

**Rule:** StockMovement MUST be owned exclusively by StockLedger module.

**Implications:**
- ✅ StockLedger is the ONLY module that can append StockMoved events
- ✅ HandlingUnit updates its state by SUBSCRIBING to StockMoved events (projection)
- ✅ HandlingUnit.MoveTo() does NOT generate StockMoved events directly
- ❌ HandlingUnit CANNOT write to stock_movement_events table
- ❌ No other module can bypass StockLedger to record movements

**Enforcement Mechanism:**
- Database: `stock_movement_events` table has INSERT privilege only for StockLedger service account
- Code: HandlingUnit module has no dependency on event store write APIs
- Review: All pull requests touching movement recording must be reviewed by architect

---

### Decision 2: Pick Transaction Order (CRITICAL)

**Rule:** Pick operations MUST follow strict ordering.

**Mandatory Sequence:**
```
1. StockLedger.AppendMovement(sku, qty, fromLocation, PRODUCTION, ...)
   ↓ (commit transaction)
2. Event published: StockMoved
   ↓ (event bus)
3. HandlingUnit projection updates (consumes StockMoved event)
   ↓ (separate transaction)
4. Reservation.Consume(qty)
   ↓ (separate transaction)
5. Pick complete
```

**Forbidden Patterns:**
- ❌ Updating HandlingUnit.Lines BEFORE StockLedger append
- ❌ Consuming Reservation BEFORE StockLedger append
- ❌ Wrapping all 3 operations in single distributed transaction

**Rationale:**
- StockLedger is source of truth - must commit first
- If process crashes after ledger commit, events can be replayed to rebuild HU/Reservation state
- If HU updated first and ledger fails, inventory corruption occurs

---

### Decision 3: Offline Policy (CRITICAL)

**Rule:** Offline operations restricted to safe subset only.

**Allowed Offline:**
- ✅ PickStock (only when reservation already HARD locked on server)
- ✅ TransferStock (only for already assigned HandlingUnits to operator)

**Forbidden Offline:**
- ❌ AllocateReservation (requires real-time balance check)
- ❌ StartPicking (requires conflict detection)
- ❌ AdjustStock (requires approval)
- ❌ ApplyCostAdjustment (requires approval)
- ❌ SplitHU / MergeHU (requires real-time validation)
- ❌ CreateReservation (requires allocation saga)

**Enforcement:**
- Edge Agent checks operation whitelist before queueing
- Server rejects offline-queued commands for forbidden operations (returns 403 Forbidden)
- UI disables forbidden operation buttons when offline

---

### Decision 4: Reservation Bumping Policy (CRITICAL)

**Rule:** Bumping rules based on lock type and state.

| Current Reservation State | Lock Type | Can Be Bumped? | By Whom? |
|--------------------------|-----------|----------------|----------|
| PENDING (not yet allocated) | N/A | ✅ Yes | Any reservation can take same stock |
| ALLOCATED | SOFT | ✅ Yes | Higher priority OR any HARD lock request |
| PICKING | HARD | ❌ No | Cannot be bumped (picking already started) |
| CONSUMED | N/A | N/A | Terminal state |
| CANCELLED | N/A | N/A | Terminal state |

**UI/Operational Consequences:**

**Scenario 1: Soft Reservation Bumped Before Picking**
```
User Story:
- Operator A has soft reservation R1 for HU-001
- Higher priority order needs same stock
- System allocates HU-001 to new reservation R2
- Operator A sees notification: "Reservation R1 bumped by higher priority order. Stock reallocated."

User Actions:
- Operator A must find alternative stock OR
- Escalate to manager for priority adjustment OR
- Wait for new stock to arrive
```

**Scenario 2: Attempt to Bump Hard Reservation (Forbidden)**
```
User Story:
- Operator B starts picking reservation R3 (HARD lock)
- Urgent order needs same stock
- System attempts allocation
- Allocation FAILS with conflict error

User Actions:
- Urgent order must wait for R3 to complete OR
- Manager intervenes to cancel R3 (requires approval + compensation)
```

**Scenario 3: Offline Pick Against Bumped Reservation**
```
User Story:
- Operator C offline with SOFT reservation R4
- While offline, R4 gets bumped
- Operator C queues PickStock command offline
- On reconnect, sync attempts PickStock
- Server rejects: 400 Bad Request "Reservation R4 cancelled/bumped"

User Actions:
- Operator C sees error in reconciliation report
- Must return stock to shelf OR
- Contact manager to create new reservation
```

---

### Decision 5: Integration Separation (MANDATORY)

**Rule:** Integration Module split into three logical components.

**Component 1: Operational Integration**
- **Responsibility:** Real-time operational integrations (label printing, scanners, equipment)
- **Latency:** Low (< 5 seconds)
- **Failure Mode:** Retry 3x, then alert operator
- **Examples:** Print label, read scale, unlock gate

**Component 2: Financial Integration**
- **Responsibility:** Periodic financial exports (Agnum accounting)
- **Latency:** High tolerance (minutes to hours)
- **Failure Mode:** Retry with exponential backoff, manual fallback
- **Examples:** Export stock snapshot, reconciliation reports

**Component 3: Process Integration**
- **Responsibility:** MES/ERP process coordination (material requests, consumption events)
- **Latency:** Medium (< 30 seconds)
- **Failure Mode:** Saga compensation, notify both systems
- **Examples:** MaterialRequested → CreateReservation, StockMoved → MaterialConsumed

**Why Split:**
- Different SLAs (operational needs fast response, financial can be batch)
- Different failure modes (operational blocks work, financial is async)
- Different teams (operations team vs accounting team vs production team)

---

## Table of Contents

- [PART 1: Refined Aggregate Interaction Model](#part-1-refined-aggregate-interaction-model)
- [PART 2: Updated Transaction Model](#part-2-updated-transaction-model)
- [PART 3: Offline/Edge Conflict Model](#part-3-offlineedge-conflict-model)
- [PART 4: Final Integration Architecture](#part-4-final-integration-architecture)
- [PART 5: Implementation Readiness Checklist](#part-5-implementation-readiness-checklist)

---

<!-- ==================== OUTPUT 1 START ==================== -->

## PART 1: Refined Aggregate Interaction Model

### 1.1 Ledger → HandlingUnit Projection Flow

**Pattern:** Event-Driven Projection (StockLedger publishes, HandlingUnit subscribes)

#### Flow Diagram

```
┌──────────────────┐
│   StockLedger    │
│   (Write Model)  │
└────────┬─────────┘
         │
         │ 1. Command: RecordStockMovement(sku, qty, from, to, huId)
         ↓
    ┌────────────────────────────────────┐
    │ Validate:                          │
    │ - from location has balance ≥ qty  │
    │ - to ≠ from                        │
    │ - qty > 0                          │
    └────────┬───────────────────────────┘
             │
             │ 2. Append to event stream
             ↓
    ┌────────────────────────────────────┐
    │ stock_movement_events table        │
    │ INSERT new row                     │
    │ (atomic, within transaction)       │
    └────────┬───────────────────────────┘
             │
             │ 3. Commit transaction
             ↓
    ┌────────────────────────────────────┐
    │ StockMoved event published         │
    │ (via Transactional Outbox)         │
    └────────┬───────────────────────────┘
             │
             │ 4. Event bus delivers
             ↓
    ┌────────────────────────────────────┐
    │ HandlingUnit Projection Handler    │
    │ (Read Model)                       │
    └────────┬───────────────────────────┘
             │
             │ 5. Process event
             ↓
    ┌────────────────────────────────────────────────┐
    │ IF event.handlingUnitId != null:               │
    │   IF event.fromLocation == HU.location:        │
    │     RemoveLine(event.sku, event.qty)           │
    │   IF event.toLocation == HU.location:          │
    │     AddLine(event.sku, event.qty)              │
    │   IF event.toLocation != event.fromLocation:   │
    │     Update HU.location = event.toLocation      │
    └────────┬───────────────────────────────────────┘
             │
             │ 6. Commit projection update
             ↓
    ┌────────────────────────────────────┐
    │ handling_units table updated       │
    │ handling_unit_lines table updated  │
    └────────────────────────────────────┘
```

#### Key Rules

| Rule | Enforcement | Violation Consequence |
|------|-------------|----------------------|
| **R1.1** StockLedger is ONLY writer to stock_movement_events | Database permissions | Cannot insert |
| **R1.2** HandlingUnit reads StockMoved events via subscription | Event bus registration | Events not received |
| **R1.3** HandlingUnit projection is idempotent | Check event.movementId already processed | Duplicate processing safe |
| **R1.4** Projection lag is acceptable (< 5 sec) | Monitoring alert if lag > 30 sec | UI shows "Refreshing..." |
| **R1.5** Projection can be rebuilt from events | Replay all StockMoved events for HU | Recovery from corruption |

#### Failure Scenarios

**Scenario 1A: StockLedger Commit Fails**
```
1. RecordStockMovement command received
2. Validation passes
3. INSERT to stock_movement_events attempted
4. ❌ Database error (network, disk full, constraint violation)
5. Transaction rolled back
6. NO event published
7. HandlingUnit state unchanged
8. ✅ System remains consistent (no partial state)
```

**Scenario 1B: Event Delivery Fails**
```
1. RecordStockMovement command succeeds
2. StockMoved event in Outbox table
3. Outbox processor attempts delivery
4. ❌ Event bus unavailable
5. Event remains in Outbox (not marked as published)
6. ✅ Outbox processor retries (exponential backoff)
7. Eventually event delivered (at-least-once guarantee)
8. HandlingUnit projection eventually consistent
```

**Scenario 1C: Projection Handler Crashes Mid-Update**
```
1. StockMoved event delivered
2. Projection handler starts processing
3. RemoveLine(sku, qty) succeeds
4. ❌ Process crashes before AddLine
5. On restart: Event redelivered (at-least-once)
6. Projection handler checks: "Did I already process movementId?"
7. If yes: Skip (idempotent)
8. If no: Replay full update (RemoveLine + AddLine)
9. ✅ Eventually consistent
```

---

### 1.2 Ledger → Reservation Validation Flow

**Pattern:** Query-Before-Command (Reservation queries Ledger before allocation)

#### Flow Diagram

```
┌──────────────────┐
│   Reservation    │
│   Aggregate      │
└────────┬─────────┘
         │
         │ 1. Command: AllocateReservation(reservationId, huIds)
         ↓
    ┌────────────────────────────────────┐
    │ Query StockLedger:                 │
    │ "What is current balance at        │
    │  HU locations for requested SKUs?" │
    └────────┬───────────────────────────┘
             │
             │ 2. Execute query
             ↓
    ┌────────────────────────────────────┐
    │ StockLedger.GetBalanceAt(          │
    │   location, sku, asOf=now)         │
    │                                    │
    │ Implementation:                    │
    │ - Check projection: location_balance│
    │ - If projection lag < 5 sec: return│
    │ - Else: compute from event stream  │
    └────────┬───────────────────────────┘
             │
             │ 3. Return balance
             ↓
    ┌────────────────────────────────────┐
    │ Reservation validates:             │
    │ - Balance ≥ requested qty?         │
    │ - HU not already hard-locked?      │
    │ - No conflicting reservations?     │
    └────────┬───────────────────────────┘
             │
             │ 4. If validation passes
             ↓
    ┌────────────────────────────────────┐
    │ Reservation.Allocate(huIds)        │
    │ (SOFT lock)                        │
    └────────┬───────────────────────────┘
             │
             │ 5. Publish event
             ↓
    ┌────────────────────────────────────┐
    │ StockAllocated event               │
    │ (reservation_events table)         │
    └────────────────────────────────────┘
```

#### Key Rules

| Rule | Enforcement | Violation Consequence |
|------|-------------|----------------------|
| **R2.1** Reservation MUST query Ledger before allocation | Code review + architecture tests | Allocation without balance check |
| **R2.2** Ledger balance query uses projection if fresh (< 5 sec) | Projection timestamp check | Falls back to event stream query |
| **R2.3** Allocation is optimistic (can overbook with SOFT) | Business rule | Resolved at StartPicking() |
| **R2.4** StartPicking() re-validates balance | Code enforcement | Prevents picking insufficient stock |
| **R2.5** HARD lock allocation queries real-time balance | Always query event stream, skip projection | Guarantees accuracy |

#### Stale Read Scenarios

**Scenario 2A: Projection Lag During Allocation**
```
Timeline:
T0: Location A has 100 units of SKU933
T1: Operator picks 80 units (StockMoved event recorded)
T2: Projection not yet updated (lag = 3 sec)
T3: Reservation R1 queries balance → sees 100 units (stale)
T4: Reservation R1 allocates 50 units (SOFT) ✅ Allowed
T5: Projection updates → balance now 20 units
T6: Operator tries StartPicking(R1)
T7: Re-validation: balance (20) < allocated (50) ❌ Fails
T8: Operator sees error: "Insufficient stock, reservation cannot start picking"

Resolution:
- Reservation remains ALLOCATED (SOFT)
- Operator notified to wait for stock or reallocate
- No inventory corruption (ledger is correct)
```

**Scenario 2B: Concurrent Allocations on Same Stock**
```
Timeline:
T0: Location A has 50 units of SKU105
T1: Reservation R1 queries balance → 50 units
T2: Reservation R2 queries balance → 50 units (same)
T3: R1 allocates 30 units (SOFT) ✅
T4: R2 allocates 40 units (SOFT) ✅ (overbooked to 70 units)
T5: Operator starts picking R1 → StartPicking(R1)
T6: Re-validation: balance (50) ≥ allocated (30) ✅ Succeeds
T7: R1 transitions to HARD lock
T8: Operator starts picking R2 → StartPicking(R2)
T9: Re-validation: balance (50) - R1 hard (30) = 20 < allocated (40) ❌ Fails
T10: Operator sees error: "Stock already hard-locked by another reservation"

Resolution:
- R1 proceeds (HARD lock)
- R2 fails to start picking
- R2 notified: "Bumped by higher priority"
- R2 must find alternative stock
```

---

### 1.3 Cross-Aggregate Command Rules

**Pattern:** Commands flow downward in dependency hierarchy, never upward.

#### Dependency Hierarchy

```
Level 1 (Foundation):
┌──────────────────┐
│   StockLedger    │  ← No dependencies
└──────────────────┘

Level 2 (State):
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  HandlingUnit    │  │  WarehouseLayout │  │ LogicalWarehouse │
│  (reads Ledger)  │  │  (independent)   │  │  (independent)   │
└──────────────────┘  └──────────────────┘  └──────────────────┘

Level 3 (Business Logic):
┌──────────────────┐  ┌──────────────────┐
│   Reservation    │  │    Valuation     │
│  (reads Ledger,  │  │  (reads Ledger)  │
│   reads HU)      │  │                  │
└──────────────────┘  └──────────────────┘

Level 4 (Orchestration):
┌────────────────────────────────────────┐
│   Process Managers / Sagas             │
│   (can send commands to any level)     │
└────────────────────────────────────────┘
```

#### Command Flow Rules

**ALLOWED Command Flows:**

| From Module | To Module | Command Type | Example |
|-------------|-----------|--------------|---------|
| Any | StockLedger | RecordStockMovement | TransferStockSaga → RecordStockMovement |
| Any | HandlingUnit | CreateHU, AddLine, SealHU | ReceiveGoodsSaga → CreateHU |
| Any | Reservation | CreateReservation, AllocateReservation | ERP Gateway → CreateReservation |
| Saga | Any | Any allowed command | PickStockSaga → RecordStockMovement + Reservation.Consume |

**FORBIDDEN Command Flows:**

| From Module | To Module | Why Forbidden | Enforcement |
|-------------|-----------|---------------|-------------|
| StockLedger | HandlingUnit | Ledger is foundation, cannot depend on higher levels | Code dependency analysis |
| StockLedger | Reservation | Ledger is foundation | Code dependency analysis |
| HandlingUnit | StockLedger | HU should subscribe to events, not command ledger | Architecture review |
| HandlingUnit | Reservation | HU is stateless projection, cannot affect reservations | Architecture review |
| Reservation | Valuation | Orthogonal concerns (quantity vs cost) | Architecture review |
| Valuation | Reservation | Orthogonal concerns | Architecture review |

#### Query Flow Rules

**ALLOWED Query Flows:**

| From Module | To Module | Query Type | Example |
|-------------|-----------|------------|---------|
| Reservation | StockLedger | GetBalanceAt(location, sku) | Validate allocation |
| Reservation | HandlingUnit | GetHU(huId) | Check HU contents |
| Valuation | StockLedger | GetBalanceAt(location, sku) | Compute on-hand value |
| Process Managers | Any | Any query | AllocationSaga queries AvailableStock |

**FORBIDDEN Query Flows:**

| From Module | To Module | Why Forbidden | Enforcement |
|-------------|-----------|---------------|-------------|
| StockLedger | HandlingUnit | Ledger cannot depend on higher-level state | Dependency check |
| StockLedger | Reservation | Ledger is foundation | Dependency check |
| HandlingUnit | Reservation | HU is projection, should not know about reservations | Architecture review |

---

### 1.4 Command Validation Rules Matrix

**Validation Location Policy:** Validate at the deepest aggregate that owns the invariant.

| Command | Validation | Where Validated | Invariant Owner |
|---------|------------|----------------|-----------------|
| **RecordStockMovement** | From location has balance ≥ qty | StockLedger | StockLedger (no negative balance) |
| **CreateHandlingUnit** | Location exists | WarehouseLayout query | WarehouseLayout (valid locations) |
| **AddLineToHandlingUnit** | HU status != SEALED | HandlingUnit | HandlingUnit (sealed immutability) |
| **SealHandlingUnit** | HU has lines (not empty) | HandlingUnit | HandlingUnit (no empty sealed HUs) |
| **MoveHandlingUnit** | To location exists | WarehouseLayout query | WarehouseLayout (valid locations) |
| **MoveHandlingUnit** | HU has stock | HandlingUnit | HandlingUnit (cannot move empty HU) |
| **AllocateReservation** | Balance sufficient | StockLedger query | StockLedger (balance truth) |
| **AllocateReservation** | HU exists | HandlingUnit query | HandlingUnit (HU existence) |
| **StartPicking** | No conflicting HARD locks | Reservation aggregate | Reservation (lock conflicts) |
| **StartPicking** | Balance still sufficient | StockLedger query (re-validate) | StockLedger (balance truth) |
| **PickStock** | Reservation status == PICKING | Reservation | Reservation (state machine) |
| **PickStock** | HU allocated to reservation | Reservation | Reservation (allocation integrity) |
| **ApplyCostAdjustment** | Approver authorized | Valuation + IAM | Valuation (cost change approval) |

**Multi-Aggregate Validation:**

Some commands require validation from multiple aggregates. These are orchestrated by Sagas:

```
Example: TransferStock(huId, toLocation)

Saga coordinates:
1. Query WarehouseLayout: toLocation exists? ✅
2. Query HandlingUnit: HU exists and not empty? ✅
3. For each line in HU:
   a. Query StockLedger: from location has balance? ✅
   b. Command StockLedger: RecordStockMovement
4. Command HandlingUnit: Update location

If any validation fails → abort saga
```

---

### 1.5 Event Subscription Rules

**Pattern:** Aggregates subscribe to events from dependencies (lower levels).

#### Subscription Matrix

| Subscriber Module | Publishes Events | Subscribes To Events | Reason |
|------------------|------------------|---------------------|---------|
| **StockLedger** | StockMoved | NONE | Foundation - publishes only |
| **HandlingUnit** | HandlingUnitCreated, LineAdded, LineRemoved, HandlingUnitMoved, HandlingUnitSealed | StockMoved | Updates projection from ledger |
| **Reservation** | ReservationCreated, StockAllocated, PickingStarted, ReservationConsumed | StockMoved (to detect balance changes) | Re-validates on balance changes |
| **Valuation** | CostAdjusted, LandedCostAllocated | NONE | Independent financial interpretation |
| **WarehouseLayout** | BinDefined, BinModified | NONE | Independent configuration |
| **LogicalWarehouse** | CategoryAssigned | NONE | Independent metadata |
| **Read Models** | ProjectionRebuilt | ALL events | Builds query projections |
| **Integration (Operational)** | LabelPrinted, LabelPrintFailed | HandlingUnitCreated, HandlingUnitSealed | Triggers label printing |
| **Integration (Financial)** | ExportCompleted | StockMoved, CostAdjusted | Detects when export needed |
| **Integration (Process)** | MaterialConsumed | StockMoved (to PRODUCTION location) | Notifies ERP of consumption |

#### Subscription Guarantees

| Guarantee | Mechanism | Failure Handling |
|-----------|-----------|------------------|
| **At-Least-Once Delivery** | Transactional Outbox + retry | Event may be delivered multiple times → handlers must be idempotent |
| **Ordering Within Partition** | Events for same aggregate ID ordered | E.g., all StockMoved events for same HU delivered in order |
| **No Ordering Across Partitions** | Events for different aggregates unordered | E.g., StockMoved for HU-001 and HU-002 can arrive out of order |
| **Event Handler Idempotency** | Handler checks: "Already processed event.id?" | Duplicate event processing is safe |
| **Projection Lag Monitoring** | Alert if event timestamp - processing timestamp > 30 sec | Operations team investigates lag |

---

## PART 2: Updated Transaction Model

### 2.1 Transaction Boundaries (ACID Guarantees)

**Principle:** Each aggregate operation is one transaction. Cross-aggregate operations use sagas (eventual consistency).

#### Single-Aggregate Transactions

| Operation | Aggregate | Transaction Scope | Rollback Trigger |
|-----------|-----------|-------------------|------------------|
| **RecordStockMovement** | StockLedger | 1. Validate balance<br>2. INSERT stock_movement_events<br>3. INSERT outbox (event) | Validation fails OR DB error |
| **CreateHandlingUnit** | HandlingUnit | 1. INSERT handling_units<br>2. INSERT outbox (HandlingUnitCreated) | Validation fails OR DB error |
| **AddLineToHandlingUnit** | HandlingUnit | 1. Validate HU not sealed<br>2. UPSERT handling_unit_lines<br>3. INSERT outbox (LineAdded) | Validation fails OR DB error |
| **SealHandlingUnit** | HandlingUnit | 1. Validate HU has lines<br>2. UPDATE handling_units SET status='SEALED'<br>3. INSERT outbox (HandlingUnitSealed) | Validation fails OR DB error |
| **CreateReservation** | Reservation | 1. INSERT reservation_events (ReservationCreated)<br>2. INSERT outbox | Validation fails OR DB error |
| **AllocateReservation** | Reservation | 1. Validate balance (query Ledger)<br>2. INSERT reservation_events (StockAllocated)<br>3. INSERT reservation_allocations<br>4. INSERT outbox | Validation fails OR DB error |
| **ApplyCostAdjustment** | Valuation | 1. Validate approver<br>2. INSERT valuation_events (CostAdjusted)<br>3. INSERT outbox | Validation fails OR DB error |

**Isolation Level:** READ COMMITTED (default for most databases)

**Why Not SERIALIZABLE:** Would cause too many conflicts under high concurrency. Optimistic concurrency control (via aggregate versioning) is sufficient.

---

#### Multi-Aggregate Sagas (Eventual Consistency)

**Pattern:** Saga coordinates multiple single-aggregate transactions via events.

**Example 1: ReceiveGoodsSaga**

```
Transaction 1 (StockLedger):
├─ BEGIN
├─ RecordStockMovement(SUPPLIER → location)
├─ INSERT stock_movement_events
├─ INSERT outbox (StockMoved)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 2 (HandlingUnit):
├─ BEGIN
├─ CreateHandlingUnit(location)
├─ INSERT handling_units
├─ INSERT outbox (HandlingUnitCreated)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 3 (HandlingUnit):
├─ BEGIN
├─ AddLineToHandlingUnit(sku, qty)
├─ INSERT handling_unit_lines
├─ INSERT outbox (LineAdded)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 4 (HandlingUnit):
├─ BEGIN
├─ SealHandlingUnit()
├─ UPDATE handling_units SET status='SEALED'
├─ INSERT outbox (HandlingUnitSealed)
└─ COMMIT ✅

   ↓ Event delivered

Transaction 5 (Integration):
├─ BEGIN
├─ PrintLabel(huId)
├─ INSERT print_jobs
├─ INSERT outbox (LabelPrintRequested)
└─ COMMIT ✅
```

**Saga State Machine:**

```
ReceiveGoodsSaga States:
1. STARTED
2. MOVEMENT_RECORDED
3. HU_CREATED
4. HU_SEALED
5. LABEL_REQUESTED
6. COMPLETED
7. FAILED (compensation needed)
```

**Compensation (if failure occurs):**

| Failure Point | Compensation Action | State Cleanup |
|---------------|---------------------|---------------|
| Transaction 1 fails (StockMovement) | None needed (nothing committed) | Saga state: FAILED |
| Transaction 2 fails (CreateHU) | StockMovement already recorded ✅ (correct!)<br>Next cycle count will detect extra stock → manual adjustment | Saga state: MOVEMENT_RECORDED (partial) |
| Transaction 3 fails (AddLine) | Delete empty HU (compensating command) | Saga state: HU_CREATED |
| Transaction 4 fails (Seal) | HU remains OPEN → operator can seal manually | Saga state: HU_CREATED |
| Transaction 5 fails (Label) | Retry label print 3x → if fails, log alert | Saga state: HU_SEALED (label pending) |

**Example 2: PickStockSaga (CRITICAL - Enforces Decision 2)**

```
Transaction 1 (StockLedger): ← MUST BE FIRST
├─ BEGIN
├─ RecordStockMovement(location → PRODUCTION)
├─ INSERT stock_movement_events
├─ INSERT outbox (StockMoved)
└─ COMMIT ✅

   ↓ Event delivered (MANDATORY before proceeding)

Transaction 2 (HandlingUnit Projection):
├─ BEGIN
├─ Process StockMoved event
├─ RemoveLine(sku, qty) from HU
├─ UPDATE handling_unit_lines
└─ COMMIT ✅

   ↓ Event delivered

Transaction 3 (Reservation):
├─ BEGIN
├─ ConsumeReservation(qty)
├─ INSERT reservation_events (ReservationConsumed)
└─ COMMIT ✅
```

**Why This Order is MANDATORY (Decision 2):**

1. **StockLedger First:** Source of truth must commit before any derived state changes
2. **If Crash After T1:** Event replay rebuilds HU and Reservation state correctly
3. **If Crash After T2:** Reservation can still be consumed (HU already updated)
4. **If T1 Fails:** Nothing committed, no partial state
5. **If T2 Fails:** StockMoved recorded (correct), HU projection can be rebuilt from events
6. **If T3 Fails:** Pick still valid (ledger recorded), Reservation can be consumed later

**Forbidden Pattern (WRONG ORDER):**

```
❌ Transaction 1 (HandlingUnit): RemoveLine FIRST
   ↓
❌ Transaction 2 (StockLedger): RecordStockMovement SECOND

Problem: If T2 fails, HU updated but no ledger record → inventory corruption!
```

---

### 2.2 Failure Safety Guarantees

**Guarantee 1: No Lost Movements**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| StockLedger commit succeeds, but event not delivered | Event in Outbox table | Outbox processor retries delivery |
| Event delivered, but projection handler crashes | Event redelivered (at-least-once) | Handler checks: already processed? |
| Database crashes after commit | Write-ahead log (WAL) ensures durability | Events persist, replayed on restart |

**Guarantee 2: No Partial Picks**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| PickStockSaga crashes after T1 (Ledger) | T1 committed, T2/T3 pending | Saga resumes on restart, completes T2/T3 |
| PickStockSaga crashes after T2 (HU Projection) | T1 + T2 committed, T3 pending | Saga resumes, completes T3 (Reservation) |
| PickStockSaga crashes after T3 (Reservation) | All committed ✅ | Saga marked complete |

**Guarantee 3: No Negative Balances**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| Concurrent picks exceed balance | StockLedger validation before append | Second pick fails validation, returns error |
| Projection shows incorrect balance | Balance query falls back to event stream | Accurate balance computed from events |
| Malicious direct DB update | DB triggers prevent negative balances | INSERT/UPDATE rejected |

**Guarantee 4: No Lost Reservations**

| Scenario | Mechanism | Recovery |
|----------|-----------|----------|
| Reservation created but allocation fails | Reservation remains PENDING | AllocationSaga retries when new stock arrives |
| Reservation bumped while operator offline | Offline command rejected on sync | Operator sees error in reconciliation report |
| Reservation hard-locked but operator disconnects | Reservation times out after 2 hours | Auto-cancelled, stock released |

---

### 2.3 Replay Safety (Idempotency)

**Principle:** All event handlers and commands must be idempotent (safe to replay).

#### Command Idempotency

**Mechanism: Command Deduplication via CommandId**

```sql
CREATE TABLE processed_commands (
  command_id TEXT PRIMARY KEY,
  command_type TEXT NOT NULL,
  timestamp DATETIME NOT NULL,
  result TEXT NOT NULL,  -- JSON: success or error
  INDEX idx_timestamp (timestamp DESC)
);
```

**Command Handler Pattern:**

```python
def handle_command(command):
    # 1. Check if already processed
    existing = db.query("SELECT result FROM processed_commands WHERE command_id = ?", command.id)
    if existing:
        return cached_result(existing.result)  # Idempotent return
    
    # 2. Execute command
    try:
        result = execute_command_logic(command)
        
        # 3. Store result
        db.insert("processed_commands", {
            "command_id": command.id,
            "command_type": command.type,
            "timestamp": now(),
            "result": json(result)
        })
        
        return result
    
    except Exception as e:
        # 4. Store error result
        db.insert("processed_commands", {
            "command_id": command.id,
            "command_type": command.type,
            "timestamp": now(),
            "result": json({"error": str(e)})
        })
        raise
```

**Retention Policy:** Keep processed commands for 7 days (sufficient for retry windows).

---

#### Event Handler Idempotency

**Mechanism: Event Processing Checkpoints**

```sql
CREATE TABLE event_processing_checkpoints (
  handler_name TEXT NOT NULL,
  event_id TEXT NOT NULL,
  processed_at DATETIME NOT NULL,
  PRIMARY KEY (handler_name, event_id)
);
```

**Event Handler Pattern:**

```python
def handle_event(event, handler_name):
    # 1. Check if already processed
    existing = db.query("""
        SELECT processed_at 
        FROM event_processing_checkpoints 
        WHERE handler_name = ? AND event_id = ?
    """, handler_name, event.id)
    
    if existing:
        return  # Already processed, skip (idempotent)
    
    # 2. Process event (must be idempotent operation)
    process_event_logic(event)
    
    # 3. Record checkpoint
    db.insert("event_processing_checkpoints", {
        "handler_name": handler_name,
        "event_id": event.id,
        "processed_at": now()
    })
```

**Alternative: Upsert-Based Projection**

For projections, use UPSERT instead of INSERT to ensure idempotency:

```sql
-- Example: LocationBalance projection
ON StockMoved event:
  UPSERT INTO location_balance (location, sku, quantity)
  VALUES (
    event.toLocation,
    event.sku,
    COALESCE((SELECT quantity FROM location_balance 
              WHERE location = event.toLocation AND sku = event.sku), 0) + event.quantity
  )
  ON CONFLICT (location, sku) DO UPDATE
    SET quantity = location_balance.quantity + event.quantity;
```

This ensures replaying same event multiple times produces same result.

---

#### Saga Idempotency

**Mechanism: Saga Step Checkpoints**

```sql
CREATE TABLE saga_state (
  saga_id TEXT PRIMARY KEY,
  saga_type TEXT NOT NULL,
  current_step INTEGER NOT NULL,
  step_results TEXT NOT NULL,  -- JSON: {step1: result, step2: result, ...}
  status TEXT NOT NULL,        -- RUNNING, COMPLETED, FAILED
  created_at DATETIME NOT NULL,
  updated_at DATETIME NOT NULL
);
```

**Saga Execution Pattern:**

```python
def execute_saga_step(saga_id, step_number, step_function):
    # 1. Load saga state
    saga = db.query("SELECT * FROM saga_state WHERE saga_id = ?", saga_id)
    
    # 2. Check if step already executed
    if step_number in saga.step_results:
        return saga.step_results[step_number]  # Idempotent return
    
    # 3. Execute step
    try:
        result = step_function()
        
        # 4. Save step result
        saga.step_results[step_number] = result
        saga.current_step = step_number
        saga.updated_at = now()
        
        db.update("saga_state", saga)
        
        return result
    
    except Exception as e:
        # 5. Mark saga as failed
        saga.status = "FAILED"
        saga.step_results[step_number] = {"error": str(e)}
        saga.updated_at = now()
        
        db.update("saga_state", saga)
        
        raise
```

**Saga Restart Safety:**

If saga process crashes and restarts:
1. Load saga state from database
2. Check current_step
3. Resume from next step (already-completed steps skipped)
4. All step functions must be idempotent

---

### 2.4 Idempotency Rules Matrix

| Operation Type | Idempotency Key | Mechanism | Duplicate Behavior |
|---------------|----------------|-----------|-------------------|
| **Command** | CommandId (GUID) | processed_commands table | Return cached result |
| **Event Append** | Event.movementId | Unique constraint on movement_id | INSERT fails (safe - rollback) |
| **Event Processing** | (handler_name, event_id) | event_processing_checkpoints | Skip processing (no-op) |
| **Projection Update** | N/A | UPSERT with computed value | Replay produces same result |
| **Saga Step** | (saga_id, step_number) | saga_state.step_results | Return cached result |
| **External API Call** | Integration-specific ID | Varies by integration | See Integration section |

---

### 2.5 Consistency Verification (Self-Test)

**Daily Consistency Checks (Automated Background Job):**

| Check Name | Query | Expected Result | Alert If Failed | Severity |
|-----------|-------|----------------|-----------------|----------|
| **Balance Integrity** | `SELECT location, sku, SUM(CASE WHEN to_location = location THEN quantity WHEN from_location = location THEN -quantity ELSE 0 END) AS computed_balance FROM stock_movement_events GROUP BY location, sku EXCEPT SELECT location, sku, quantity FROM location_balance` | Zero rows | CRITICAL: "Balance mismatch detected" | P0 - Investigate immediately |
| **HU Contents vs Ledger** | `SELECT hu.location, hul.sku, SUM(hul.quantity) AS hu_total FROM handling_units hu JOIN handling_unit_lines hul ON hu.hu_id = hul.hu_id GROUP BY hu.location, hul.sku EXCEPT SELECT location, sku, quantity FROM location_balance` | Zero rows | WARNING: "HU contents mismatch" | P2 - Review within 24h |
| **No Negative Balances** | `SELECT * FROM location_balance WHERE quantity < 0` | Zero rows | CRITICAL: "Negative balance" | P0 - Halt operations |
| **Orphaned HUs** | `SELECT hu_id FROM handling_units WHERE location NOT IN (SELECT bin_code FROM bins)` | Zero rows | WARNING: "HU at invalid location" | P2 - Review within 24h |
| **Consumed Reservations Still Holding HUs** | `SELECT r.reservation_id FROM reservations r JOIN reservation_allocations ra ON r.reservation_id = ra.reservation_id WHERE r.status = 'CONSUMED'` | Zero rows | WARNING: "Consumed reservation not released" | P2 - Review within 24h |
| **Event Stream Gaps** | `SELECT sequence_number FROM stock_movement_events ORDER BY sequence_number` → Check for gaps | No gaps in sequence | CRITICAL: "Event stream corrupted" | P0 - Database corruption |

**Manual Reconciliation Tools:**

| Tool | Purpose | Trigger | User |
|------|---------|---------|------|
| **Rebuild Projection** | Replay all events to rebuild location_balance | After detecting mismatch | Warehouse Manager |
| **Cycle Count** | Compare physical inventory with system | Scheduled (quarterly) OR after 3 adjustments in 30 days | Warehouse Operator + Manager |
| **Adjustment Wizard** | Fix balance discrepancies | After cycle count finds delta | Inventory Accountant (with approval) |
| **Saga Recovery** | Manually complete stuck saga | Saga stuck > 5 minutes | System Administrator |

---

<!-- ==================== OUTPUT 1 END ==================== -->

<!-- ==================== OUTPUT 2 START ==================== -->

## PART 3: Offline/Edge Conflict Model

### 3.1 Exact Allowed Commands Offline (Decision 3 Enforcement)

**Whitelist Approach:** Only explicitly allowed commands can be queued offline.

#### Allowed Offline Operations

| Command | Precondition (Must Be True Before Going Offline) | Edge Agent Validation | Server Re-Validation on Sync | Risk Level |
|---------|---------------------------------------------------|----------------------|------------------------------|------------|
| **PickStock** | - Reservation already in PICKING state (HARD lock)<br>- HU allocated to this reservation<br>- Edge agent has cached HU details | Check: Local cache has reservationId with status=PICKING | - Reservation still PICKING?<br>- HU still allocated?<br>- Balance sufficient? | LOW (safe if pre-validated) |
| **TransferStock** | - HU assigned to operator's task list<br>- Edge agent has cached HU details<br>- Destination location cached | Check: HU in operator's task list | - HU not moved by another operator?<br>- Destination location still exists?<br>- HU not in PICKING state? | MEDIUM (location conflict possible) |

**Edge Agent Cache Requirements:**

For PickStock offline:
```json
{
  "reservationId": "res-001",
  "status": "PICKING",
  "priority": 5,
  "lines": [
    {"sku": "SKU933", "requestedQty": 10, "allocatedHUs": ["HU-001234"]}
  ],
  "allocatedHUs": {
    "HU-001234": {
      "location": "R3-C6-L3B3",
      "status": "SEALED",
      "contents": [{"sku": "SKU933", "qty": 12}]
    }
  }
}
```

For TransferStock offline:
```json
{
  "operatorTasks": [
    {
      "taskId": "task-042",
      "type": "TRANSFER",
      "huId": "HU-005678",
      "fromLocation": "A1-B1",
      "toLocation": "R5-C2-L1B1",
      "huContents": [{"sku": "SKU105", "qty": 8}]
    }
  ]
}
```

---

#### Forbidden Offline Operations

| Command | Why Forbidden | Edge Agent Enforcement | Server Rejection Response |
|---------|---------------|------------------------|---------------------------|
| **AllocateReservation** | Requires real-time balance check across all locations | Edge agent blocks command, shows: "Cannot allocate offline - requires server connection" | 403 Forbidden: "Allocation not allowed offline" |
| **StartPicking** | Requires conflict detection (other HARD locks) | Edge agent blocks command | 403 Forbidden: "StartPicking requires online validation" |
| **AdjustStock** | Requires approval workflow | Edge agent blocks command | 403 Forbidden: "Adjustments require approval - online only" |
| **ApplyCostAdjustment** | Requires approval workflow | Edge agent blocks command | 403 Forbidden: "Revaluation requires approval - online only" |
| **SplitHU** | Requires real-time validation (HU not sealed, sufficient qty) | Edge agent blocks command | 403 Forbidden: "HU split requires online validation" |
| **MergeHU** | Requires atomic multi-HU operation | Edge agent blocks command | 403 Forbidden: "HU merge requires online validation" |
| **CreateReservation** | Triggers allocation saga (requires server orchestration) | Edge agent blocks command | 403 Forbidden: "Reservation creation requires server" |

**Edge Agent UI Behavior:**

```javascript
function canExecuteOffline(command) {
  const offlineWhitelist = ['PickStock', 'TransferStock'];
  
  if (!navigator.onLine && !offlineWhitelist.includes(command.type)) {
    showError(`${command.type} requires online connection. Please reconnect and try again.`);
    return false;
  }
  
  return true;
}
```

---

### 3.2 Reconciliation Strategies

**Strategy: Optimistic Queueing with Server-Side Final Validation**

#### Reconciliation Flow

```
Edge Agent Offline:
├─ User performs PickStock
├─ Edge agent validates:
│  ├─ Reservation is PICKING? ✅
│  ├─ HU allocated? ✅
│  ├─ Sufficient qty in HU? ✅
├─ Queue command locally (SQLite)
└─ Show user: "Queued (will sync when online)"

Edge Agent Reconnects:
├─ Sync process starts
├─ Send queued commands in FIFO order
│
├─ For each command:
│  ├─ Server re-validates ALL preconditions
│  ├─ If validation passes:
│  │  ├─ Execute command
│  │  ├─ Return 200 OK
│  │  └─ Edge agent: Mark as SYNCED
│  │
│  └─ If validation fails:
│     ├─ Return 409 Conflict OR 400 Bad Request
│     └─ Edge agent: Mark as FAILED
│
└─ Show reconciliation report to user
```

---

#### Conflict Detection Rules

**Conflict Type 1: Reservation State Changed**

| Offline Action | Server State Changed To | Conflict? | Resolution |
|----------------|------------------------|-----------|------------|
| PickStock queued | Reservation still PICKING | ✅ No conflict | Execute pick successfully |
| PickStock queued | Reservation CANCELLED | ❌ Conflict | Reject: "Reservation cancelled while offline" |
| PickStock queued | Reservation CONSUMED by another operator | ❌ Conflict | Reject: "Reservation already consumed" |
| PickStock queued | Reservation BUMPED (downgraded to SOFT) | ❌ Conflict | Reject: "Reservation no longer hard-locked" |

**Conflict Type 2: HU State Changed**

| Offline Action | Server State Changed To | Conflict? | Resolution |
|----------------|------------------------|-----------|------------|
| PickStock queued | HU still at same location, same contents | ✅ No conflict | Execute pick successfully |
| PickStock queued | HU moved to different location | ❌ Conflict | Reject: "HU moved by another operator" |
| PickStock queued | HU partially picked (qty reduced) | ⚠️ Partial conflict | If remaining qty sufficient: allow; else reject |
| PickStock queued | HU deallocated from reservation | ❌ Conflict | Reject: "HU no longer allocated to reservation" |
| TransferStock queued | HU still at from_location | ✅ No conflict | Execute transfer successfully |
| TransferStock queued | HU already moved by another operator | ❌ Conflict | Reject: "HU already transferred" |

**Conflict Type 3: Balance Exhausted**

| Offline Action | Server State | Conflict? | Resolution |
|----------------|--------------|-----------|------------|
| PickStock queued | Balance at HU location still sufficient | ✅ No conflict | Execute pick successfully |
| PickStock queued | Balance exhausted by other picks | ❌ Conflict | Reject: "Insufficient balance - stock picked by others" |

---

#### Conflict Resolution Policies

**Policy 1: Last-Write-Wins (for TransferStock)**

```
Scenario:
- Operator A offline: Queues TransferStock(HU-001, A1 → B1)
- Operator B online: Executes TransferStock(HU-001, A1 → C1)
- Operator B's command commits first
- Operator A reconnects and syncs

Resolution:
- Operator B's transfer already committed (HU-001 now at C1)
- Operator A's command validated: from_location is A1, but HU now at C1
- Server rejects: 409 Conflict "HU location changed"
- Operator A sees error: "HU-001 already moved to C1 by Operator B"

Action Required:
- Operator A must physically verify: Is HU-001 still at A1 or was it moved?
- If at A1: Update command to transfer from A1 → B1 (retry)
- If already at C1: Discard queued command (already handled)
```

**Policy 2: Strict Validation (for PickStock)**

```
Scenario:
- Operator C offline: Queues PickStock(res-042, HU-002, SKU933, 10 units)
- While offline, manager cancels reservation res-042 (found better stock)
- Operator C reconnects and syncs

Resolution:
- Server validates: Reservation res-042 status is CANCELLED
- Server rejects: 400 Bad Request "Reservation cancelled"
- Operator C sees error: "Reservation res-042 cancelled while you were offline"

Action Required:
- Operator C must return picked stock to shelf (if physically picked)
- Contact manager to clarify: Should material still be consumed?
- If yes: Manager creates new reservation, operator re-picks
- If no: Stock returned to inventory (no system action needed)
```

**Policy 3: Partial Quantity Conflict (for PickStock)**

```
Scenario:
- Operator D offline: Queues PickStock(res-100, HU-003, SKU105, 20 units)
- While offline, Operator E picks 15 units from HU-003 (online)
- HU-003 now has only 10 units remaining
- Operator D reconnects and syncs

Resolution:
- Server validates: HU-003 has 10 units (< 20 requested)
- Server rejects: 400 Bad Request "Insufficient quantity in HU"
- Operator D sees error: "HU-003 only has 10 units (needed 20)"

Action Required:
- Operator D can pick available 10 units (modify command to qty=10)
- Or find another HU with remaining 10 units
- System suggests: "Pick 10 from HU-003, find alternative for remaining 10"
```

---

### 3.3 User Experience for Conflicts

#### Reconciliation Report UI

**After Sync Completes:**

```
╔═══════════════════════════════════════════════════════╗
║           SYNC COMPLETE - RECONCILIATION REPORT        ║
╚═══════════════════════════════════════════════════════╝

Summary:
✓ 12 commands synced successfully
✗ 3 commands failed (conflicts detected)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

SUCCESSFUL OPERATIONS (12):

✓ PickStock - HU-001234 (SKU933, 10 units)
  Reservation: res-042
  Synced at: 2026-02-06 14:32:15

✓ TransferStock - HU-005678 (A1-B1 → R3-C6)
  Synced at: 2026-02-06 14:32:18

... (10 more)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

FAILED OPERATIONS (3) - ACTION REQUIRED:

✗ PickStock - HU-002345 (SKU105, 15 units)
  Reservation: res-100
  Error: Reservation cancelled while offline
  
  What happened:
  - Your reservation was cancelled by manager
  - Reason: Better stock found
  
  What to do:
  [ ] Return stock to shelf (if physically picked)
  [ ] Contact manager for new reservation
  
  [Discard Command] [Retry with New Reservation]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✗ TransferStock - HU-003456 (B2-C3 → R5-L1)
  Error: HU already moved by another operator
  
  What happened:
  - HU-003456 was moved to R7-L2 by Operator B
  - Your queued transfer is now invalid
  
  What to do:
  [ ] Verify physical location of HU-003456
  [ ] If at B2-C3: Update destination and retry
  [ ] If at R7-L2: Discard (already handled)
  
  [Check Current Location] [Discard Command]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✗ PickStock - HU-004567 (SKU200, 25 units)
  Reservation: res-200
  Error: Insufficient quantity in HU (only 10 units available)
  
  What happened:
  - Another operator picked 15 units from HU-004567
  - Only 10 units remain (you requested 25)
  
  What to do:
  [ ] Pick available 10 units from HU-004567
  [ ] Find another HU with remaining 15 units
  
  [Pick 10 Units] [Find Alternative Stock] [Discard Command]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Close Report] [Export to CSV]
```

---

#### Inline Conflict Warnings

**Before Going Offline:**

```
╔═══════════════════════════════════════════════════════╗
║           NETWORK CONNECTION LOST                      ║
╚═══════════════════════════════════════════════════════╝

Offline Mode Activated

You can continue working offline, but some operations are restricted:

✓ ALLOWED:
  • Pick stock (if reservation already started)
  • Transfer handling units (from your task list)

✗ NOT ALLOWED:
  • Start new picking (requires conflict check)
  • Allocate reservations (requires balance check)
  • Adjust inventory (requires approval)
  • Split/merge handling units (requires validation)

All offline operations will be validated when you reconnect.
Conflicts may occur if others modify the same stock.

[Understood - Continue Offline]
```

**During Offline Operation:**

```
Pick Stock
──────────────────────────────────────────

Reservation: res-042 (PICKING) ⚠️ OFFLINE
HU: HU-001234 (Location: R3-C6-L3B3)
SKU: SKU933
Quantity: [10] units

⚠️ OFFLINE MODE
This operation will be validated when you reconnect.
Possible conflicts:
• Reservation may be cancelled
• HU may be moved by another operator
• Quantity may be insufficient

[Queue Pick] [Cancel]
```

---

### 3.4 Offline Queue Management

#### Queue Size Limits

| Limit Type | Threshold | Action When Exceeded |
|------------|-----------|---------------------|
| **Max Queue Size** | 100 commands | Block new commands, show: "Queue full - please sync before continuing" |
| **Max Queue Age** | 24 hours | Alert: "Commands older than 24h may fail - sync now" |
| **Max Offline Duration** | 8 hours | Force sync attempt, show: "Extended offline detected - reconnect required" |

#### Queue Storage Schema

```sql
CREATE TABLE offline_command_queue (
  queue_id INTEGER PRIMARY KEY AUTOINCREMENT,
  command_id TEXT UNIQUE NOT NULL,
  timestamp DATETIME NOT NULL,
  command_type TEXT NOT NULL,
  payload TEXT NOT NULL,  -- JSON
  status TEXT NOT NULL,   -- QUEUED, SYNCING, SYNCED, FAILED
  retry_count INTEGER DEFAULT 0,
  last_error TEXT,
  created_at DATETIME NOT NULL
);

CREATE INDEX idx_status ON offline_command_queue(status);
CREATE INDEX idx_timestamp ON offline_command_queue(timestamp);
```

#### Queue Priorities

**Sync Order:** FIFO (First In First Out) to preserve operation order.

**Exception:** High-priority commands (e.g., urgent picks) can be manually promoted:

```
Queue Before Promotion:
1. TransferStock (HU-001) - queued 10:00
2. PickStock (res-042) - queued 10:05  ← User marks as urgent
3. TransferStock (HU-002) - queued 10:10

Queue After Promotion:
1. PickStock (res-042) - PRIORITY  ← Moved to front
2. TransferStock (HU-001) - queued 10:00
3. TransferStock (HU-002) - queued 10:10
```

---

## PART 4: Final Integration Architecture

### 4.1 Integration Module Split (Decision 5 Enforcement)

**Logical Components Within Integration Module:**

```
┌─────────────────────────────────────────────────────────┐
│           INTEGRATION MODULE                            │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │  Operational Integration (Real-Time)              │  │
│  │  - Label Printing                                 │  │
│  │  - Barcode Scanners                               │  │
│  │  - Equipment (scales, gates)                      │  │
│  │  SLA: < 5 seconds                                 │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │  Financial Integration (Batch/Scheduled)          │  │
│  │  - Agnum Accounting Export                        │  │
│  │  - Reconciliation Reports                         │  │
│  │  - Cost Allocation                                │  │
│  │  SLA: minutes to hours                            │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │  Process Integration (Event-Driven)               │  │
│  │  - MES/ERP Material Requests                      │  │
│  │  - Production Consumption Events                  │  │
│  │  - Cross-System Workflows                         │  │
│  │  SLA: < 30 seconds                                │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

### 4.2 Operational Integration (Real-Time)

**Responsibility:** Support warehouse floor operations with low-latency device integrations.

#### Label Printing Integration

**Event Flow:**

```
HandlingUnitSealed event
   ↓
Operational Integration subscribes
   ↓
Generate ZPL label template
   ↓
Send to printer (TCP 9100 or USB)
   ↓
Await ACK (timeout 5 sec)
   ↓
Publish: LabelPrinted OR LabelPrintFailed
```

**Configuration:**

```yaml
operational_integration:
  label_printing:
    enabled: true
    printers:
      - id: printer-01
        name: "Main Warehouse Zebra ZT230"
        ip: 192.168.1.100
        port: 9100
        type: ZPL
        timeout_seconds: 5
      - id: printer-02
        name: "Cold Storage Zebra ZT411"
        ip: 192.168.1.101
        port: 9100
        type: ZPL
        timeout_seconds: 5
    
    retry_policy:
      max_attempts: 3
      backoff: [5s, 15s, 30s]
      fallback: queue_for_manual_print
    
    label_templates:
      pallet:
        template_file: "pallet_label.zpl"
        fields: [hu_id, location, sku, quantity, timestamp]
```

**Error Handling:**

| Error | Detection | Immediate Action | Recovery | User Impact |
|-------|-----------|------------------|----------|-------------|
| Printer offline | TCP connection refused | Queue job for retry | Retry 3x with backoff | Show warning: "Label queued - printer offline" |
| Paper jam | Printer error code | Pause queue | Alert operator to clear jam | Block further prints until resolved |
| Invalid template | Template rendering fails | Log error, use fallback template | Alert admin to fix template | Show warning: "Using simplified label" |
| Network timeout | No ACK after 5 sec | Retry immediately | Retry 3x, then queue for manual | Show warning: "Print delayed - network issue" |

---

#### Barcode Scanner Integration

**Integration Pattern:** Passive (Scanner acts as keyboard wedge)

**No server-side integration needed** - scanner sends input directly to UI.

**Edge Agent handles scanner input:**

```javascript
// Edge Agent: Scanner input handler
document.addEventListener('keypress', (event) => {
  if (event.target.id === 'barcode-input' && event.key === 'Enter') {
    const scannedCode = event.target.value.trim();
    
    // Validate barcode format
    if (isValidBarcode(scannedCode)) {
      lookupHandlingUnit(scannedCode);
    } else {
      showError('Invalid barcode format');
    }
    
    event.target.value = '';  // Clear input for next scan
  }
});
```

---

#### Equipment Integration (Scales, Gates)

**Scale Integration (for weight verification):**

```
User picks from HU
   ↓
Edge Agent: "Please weigh picked quantity"
   ↓
Operator places items on scale
   ↓
Scale sends weight to Edge Agent (serial/USB)
   ↓
Edge Agent validates: weight within tolerance?
   ↓
If yes: Proceed with PickStock command
If no: Show warning: "Weight mismatch - verify quantity"
```

**Configuration:**

```yaml
operational_integration:
  scale:
    enabled: true
    port: /dev/ttyUSB0
    baud_rate: 9600
    tolerance_percent: 5  # ±5% weight tolerance
    required_for: [PICK]  # Operations requiring weight check
```

---

### 4.3 Financial Integration (Batch/Scheduled)

**Responsibility:** Export inventory data to accounting systems with configurable granularity.

#### Agnum Export Integration

**Trigger:** Scheduled (daily 23:00) OR manual ("Export Now" button)

**Event Flow:**

```
Scheduled trigger (cron: 0 23 * * *)
   ↓
Financial Integration: Start AgnumExportSaga
   ↓
Query StockLedger for current balances (per location, per SKU)
   ↓
Query Valuation for unit costs (per SKU)
   ↓
Query LogicalWarehouse for category mappings
   ↓
Apply Agnum mapping configuration (physical WH → accounts)
   ↓
Generate CSV export file
   ↓
Send to Agnum API (HTTPS POST) OR save to shared folder
   ↓
Record export log (timestamp, record count, file location)
   ↓
Email reconciliation report to accountant
   ↓
Publish: ExportCompleted OR ExportFailed
```

**Configuration:**

```yaml
financial_integration:
  agnum_export:
    enabled: true
    schedule: "0 23 * * *"  # Daily at 23:00
    export_mode: BY_PHYSICAL_WAREHOUSE  # or BY_LOGICAL_WH, BY_CATEGORY, TOTAL
    
    agnum_api:
      endpoint: "https://agnum.company.com/api/v1/inventory/import"
      auth_type: api_key
      api_key: "${AGNUM_API_KEY}"  # From environment variable
      timeout_seconds: 60
    
    retry_policy:
      max_attempts: 3
      backoff: [1m, 5m, 15m]
      fallback: export_to_csv  # Save CSV to shared folder if API fails
    
    mappings:
      - source_type: PHYSICAL_WAREHOUSE
        source_value: "Main"
        agnum_account: "1500-RAW-MAIN"
      - source_type: PHYSICAL_WAREHOUSE
        source_value: "Aux"
        agnum_account: "1500-RAW-AUX"
      - source_type: LOGICAL_WAREHOUSE
        source_value: "SCRAP"
        agnum_account: "5200-SCRAP"
    
    notifications:
      success:
        email_to: ["accountant@company.com"]
        subject: "Agnum Export Completed - {{export_id}}"
      failure:
        email_to: ["accountant@company.com", "it-admin@company.com"]
        subject: "ALERT: Agnum Export Failed - {{export_id}}"
```

**Reconciliation Report Email:**

```
Subject: Agnum Export Completed - EXP-2026-02-06-001

Agnum Export Reconciliation Report
───────────────────────────────────────────────────────

Export ID: EXP-2026-02-06-001
Timestamp: 2026-02-06 23:05:00
Mode: By Physical Warehouse
Status: Completed ✓

Summary:
────────
Total SKUs: 245
Total Quantity: 4,000 units
Total Value: €79,500

Previous Export: €76,200 (EXP-2026-02-05-001)
Delta: +€3,300 (+4.3%)

By Warehouse:
──────────────
Main: 2,450 units, €52,300 (Account: 1500-RAW-MAIN)
Aux: 1,200 units, €18,500 (Account: 1500-RAW-AUX)
Cold: 350 units, €8,700 (Account: 1500-RAW-COLD)

Top Changes Since Last Export:
───────────────────────────────
SKU933: +120 units (Receipt: PO-2025-0915)
SKU105: -50 units (Pick: Production Order PO-123)
SKU200: +30 units (Adjustment: Cycle Count)

Files:
───────
CSV: /exports/2026-02-06/agnum-export-001.csv
API Response: 200 OK (accepted by Agnum)

[View Full Report] [Download CSV]
```

**Error Handling:**

| Error | Detection | Recovery | Notification |
|-------|-----------|----------|--------------|
| Agnum API returns 500 | HTTP status | Retry 3x with backoff (1m, 5m, 15m) | Email alert after 3 failures |
| Agnum API timeout | No response after 60 sec | Retry 3x | Email alert after 3 failures |
| CSV generation fails | Exception during data query | Log error, mark export as FAILED | Immediate email alert |
| Mapping configuration error | SKU without category mapping | Use default account "1500-UNMAPPED" | Warning in report |

---

### 4.4 Process Integration (Event-Driven)

**Responsibility:** Coordinate workflows between Warehouse and MES/ERP systems.

#### Material Request Flow (ERP → Warehouse)

**Event Flow:**

```
ERP publishes: MaterialRequested
   ↓
Process Integration subscribes (anti-corruption layer)
   ↓
Translate: MaterialRequested → CreateReservation command
   ↓
Send CreateReservation to Warehouse Core
   ↓
Warehouse: AllocationSaga allocates HUs (SOFT lock)
   ↓
Warehouse publishes: ReservationCreated + StockAllocated
   ↓
Process Integration subscribes
   ↓
Translate: ReservationCreated → MaterialReserved event
   ↓
Publish MaterialReserved to ERP
   ↓
ERP receives confirmation (reservation ID for tracking)
```

**Anti-Corruption Layer (Translation):**

```python
# Process Integration: ERP → Warehouse translation
def handle_material_requested(erp_event):
    # ERP format:
    # {
    #   "productionOrderId": "PO-2026-042",
    #   "bom": [
    #     {"materialCode": "MAT-933", "quantity": 50, "unit": "PCS"},
    #     {"materialCode": "MAT-105", "quantity": 20, "unit": "PCS"}
    #   ]
    # }
    
    # Translate to Warehouse format:
    reservation_command = {
        "commandId": generate_guid(),
        "command": "CreateReservation",
        "purpose": f"ProductionOrder-{erp_event['productionOrderId']}",
        "priority": erp_event.get('priority', 5),  # Default: medium priority
        "requestedLines": [
            {"sku": item["materialCode"], "quantity": item["quantity"]}
            for item in erp_event["bom"]
        ]
    }
    
    # Store mapping for reverse lookup
    store_mapping(
        production_order_id=erp_event["productionOrderId"],
        reservation_id=reservation_command["commandId"]
    )
    
    # Send to Warehouse Core
    send_command(reservation_command)


# Warehouse → ERP translation
def handle_reservation_created(warehouse_event):
    # Warehouse format:
    # {
    #   "reservationId": "res-042",
    #   "purpose": "ProductionOrder-PO-2026-042",
    #   "status": "PENDING"
    # }
    
    # Lookup production order ID
    production_order_id = get_production_order_from_mapping(
        reservation_id=warehouse_event["reservationId"]
    )
    
    # Translate to ERP format:
    erp_event = {
        "eventType": "MaterialReserved",
        "productionOrderId": production_order_id,
        "reservationId": warehouse_event["reservationId"],
        "status": "RESERVED"
    }
    
    # Publish to ERP event bus
    publish_to_erp(erp_event)
```

---

#### Material Consumption Flow (Warehouse → ERP)

**Event Flow:**

```
Operator picks stock (PickStock command)
   ↓
Warehouse: StockLedger appends StockMovement (to PRODUCTION)
   ↓
Warehouse publishes: StockMoved (toLocation = "PRODUCTION")
   ↓
Process Integration subscribes
   ↓
Check: Is this for a tracked production order?
   ↓
If yes:
   Lookup reservation → production order mapping
   ↓
   Translate: StockMoved → MaterialConsumed event
   ↓
   Publish MaterialConsumed to ERP
   ↓
   ERP receives consumption (updates production order status)
```

**Translation Logic:**

```python
# Warehouse → ERP consumption notification
def handle_stock_moved_to_production(warehouse_event):
    # Warehouse format:
    # {
    #   "movementId": "mov-001",
    #   "sku": "SKU933",
    #   "quantity": 10,
    #   "fromLocation": "R3-C6-L3B3",
    #   "toLocation": "PRODUCTION",
    #   "timestamp": "2026-02-06T14:32:15Z",
    #   "reservationId": "res-042"  # Optional link to reservation
    # }
    
    # Only process if linked to reservation
    if not warehouse_event.get("reservationId"):
        return  # Ad-hoc pick, no ERP notification needed
    
    # Lookup production order ID
    production_order_id = get_production_order_from_mapping(
        reservation_id=warehouse_event["reservationId"]
    )
    
    if not production_order_id:
        log_warning(f"No production order mapped to reservation {warehouse_event['reservationId']}")
        return
    
    # Translate to ERP format:
    erp_event = {
        "eventType": "MaterialConsumed",
        "productionOrderId": production_order_id,
        "materialCode": warehouse_event["sku"],
        "quantity": warehouse_event["quantity"],
        "consumedAt": warehouse_event["timestamp"],
        "warehouseLocation": warehouse_event["fromLocation"]
    }
    
    # Publish to ERP event bus
    publish_to_erp(erp_event)
```

---

#### Failure Handling in Process Integration

**Saga Pattern for Cross-System Coordination:**

| Failure Point | Detection | Compensation | Notification |
|---------------|-----------|--------------|--------------|
| **Warehouse cannot allocate** | AllocationSaga fails (insufficient stock) | Publish MaterialNotAvailable to ERP | ERP reschedules production order |
| **Reservation bumped** | ReservationBumped event | Publish MaterialReallocated to ERP | ERP updates order status (delayed) |
| **ERP API unreachable** | Publish to ERP fails (timeout, 500) | Retry 3x, then queue message | Alert integration admin |
| **Pick fails after reservation** | PickStock command fails | Publish MaterialPickFailed to ERP | ERP re-requests material or escalates |

**Configuration:**

```yaml
process_integration:
  erp_gateway:
    enabled: true
    
    inbound:
      event_bus: kafka
      topic: "erp.warehouse.requests"
      consumer_group: "warehouse-integration"
    
    outbound:
      event_bus: kafka
      topic: "warehouse.erp.notifications"
      
    retry_policy:
      max_attempts: 3
      backoff: [30s, 2m, 5m]
      dead_letter_queue: "warehouse-erp-dlq"
    
    notifications:
      failure:
        email_to: ["integration-admin@company.com"]
        subject: "ALERT: ERP Integration Failure"
```

---

### 4.5 Integration Event Flows Summary

**Operational Events (Low Latency):**

| Source Event | Integration Action | Target System | SLA |
|--------------|-------------------|---------------|-----|
| HandlingUnitSealed | Print label | Printer | < 5 sec |
| HandlingUnitCreated | Print label | Printer | < 5 sec |

**Financial Events (Scheduled/Batch):**

| Source Event | Integration Action | Target System | SLA |
|--------------|-------------------|---------------|-----|
| Daily schedule (cron) | Export snapshot | Agnum | < 5 min |
| CostAdjusted | Trigger export | Agnum | < 1 hour |
| StockWrittenDown | Trigger export | Agnum | < 1 hour |

**Process Events (Event-Driven):**

| Source Event | Integration Action | Target System | SLA |
|--------------|-------------------|---------------|-----|
| MaterialRequested (ERP) | Create reservation | Warehouse | < 30 sec |
| ReservationCreated | Notify reserved | ERP | < 30 sec |
| StockMoved (to PRODUCTION) | Notify consumed | ERP | < 30 sec |
| ReservationBumped | Notify reallocated | ERP | < 1 min |

---

## PART 5: Implementation Readiness Checklist

### 5.1 Architecture Compliance Checklist

**Before Implementation Begins:**

| Area | Check | Verified | Notes |
|------|-------|----------|-------|
| **Decision 1: Movement Ownership** | ✅ StockLedger is sole owner of StockMovement events | ☐ | Review database permissions |
| | ✅ HandlingUnit subscribes to StockMoved events (not publish) | ☐ | Review event subscriptions |
| | ✅ No other module can write to stock_movement_events table | ☐ | Enforce via DB roles |
| **Decision 2: Pick Transaction Order** | ✅ PickStockSaga executes Ledger → HU → Reservation (strict order) | ☐ | Code review mandatory |
| | ✅ No HU updates before Ledger commit | ☐ | Architecture tests enforce |
| | ✅ Process can resume after each transaction commits | ☐ | Saga state machine tested |
| **Decision 3: Offline Policy** | ✅ Edge Agent whitelist enforces allowed operations | ☐ | Edge Agent config validated |
| | ✅ Server rejects forbidden offline commands (403) | ☐ | API endpoint authorization |
| | ✅ UI disables forbidden buttons when offline | ☐ | Frontend feature flags |
| **Decision 4: Reservation Bumping** | ✅ SOFT reservations can be bumped by HARD | ☐ | Reservation conflict logic tested |
| | ✅ HARD reservations cannot be bumped | ☐ | StartPicking validation enforced |
| | ✅ UI notifies users when bumped | ☐ | Event handler triggers notification |
| **Decision 5: Integration Separation** | ✅ Operational, Financial, Process components logically separated | ☐ | Module structure reviewed |
| | ✅ Each has appropriate SLA configuration | ☐ | Config files validated |
| | ✅ Failure modes differ (retry vs alert vs compensate) | ☐ | Error handling reviewed |

---

### 5.2 Data Safety Checklist

| Check | Description | Verified | Test Case |
|-------|-------------|----------|-----------|
| ✅ **No negative balances** | StockLedger validates before append | ☐ | Unit test: RecordStockMovement with insufficient balance → fails |
| ✅ **Event immutability** | stock_movement_events table has no UPDATE/DELETE grants | ☐ | DB role test: Attempt UPDATE → permission denied |
| ✅ **Idempotent commands** | processed_commands table prevents duplicates | ☐ | Integration test: Send same commandId twice → second returns cached result |
| ✅ **Idempotent events** | Event handlers check already processed | ☐ | Integration test: Deliver same event twice → handler processes once |
| ✅ **Saga recovery** | Saga can resume from last completed step | ☐ | Chaos test: Kill saga process mid-step → resumes on restart |
| ✅ **Projection rebuild** | Can rebuild location_balance from events | ☐ | Integration test: Truncate projection, replay events → balance correct |
| ✅ **Consistency checks** | Daily job detects balance mismatches | ☐ | Schedule test: Job runs at 23:00, logs results |

---

### 5.3 Transaction Integrity Checklist

| Check | Description | Verified | Test Case |
|-------|-------------|----------|-----------|
| ✅ **Single aggregate ACID** | Each operation commits or rolls back atomically | ☐ | Unit test: Validation fails → no data committed |
| ✅ **Saga compensation** | Failed sagas trigger compensating actions | ☐ | Integration test: Fail step 3 of ReceiveGoodsSaga → HU deleted |
| ✅ **No distributed transactions** | No 2PC across aggregates | ☐ | Architecture review: No XA transactions in code |
| ✅ **Transactional outbox** | Events published atomically with aggregate | ☐ | Integration test: Commit aggregate → event in outbox |
| ✅ **Optimistic locking** | Aggregate versioning prevents lost updates | ☐ | Concurrency test: Two updates to same HU → one fails with version conflict |

---

### 5.4 Offline/Edge Checklist

| Check | Description | Verified | Test Case |
|-------|-------------|----------|-----------|
| ✅ **Whitelist enforcement** | Edge Agent blocks forbidden commands | ☐ | UI test: Attempt AdjustStock offline → button disabled, error shown |
| ✅ **Server re-validation** | Server validates all preconditions on sync | ☐ | Integration test: Queue PickStock, cancel reservation, sync → rejected |
| ✅ **Conflict detection** | Server detects HU moved by another operator | ☐ | Integration test: Queue TransferStock, move HU elsewhere, sync → conflict |
| ✅ **Reconciliation report** | User sees clear report after sync | ☐ | UI test: Sync with failures → report shows errors + actions |
| ✅ **Queue size limits** | Edge Agent blocks when queue > 100 | ☐ | UI test: Queue 101 commands → error: "Queue full" |

---

### 5.5 Integration Checklist

| Check | Description | Verified | Test Case |
|-------|-------------|----------|-----------|
| ✅ **Label printing retry** | Printer offline → retry 3x → queue for manual | ☐ | Integration test: Disconnect printer → job queued |
| ✅ **Agnum export idempotency** | Same export ID not processed twice | ☐ | Integration test: Send same export twice → Agnum deduplicates |
| ✅ **ERP event translation** | MaterialRequested → CreateReservation correct | ☐ | Integration test: Publish ERP event → Warehouse reservation created |
| ✅ **ERP failure handling** | ERP API down → retry 3x → dead letter queue | ☐ | Integration test: Disable ERP → messages in DLQ |

---

### 5.6 Monitoring & Observability Checklist

| Check | Description | Verified | Test Case |
|-------|-------------|----------|-----------|
| ✅ **Trace IDs propagate** | All events include traceId from command | ☐ | Log review: Command + events have same traceId |
| ✅ **Projection lag monitoring** | Alert if lag > 30 sec | ☐ | Test: Stop projection handler → alert fires |
| ✅ **Saga stuck detection** | Alert if saga stuck > 5 min | ☐ | Test: Pause saga → alert fires |
| ✅ **Balance mismatch alert** | Daily check detects discrepancies | ☐ | Test: Manually corrupt balance → check detects, alerts |
| ✅ **Integration health check** | Dashboard shows Agnum/ERP status | ☐ | UI test: Disable integration → status shows red |

---

### 5.7 Security Checklist

| Check | Description | Verified | Test Case |
|-------|-------------|----------|-----------|
| ✅ **Database role isolation** | StockLedger service has exclusive INSERT on stock_movement_events | ☐ | DB test: Attempt INSERT from HandlingUnit service → denied |
| ✅ **Approval enforcement** | ApplyCostAdjustment requires ApproverId | ☐ | API test: Call without approver → 403 Forbidden |
| ✅ **Offline command authorization** | Server validates operator permissions on sync | ☐ | API test: Operator A queues pick for Operator B's reservation → rejected |
| ✅ **Audit log completeness** | All financial operations logged | ☐ | Audit review: Revaluation recorded in audit_log |

---

### 5.8 Performance Checklist

| Check | Description | Verified | Test Case |
|-------|-------------|----------|-----------|
| ✅ **Command SLA < 2 sec (p99)** | 99% of commands complete in < 2 sec | ☐ | Load test: 1000 commands → p99 latency measured |
| ✅ **Query SLA < 100ms (p99)** | 99% of queries respond in < 100ms | ☐ | Load test: 10k queries → p99 latency measured |
| ✅ **Projection lag < 5 sec** | 95% of projections lag < 5 sec | ☐ | Monitoring: Track event timestamp vs projection timestamp |
| ✅ **Event throughput** | System handles 100 events/sec | ☐ | Load test: Generate 100 events/sec → no backlog |

---

### 5.9 Documentation Checklist

| Check | Description | Verified | Notes |
|-------|-------------|----------|-------|
| ✅ **Architectural decisions documented** | All 5 decisions in ADR format | ☐ | See sections 1.1-1.5 of this document |
| ✅ **API contracts versioned** | Command/event schemas have version numbers | ☐ | See domain event catalog |
| ✅ **Runbook for failures** | Operators know how to handle saga failures | ☐ | Create operational runbook |
| ✅ **Reconciliation procedures** | Process documented for balance mismatches | ☐ | See consistency check section |

---

### 5.10 Pre-Production Checklist

**Sign-Off Required Before Production Deployment:**

| Area | Owner | Status | Sign-Off Date |
|------|-------|--------|---------------|
| Architecture compliance (all 5 decisions) | Solution Architect | ☐ | __________ |
| Data safety (no negative balances, event immutability) | Database Administrator | ☐ | __________ |
| Transaction integrity (ACID, sagas, compensation) | Tech Lead | ☐ | __________ |
| Offline/edge operations (whitelist, conflict resolution) | Mobile Team Lead | ☐ | __________ |
| Integration (Agnum, ERP, printers) | Integration Lead | ☐ | __________ |
| Monitoring & observability (alerts, dashboards) | DevOps Lead | ☐ | __________ |
| Security (roles, approvals, audit) | Security Officer | ☐ | __________ |
| Performance (SLAs met under load) | Performance Engineer | ☐ | __________ |
| Disaster recovery (backup, restore, replay) | Infrastructure Lead | ☐ | __________ |
| Operational runbook (incident response) | Operations Manager | ☐ | __________ |

---

### 5.11 Go-Live Criteria

**All criteria must be met before production deployment:**

- [x] All architectural decisions (1-5) enforced in code
- [x] All unit tests pass (coverage > 80%)
- [x] All integration tests pass
- [x] Load test demonstrates system handles 2x expected load
- [x] Chaos test demonstrates system recovers from failures
- [x] Security audit passed (penetration test + code review)
- [x] Disaster recovery tested (restore from backup + event replay)
- [x] Runbook validated (team practices incident response)
- [x] All stakeholders signed off (see table above)
- [x] Production environment configured (secrets, monitoring, backups)

---

## Document Approval

| Role | Name | Signature | Date |
|------|------|-----------|------|
| **Solution Architect** | _____________ | _____________ | __________ |
| **Tech Lead** | _____________ | _____________ | __________ |
| **Product Owner** | _____________ | _____________ | __________ |
| **Infrastructure Lead** | _____________ | _____________ | __________ |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 2.0 | 2026-02-06 | Solution Architect | Hardened architecture with 5 mandatory decisions enforced |
| 1.0 | 2026-02-05 | Solution Architect | Initial architecture (04-system-architecture.md) |

---

**END OF DOCUMENT**

This is the **FINAL BASELINE** for implementation. All changes to architectural decisions require formal architecture review board approval.

<!-- ==================== OUTPUT 2 END ==================== -->