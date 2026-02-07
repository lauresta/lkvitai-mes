# Feature Decomposition

**Project:** LKvitai.MES Warehouse Management System  
**Document:** Feature Decomposition  
**Version:** 1.0  
**Date:** February 2026  
**Status:** Implementation Specification

---

## Document Purpose

This document decomposes the warehouse system into discrete feature groups, each with clear boundaries, dependencies, and acceptance criteria. Each feature group represents a cohesive unit of functionality that can be developed, tested, and deployed incrementally.

**Source Documents:**
- docs/01-discovery.md (Use Cases & Capabilities)
- docs/02-warehouse-domain-model-v1.md (Domain Model)
- docs/03-implementation-guide.md (Implementation Patterns)
- docs/04-system-architecture.md (Architecture Decisions)

---

## Feature Group Overview

```
Foundation Layer:
├─ FG-01: Movement Ledger (Event Store)
├─ FG-02: Warehouse Layout & Configuration
└─ FG-03: Handling Units (Physical Containers)

Business Logic Layer:
├─ FG-04: Reservation Engine
├─ FG-05: Valuation Engine
└─ FG-06: Logical Warehouses & Categories

Query Layer:
├─ FG-07: Read Models & Projections
└─ FG-08: 3D Warehouse Visualization

Operations Layer:
├─ FG-09: Inbound Operations (Receive Goods)
├─ FG-10: Movement Operations (Transfer, Pick)
└─ FG-11: Inventory Adjustments & Cycle Counting

Integration Layer:
├─ FG-12: Operational Integration (Labels, Scanners)
├─ FG-13: Financial Integration (Agnum Export)
└─ FG-14: Process Integration (MES/ERP)

Infrastructure Layer:
├─ FG-15: Process Managers & Sagas
├─ FG-16: Edge/Offline Agent
├─ FG-17: Observability & Audit
└─ FG-18: Security & Permissions

---

## FG-01: Movement Ledger (Event Store)

### Purpose

Provide the single source of truth for all stock movements. The Movement Ledger is an append-only event store that records every physical inventory change as an immutable fact.

### Technical Scope

**Core Responsibilities:**
- Record all StockMoved events (receipts, transfers, picks, adjustments)
- Validate balance constraints (no negative stock)
- Provide event stream for projections
- Support time-travel queries ("stock at date X")
- Enforce movement ownership (ONLY this module writes movements)

**Event Schema:**
```
StockMoved {
  movementId: GUID (unique)
  timestamp: DateTime
  sku: String
  quantity: Decimal (positive)
  fromLocation: String (physical or virtual)
  toLocation: String (physical or virtual)
  movementType: RECEIPT | TRANSFER | PICK | SCRAP | ADJUSTMENT | RETURN
  operatorId: GUID
  handlingUnitId: GUID (optional)
  reason: String (optional)
  sequenceNumber: Int64 (auto-increment)
}
```

**Storage:**
- Event-sourced (append-only table: `stock_movement_events`)
- Indexed by: sequenceNumber, timestamp, sku, fromLocation, toLocation, handlingUnitId
- Retention: Permanent (regulatory requirement)

### Dependencies

**Upstream:** None (foundation layer)

**Downstream:**
- HandlingUnit (subscribes to StockMoved for projection)
- Reservation (queries balance before allocation)
- Valuation (queries quantity for on-hand value)
- Read Models (project current balances)

### Acceptance Criteria

**AC-01.1:** Record Movement
- GIVEN a valid movement command (sku, qty, from, to)
- WHEN from-location has sufficient balance (or from is virtual)
- THEN append StockMoved event to stream
- AND publish event to subscribers

**AC-01.2:** Prevent Negative Balance
- GIVEN a movement command with from-location = physical bin
- WHEN current balance at from-location < requested quantity
- THEN reject command with error "Insufficient balance"
- AND do NOT append event

**AC-01.3:** Support Virtual Locations
- GIVEN a movement with from-location = SUPPLIER (virtual)
- WHEN recording receipt
- THEN allow movement without balance check
- AND append event successfully

**AC-01.4:** Time-Travel Query
- GIVEN a date/time in the past
- WHEN querying balance at location for SKU
- THEN compute balance by replaying events up to that timestamp
- AND return accurate historical balance

**AC-01.5:** Event Ordering Guarantee
- GIVEN multiple movements for same handling unit
- WHEN events published
- THEN events delivered to subscribers in sequence order
- AND no out-of-order processing within same HU

### Non-Functional Requirements

**NFR-01.1 Performance:**
- Append latency: < 50ms (p95)
- Query latency (current balance): < 100ms (p95)
- Query latency (time-travel): < 2 seconds (p95)
- Throughput: 1000 movements/second sustained

**NFR-01.2 Durability:**
- Write-ahead log (WAL) enabled
- Synchronous commit (fsync)
- Zero data loss on crash

**NFR-01.3 Scalability:**
- Support 10M+ events in stream
- Efficient querying via indexes
- Snapshot projections for performance

**NFR-01.4 Auditability:**
- Every event immutable (no updates/deletes)
- Operator ID captured for all movements
- Reason field for manual adjustments

---

## FG-02: Warehouse Layout & Configuration

### Purpose

Define the physical topology of warehouses (buildings, aisles, racks, bins) and provide spatial data for 3D visualization and location validation.

### Technical Scope

**Core Responsibilities:**
- Define warehouse structures (warehouses, aisles, racks, bins)
- Store 3D coordinates for each bin
- Validate location references (bin exists?)
- Provide capacity constraints
- Support layout modifications

**Aggregate:** WarehouseLayout (state-based)

**Entities:**
- Warehouse (id, name, type: MAIN/AUX/COLD)
- Aisle (code, warehouse, location)
- Rack (code, aisle, dimensions)
- Bin (code, rack, coordinates3D, capacity)

**Storage:**
- Relational tables: warehouses, aisles, racks, bins
- Indexed by: bin_code, warehouse_id

### Dependencies

**Upstream:** None (configuration data)

**Downstream:**
- HandlingUnit (validates location on create/move)
- 3D Visualization (reads coordinates)
- Movement Ledger (validates from/to locations)

### Acceptance Criteria

**AC-02.1:** Define Bin
- GIVEN warehouse structure (warehouse, aisle, rack)
- WHEN defining new bin with code, coordinates, capacity
- THEN create bin record
- AND validate coordinates unique within warehouse
- AND make bin available for stock placement

**AC-02.2:** Validate Location
- GIVEN a location code (e.g., "R3-C6-L3B3")
- WHEN validating location exists
- THEN return bin details if exists
- OR return error "Location not found"

**AC-02.3:** Enforce Capacity
- GIVEN a bin with capacity = 4 pallets
- WHEN attempting to place 5th pallet
- THEN reject with error "Bin capacity exceeded"

**AC-02.4:** Prevent Deletion of Occupied Bin
- GIVEN a bin containing handling units
- WHEN attempting to delete bin
- THEN reject with error "Cannot delete occupied bin"

### Non-Functional Requirements

**NFR-02.1 Performance:**
- Location validation: < 10ms
- Layout query (full warehouse): < 500ms

**NFR-02.2 Data Integrity:**
- Unique bin codes within warehouse
- No overlapping 3D coordinates

---

## FG-03: Handling Units (Physical Containers)

### Purpose

Track physical containers (pallets, boxes, bags) with barcode identifiers, their contents, and current location.

### Technical Scope

**Core Responsibilities:**
- Create handling units with unique LPN (barcode)
- Maintain contents (multiple SKUs per HU)
- Track current location
- Enforce sealing rules (immutable after seal)
- Project state from StockMoved events

**Aggregate:** HandlingUnit (state-based with event projection)

**Entities:**
- HandlingUnit (id, type, status, location, created, sealed)
- HandlingUnitLine (HU id, SKU, quantity)

**Value Objects:**
- HandlingUnitType: PALLET, BOX, BAG, UNIT
- HandlingUnitStatus: OPEN, SEALED, PICKED, EMPTY

**Storage:**
- Relational tables: handling_units, handling_unit_lines
- Indexed by: hu_id, location, sku

### Dependencies

**Upstream:**
- Movement Ledger (subscribes to StockMoved events)
- Warehouse Layout (validates location)

**Downstream:**
- Reservation (queries HU contents for allocation)
- 3D Visualization (displays HUs at locations)

### Acceptance Criteria

**AC-03.1:** Create Handling Unit
- GIVEN a valid location and HU type
- WHEN creating HU
- THEN generate unique LPN
- AND set status = OPEN
- AND place at specified location

**AC-03.2:** Add Line to HU
- GIVEN an OPEN handling unit
- WHEN adding SKU with quantity
- THEN create or update line
- AND HU remains OPEN

**AC-03.3:** Seal Handling Unit
- GIVEN an OPEN HU with lines
- WHEN sealing HU
- THEN set status = SEALED
- AND prevent further modifications

**AC-03.4:** Prevent Modification of Sealed HU
- GIVEN a SEALED handling unit
- WHEN attempting to add/remove line
- THEN reject with error "Cannot modify sealed HU"

**AC-03.5:** Project from StockMoved Events
- GIVEN StockMoved event (HU-001, SKU933, 10 units, A1 → B1)
- WHEN projection handler processes event
- THEN remove 10 units of SKU933 from HU-001 at A1
- AND add 10 units of SKU933 to HU-001 at B1
- AND update HU-001 location to B1

### Non-Functional Requirements

**NFR-03.1 Performance:**
- HU lookup by ID: < 10ms
- HU query by location: < 50ms
- Projection lag: < 5 seconds (p95)

**NFR-03.2 Consistency:**
- Projection idempotent (replay-safe)
- Eventually consistent with Movement Ledger

---

## FG-04: Reservation Engine

### Purpose

Manage stock reservations with hybrid locking strategy (SOFT → HARD) to balance planning flexibility with execution safety.

### Technical Scope

**Core Responsibilities:**
- Create reservations for production orders/sales orders
- Allocate handling units (SOFT lock during planning)
- Transition to HARD lock when picking starts
- Enforce bumping rules (SOFT can be bumped, HARD cannot)
- Track consumption and release stock

**Aggregate:** Reservation (event-sourced)

**Events:**
- ReservationCreated
- StockAllocated (SOFT lock)
- PickingStarted (SOFT → HARD transition)
- ReservationConsumed
- ReservationCancelled
- ReservationBumped

**State Machine:**
```
PENDING → ALLOCATED (SOFT) → PICKING (HARD) → CONSUMED
   ↓            ↓
