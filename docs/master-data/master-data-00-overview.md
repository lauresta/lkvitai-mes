# Master Data Architecture - Overview

## Purpose

Establish the foundational master data layer for the Warehouse module (LKvitai.MES), enabling stock tracking, receiving, putaway, picking, and adjustments for 500+ product variants.

## Core Architectural Principles

### 1. Separation of Concerns

**Master Data (EF Core - State-Based)**
- Items, Suppliers, Locations, UoM, Barcodes, Lots
- Stored as current state in relational tables
- Updated via standard CRUD operations
- Optimized for reference lookups and joins

**Operational Data (Marten - Event-Sourced)**
- Stock movements: GoodsReceived, StockMoved, PickCompleted, StockAdjusted
- Stored as immutable event stream
- Projections derive current state (AvailableStock, LocationBalance)
- Optimized for audit trail and temporal queries

**Rationale**: Master data changes infrequently and doesn't require event sourcing complexity. Transactional stock movements must be auditable and support eventual consistency patterns.

### 2. Configuration Over Code

Business rules driven by data flags, not hard-coded logic:
- `Item.RequiresQC` → controls QC gate workflow
- `Item.RequiresLotTracking` → enforces lot assignment
- `Location.IsVirtual` → distinguishes process vs physical locations
- `AdjustmentReasonCode` → configurable reason taxonomy

### 3. Import-First Strategy

Bulk data entry via Excel templates, not manual UI entry:
- 500+ items imported in <5 minutes
- Validation before commit (dry-run mode)
- Detailed error reports (row, column, message)
- Upsert logic (insert new, update existing)

### 4. Minimal but Extensible

Simple Phase 1 model with clear hooks for future enhancements:
- Reserved columns: `SerialNumber.Id`, `HandlingUnit.ParentHUId`
- Optional features: lot tracking, QC gate, barcode scanning
- Deferred complexity: HU hierarchy, wave picking, FEFO allocation

## Technology Stack

| Layer | Technology | Database |
|-------|-----------|----------|
| Master Data | EF Core | PostgreSQL (state-based tables) |
| Stock Events | Marten | PostgreSQL (event store + projections) |
| UI | Blazor Server | Bootstrap 5 |
| API | ASP.NET Core | REST + JSON |
| Messaging | RabbitMQ/ASB | Async event publishing |

