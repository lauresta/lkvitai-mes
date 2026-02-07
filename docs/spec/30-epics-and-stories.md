# Epics and Stories

**Project:** LKvitai.MES Warehouse Management System  
**Document:** Epics and Stories  
**Version:** 1.0  
**Date:** February 2026  
**Status:** Implementation Specification

---

## Document Purpose

This document translates the Phase Plan into an execution backlog of epics and user stories. Each story is mapped to:
- Requirements (Req 1-18 from requirements.md)
- Architecture components (aggregates, sagas from design.md)
- Implementation tasks (from tasks.md)
- Priority classification (MVP vs Later Phase)

**Traceability:**
- **Requirements:** `.kiro/specs/warehouse-core-phase1/requirements.md`
- **Design:** `.kiro/specs/warehouse-core-phase1/design.md`
- **Tasks:** `.kiro/specs/warehouse-core-phase1/tasks.md`
- **Phase Plan:** `docs/spec/20-phase-plan.md`

---

## Epic Structure

```
Phase 0: Foundation
├─ Epic 0.1: Event Store Infrastructure
└─ Epic 0.2: Command/Query Infrastructure

Phase 1: Core Inventory
├─ Epic 1.1: Movement Ledger
├─ Epic 1.2: Warehouse Layout
├─ Epic 1.3: Handling Units
├─ Epic 1.4: Inbound Operations
└─ Epic 1.5: Transfer Operations

Phase 2: Reservation & Picking
├─ Epic 2.1: Reservation Engine
├─ Epic 2.2: Allocation Logic
└─ Epic 2.3: Pick Workflows

Phase 3: Financial & Integration
├─ Epic 3.1: Valuation Engine
├─ Epic 3.2: Logical Warehouses
├─ Epic 3.3: Agnum Export
└─ Epic 3.4: MES/ERP Integration

Phase 4: Offline & Edge
└─ Epic 4.1: Edge Agent & Offline Operations

Phase 5: Visualization & Optimization
├─ Epic 5.1: 3D Warehouse Visualization
├─ Epic 5.2: Cycle Counting & Adjustments
└─ Epic 5.3: Observability & Performance
```

---

## Phase 0: Foundation (4 weeks)

### Epic 0.1: Event Store Infrastructure

**Goal:** Establish event sourcing foundation with Marten and PostgreSQL

**Architecture References:**
- Implementation Blueprint: Section 1 (Marten Event Store Configuration)
- Implementation Blueprint: Section 4 (Transactional Outbox)

**Tasks:** Task 1 (Solution Structure and Foundation)

**Priority:** MVP (Critical Foundation)

---

#### Story 0.1.1: Configure Marten Event Store

**As a** developer  
**I want** Marten configured with PostgreSQL  
**So that** I can store event streams for aggregates

**Acceptance Criteria:**
- Marten 7.x installed and configured
- PostgreSQL 15+ database created
- Event store schema created (mt_events, mt_streams)
- Connection string configured
- Event metadata enabled (correlation ID, causation ID)

**Definition of Done:**
- Configuration code committed
- Database migrations applied
- Connection test passes
- Documentation updated

**Complexity:** Medium (3 days)

**Dependencies:** None

**Implementation Order:** 1

---

#### Story 0.1.2: Implement Transactional Outbox

**As a** developer  
**I want** transactional outbox pattern implemented  
**So that** events are reliably published to subscribers

**Acceptance Criteria:**
- Outbox table schema created
- Outbox writer stores events atomically with aggregate changes
- Outbox processor polls and publishes events
- Retry logic with exponential backoff implemented
- At-least-once delivery guaranteed

**Definition of Done:**
- Outbox code committed
- Unit tests passing (95%+ coverage)
- Integration test demonstrates atomic write + publish
- Documentation updated

**Complexity:** High (5 days)

**Dependencies:** Story 0.1.1

**Implementation Order:** 2

---

### Epic 0.2: Command/Query Infrastructure

**Goal:** Implement CQRS infrastructure with command pipeline

**Architecture References:**
- Implementation Blueprint: Section 3 (Command Pipeline Architecture)

**Tasks:** Task 3 (Implement Command Infrastructure)

**Priority:** MVP (Critical Foundation)

---

#### Story 0.2.1: Create Command Handler Pipeline

**As a** developer  
**I want** command handler pipeline with MediatR  
**So that** I can process commands with validation and idempotency

**Acceptance Criteria:**
- MediatR configured
- ICommandHandler<TCommand, TResult> interface defined
- Pipeline behaviors: Validation, Idempotency, Transaction, Logging
- Processed commands table for idempotency
- Command returns cached result if already processed

**Definition of Done:**
- Pipeline code committed
- Unit tests passing (90%+ coverage)
- Sample command demonstrates pipeline
- Documentation updated

**Complexity:** High (5 days)

**Dependencies:** Story 0.1.1

**Implementation Order:** 3

---

## Phase 1: Core Inventory (6 weeks)

### Epic 1.1: Movement Ledger

**Goal:** Implement event-sourced StockLedger as single source of truth for all stock movements

