# Design Document: Warehouse Core Phase 1

## Overview

This document provides the technical design for Phase 1 of the LKvitai.MES Warehouse Management System. The system implements a modular monolith architecture with event sourcing for critical aggregates, hybrid reservation locking, offline edge operations, and multi-layered integration.

**Core Architecture Principles:**
- StockLedger is the single source of truth for all stock quantity changes
- HandlingUnit is projection-driven state (subscribes to StockMoved events)
- Reservation manages intent and locking only (SOFT → HARD transitions)
- Pick operations follow strict transaction ordering: StockLedger → HU Projection → Reservation
- Offline operations restricted to safe whitelist (PickStock with HARD lock, TransferStock with assigned HUs)
- Integration separated into Operational (<5s), Financial (minutes), and Process (<30s) layers

**Technology Stack:**
- .NET 8+ backend (modular monolith)
- PostgreSQL or SQL Server for relational storage
- Event sourcing for StockLedger, Reservation, and Valuation aggregates
- Transactional outbox pattern for event publishing
- MassTransit or NServiceBus for event bus
- Marten or EventStoreDB for event store

**Phase 1 Scope:**
- StockMovement ledger (event sourced, append-only)
- HandlingUnit lifecycle (create, seal, move, split, merge)
- Reservation system with hybrid locking
- Core read models (LocationBalance, AvailableStock, OnHandValue)
- Basic operator UI flows (receive goods, transfer, pick)
- Label printing integration
- Agnum export baseline

## Architecture

### System Context

```
┌─────────────────────────────────────────────────────────────┐
│                    WAREHOUSE SYSTEM                          │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │ StockLedger  │  │ HandlingUnit │  │ Reservation  │     │
│  │ (Event       │  │ (Projection) │  │ (Event       │     │
│  │  Sourced)    │  │              │  │  Sourced)    │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Valuation   │  │   Layout     │  │   Logical    │     │
│  │ (Event       │  │ (State-Based)│  │  Warehouse   │     │
│  │  Sourced)    │  │              │  │ (State-Based)│     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│                                                              │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
         ↓                    ↓                    ↓
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Operational  │    │  Financial   │    │   Process    │
│ Integration  │    │ Integration  │    │ Integration  │
│ (<5s SLA)    │    │ (minutes)    │    │ (<30s SLA)   │
└──────────────┘    └──────────────┘    └──────────────┘
      │                    │                    │
      ↓                    ↓                    ↓
┌──────────┐        ┌──────────┐        ┌──────────┐
│ Printers │        │  Agnum   │        │ ERP/MES  │
│ Scanners │        │          │        │   Core   │
└──────────┘        └──────────┘        └──────────┘
```

### Dependency Hierarchy

```
Level 1 (Foundation):
  StockLedger (no dependencies)

Level 2 (State):
  HandlingUnit (reads StockLedger events)
  WarehouseLayout (independent)
  LogicalWarehouse (independent)

Level 3 (Business Logic):
  Reservation (reads StockLedger, reads HandlingUnit)
  Valuation (reads StockLedger)

Level 4 (Orchestration):
  Process Managers / Sagas (coordinate all levels)
```


## Components and Interfaces

### Aggregate 1: StockLedger (Event Sourced)

**Purpose:** Single source of truth for all stock quantity changes. Owns the immutable append-only ledger of stock movements.

**Aggregate Root:** `StockLedger`

**Identity:** Singleton per warehouse system (or partitioned by warehouse if multi-tenant)

**Value Objects:**
- `MovementId` (GUID)
- `SKU` (string)
- `Quantity` (decimal, must be > 0)
- `Location` (string - physical or virtual)
- `MovementType` (enum: RECEIPT, TRANSFER, PICK, SCRAP, ADJUSTMENT, RETURN)
- `Timestamp` (DateTime)
- `OperatorId` (GUID)
- `HandlingUnitId` (GUID, optional)
- `Reason` (string, optional)

**Commands:**
- `RecordStockMovement(sku, quantity, fromLocation, toLocation, type, operatorId, huId?, reason?)`

**Events:**
- `StockMoved(movementId, sku, quantity, fromLocation, toLocation, type, timestamp, operatorId, huId, reason)`

**Invariants:**
1. Movements are immutable (append-only)
2. From location ≠ to location
3. Quantity > 0
4. Physical from locations must have sufficient balance
5. Virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM) skip balance validation
6. **[MITIGATION V-2]** Balance validation and movement append must be atomic within StockLedger concurrency boundary using optimistic concurrency control

**Business Rules:**
- Balance validation: `SUM(movements TO location) - SUM(movements FROM location) >= quantity`
- Virtual locations are always valid sources (infinite supply)
- Balance computed from event stream for any point in time

**Concurrency Control (MITIGATION V-2):**
- Uses Marten's expected-version append for atomic balance validation and event append
- Stream version tracked per warehouse
- Retry policy: Maximum 3 retries with exponential backoff (100ms, 200ms, 400ms)
- Concurrency conflicts return error to caller after retries exhausted
- Serialization boundary: Per-warehouse stream (all movements for a warehouse serialize through single stream)

**Storage:**
- Event stream: `stock_movement_events` table
- Columns: movement_id, sku, quantity, from_location, to_location, movement_type, timestamp, operator_id, handling_unit_id, reason, sequence_number

---

### Aggregate 2: HandlingUnit (State-Based with Event Projection)

**Purpose:** Represents physical containers (pallets, boxes, bags, units) with barcode tracking. State is derived from StockMoved events.

**Aggregate Root:** `HandlingUnit`

**Identity:** `HandlingUnitId` (GUID / LPN)

**Entities:**
- `HandlingUnitLine` (SKU, Quantity)

**Value Objects:**
- `HandlingUnitType` (enum: PALLET, BOX, BAG, UNIT)
- `HandlingUnitStatus` (enum: OPEN, SEALED, PICKED, EMPTY)
- `Location` (string)
- `Timestamp` (created, sealed, lastMoved)

**Commands:**
- `CreateHandlingUnit(type, location, operatorId)`
- `AddLine(huId, sku, quantity)`
- `RemoveLine(huId, sku, quantity)`
- `SealHandlingUnit(huId)`
- `SplitHandlingUnit(sourceHuId, sku, quantity)`
- `MergeHandlingUnits(sourceHuIds[], targetHuId)`

**Events:**
- `HandlingUnitCreated(huId, type, location, timestamp)`
- `LineAddedToHandlingUnit(huId, sku, quantity)`
- `LineRemovedFromHandlingUnit(huId, sku, quantity)`
- `HandlingUnitSealed(huId, timestamp)`
- `HandlingUnitMoved(huId, fromLocation, toLocation, timestamp)`
- `HandlingUnitSplit(sourceHuId, newHuId, sku, quantity)`
- `HandlingUnitMerged(sourceHuIds[], targetHuId)`
- `HandlingUnitEmptied(huId)`

**Invariants:**
1. Cannot modify SEALED HU
2. Cannot seal empty HU
3. Line quantity cannot go negative
4. HU has exactly one location at any time
5. Moving HU generates StockMovement for each line (via StockLedger)

**Projection Logic:**
- Subscribes to `StockMoved` events
- When `StockMoved` with `handlingUnitId`:
  - If `fromLocation == HU.location`: RemoveLine(sku, qty)
  - If `toLocation == HU.location`: AddLine(sku, qty)
  - If locations differ: Update HU.location

**Storage:**
- State tables: `handling_units`, `handling_unit_lines`
- Event subscription checkpoint: `hu_projection_checkpoint`

---

### Aggregate 3: Reservation (Event Sourced)

**Purpose:** Manages claims on future stock consumption with hybrid locking (SOFT → HARD transitions).

**Aggregate Root:** `Reservation`

**Identity:** `ReservationId` (GUID)

**Entities:**
- `ReservationLine` (SKU, requestedQty, allocatedHUs[])

**Value Objects:**
- `ReservationPurpose` (ProductionOrder, SalesOrder, etc.)
- `ReservationStatus` (enum: PENDING, ALLOCATED, PICKING, CONSUMED, CANCELLED, BUMPED)
- `ReservationLockType` (enum: SOFT, HARD)
- `Priority` (int, 1-10 where 10 = highest)
- `ExpirationDate` (DateTime, optional)

**Commands:**
- `CreateReservation(purpose, priority, requestedLines[])`
- `AllocateReservation(reservationId, huIds[])`
- `StartPicking(reservationId)` **[MITIGATION R-3]**
- `ConsumeReservation(reservationId, actualQuantity)`
- `CancelReservation(reservationId, reason)`
- `BumpReservation(reservationIdToBump, bumpingReservationId)`

