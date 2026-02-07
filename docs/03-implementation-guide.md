# LKvitai.MES - Warehouse Implementation Guide

**Project:** Modular Manufacturing Automation System (MES/ERP-light)  
**Module:** Warehouse Management  
**Date:** February 2026  
**Version:** 1.0  
**Status:** Implementation Specification

---

## Document Purpose

This guide provides implementation specifications for the Warehouse domain model. It defines:
- Interaction diagrams for key workflows
- Command catalog with handlers
- Process managers and sagas
- Read model projections
- Consistency guarantees
- Idempotency strategy
- Integration patterns

**Prerequisites:** Read 02-warehouse-domain-model-v1.md first.

**Scope:** Domain logic and patterns only - no infrastructure or framework specifics.

---

## Table of Contents

1. Interaction Diagrams
2. Command Catalog
3. Process Managers and Sagas
4. Read Model Projections
5. Consistency Model
6. Idempotency Strategy
7. Integration Patterns

---

## 1. Interaction Diagrams

### 1.1 Receive Goods

**Scenario:** Operator scans barcode on incoming pallet from supplier.

```
┌─────────┐      ┌──────────────────┐      ┌─────────────────┐      ┌──────────────┐
│Operator │      │ReceiveGoodsCommand│      │HandlingUnit     │      │StockMovement │
│         │      │Handler            │      │Aggregate        │      │Ledger        │
└────┬────┘      └────────┬──────────┘      └────────┬────────┘      └──────┬───────┘
     │                    │                          │                       │
     │ ReceiveGoods(      │                          │                       │
     │   SKU, qty,        │                          │                       │
     │   supplier,        │                          │                       │
     │   location)        │                          │                       │
     ├───────────────────>│                          │                       │
     │                    │                          │                       │
     │                    │ 1. Record Movement       │                       │
     │                    │    (SUPPLIER → location) │                       │
     │                    ├─────────────────────────────────────────────────>│
     │                    │                          │                       │
     │                    │                          │                       │ Validate
     │                    │                          │                       │ (source = SUPPLIER,
     │                    │                          │                       │  no balance check)
     │                    │                          │                       │
     │                    │<─────────────────────────────────────────────────┤
     │                    │ StockMoved event         │                       │
     │                    │                          │                       │
     │                    │ 2. Create HandlingUnit   │                       │
     │                    │    (OPEN, at location)   │                       │
     │                    ├─────────────────────────>│                       │
     │                    │                          │                       │
     │                    │                          │ Create HU             │
     │                    │                          │ AddLine(SKU, qty)     │
     │                    │                          │ Seal()                │
     │                    │                          │                       │
     │                    │<─────────────────────────┤                       │
     │                    │ HandlingUnitCreated      │                       │
     │                    │ LineAdded                │                       │
     │                    │ HandlingUnitSealed       │                       │
     │                    │                          │                       │
     │                    │ 3. Print Label           │                       │
     │                    ├─────────────────────────>│                       │
     │                    │    (send to printer)     │                       │
     │                    │                          │                       │
     │<───────────────────┤                          │                       │
     │ Success            │                          │                       │
     │ (HU id, label)     │                          │                       │
     │                    │                          │                       │
```

**Key Points:**
1. StockMovement recorded first (source of truth)
2. HandlingUnit created after movement (can be compensated)
3. HU automatically sealed (ready for storage)
4. Label printed for operator to attach

**Compensation:** If HU creation fails, StockMovement already recorded (correct). Physical inventory will show discrepancy → cycle count will correct.

---

### 1.2 Transfer Between Bins

**Scenario:** Operator moves pallet from A1-B1 to R3-C6-L3B3.

```
┌─────────┐      ┌──────────────────┐      ┌─────────────────┐      ┌──────────────┐
│Operator │      │TransferStockCommand│    │HandlingUnit     │      │StockMovement │
│         │      │Handler             │    │Aggregate        │      │Ledger        │
└────┬────┘      └────────┬───────────┘     └────────┬────────┘      └──────┬───────┘
     │                    │                          │                       │
     │ TransferStock(     │                          │                       │
     │   HU id,           │                          │                       │
     │   to location)     │                          │                       │
     ├───────────────────>│                          │                       │
     │                    │                          │                       │
     │                    │ 1. Get HandlingUnit      │                       │
     │                    ├─────────────────────────>│                       │
     │                    │                          │                       │
     │                    │<─────────────────────────┤                       │
     │                    │ HU (with Lines)          │                       │
     │                    │                          │                       │
     │                    │ 2. For each Line in HU:  │                       │
     │                    │    Record Movement       │                       │
     │                    │    (from → to)           │                       │
     │                    ├─────────────────────────────────────────────────>│
     │                    │                          │                       │
     │                    │                          │                       │ Validate
     │                    │                          │                       │ (from has
     │                    │                          │                       │  sufficient
     │                    │                          │                       │  balance)
     │                    │                          │                       │
     │                    │<─────────────────────────────────────────────────┤
     │                    │ StockMoved events (N)    │                       │
     │                    │                          │                       │
     │                    │ 3. Update HU location    │                       │
     │                    ├─────────────────────────>│                       │
     │                    │                          │                       │
     │                    │                          │ MoveTo(new location)  │
     │                    │                          │                       │
     │                    │<─────────────────────────┤                       │
     │                    │ HandlingUnitMoved        │                       │
     │                    │                          │                       │
     │<───────────────────┤                          │                       │
     │ Success            │                          │                       │
     │                    │                          │                       │
```