**Requirements:** Req 1 (Stock Movement Ledger)

**Architecture References:**
- Design: Aggregate 1 (StockLedger)
- Design: Property 1-5 (Movement properties)
- Implementation Blueprint: Section 2.1 (Event-Sourced Aggregates)

**Tasks:** Task 2 (Implement StockLedger Aggregate)

**Priority:** MVP (Core Foundation)

---

#### Story 1.1.1: Record Stock Movement

**As a** warehouse operator  
**I want** to record stock movements  
**So that** all inventory changes are tracked in an immutable ledger

**Acceptance Criteria:**
- RecordStockMovement command accepts: sku, quantity, fromLocation, toLocation, type, operatorId
- Validates: from ≠ to, quantity > 0
- For physical locations: validates sufficient balance
- For virtual locations (SUPPLIER, PRODUCTION, SCRAP, SYSTEM): skips balance validation
- Appends StockMoved event to event stream
- Publishes event via transactional outbox

**Requirements Validated:** Req 1.1, 1.2, 1.3, 1.6, 1.7

**Definition of Done:**
- StockLedger aggregate implemented
- RecordStockMovement command handler implemented
- Unit tests passing (95%+ coverage)
- Property test: Balance Non-Negativity (Property 2)
- Property test: Movement Immutability (Property 1)
- Property test: Movement Constraints (Property 4)
- Property test: Virtual Location Bypass (Property 3)
- Integration test: Record movement end-to-end

**Complexity:** High (5 days)

**Dependencies:** Epic 0.1, Epic 0.2

**Implementation Order:** 4

---

#### Story 1.1.2: Query Historical Balance

**As a** warehouse manager  
**I want** to query stock balance at any point in time  
**So that** I can audit historical inventory levels

**Acceptance Criteria:**
- Query accepts: location, sku, timestamp
- Computes balance by replaying events up to timestamp
- Returns accurate historical balance
- Query completes in < 2 seconds (p95)

**Requirements Validated:** Req 1.8

**Definition of Done:**
- Time-travel query implemented
- Unit tests passing
- Performance test validates < 2s latency
- Documentation updated

**Complexity:** Medium (3 days)

**Dependencies:** Story 1.1.1

**Implementation Order:** 5

---

### Epic 1.2: Warehouse Layout

**Goal:** Define physical warehouse topology for location validation and 3D visualization

**Requirements:** Req 11 (Warehouse Layout Configuration)

**Architecture References:**
- Design: Aggregate 5 (WarehouseLayout)
- Design: Property 34-35 (Layout properties)

**Tasks:** Task 9 (Implement WarehouseLayout Aggregate)

**Priority:** MVP (Core Foundation)

---

#### Story 1.2.1: Define Warehouse Structure

**As a** warehouse manager  
**I want** to define bins, aisles, and racks  
**So that** the system knows the physical layout

**Acceptance Criteria:**
- DefineBin command accepts: warehouse, aisle, rack, binCode, coordinates3D, capacity
- Validates: coordinates unique within warehouse
- Validates: bins do not overlap in 3D space
- Stores bin configuration
- Provides layout data for 3D visualization

**Requirements Validated:** Req 11.1, 11.2, 11.7

**Definition of Done:**
- WarehouseLayout aggregate implemented
- DefineBin command handler implemented
- Unit tests passing (90%+ coverage)
- Property test: Coordinate Uniqueness (Property 34)
- Integration test: Define 100 bins

**Complexity:** Medium (4 days)

**Dependencies:** Epic 0.2

**Implementation Order:** 6

---

#### Story 1.2.2: Validate Locations

**As a** warehouse operator  
**I want** the system to validate location codes  
**So that** I cannot place stock at invalid locations

**Acceptance Criteria:**
- Location validation checks if bin exists
- Returns bin details if valid
- Returns error "Location not found" if invalid
- Validation completes in < 10ms

**Requirements Validated:** Req 11.3, 11.4

**Definition of Done:**
- Location validation implemented
- Unit tests passing
- Performance test validates < 10ms latency
- Integration test: Validate 1000 locations

**Complexity:** Low (2 days)

**Dependencies:** Story 1.2.1

**Implementation Order:** 7

---

### Epic 1.3: Handling Units

**Goal:** Track physical containers with barcode identifiers and project state from StockMoved events

**Requirements:** Req 2 (Handling Unit Lifecycle)

**Architecture References:**
- Design: Aggregate 2 (HandlingUnit)
- Design: Property 6-13 (HU properties)
- Implementation Blueprint: Section 2.2 (State-Based Aggregates)

**Tasks:** Task 6 (Implement HandlingUnit Aggregate)

**Priority:** MVP (Core Foundation)

---

#### Story 1.3.1: Create Handling Unit

**As a** warehouse operator  
**I want** to create handling units with unique barcodes  
**So that** I can track physical containers

**Acceptance Criteria:**
- CreateHandlingUnit command accepts: type, location, operatorId
- Generates unique LPN (barcode)
- Sets status = OPEN
- Places HU at specified location
- Validates location exists

**Requirements Validated:** Req 2.1

