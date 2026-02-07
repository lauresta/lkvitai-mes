# Requirements Document: Warehouse Core Phase 1

## Introduction

This document specifies the requirements for Phase 1 of the LKvitai.MES Warehouse Management System. The system provides real-time inventory tracking using Handling Units, an event-sourced StockMovement ledger, hybrid reservation locking (SOFT → HARD transitions), offline edge operations, and accounting integration with Agnum.

**Scope:** Core warehouse functionality including stock movement ledger, handling unit lifecycle, reservation system, read models, operator UI flows, label printing, and Agnum export baseline.

**Architecture Constraints:**
- StockLedger is the ONLY module that can append StockMoved events
- Pick operations follow strict transaction ordering (Ledger → HU Projection → Reservation)
- Offline operations restricted to PickStock (HARD locked) and TransferStock (assigned HUs)
- SOFT reservations can be bumped; HARD reservations cannot
- Integration split into Operational (<5s), Financial (minutes), and Process (<30s) components

## Glossary

- **System**: The LKvitai.MES Warehouse Management System
- **StockLedger**: Event-sourced aggregate that owns all StockMovement events
- **HandlingUnit (HU)**: Physical container (pallet, box, bag, unit) with unique barcode identifier
- **LPN**: License Plate Number - unique identifier for handling unit
- **StockMovement**: Immutable record of stock moving from one location to another
- **Reservation**: Claim on future stock consumption with hybrid locking (SOFT/HARD)
- **SOFT_Lock**: Advisory reservation lock that can be bumped by higher priority
- **HARD_Lock**: Exclusive reservation lock that cannot be bumped (picking in progress)
- **Location**: Physical or virtual address in warehouse (e.g., "R3-C6-L3B3" or "PRODUCTION")
- **Virtual_Location**: Conceptual endpoint (SUPPLIER, PRODUCTION, SCRAP, SYSTEM)
- **Operator**: Warehouse worker performing physical operations
- **Manager**: Warehouse supervisor with approval authority
- **Accountant**: Financial user managing valuations and adjustments
- **Agnum**: External accounting system receiving stock/valuation exports
- **Projection**: Read model derived from event stream
- **Saga**: Process manager coordinating multi-aggregate operations
- **Transactional_Outbox**: Pattern ensuring at-least-once event delivery
- **Idempotent**: Operation that produces same result when executed multiple times

## Requirements

### Requirement 1: Stock Movement Ledger

**User Story:** As a warehouse operator, I want all stock movements to be recorded in an immutable ledger, so that I have a complete audit trail and can query historical balances.

#### Acceptance Criteria

1. WHEN a stock movement is recorded, THE StockLedger SHALL append an immutable StockMoved event to the event stream
2. WHEN recording a movement from a physical location, THE StockLedger SHALL validate that the source location has sufficient balance before appending
3. WHEN recording a movement from a virtual location (SUPPLIER, SYSTEM), THE StockLedger SHALL skip balance validation
4. THE StockLedger SHALL be the ONLY module with permission to append StockMoved events
5. WHEN a movement is appended, THE StockLedger SHALL publish the StockMoved event via transactional outbox pattern
6. FOR ALL movements, THE System SHALL ensure from location ≠ to location
7. FOR ALL movements, THE System SHALL ensure quantity > 0
8. WHEN querying historical balance, THE System SHALL compute balance from event stream for any point in time
9. **[MITIGATION V-2]** WHEN recording a movement, THE StockLedger SHALL perform balance validation and event append atomically using optimistic concurrency control on the event stream
10. **[MITIGATION V-2]** WHEN a concurrency conflict occurs during movement recording, THE System SHALL retry the operation with exponential backoff (maximum 3 retries)
11. **[MITIGATION V-2]** WHEN retries are exhausted, THE System SHALL return a concurrency error to the caller

### Requirement 2: Handling Unit Lifecycle

**User Story:** As a warehouse operator, I want to create, modify, seal, and track handling units, so that I can manage physical containers throughout their lifecycle.

#### Acceptance Criteria

