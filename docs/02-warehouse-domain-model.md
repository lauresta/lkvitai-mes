# LKvitai.MES - Warehouse Domain Model v1

**Project:** Modular Manufacturing Automation System (MES/ERP-light)  
**Module:** Warehouse Management  
**Date:** February 2026  
**Version:** 1.0  
**Status:** Canonical Model - Single Source of Truth

---

## Document Purpose

This is the **canonical domain model** for the Warehouse module. It is the single source of truth for:
- Domain structure and boundaries
- Aggregate definitions and responsibilities
- Business rules and invariants
- Data ownership
- Event sourcing strategy

All implementation must conform to this model.

---

## Table of Contents

1. Context Map
2. Ubiquitous Language Glossary
3. Canonical Aggregates
4. Ownership Matrix
5. Domain Invariants
6. Transaction Boundaries
7. Event Sourcing Strategy
8. Domain Event Catalog

---

## 1. Context Map

```
┌─────────────────────────────────────────────────────────────┐
│                    WAREHOUSE CONTEXT                         │
│                  (Core Domain - Inventory)                   │
└─────────────────────────────────────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ↓                 ↓                 ↓
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│   Physical   │  │   Logical    │  │  Financial   │
│   Inventory  │  │  Warehouses  │  │  Valuation   │
└──────────────┘  └──────────────┘  └──────────────┘
        │                 │                 │
        └─────────────────┼─────────────────┘
                          │
            ┌─────────────┴─────────────┐
            ↓                           ↓
    ┌──────────────┐            ┌──────────────┐
    │ INTEGRATION  │            │    LAYOUT    │
    │   LAYER      │            │ CONFIGURATION│
    └──────────────┘            └──────────────┘
            │
    ┌───────┼───────┐
    ↓       ↓       ↓
┌──────┐ ┌────┐ ┌─────┐
│Agnum │ │ERP │ │Label│
└──────┘ └────┘ └─────┘
```

### Upstream/Downstream Relationships

| Upstream Context | Downstream Context | Relationship Type |
|-----------------|-------------------|-------------------|
| **Warehouse** | Agnum (Accounting) | Published Language (export API) |
| **Warehouse** | ERP/MES Core | Published Language (events) |
| **Warehouse** | Label Printing | Open Host Service |
| ERP/MES Core | **Warehouse** | Customer/Supplier (material requests) |

### Context Boundaries

- **Physical Inventory** - owns handling units, locations, movements
- **Logical Warehouses** - owns virtual grouping and categorization
- **Financial Valuation** - owns cost data, independent of quantities
- **Integration Layer** - anti-corruption layer for external systems
- **Layout Configuration** - owns physical warehouse topology

---

## 2. Ubiquitous Language Glossary

### Core Entities

| Term | Definition | Synonyms | Notes |
|------|------------|----------|-------|
| **HandlingUnit (HU)** | Physical container with unique barcode identifier | LPN, Container, Carrier | Can contain multiple SKUs |
| **SKU** | Stock Keeping Unit - unique product identifier | Product Code, Item Code | Identifies what, not where |
| **StockMovement** | Immutable record of stock moving from one location to another | Movement, Transfer Record | Single atomic fact with FROM/TO |
| **Reservation** | Claim on future stock consumption | Allocation, Lock | Can be SOFT or HARD |
| **Location** | Physical or virtual address in warehouse | Bin, Position, Zone | Physical: "R3-C6-L3B3", Virtual: "PRODUCTION" |

### Handling Unit Terminology

| Term | Definition | Example |
|------|------------|---------|
| **Pallet** | Large handling unit type | Europallet 1200x800mm |
| **Box** | Medium handling unit type | Cardboard box |
| **Bag** | Small handling unit type | 25kg cement bag |
| **Unit** | Individual item (no container) | Single piece |
| **LPN (License Plate Number)** | Barcode/label on handling unit | "HU-001234" |
| **HandlingUnitLine** | Content record within HU | SKU + Quantity |
| **Sealed** | HU status preventing modifications | Ready for movement/shipment |

### Movement Terminology

| Term | Definition | From Location | To Location |
|------|------------|--------------|-------------|
| **Receipt** | Goods arriving from supplier | EXTERNAL_SUPPLIER | Warehouse bin |
| **Transfer** | Movement between bins | Bin A | Bin B |
| **Pick** | Withdrawal for consumption | Warehouse bin | PRODUCTION / SHIPPED |
| **Scrap** | Disposal of damaged goods | Warehouse bin | SCRAP_ZONE |
| **Adjustment** | Inventory correction | SYSTEM | Warehouse (or reverse) |
| **Return** | Goods coming back | CUSTOMER | Warehouse bin |