**Events:**
- `ReservationCreated(reservationId, purpose, priority, requestedLines[])`
- `StockAllocated(reservationId, huIds[], lockType)`
- `PickingStarted(reservationId, lockType=HARD)` **[MITIGATION R-3]**
- `ReservationConsumed(reservationId, actualQuantity)`
- `ReservationCancelled(reservationId, reason)`
- `ReservationBumped(bumpedReservationId, bumpingReservationId, huIds[])`

**Invariants:**
1. Allocated quantity ≤ requested quantity
2. SOFT lock can be bumped by higher priority or HARD lock
3. HARD lock is exclusive (cannot be bumped)
4. Consumed reservation is immutable
5. StartPicking() re-validates balance
6. HARD reservations do NOT auto-expire
7. **[MITIGATION R-3]** StartPicking must re-validate balance from event stream and acquire HARD lock atomically using optimistic concurrency control

**State Machine:**
```
PENDING → ALLOCATED (SOFT) → PICKING (HARD) → CONSUMED
   ↓           ↓
CANCELLED   BUMPED
```

**Concurrency Control (MITIGATION R-3):**
- Uses Marten's expected-version append for atomic HARD lock acquisition
- Stream version tracked per reservation
- Retry policy: Maximum 3 retries with exponential backoff (100ms, 200ms, 400ms)
- Concurrency conflicts return error to caller after retries exhausted
- Serialization boundary: Per-reservation stream

**Storage:**
- Event stream: `reservation_events` table
- Projection: `reservations`, `reservation_allocations` tables

---

### StartPicking Workflow (MITIGATION R-3)

**Purpose:** Atomically transition reservation from SOFT to HARD lock with balance re-validation and conflict detection.

**Workflow Steps:**

1. **Load Reservation State**
   - Load reservation from event stream
   - Verify status is ALLOCATED with SOFT lock
   - Extract allocated HUs and requested quantities

2. **Re-validate Balance from Event Stream**
   - Query StockLedger event stream (NOT projection) for current balance
   - Compute balance for each (location, SKU) pair
   - Verify balance >= requested quantity
   - If insufficient: Return error (do not proceed)

3. **Check for HARD Lock Conflicts**
   - Query ActiveHardLocks projection for matching (location, SKU)
   - Sum existing hard_locked_qty
   - Verify available balance (physical - hard_locked) >= requested quantity
   - If conflict: Return error (do not proceed)

4. **Acquire HARD Lock Atomically**
   - Append PickingStarted event to Reservation stream with expected version
   - If concurrency conflict: Retry with exponential backoff (max 3 retries)
   - If retries exhausted: Return concurrency error

5. **Update ActiveHardLocks Projection (Inline)**
   - Insert row into ActiveHardLocks in same transaction as PickingStarted event
   - Atomic update ensures consistency

**Concurrency Guarantees:**
- Balance validation and HARD lock acquisition are atomic
- Multiple concurrent StartPicking commands serialize via optimistic concurrency
- ActiveHardLocks projection updated atomically with event append

**Error Handling:**
- Insufficient balance: Return domain error (no retry)
- HARD lock conflict: Return domain error (no retry)
- Concurrency conflict: Retry with backoff (max 3 attempts)
- Retries exhausted: Return concurrency error to caller

---

### Aggregate 4: Valuation (Event Sourced)

**Purpose:** Financial interpretation of stock, completely decoupled from physical quantities.

**Aggregate Root:** `Valuation`

**Identity:** `ValuationId` (typically per SKU)

**Entities:**
- `CostAdjustment` (timestamp, amount, reason, approver)

**Value Objects:**
- `SKU` (string)
- `UnitCost` (decimal)
- `AdjustmentReason` (enum: DAMAGE, OBSOLESCENCE, LANDED_COST, MANUAL, WRITE_DOWN)

**Commands:**
- `ApplyCostAdjustment(sku, newCost, reason, approver)`
- `AllocateLandedCost(sku, amount, reason)`
- `WriteDownStock(sku, percentage, reason, approver)`

**Events:**
- `CostAdjusted(sku, oldCost, newCost, reason, approver, timestamp)`
- `LandedCostAllocated(sku, amount, reason, timestamp)`
- `StockWrittenDown(sku, percentage, reason, approver, timestamp)`

**Invariants:**
1. Unit cost can be adjusted without changing physical quantity
2. Adjustments require reason and approver
3. Historical cost changes are immutable
4. On-hand value = quantity (from StockLedger) × cost (from Valuation)

**Storage:**
- Event stream: `valuation_events` table
- Projection: `valuations` table (current unit cost per SKU)

---

### Aggregate 5: WarehouseLayout (State-Based)

**Purpose:** Configuration defining physical topology of warehouses.

**Aggregate Root:** `WarehouseLayout`

**Identity:** `WarehouseId` (GUID)

**Entities:**
- `Aisle` (code, location)
- `Rack` (code, aisle, dimensions)
- `Bin` (code, rack, 3D coordinates, capacity)

**Value Objects:**
- `Coordinates3D` (x, y, z)
- `BinCapacity` (maxPallets, weightLimit, volume)
- `ZoneType` (enum: COLD, AMBIENT, HAZMAT)

**Commands:**
- `DefineBin(warehouse, aisle, rack, binCode, coordinates, capacity)`
- `ModifyBin(binCode, newCapacity)`
- `RemoveBin(binCode)`

**Events:**
- `BinDefined(warehouse, binCode, coordinates, capacity)`
- `BinModified(binCode, newCapacity)`
- `BinRemoved(binCode)`

**Invariants:**
1. Bin coordinates must be unique within warehouse
2. Bins cannot overlap in 3D space
3. Capacity constraints enforced when placing HUs
4. Cannot delete bin if it contains stock

**Storage:**
- State tables: `warehouses`, `aisles`, `racks`, `bins`

---

### Aggregate 6: LogicalWarehouse (State-Based)

**Purpose:** Virtual grouping of stock for categorization and reporting.

**Aggregate Root:** `LogicalWarehouse`

**Identity:** `LogicalWarehouseId` (GUID)

**Entities:**
- `CategoryAssignment` (SKU → category mapping)

**Value Objects:**
- `CategoryName` (string: TEXTILE, GREEN, HARDWARE, etc.)
- `WarehouseCode` (string: RES, PROD, NLQ, SCRAP)

**Commands:**
- `AssignCategory(sku, category, logicalWH)`
- `RemoveCategory(sku, category)`

**Events:**
- `CategoryAssigned(sku, category, logicalWH)`
- `CategoryRemoved(sku, category)`

**Invariants:**
1. SKU can belong to multiple logical warehouses simultaneously
2. Category assignments are metadata only
3. Changing logical warehouse does NOT trigger physical movement

**Storage:**
- State tables: `logical_warehouses`, `category_assignments`


## Data Models

### Event Store Schema

**stock_movement_events**
```sql
CREATE TABLE stock_movement_events (
  movement_id UUID PRIMARY KEY,
  sequence_number BIGSERIAL NOT NULL,
  sku VARCHAR(100) NOT NULL,
  quantity DECIMAL(18,4) NOT NULL CHECK (quantity > 0),
  from_location VARCHAR(200) NOT NULL,
  to_location VARCHAR(200) NOT NULL CHECK (to_location != from_location),
  movement_type VARCHAR(50) NOT NULL,
  timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  operator_id UUID NOT NULL,
  handling_unit_id UUID,
  reason TEXT,
  INDEX idx_sequence (sequence_number),
  INDEX idx_sku_timestamp (sku, timestamp),
  INDEX idx_location_sku (from_location, sku),
  INDEX idx_location_sku_to (to_location, sku),
  INDEX idx_hu (handling_unit_id)
);
```

**reservation_events**
```sql
CREATE TABLE reservation_events (
  event_id UUID PRIMARY KEY,
  reservation_id UUID NOT NULL,
  sequence_number BIGSERIAL NOT NULL,
  event_type VARCHAR(100) NOT NULL,
  event_data JSONB NOT NULL,
  timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  INDEX idx_reservation_sequence (reservation_id, sequence_number),
  INDEX idx_timestamp (timestamp)
);
```

**valuation_events**
```sql
CREATE TABLE valuation_events (
  event_id UUID PRIMARY KEY,
  sku VARCHAR(100) NOT NULL,
  sequence_number BIGSERIAL NOT NULL,
  event_type VARCHAR(100) NOT NULL,
  event_data JSONB NOT NULL,
  timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  INDEX idx_sku_sequence (sku, sequence_number),
  INDEX idx_timestamp (timestamp)
);
```

### State Tables