1. WHEN creating a handling unit, THE System SHALL assign a unique LPN identifier
2. WHEN adding a line to an OPEN handling unit, THE System SHALL update the contents
3. WHEN attempting to modify a SEALED handling unit, THE System SHALL reject the operation
4. WHEN sealing a handling unit, THE System SHALL validate that it contains at least one line
5. WHEN sealing an empty handling unit, THE System SHALL reject the operation
6. WHEN moving a handling unit, THE System SHALL record StockMovement events for each line via StockLedger
7. WHEN a StockMoved event is published, THE HandlingUnit projection SHALL update its state by subscribing to the event
8. FOR ALL handling units, THE System SHALL maintain exactly one current location
9. WHEN splitting a handling unit, THE System SHALL create a new HU and reduce the source HU quantity
10. WHEN merging handling units at the same location, THE System SHALL transfer lines to target HU and mark sources as EMPTY

### Requirement 3: Reservation System with Hybrid Locking

**User Story:** As a production planner, I want to reserve stock with flexible locking, so that I can plan ahead while ensuring execution safety.

#### Acceptance Criteria

1. WHEN creating a reservation, THE System SHALL initialize it in PENDING state
2. WHEN allocating stock to a reservation, THE System SHALL query StockLedger for current balance
3. WHEN allocating with SOFT lock, THE System SHALL allow overbooking (multiple reservations on same stock)
4. WHEN starting picking, THE System SHALL transition reservation from SOFT to HARD lock
5. WHEN transitioning to HARD lock, THE System SHALL re-validate that balance is sufficient
6. WHEN a SOFT reservation conflicts with a HARD lock request, THE System SHALL bump the SOFT reservation
7. WHEN a HARD reservation exists, THE System SHALL prevent other reservations from bumping it
8. WHEN a reservation is bumped, THE System SHALL notify the affected user
9. WHEN consuming a reservation, THE System SHALL mark it as CONSUMED after all quantity is picked
10. **[ARCHITECTURAL CORRECTION]** WHEN a HARD reservation is in PICKING state, THE System SHALL NOT auto-expire or auto-cancel it
11. **[ARCHITECTURAL CORRECTION]** WHEN a HARD reservation requires cancellation, THE System SHALL require manual release or explicit failure handling
12. **[ARCHITECTURAL CORRECTION]** WHEN an operator is offline with a HARD reservation, THE System SHALL guarantee the reservation remains valid until explicit cancellation
13. **[MITIGATION R-3]** WHEN starting picking (StartPicking command), THE System SHALL re-validate balance from the event stream (not projection)
14. **[MITIGATION R-3]** WHEN starting picking, THE System SHALL acquire HARD lock atomically using optimistic concurrency control on the Reservation stream
15. **[MITIGATION R-3]** WHEN a concurrency conflict occurs during StartPicking, THE System SHALL retry the operation with exponential backoff (maximum 3 retries)
16. **[MITIGATION R-3]** WHEN checking for HARD lock conflicts, THE System SHALL query the ActiveHardLocks projection for existing locks at the same location and SKU

### Requirement 4: Transaction Ordering for Pick Operations

**User Story:** As a system architect, I want pick operations to follow strict transaction ordering, so that inventory remains consistent even during failures.

#### Acceptance Criteria

1. WHEN picking stock, THE System SHALL record StockMovement via StockLedger BEFORE updating HandlingUnit
2. WHEN StockMovement is recorded, THE System SHALL commit the transaction before publishing events
3. **[MITIGATION V-3]** WHEN StockMoved event is published, THE HandlingUnit projection SHALL process it asynchronously (not as a saga step)
4. **[MITIGATION V-3]** WHEN HandlingUnit projection completes, THE Reservation consumption SHALL occur independently (not waiting for projection)
5. IF StockLedger transaction fails, THE System SHALL rollback with no partial state
6. IF HandlingUnit projection fails, THE System SHALL replay from StockMoved event
7. IF Reservation update fails, THE System SHALL retry consumption independently of projection status

### Requirement 5: Offline Edge Operations

**User Story:** As a warehouse operator, I want to perform safe operations while offline, so that I can continue working during network outages.

#### Acceptance Criteria

