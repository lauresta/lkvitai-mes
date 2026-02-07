# LKvitai.MES - Warehouse Module Discovery

**Project:** Modular Manufacturing Automation System (MES/ERP-light)  
**Module:** Warehouse Management  
**Date:** February 2026  
**Status:** Discovery Phase

---

## Executive Summary

This document captures the domain model for the LKvitai.MES Warehouse Module - a visual, real-time inventory management system designed for high-variance manufacturing operations.

**Core Value Proposition:**
- **Accurate stock levels always** - every pallet, box, or unit is labeled and scanned in real-time
- **Financial flexibility** - valuation can be adjusted independently from physical quantities
- **Visual warehouse plan (2D/3D)** - instant location lookup on screen
- **Simplest UX on the market** - computer, tablet, or Raspberry Pi with scanner
- **Cycle counting via scanning** - scan shows origin, quantity, reorder status
- **Unit conversion** - automatic pallet ↔ bag ↔ unit conversions
- **Unlimited warehouses and categories** - logical and physical, multi-category support
- **Flexible Agnum integration** - share stock by warehouse, category, virtual group, or total sum

---

## 1. Actors

### Primary Actors

| Actor | Responsibilities | Key Actions |
|-------|-----------------|-------------|
| **Warehouse Operator** | Day-to-day operations | Receive goods, scan labels, pick items, transfer stock |
| **Warehouse Manager** | Oversight and planning | Manage locations, run reports, configure layouts |
| **Quality Inspector** | Quality control | Quarantine items, mark scrap/defects |
| **Inventory Accountant** | Financial management | Revaluations, adjustments, cost corrections |
| **Production Planner** | Material planning | Reserve materials, request picks for orders |
| **Procurement Specialist** | Purchasing | Trigger restocking based on alerts |

### Secondary Actors

| Actor | Role | Integration |
|-------|------|-------------|
| **Agnum (External Accounting)** | Financial system | Receives stock/valuation data via export/API |
| **ERP/MES Core** | Parent system | Sends material requests, consumes stock events |
| **Label Printer** | Hardware | Receives print commands for SKU labels |
| **Barcode Scanner** | Input device | Provides input for all operations |

---

## 2. Use Cases

### Core Operations

1. **Receive Goods**
   - Scan incoming pallet/box
   - Assign storage location
   - Update stock levels
   - Generate label if needed

2. **Transfer Between Locations**
   - Move SKU from bin A to bin B
   - Support physical and logical warehouse transfers
   - Update 3D visualization

3. **Pick for Production/Order**
   - Allocate material
   - Withdraw from stock
   - Update reservation status

4. **Scrap/Quarantine**
   - Mark defective/damaged items
   - Move to special warehouse
   - Track quality issues

5. **Relabel SKU**
   - Generate new label if damaged/missing
   - Maintain inventory continuity

6. **Reserve Stock**
   - Lock quantity for specific order
   - Support production task allocation

### Inventory Management

7. **Perform Cycle Count**
   - Scan location
   - Confirm or correct quantities
   - Real-time accuracy updates

8. **Adjust Quantity**
   - Manual corrections
   - Physical count vs system reconciliation

9. **Revalue Stock**
   - Financial adjustments (write-downs, landed costs)
   - Independent from physical quantity changes

10. **Move to Logical Warehouse**
    - Shift between RES/PROD/NLQ/SCRAP categories
    - No physical movement required

11. **Mark Low Stock Alert**
    - System-generated or manual flags
    - Trigger procurement workflow

12. **Convert Units**
    - Automatic pallet ↔ bag ↔ unit conversions
    - Configuration-driven rules

### Visualization & Search

13. **View 3D Warehouse Map**
    - Browse visual representation
    - Click location to see contents
    - Color-coded status indicators

14. **Search SKU/Bin**
    - Find all instances of product across locations
    - Show handling units containing SKU

15. **View SKU Details**
    - History: received date, source
    - Current locations
    - Reserved quantities

### Reporting & Integration

16. **Export to Agnum**
    - Send stock snapshot
    - Configurable: by warehouse/category/logical group or total
    - Scheduled or on-demand

17. **Generate Stock Report**
    - On-hand value
    - SKU lists
    - Alerts and movements

18. **Mapping with Agnum**
    - Configure which warehouses map to Agnum accounts
    - Flexible integration rules

---

## 3. Bounded Contexts