**Key Points:**
1. Multiple StockMovement records (one per SKU line in HU)
2. Each movement is atomic (single FROM→TO fact)
3. HU location updated after all movements recorded
4. Process is orchestrated by command handler

**Failure Handling:**
- If any movement fails → rollback HU location update
- StockMovement ledger remains consistent (balance check failed)
- Operator sees error, retries

---

### 1.3 Pick for Production

**Scenario:** Operator picks material against reservation for production order.

```
┌─────────┐  ┌─────────────┐  ┌───────────┐  ┌─────────────┐  ┌──────────────┐
│Operator │  │PickStockCmd │  │Reservation│  │HandlingUnit │  │StockMovement │
│         │  │Handler      │  │Aggregate  │  │Aggregate    │  │Ledger        │
└────┬────┘  └──────┬──────┘  └─────┬─────┘  └──────┬──────┘  └──────┬───────┘
     │              │                │               │                 │
     │ PickStock(   │                │               │                 │
     │   reservation│                │               │                 │
     │   HU id,     │                │               │                 │
     │   qty)       │                │               │                 │
     ├─────────────>│                │               │                 │
     │              │                │               │                 │
     │              │ 1. Get Reservation             │                 │
     │              ├───────────────>│               │                 │
     │              │                │               │                 │
     │              │<───────────────┤               │                 │
     │              │ Reservation    │               │                 │
     │              │ (must be PICKING state)        │                 │
     │              │                │               │                 │
     │              │ 2. Get HandlingUnit            │                 │
     │              ├───────────────────────────────>│                 │
     │              │                │               │                 │
     │              │<───────────────────────────────┤                 │
     │              │ HU (must be allocated to       │                 │
     │              │     this reservation)          │                 │
     │              │                │               │                 │
     │              │ 3. Remove from HU              │                 │
     │              ├───────────────────────────────>│                 │
     │              │                │               │                 │
     │              │                │               │ RemoveLine      │
     │              │                │               │ (SKU, qty)      │
     │              │                │               │                 │
     │              │<───────────────────────────────┤                 │
     │              │ LineRemovedFromHandlingUnit    │                 │
     │              │                │               │                 │
     │              │ 4. Record Movement             │                 │
     │              │    (HU location → PRODUCTION)  │                 │
     │              ├───────────────────────────────────────────────>│
     │              │                │               │                 │
     │              │<───────────────────────────────────────────────┤
     │              │ StockMoved     │               │                 │
     │              │                │               │                 │
     │              │ 5. Update Reservation          │                 │
     │              ├───────────────>│               │                 │
     │              │                │               │                 │
     │              │                │ Consume(qty)  │                 │
     │              │                │               │                 │
     │              │<───────────────┤               │                 │
     │              │ ReservationConsumed (if all picked)              │
     │              │                │               │                 │
     │<─────────────┤                │               │                 │
     │ Success      │                │               │                 │
     │              │                │               │                 │
```

**Key Points:**
1. Reservation must be in PICKING state (HARD lock)
2. HU must be allocated to this reservation
3. RemoveLine from HU (may empty it)
4. StockMovement to PRODUCTION virtual location
5. Reservation marked consumed when fully picked

**Failure Handling:**
- If reservation not PICKING → error (must call StartPicking first)
- If HU not allocated → error (allocation conflict)
- If insufficient quantity in HU → error

---

### 1.4 Split Handling Unit

**Scenario:** Operator breaks a pallet into two smaller units.