1. WHEN offline, THE System SHALL allow PickStock operations ONLY if reservation is already HARD locked on server
2. WHEN offline, THE System SHALL allow TransferStock operations ONLY for handling units already assigned to operator
3. WHEN offline, THE System SHALL reject AllocateReservation operations
4. WHEN offline, THE System SHALL reject StartPicking operations
5. WHEN offline, THE System SHALL reject AdjustStock operations
6. WHEN offline, THE System SHALL reject SplitHU and MergeHU operations
7. WHEN reconnecting after offline period, THE System SHALL sync queued commands to server
8. WHEN syncing offline commands, THE System SHALL reject commands for bumped reservations
9. WHEN sync fails for an offline command, THE System SHALL show error in reconciliation report

### Requirement 6: Read Model Projections

**User Story:** As a warehouse operator, I want to query current stock levels and locations quickly, so that I can make operational decisions in real-time.

#### Acceptance Criteria

1. WHEN StockMoved events are published, THE System SHALL update LocationBalance projection
2. WHEN querying location balance, THE System SHALL return projected balance if projection lag < 5 seconds
3. **[ARCHITECTURAL CORRECTION]** WHEN projection lag > 5 seconds, THE System SHALL display stale data indicator in UI
4. **[ARCHITECTURAL CORRECTION]** WHEN projection lag > 30 seconds, THE System SHALL alert operations team
5. **[ARCHITECTURAL CORRECTION]** WHEN projection rebuild is needed, THE System SHALL support manual or background replay
6. **[ARCHITECTURAL CORRECTION]** WHEN user queries balance, THE System SHALL NOT perform full event stream replay synchronously
7. THE System SHALL maintain AvailableStock projection (physical quantity - reserved quantity)
8. THE System SHALL maintain OnHandValue projection (quantity × unit cost)
9. THE System SHALL maintain HandlingUnitLocation projection from HU aggregate state
10. THE System SHALL maintain StockByCategory projection for Agnum export
11. FOR ALL projections, THE System SHALL support rebuild from event stream via background process
12. WHEN rebuilding projections, THE System SHALL replay all relevant events asynchronously
13. **[MITIGATION V-5]** WHEN rebuilding projections, THE System SHALL replay events in stream order (by sequence number, not timestamp)
14. **[MITIGATION V-5]** WHEN processing events during projection, THE System SHALL use only self-contained event data (no external queries)
15. **[MITIGATION V-5]** WHEN rebuilding projections, THE System SHALL use shadow table approach with checksum verification before swapping to production

### Requirement 7: Operator UI Flows

**User Story:** As a warehouse operator, I want simple UI flows for common operations, so that I can work efficiently with minimal training.

#### Acceptance Criteria

1. WHEN receiving goods, THE System SHALL guide operator through: scan barcode → assign location → generate label
2. WHEN transferring stock, THE System SHALL guide operator through: scan HU → scan destination → confirm
3. WHEN picking for production, THE System SHALL guide operator through: select reservation → scan HU → confirm quantity
4. WHEN a reservation is bumped, THE System SHALL display notification to operator
5. WHEN projection is updating, THE System SHALL display "Refreshing..." indicator
6. WHEN operation fails, THE System SHALL display clear error message with corrective action
7. WHEN offline, THE System SHALL disable forbidden operation buttons
8. WHEN reconnecting, THE System SHALL display reconciliation report for offline operations

### Requirement 8: Label Printing Integration

**User Story:** As a warehouse operator, I want labels to print automatically when handling units are created, so that I can immediately attach them to physical containers.

#### Acceptance Criteria

1. WHEN a handling unit is sealed, THE System SHALL send print command to label printer
2. WHEN label printing fails, THE System SHALL retry 3 times
3. WHEN retries are exhausted, THE System SHALL log error and alert operator
4. WHEN operator requests manual reprint, THE System SHALL send print command with same HU data
5. THE System SHALL ensure print commands are idempotent via PrintJobId
6. WHEN printer receives duplicate PrintJobId, THE System SHALL skip reprint
7. THE System SHALL complete label printing within 5 seconds (operational integration SLA)

### Requirement 9: Agnum Export Baseline

**User Story:** As an inventory accountant, I want to export stock snapshots to Agnum, so that financial records stay synchronized with physical inventory.