### Reservation Terminology

| Term | Definition | Business Rule |
|------|------------|--------------|
| **Soft Reservation** | Advisory lock - can be overridden | Used during planning phase |
| **Hard Reservation** | Exclusive lock - cannot be touched | Used during execution phase |
| **Allocation** | Assignment of HUs to reservation | Links reservation to physical stock |
| **Bumped** | Reservation overridden by higher priority | Triggers notification |
| **Consumed** | Reservation fully picked | Terminal state |

### Location Terminology

| Term | Definition | Type |
|------|------------|------|
| **Physical Location** | Real warehouse position | "Main/R3-C6-L3B3" |
| **Virtual Location** | Conceptual endpoint | "PRODUCTION", "SUPPLIER" |
| **Aisle** | Row in warehouse | "R3" |
| **Rack** | Storage structure | "C6" |
| **Bin** | Specific position on rack | "L3B3" (Level 3, Bin 3) |

### Warehouse Terminology

| Term | Definition | Examples |
|------|------------|----------|
| **Physical Warehouse** | Actual building/zone | Main, Aux, Cold |
| **Logical Warehouse** | Virtual grouping for reporting | RES, PROD, NLQ, SCRAP |
| **Category** | Product classification | Textile, Green, Hardware |
| **Multi-categorization** | SKU in multiple categories | Allowed by design |

### Status Terminology

| Term | Definition | Applies To |
|------|------------|-----------|
| **FULL** | Stock level is adequate | Location / HU |
| **LOW** | Stock below threshold | Location / HU |
| **EMPTY** | No stock remaining | Location / HU |
| **RESERVED** | Stock allocated but not picked | HU |
| **OPEN** | Can be modified | HU |
| **SEALED** | Cannot be modified | HU |
| **PICKED** | Partially consumed | HU |

### Financial Terminology

| Term | Definition | Notes |
|------|------------|-------|
| **Valuation** | Financial interpretation of stock | Independent from physical qty |
| **Unit Cost** | Average or FIFO cost per unit | Can be adjusted |
| **On-Hand Value** | Physical qty × unit cost | Computed |
| **Revaluation** | Adjustment of unit cost | Requires reason + approver |
| **Landed Cost** | Total cost including freight/duties | Allocated to inventory |
| **Write-down** | Reduction in value (damage/obsolescence) | Financial adjustment |

---

## 3. Canonical Aggregates

### Aggregate 1: HandlingUnit (Aggregate Root)

**Purpose:** Represents a physical container (pallet, box, bag, unit) with barcode tracking.

**Aggregate Root:** `HandlingUnit`

**Identity:** `HandlingUnitId` (LPN / barcode)

**Contained Entities:**
- `HandlingUnitLine` (SKU, Quantity) - one or more per HU

**Value Objects:**
- `HandlingUnitType` (PALLET, BOX, BAG, UNIT)
- `HandlingUnitStatus` (OPEN, SEALED, PICKED, EMPTY)
- `Location` (warehouse, aisle, rack, bin)
- `Timestamp` (created, sealed, last moved)

**Responsibilities:**
- Track physical container through warehouse
- Maintain contents (multiple SKUs possible)
- Enforce sealing rules (cannot modify after seal)
- Emit movement events when location changes

**Key Operations:**
- `AddLine(SKU, Quantity)` - add content to HU
- `RemoveLine(SKU, Quantity)` - remove content (pick)
- `Seal()` - lock contents for movement
- `MoveTo(Location)` - change location (triggers StockMovement events)
- `Split(Quantity)` - create new HU from existing
- `Merge(HandlingUnit)` - combine two HUs

**Invariants:**
1. ✅ Cannot modify `SEALED` HU
2. ✅ Cannot seal empty HU
3. ✅ Quantity in line cannot go negative
4. ✅ HU has exactly one location at any time
5. ✅ Moving HU generates StockMovement for each line

**State Transitions:**
```
OPEN ──────────> SEALED ──────────> PICKED ──────────> EMPTY
  │                                    │
  │                                    ↓
  └─────────────────────────────> EMPTY (if emptied while open)
```

---

### Aggregate 2: StockMovement (Aggregate Root - Event Sourced)