**Definition of Done:**
- HandlingUnit aggregate implemented
- CreateHandlingUnit command handler implemented
- Unit tests passing (95%+ coverage)
- Property test: LPN Uniqueness (Property 6)
- Integration test: Create 100 HUs

**Complexity:** Medium (3 days)

**Dependencies:** Epic 1.2

**Implementation Order:** 8

---

#### Story 1.3.2: Add Lines to Handling Unit

**As a** warehouse operator  
**I want** to add SKU lines to handling units  
**So that** I can track contents

**Acceptance Criteria:**
- AddLine command accepts: huId, sku, quantity
- Validates HU status = OPEN
- Creates or updates line
- Rejects if HU is SEALED

**Requirements Validated:** Req 2.2, 2.3

**Definition of Done:**
- AddLine command handler implemented
- Unit tests passing
- Property test: Sealed HU Immutability (Property 7)
- Integration test: Add 10 lines to HU

**Complexity:** Low (2 days)

**Dependencies:** Story 1.3.1

**Implementation Order:** 9

---

#### Story 1.3.3: Seal Handling Unit

**As a** warehouse operator  
**I want** to seal handling units  
**So that** contents cannot be modified after sealing

**Acceptance Criteria:**
- SealHandlingUnit command accepts: huId
- Validates HU has at least one line
- Sets status = SEALED
- Prevents further modifications
- Rejects if HU is empty

**Requirements Validated:** Req 2.4, 2.5

**Definition of Done:**
- SealHandlingUnit command handler implemented
- Unit tests passing
- Property test: Empty HU Seal Rejection (Property 8)
- Integration test: Seal HU end-to-end

**Complexity:** Low (2 days)

**Dependencies:** Story 1.3.2

**Implementation Order:** 10

---

#### Story 1.3.4: Project HU State from StockMoved Events

**As a** developer  
**I want** HandlingUnit state projected from StockMoved events  
**So that** HU location and contents stay synchronized with movements

**Acceptance Criteria:**
- Subscribes to StockMoved events
- When StockMoved with handlingUnitId:
  - If fromLocation == HU.location: RemoveLine(sku, qty)
  - If toLocation == HU.location: AddLine(sku, qty)
  - If locations differ: Update HU.location
- Projection is idempotent (replay-safe)
- Projection lag < 5 seconds (p95)

**Requirements Validated:** Req 2.6, 2.7

**Definition of Done:**
- HU projection handler implemented
- Unit tests passing (90%+ coverage)
- Property test: HU Projection Consistency (Property 10)
- Property test: HU Single Location (Property 11)
- Integration test: Move HU, verify projection updates

**Complexity:** High (5 days)

**Dependencies:** Story 1.1.1, Story 1.3.1

**Implementation Order:** 11

---

### Epic 1.4: Inbound Operations

**Goal:** Implement goods receipt workflow from supplier to warehouse location

**Requirements:** Req 15 (Goods Receipt Workflow)

**Architecture References:**
- Design: ReceiveGoodsSaga
- Implementation Blueprint: Section 5 (Saga Runtime)

**Tasks:** Task 14 (Implement ReceiveGoodsSaga)

**Priority:** MVP (Core Workflow)

---

#### Story 1.4.1: Receive Goods from Supplier

**As a** warehouse operator  
**I want** to receive goods from suppliers  
**So that** incoming inventory is recorded and labeled

**Acceptance Criteria:**
- ReceiveGoods command accepts: sku, quantity, location, operatorId
- Saga steps:
  1. Record StockMovement (SUPPLIER → location) via StockLedger
  2. Create HandlingUnit at location
  3. Add lines to HU
  4. Seal HU
  5. Request label print
- If HU creation fails after movement: Create orphan movement alert
- Saga is idempotent (restart-safe)

**Requirements Validated:** Req 15.1-15.9

**Definition of Done:**
- ReceiveGoodsSaga implemented
- Unit tests passing (90%+ coverage)
- Integration test: Receive goods end-to-end
- Compensation test: HU creation fails, orphan alert created
- UI for goods receipt implemented

**Complexity:** High (6 days)

**Dependencies:** Epic 1.1, Epic 1.3

**Implementation Order:** 12

---

### Epic 1.5: Transfer Operations

**Goal:** Implement stock transfer workflow between locations

**Requirements:** Req 16 (Transfer Workflow)

**Architecture References:**
- Design: TransferStockSaga

**Tasks:** Task 15 (Implement TransferStockSaga)

**Priority:** MVP (Core Workflow)

---

#### Story 1.5.1: Transfer Handling Unit Between Locations

**As a** warehouse operator  
**I want** to transfer handling units between locations  
**So that** I can reorganize inventory

**Acceptance Criteria:**
- TransferStock command accepts: huId, toLocation, operatorId
- Saga steps:
  1. Validate destination location exists
  2. For each line in HU: Record StockMovement (fromLocation → toLocation)
  3. Update HU location
- If any movement fails: Abort transfer, do not update HU location
- Saga is idempotent

**Requirements Validated:** Req 16.1-16.5