#### Acceptance Criteria

1. WHEN export is triggered, THE System SHALL query StockMovement ledger for current balances
2. WHEN computing export data, THE System SHALL query Valuation for unit costs
3. WHEN computing export data, THE System SHALL query LogicalWarehouse for category mappings
4. WHEN exporting, THE System SHALL apply configured mapping rules (warehouse → account)
5. THE System SHALL support export modes: by physical warehouse, by logical warehouse, by category, or total sum
6. WHEN export is sent to Agnum, THE System SHALL include unique ExportId for deduplication
7. WHEN Agnum API fails, THE System SHALL retry with exponential backoff
8. WHEN retries are exhausted after 3 attempts, THE System SHALL alert administrator
9. THE System SHALL complete export within minutes (financial integration SLA)
10. WHEN export completes, THE System SHALL record export timestamp

### Requirement 10: Valuation Management

**User Story:** As an inventory accountant, I want to adjust stock valuations independently from physical quantities, so that I can handle write-downs, landed costs, and revaluations.

#### Acceptance Criteria

1. WHEN applying cost adjustment, THE System SHALL require reason and approver
2. WHEN applying cost adjustment, THE System SHALL record immutable CostAdjusted event
3. WHEN allocating landed cost, THE System SHALL increase unit cost by specified amount
4. WHEN writing down stock, THE System SHALL reduce unit cost by specified percentage
5. THE System SHALL compute on-hand value as: physical quantity (from StockLedger) × unit cost (from Valuation)
6. FOR ALL cost adjustments, THE System SHALL maintain immutable history
7. WHEN querying historical cost, THE System SHALL replay valuation events to compute cost at any point in time

### Requirement 11: Warehouse Layout Configuration

**User Story:** As a warehouse manager, I want to define the physical warehouse layout, so that the system can validate locations and support 3D visualization.

#### Acceptance Criteria

1. WHEN defining a bin, THE System SHALL validate that 3D coordinates are unique
2. WHEN defining a bin, THE System SHALL validate that bins do not overlap in 3D space
3. WHEN creating a handling unit, THE System SHALL validate that the location exists in layout
4. WHEN moving a handling unit, THE System SHALL validate that the destination location exists
5. WHEN attempting to delete a bin with stock, THE System SHALL reject the operation
6. THE System SHALL enforce capacity constraints when placing handling units
7. THE System SHALL provide layout data for 3D warehouse visualization

### Requirement 12: Idempotency and Replay Safety

**User Story:** As a system architect, I want all operations to be idempotent, so that the system remains consistent during retries and event replays.

#### Acceptance Criteria

1. FOR ALL commands, THE System SHALL include unique CommandId
2. WHEN receiving a command, THE System SHALL check if CommandId was already processed
3. WHEN CommandId exists, THE System SHALL return cached result without re-executing
4. FOR ALL event handlers, THE System SHALL check if event was already processed
5. WHEN event was already processed, THE System SHALL skip processing (no-op)
6. FOR ALL projections, THE System SHALL use UPSERT operations to ensure replay safety
7. FOR ALL saga steps, THE System SHALL check if step was already executed before proceeding
8. THE System SHALL retain processed commands for 7 days
9. THE System SHALL retain event processing checkpoints indefinitely

### Requirement 13: Consistency Verification

**User Story:** As a warehouse manager, I want automated consistency checks, so that I can detect and correct inventory discrepancies early.

#### Acceptance Criteria

1. WHEN running daily consistency check, THE System SHALL verify LocationBalance matches computed balance from events
2. WHEN running daily consistency check, THE System SHALL verify no negative balances exist
3. WHEN running daily consistency check, THE System SHALL verify HU contents match LocationBalance
4. WHEN running daily consistency check, THE System SHALL verify no orphaned HUs at invalid locations
5. WHEN running daily consistency check, THE System SHALL verify consumed reservations have released all HUs
6. WHEN running daily consistency check, THE System SHALL verify event stream has no sequence gaps
7. WHEN consistency check fails, THE System SHALL alert with severity level (P0 for critical, P2 for warnings)
8. WHEN balance mismatch is detected, THE System SHALL provide rebuild projection tool
9. WHEN discrepancy is found, THE System SHALL provide cycle count wizard for manual reconciliation