**handling_units**
```sql
CREATE TABLE handling_units (
  hu_id UUID PRIMARY KEY,
  lpn VARCHAR(100) UNIQUE NOT NULL,
  type VARCHAR(50) NOT NULL,
  status VARCHAR(50) NOT NULL,
  location VARCHAR(200) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  sealed_at TIMESTAMPTZ,
  last_moved_at TIMESTAMPTZ,
  version INT NOT NULL DEFAULT 1,
  INDEX idx_location (location),
  INDEX idx_status (status),
  INDEX idx_lpn (lpn)
);

CREATE TABLE handling_unit_lines (
  hu_id UUID NOT NULL REFERENCES handling_units(hu_id),
  sku VARCHAR(100) NOT NULL,
  quantity DECIMAL(18,4) NOT NULL CHECK (quantity >= 0),
  PRIMARY KEY (hu_id, sku),
  INDEX idx_sku (sku)
);
```

**reservations (projection)**
```sql
CREATE TABLE reservations (
  reservation_id UUID PRIMARY KEY,
  purpose VARCHAR(200) NOT NULL,
  priority INT NOT NULL CHECK (priority BETWEEN 1 AND 10),
  status VARCHAR(50) NOT NULL,
  lock_type VARCHAR(50),
  created_at TIMESTAMPTZ NOT NULL,
  started_picking_at TIMESTAMPTZ,
  consumed_at TIMESTAMPTZ,
  version INT NOT NULL DEFAULT 1,
  INDEX idx_status (status),
  INDEX idx_priority (priority DESC)
);

CREATE TABLE reservation_allocations (
  reservation_id UUID NOT NULL REFERENCES reservations(reservation_id),
  handling_unit_id UUID NOT NULL,
  sku VARCHAR(100) NOT NULL,
  allocated_quantity DECIMAL(18,4) NOT NULL,
  PRIMARY KEY (reservation_id, handling_unit_id, sku),
  INDEX idx_hu (handling_unit_id)
);
```

**valuations (projection)**
```sql
CREATE TABLE valuations (
  sku VARCHAR(100) PRIMARY KEY,
  unit_cost DECIMAL(18,4) NOT NULL,
  last_adjusted_at TIMESTAMPTZ NOT NULL,
  version INT NOT NULL DEFAULT 1
);
```

**warehouses and layout**
```sql
CREATE TABLE warehouses (
  warehouse_id UUID PRIMARY KEY,
  code VARCHAR(50) UNIQUE NOT NULL,
  name VARCHAR(200) NOT NULL
);

CREATE TABLE bins (
  bin_id UUID PRIMARY KEY,
  warehouse_id UUID NOT NULL REFERENCES warehouses(warehouse_id),
  code VARCHAR(100) NOT NULL,
  aisle VARCHAR(50),
  rack VARCHAR(50),
  coord_x DECIMAL(10,2),
  coord_y DECIMAL(10,2),
  coord_z DECIMAL(10,2),
  capacity_pallets INT,
  capacity_weight DECIMAL(18,2),
  capacity_volume DECIMAL(18,2),
  UNIQUE (warehouse_id, code),
  UNIQUE (warehouse_id, coord_x, coord_y, coord_z)
);
```

**logical_warehouses**
```sql
CREATE TABLE logical_warehouses (
  logical_wh_id UUID PRIMARY KEY,
  code VARCHAR(50) UNIQUE NOT NULL,
  name VARCHAR(200) NOT NULL
);

CREATE TABLE category_assignments (
  sku VARCHAR(100) NOT NULL,
  category VARCHAR(100) NOT NULL,
  logical_wh_id UUID NOT NULL REFERENCES logical_warehouses(logical_wh_id),
  PRIMARY KEY (sku, category, logical_wh_id),
  INDEX idx_sku (sku),
  INDEX idx_category (category)
);
```

### Projection Tables

**location_balance (projection from stock_movement_events)**
```sql
CREATE TABLE location_balance (
  location VARCHAR(200) NOT NULL,
  sku VARCHAR(100) NOT NULL,
  quantity DECIMAL(18,4) NOT NULL CHECK (quantity >= 0),
  last_updated TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (location, sku),
  INDEX idx_sku (sku),
  INDEX idx_last_updated (last_updated)
);
```

**available_stock (projection from stock_movement_events + reservation_allocations)**
```sql
CREATE TABLE available_stock (
  location VARCHAR(200) NOT NULL,
  sku VARCHAR(100) NOT NULL,
  physical_quantity DECIMAL(18,4) NOT NULL,
  reserved_quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
  available_quantity DECIMAL(18,4) GENERATED ALWAYS AS (physical_quantity - reserved_quantity) STORED,
  last_updated TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (location, sku),
  INDEX idx_sku (sku),
  INDEX idx_available (available_quantity)
);
```

**active_hard_locks (projection from reservation events) [MITIGATION R-4]**
```sql
CREATE TABLE active_hard_locks (
  reservation_id UUID NOT NULL,
  location VARCHAR(200) NOT NULL,
  sku VARCHAR(100) NOT NULL,
  hard_locked_qty DECIMAL(18,4) NOT NULL CHECK (hard_locked_qty > 0),
  started_at TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (reservation_id, location, sku),
  INDEX idx_location_sku (location, sku),
  INDEX idx_started_at (started_at)
);
```

**on_hand_value (projection from location_balance + valuations)**
```sql
CREATE TABLE on_hand_value (
  location VARCHAR(200) NOT NULL,
  sku VARCHAR(100) NOT NULL,
  quantity DECIMAL(18,4) NOT NULL,
  unit_cost DECIMAL(18,4) NOT NULL,
  total_value DECIMAL(18,4) GENERATED ALWAYS AS (quantity * unit_cost) STORED,
  last_updated TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (location, sku),
  INDEX idx_sku (sku)
);
```

### Transactional Outbox

**outbox_messages**
```sql
CREATE TABLE outbox_messages (
  message_id UUID PRIMARY KEY,
  aggregate_type VARCHAR(100) NOT NULL,
  aggregate_id UUID NOT NULL,
  event_type VARCHAR(100) NOT NULL,
  event_data JSONB NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  published_at TIMESTAMPTZ,
  INDEX idx_unpublished (published_at) WHERE published_at IS NULL,
  INDEX idx_created (created_at)
);
```

### Idempotency Tables

**processed_commands**
```sql
CREATE TABLE processed_commands (
  command_id UUID PRIMARY KEY,
  command_type VARCHAR(100) NOT NULL,
  timestamp TIMESTAMPTZ NOT NULL,
  result JSONB NOT NULL,
  INDEX idx_timestamp (timestamp DESC)
);
```

**event_processing_checkpoints**
```sql
CREATE TABLE event_processing_checkpoints (
  handler_name VARCHAR(200) NOT NULL,
  event_id UUID NOT NULL,
  processed_at TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (handler_name, event_id)
);
```

**saga_state**
```sql
CREATE TABLE saga_state (
  saga_id UUID PRIMARY KEY,
  saga_type VARCHAR(100) NOT NULL,
  current_step INT NOT NULL,
  step_results JSONB NOT NULL,
  status VARCHAR(50) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL,
  INDEX idx_status (status),
  INDEX idx_type (saga_type)
);
```


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property Reflection

After analyzing all acceptance criteria, the following properties have been identified. Redundant properties have been eliminated through logical analysis:

**Eliminated Redundancies:**
- Requirement 2.5 is redundant with 2.4 (both test sealing empty HU)
- Several UI/UX requirements (7.1-7.8) are not testable as properties
- Projection lag UI behavior (6.3-6.6) is UI-specific, not core logic
- Database permission enforcement (1.4) is infrastructure, not runtime property

**Consolidated Properties:**
- Split and merge operations combined into container operation properties
- Idempotency rules consolidated into general idempotency property
- Consistency checks consolidated into verification properties

### Core Domain Properties

**Property 1: Movement Immutability**
*For any* StockMovement event, once appended to the event stream, it SHALL remain unchanged and SHALL NOT be deleted.
**Validates: Requirements 1.1**

**Property 2: Balance Non-Negativity**
*For any* physical location and SKU, the computed balance (sum of movements TO minus sum of movements FROM) SHALL always be >= 0.
**Validates: Requirements 1.2**

**Property 3: Virtual Location Bypass**
*For any* movement from a virtual location (SUPPLIER, PRODUCTION, SCRAP, SYSTEM), the balance validation SHALL be skipped and the movement SHALL succeed regardless of source balance.
**Validates: Requirements 1.3**

**Property 4: Movement Constraint Validation**
*For any* movement, the from location SHALL NOT equal the to location, and the quantity SHALL be > 0.
**Validates: Requirements 1.6, 1.7**

**Property 5: Time-Travel Balance Computation**
*For any* location, SKU, and timestamp, computing the balance from the event stream up to that timestamp SHALL produce the correct historical balance.
**Validates: Requirements 1.8**

**Property 6: Handling Unit LPN Uniqueness**
*For any* set of handling units, all LPN identifiers SHALL be unique.
**Validates: Requirements 2.1**

**Property 7: Sealed Handling Unit Immutability**
*For any* handling unit in SEALED status, attempts to add or remove lines SHALL be rejected.
**Validates: Requirements 2.3**