**Purpose:** Immutable record of stock moving from one location to another. This is the **source of truth** for all quantity changes.

**Aggregate Root:** `StockMovement`

**Identity:** `MovementId`

**Contained Entities:** None (value-based aggregate)

**Value Objects:**
- `SKU` (what moved)
- `Quantity` (how much)
- `Location` (from/to)
- `MovementType` (RECEIPT, TRANSFER, PICK, SCRAP, ADJUSTMENT, RETURN)
- `Timestamp`
- `OperatorId`
- `HandlingUnitId` (optional - for traceability)
- `Reason` (optional - why)

**Responsibilities:**
- Record every stock movement as immutable fact
- Enforce "from" location has sufficient stock
- Provide basis for location balance projections
- Enable audit trail and time-travel queries

**Key Operations:**
- `Record(from, to, SKU, qty, type, reason)` - append movement to ledger

**Invariants:**
1. ✅ Movement is **immutable** (append-only)
2. ✅ From and To locations cannot be the same
3. ✅ Quantity must be positive
4. ✅ From location must have sufficient stock (validated before append)
5. ✅ Virtual locations allowed (SUPPLIER, PRODUCTION, SCRAP, SYSTEM)

**Storage:** Event Sourced (append-only log)

**Critical Design Decision:**
- This is **write model** - source of truth for "what happened"
- Current location balances are **read models** projected from movements
- Transfer is **ONE movement** (not two), with FROM/TO in single record

---

### Aggregate 3: Reservation (Aggregate Root - Event Sourced)

**Purpose:** Represents a claim on future stock consumption with hybrid locking strategy.

**Aggregate Root:** `Reservation`

**Identity:** `ReservationId`

**Contained Entities:**
- `ReservationLine` (SKU, requested qty, allocated HUs)

**Value Objects:**
- `ReservationPurpose` (ProductionOrder, SalesOrder, etc.)
- `ReservationStatus` (PENDING, ALLOCATED, PICKING, CONSUMED, CANCELLED, BUMPED)
- `ReservationLockType` (SOFT, HARD)
- `Priority` (1-10, where 10 = highest)
- `ExpirationDate`

**Responsibilities:**
- Allocate handling units to specific orders
- Manage soft → hard lock transitions
- Prevent conflicts (soft) or block access (hard)
- Track consumption

**Key Operations:**
- `Allocate(List<HandlingUnitId>)` - assign HUs (SOFT lock)
- `StartPicking()` - transition to HARD lock
- `Consume(Quantity)` - mark picked
- `Cancel()` - release allocation
- `Bump()` - override by higher priority

**Invariants:**
1. ✅ Allocated quantity ≤ requested quantity
2. ✅ SOFT lock can be bumped by higher priority or HARD lock
3. ✅ HARD lock is exclusive (cannot be overridden)
4. ✅ Consumed reservation is immutable
5. ✅ StartPicking() checks for conflicts again (may fail if now hard-locked)
6. ✅ Priority determines conflict resolution

**State Machine:**
```
PENDING ──Allocate()──> ALLOCATED (SOFT) ──StartPicking()──> PICKING (HARD) ──Complete()──> CONSUMED
   │                         │                                      
   │                         │                                      
   └──Cancel()──────────────┴──Cancel()────────────────────────────> CANCELLED
                             │
                             └──Bumped()──────────────────────────> BUMPED
```

**Locking Strategy:**
- **SOFT:** Advisory lock during planning phase - can be reallocated
- **HARD:** Exclusive lock during execution - cannot be touched
- **Transition:** SOFT → HARD when `StartPicking()` called

---

### Aggregate 4: Valuation (Aggregate Root - Event Sourced)

**Purpose:** Financial interpretation of stock, completely decoupled from physical quantities.

**Aggregate Root:** `Valuation`

**Identity:** `ValuationId` (typically per SKU)

**Contained Entities:**
- `CostAdjustment` (timestamp, amount, reason, approver)

**Value Objects:**
- `SKU`
- `UnitCost` (current average or FIFO cost)
- `OnHandValue` (computed: physical qty × unit cost)
- `AdjustmentReason` (DAMAGE, OBSOLESCENCE, LANDED_COST, MANUAL, WRITE_DOWN)

**Responsibilities:**
- Calculate and maintain unit cost
- Apply cost adjustments independently of quantity changes
- Provide on-hand value for reporting
- Maintain immutable history of cost changes