**Definition of Done:**
- TransferStockSaga implemented
- Unit tests passing (90%+ coverage)
- Integration test: Transfer HU end-to-end
- Compensation test: Movement fails, HU location not updated
- UI for transfer implemented

**Complexity:** Medium (4 days)

**Dependencies:** Epic 1.1, Epic 1.3

**Implementation Order:** 13

---

## Phase 2: Reservation & Picking (6 weeks)

### Epic 2.1: Reservation Engine

**Goal:** Implement reservation system with hybrid locking (SOFT → HARD transitions)

**Requirements:** Req 3 (Reservation System with Hybrid Locking)

**Architecture References:**
- Design: Aggregate 3 (Reservation)
- Design: Property 14-22 (Reservation properties)
- Implementation Blueprint: Section 2.1 (Event-Sourced Aggregates)

**Tasks:** Task 7 (Implement Reservation Aggregate)

**Priority:** MVP (Core Business Logic)

---

#### Story 2.1.1: Create Reservation

**As a** production planner  
**I want** to create reservations for production orders  
**So that** materials are reserved for my orders

**Acceptance Criteria:**
- CreateReservation command accepts: purpose, priority, requestedLines[]
- Sets status = PENDING
- Triggers AllocationSaga
- Stores reservation in event stream

**Requirements Validated:** Req 3.1

**Definition of Done:**
- Reservation aggregate implemented
- CreateReservation command handler implemented
- Unit tests passing (95%+ coverage)
- Property test: Reservation Initial State (Property 14)
- Integration test: Create reservation end-to-end

**Complexity:** Medium (4 days)

**Dependencies:** Epic 0.1, Epic 0.2

**Implementation Order:** 14

---

#### Story 2.1.2: Allocate Stock with SOFT Lock

**As a** production planner  
**I want** stock allocated to my reservation  
**So that** I know materials are available

**Acceptance Criteria:**
- AllocateReservation command accepts: reservationId, huIds[]
- Queries StockLedger for current balance
- Sets lock type = SOFT
- Sets status = ALLOCATED
- Allows overbooking (multiple SOFT locks on same stock)

**Requirements Validated:** Req 3.2, 3.3

**Definition of Done:**
- AllocateReservation command handler implemented
- Unit tests passing
- Property test: Allocation Balance Query (Property 15)
- Property test: Soft Lock Overbooking (Property 16)
- Integration test: Allocate reservation

**Complexity:** High (5 days)

**Dependencies:** Story 2.1.1, Epic 1.1

**Implementation Order:** 15

---

#### Story 2.1.3: Start Picking (SOFT → HARD Transition)

**As a** warehouse operator  
**I want** to start picking against a reservation  
**So that** stock is hard-locked and cannot be taken by others

**Acceptance Criteria:**
- StartPicking command accepts: reservationId
- Validates reservation status = ALLOCATED
- Validates lock type = SOFT
- Re-validates balance is sufficient
- Transitions lock type to HARD
- Sets status = PICKING
- Rejects if balance insufficient or conflict detected

**Requirements Validated:** Req 3.4, 3.5

**Definition of Done:**
- StartPicking command handler implemented
- Unit tests passing
- Property test: Hard Lock Transition (Property 17)
- Property test: Hard Lock Re-validation (Property 18)
- Integration test: Start picking end-to-end

**Complexity:** High (5 days)

**Dependencies:** Story 2.1.2

**Implementation Order:** 16

---

#### Story 2.1.4: Implement Reservation Bumping

**As a** production planner  
**I want** higher priority reservations to bump lower priority SOFT locks  
**So that** urgent orders get materials first

**Acceptance Criteria:**
- When allocating reservation R2 (high priority):
  - If HU is SOFT locked by R1 (lower priority): Bump R1
  - If HU is HARD locked: Reject allocation
- Bumped reservation status = BUMPED
- Notify owner of bumped reservation

**Requirements Validated:** Req 3.6, 3.7, 3.8

**Definition of Done:**
- Bumping logic implemented
- Unit tests passing (90%+ coverage)
- Property test: Soft Lock Bumping (Property 19)
- Property test: Hard Lock Exclusivity (Property 20)
- Integration test: Bump SOFT reservation
- Integration test: Cannot bump HARD reservation

**Complexity:** High (5 days)

**Dependencies:** Story 2.1.3

**Implementation Order:** 17

---

#### Story 2.1.5: Consume Reservation

**As a** warehouse operator  
**I want** reservations marked as consumed after picking  
**So that** stock is released

**Acceptance Criteria:**
- ConsumeReservation command accepts: reservationId, actualQuantity
- Validates reservation status = PICKING
- Marks status = CONSUMED when all quantity picked
- Releases HU allocations

**Requirements Validated:** Req 3.9

**Definition of Done:**
- ConsumeReservation command handler implemented
- Unit tests passing
- Property test: Reservation Consumption (Property 21)
- Integration test: Consume reservation end-to-end

**Complexity:** Medium (3 days)

**Dependencies:** Story 2.1.3

**Implementation Order:** 18

---

### Epic 2.2: Allocation Logic