**Property 8: Empty Handling Unit Seal Rejection**
*For any* handling unit with zero lines, attempts to seal it SHALL be rejected.
**Validates: Requirements 2.4**

**Property 9: Handling Unit Movement Generates Stock Movements**
*For any* handling unit with N lines, moving it to a new location SHALL generate exactly N StockMovement events via StockLedger.
**Validates: Requirements 2.6**

**Property 10: Handling Unit Projection Consistency**
*For any* StockMoved event with a handling unit ID, the HandlingUnit projection SHALL update its state to reflect the movement (add/remove lines, update location).
**Validates: Requirements 2.7**

**Property 11: Handling Unit Single Location**
*For any* handling unit at any point in time, it SHALL have exactly one current location.
**Validates: Requirements 2.8**

**Property 12: Split Operation Correctness**
*For any* handling unit split operation, the sum of quantities in source HU and new HU SHALL equal the original source HU quantity.
**Validates: Requirements 2.9**

**Property 13: Merge Operation Correctness**
*For any* merge operation on handling units at the same location, the target HU SHALL contain all lines from source HUs, and source HUs SHALL be marked EMPTY.
**Validates: Requirements 2.10**

**Property 14: Reservation Initial State**
*For any* newly created reservation, its status SHALL be PENDING.
**Validates: Requirements 3.1**

**Property 15: Allocation Balance Query**
*For any* reservation allocation, the system SHALL query StockLedger for current balance before allocating.
**Validates: Requirements 3.2**

**Property 16: Soft Lock Overbooking**
*For any* stock with SOFT lock reservations, multiple reservations SHALL be allowed to allocate the same stock (overbooking).
**Validates: Requirements 3.3**

**Property 17: Hard Lock Transition**
*For any* reservation transitioning from ALLOCATED to PICKING, the lock type SHALL change from SOFT to HARD.
**Validates: Requirements 3.4**

**Property 18: Hard Lock Re-validation**
*For any* reservation transitioning to HARD lock, the system SHALL re-validate that balance is sufficient.
**Validates: Requirements 3.5**

**Property 19: Soft Lock Bumping**
*For any* SOFT lock reservation, when a HARD lock request conflicts, the SOFT reservation SHALL be bumped.
**Validates: Requirements 3.6**

**Property 20: Hard Lock Exclusivity**
*For any* HARD lock reservation, attempts to bump it SHALL be rejected.
**Validates: Requirements 3.7**

**Property 21: Reservation Consumption**
*For any* reservation, when all requested quantity is picked, the status SHALL transition to CONSUMED.
**Validates: Requirements 3.9**

**Property 22: Hard Reservation No Auto-Expiry**
*For any* HARD lock reservation in PICKING state, it SHALL NOT auto-expire or auto-cancel.
**Validates: Requirements 3.10-3.12**

**Property 23: Pick Transaction Ordering**
*For any* pick operation, StockMovement SHALL be recorded BEFORE HandlingUnit projection update, which SHALL complete BEFORE Reservation consumption.
**Validates: Requirements 4.1-4.7, 17.3-17.12**

**Property 24: Offline Operation Whitelist**
*For any* offline operation, only PickStock (with HARD lock) and TransferStock (with assigned HUs) SHALL be allowed; all other operations SHALL be rejected.
**Validates: Requirements 5.1-5.6**

**Property 25: Offline Sync Rejection**
*For any* offline command synced to server, if the reservation was bumped while offline, the command SHALL be rejected.
**Validates: Requirements 5.8**

**Property 26: Projection Update Timeliness**
*For any* StockMoved event, the LocationBalance projection SHALL be updated within 5 seconds.
**Validates: Requirements 6.1, 6.2**

**Property 27: Projection Rebuild Correctness**
*For any* projection, rebuilding it from the event stream SHALL produce the same state as the current projection (assuming no events were missed).
**Validates: Requirements 6.11, 6.12**

**Property 28: Label Print on Seal**
*For any* handling unit that is sealed, a print command SHALL be sent to the label printer.
**Validates: Requirements 8.1**

**Property 29: Label Print Idempotency**
*For any* print command with the same PrintJobId, the printer SHALL process it only once (idempotent).
**Validates: Requirements 8.5, 8.6**

**Property 30: Agnum Export Data Correctness**
*For any* Agnum export, the data SHALL be computed from StockMovement ledger (balances), Valuation (costs), and LogicalWarehouse (categories).
**Validates: Requirements 9.1-9.4**

**Property 31: Agnum Export Idempotency**
*For any* export with the same ExportId, Agnum SHALL process it only once (idempotent).
**Validates: Requirements 9.6**

**Property 32: Valuation Adjustment Requires Approval**
*For any* cost adjustment, it SHALL require both a reason and an approver.
**Validates: Requirements 10.1**

**Property 33: Valuation Independence**
*For any* cost adjustment, the physical quantity (from StockLedger) SHALL remain unchanged.
**Validates: Requirements 10.7**

**Property 34: Layout Coordinate Uniqueness**
*For any* warehouse, all bin 3D coordinates SHALL be unique.
**Validates: Requirements 11.1**

**Property 35: Layout Occupied Bin Protection**
*For any* bin with stock, attempts to delete it SHALL be rejected.
**Validates: Requirements 11.5**

**Property 36: Command Idempotency**
*For any* command with the same CommandId, executing it multiple times SHALL return the same result (cached from first execution).
**Validates: Requirements 12.1-12.3**

**Property 37: Event Handler Idempotency**
*For any* event, processing it multiple times by the same handler SHALL produce the same result (idempotent).
**Validates: Requirements 12.4-12.5**

**Property 38: Saga Step Idempotency**
*For any* saga step, executing it multiple times SHALL return the same result (cached from first execution).
**Validates: Requirements 12.7**

**Property 39: Balance Consistency Verification**
*For any* location and SKU, the LocationBalance projection SHALL match the computed balance from the event stream.
**Validates: Requirements 13.1**

**Property 40: No Negative Balance Verification**
*For any* location and SKU, the balance SHALL NOT be negative.
**Validates: Requirements 13.2**

**Property 41: ERP Integration Retry**
*For any* ERP notification failure, the system SHALL retry with exponential backoff.
**Validates: Requirements 14.7-14.10**

**Property 42: ERP Event Idempotency**
*For any* MaterialConsumed event with the same event ID, ERP SHALL process it only once (idempotent).
**Validates: Requirements 14.11-14.12**

**Property 43: Goods Receipt Orphan Detection**
*For any* StockMovement recorded without corresponding HandlingUnit creation, the system SHALL create an orphan movement alert.
**Validates: Requirements 15.6-15.8**

**Property 44: Transfer Validation**
*For any* transfer operation, if any StockMovement fails validation, the entire transfer SHALL be aborted without updating HU location.
**Validates: Requirements 16.4**

**Property 45: Split and Merge Audit Trail**
*For any* split or merge operation, domain events (HandlingUnitSplit, HandlingUnitMerged) SHALL be emitted for audit trail.
**Validates: Requirements 18.4, 18.10, 18.12**

### Round-Trip Properties

**Property 46: Event Serialization Round-Trip**
*For any* domain event, serializing it to JSON and then deserializing SHALL produce an equivalent event.
**Validates: System-wide serialization correctness**

**Property 47: Projection Rebuild Round-Trip**
*For any* projection, recording events → building projection → rebuilding from events SHALL produce the same projection state.
**Validates: System-wide projection correctness**

### Metamorphic Properties

**Property 48: Balance Computation Associativity**
*For any* set of movements, computing balance by grouping movements in different orders SHALL produce the same result.
**Validates: System-wide balance computation correctness**

**Property 49: Reservation Priority Ordering**
*For any* set of reservations on the same stock, allocating them in priority order SHALL result in higher priority reservations being allocated first.
**Validates: System-wide reservation fairness**

### Mitigation-Specific Properties

**Property 50: StockLedger Atomic Balance Validation (MITIGATION V-2)**
*For any* concurrent stock movements on the same location and SKU, balance validation and event append SHALL be atomic, preventing overdraw even under concurrent load.
**Validates: Requirements 1.9-1.11**

**Property 51: StartPicking Atomic HARD Lock Acquisition (MITIGATION R-3)**
*For any* concurrent StartPicking commands on overlapping stock, exactly one SHALL acquire the HARD lock, and others SHALL fail with concurrency error.
**Validates: Requirements 3.13-3.16**

**Property 52: ActiveHardLocks Consistency (MITIGATION R-4)**
*For any* reservation in PICKING state, the ActiveHardLocks projection SHALL contain exactly one corresponding row with matching location, SKU, and quantity.
**Validates: Requirement 19**

**Property 53: Projection Rebuild Determinism (MITIGATION V-5)**
*For any* projection and event stream, rebuilding the projection from the event stream SHALL produce identical state to live processing.
**Validates: Requirements 6.13-6.15**