```
┌─────────┐      ┌──────────────┐      ┌─────────────────┐      ┌──────────────┐
│Operator │      │SplitHUCommand│      │HandlingUnit     │      │StockMovement │
│         │      │Handler       │      │Aggregate        │      │Ledger        │
└────┬────┘      └──────┬───────┘      └────────┬────────┘      └──────┬───────┘
     │                  │                       │                       │
     │ SplitHU(         │                       │                       │
     │   source HU,     │                       │                       │
     │   SKU,           │                       │                       │
     │   qty to split)  │                       │                       │
     ├─────────────────>│                       │                       │
     │                  │                       │                       │
     │                  │ 1. Get Source HU      │                       │
     │                  ├──────────────────────>│                       │
     │                  │                       │                       │
     │                  │<──────────────────────┤                       │
     │                  │ Source HU             │                       │
     │                  │                       │                       │
     │                  │ 2. Validate:          │                       │
     │                  │    - HU not SEALED    │                       │
     │                  │    - Has SKU with     │                       │
     │                  │      sufficient qty   │                       │
     │                  │                       │                       │
     │                  │ 3. Create New HU      │                       │
     │                  │    (same location)    │                       │
     │                  ├──────────────────────>│                       │
     │                  │                       │                       │
     │                  │                       │ Create(location)      │
     │                  │                       │ AddLine(SKU, qty)     │
     │                  │                       │                       │
     │                  │<──────────────────────┤                       │
     │                  │ New HU created        │                       │
     │                  │                       │                       │
     │                  │ 4. Remove from Source │                       │
     │                  ├──────────────────────>│                       │
     │                  │                       │                       │
     │                  │                       │ RemoveLine(SKU, qty)  │
     │                  │                       │                       │
     │                  │<──────────────────────┤                       │
     │                  │ Line removed          │                       │
     │                  │                       │                       │
     │                  │ 5. Record Movement    │                       │
     │                  │    (conceptual:       │                       │
     │                  │     Source HU → New HU)                       │
     │                  │                       │                       │
     │                  │ Note: StockMovement not recorded for split    │
     │                  │       because both HUs at same location       │
     │                  │       Location balances unchanged             │
     │                  │                       │                       │
     │                  │ 6. Print Label for New HU                     │
     │                  │                       │                       │
     │<─────────────────┤                       │                       │
     │ Success          │                       │                       │
     │ (New HU id)      │                       │                       │
     │                  │                       │                       │
```

**Key Points:**
1. Split does NOT create StockMovement (same location)
2. New HU created at same location as source
3. Source HU quantity reduced
4. Both HUs remain at original location
5. Operator must label new HU

**Design Rationale:**
- StockMovement tracks location changes, not container changes
- Location balance unchanged (same SKU, same location, same total qty)
- Audit trail: HandlingUnitCreated + LineRemovedFromHandlingUnit events

---

### 1.5 Merge Handling Units

**Scenario:** Operator consolidates two boxes into one pallet.

```
┌─────────┐      ┌──────────────┐      ┌─────────────────┐      ┌──────────────┐
│Operator │      │MergeHUsCommand│     │HandlingUnit     │      │StockMovement │
│         │      │Handler        │     │Aggregate        │      │Ledger        │
└────┬────┘      └──────┬────────┘     └────────┬────────┘      └──────┬───────┘
     │                  │                       │                       │
     │ MergeHUs(        │                       │                       │
     │   source HUs[],  │                       │                       │
     │   target HU)     │                       │                       │
     ├─────────────────>│                       │                       │
     │                  │                       │                       │
     │                  │ 1. Get All HUs        │                       │
     │                  ├──────────────────────>│                       │
     │                  │                       │                       │
     │                  │<──────────────────────┤                       │
     │                  │ Source HUs + Target HU│                       │
     │                  │                       │                       │
     │                  │ 2. Validate:          │                       │
     │                  │    - All at same location                     │
     │                  │    - Target not SEALED│                       │
     │                  │                       │                       │
     │                  │ 3. For each Source HU:│                       │
     │                  │    Transfer Lines to  │                       │
     │                  │    Target HU          │                       │
     │                  │                       │                       │
     │                  │    For each Line:     │                       │
     │                  ├──────────────────────>│                       │
     │                  │                       │                       │
     │                  │                       │ Target.AddLine(SKU,   │
     │                  │                       │               qty)    │
     │                  │                       │ Source.RemoveLine     │
     │                  │                       │        (SKU, qty)     │
     │                  │                       │                       │
     │                  │<──────────────────────┤                       │
     │                  │ Lines transferred     │                       │
     │                  │                       │                       │
     │                  │ 4. Mark Source HUs    │                       │
     │                  │    as EMPTY           │                       │
     │                  ├──────────────────────>│                       │
     │                  │                       │                       │
     │                  │                       │ Status → EMPTY        │
     │                  │                       │                       │
     │                  │<──────────────────────┤                       │
     │                  │ HandlingUnitEmptied   │                       │
     │                  │                       │                       │
     │                  │ Note: No StockMovement (same location)        │
     │                  │                       │                       │
     │<─────────────────┤                       │                       │
     │ Success          │                       │                       │
     │                  │                       │                       │
```

**Key Points:**
1. All HUs must be at same location
2. Lines transferred to target HU
3. Source HUs marked EMPTY (can be discarded)
4. No StockMovement recorded (location unchanged)
5. Audit trail via HU events

**Design Rationale:**
- Merge is a container operation, not a location operation
- StockMovement unchanged because location balances unchanged
- Empty HUs can be cleaned up by background process

---

## 2. Command Catalog

### HandlingUnit Commands