**Goal:** Implement saga to allocate available HUs to reservations

**Requirements:** Req 3 (Reservation System)

**Architecture References:**
- Design: AllocationSaga

**Tasks:** Task 18 (Implement AllocationSaga)

**Priority:** MVP (Core Business Logic)

---

#### Story 2.2.1: Allocate Available Stock to Reservation

**As a** production planner  
**I want** the system to automatically find and allocate stock  
**So that** I don't have to manually select handling units

**Acceptance Criteria:**
- AllocationSaga triggered by ReservationCreated event
- Queries AvailableStock projection
- Finds HUs matching requested SKUs
- Validates balance sufficient
- Allocates HUs to reservation (SOFT lock)
- If insufficient stock: Notify user, keep reservation PENDING

**Requirements Validated:** Req 3.2

**Definition of Done:**
- AllocationSaga implemented
- Unit tests passing (90%+ coverage)
- Integration test: Allocate reservation end-to-end
- Integration test: Insufficient stock scenario

**Complexity:** High (6 days)

**Dependencies:** Story 2.1.2, Epic 1.1

**Implementation Order:** 19

---

### Epic 2.3: Pick Workflows

**Goal:** Implement picking workflow with strict transaction ordering

**Requirements:** Req 4 (Transaction Ordering for Pick Operations), Req 17 (Pick Workflow)

**Architecture References:**
- Design: PickStockSaga
- Design: Property 23 (Pick Transaction Ordering)
- Implementation Blueprint: Section 3.3 (Transaction Boundaries)

**Tasks:** Task 16 (Implement PickStockSaga)

**Priority:** MVP (Critical Workflow)

---

#### Story 2.3.1: Pick Stock Against Reservation

**As a** warehouse operator  
**I want** to pick stock against a reservation  
**So that** materials are moved to production

**Acceptance Criteria:**
- PickStock command accepts: reservationId, huId, sku, quantity, operatorId
- Saga enforces strict transaction ordering:
  1. Validate reservation status = PICKING (HARD locked)
  2. Validate HU allocated to reservation
  3. **CRITICAL:** Record StockMovement (location → PRODUCTION) via StockLedger FIRST
  4. Wait for HandlingUnit projection to process StockMoved event
  5. Update Reservation consumption
- If StockLedger fails: Rollback (no partial state)
- If HU projection fails: Replay from StockMoved event
- If Reservation update fails: Retry consumption

**Requirements Validated:** Req 4.1-4.7, Req 17.1-17.12

**Definition of Done:**
- PickStockSaga implemented
- Unit tests passing (95%+ coverage)
- Property test: Pick Transaction Ordering (Property 23)
- Integration test: Pick stock end-to-end
- Compensation test: StockLedger fails, rollback
- Compensation test: HU projection fails, replay
- Compensation test: Reservation update fails, retry
- UI for picking implemented

**Complexity:** Critical (8 days)

**Dependencies:** Story 2.1.3, Epic 1.1, Epic 1.3

**Implementation Order:** 20

---

## Phase 3: Financial & Integration (5 weeks)

### Epic 3.1: Valuation Engine

**Goal:** Manage financial interpretation of stock independently from physical quantities

**Requirements:** Req 10 (Valuation Management)

**Architecture References:**
- Design: Aggregate 4 (Valuation)
- Design: Property 32-33 (Valuation properties)

**Tasks:** Task 8 (Implement Valuation Aggregate)

**Priority:** MVP (Financial Compliance)

---

#### Story 3.1.1: Apply Cost Adjustments

**As an** inventory accountant  
**I want** to adjust unit costs for SKUs  
**So that** I can handle revaluations and landed costs

**Acceptance Criteria:**
- ApplyCostAdjustment command accepts: sku, newCost, reason, approver
- Validates approver is authorized
- Records CostAdjusted event
- Updates unit cost
- Physical quantity remains unchanged

**Requirements Validated:** Req 10.1, 10.2, 10.7

**Definition of Done:**
- Valuation aggregate implemented
- ApplyCostAdjustment command handler implemented
- Unit tests passing (95%+ coverage)
- Property test: Valuation Adjustment Requires Approval (Property 32)
- Property test: Valuation Independence (Property 33)
- Integration test: Adjust cost end-to-end

**Complexity:** Medium (4 days)

**Dependencies:** Epic 0.1, Epic 0.2

**Implementation Order:** 21

---

#### Story 3.1.2: Allocate Landed Costs

**As an** inventory accountant  
**I want** to allocate landed costs (freight, duties) to SKUs  
**So that** total cost reflects all expenses

**Acceptance Criteria:**
- AllocateLandedCost command accepts: sku, amount, reason
- Increases unit cost by amount
- Records LandedCostAllocated event

**Requirements Validated:** Req 10.3

**Definition of Done:**
- AllocateLandedCost command handler implemented
- Unit tests passing
- Integration test: Allocate landed cost

**Complexity:** Low (2 days)

**Dependencies:** Story 3.1.1

**Implementation Order:** 22

---

#### Story 3.1.3: Write Down Stock