## Error Handling

### Error Categories

**Domain Errors (Business Rule Violations)**
- `InsufficientBalanceError`: Attempted movement from location with insufficient stock
- `SealedHandlingUnitError`: Attempted modification of sealed HU
- `EmptyHandlingUnitError`: Attempted to seal empty HU
- `InvalidLocationError`: Location does not exist in layout
- `ReservationConflictError`: HARD lock conflict during StartPicking
- `ReservationBumpedError`: Reservation was bumped by higher priority
- `NegativeQuantityError`: Quantity <= 0
- `SameLocationError`: From location == to location
- `DuplicateLPNError`: LPN already exists

**Integration Errors**
- `LabelPrintFailedError`: Label printer unavailable or failed
- `AgnumExportFailedError`: Agnum API unavailable or rejected export
- `ERPNotificationFailedError`: ERP/MES notification failed

**Infrastructure Errors**
- `EventStoreUnavailableError`: Event store database unavailable
- `ProjectionLagExceededError`: Projection lag > 30 seconds
- `OutboxProcessorFailedError`: Outbox processor unable to deliver events

### Error Handling Strategies

**Domain Errors:**
- Return error to caller immediately
- Do NOT retry (business rule violations are permanent)
- Log error with context (aggregate ID, command, user)
- Return HTTP 400 Bad Request with error details

**Integration Errors:**
- Retry with exponential backoff (3 attempts)
- If retries exhausted, log error and alert administrator
- For operational integration (<5s SLA): Alert operator immediately
- For financial integration (minutes SLA): Queue for background retry
- For process integration (<30s SLA): Use saga compensation

**Infrastructure Errors:**
- Retry with exponential backoff (5 attempts)
- If retries exhausted, enter degraded mode
- Projection lag: Display stale data indicator in UI
- Event store unavailable: Queue commands for later processing
- Outbox processor failed: Alert operations team (P0)

### Compensation Strategies

**ReceiveGoodsSaga Compensation:**
- If HU creation fails after StockMovement recorded:
  - Create orphan movement alert
  - Provide manual reconciliation workflow
  - Operator can create HU retroactively or adjust inventory

**PickStockSaga Compensation:**
- If StockLedger transaction fails: Rollback (no partial state)
- If HandlingUnit projection fails: Replay from StockMoved event
- If Reservation update fails: Retry consumption after HU projection completes

**TransferStockSaga Compensation:**
- If any StockMovement fails: Abort transfer, do not update HU location
- If HU location update fails: Retry update (StockMovements already recorded)

**AllocationSaga Compensation:**
- If allocation fails (insufficient stock): Notify user, keep reservation PENDING
- If allocation succeeds but notification fails: Retry notification

**AgnumExportSaga Compensation:**
- If export fails: Retry with exponential backoff
- If retries exhausted: Alert administrator, queue for manual export

**ERP Integration Compensation:**
- If MaterialReserved notification fails: Retry, queue for background delivery
- If MaterialConsumed notification fails: Retry, maintain eventual consistency via reconciliation

### Error Recovery Procedures

**Orphan Movement Recovery:**
1. Detect: StockMovement exists without corresponding HU
2. Alert: Create orphan movement alert
3. Reconcile: Operator creates HU retroactively OR adjusts inventory via AdjustStock command

**Projection Lag Recovery:**
1. Detect: Projection lag > 30 seconds
2. Alert: Operations team notified
3. Investigate: Check event bus health, projection handler health
4. Recover: Restart projection handler, replay missed events

**Balance Mismatch Recovery:**
1. Detect: Daily consistency check finds mismatch
2. Alert: P0 alert to operations team
3. Investigate: Compare LocationBalance projection with event stream computation
4. Recover: Rebuild projection from event stream

**Reservation Conflict Recovery:**
1. Detect: StartPicking fails due to insufficient balance
2. Notify: Operator sees error message
3. Resolve: Operator finds alternative stock OR waits for stock arrival OR escalates to manager

**Offline Sync Conflict Recovery:**
1. Detect: Offline command rejected (reservation bumped)
2. Notify: Operator sees error in reconciliation report
3. Resolve: Operator returns stock to shelf OR contacts manager to create new reservation


## Testing Strategy

### Dual Testing Approach

The system requires both unit testing and property-based testing for comprehensive coverage:

**Unit Tests:**
- Specific examples demonstrating correct behavior
- Edge cases (empty HU, zero quantity, invalid location)
- Error conditions (insufficient balance, sealed HU modification)
- Integration points between components
- Saga compensation scenarios

**Property-Based Tests:**
- Universal properties across all inputs (see Correctness Properties section)
- Comprehensive input coverage through randomization
- Invariant validation (no negative balances, sealed HU immutability)
- Round-trip properties (event serialization, projection rebuild)
- Metamorphic properties (balance computation associativity)

### Property-Based Testing Configuration

**Library:** Use FsCheck (.NET) or Hypothesis (Python) or fast-check (TypeScript)

**Configuration:**
- Minimum 100 iterations per property test
- Each test references its design document property
- Tag format: `Feature: warehouse-core-phase1, Property {number}: {property_text}`

**Example Property Test (Pseudocode):**
```csharp
[Property]
[Tag("Feature: warehouse-core-phase1, Property 2: Balance Non-Negativity")]
public Property BalanceNeverNegative()
{
    return Prop.ForAll(
        Arb.Generate<List<StockMovement>>(),
        movements =>
        {
            var ledger = new StockLedger();
            foreach (var movement in movements)
            {
                try
                {
                    ledger.RecordMovement(movement);
                }
                catch (InsufficientBalanceError)
                {
                    // Expected for invalid movements
                }
            }
            
            var balances = ledger.ComputeAllBalances();
            return balances.All(b => b.Quantity >= 0);
        }
    );
}
```

### Unit Testing Strategy

**Aggregate Tests:**
- Test each aggregate command with valid inputs
- Test each aggregate command with invalid inputs (expect errors)
- Test aggregate invariants are enforced
- Test aggregate state transitions

**Projection Tests:**
- Test projection updates from events
- Test projection idempotency (replay same event)
- Test projection rebuild from event stream

**Saga Tests:**
- Test saga happy path (all steps succeed)
- Test saga compensation (step fails, compensation triggered)
- Test saga idempotency (restart saga, steps not re-executed)

**Integration Tests:**
- Test label printing integration (mock printer)
- Test Agnum export integration (mock Agnum API)
- Test ERP integration (mock ERP)

### Test Data Generators

**For Property-Based Tests:**

```csharp
// Generate valid StockMovement
Arb.Generate<StockMovement>()
    .Where(m => m.Quantity > 0 && m.FromLocation != m.ToLocation)

// Generate HandlingUnit with random lines
Arb.Generate<HandlingUnit>()
    .Where(hu => hu.Lines.Count > 0 && hu.Status == HandlingUnitStatus.OPEN)

// Generate Reservation with random priority
Arb.Generate<Reservation>()
    .Where(r => r.Priority >= 1 && r.Priority <= 10)

// Generate valid Location (physical or virtual)
Gen.OneOf(
    Gen.Elements("R1-C1-L1", "R2-C3-L2", "A1-B1"),  // Physical
    Gen.Elements("SUPPLIER", "PRODUCTION", "SCRAP")  // Virtual
)
```

### Test Coverage Goals

**Code Coverage:**
- Aggregate logic: 95%+
- Saga logic: 90%+
- Projection logic: 90%+
- Integration adapters: 80%+

**Property Coverage:**
- All 49 correctness properties implemented as property tests
- Each property test runs minimum 100 iterations
- All properties pass consistently

**Scenario Coverage:**
- All user stories have at least one unit test
- All error scenarios have unit tests
- All compensation scenarios have unit tests

### Testing Pyramid

```
        /\
       /  \
      / E2E \      (10% - Critical user flows)
     /______\
    /        \
   / Integr.  \    (20% - Component integration)
  /____________\
 /              \
/   Unit + Prop  \  (70% - Aggregate logic, properties)
/__________________\
```

### Continuous Testing

**Pre-Commit:**
- Run all unit tests
- Run fast property tests (10 iterations)

**CI Pipeline:**
- Run all unit tests
- Run all property tests (100 iterations)
- Run integration tests
- Check code coverage

**Nightly:**
- Run extended property tests (1000 iterations)
- Run consistency verification tests
- Run performance tests


## Process Managers and Sagas

### ReceiveGoodsSaga

**Purpose:** Orchestrates goods receipt workflow from supplier.

**Triggered By:** `ReceiveGoodsCommand`

**State Machine:**
```
STARTED → MOVEMENT_RECORDED → HU_CREATED → HU_SEALED → LABEL_REQUESTED → COMPLETED
   ↓
FAILED (compensation)
```