## Data Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│ EF Core (State-Based Master Data)                           │
├─────────────────────────────────────────────────────────────┤
│ • Item                    • Location                         │
│ • ItemCategory            • HandlingUnitType                 │
│ • Supplier                • HandlingUnit (metadata only)     │
│ • SupplierItemMapping     • Lot                              │
│ • ItemBarcode             • InboundShipment                  │
│ • UnitOfMeasure           • InboundShipmentLine              │
│ • ItemUoMConversion       • AdjustmentReasonCode             │
└─────────────────────────────────────────────────────────────┘
                              ↓
                    Read for validation
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ Marten (Event-Sourced Operations)                           │
├─────────────────────────────────────────────────────────────┤
│ Events:                                                      │
│ • GoodsReceived           • PickCompleted                    │
│ • StockMoved              • ReservationCreated               │
│ • StockAdjusted           • QCPassed / QCFailed              │
│                                                              │
│ Projections (Read Models):                                  │
│ • AvailableStock(ItemId, LocationId, LotId, Qty)            │
│ • LocationBalance(LocationId, TotalWeight, TotalVolume)      │
│ • ActiveReservations(ItemId, ReservedQty, OrderId)          │
│ • InboundShipmentSummary(ShipmentId, ReceivedQty)           │
└─────────────────────────────────────────────────────────────┘
```

## Phase 1 Scope (MVP)

### In Scope
✅ Master data management (Items, Suppliers, Locations, UoM, Barcodes)  
✅ Excel import/export for bulk data entry  
✅ Receiving workflow (create shipment, receive goods, optional QC)  
✅ Putaway workflow (manual location assignment)  
✅ Picking workflow (create task, scan & confirm)  
✅ Stock adjustments (manual corrections with reason codes)  
✅ Real-time stock visibility (AvailableStock projection)  
✅ Lot tracking (optional per item)  
✅ Barcode scanning (with manual entry fallback)  
✅ Audit trail (automatic via event store)  

### Out of Scope (Deferred to Phase 2/3)
❌ Serial number tracking  
❌ Handling unit hierarchy (nested HUs)  
❌ Wave picking (batch picking for multiple orders)  
❌ Zone picking (multi-operator coordination)  
❌ Putaway strategy (AI-based location suggestions)  
❌ Picking strategy (auto-FEFO by expiry date)  
❌ ASN import (automated supplier advance shipping notices)  
❌ Label printing (location/item/HU barcodes)  
❌ QC approval workflow (multi-level authorization)  
❌ Inter-warehouse transfers  
❌ Cross-docking  
❌ Mobile app (Blazor Server in tablet browser only)  

## Critical Design Decisions (Resolved)

| Decision | Resolution | Impact |
|----------|-----------|--------|
| InternalSKU generation | Auto-increment with prefix: `RM-{0001}`, `FG-{0001}` | Semantic readability + uniqueness |
| Item variant model | Flat: 1 Item per variant (500 records) | Simple Phase 1, defer matrix variants |
| BaseUoM policy | Mandatory `Item.BaseUoM`, all stock in base unit | Eliminates rounding errors |
| UoM rounding | Default `Up` for picking, `Nearest` for reports | Safety: over-deliver vs under-deliver |
| Virtual locations | 7 mandatory: RECEIVING, QC_HOLD, QUARANTINE, PRODUCTION, SHIPPING, SCRAP, RETURN_TO_SUPPLIER | Track stock in process states |
| Barcode policy | `Item.PrimaryBarcode` + multi-barcode table, `Location.Barcode` required | Support multiple barcodes per item |
| Handling units | Flat model, homogeneous only (HU.ItemId not null) | Defer nesting complexity |
| Lot tracking | Optional per item: `Item.RequiresLotTracking` | Flexible for different product types |
| Serial tracking | **Deferred to Phase 2** | High complexity, not critical |
| QC gate | Optional per item: `Item.RequiresQC` | Configurable workflow |
| Mixed SKU in location | **Allowed** (multiple ItemIds per location) | Realistic shelf storage |
| Picking confirmation | **Mandatory barcode scan** with manual fallback | Reduce errors, allow damaged labels |
| Excel import | 5 templates: Items, Suppliers, Mappings, Barcodes, Locations | Bulk preparation offline |

## Success Criteria

### Performance Targets
- Import 500 items: <5 minutes
- Receive 10-item shipment: <15 minutes (with QC)
- Complete 5-item pick: <5 minutes
- Projection lag: <1 second (under normal load)
- Stock report (10k rows): <3 seconds

### Functional Targets
- Warehouse admin can import master data from Excel with validation
- Operator can receive goods with barcode scanning
- Operator can perform putaway with location capacity warnings
- Operator can pick orders with lot tracking
- Manager can adjust stock with audit trail
- All users see real-time inventory (projection updates)

## Integration Points

### Inbound (Events Consumed)
- Order module: `OrderCreated` → trigger reservation
- Production module: `MaterialRequested` → trigger picking

### Outbound (Events Published)
- `GoodsReceived` → ERP updates procurement
- `PickCompleted` → Order module marks order shipped
- `StockAdjusted` → ERP records loss/gain
- `ShipmentDispatched` → Logistics module triggers delivery

### Master Data Sync
- Master data changes logged (EF Core change tracking)
- `MasterDataChanged` event published for downstream systems
- Audit fields: CreatedBy, CreatedAt, UpdatedBy, UpdatedAt

## Consistency Model

### Master Data (EF Core)
- **Strongly consistent**: Read your own writes
- Transactions: ACID guarantees within bounded context
- Concurrency: Optimistic (rowversion) for updates

### Stock Operations (Marten)
- **Eventually consistent**: Projection lag <1 second
- Transactions: Optimistic concurrency on aggregates
- Concurrency: Retry saga on conflict
- UI: Display "Stock as of HH:MM:SS" timestamp

### Cross-Boundary
- Master data → Operations: Read-only reference
- Operations → Master data: No reverse updates
- Events propagate asynchronously via message queue

## Risk Mitigation

### Technical Risks
- **Projection rebuild issue** (42P01 error): Fix before Phase 1 launch
- **Barcode collision**: Global uniqueness constraint enforced
- **Concurrent reservations**: Optimistic concurrency + retry logic
- **Excel import performance**: Bulk insert for >10k rows

### Business Risks
- **Data preparation delay**: Provide templates early, train users
- **Scanner adoption**: Provide manual entry fallback
- **Scope creep**: Defer aggressively, review weekly

## Next Steps

1. **Week 1-2**: Implement EF Core model + seed data
2. **Week 3**: Implement import APIs + validation
3. **Week 4**: Fix projection rebuild issue + implement projections
4. **Week 5-6**: Implement Phase 1 UI screens
5. **Week 7**: End-to-end testing + UAT
6. **Week 8**: Production deployment + user training

## References

- Domain model: `master-data-01-domain-model.md`
- API contracts: `master-data-02-api-contracts.md`
- Events/projections: `master-data-03-events-and-projections.md`
- UI scope: `master-data-04-ui-scope.md`
- Implementation plan: `master-data-05-implementation-plan-and-tests.md`
- Ops runbook: `master-data-06-ops-runbook-projections.md`