**Key Operations:**
- `ApplyAdjustment(newCost, reason, approver)` - change unit cost
- `AllocateLandedCost(amount, reason)` - add freight/duties
- `WriteDown(percentage, reason, approver)` - reduce value
- `GetOnHandValue(quantity)` - compute value

**Invariants:**
1. ✅ Unit cost can be adjusted without changing physical quantity
2. ✅ Adjustments require reason and approver
3. ✅ Historical cost changes are immutable
4. ✅ On-hand value is computed (qty from StockMovement × cost from Valuation)
5. ✅ Financial quantity can temporarily diverge from physical during revaluation

**Critical Separation:**
- Valuation owns **cost** (interpretation)
- StockMovement owns **quantity** (fact)
- These are completely independent aggregates

---

### Aggregate 5: WarehouseLayout (Aggregate Root)

**Purpose:** Configuration defining physical topology of warehouses.

**Aggregate Root:** `WarehouseLayout`

**Identity:** `WarehouseId`

**Contained Entities:**
- `Aisle` (code, location)
- `Rack` (code, aisle, dimensions)
- `Bin` (code, rack, 3D coordinates, capacity)

**Value Objects:**
- `Coordinates3D` (x, y, z in warehouse space)
- `BinCapacity` (max pallets, weight limit, volume)
- `ZoneType` (COLD, AMBIENT, HAZMAT, etc.)

**Responsibilities:**
- Define physical warehouse structure
- Validate location assignments (bin exists)
- Provide 3D visualization data
- Enforce capacity constraints

**Key Operations:**
- `DefineBin(aisle, rack, bin, coordinates, capacity)`
- `ModifyBin(bin, newCapacity)` - change capacity
- `GetBinByCode(code)` - lookup
- `ValidateLocation(location)` - check exists

**Invariants:**
1. ✅ Bin coordinates must be unique within warehouse
2. ✅ Bins cannot overlap in 3D space
3. ✅ Capacity constraints are enforced when placing HUs
4. ✅ Cannot delete bin if it contains stock

**State:** Traditional state-based (CRUD)

**Note:** Layout changes are infrequent; does not require event sourcing.

---

### Aggregate 6: LogicalWarehouse (Aggregate Root)

**Purpose:** Virtual grouping of stock for categorization and reporting.

**Aggregate Root:** `LogicalWarehouse`

**Identity:** `LogicalWarehouseId`

**Contained Entities:**
- `CategoryAssignment` (SKU → category mapping)

**Value Objects:**
- `CategoryName` (TEXTILE, GREEN, HARDWARE, etc.)
- `WarehouseCode` (RES, PROD, NLQ, SCRAP)
- `AllowMultiCategory` (boolean - always true)

**Responsibilities:**
- Group stock for Agnum export
- Support multi-categorization (SKU in multiple categories)
- Provide metadata for reporting

**Key Operations:**
- `AssignCategory(SKU, category)` - add category
- `RemoveCategory(SKU, category)` - remove category
- `GetCategories(SKU)` - list all categories for SKU

**Invariants:**
1. ✅ SKU can belong to multiple logical warehouses simultaneously
2. ✅ Category assignments are metadata only (do not affect physical reality)
3. ✅ Changing logical warehouse does NOT trigger physical movement
4. ✅ Same SKU can be in both RES and PROD logical warehouses

**State:** Traditional state-based (CRUD)

**Integration:** Used by Agnum export to map physical stock to financial accounts.

---

## 4. Ownership Matrix

### Data Ownership Table

| Data Element | Owner Aggregate | Reason | Access Pattern |
|--------------|----------------|--------|----------------|
| **Physical Quantity** | `StockMovement` | Movements are source of truth | Write: append movement; Read: project balance |
| **Physical Location (current)** | `HandlingUnit` | HU owns its position | Direct read from HU |
| **Physical Location (history)** | `StockMovement` | Audit trail in movements | Time-travel query on movements |
| **Handling Unit Contents** | `HandlingUnit` | HU owns what's inside it | Direct read from HU.Lines |
| **Reservation Claims** | `Reservation` | Reservation owns allocation | Direct read from Reservation |
| **Unit Cost** | `Valuation` | Financial interpretation | Direct read from Valuation |
| **On-Hand Value** | Computed | Qty (from StockMovement) × Cost (from Valuation) | Query both aggregates |
| **Logical Categories** | `LogicalWarehouse` | Metadata for grouping | Direct read from LogicalWH |
| **Physical Topology** | `WarehouseLayout` | Configuration data | Direct read from Layout |
| **Audit Trail** | `StockMovement` | Event sourced ledger | Query movement stream |