| Command | Parameters | Preconditions | Postconditions | Events Emitted |
|---------|-----------|---------------|----------------|----------------|
| **CreateHandlingUnit** | Type, Location, OperatorId | Location exists | HU created (OPEN) | HandlingUnitCreated |
| **AddLineToHandlingUnit** | HU Id, SKU, Quantity | HU exists, Status != SEALED | Line added or qty increased | LineAddedToHandlingUnit |
| **RemoveLineFromHandlingUnit** | HU Id, SKU, Quantity | HU exists, Status != SEALED, Line has sufficient qty | Line removed or qty decreased | LineRemovedFromHandlingUnit, HandlingUnitEmptied (if last line) |
| **SealHandlingUnit** | HU Id | HU exists, Status == OPEN, Has lines | Status → SEALED | HandlingUnitSealed |
| **MoveHandlingUnit** | HU Id, To Location | HU exists, Location exists, HU has stock | Location changed, StockMovements recorded | HandlingUnitMoved, StockMoved (N times) |
| **SplitHandlingUnit** | Source HU Id, SKU, Quantity | Source HU exists, Status != SEALED, Has SKU with qty | New HU created, Source qty reduced | HandlingUnitCreated, LineAddedToHandlingUnit, LineRemovedFromHandlingUnit |
| **MergeHandlingUnits** | Source HU Ids[], Target HU Id | All HUs at same location, Target != SEALED | Lines transferred, Sources → EMPTY | LineAddedToHandlingUnit, LineRemovedFromHandlingUnit, HandlingUnitEmptied |

### StockMovement Commands

| Command | Parameters | Preconditions | Postconditions | Events Emitted |
|---------|-----------|---------------|----------------|----------------|
| **RecordStockMovement** | SKU, Quantity, From Location, To Location, Type, Operator, Reason, HU Id (optional) | From location has sufficient balance (except for RECEIPT/ADJUSTMENT), From ≠ To | Movement appended to ledger | StockMoved |

### Reservation Commands

| Command | Parameters | Preconditions | Postconditions | Events Emitted |
|---------|-----------|---------------|----------------|----------------|
| **CreateReservation** | Purpose, Priority, Requested Quantity (per SKU) | None | Reservation created (PENDING) | ReservationCreated |
| **AllocateReservation** | Reservation Id, HU Ids[] | Reservation exists, Status == PENDING, HUs available | HUs allocated (SOFT lock), Status → ALLOCATED | StockAllocated |
| **StartPicking** | Reservation Id | Reservation exists, Status == ALLOCATED | Lock → HARD, Status → PICKING | PickingStarted |
| **ConsumeReservation** | Reservation Id, Actual Quantity | Reservation exists, Status == PICKING | Qty consumed, Status → CONSUMED (if complete) | ReservationConsumed |
| **CancelReservation** | Reservation Id, Reason | Reservation exists, Status != CONSUMED | HUs released, Status → CANCELLED | ReservationCancelled |
| **BumpReservation** | Reservation Id (to bump), Bumping Reservation Id | Target exists, Target is SOFT locked | Target status → BUMPED | ReservationBumped |

### Valuation Commands

| Command | Parameters | Preconditions | Postconditions | Events Emitted |
|---------|-----------|---------------|----------------|----------------|
| **ApplyCostAdjustment** | SKU, New Unit Cost, Reason, Approver | Valuation exists | Unit cost updated | CostAdjusted |
| **AllocateLandedCost** | SKU, Amount, Reason | Valuation exists | Unit cost increased | LandedCostAllocated |
| **WriteDownStock** | SKU, Percentage, Reason, Approver | Valuation exists | Unit cost reduced | StockWrittenDown |

### WarehouseLayout Commands

| Command | Parameters | Preconditions | Postconditions | Events Emitted |
|---------|-----------|---------------|----------------|----------------|
| **DefineBin** | Warehouse, Aisle, Rack, Bin Code, Coordinates, Capacity | Coordinates unique | Bin created | BinDefined |
| **ModifyBin** | Bin Code, New Capacity | Bin exists | Capacity updated | BinModified |
| **RemoveBin** | Bin Code | Bin exists, No stock at bin | Bin deleted | BinRemoved |

### LogicalWarehouse Commands

| Command | Parameters | Preconditions | Postconditions | Events Emitted |
|---------|-----------|---------------|----------------|----------------|
| **AssignCategory** | SKU, Category, Logical WH | LogicalWH exists | SKU categorized | CategoryAssigned |
| **RemoveCategory** | SKU, Category | Assignment exists | SKU uncategorized | CategoryRemoved |

---

## 3. Process Managers and Sagas

### 3.1 ReceiveGoodsProcessManager

**Purpose:** Orchestrates goods receipt workflow.

**Triggered By:** `ReceiveGoodsCommand`

**Steps:**
1. Validate command (SKU exists, location exists)
2. Record StockMovement (SUPPLIER → location)
3. Create HandlingUnit (at location)
4. Add lines to HU (from command)
5. Seal HU
6. Request label print
7. Publish success event

**Compensation:**
- If HU creation fails after movement recorded → StockMovement remains (correct)
- Physical inventory shows extra stock → next cycle count corrects via Adjustment

**State:**
```
{
  commandId: guid,
  sku: string,
  quantity: number,
  location: string,
  movementId: guid,
  handlingUnitId: guid,
  status: 'MovementRecorded' | 'HUCreated' | 'Completed' | 'Failed'
}
```

---

### 3.2 TransferStockProcessManager

**Purpose:** Orchestrates transfer of HU between locations.

**Triggered By:** `TransferStockCommand`