### Requirement 14: Process Integration with ERP/MES

**User Story:** As a production planner, I want the warehouse to coordinate with ERP for material requests, so that production orders automatically trigger stock reservations.

#### Acceptance Criteria

1. WHEN ERP sends MaterialRequested event, THE System SHALL create a reservation
2. WHEN reservation is allocated, THE System SHALL send MaterialReserved event to ERP
3. WHEN stock is picked to PRODUCTION location, THE System SHALL send MaterialConsumed event to ERP
4. WHEN integration fails, THE System SHALL use saga compensation to notify both systems
5. THE System SHALL complete process integration within 30 seconds (process integration SLA)
6. THE System SHALL maintain anti-corruption layer to translate between ERP and warehouse domain models
7. **[ARCHITECTURAL CORRECTION]** WHEN reservation is created but ERP notification fails, THE System SHALL retry notification with exponential backoff
8. **[ARCHITECTURAL CORRECTION]** WHEN ERP notification retries are exhausted, THE System SHALL log compensation event and alert administrator
9. **[ARCHITECTURAL CORRECTION]** WHEN material is consumed but ERP acknowledgment fails, THE System SHALL queue consumption event for retry
10. **[ARCHITECTURAL CORRECTION]** WHEN ERP acknowledgment retries are exhausted, THE System SHALL maintain eventual consistency via reconciliation process
11. **[ARCHITECTURAL CORRECTION]** THE System SHALL guarantee at-least-once delivery of MaterialConsumed events to ERP
12. **[ARCHITECTURAL CORRECTION]** THE ERP SHALL implement idempotent handling of MaterialConsumed events via unique event identifiers

### Requirement 15: Goods Receipt Workflow

**User Story:** As a warehouse operator, I want to receive goods from suppliers, so that incoming inventory is recorded and labeled.

#### Acceptance Criteria

1. WHEN receiving goods, THE System SHALL record StockMovement from SUPPLIER to warehouse location
2. WHEN StockMovement is recorded, THE System SHALL create a handling unit at the destination location
3. WHEN handling unit is created, THE System SHALL add lines for received SKUs
4. WHEN lines are added, THE System SHALL seal the handling unit
5. WHEN handling unit is sealed, THE System SHALL request label printing
6. **[ARCHITECTURAL CORRECTION]** IF handling unit creation fails after movement recorded, THE System SHALL create orphan movement alert
7. **[ARCHITECTURAL CORRECTION]** WHEN orphan movement is detected, THE System SHALL provide manual reconciliation workflow
8. **[ARCHITECTURAL CORRECTION]** WHEN reconciling orphan movement, THE System SHALL allow operator to create HU retroactively or adjust inventory
9. THE System SHALL coordinate receipt workflow via ReceiveGoodsSaga

### Requirement 16: Transfer Workflow

**User Story:** As a warehouse operator, I want to transfer handling units between locations, so that I can reorganize inventory.

#### Acceptance Criteria

1. WHEN transferring a handling unit, THE System SHALL validate destination location exists
2. WHEN transferring, THE System SHALL record StockMovement for each line in the handling unit
3. WHEN all movements are recorded, THE System SHALL update handling unit location
4. IF any movement fails validation, THE System SHALL abort transfer without updating HU location
5. THE System SHALL coordinate transfer workflow via TransferStockSaga

### Requirement 17: Pick Workflow

**User Story:** As a warehouse operator, I want to pick stock against reservations, so that I can fulfill production orders.

#### Acceptance Criteria