### Truth Assignment

| Question | Source of Truth | Implementation |
|----------|----------------|----------------|
| "How much of SKU X is at location Y?" | `StockMovement` ledger | Query: sum movements where to=Y minus movements where from=Y |
| "What's inside handling unit HU-001?" | `HandlingUnit` aggregate | Direct read: HU.Lines |
| "Where is HU-001 right now?" | `HandlingUnit` aggregate | Direct read: HU.CurrentLocation |
| "Where was HU-001 on 2025-09-01?" | `StockMovement` ledger | Time-travel query on movements linked to HU-001 |
| "Is this stock reserved?" | `Reservation` aggregate | Query: find active reservations containing HU |
| "What does SKU X cost?" | `Valuation` aggregate | Direct read: Valuation.UnitCost |
| "Which categories does SKU X belong to?" | `LogicalWarehouse` aggregate | Direct read: LogicalWH.GetCategories(SKU) |
| "Does bin R3-C6 exist?" | `WarehouseLayout` aggregate | Direct read: Layout.GetBin("R3-C6") |

### Write Responsibilities

| Operation | Primary Aggregate | Secondary Effects |
|-----------|------------------|-------------------|
| Receive goods | `HandlingUnit` (create), `StockMovement` (append) | None |
| Transfer between bins | `HandlingUnit` (update location), `StockMovement` (append) | None |
| Pick for production | `HandlingUnit` (remove line), `StockMovement` (append) | Reservation (consume) |
| Reserve stock | `Reservation` (create/allocate) | None (soft lock) |
| Start picking | `Reservation` (start picking) | None (transitions to hard lock) |
| Revalue stock | `Valuation` (apply adjustment) | None (independent of qty) |
| Categorize SKU | `LogicalWarehouse` (assign) | None |
| Define bin | `WarehouseLayout` (create bin) | None |

---

## 5. Domain Invariants

### Global Invariants (Cross-Aggregate)

| Rule | Enforced By | Validation Point |
|------|-------------|------------------|
| **No negative stock** | StockMovement ledger | Before appending movement, validate from-location balance ≥ quantity |
| **HU location uniqueness** | HandlingUnit | HU.MoveTo() enforces single location |
| **Sealed HU immutability** | HandlingUnit | AddLine/RemoveLine check status != SEALED |
| **Hard lock exclusivity** | Reservation + Query Service | StartPicking() re-validates no conflicts |
| **Cost independence** | Separation of Valuation from StockMovement | No direct references between aggregates |

### Aggregate-Specific Invariants

#### HandlingUnit Invariants

| Invariant | Rule | Enforcement |
|-----------|------|-------------|
| **INVA-HU-01** | Cannot modify SEALED HU | `AddLine()`, `RemoveLine()` throw if status == SEALED |
| **INVA-HU-02** | Cannot seal empty HU | `Seal()` throws if Lines.Count == 0 |
| **INVA-HU-03** | Line quantity ≥ 0 | `RemoveLine()` throws if result would be negative |
| **INVA-HU-04** | Exactly one location | `MoveTo()` replaces current location atomically |
| **INVA-HU-05** | Moving HU emits movement per line | `MoveTo()` generates N StockMovement events for N lines |

#### StockMovement Invariants

| Invariant | Rule | Enforcement |
|-----------|------|-------------|
| **INVA-SM-01** | Movements are immutable | Append-only; no update or delete operations |
| **INVA-SM-02** | From ≠ To | Constructor validates from != to |
| **INVA-SM-03** | Quantity > 0 | Constructor validates qty > 0 |
| **INVA-SM-04** | Sufficient stock at source | Ledger validates balance before append |
| **INVA-SM-05** | Virtual locations allowed | SUPPLIER, PRODUCTION, etc. are valid |

#### Reservation Invariants

| Invariant | Rule | Enforcement |
|-----------|------|-------------|
| **INVA-RES-01** | Allocated ≤ Requested | `Allocate()` validates quantity |
| **INVA-RES-02** | SOFT can be bumped | `Allocate()` emits ReservationBumped event |
| **INVA-RES-03** | HARD is exclusive | `StartPicking()` throws if conflicting HARD exists |
| **INVA-RES-04** | Consumed is immutable | State machine prevents transitions from CONSUMED |
| **INVA-RES-05** | StartPicking re-validates | Checks for conflicts again before transition |

#### Valuation Invariants