**Steps:**
1. Validate command (HU exists, target location exists)
2. Get HU with all lines
3. For each line: Record StockMovement (from location → to location)
4. Update HU location
5. Publish success event

**Compensation:**
- If any StockMovement fails → abort, do not update HU location
- Rollback: None needed (StockMovement validates balance before append)

**State:**
```
{
  commandId: guid,
  handlingUnitId: guid,
  fromLocation: string,
  toLocation: string,
  lines: [{sku, qty}],
  movementsRecorded: number,
  status: 'MovementsRecording' | 'HUUpdating' | 'Completed' | 'Failed'
}
```

---

### 3.3 PickStockProcessManager

**Purpose:** Orchestrates picking against reservation.

**Triggered By:** `PickStockCommand`

**Steps:**
1. Validate command (reservation exists and is PICKING)
2. Validate HU is allocated to this reservation
3. Remove line from HU
4. Record StockMovement (HU location → PRODUCTION)
5. Update reservation consumption
6. If reservation fully consumed → mark CONSUMED

**Compensation:**
- If StockMovement fails after RemoveLine → inventory discrepancy
- Recovery: Cycle count will detect missing stock at location
- Operator notified to investigate

**State:**
```
{
  commandId: guid,
  reservationId: guid,
  handlingUnitId: guid,
  sku: string,
  quantity: number,
  status: 'LineRemoved' | 'MovementRecorded' | 'ReservationUpdated' | 'Completed' | 'Failed'
}
```

---

### 3.4 AllocationProcessManager

**Purpose:** Allocates available HUs to reservation.

**Triggered By:** `ReservationCreated` event

**Steps:**
1. Query available stock (LocationBalance - Reserved)
2. Find HUs matching requested SKUs
3. Allocate HUs to reservation (SOFT lock)
4. Publish `StockAllocated` event

**Compensation:**
- If allocation fails (insufficient stock) → notify user
- Reservation remains in PENDING state
- Can retry allocation later

**State:**
```
{
  reservationId: guid,
  requestedLines: [{sku, qty}],
  allocatedHUs: [guid],
  status: 'Searching' | 'Allocating' | 'Completed' | 'Failed'
}
```

---

### 3.5 AgnumExportSaga

**Purpose:** Exports stock snapshot to Agnum accounting system.

**Triggered By:** `ExportToAgnumCommand` (scheduled or manual)

**Steps:**
1. Query StockMovement ledger for current balances (per location, per SKU)
2. Query Valuation for unit costs
3. Compute on-hand values (balance × cost)
4. Query LogicalWarehouse for category mappings
5. Apply Agnum mapping rules (which logical WH → which account)
6. Transform to Agnum format (CSV or API payload)
7. Send to Agnum API
8. Record export timestamp
9. Mark as completed

**Compensation:**
- If Agnum API fails → retry with exponential backoff
- If retry fails 3 times → alert administrator
- Saga remains in retry state until success

**Idempotency:**
- Export includes timestamp + unique export ID
- Agnum should deduplicate by export ID

**State:**
```
{
  exportId: guid,
  timestamp: datetime,
  scope: 'ByWarehouse' | 'ByCategory' | 'Total',
  snapshotData: [{sku, location, qty, cost, value, category}],
  status: 'Gathering' | 'Mapping' | 'Sending' | 'Completed' | 'Failed',
  retryCount: number
}
```

---

## 4. Read Model Projections

### 4.1 LocationBalance

**Purpose:** Current stock level per location per SKU.

**Source Events:** `StockMoved`

**Projection Logic:**
```
ON StockMoved(sku, qty, from, to):
  IF from is not virtual:
    LocationBalance[from][sku] -= qty
  
  IF to is not virtual:
    LocationBalance[to][sku] += qty
```

**Schema:**
```
LocationBalance {
  location: string,
  sku: string,
  quantity: number,
  lastUpdated: datetime
}
```

**Queries:**
- Get balance at location for SKU
- Get all SKUs at location
- Get all locations with SKU

---

### 4.2 AvailableStock

**Purpose:** Stock available for allocation (not reserved).

**Source Events:** `StockMoved`, `StockAllocated`, `ReservationConsumed`, `ReservationCancelled`

**Projection Logic:**
```
ON StockMoved:
  Update LocationBalance (see above)

ON StockAllocated(reservationId, HUs, lockType):
  FOR each HU:
    FOR each line in HU:
      ReservedQuantity[location][sku] += line.qty

ON ReservationConsumed(reservationId):
  FOR each allocated HU:
    FOR each line:
      ReservedQuantity[location][sku] -= line.qty

ON ReservationCancelled(reservationId):
  (same as ReservationConsumed - release reservation)
```

**Schema:**
```
AvailableStock {
  location: string,
  sku: string,
  physicalQuantity: number,
  reservedQuantity: number,
  availableQuantity: number (computed: physical - reserved)
}
```

**Queries:**
- Get available stock for SKU across all locations
- Find locations with available stock > threshold

---

### 4.3 HandlingUnitLocation

**Purpose:** Current location and contents of each HU.

**Source:** Direct read from `HandlingUnit` aggregate (state-based)