**Steps:**
1. Validate command (SKU exists, location exists)
2. Record StockMovement (SUPPLIER → location) via StockLedger
3. Create HandlingUnit at location
4. Add lines to HU
5. Seal HU
6. Request label print
7. Mark saga complete

**Compensation:**
- If HU creation fails after movement: Create orphan movement alert
- If seal fails: HU remains OPEN, operator can seal manually
- If label print fails: Retry 3x, then alert operator

**Idempotency:**
- Each step checks if already executed
- StockMovement uses unique MovementId
- HU creation checks if HU already exists

---

### TransferStockSaga

**Purpose:** Orchestrates transfer of HU between locations.

**Triggered By:** `TransferStockCommand`

**State Machine:**
```
STARTED → VALIDATING → MOVEMENTS_RECORDING → HU_UPDATING → COMPLETED
   ↓
FAILED (rollback)
```

**Steps:**
1. Validate destination location exists
2. Get HU with all lines
3. For each line: Record StockMovement (from → to) via StockLedger
4. Update HU location
5. Mark saga complete

**Compensation:**
- If any movement fails: Abort, do not update HU location
- If HU update fails: Retry update (movements already recorded)

**Idempotency:**
- Each movement uses unique MovementId
- HU location update is idempotent

---

### PickStockSaga

**Purpose:** Orchestrates picking against reservation (CRITICAL - enforces transaction ordering).

**[MITIGATION V-3]** Simplified to two-step saga: StockMovement recording → Reservation consumption. HandlingUnit projection updates asynchronously (not a saga step).

**Triggered By:** `PickStockCommand`

**State Machine:**
```
STARTED → VALIDATING → MOVEMENT_RECORDED → RESERVATION_CONSUMED → COMPLETED
   ↓
FAILED (compensation)
```

**Steps:**
1. Validate reservation is PICKING (HARD locked)
2. Validate HU is allocated to reservation
3. **CRITICAL:** Record StockMovement (location → PRODUCTION) via StockLedger FIRST
4. **[MITIGATION V-3]** Update Reservation consumption (do NOT wait for HU projection)
5. If fully consumed, mark reservation CONSUMED
6. Mark saga complete

**HandlingUnit Projection (Asynchronous):**
- HU projection subscribes to StockMoved events independently
- Processes events asynchronously (not part of saga coordination)
- Updates HU state (remove line, update location) in separate transaction
- Eventual consistency: HU projection may lag behind saga completion

**Transaction Ordering (MANDATORY):**
```
T1: StockLedger.RecordMovement → COMMIT → Publish StockMoved event
T2: Reservation.Consume → COMMIT
T3: (Async) HandlingUnit projection processes StockMoved event → COMMIT
```

**Compensation:**
- If T1 fails: Rollback (no partial state)
- If T2 fails: Retry consumption (StockMovement already recorded)
- If T3 fails: Replay from StockMoved event (projection recovery)

**Idempotency:**
- Movement uses unique MovementId
- Reservation consumption is idempotent
- HU projection checks if event already processed

---

### AllocationSaga

**Purpose:** Allocates available HUs to reservation.

**Triggered By:** `ReservationCreated` event

**State Machine:**
```
STARTED → SEARCHING → ALLOCATING → COMPLETED
   ↓
FAILED (insufficient stock)
```

**Steps:**
1. Query AvailableStock projection
2. Find HUs matching requested SKUs
3. Validate balance sufficient
4. Allocate HUs to reservation (SOFT lock)
5. Publish StockAllocated event
6. Mark saga complete

**Compensation:**
- If allocation fails: Notify user, keep reservation PENDING
- Can retry allocation when new stock arrives

**Idempotency:**
- Allocation checks if already allocated
- Uses reservation version for optimistic locking

---

### AgnumExportSaga

**Purpose:** Exports stock snapshot to Agnum accounting system.

**Triggered By:** `ExportToAgnumCommand` (scheduled or manual)

**State Machine:**
```
STARTED → QUERYING → TRANSFORMING → SENDING → COMPLETED
   ↓
FAILED (retry)
```

**Steps:**
1. Query StockMovement ledger for current balances
2. Query Valuation for unit costs
3. Query LogicalWarehouse for category mappings
4. Apply Agnum mapping rules (warehouse → account)
5. Transform to Agnum format (CSV or JSON)
6. Send to Agnum API with unique ExportId
7. Record export timestamp
8. Mark saga complete

**Compensation:**
- If Agnum API fails: Retry with exponential backoff (3 attempts)
- If retries exhausted: Alert administrator, queue for manual export

**Idempotency:**
- Export includes unique ExportId
- Agnum deduplicates by ExportId

---

### ERP Integration Saga

**Purpose:** Coordinates material requests from ERP to warehouse reservations.

**Triggered By:** `MaterialRequested` event from ERP

**State Machine:**
```
STARTED → RESERVATION_CREATED → ALLOCATED → NOTIFIED → COMPLETED
   ↓
FAILED (compensation)
```

**Steps:**
1. Translate MaterialRequested to CreateReservation command
2. Create reservation
3. Wait for AllocationSaga to allocate stock
4. Send MaterialReserved event to ERP
5. Mark saga complete

**Compensation:**
- If reservation creation fails: Notify ERP of failure
- If allocation fails: Notify ERP, keep reservation PENDING
- If ERP notification fails: Retry with exponential backoff

**Idempotency:**
- Reservation uses unique ReservationId
- ERP notification includes unique event ID

---

## Projection Rebuild Contract (MITIGATION V-5)

**Purpose:** Ensure projection rebuilds produce identical results to live processing (determinism guarantee).

**Problem:** Non-deterministic projections can produce different results when rebuilt from event stream vs. live processing, leading to data inconsistencies.

**Solution:** Three-rule contract for all projections:

### Rule A: Stream-Ordered Replay

**Requirement:** Replay events in stream order (by sequence number, not timestamp).

**Rationale:**
- Timestamps can be non-monotonic (clock skew, out-of-order events)
- Sequence numbers guarantee total ordering within a stream
- Live processing follows sequence order, rebuild must match

**Implementation:**
```sql
-- Correct: Order by sequence number
SELECT * FROM mt_events 
WHERE stream_id = 'stock-ledger-main' 
ORDER BY sequence_number ASC;

-- Incorrect: Order by timestamp (non-deterministic)
SELECT * FROM mt_events 
WHERE stream_id = 'stock-ledger-main' 
ORDER BY timestamp ASC;
```

---

### Rule B: Self-Contained Event Data

**Requirement:** Projection logic must use only data contained in the event itself (no external queries).

**Rationale:**
- External data can change between live processing and rebuild
- Queries to other aggregates/projections introduce non-determinism
- Events must be self-contained snapshots of all required data

**Violations to Avoid:**
```csharp
// VIOLATION: Querying external data during projection
public void Apply(StockMovedEvent evt)
{
    // BAD: Querying Valuation during projection
    var unitCost = _valuationRepository.GetUnitCost(evt.SKU);
    _projection.Value = evt.Quantity * unitCost;
}

// CORRECT: Event contains all required data
public void Apply(StockMovedEvent evt)
{
    // GOOD: Event includes unit cost
    _projection.Value = evt.Quantity * evt.UnitCostAtTime;
}
```

**Audit Checklist:**
- [ ] LocationBalance: Uses only StockMoved event data ✓
- [ ] AvailableStock: Uses only StockMoved + StockAllocated event data ✓
- [ ] OnHandValue: Requires unit cost - **must be included in StockMoved event**
- [ ] ActiveHardLocks: Uses only PickingStarted/Consumed/Cancelled event data ✓

---

### Rule C: Rebuild Verification Gate

**Requirement:** Use shadow table approach with checksum verification before swapping to production.

**Rationale:**
- Detect rebuild errors before affecting production queries
- Provide rollback mechanism if rebuild produces different results
- Enable safe, zero-downtime projection rebuilds

**Rebuild Process:**

1. **Create Shadow Table**
   ```sql
   CREATE TABLE location_balance_shadow (LIKE location_balance INCLUDING ALL);
   ```

2. **Replay Events to Shadow Table**
   ```csharp
   var events = await _session.Events.FetchStreamAsync("stock-ledger-main");
   foreach (var evt in events.OrderBy(e => e.Sequence))
   {
       await _shadowProjection.Apply(evt);
   }
   ```

3. **Compute Checksums**
   ```sql
   -- Production table checksum
   SELECT MD5(STRING_AGG(location || sku || quantity::text, '' ORDER BY location, sku))
   FROM location_balance;
   
   -- Shadow table checksum
   SELECT MD5(STRING_AGG(location || sku || quantity::text, '' ORDER BY location, sku))
   FROM location_balance_shadow;
   ```