| Invariant | Rule | Enforcement |
|-----------|------|-------------|
| **INVA-VAL-01** | Adjustments require reason | `ApplyAdjustment()` validates reason != null |
| **INVA-VAL-02** | Adjustments require approver | `ApplyAdjustment()` validates approver != null |
| **INVA-VAL-03** | Historical adjustments immutable | Append-only list of CostAdjustment |
| **INVA-VAL-04** | Cost independent of quantity | No references to StockMovement |

#### WarehouseLayout Invariants

| Invariant | Rule | Enforcement |
|-----------|------|-------------|
| **INVA-LAY-01** | Bin coordinates unique | `DefineBin()` checks uniqueness |
| **INVA-LAY-02** | No overlapping bins | 3D space validation |
| **INVA-LAY-03** | Cannot delete occupied bin | Validate no HUs at location before delete |
| **INVA-LAY-04** | Capacity constraints enforced | Validate HU placement against bin capacity |

#### LogicalWarehouse Invariants

| Invariant | Rule | Enforcement |
|-----------|------|-------------|
| **INVA-LOG-01** | Multi-category allowed | AssignCategory allows duplicates |
| **INVA-LOG-02** | Metadata only | No impact on physical movements |
| **INVA-LOG-03** | Same SKU in multiple logical WHs | Explicitly allowed |

---

## 6. Transaction Boundaries

### Strongly Consistent Operations (ACID)

| Operation | Aggregate(s) | Transaction Scope |
|-----------|-------------|-------------------|
| Create handling unit | `HandlingUnit` | Single aggregate create |
| Add line to HU | `HandlingUnit` | Single aggregate update |
| Remove line from HU | `HandlingUnit` | Single aggregate update |
| Seal HU | `HandlingUnit` | Single aggregate state change |
| Record movement | `StockMovement` | Append to event stream (atomic) |
| Create reservation | `Reservation` | Single aggregate create |
| Allocate reservation | `Reservation` | Single aggregate update |
| Start picking | `Reservation` | Single aggregate state transition |
| Apply cost adjustment | `Valuation` | Single aggregate update |
| Define bin | `WarehouseLayout` | Single aggregate update |
| Assign category | `LogicalWarehouse` | Single aggregate update |

### Eventually Consistent Operations

| Operation | Pattern | Reason | Compensation |
|-----------|---------|--------|--------------|
| **Receive goods** | Process Manager | Creates HU + appends StockMovement | Rollback HU creation if movement fails |
| **Move HU with multiple SKUs** | Process Manager | Updates HU location + appends N StockMovements | Revert HU location if any movement fails |
| **Pick against reservation** | Process Manager | Updates HU + appends StockMovement + updates Reservation | Rollback all or none |
| **Allocate reservation to HUs** | Process Manager | Queries available HUs + updates Reservation | Release allocation if HUs become unavailable |
| **Export to Agnum** | Integration Saga | Queries StockMovement + Valuation + LogicalWH + calls API | Retry on failure; idempotent |
| **Split HU** | Process Manager | Creates new HU + removes from old HU + appends 2 StockMovements | Rollback new HU if movements fail |
| **Merge HUs** | Process Manager | Updates target HU + deletes source HU + appends StockMovements | Restore source HU if merge fails |

### Consistency Model

```
WRITE MODEL (Commands):
- HandlingUnit (state-based aggregate)
- StockMovement (event-sourced, append-only)
- Reservation (event-sourced)
- Valuation (event-sourced)
- WarehouseLayout (state-based)
- LogicalWarehouse (state-based)

READ MODEL (Queries):
- LocationBalance (projected from StockMovement)
- AvailableStock (LocationBalance - ReservedQuantity)
- HandlingUnitLocation (direct from HandlingUnit)
- OnHandValue (LocationBalance × UnitCost)
- StockByCategory (join LocationBalance + LogicalWH)
- 3D WarehouseView (join HandlingUnit + WarehouseLayout)
```

**Critical Principle:**
- **Write model** = source of truth (aggregates)
- **Read model** = projections for queries (optimized for UI)
- Read models can be rebuilt from write model events

---

## 7. Event Sourcing Strategy

### Event Sourced Aggregates

#### StockMovement (Mandatory Event Sourcing)

**Why:** 
- Regulatory compliance (audit trail)
- Time-travel queries ("stock at 2025-09-01")
- Immutable record of "what happened"

**Event Stream:**
- `StockMoved` (SKU, from, to, qty, timestamp, operator, HU)