**Schema:**
```
HandlingUnitLocation {
  handlingUnitId: guid,
  type: string,
  status: string,
  location: string,
  lines: [{ sku: string, quantity: number }],
  createdAt: datetime,
  sealedAt: datetime
}
```

**Queries:**
- Get HU by ID
- Find HUs at location
- Find HUs containing SKU

---

### 4.4 OnHandValue

**Purpose:** Financial value of stock on hand.

**Source Events:** `StockMoved`, `CostAdjusted`

**Projection Logic:**
```
ON StockMoved:
  Update LocationBalance

ON CostAdjusted(sku, newCost):
  Update UnitCost[sku]

ON Query:
  FOR each location, sku:
    OnHandValue[location][sku] = LocationBalance[location][sku] × UnitCost[sku]
```

**Schema:**
```
OnHandValue {
  location: string,
  sku: string,
  quantity: number,
  unitCost: decimal,
  totalValue: decimal (computed)
}
```

**Queries:**
- Get total on-hand value (sum across all locations)
- Get on-hand value by location
- Get on-hand value by category

---

### 4.5 StockByCategory

**Purpose:** Stock grouped by logical warehouse and category.

**Source Events:** `StockMoved`, `CategoryAssigned`, `CategoryRemoved`

**Projection Logic:**
```
ON StockMoved:
  Update LocationBalance

ON CategoryAssigned(sku, category):
  CategoryMapping[sku].add(category)

ON CategoryRemoved(sku, category):
  CategoryMapping[sku].remove(category)

ON Query:
  FOR each category:
    FOR each sku in category:
      StockByCategory[category][sku] = SUM(LocationBalance[*][sku])
```

**Schema:**
```
StockByCategory {
  category: string,
  sku: string,
  totalQuantity: number,
  locations: [{ location: string, quantity: number }]
}
```

**Queries:**
- Get stock by category
- Get categories for SKU

---

### 4.6 WarehouseVisualization3D

**Purpose:** Data for 3D warehouse view.

**Source:** 
- `WarehouseLayout` (bin definitions)
- `HandlingUnitLocation` (HU positions)
- `AvailableStock` (status colors)

**Projection Logic:**
```
ON BinDefined:
  Add bin to 3D model

ON HandlingUnitMoved:
  Update HU position in 3D model

ON Query:
  FOR each bin:
    Get HUs at bin
    Determine color:
      - Green: FULL (stock > 80% capacity)
      - Yellow: LOW (stock 20-80% capacity)
      - Red: EMPTY (stock < 20% capacity)
      - Orange: RESERVED (has reservations)
```

**Schema:**
```
WarehouseVisualization3D {
  bins: [{
    code: string,
    coordinates: {x, y, z},
    capacity: number,
    handlingUnits: [guid],
    status: 'FULL' | 'LOW' | 'EMPTY' | 'RESERVED',
    color: string
  }]
}
```

**Queries:**
- Get full 3D model
- Get bin details by code
- Find nearest bin with space

---

### 4.7 MovementAuditTrail

**Purpose:** Complete audit log of all movements.

**Source Events:** `StockMoved`

**Projection Logic:**
```
ON StockMoved:
  Append to audit trail (no aggregation)
```

**Schema:**
```
MovementAuditTrail {
  movementId: guid,
  timestamp: datetime,
  sku: string,
  quantity: number,
  fromLocation: string,
  toLocation: string,
  type: string,
  operatorId: guid,
  handlingUnitId: guid (optional),
  reason: string (optional)
}
```

**Queries:**
- Get movements for SKU
- Get movements by operator
- Get movements in date range
- Get movements for HU

---

## 5. Consistency Model

### 5.1 Consistency Guarantees

| Operation | Guarantee | Implementation |
|-----------|-----------|----------------|
| **Record Movement** | Immediate consistency | Single transaction appends to event stream |
| **Location Balance Query** | Eventual consistency (< 1 sec) | Projected from StockMoved events |
| **HU Location** | Immediate consistency | Direct read from HU aggregate |
| **Available Stock** | Eventual consistency (< 1 sec) | Projected from StockMoved + Reservation events |
| **On-Hand Value** | Eventual consistency (< 5 sec) | Joins LocationBalance + Valuation |
| **Reservation Allocation** | Eventual consistency (< 2 sec) | Process manager orchestrates |
| **Agnum Export** | Eventual consistency (minutes) | Scheduled batch or on-demand |

### 5.2 Conflict Resolution

#### Concurrent Reservations

**Scenario:** Two reservations allocate same HU simultaneously (race condition).

**Detection:**
- Both reservations query AvailableStock
- Both see HU as available (projection lag)
- Both attempt to allocate

**Resolution:**
1. First reservation commits → succeeds
2. Second reservation commits → sees conflict
3. AllocationProcessManager detects conflict
4. Emits `AllocationConflictDetected` event
5. Notifies user: "Stock no longer available, please retry"

**Prevention:**
- Use optimistic locking on Reservation aggregate
- Include expected version in AllocateReservation command
- Command handler validates version before commit

---

#### Concurrent Picks from Same HU