1. WHEN picking stock, THE System SHALL validate reservation is in PICKING state (HARD locked)
2. WHEN picking, THE System SHALL validate handling unit is allocated to the reservation
3. **[ARCHITECTURAL CORRECTION]** WHEN picking, THE System SHALL record StockMovement to PRODUCTION location via StockLedger FIRST
4. **[ARCHITECTURAL CORRECTION]** WHEN StockMovement is recorded, THE System SHALL publish StockMoved event via transactional outbox
5. **[ARCHITECTURAL CORRECTION]** WHEN StockMoved event is published, THE HandlingUnit projection SHALL process event and remove line from handling unit
6. **[ARCHITECTURAL CORRECTION]** WHEN HandlingUnit projection completes, THE Reservation SHALL update consumption
7. **[ARCHITECTURAL CORRECTION]** THE System SHALL NOT mutate HandlingUnit state directly during pick operation
8. WHEN reservation is fully consumed, THE System SHALL mark it as CONSUMED
9. THE System SHALL coordinate pick workflow via PickStockSaga
10. **[ARCHITECTURAL CORRECTION]** IF StockLedger transaction fails, THE System SHALL rollback with no partial state
11. **[ARCHITECTURAL CORRECTION]** IF HandlingUnit projection fails, THE System SHALL replay from StockMoved event
12. **[ARCHITECTURAL CORRECTION]** IF Reservation update fails, THE System SHALL retry consumption after HU projection completes

### Requirement 18: Split and Merge Operations

**User Story:** As a warehouse operator, I want to split and merge handling units, so that I can adjust container sizes for operational needs.

#### Acceptance Criteria

1. WHEN splitting a handling unit, THE System SHALL validate source HU is not SEALED
2. WHEN splitting, THE System SHALL create new HU at same location as source
3. WHEN splitting, THE System SHALL reduce source HU quantity
4. **[ARCHITECTURAL CORRECTION]** WHEN splitting, THE System SHALL emit HandlingUnitSplit domain event for audit trail
5. **[ARCHITECTURAL CORRECTION]** WHEN splitting at same location, THE System SHALL NOT record StockMovement event
6. WHEN merging handling units, THE System SHALL validate all HUs are at same location
7. WHEN merging, THE System SHALL validate target HU is not SEALED
8. WHEN merging, THE System SHALL transfer lines from source HUs to target HU
9. WHEN merging, THE System SHALL mark source HUs as EMPTY
10. **[ARCHITECTURAL CORRECTION]** WHEN merging, THE System SHALL emit HandlingUnitMerged domain event for audit trail
11. **[ARCHITECTURAL CORRECTION]** WHEN merging at same location, THE System SHALL NOT record StockMovement event
12. **[ARCHITECTURAL CORRECTION]** FOR ALL split and merge operations, THE System SHALL maintain complete audit trail via domain events

### Requirement 19: ActiveHardLocks Read Model

**User Story:** As a system architect, I want to efficiently query active HARD locks, so that StartPicking can detect conflicts without scanning all reservations.

**[MITIGATION R-4]** This requirement addresses the need for efficient HARD lock conflict detection during StartPicking operations.

#### Acceptance Criteria

1. WHEN a reservation transitions to PICKING state (HARD lock), THE System SHALL insert a row into ActiveHardLocks projection
2. WHEN a HARD locked reservation is consumed, THE System SHALL delete the corresponding row from ActiveHardLocks projection
3. WHEN a HARD locked reservation is cancelled, THE System SHALL delete the corresponding row from ActiveHardLocks projection
4. THE ActiveHardLocks projection SHALL be updated atomically in the same transaction as the Reservation event (inline projection)
5. WHEN querying for HARD lock conflicts, THE System SHALL query ActiveHardLocks for matching location and SKU
6. THE ActiveHardLocks projection SHALL contain: location, SKU, reservation_id, hard_locked_qty, started_at
7. FOR ALL HARD locked reservations, THE ActiveHardLocks projection SHALL have exactly one corresponding row
8. WHEN rebuilding ActiveHardLocks projection, THE System SHALL scan all Reservation streams for PICKING state reservations

---

## Special Requirements Guidance

### Parser and Serializer Requirements

This system does not include custom parsers or serializers beyond standard JSON/database serialization. Event serialization uses standard JSON with schema versioning for evolution.

### Round-Trip Properties

- Event serialization: Serialize event → Deserialize → Should produce equivalent event
- Projection rebuild: Record events → Build projection → Rebuild from events → Should produce same projection state
- Command idempotency: Execute command → Store result → Replay command → Should return cached result

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-07 | Initial requirements for Phase 1 |

---

**End of Requirements Document**