**As an** inventory accountant  
**I want** to write down stock value  
**So that** I can account for damage or obsolescence

**Acceptance Criteria:**
- WriteDownStock command accepts: sku, percentage, reason, approver
- Reduces unit cost by percentage
- Records StockWrittenDown event

**Requirements Validated:** Req 10.4

**Definition of Done:**
- WriteDownStock command handler implemented
- Unit tests passing
- Integration test: Write down stock

**Complexity:** Low (2 days)

**Dependencies:** Story 3.1.1

**Implementation Order:** 23

---

### Epic 3.2: Logical Warehouses

**Goal:** Provide virtual grouping of stock for reporting and financial mapping

**Requirements:** None (supporting feature)

**Architecture References:**
- Design: Aggregate 6 (LogicalWarehouse)

**Tasks:** None (simple CRUD)

**Priority:** MVP (Financial Compliance)

---

#### Story 3.2.1: Define Logical Warehouses and Categories

**As a** warehouse manager  
**I want** to define logical warehouses and categories  
**So that** I can group stock for reporting

**Acceptance Criteria:**
- Define logical warehouses (RES, PROD, NLQ, SCRAP)
- Define categories (Textile, Green, Hardware)
- Assign SKUs to categories (many-to-many)
- Query categories for SKU

**Definition of Done:**
- LogicalWarehouse aggregate implemented
- CRUD operations implemented
- Unit tests passing
- Integration test: Assign categories

**Complexity:** Low (3 days)

**Dependencies:** None

**Implementation Order:** 24

---

### Epic 3.3: Agnum Export

**Goal:** Export inventory snapshots to Agnum accounting system

**Requirements:** Req 9 (Agnum Export Baseline)

**Architecture References:**
- Design: AgnumExportSaga
- Design: Property 30-31 (Agnum properties)

**Tasks:** Task 21 (Implement Agnum Export Integration)

**Priority:** MVP (Financial Compliance)

---

#### Story 3.3.1: Export Stock Snapshot to Agnum

**As an** inventory accountant  
**I want** to export stock snapshots to Agnum  
**So that** financial records stay synchronized

**Acceptance Criteria:**
- AgnumExport command accepts: exportMode (ByPhysicalWarehouse, ByLogicalWarehouse, ByCategory, TotalSum)
- Saga steps:
  1. Query StockMovement ledger for current balances
  2. Query Valuation for unit costs
  3. Query LogicalWarehouse for category mappings
  4. Apply Agnum mapping rules
  5. Generate CSV or call Agnum API
  6. Record export timestamp
- Export includes unique ExportId for deduplication
- Retry with exponential backoff (3 attempts)
- If failed: Alert administrator, save CSV to shared folder

**Requirements Validated:** Req 9.1-9.10

**Definition of Done:**
- AgnumExportSaga implemented
- Unit tests passing (90%+ coverage)
- Property test: Agnum Export Data Correctness (Property 30)
- Property test: Agnum Export Idempotency (Property 31)
- Integration test: Export to Agnum end-to-end
- Integration test: Retry on failure

**Complexity:** High (6 days)

**Dependencies:** Epic 3.1, Epic 3.2, Epic 1.1

**Implementation Order:** 25

---

### Epic 3.4: MES/ERP Integration

**Goal:** Coordinate workflows between Warehouse and MES/ERP systems

**Requirements:** Req 14 (Process Integration with ERP/MES)

**Architecture References:**
- Design: ERP Integration Saga
- Design: Property 41-42 (ERP properties)
- Implementation Blueprint: Section 9 (Integration Adapter Contracts)

**Tasks:** Task 22 (Implement ERP Integration)

**Priority:** MVP (Process Integration)

---

#### Story 3.4.1: Handle Material Requests from ERP

**As a** production planner (in ERP)  
**I want** to request materials from warehouse  
**So that** reservations are automatically created

**Acceptance Criteria:**
- Subscribe to MaterialRequested events from ERP
- Translate to CreateReservation command
- Create reservation in warehouse
- Wait for AllocationSaga to allocate
- Send MaterialReserved event to ERP
- Retry with exponential backoff on failure

**Requirements Validated:** Req 14.1, 14.2, 14.7-14.10

**Definition of Done:**
- ERP Integration Saga implemented
- Anti-corruption layer implemented
- Unit tests passing (90%+ coverage)
- Property test: ERP Integration Retry (Property 41)
- Integration test: Material request end-to-end

**Complexity:** High (6 days)

**Dependencies:** Epic 2.1, Epic 2.2

**Implementation Order:** 26

---

#### Story 3.4.2: Notify ERP of Material Consumption

**As a** production planner (in ERP)  
**I want** to be notified when materials are consumed  
**So that** I can update production orders

**Acceptance Criteria:**
- Subscribe to StockMoved events (to PRODUCTION location)
- Translate to MaterialConsumed event
- Send to ERP with unique event ID
- ERP deduplicates by event ID
- Retry with exponential backoff on failure

**Requirements Validated:** Req 14.3, 14.11, 14.12

**Definition of Done:**
- MaterialConsumed publisher implemented
- Unit tests passing
- Property test: ERP Event Idempotency (Property 42)
- Integration test: Material consumption notification