**Scenario:** Two operators pick from same HU simultaneously.

**Detection:**
- Both call RemoveLineFromHandlingUnit
- Both see sufficient quantity (stale read)
- Both attempt to remove

**Resolution:**
1. First pick commits → succeeds
2. Second pick commits → validation fails (insufficient quantity)
3. RemoveLineFromHandlingUnit throws DomainException
4. Operator sees error: "Insufficient quantity in HU"

**Prevention:**
- Use optimistic locking on HandlingUnit aggregate
- Validate quantity at commit time (not query time)

---

#### Hard vs Soft Lock Conflicts

**Scenario:** 
- Reservation A: SOFT lock on HU-001
- Reservation B: Starts PICKING (HARD lock) on HU-001

**Detection:**
- Reservation B calls StartPicking()
- Detects Reservation A has SOFT lock

**Resolution:**
1. Reservation B transitions to HARD lock → succeeds
2. Emits `ReservationBumped(A, B, [HU-001])`
3. Reservation A notified it was bumped
4. User sees: "Stock reallocated to higher priority order"

**Business Rule:**
- HARD lock always wins over SOFT
- StartPicking() re-validates no conflicts
- Bumped reservation can be retried

---

### 5.3 Stale Read Handling

**Problem:** Read models are eventually consistent (projection lag).

**Mitigation Strategies:**

1. **Version-Based Validation:**
```
Command includes expected version
Handler validates version at commit time
If version mismatch → reject with current version
```

2. **Idempotent Retries:**
```
Client retries with exponential backoff
Command handler checks for duplicate command ID
If duplicate → return cached result
```

3. **User Feedback:**
```
UI shows "Last updated: 2 seconds ago"
Critical operations show "Refreshing..." spinner
After submit, poll for confirmation
```

---

## 6. Idempotency Strategy

### 6.1 Command Idempotency

**Problem:** Network failures may cause duplicate commands.

**Solution:** Every command includes unique `CommandId` (GUID).

**Implementation:**
```
Command {
  commandId: guid,
  payload: {...}
}

CommandHandler:
  IF commandId exists in processed_commands:
    RETURN cached result
  ELSE:
    Execute command
    Store commandId + result in processed_commands
    RETURN result
```

**Retention:** Keep processed commands for 24 hours (or longer for audit).

---

### 6.2 Event Idempotency

**Problem:** Event handlers may process same event multiple times.

**Solution:** Event handlers track last processed event position.

**Implementation:**
```
EventHandler:
  Load last_processed_position from checkpoint store
  
  FOR each event in stream starting from last_processed_position:
    Process event (idempotent operations)
    Update last_processed_position
    Save checkpoint
```

**Idempotent Operations:**
- Set location balance (not increment) → `Balance[location][sku] = computed_value`
- Upsert records (not insert) → `UPDATE ... ON CONFLICT DO UPDATE`

---

### 6.3 Saga Idempotency

**Problem:** Process manager may restart and retry steps.

**Solution:** Each saga step is idempotent.

**Implementation:**
```
ProcessManager State:
  {
    sagaId: guid,
    currentStep: number,
    stepResults: { step1: result1, step2: result2, ... }
  }

ExecuteStep(step):
  IF stepResults[step] exists:
    RETURN cached result (skip execution)
  ELSE:
    result = execute step
    stepResults[step] = result
    Save state
    RETURN result
```

---

### 6.4 Integration Idempotency

#### Agnum Export

**Problem:** Export may fail mid-flight, retry may send duplicates.

**Solution:** Include unique `ExportId` in payload.

**Implementation:**
```
AgnumExportPayload {
  exportId: guid,
  timestamp: datetime,
  data: [...]
}

Agnum receives:
  IF exportId already processed:
    RETURN success (deduplication)
  ELSE:
    Process data
    Store exportId in processed_exports
    RETURN success
```

---

#### Label Printing

**Problem:** Print command may be sent multiple times.

**Solution:** Label printer idempotency via `PrintJobId`.

**Implementation:**
```
PrintLabelCommand {
  printJobId: guid,
  handlingUnitId: guid,
  labelData: {...}
}

Printer receives:
  IF printJobId already printed:
    RETURN success (do not reprint)
  ELSE:
    Print label
    Store printJobId
    RETURN success
```

---

## 7. Integration Patterns

### 7.1 Agnum (Accounting System) - Outbound

**Pattern:** Scheduled Export (Pull from Warehouse)

**Flow:**
```
┌────────────┐       ┌──────────────┐       ┌────────────┐
│ Scheduler  │       │ Agnum Export │       │   Agnum    │
│            │       │     Saga     │       │    API     │
└─────┬──────┘       └───────┬──────┘       └─────┬──────┘
      │                      │                     │
      │ Trigger (daily)      │                     │
      ├─────────────────────>│                     │
      │                      │                     │
      │                      │ 1. Query Stock      │
      │                      │    (StockMovement)  │
      │                      │                     │
      │                      │ 2. Query Costs      │
      │                      │    (Valuation)      │
      │                      │                     │
      │                      │ 3. Query Categories │
      │                      │    (LogicalWH)      │
      │                      │                     │
      │                      │ 4. Apply Mapping    │
      │                      │    (WH → Account)   │
      │                      │                     │
      │                      │ 5. Transform to CSV │
      │                      │                     │
      │                      │ 6. POST to Agnum    │
      │                      ├────────────────────>│
      │                      │                     │
      │                      │<────────────────────┤
      │                      │ 200 OK              │
      │                      │                     │
      │                      │ 7. Record Export    │
      │                      │    Timestamp        │
      │                      │                     │
```