4. **Verify and Swap**
   ```csharp
   if (productionChecksum == shadowChecksum)
   {
       // Checksums match - safe to swap
       await SwapTables("location_balance", "location_balance_shadow");
   }
   else
   {
       // Checksums differ - investigate before swapping
       await AlertAdministrator("Projection rebuild checksum mismatch");
       await GenerateDiffReport("location_balance", "location_balance_shadow");
   }
   ```

5. **Atomic Swap**
   ```sql
   BEGIN;
   ALTER TABLE location_balance RENAME TO location_balance_old;
   ALTER TABLE location_balance_shadow RENAME TO location_balance;
   DROP TABLE location_balance_old;
   COMMIT;
   ```

**Verification Tooling:**
- Rebuild command: `dotnet run -- rebuild-projection LocationBalance --verify`
- Diff report: Shows rows that differ between production and shadow
- Rollback: Keep old table for 24 hours before dropping

---

### Projection Rebuild Invariant

**Invariant:** For all projections P and event streams E, rebuilding P from E produces identical state to live processing of E.

**Formal Statement:**
```
∀ projection P, event_stream E:
  rebuild(P, E) = live_process(P, E)
```

**Testing Strategy:**
- Property test: Generate random event stream, compare live vs. rebuild
- Integration test: Rebuild production projections weekly, verify checksums
- Chaos test: Randomly rebuild projections during load testing

---

## Offline / Edge Architecture

### Edge Agent Architecture

```
┌─────────────────────────────────────────┐
│         Edge Device (Tablet/PC)         │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │      Offline-Capable UI           │ │
│  └───────────────┬───────────────────┘ │
│                  │                      │
│  ┌───────────────▼───────────────────┐ │
│  │      Edge Command Queue           │ │
│  │  (SQLite local storage)           │ │
│  └───────────────┬───────────────────┘ │
│                  │                      │
│  ┌───────────────▼───────────────────┐ │
│  │   Offline Operation Whitelist     │ │
│  │   - PickStock (HARD locked)       │ │
│  │   - TransferStock (assigned HUs)  │ │
│  └───────────────┬───────────────────┘ │
│                  │                      │
│  ┌───────────────▼───────────────────┐ │
│  │      Sync Engine                  │ │
│  │  (reconnect → sync queue)         │ │
│  └───────────────┬───────────────────┘ │
└──────────────────┼─────────────────────┘
                   │
                   ↓ (when online)
┌─────────────────────────────────────────┐
│         Server (Warehouse System)       │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │   Command Validation              │ │
│  │   - Check reservation still valid │ │
│  │   - Check HU still assigned       │ │
│  └───────────────┬───────────────────┘ │
│                  │                      │
│  ┌───────────────▼───────────────────┐ │
│  │   Command Processing              │ │
│  │   (normal saga execution)         │ │
│  └───────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

### Offline Operation Rules

**Allowed Offline:**
1. **PickStock** - Only if reservation is HARD locked on server before going offline
2. **TransferStock** - Only for HUs already assigned to operator before going offline

**Forbidden Offline:**
- AllocateReservation (requires real-time balance check)
- StartPicking (requires conflict detection)
- AdjustStock (requires approval)
- ApplyCostAdjustment (requires approval)
- SplitHU / MergeHU (requires real-time validation)
- CreateReservation (requires allocation saga)

### Offline Command Queue

**Schema:**
```sql
CREATE TABLE offline_command_queue (
  command_id UUID PRIMARY KEY,
  command_type VARCHAR(100) NOT NULL,
  command_data JSONB NOT NULL,
  queued_at TIMESTAMPTZ NOT NULL,
  synced_at TIMESTAMPTZ,
  sync_result JSONB,
  INDEX idx_unsynced (synced_at) WHERE synced_at IS NULL
);
```

**Queue Behavior:**
- Commands queued in order
- Synced in FIFO order when reconnected
- Failed commands marked with error, not retried automatically
- Operator sees reconciliation report for failed commands

### Conflict Detection

**Reservation Bumped While Offline:**
```
Timeline:
T0: Operator goes offline with SOFT reservation R1
T1: Server bumps R1 (higher priority order)
T2: Operator queues PickStock(R1) offline
T3: Operator reconnects
T4: Sync attempts PickStock(R1)
T5: Server validates: R1 status = BUMPED
T6: Server rejects: 400 Bad Request "Reservation bumped"
T7: Operator sees error in reconciliation report
```

**HU Moved While Offline:**
```
Timeline:
T0: Operator goes offline with assigned HU-001
T1: Server moves HU-001 (manager override)
T2: Operator queues TransferStock(HU-001) offline
T3: Operator reconnects
T4: Sync attempts TransferStock(HU-001)
T5: Server validates: HU-001 location changed
T6: Server rejects: 400 Bad Request "HU location changed"
T7: Operator sees error in reconciliation report
```

### Sync Reconciliation Report

**Report Format:**
```json
{
  "syncTimestamp": "2026-02-07T10:30:00Z",
  "totalCommands": 15,
  "successful": 13,
  "failed": 2,
  "failures": [
    {
      "commandId": "...",
      "commandType": "PickStock",
      "error": "Reservation R1 bumped by higher priority",
      "suggestedAction": "Return stock to shelf or contact manager"
    },
    {
      "commandId": "...",
      "commandType": "TransferStock",
      "error": "HU location changed",
      "suggestedAction": "Verify current HU location and retry"
    }
  ]
}
```

### Offline Data Synchronization

**Before Going Offline:**
- Download assigned reservations (HARD locked)
- Download assigned HUs
- Download location layout (for validation)
- Download SKU master data

**While Offline:**
- Queue commands locally
- Validate against local data
- Show "Offline Mode" indicator in UI

**On Reconnect:**
- Sync queued commands in order
- Validate each command on server
- Update local data with server state
- Show reconciliation report


## Integration Architecture

### Integration Layer Separation

The integration module is split into three logical components with different SLAs and failure modes:

#### 1. Operational Integration (<5s SLA)

**Purpose:** Real-time operational integrations for warehouse floor operations.

**Components:**
- Label printing service
- Barcode scanner integration
- Weight scale integration
- RFID reader integration
- Gate/door control

**Failure Mode:**
- Retry 3 times immediately
- If failed, alert operator
- Operator can retry manually or continue without (e.g., manual label)

**Example: Label Printing**
```
HandlingUnitSealed event
  ↓
Label Print Handler
  ↓
Generate ZPL (Zebra Programming Language)
  ↓
Send to Printer (HTTP or TCP)
  ↓
Retry 3x if failed
  ↓
If still failed: Log error, alert operator
```

**Idempotency:**
- Print commands include PrintJobId
- Printer checks if PrintJobId already processed
- Duplicate commands skipped

---

#### 2. Financial Integration (minutes SLA)

**Purpose:** Periodic financial exports to accounting systems.

**Components:**
- Agnum export service
- Financial reconciliation reports
- Cost allocation exports

**Failure Mode:**
- Retry with exponential backoff (3 attempts over 30 minutes)
- If failed, alert administrator
- Manual fallback: Export to CSV, upload manually

**Example: Agnum Export**
```
Scheduled trigger (daily 6 AM) OR manual trigger
  ↓
AgnumExportSaga starts
  ↓
Query StockMovement ledger (balances)
Query Valuation (costs)
Query LogicalWarehouse (categories)
  ↓
Apply mapping rules (warehouse → account)
  ↓
Transform to Agnum format (CSV or JSON)
  ↓
POST to Agnum API with ExportId
  ↓
Retry with backoff: 1min, 5min, 15min
  ↓
If still failed: Alert administrator, queue for manual export
```

**Idempotency:**
- Export includes unique ExportId
- Agnum deduplicates by ExportId
- Safe to retry

**Mapping Configuration:**
```json
{
  "mappings": [
    {
      "sourceType": "PhysicalWarehouse",
      "sourceCode": "Main",
      "agnumAccount": "1500-RAW-MAIN"
    },
    {
      "sourceType": "LogicalWarehouse",
      "sourceCode": "RES",
      "agnumAccount": "1550-RESERVED"
    },
    {
      "sourceType": "LogicalWarehouse",
      "sourceCode": "SCRAP",
      "agnumAccount": "5200-SCRAP"
    }
  ],
  "exportMode": "ByLogicalWarehouse"
}
```

---

#### 3. Process Integration (<30s SLA)

**Purpose:** MES/ERP process coordination for material flow.

**Components:**
- ERP material request handler
- Material consumption notifier
- Production order integration

**Failure Mode:**
- Saga compensation
- Notify both systems of failure
- Maintain eventual consistency via reconciliation

**Example: Material Request Flow**
```
ERP sends MaterialRequested event
  ↓
ERP Integration Saga starts
  ↓
Translate to CreateReservation command
  ↓
Create reservation in warehouse
  ↓
AllocationSaga allocates stock
  ↓
Send MaterialReserved event to ERP
  ↓