**Complexity:** Medium (4 days)

**Dependencies:** Epic 1.1, Story 3.4.1

**Implementation Order:** 27

---

## Phase 4: Offline & Edge (4 weeks)

### Epic 4.1: Edge Agent & Offline Operations

**Goal:** Enable offline warehouse operations with conflict detection and reconciliation

**Requirements:** Req 5 (Offline Edge Operations)

**Architecture References:**
- Design: Offline / Edge Architecture
- Design: Property 24-25 (Offline properties)
- Implementation Blueprint: Section 8 (Offline Sync Protocol)

**Tasks:** Task 24 (Implement Offline/Edge Architecture)

**Priority:** Later Phase (Operational Resilience)

---

#### Story 4.1.1: Implement Edge Command Queue

**As a** warehouse operator  
**I want** to queue commands offline  
**So that** I can continue working during network outages

**Acceptance Criteria:**
- Local SQLite database for command queue
- Queue commands in FIFO order
- Enforce whitelist: PickStock (HARD locked), TransferStock (assigned HUs)
- Block forbidden commands: AllocateReservation, StartPicking, AdjustStock, etc.
- Queue size limit: 100 commands

**Requirements Validated:** Req 5.1-5.6

**Definition of Done:**
- Edge agent implemented
- Offline command queue implemented
- Unit tests passing (90%+ coverage)
- Property test: Offline Operation Whitelist (Property 24)
- Integration test: Queue commands offline

**Complexity:** High (6 days)

**Dependencies:** Epic 2.3

**Implementation Order:** 28

---

#### Story 4.1.2: Implement Sync Engine with Conflict Detection

**As a** warehouse operator  
**I want** offline commands synced when I reconnect  
**So that** my work is saved to the server

**Acceptance Criteria:**
- Sync engine syncs queued commands in FIFO order
- Server re-validates each command:
  - Check reservation still PICKING
  - Check HU still allocated
  - Check balance sufficient
- If validation fails: Reject command, add to reconciliation report
- If validation succeeds: Execute command
- Display reconciliation report to operator

**Requirements Validated:** Req 5.7-5.9

**Definition of Done:**
- Sync engine implemented
- Server-side validation implemented
- Unit tests passing (90%+ coverage)
- Property test: Offline Sync Rejection (Property 25)
- Integration test: Sync commands end-to-end
- Integration test: Conflict detection (reservation bumped)
- Integration test: Conflict detection (HU moved)
- UI for reconciliation report

**Complexity:** High (8 days)

**Dependencies:** Story 4.1.1

**Implementation Order:** 29

---

## Phase 5: Visualization & Optimization (5 weeks)

### Epic 5.1: 3D Warehouse Visualization

**Goal:** Provide interactive 3D visualization of warehouse layout with real-time stock status

**Requirements:** None (UI feature)

**Architecture References:**
- Design: 3D Warehouse Visualization

**Tasks:** None (UI implementation)

**Priority:** Later Phase (User Experience)

---

#### Story 5.1.1: Render 3D Warehouse Layout

**As a** warehouse manager  
**I want** to view 3D warehouse layout  
**So that** I can visualize stock locations

**Acceptance Criteria:**
- Render warehouse with bins, racks, aisles using Three.js or Babylon.js
- Display handling units at locations
- Color-code by status: FULL=green, LOW=yellow, EMPTY=red, RESERVED=orange
- Support pan, zoom, rotate navigation
- Initial load < 2 seconds for 1000 bins

**Definition of Done:**
- 3D visualization implemented
- Unit tests passing (UI components)
- Performance test validates < 2s load time
- Integration test: Render 1000 bins

**Complexity:** High (8 days)

**Dependencies:** Epic 1.2, Epic 1.3

**Implementation Order:** 30

---

#### Story 5.1.2: Click-to-Drill-Down

**As a** warehouse manager  
**I want** to click on bins to see contents  
**So that** I can inspect stock details

**Acceptance Criteria:**
- Click on bin displays right panel with:
  - Bin code and location
  - List of HUs at bin
  - Contents of each HU (SKU, quantity)
  - Actions: Pick, Reserve, Transfer
- Click on HU displays HU details

**Definition of Done:**
- Drill-down UI implemented
- Unit tests passing
- Integration test: Click bin, view contents

**Complexity:** Medium (4 days)

**Dependencies:** Story 5.1.1

**Implementation Order:** 31

---

#### Story 5.1.3: Real-Time Updates via WebSocket

**As a** warehouse manager  
**I want** 3D view to update in real-time  
**So that** I see current stock status

**Acceptance Criteria:**
- Subscribe to StockMoved events via WebSocket
- Update 3D view when HU moves
- Animate HU moving from source to destination
- Update latency < 1 second

**Definition of Done:**
- WebSocket integration implemented
- Real-time updates working
- Performance test validates < 1s latency
- Integration test: Move HU, verify 3D updates

**Complexity:** Medium (4 days)

**Dependencies:** Story 5.1.1

**Implementation Order:** 32

---