**Configuration:**
```
AgnumMapping {
  physicalWarehouse: "Main" → agnumAccount: "1500-RAW-MAIN",
  physicalWarehouse: "Aux" → agnumAccount: "1500-RAW-AUX",
  logicalWarehouse: "RES" → agnumAccount: "1550-RESERVED",
  logicalWarehouse: "SCRAP" → agnumAccount: "5200-SCRAP"
}
```

**Export Modes:**
1. **By Physical Warehouse** - Group by Main/Aux/Cold
2. **By Logical Warehouse** - Group by RES/PROD/NLQ/SCRAP
3. **By Category** - Group by Textile/Green/Hardware
4. **Total Only** - Single sum across all warehouses

---

### 7.2 ERP/MES Core - Inbound

**Pattern:** Event Subscription (Push from ERP)

**Flow:**
```
┌────────────┐       ┌──────────────┐       ┌────────────┐
│  ERP/MES   │       │  Warehouse   │       │ Allocation │
│   Core     │       │   Gateway    │       │Process Mgr │
└─────┬──────┘       └───────┬──────┘       └─────┬──────┘
      │                      │                     │
      │ MaterialRequested    │                     │
      │ (ProductionOrder-123)│                     │
      ├─────────────────────>│                     │
      │                      │                     │
      │                      │ Translate to        │
      │                      │ CreateReservation   │
      │                      ├────────────────────>│
      │                      │                     │
      │                      │<────────────────────┤
      │                      │ ReservationCreated  │
      │                      │                     │
      │<─────────────────────┤                     │
      │ MaterialReserved     │                     │
      │                      │                     │
```

**Anti-Corruption Layer:**
- ERP sends `MaterialRequested` → Gateway translates to `CreateReservation`
- Warehouse sends `StockMoved` → Gateway translates to `MaterialConsumed` for ERP
- Gateway maintains mapping: ProductionOrder → Reservation

---

### 7.3 Label Printing Service - Outbound

**Pattern:** Fire-and-Forget (Best Effort)

**Flow:**
```
┌────────────┐       ┌──────────────┐       ┌────────────┐
│ Warehouse  │       │   Printer    │       │   Zebra    │
│   Domain   │       │   Gateway    │       │  Printer   │
└─────┬──────┘       └───────┬──────┘       └─────┬──────┘
      │                      │                     │
      │ HandlingUnitCreated  │                     │
      ├─────────────────────>│                     │
      │                      │                     │
      │                      │ Generate ZPL        │
      │                      │                     │
      │                      │ Send to Printer     │
      │                      ├────────────────────>│
      │                      │                     │
      │                      │<────────────────────┤
      │                      │ OK / Error          │
      │                      │                     │
      │<─────────────────────┤                     │
      │ LabelPrintRequested  │                     │
      │                      │                     │
```

**Retry Strategy:**
- If print fails → retry 3 times
- If still fails → log error, alert operator
- Operator can reprint manually from UI

---

### 7.4 Equipment Integration (Scales, Scanners) - Inbound

**Pattern:** Device Abstraction Layer

**Flow:**
```
┌────────────┐       ┌──────────────┐       ┌────────────┐
│  Barcode   │       │   Device     │       │ Warehouse  │
│  Scanner   │       │   Gateway    │       │   Domain   │
└─────┬──────┘       └───────┬──────┘       └─────┬──────┘
      │                      │                     │
      │ Scan: HU-001234      │                     │
      ├─────────────────────>│                     │
      │                      │                     │
      │                      │ Resolve HU          │
      │                      ├────────────────────>│
      │                      │                     │
      │                      │<────────────────────┤
      │                      │ HU Details          │
      │                      │                     │
      │<─────────────────────┤                     │
      │ Display HU Info      │                     │
      │                      │                     │
```

**Device Types:**
- Barcode Scanner → Resolves HU ID
- Weight Scale → Validates picked quantity
- RFID Reader → Bulk reads multiple HUs

---

## Appendix: Sequence Diagram Notation

**Legend:**
```
┌─────────┐   Actor or System
│         │
└────┬────┘
     │
     ├───────────>  Synchronous call
     │
     │<────────────  Synchronous return
     │
     ├──────────>│  Asynchronous event
```

**Aggregate Representation:**
- Boxes represent aggregates or handlers
- Arrows represent commands or events
- Dashed arrows represent queries

---

**End of Implementation Guide**

This guide provides implementation patterns without framework coupling. All patterns can be implemented in .NET, Java, Node.js, or other platforms following these principles.