**Projections:**
- Current location balances (per location, per SKU)
- Historical balances (time-travel queries)
- Movement audit trail

**Snapshot Strategy:**
- Snapshot location balances daily
- Replay from snapshot for queries

---

#### Reservation (Event Sourcing)

**Why:**
- Complex lifecycle needs audit
- Conflict resolution requires history
- Compliance (who reserved what when)

**Event Stream:**
- `ReservationCreated` (id, purpose, priority, requested qty)
- `StockAllocated` (id, HU list, lock type)
- `PickingStarted` (id, lock type changed to HARD)
- `ReservationConsumed` (id, actual qty)
- `ReservationCancelled` (id, reason)
- `ReservationBumped` (id, bumping reservation id)
- `ReservationExpired` (id, expiration date)

**Projections:**
- Active reservations (status != CONSUMED/CANCELLED)
- Reserved quantity per HU
- Reservation history per order

**Snapshot Strategy:**
- Snapshot current state per reservation
- Full replay only for audit queries

---

#### Valuation (Event Sourcing)

**Why:**
- Financial regulations require immutable cost history
- Audit trail of adjustments
- Recompute on-hand value at any point in time

**Event Stream:**
- `CostAdjusted` (SKU, new cost, reason, approver)
- `LandedCostAllocated` (SKU, amount, reason)
- `StockWrittenDown` (SKU, percentage, reason)

**Projections:**
- Current unit cost per SKU
- Historical cost changes
- On-hand value trend

**Snapshot Strategy:**
- Snapshot current cost per SKU
- Full replay for historical cost queries

---

### State-Based Aggregates

#### HandlingUnit (State-Based)

**Why:**
- Frequently updated (location changes, line additions)
- Current state is what matters (not history)
- History available from StockMovement events

**Storage:** Traditional relational tables
- `HandlingUnits` table (id, type, status, location, created, sealed)
- `HandlingUnitLines` table (HU id, SKU, qty)

**Note:** Movement history comes from StockMovement stream, not HU itself.

---

#### WarehouseLayout (State-Based)

**Why:**
- Configuration data (changes infrequently)
- Current state is sufficient
- History not critical

**Storage:** Traditional relational tables
- `Warehouses` table
- `Aisles` table
- `Racks` table
- `Bins` table (with 3D coordinates)

---

#### LogicalWarehouse (State-Based)

**Why:**
- Simple metadata (category assignments)
- Current state is sufficient
- History not critical

**Storage:** Traditional relational tables
- `LogicalWarehouses` table
- `CategoryAssignments` table (SKU, category, logical WH)

---

### Event Sourcing Implementation Notes

**Event Store Technology:**
- EventStoreDB (recommended)
- PostgreSQL with append-only table (alternative)
- Azure Event Hubs (cloud alternative)

**Projection Rebuilding:**
- Can rebuild read models from event stream
- Critical for disaster recovery
- Used for testing (replay production events in test env)

**Event Versioning:**
- Use event version numbers
- Upcasters for schema evolution
- Never delete old event types

---

## 8. Domain Event Catalog

### HandlingUnit Events

| Event | Trigger | Payload | Consumers |
|-------|---------|---------|-----------|
| `HandlingUnitCreated` | HU created | HU id, type, location | 3D visualization, reporting |
| `LineAddedToHandlingUnit` | Content added | HU id, SKU, qty | Inventory projection |
| `LineRemovedFromHandlingUnit` | Content picked | HU id, SKU, qty | Inventory projection |
| `HandlingUnitSealed` | HU sealed | HU id, sealed timestamp | Workflow manager |
| `HandlingUnitMoved` | Location changed | HU id, from, to | 3D visualization, audit |
| `HandlingUnitSplit` | HU divided | Source HU, new HU, lines | Inventory projection |
| `HandlingUnitMerged` | HUs combined | Source HUs, target HU | Inventory projection |
| `HandlingUnitEmptied` | Last line removed | HU id | Cleanup process |

### StockMovement Events

| Event | Trigger | Payload | Consumers |
|-------|---------|---------|-----------|
| `StockMoved` | Movement recorded | Movement id, SKU, from, to, qty, type, HU, operator, timestamp, reason | **ALL** - this is the core event |

**Consumers of StockMoved:**
- Location balance projection (updates current stock levels)
- Audit trail view
- Low stock alert generator
- Agnum export service (aggregates for accounting)
- ERP/MES material consumption tracking
- Inventory reports