### Epic 5.2: Cycle Counting & Adjustments

**Goal:** Support manual inventory corrections and cycle counting workflows

**Requirements:** None (operational feature)

**Architecture References:**
- Design: Inventory Adjustments & Cycle Counting

**Tasks:** None (simple workflows)

**Priority:** Later Phase (Operational Excellence)

---

#### Story 5.2.1: Manual Inventory Adjustment

**As an** inventory accountant  
**I want** to manually adjust stock levels  
**So that** I can correct discrepancies

**Acceptance Criteria:**
- AdjustStock command accepts: sku, location, quantity, reason, approver
- Records StockMovement (SYSTEM → location or reverse)
- Requires reason and approval
- Logs adjustment for audit

**Definition of Done:**
- AdjustStock command handler implemented
- Unit tests passing
- Integration test: Adjust stock end-to-end
- UI for adjustments

**Complexity:** Medium (3 days)

**Dependencies:** Epic 1.1

**Implementation Order:** 33

---

#### Story 5.2.2: Cycle Count Workflow

**As a** warehouse manager  
**I want** to perform cycle counts  
**So that** I can verify stock accuracy

**Acceptance Criteria:**
- Cycle count workflow:
  1. Select location and SKU
  2. Enter physical count
  3. Compare with system balance
  4. If delta > 0: Suggest adjustment
  5. Track accuracy (count vs system)
- Generate accuracy report

**Definition of Done:**
- Cycle count workflow implemented
- Unit tests passing
- Integration test: Cycle count end-to-end
- UI for cycle counting

**Complexity:** Medium (4 days)

**Dependencies:** Story 5.2.1

**Implementation Order:** 34

---

### Epic 5.3: Observability & Performance

**Goal:** Implement comprehensive observability and performance optimization

**Requirements:** Req 13 (Consistency Verification)

**Architecture References:**
- Design: Observability & Monitoring Strategy
- Design: Property 39-40 (Consistency properties)
- Implementation Blueprint: Section 10 (Observability Implementation)

**Tasks:** Task 25 (Implement Consistency Verification), Task 26 (Implement Observability Infrastructure)

**Priority:** Later Phase (Operational Excellence)

---

#### Story 5.3.1: Daily Consistency Checks

**As a** warehouse manager  
**I want** automated consistency checks  
**So that** I can detect discrepancies early

**Acceptance Criteria:**
- Daily job runs consistency checks:
  1. Verify LocationBalance matches event stream computation
  2. Verify no negative balances
  3. Verify HU contents match LocationBalance
  4. Verify no orphaned HUs
  5. Verify consumed reservations released HUs
  6. Verify event stream has no sequence gaps
- Generate alerts for failures (P0, P1, P2)
- Provide rebuild projection tool

**Requirements Validated:** Req 13.1-13.9

**Definition of Done:**
- Consistency check job implemented
- Unit tests passing (90%+ coverage)
- Property test: Balance Consistency Verification (Property 39)
- Property test: No Negative Balance Verification (Property 40)
- Integration test: Run consistency checks
- Alert configuration

**Complexity:** High (6 days)

**Dependencies:** Epic 1.1, Epic 1.3

**Implementation Order:** 35

---

#### Story 5.3.2: Metrics and Dashboards

**As a** system administrator  
**I want** metrics and dashboards  
**So that** I can monitor system health

**Acceptance Criteria:**
- Metrics collected: command latency, projection lag, throughput, etc.
- Prometheus metrics endpoint exposed
- Grafana dashboards created:
  - Operations Dashboard (stock levels, reservations, picks)
  - Technical Dashboard (event store, projections, sagas)
  - Business Dashboard (throughput, accuracy, turnover)

**Definition of Done:**
- Metrics collection implemented
- Prometheus integration configured
- Grafana dashboards created
- Documentation updated

**Complexity:** Medium (5 days)

**Dependencies:** All epics

**Implementation Order:** 36

---

#### Story 5.3.3: Performance Optimization

**As a** system administrator  
**I want** system optimized for performance  
**So that** it handles 1000 movements/hour

**Acceptance Criteria:**
- Query optimization (indexes, query plans)
- Projection optimization (batch processing)
- Load testing validates targets:
  - 1000 movements/second
  - Command latency < 100ms (p95)
  - Projection lag < 5 seconds (p95)

**Definition of Done:**
- Performance optimization completed
- Load tests passing
- Performance report generated
- Documentation updated

**Complexity:** High (6 days)

**Dependencies:** All epics

**Implementation Order:** 37

---

## Summary

This document provides a complete backlog of epics and stories for the warehouse system, organized by phase and mapped to requirements, architecture components, and implementation tasks. Each story includes:

- Clear acceptance criteria
- Requirements validation
- Definition of done
- Complexity estimate
- Dependencies
- Implementation order

**Total Stories:** 37  
**Total Duration:** 30 weeks  
**MVP Stories:** 29 (Phases 0-3)  
**Later Phase Stories:** 8 (Phases 4-5)

**Next Document:** 40-technical-implementation-guidelines.md (Coding Standards and Patterns)