```
┌─────────────────────────────────────────────────────────┐
│  WAREHOUSE MANAGEMENT (Core Context)                    │
├─────────────────────────────────────────────────────────┤
│  - Physical Inventory (bins, pallets, SKUs)            │
│  - Stock Movements (receipts, transfers, picks)        │
│  - Location Management (aisles, racks, 3D coordinates) │
│  - Stock Status (FULL, LOW, EMPTY, RESERVED)           │
└─────────────────────────────────────────────────────────┘
                    ↓ events ↑ commands
┌─────────────────────────────────────────────────────────┐
│  LOGICAL WAREHOUSES (Sub-context)                       │
├─────────────────────────────────────────────────────────┤
│  - Virtual stock groups (RES, PROD, NLQ, SCRAP)        │
│  - Category assignments (Textile, Green, Hardware)      │
│  - Multi-categorization support                         │
└─────────────────────────────────────────────────────────┘
                    ↓ events
┌─────────────────────────────────────────────────────────┐
│  VALUATION (Sub-context)                                │
├─────────────────────────────────────────────────────────┤
│  - Cost adjustments (write-downs, landed costs)        │
│  - Revaluation operations (independent of qty)          │
│  - Financial reporting (avg cost, on-hand value)        │
└─────────────────────────────────────────────────────────┘
                    ↓ export/sync
┌─────────────────────────────────────────────────────────┐
│  INTEGRATION LAYER                                       │
├─────────────────────────────────────────────────────────┤
│  - Agnum export (by warehouse, category, or summary)   │
│  - ERP material requests                                │
│  - Label printing service                               │
└─────────────────────────────────────────────────────────┘
```

### Context Boundaries

- **Warehouse Management** owns physical reality (bins, SKUs, movements)
- **Logical Warehouses** owns grouping logic (not tightly coupled to physical)
- **Valuation** owns financial calculations (decoupled from quantities)
- **Integration Layer** handles outbound sync (anti-corruption layer for Agnum)

---

## 4. Domain Glossary

| Term | Definition |
|------|------------|
| **SKU** | Stock Keeping Unit – unique identifier for a product variant |
| **Bin** | Physical storage location (e.g., A1-B1, R3-C6-L3B3) |
| **Pallet** | Logical container object (can have a barcode/label) |
| **Logical Warehouse** | Virtual grouping (RES=Reserved, PROD=Production, NLQ=Non-Liquid, SCRAP) |
| **Physical Warehouse** | Actual building/zone (Main, Aux, Cold) |
| **Category** | Product classification (Textile, Green, Hardware) – multi-assign allowed |
| **Stock Status** | FULL, LOW, EMPTY, RESERVED |
| **Unit Conversion** | Mapping between pallet ↔ bag ↔ piece |
| **Revaluation** | Financial adjustment without changing physical quantity |
| **Quarantine** | Isolated stock pending quality decision |
| **Landed Cost** | Total cost including freight, duties, allocated to inventory |
| **On-Hand Value** | Current stock × cost (shown in valuation footer) |
| **Period COGS** | Cost of Goods Sold for selected date range |
| **Self-Test** | Automated stock consistency check (shown in UI: "Self-test: 10/10 passed") |
| **Handling Unit (HU)** | Physical container (pallet, box, bag) with barcode |
| **LPN** | License Plate Number - unique identifier for handling unit |

---

## 5. Domain Events

### Inbound Flow
- `GoodsReceived` – pallet/box arrives, label scanned, location assigned
- `SKULabeled` – new or replacement label generated
- `StockPlaced` – item placed in specific bin

### Movement Flow
- `StockTransferred` – moved between bins or warehouses
- `StockReserved` – quantity locked for production/order
- `StockPicked` – material withdrawn for use
- `StockQuarantined` – moved to quarantine due to defect
- `StockScrapped` – marked as waste/loss

### Inventory Adjustments
- `QuantityCorrected` – manual or cycle count adjustment
- `StockRevalued` – financial value adjusted (write-down, landed cost)
- `LowStockDetected` – alert triggered
- `StockMarkedEmpty` – bin emptied

### Integration Events
- `StockExportedToAgnum` – snapshot sent to accounting system
- `LabelPrintRequested` – command to printer
- `MaterialRequestReceived` – from ERP/production module

---

## 6. Capability Map