### Reservation Events

| Event | Trigger | Payload | Consumers |
|-------|---------|---------|-----------|
| `ReservationCreated` | Reservation created | Reservation id, purpose, priority, requested qty | Allocation process manager |
| `StockAllocated` | HUs assigned | Reservation id, HU list, lock type (SOFT) | Available stock projection |
| `PickingStarted` | Transition to HARD lock | Reservation id, lock type (HARD) | Available stock projection, conflict detector |
| `ReservationConsumed` | Fully picked | Reservation id, actual qty | Production tracking, cleanup |
| `ReservationCancelled` | Cancelled | Reservation id, reason | Available stock projection, notification |
| `ReservationBumped` | Overridden | Bumped reservation id, bumping reservation id, HUs | Notification service, escalation workflow |
| `ReservationExpired` | Timeout reached | Reservation id, expiration date | Cleanup process, notification |

### Valuation Events

| Event | Trigger | Payload | Consumers |
|-------|---------|---------|-----------|
| `CostAdjusted` | Manual adjustment | SKU, old cost, new cost, reason, approver | On-hand value projection, financial reporting |
| `LandedCostAllocated` | Freight/duties added | SKU, amount, reason | On-hand value projection, COGS calculation |
| `StockWrittenDown` | Value reduced | SKU, percentage, reason, approver | On-hand value projection, financial reporting |

### WarehouseLayout Events

| Event | Trigger | Payload | Consumers |
|-------|---------|---------|-----------|
| `BinDefined` | New bin created | Warehouse, bin code, coordinates, capacity | 3D visualization |
| `BinModified` | Capacity changed | Bin code, new capacity | 3D visualization, capacity monitor |
| `BinRemoved` | Bin deleted | Bin code | 3D visualization |
| `LayoutModified` | Structure changed | Warehouse, change type | 3D visualization rebuild |

### LogicalWarehouse Events

| Event | Trigger | Payload | Consumers |
|-------|---------|---------|-----------|
| `CategoryAssigned` | SKU categorized | SKU, category, logical WH | Agnum export mapping |
| `CategoryRemoved` | Category unassigned | SKU, category | Agnum export mapping |

---

## Appendix: Design Decisions and Rationale

### Why StockMovement is Event Sourced but HandlingUnit is Not

**StockMovement:**
- Regulatory requirement for audit trail
- Time-travel queries essential ("what was stock 6 months ago")
- Movements are **facts** - never change once recorded
- Source of truth for quantities

**HandlingUnit:**
- Current state matters most (where is it now, what's in it)
- Movement history already captured in StockMovement
- Frequent state changes (adding/removing lines)
- Easier to model as state-based with change events

### Why Reservation Uses Hybrid Lock Strategy

**Alternative 1: Hard Lock Only**
- Problem: Blocks stock too early (during planning)
- Problem: Prevents urgent orders from accessing stock
- Inflexible

**Alternative 2: Soft Lock Only**
- Problem: No guarantees during execution
- Problem: Operator starts picking, stock gets reallocated
- Dangerous for production

**Hybrid (SOFT → HARD):**
- Flexible during planning phase
- Safe during execution phase
- Matches real warehouse operations

### Why Valuation is Separate from StockMovement

**Coupled Model (Anti-pattern):**
```
StockMovement {
  quantity
  unitCost  // ❌ WRONG - ties financial to physical
}
```

**Problems:**
- Cannot revalue stock without creating fake movements
- Financial adjustments corrupt quantity audit trail
- Cannot have different costs for same SKU at different times

**Separated Model (Correct):**
```
StockMovement { quantity } // FACT
Valuation { unitCost }     // INTERPRETATION
OnHandValue = query(StockMovement) × query(Valuation) // COMPUTED
```

**Benefits:**
- Financial flexibility (write-downs, landed costs)
- Physical integrity maintained
- Clean separation of concerns

### Why Transfers are Single Movement Facts

**Wrong Approach (Two Ledger Entries):**
```
Entry 1: Location A, -qty
Entry 2: Location B, +qty
```

**Problems:**
- Stock "in limbo" between entries
- System crash = inventory corruption
- Impossible to make atomic

**Correct Approach (Single Movement):**
```
Movement: FROM A, TO B, qty
```

**Benefits:**
- Atomic fact
- No intermediate state
- Crash-safe
- Clear audit trail

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-06 | Initial canonical model |

---

**End of Document**

This is the canonical model. All implementation must conform to this specification.