If ERP notification fails:
  - Retry with backoff: 5s, 15s, 30s
  - If still failed: Queue for background delivery
  - Maintain eventual consistency
```

**Anti-Corruption Layer:**
```csharp
// ERP → Warehouse translation
public class ERPToWarehouseTranslator
{
    public CreateReservationCommand Translate(MaterialRequestedEvent erpEvent)
    {
        return new CreateReservationCommand
        {
            ReservationId = Guid.NewGuid(),
            Purpose = $"ProductionOrder-{erpEvent.OrderNumber}",
            Priority = MapPriority(erpEvent.Urgency),
            RequestedLines = erpEvent.Materials.Select(m => new ReservationLine
            {
                SKU = MapSKU(m.MaterialCode),
                RequestedQuantity = m.Quantity
            }).ToList()
        };
    }
}

// Warehouse → ERP translation
public class WarehouseToERPTranslator
{
    public MaterialConsumedEvent Translate(StockMovedEvent warehouseEvent)
    {
        if (warehouseEvent.ToLocation != "PRODUCTION")
            return null; // Only translate production picks
            
        return new MaterialConsumedEvent
        {
            OrderNumber = ExtractOrderNumber(warehouseEvent.Reason),
            MaterialCode = MapMaterialCode(warehouseEvent.SKU),
            Quantity = warehouseEvent.Quantity,
            Timestamp = warehouseEvent.Timestamp
        };
    }
}
```

**Idempotency:**
- ERP events include unique event ID
- Warehouse deduplicates by event ID
- MaterialConsumed events include unique event ID
- ERP deduplicates by event ID

---

### Integration Event Catalog

**Outbound Events (Warehouse → External Systems):**

| Event | Target System | Trigger | Delivery Guarantee |
|-------|--------------|---------|-------------------|
| `LabelPrintRequested` | Label Printer | HandlingUnitSealed | At-least-once, idempotent |
| `StockExported` | Agnum | Scheduled/Manual | At-least-once, idempotent |
| `MaterialReserved` | ERP/MES | ReservationAllocated | At-least-once, idempotent |
| `MaterialConsumed` | ERP/MES | StockMoved to PRODUCTION | At-least-once, idempotent |

**Inbound Events (External Systems → Warehouse):**

| Event | Source System | Handler | Idempotency |
|-------|--------------|---------|-------------|
| `MaterialRequested` | ERP/MES | ERP Integration Saga | Event ID deduplication |
| `ProductionOrderCancelled` | ERP/MES | Cancel Reservation | Event ID deduplication |

---

### Integration Monitoring

**Operational Integration Metrics:**
- Label print success rate (target: >99%)
- Label print latency (target: <5s)
- Scanner read success rate (target: >95%)

**Financial Integration Metrics:**
- Agnum export success rate (target: >99%)
- Agnum export latency (target: <5 minutes)
- Export data accuracy (manual spot checks)

**Process Integration Metrics:**
- ERP notification success rate (target: >99%)
- ERP notification latency (target: <30s)
- Material request processing time (target: <2 minutes)

**Alerts:**
- Operational integration failure: Immediate alert to operator
- Financial integration failure: Alert to administrator within 1 hour
- Process integration failure: Alert to both warehouse and production teams

---

## Observability & Monitoring Strategy

### Logging

**Structured Logging Format:**
```json
{
  "timestamp": "2026-02-07T10:30:00Z",
  "level": "INFO",
  "logger": "StockLedger",
  "message": "StockMovement recorded",
  "context": {
    "movementId": "...",
    "sku": "SKU933",
    "quantity": 100,
    "fromLocation": "R3-C6",
    "toLocation": "PRODUCTION",
    "operatorId": "...",
    "handlingUnitId": "..."
  },
  "correlationId": "...",
  "traceId": "..."
}
```

**Log Levels:**
- **TRACE:** Detailed execution flow (disabled in production)
- **DEBUG:** Diagnostic information (enabled for troubleshooting)
- **INFO:** Normal operations (movement recorded, HU created, etc.)
- **WARN:** Recoverable errors (retry succeeded, projection lag)
- **ERROR:** Unrecoverable errors (saga failed, integration failed)
- **FATAL:** System-wide failures (event store unavailable)

**Log Retention:**
- INFO and above: 90 days
- DEBUG: 7 days
- TRACE: Not stored in production

---

### Metrics

**Business Metrics:**
- Stock movements per hour
- Handling units created per day
- Reservations created per day
- Pick operations per hour
- Average pick time
- Goods receipt throughput

**Technical Metrics:**
- Event store write latency (p50, p95, p99)
- Projection lag (per projection)
- Command processing latency (per command type)
- Saga completion time (per saga type)
- Outbox delivery latency
- Database connection pool utilization

**System Health Metrics:**
- CPU utilization
- Memory utilization
- Disk I/O
- Network I/O
- Database query latency

---

### Tracing

**Distributed Tracing:**
- Use OpenTelemetry for tracing
- Trace ID propagated through all operations
- Spans for: command handling, event processing, saga steps, integration calls

**Example Trace:**
```
Trace: PickStock operation
├─ Span: PickStockCommand received
├─ Span: Validate reservation (PICKING state)
├─ Span: Validate HU allocated
├─ Span: StockLedger.RecordMovement
│  ├─ Span: Validate balance
│  ├─ Span: Append to event stream
│  └─ Span: Publish to outbox
├─ Span: Wait for HandlingUnit projection
│  └─ Span: Process StockMoved event
├─ Span: Reservation.Consume
│  ├─ Span: Update consumption
│  └─ Span: Publish ReservationConsumed event
└─ Span: PickStockSaga complete
```

---

### Alerting

**P0 Alerts (Immediate Response Required):**
- Event store unavailable
- Negative balance detected
- Balance mismatch detected
- Event stream sequence gap detected
- Outbox processor failed

**P1 Alerts (Response Within 1 Hour):**
- Projection lag > 30 seconds
- Saga stuck > 5 minutes
- Integration failure rate > 5%

**P2 Alerts (Response Within 24 Hours):**
- Orphaned HU detected
- Consumed reservation still holding HUs
- HU at invalid location

**Alert Channels:**
- P0: PagerDuty + SMS + Email
- P1: Email + Slack
- P2: Email

---

### Dashboards

**Operations Dashboard:**
- Current stock levels by location
- Active reservations
- Pick operations in progress
- Recent movements (last 1 hour)
- Projection lag status
- Integration health

**Technical Dashboard:**
- Event store metrics
- Projection lag by projection
- Saga execution times
- Command processing latency
- Outbox delivery latency
- Database performance

**Business Dashboard:**
- Daily throughput (receipts, transfers, picks)
- Inventory turnover
- Reservation fulfillment rate
- Average pick time
- Stock accuracy (cycle count results)

---

## Performance & Scaling Strategy

### Performance Targets

**Command Processing:**
- RecordStockMovement: <50ms (p95)
- CreateHandlingUnit: <100ms (p95)
- AllocateReservation: <200ms (p95)
- PickStock: <500ms (p95) - includes saga coordination

**Query Performance:**
- LocationBalance query: <10ms (p95)
- AvailableStock query: <20ms (p95)
- HandlingUnit lookup: <5ms (p95)

**Projection Lag:**
- LocationBalance: <1 second (p95)
- AvailableStock: <2 seconds (p95)
- OnHandValue: <5 seconds (p95)

**Throughput:**
- Stock movements: 1000/second
- Handling unit operations: 500/second
- Reservation operations: 100/second

---

### Scaling Strategy

**Horizontal Scaling:**
- Command handlers: Stateless, scale horizontally
- Event processors: Partition by aggregate ID
- Projection handlers: Partition by location or SKU
- Saga coordinators: Partition by saga ID

**Vertical Scaling:**
- Event store: Scale database (read replicas for queries)
- Projection storage: Scale database (read replicas for queries)
- Outbox processor: Scale with more workers

**Partitioning Strategy:**
- Event streams partitioned by aggregate ID
- Projections partitioned by location (for LocationBalance)
- Projections partitioned by SKU (for Valuation)

**Caching Strategy:**
- Cache WarehouseLayout (rarely changes)
- Cache LogicalWarehouse mappings (rarely changes)
- Cache Valuation (changes infrequently)
- Do NOT cache LocationBalance (changes frequently)

---

### Database Optimization

**Indexes:**
- Event streams: Index on sequence_number, timestamp, aggregate_id
- Projections: Index on primary keys, foreign keys, query columns
- Outbox: Index on published_at (for unpublished messages)

**Partitioning:**
- Event streams: Partition by month (for archival)
- Projections: Partition by location or SKU (if very large)

**Archival:**
- Archive old events (>1 year) to cold storage
- Keep projections current (rebuild from archived events if needed)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-07 | Initial design for Phase 1 |

---

**End of Design Document**