```
WAREHOUSE MANAGEMENT DOMAIN
│
├─ STOCK VISIBILITY
│  ├─ 3D Warehouse Visualization
│  ├─ SKU/Bin Search
│  ├─ Real-time Stock Levels
│  └─ Multi-Location View (same SKU across bins)
│
├─ INBOUND OPERATIONS
│  ├─ Goods Receipt
│  ├─ Label Generation
│  ├─ Location Assignment
│  └─ Quantity Verification
│
├─ MOVEMENT OPERATIONS
│  ├─ Inter-Bin Transfer
│  ├─ Warehouse Transfer
│  ├─ Logical Warehouse Reclassification
│  └─ Reservation Management
│
├─ OUTBOUND OPERATIONS
│  ├─ Picking (for production/orders)
│  ├─ Stock Release
│  └─ Reservation Consumption
│
├─ QUALITY & EXCEPTION HANDLING
│  ├─ Quarantine
│  ├─ Scrap Management
│  ├─ Relabeling
│  └─ Defect Tracking
│
├─ INVENTORY ACCURACY
│  ├─ Cycle Counting
│  ├─ Manual Adjustments
│  ├─ Self-Test Validation
│  └─ Alerts (Low Stock, Empty)
│
├─ FINANCIAL MANAGEMENT
│  ├─ Stock Valuation (avg cost, on-hand value)
│  ├─ Revaluation Operations
│  ├─ Landed Cost Allocation
│  └─ COGS Calculation
│
├─ CONFIGURATION
│  ├─ Warehouse Layout (bins, aisles, 3D coordinates)
│  ├─ Unit Conversion Rules
│  ├─ Category & Logical Warehouse Setup
│  └─ Agnum Mapping Configuration
│
└─ INTEGRATION
   ├─ Agnum Export (by warehouse/category/summary)
   ├─ ERP/MES Sync (material requests, consumption)
   └─ Label Printing Service
```

---

## Key Architectural Insights

### 1. Separation of Physical vs Financial
- Physical quantities are immutable facts (scanned, verified)
- Financial valuations are adjustable interpretations
- This separation enables flexible accounting without corrupting inventory truth

### 2. Multi-Dimensional Organization
- SKUs can exist in multiple physical bins simultaneously
- Same SKU can belong to multiple logical warehouses/categories
- Requires event-driven sync to maintain consistency

### 3. Configuration-Driven Flexibility
- Unit conversions defined as data (pallet = 32 bags)
- Warehouse layout stored as metadata (3D coordinates)
- Agnum mapping configurable (warehouse → account mapping)

### 4. Visual-First UX
- 3D warehouse view is primary navigation interface
- Color-coded statuses (FULL=green, LOW=yellow, EMPTY=red, RESERVED=orange)
- Click-to-drill-down interaction model

### 5. Audit Trail by Design
- Every operation is an event (timestamped, user-attributed)
- "Since" field shows when stock entered current state
- Recent Actions log provides operational transparency

---

## UI Observations (from Screenshots)

### Screenshot 1: 3D Warehouse View
- Interactive 3D visualization of warehouse layout
- Color-coded pallets/bins:
  - Green = FULL
  - Yellow/Gold = LOW or RESERVED
  - Red/Pink = EMPTY or SCRAP
  - Blue = Special status
- Right panel shows selected object details:
  - Pallet code (R3-C6)
  - SKU (SKU933)
  - Quantity (12)
  - Status (FULL)
  - Since date (2025-09-01)
  - Location (R3-C6 L3B3)
- Actions available: Pick, Reserve, Re-label, Quarantine
- Footer metrics:
  - Average cost: €10.35
  - On-hand value: €52,278
  - Period COGS: €9,410

### Screenshot 2: Logical Warehouses List View
- Filters: Logical warehouse, Physical warehouse, Category
- Table columns:
  - SKU
  - Bin (physical location)
  - Logical WH (RES, PROD, SCRAP, NLQ)
  - Qty
  - Status (EMPTY, FULL, LOW)
  - Since (date)
  - Tags (reserved)
- Bulk actions: Move to logical, Reclassify, Quarantine, Relabel
- Bottom actions: Reserve, Mark Low, Mark Empty

### Screenshot 3: Agnum Export View
- Scope selector: By virtual warehouse
- Period selection: Date range
- Format: CSV
- Preview table shows:
  - Warehouse
  - Virtual/Group
  - SKUs count
  - Qty
  - Sum (€)
- Mapping configuration visible
- Export and Preview buttons

---

## Next Steps

This discovery phase has established:
- ✅ Clear actor definitions
- ✅ Comprehensive use case catalog
- ✅ Bounded context boundaries
- ✅ Domain terminology glossary
- ✅ Event catalog
- ✅ Capability map

**Required for next phase:**
- Tactical DDD modeling (aggregates, value objects, entities)
- Transactional boundary definition
- Invariant rules specification
- Data ownership model
- Aggregate interaction patterns
- Event sourcing candidate identification

---

## Alignment with Core Principles

✅ **Modular architecture** - Warehouse is independent bounded context  
✅ **Strong separation of concerns** - Physical vs financial vs logical clearly separated  
✅ **Business logic via configuration** - Unit conversions, layout, mappings are data  
✅ **Event-driven integration** - All operations emit events for other modules  
✅ **Designed for evolution** - Can add new logical warehouses, categories without code changes

This model supports **configuration over code** principle throughout.