CANCELLED   BUMPED
```

### Dependencies

**Upstream:**
- Movement Ledger (queries balance for validation)
- Handling Unit (queries HU contents)

**Downstream:**
- Process Managers (AllocationSaga, PickStockSaga)

### Acceptance Criteria

**AC-04.1:** Create Reservation
- GIVEN a purpose (e.g., ProductionOrder-123) and requested SKUs/quantities
- WHEN creating reservation
- THEN set status = PENDING
- AND trigger AllocationSaga

**AC-04.2:** Allocate Stock (SOFT Lock)
- GIVEN a PENDING reservation
- WHEN sufficient stock available
- THEN allocate handling units
- AND set lock type = SOFT
- AND status = ALLOCATED

**AC-04.3:** Start Picking (SOFT → HARD Transition)
- GIVEN an ALLOCATED reservation (SOFT)
- WHEN operator starts picking
- THEN re-validate balance and conflicts
- AND if valid: transition to HARD lock, status = PICKING
- AND if conflict: reject with error

**AC-04.4:** Bump SOFT Reservation
- GIVEN reservation R1 (SOFT lock on HU-001)
- WHEN higher priority reservation R2 needs HU-001
- THEN bump R1 (status = BUMPED)
- AND allocate HU-001 to R2
- AND notify R1 owner

**AC-04.5:** Prevent Bumping HARD Reservation
- GIVEN reservation R3 (HARD lock, status = PICKING)
- WHEN attempting to allocate same HU to R4
- THEN reject with error "Stock hard-locked by another reservation"

**AC-04.6:** Consume Reservation
- GIVEN a PICKING reservation
- WHEN all requested quantity picked
- THEN set status = CONSUMED
- AND release HU allocations

### Non-Functional Requirements

**NFR-04.1 Performance:**
- Allocation query: < 200ms
- StartPicking validation: < 100ms

**NFR-04.2 Consistency:**
- Re-validation on StartPicking (prevent stale reads)
- Optimistic concurrency control (version checks)

---

## FG-05: Valuation Engine

### Purpose

Manage financial interpretation of stock independently from physical quantities, supporting revaluations, landed costs, and write-downs.

### Technical Scope

**Core Responsibilities:**
- Maintain unit cost per SKU
- Apply cost adjustments (requires approval)
- Allocate landed costs (freight, duties)
- Support write-downs (damage, obsolescence)
- Compute on-hand value (qty × cost)

**Aggregate:** Valuation (event-sourced)

**Events:**
- CostAdjusted
- LandedCostAllocated
- StockWrittenDown

**Storage:**
- Event stream: valuation_events
- Projection: current_unit_costs (SKU → cost)

### Dependencies

**Upstream:**
- Movement Ledger (queries quantity for on-hand value)

**Downstream:**
- Financial Integration (exports cost data to Agnum)
- Read Models (OnHandValue projection)

### Acceptance Criteria

**AC-05.1:** Apply Cost Adjustment
- GIVEN a SKU with current cost €10
- WHEN accountant adjusts cost to €12 with reason and approval
- THEN record CostAdjusted event
- AND update unit cost to €12

**AC-05.2:** Require Approval for Adjustments
- GIVEN a cost adjustment request
- WHEN approver not authorized
- THEN reject with error "Unauthorized approver"

**AC-05.3:** Allocate Landed Cost
- GIVEN a SKU with cost €10 and landed cost €2 (freight)
- WHEN allocating landed cost
- THEN increase unit cost to €12
- AND record LandedCostAllocated event

**AC-05.4:** Write Down Stock
- GIVEN a SKU with cost €10
- WHEN writing down by 30% (damage)
- THEN reduce unit cost to €7
- AND record StockWrittenDown event

**AC-05.5:** Compute On-Hand Value
- GIVEN SKU933 with quantity 100 units and cost €10
- WHEN querying on-hand value
- THEN return €1,000

### Non-Functional Requirements

**NFR-05.1 Auditability:**
- All adjustments require reason and approver
- Immutable history of cost changes

**NFR-05.2 Independence:**
- Cost changes do NOT affect physical quantities
- Valuation and Movement Ledger are separate aggregates

---


## FG-06: Logical Warehouses & Categories

### Purpose

Provide virtual grouping of stock for reporting and financial mapping, supporting multi-categorization (SKU can belong to multiple categories).

### Technical Scope

**Core Responsibilities:**
- Define logical warehouses (RES, PROD, NLQ, SCRAP)
- Define product categories (Textile, Green, Hardware)
- Assign SKUs to categories (many-to-many)
- Provide metadata for Agnum export mapping

**Aggregate:** LogicalWarehouse (state-based)

**Entities:**
- LogicalWarehouse (id, code, name)
- Category (id, name, logical_wh_id)
- CategoryAssignment (sku, category_id)

**Storage:**
- Relational tables: logical_warehouses, categories, category_assignments

### Dependencies

**Upstream:** None (metadata)

**Downstream:**
- Financial Integration (uses categories for Agnum mapping)
- Read Models (StockByCategory projection)

### Acceptance Criteria

**AC-06.1:** Assign Category
- GIVEN a SKU and category
- WHEN assigning category
- THEN create assignment record
- AND allow multiple categories per SKU

**AC-06.2:** Remove Category
- GIVEN an existing category assignment
- WHEN removing category
- THEN delete assignment
- AND do NOT affect physical stock

**AC-06.3:** Query Categories for SKU
- GIVEN a SKU assigned to categories [Textile, Green]
- WHEN querying categories
- THEN return [Textile, Green]

### Non-Functional Requirements

**NFR-06.1 Flexibility:**
- Support unlimited categories per SKU
- Categories are metadata only (no physical impact)

---

## FG-07: Read Models & Projections

### Purpose

Provide optimized query models projected from event streams for fast read access.

### Technical Scope

**Core Projections:**

**1. LocationBalance**
- Source: StockMoved events
- Schema: (location, sku, quantity, last_updated)
- Query: Current balance at location for SKU

**2. AvailableStock**
- Source: StockMoved + StockAllocated + ReservationConsumed
- Schema: (location, sku, physical_qty, reserved_qty, available_qty)
- Query: Available stock for allocation

**3. OnHandValue**
- Source: StockMoved + CostAdjusted
- Schema: (location, sku, quantity, unit_cost, total_value)
- Query: Financial value of stock

**4. StockByCategory**
- Source: StockMoved + CategoryAssigned
- Schema: (category, sku, total_quantity, locations[])
- Query: Stock grouped by category

**5. MovementAuditTrail**
- Source: StockMoved events (no aggregation)
- Schema: Full event details
- Query: Audit log of all movements

### Dependencies

**Upstream:**
- Movement Ledger (StockMoved events)
- Reservation (allocation events)
- Valuation (cost events)
- Logical Warehouses (category assignments)

**Downstream:**
- UI (queries for display)
- Reports (queries for exports)

### Acceptance Criteria

**AC-07.1:** Project LocationBalance
- GIVEN StockMoved event (SKU933, 10 units, A1 → B1)
- WHEN projection processes event
- THEN decrease balance at A1 by 10
- AND increase balance at B1 by 10

**AC-07.2:** Projection Idempotency
- GIVEN a StockMoved event processed twice
- WHEN projection replays event
- THEN balance remains correct (no double-counting)

**AC-07.3:** Projection Lag Monitoring
- GIVEN projection processing events
- WHEN lag exceeds 30 seconds
- THEN alert operations team

**AC-07.4:** Rebuild Projection
- GIVEN corrupted LocationBalance projection
- WHEN triggering rebuild
- THEN replay all StockMoved events
- AND recompute balances from scratch

### Non-Functional Requirements

**NFR-07.1 Performance:**
- Projection lag: < 5 seconds (p95)
- Query latency: < 50ms (p95)

**NFR-07.2 Consistency:**
- Eventually consistent with event streams
- Idempotent projection handlers

---

## FG-08: 3D Warehouse Visualization

### Purpose

Provide interactive 3D visualization of warehouse layout with real-time stock status indicators.

### Technical Scope

**Core Responsibilities:**
- Render 3D warehouse model (bins, racks, aisles)
- Display handling units at locations
- Color-code status (FULL=green, LOW=yellow, EMPTY=red, RESERVED=orange)
- Support click-to-drill-down (bin → HU → contents)
- Real-time updates via WebSocket

**Data Sources:**
- Warehouse Layout (bin coordinates)
- Handling Unit Location (HU positions)
- Available Stock (status determination)

**Technology:**
- Frontend: Three.js or Babylon.js (3D rendering)
- Backend: WebSocket server for real-time updates

### Dependencies

**Upstream:**
- Warehouse Layout (bin definitions)
- Handling Unit (HU locations)
- Read Models (AvailableStock for status)

**Downstream:** None (UI component)

### Acceptance Criteria

**AC-08.1:** Render Warehouse
- GIVEN warehouse layout with bins
- WHEN loading 3D view
- THEN render all bins with correct 3D coordinates
- AND display aisles and racks

**AC-08.2:** Display Handling Units
- GIVEN handling units at locations
- WHEN rendering 3D view
- THEN display HU icons at bin positions
- AND color-code by status

**AC-08.3:** Click-to-Drill-Down
- GIVEN a bin with handling units
- WHEN user clicks bin
- THEN show right panel with:
  - Bin code and location
  - List of HUs at bin
  - Contents of each HU (SKU, quantity)
  - Actions: Pick, Reserve, Transfer

**AC-08.4:** Real-Time Updates
- GIVEN a StockMoved event (HU moved from A1 to B1)
- WHEN event published
- THEN update 3D view via WebSocket
- AND animate HU moving from A1 to B1

### Non-Functional Requirements

**NFR-08.1 Performance:**
- Initial load: < 2 seconds for 1000 bins
- Real-time update latency: < 1 second

**NFR-08.2 Usability:**
- Smooth 3D navigation (pan, zoom, rotate)
- Responsive on tablet devices

---

## FG-09: Inbound Operations (Receive Goods)

### Purpose

Handle goods receipt workflow from supplier to warehouse location.

### Technical Scope

**Core Workflow:**
1. Scan incoming pallet/box barcode
2. Record StockMovement (SUPPLIER → location)
3. Create HandlingUnit at location
4. Add lines to HU (SKU, quantity)
5. Seal HU
6. Print label

**Process Manager:** ReceiveGoodsSaga

### Dependencies

**Upstream:**
- Movement Ledger (record movement)
- Handling Unit (create HU)
- Warehouse Layout (validate location)
- Operational Integration (print label)

**Downstream:** None (entry point)

### Acceptance Criteria

**AC-09.1:** Receive Goods
- GIVEN incoming goods (SKU, quantity, supplier)
- WHEN operator scans barcode and assigns location
- THEN record StockMovement (SUPPLIER → location)
- AND create HandlingUnit at location
- AND seal HU
- AND print label

**AC-09.2:** Validate Location
- GIVEN invalid location code
- WHEN attempting to receive goods
- THEN reject with error "Location not found"

**AC-09.3:** Compensation on Failure
- GIVEN StockMovement recorded but HU creation fails
- WHEN saga fails
- THEN StockMovement remains (correct)
- AND next cycle count detects extra stock
- AND manual adjustment corrects

### Non-Functional Requirements

**NFR-09.1 Performance:**
- End-to-end receipt: < 5 seconds

**NFR-09.2 Reliability:**
- Saga compensation on failure
- Idempotent retry

---

## FG-10: Movement Operations (Transfer, Pick)

### Purpose

Handle stock transfers between locations and picking for production/orders.

### Technical Scope

**Core Workflows:**

**1. Transfer Between Bins**
- Move HU from location A to location B
- Record StockMovement for each line in HU
- Update HU location

**2. Pick for Production**
- Remove quantity from HU
- Record StockMovement (location → PRODUCTION)
- Consume reservation

**Process Managers:**
- TransferStockSaga
- PickStockSaga

### Dependencies

**Upstream:**
- Movement Ledger (record movements)
- Handling Unit (update HU)
- Reservation (consume reservation)

**Downstream:** None (operational workflows)

### Acceptance Criteria

**AC-10.1:** Transfer Stock
- GIVEN HU at location A
- WHEN transferring to location B
- THEN record StockMovement for each line (A → B)
- AND update HU location to B

**AC-10.2:** Pick Stock
- GIVEN reservation in PICKING state (HARD lock)
- WHEN operator picks quantity from HU
- THEN remove line from HU
- AND record StockMovement (location → PRODUCTION)
- AND consume reservation

**AC-10.3:** Enforce Pick Transaction Order (CRITICAL)
- GIVEN pick operation
- WHEN executing
- THEN record StockMovement FIRST (commit)
- THEN update HU projection (separate transaction)
- THEN consume reservation (separate transaction)

**AC-10.4:** Validate Reservation State
- GIVEN reservation NOT in PICKING state
- WHEN attempting to pick
- THEN reject with error "Reservation not in picking state"

### Non-Functional Requirements

**NFR-10.1 Performance:**
- Transfer: < 2 seconds
- Pick: < 3 seconds

**NFR-10.2 Consistency:**
- Strict transaction ordering (Decision 2)
- Saga compensation on failure

---

## FG-11: Inventory Adjustments & Cycle Counting

### Purpose

Support manual inventory corrections and cycle counting workflows.

### Technical Scope

**Core Workflows:**

**1. Manual Adjustment**
- Record StockMovement (SYSTEM → location or reverse)
- Requires reason and approval

**2. Cycle Count**
- Compare physical count with system balance
- Generate adjustment if discrepancy
- Track accuracy metrics

### Dependencies

**Upstream:**
- Movement Ledger (record adjustments)
- Read Models (query current balance)

**Downstream:**
- Observability (track adjustment frequency)

### Acceptance Criteria

**AC-11.1:** Manual Adjustment
- GIVEN discrepancy between physical and system
- WHEN accountant creates adjustment with reason and approval
- THEN record StockMovement (SYSTEM → location or reverse)
- AND update balance

**AC-11.2:** Require Approval
- GIVEN adjustment request without approval
- WHEN attempting to adjust
- THEN reject with error "Approval required"

**AC-11.3:** Cycle Count
- GIVEN physical count at location
- WHEN comparing with system balance
- THEN calculate delta
- AND if delta > 0: suggest adjustment
- AND track accuracy (count vs system)

### Non-Functional Requirements

**NFR-11.1 Auditability:**
- All adjustments logged with reason and approver
- Adjustment frequency monitored (alert if > 3 in 30 days)

---

## FG-12: Operational Integration (Labels, Scanners)

### Purpose

Integrate with warehouse floor equipment for real-time operations.

### Technical Scope

**Core Integrations:**

**1. Label Printing**
- Subscribe to HandlingUnitSealed events
- Generate ZPL label template
- Send to Zebra printer (TCP 9100)
- Retry on failure (3x)

**2. Barcode Scanners**
- Passive integration (keyboard wedge)
- Edge agent handles scanner input
- Lookup HU by barcode

**3. Equipment (Scales, Gates)**
- Weight verification for picks
- Serial/USB communication
- Tolerance validation (±5%)

### Dependencies

**Upstream:**
- Handling Unit (HandlingUnitSealed events)

**Downstream:** None (device integration)

### Acceptance Criteria

**AC-12.1:** Print Label
- GIVEN HandlingUnitSealed event
- WHEN label printing triggered
- THEN generate ZPL template
- AND send to printer
- AND await ACK (timeout 5 sec)

**AC-12.2:** Retry on Failure
- GIVEN printer offline
- WHEN print fails
- THEN retry 3x with backoff (5s, 15s, 30s)
- AND if still fails: queue for manual print

**AC-12.3:** Scan Barcode
- GIVEN operator scans HU barcode
- WHEN scanner sends input
- THEN lookup HU by barcode
- AND display HU details

### Non-Functional Requirements

**NFR-12.1 Latency:**
- Label print: < 5 seconds
- Barcode lookup: < 100ms

**NFR-12.2 Reliability:**
- Retry with exponential backoff
- Fallback to manual print

---

## FG-13: Financial Integration (Agnum Export)

### Purpose

Export inventory snapshots to Agnum accounting system with configurable granularity.

### Technical Scope

**Core Workflow:**
1. Scheduled trigger (daily 23:00) or manual
2. Query StockLedger for current balances
3. Query Valuation for unit costs
4. Query LogicalWarehouse for category mappings
5. Apply Agnum mapping configuration
6. Generate CSV or call Agnum API
7. Email reconciliation report

**Process Manager:** AgnumExportSaga

**Export Modes:**
- By Physical Warehouse
- By Logical Warehouse
- By Category
- Total Only

### Dependencies

**Upstream:**
- Movement Ledger (query balances)
- Valuation (query costs)
- Logical Warehouses (query categories)

**Downstream:**
- Agnum API (external system)

### Acceptance Criteria

**AC-13.1:** Scheduled Export
- GIVEN scheduled trigger (daily 23:00)
- WHEN export runs
- THEN query current balances and costs
- AND generate CSV
- AND send to Agnum API
- AND email reconciliation report

**AC-13.2:** Manual Export
- GIVEN user clicks "Export Now"
- WHEN export triggered
- THEN execute export immediately
- AND return export ID

**AC-13.3:** Retry on Failure
- GIVEN Agnum API returns 500
- WHEN export fails
- THEN retry 3x with backoff (1m, 5m, 15m)
- AND if still fails: save CSV to shared folder
- AND email alert

**AC-13.4:** Idempotent Export
- GIVEN export with ID EXP-001
- WHEN Agnum receives duplicate
- THEN deduplicate by export ID
- AND return success

### Non-Functional Requirements

**NFR-13.1 Latency:**
- Export generation: < 60 seconds
- API call timeout: 60 seconds

**NFR-13.2 Reliability:**
- Retry with exponential backoff
- Fallback to CSV export

---

## FG-14: Process Integration (MES/ERP)

### Purpose

Coordinate workflows between Warehouse and MES/ERP systems via anti-corruption layer.

### Technical Scope

**Core Workflows:**

**1. Material Request (ERP → Warehouse)**
- ERP publishes MaterialRequested
- Process Integration translates to CreateReservation
- Warehouse allocates stock
- Process Integration publishes MaterialReserved to ERP

**2. Material Consumption (Warehouse → ERP)**
- Warehouse publishes StockMoved (to PRODUCTION)
- Process Integration translates to MaterialConsumed
- ERP updates production order

**Anti-Corruption Layer:**
- Translate ERP events to Warehouse commands
- Translate Warehouse events to ERP events
- Maintain mapping (ProductionOrder → Reservation)

### Dependencies

**Upstream:**
- Reservation (CreateReservation command)
- Movement Ledger (StockMoved events)

**Downstream:**
- ERP/MES Core (external system)

### Acceptance Criteria

**AC-14.1:** Material Request
- GIVEN ERP publishes MaterialRequested (ProductionOrder-123)
- WHEN Process Integration receives event
- THEN translate to CreateReservation command
- AND send to Warehouse
- AND store mapping (PO-123 → res-042)

**AC-14.2:** Material Reserved
- GIVEN Warehouse publishes ReservationCreated (res-042)
- WHEN Process Integration receives event
- THEN translate to MaterialReserved event
- AND publish to ERP with ProductionOrder-123

**AC-14.3:** Material Consumed
- GIVEN Warehouse publishes StockMoved (to PRODUCTION)
- WHEN Process Integration receives event
- THEN translate to MaterialConsumed event
- AND publish to ERP

### Non-Functional Requirements

**NFR-14.1 Latency:**
- Translation: < 1 second
- End-to-end (ERP → Warehouse → ERP): < 30 seconds

**NFR-14.2 Reliability:**
- Saga compensation on failure
- Notify both systems on error

---

## FG-15: Process Managers & Sagas

### Purpose

Orchestrate multi-aggregate workflows with eventual consistency and compensation.

### Technical Scope

**Core Sagas:**

**1. ReceiveGoodsSaga**
- Steps: RecordMovement → CreateHU → AddLines → SealHU → PrintLabel
- Compensation: Delete empty HU if creation fails

**2. TransferStockSaga**
- Steps: ValidateLocation → RecordMovements (N) → UpdateHULocation
- Compensation: Revert HU location if movements fail

**3. PickStockSaga**
- Steps: RecordMovement → UpdateHUProjection → ConsumeReservation
- Compensation: None (StockMovement is source of truth)

**4. AllocationSaga**
- Steps: QueryAvailableStock → AllocateReservation
- Compensation: Release allocation if insufficient stock

**5. AgnumExportSaga**
- Steps: QueryBalances → QueryCosts → GenerateCSV → SendToAgnum → EmailReport
- Compensation: Retry with backoff, fallback to CSV

### Dependencies

**Upstream:** All aggregates (orchestrates commands)

**Downstream:** None (orchestration layer)

### Acceptance Criteria

**AC-15.1:** Saga Execution
- GIVEN a saga with 5 steps
- WHEN executing saga
- THEN execute steps sequentially
- AND save state after each step
- AND if crash: resume from last completed step

**AC-15.2:** Saga Idempotency
- GIVEN a saga step executed twice
- WHEN replaying step
- THEN return cached result (no duplicate execution)

**AC-15.3:** Saga Compensation
- GIVEN a saga fails at step 3
- WHEN compensation triggered
- THEN rollback steps 1-2
- AND mark saga as FAILED

### Non-Functional Requirements

**NFR-15.1 Reliability:**
- Saga state persisted after each step
- Restart-safe (resume from checkpoint)

**NFR-15.2 Monitoring:**
- Alert if saga stuck > 5 minutes

---

## FG-16: Edge/Offline Agent

### Purpose

Enable offline warehouse operations with conflict detection and reconciliation on reconnect.

### Technical Scope

**Core Responsibilities:**
- Cache reservation and HU data for offline use
- Queue allowed commands offline (PickStock, TransferStock)
- Block forbidden commands offline (AllocateReservation, StartPicking, etc.)
- Sync queued commands on reconnect
- Detect conflicts and show reconciliation report

**Allowed Offline:**
- PickStock (if reservation already HARD locked)
- TransferStock (if HU assigned to operator)

**Forbidden Offline:**
- AllocateReservation, StartPicking, AdjustStock, ApplyCostAdjustment, SplitHU, MergeHU, CreateReservation

**Storage:**
- Local SQLite database (offline_command_queue)

### Dependencies

**Upstream:**
- Warehouse Core (sync commands on reconnect)

**Downstream:** None (edge device)

### Acceptance Criteria

**AC-16.1:** Queue Command Offline
- GIVEN operator offline
- WHEN executing allowed command (PickStock)
- THEN validate preconditions locally
- AND queue command in SQLite
- AND show "Queued (will sync when online)"

**AC-16.2:** Block Forbidden Command Offline
- GIVEN operator offline
- WHEN attempting forbidden command (AllocateReservation)
- THEN block command
- AND show error "Cannot allocate offline - requires server connection"

**AC-16.3:** Sync on Reconnect
- GIVEN queued commands in SQLite
- WHEN reconnecting to server
- THEN send commands in FIFO order
- AND server re-validates all preconditions
- AND show reconciliation report

**AC-16.4:** Conflict Detection
- GIVEN queued PickStock command
- WHEN reservation cancelled while offline
- THEN server rejects command
- AND show error "Reservation cancelled while offline"
- AND prompt operator for action

### Non-Functional Requirements

**NFR-16.1 Offline Duration:**
- Support up to 8 hours offline
- Alert if offline > 8 hours

**NFR-16.2 Queue Size:**
- Max 100 commands in queue
- Alert if queue full

---

## FG-17: Observability & Audit

### Purpose

Provide comprehensive monitoring, logging, and audit trails for operations and compliance.

### Technical Scope

**Core Capabilities:**

**1. Audit Trail**
- All movements logged with operator, timestamp, reason
- Immutable event stream (StockMoved)
- Query: "Who moved SKU X on date Y?"

**2. Consistency Checks (Self-Test)**
- Daily automated checks:
  - Balance integrity (ledger vs projection)
  - No negative balances
  - No orphaned HUs
  - Event stream gaps
- Alert on failure

**3. Metrics & Monitoring**
- Projection lag
- Command latency
- Saga duration
- Error rates

**4. Operational Dashboards**
- Real-time stock levels
- Reservation status
- Movement activity
- Alert summary

### Dependencies

**Upstream:**
- All modules (collect metrics and logs)

**Downstream:** None (observability layer)

### Acceptance Criteria

**AC-17.1:** Audit Query
- GIVEN a date range and SKU
- WHEN querying audit trail
- THEN return all movements for SKU in range
- AND include operator, timestamp, reason

**AC-17.2:** Consistency Check
- GIVEN daily consistency check runs
- WHEN balance mismatch detected
- THEN alert operations team (P0)
- AND provide details for investigation

**AC-17.3:** Projection Lag Alert
- GIVEN projection lag exceeds 30 seconds
- WHEN monitoring detects lag
- THEN alert operations team
- AND show "Refreshing..." in UI

### Non-Functional Requirements

**NFR-17.1 Retention:**
- Audit logs: Permanent
- Metrics: 90 days

**NFR-17.2 Performance:**
- Audit query: < 2 seconds
- Metrics collection: < 10ms overhead

---

## FG-18: Security & Permissions

### Purpose

Enforce role-based access control and audit all privileged operations.

### Technical Scope

**Core Roles:**
- Warehouse Operator: Receive, Transfer, Pick
- Warehouse Manager: Adjust, Cycle Count, Reports
- Inventory Accountant: Revalue, Approve Adjustments
- System Administrator: Configuration, User Management

**Permissions:**
- ReceiveGoods: Operator, Manager
- TransferStock: Operator, Manager
- PickStock: Operator, Manager
- AdjustStock: Manager, Accountant (requires approval)
- ApplyCostAdjustment: Accountant (requires approval)
- StartPicking: Operator, Manager
- AllocateReservation: System (automated)

### Dependencies

**Upstream:**
- IAM (Identity & Access Management)

**Downstream:**
- All modules (enforce permissions)

### Acceptance Criteria

**AC-18.1:** Enforce Permissions
- GIVEN user with role Operator
- WHEN attempting AdjustStock
- THEN reject with error "Unauthorized"

**AC-18.2:** Require Approval
- GIVEN cost adjustment request
- WHEN approver not authorized
- THEN reject with error "Unauthorized approver"

**AC-18.3:** Audit Privileged Operations
- GIVEN cost adjustment executed
- WHEN logging
- THEN record operator, approver, reason, timestamp

### Non-Functional Requirements

**NFR-18.1 Security:**
- All commands authenticated
- Privileged operations require approval

---

## Summary

This feature decomposition provides 18 discrete feature groups, each with clear boundaries, dependencies, and acceptance criteria. These feature groups can be developed incrementally following the phased delivery plan in the next document.

**Next Document:** 20-phase-plan.md (Phased Delivery Plan)

