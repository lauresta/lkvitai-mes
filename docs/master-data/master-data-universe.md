# Master Data Universe - One-Page Map

## Modules / Epics / Features

### Epic 0: Fix Projections Rebuild Reliability (CRITICAL - MUST BE FIRST)
**Problem**: 42P01 error "relation mt_doc_locationbalanceview_shadow does not exist"
**Impact**: Blocks all projection rebuilds, new environment setup, data recovery
**Features**:
- 0.1 Root cause analysis and schema separation (EF Core vs Marten)
- 0.2 Orphaned shadow table cleanup automation
- 0.3 Pre-deployment schema validation
- 0.4 Distributed lock on rebuild operations
- 0.5 Startup schema validation service
- 0.6 Zero-downtime rebuild with shadow tables

### Epic 1: Master Data Foundation (EF Core)
**Purpose**: State-based master data layer for items, suppliers, locations
**Features**:
- 1.1 Entity models with constraints and indexes
- 1.2 Seed data (UoM, virtual locations, reason codes, categories, HU types)
- 1.3 Audit fields pattern (CreatedBy, UpdatedAt, etc.)
- 1.4 InternalSKU auto-generation logic
- 1.5 Database migrations

### Epic 2: Bulk Import System
**Purpose**: Excel-based bulk data entry for 500+ items
**Features**:
- 2.1 Excel template generation (Items, Suppliers, Mappings, Barcodes, Locations)
- 2.2 Excel parsing and validation engine
- 2.3 Upsert logic (insert new, update existing by identity key)
- 2.4 Dry-run mode (validation without DB writes)
- 2.5 Error reporting (row, column, message)
- 2.6 Batch insert optimization (>1000 rows)

### Epic 3: Event Store & Projections (Marten)
**Purpose**: Event-sourced operational data with read models
**Features**:
- 3.1 Event contracts (GoodsReceived, StockMoved, PickCompleted, StockAdjusted, Reservations, QC)
- 3.2 AvailableStock projection (Item/Location/Lot → Qty)
- 3.3 LocationBalance projection (capacity utilization)
- 3.4 ActiveReservations projection (hard locks)
- 3.5 InboundShipmentSummary projection (receiving dashboard)
- 3.6 AdjustmentHistory projection (audit trail)
- 3.7 Projection health check API (lag monitoring)

### Epic 4: Receiving Workflow
**Purpose**: Goods receipt with optional QC gate
**Features**:
- 4.1 Inbound shipment creation (PO-based)
- 4.2 Receive goods with barcode scanning
- 4.3 Lot tracking (optional per item)
- 4.4 QC pass/fail actions (move to RECEIVING or QUARANTINE)
- 4.5 Receiving dashboard (shipment list, status tracking)

### Epic 5: Putaway Workflow
**Purpose**: Move stock from RECEIVING to storage locations
**Features**:
- 5.1 Putaway task list (items in RECEIVING)
- 5.2 Location barcode scanning
- 5.3 Capacity warning (weight/volume utilization)
- 5.4 StockMoved event emission

### Epic 6: Picking Workflow
**Purpose**: Order fulfillment with barcode confirmation
**Features**:
- 6.1 Pick task creation (manual, Phase 1)
- 6.2 Pick execution (location selection, barcode scan, confirm)
- 6.3 FEFO location suggestion (earliest expiry first)
- 6.4 PickCompleted event emission
- 6.5 Pick history report

### Epic 7: Stock Adjustments
**Purpose**: Manual corrections with audit trail
**Features**:
- 7.1 Adjustment creation (qty delta, reason code, notes)
- 7.2 Confirmation dialog (irreversible action warning)
- 7.3 StockAdjusted event emission
- 7.4 Adjustment history report (filterable by item, location, reason, user)

### Epic 8: Admin UI (Master Data CRUD)
**Purpose**: Manage items, suppliers, locations, categories
**Features**:
- 8.1 Items management (list, create, edit, deactivate)
- 8.2 Suppliers management
- 8.3 Locations management (hierarchical tree view)
- 8.4 Categories management
- 8.5 Barcodes management (multi-barcode per item)
- 8.6 Import wizard UI (tabbed interface, drag-drop upload)

### Epic 9: Stock Visibility & Reports
**Purpose**: Real-time inventory queries and exports
**Features**:
- 9.1 Available Stock report (filterable by item, location, category, expiry)
- 9.2 Location Balance report (capacity utilization)
- 9.3 Reservations report (active locks)
- 9.4 CSV export for all reports
- 9.5 Projection timestamp display (staleness indicator)

### Epic 10: Operational Workflows UI
**Purpose**: Blazor pages for receiving, putaway, picking, adjustments
**Features**:
- 10.1 Receiving pages (shipment list, detail, receive modal, QC panel)
- 10.2 Putaway page (task list, location scan modal)
- 10.3 Picking pages (task list, execution page with barcode scan)
- 10.4 Adjustments pages (create form, history list)
- 10.5 Barcode scanner component (auto-focus, auto-submit, manual fallback)

---

## Dependencies Graph

```
Epic 0 (Fix Projections)
  ↓
Epic 1 (Master Data Foundation)
  ↓
Epic 2 (Bulk Import) ← depends on Epic 1
  ↓
Epic 3 (Event Store & Projections) ← depends on Epic 0, Epic 1
  ↓
Epic 4 (Receiving) ← depends on Epic 1, Epic 3
  ↓
Epic 5 (Putaway) ← depends on Epic 3, Epic 4
  ↓
Epic 6 (Picking) ← depends on Epic 3, Epic 5
  ↓
Epic 7 (Adjustments) ← depends on Epic 3

Epic 8 (Admin UI) ← depends on Epic 1, Epic 2 (can run parallel to Epic 3-7)
Epic 9 (Reports) ← depends on Epic 3 (can run parallel to Epic 4-7)
Epic 10 (Operational UI) ← depends on Epic 4, Epic 5, Epic 6, Epic 7
```

**Critical Path**: Epic 0 → Epic 1 → Epic 3 → Epic 4 → Epic 5 → Epic 6 → Epic 10

**Parallel Tracks**:
- Track A (Backend): Epic 0 → 1 → 3 → 4 → 5 → 6 → 7
- Track B (Import): Epic 1 → 2 → 8 (Admin UI)
- Track C (Reports): Epic 3 → 9

---

## Non-Goals / Deferred Items (Phase 2+)

### Deferred to Phase 2
- Serial number tracking (high complexity, not critical)
- Handling unit hierarchy (nested HUs, mixed SKUs in HU)
- Wave picking (batch picking for multiple orders)
- Zone picking (multi-operator coordination)
- Putaway strategy (AI-based location suggestions)
- Picking strategy (auto-FEFO by expiry date)
- ASN import (automated supplier advance shipping notices)
- Label printing (location/item/HU barcodes)
- QC approval workflow (multi-level authorization)
- Inter-warehouse transfers
- Cross-docking
- Mobile app (Phase 1: tablet browser only)

### Out of Scope (Not Planned)
- Integration with external WMS systems
- Robotics/automation (AGVs, AS/RS)
- Voice picking
- RFID tracking
- Blockchain-based traceability
- AI-powered demand forecasting

---

## Key Decisions & Invariants

### Architectural Decisions
1. **Separation of Concerns**: Master data (EF Core, state-based) vs Operations (Marten, event-sourced)
2. **Schema Separation**: EF Core in `public` schema, Marten in `warehouse_events` schema (prevents conflicts)
3. **Consistency Model**: Master data strongly consistent, operations eventually consistent (projection lag <1 second)
4. **Import-First Strategy**: Bulk data entry via Excel, not manual UI entry (500+ items in <5 minutes)

### Data Model Decisions
5. **InternalSKU Generation**: Auto-increment with prefix (RM-0001, FG-0001) for semantic readability
6. **Item Variant Model**: Flat (1 Item per variant), defer matrix variants to Phase 2
7. **BaseUoM Policy**: Mandatory, all stock in base unit (eliminates rounding errors)
8. **UoM Rounding**: Default "Up" for picking (over-deliver vs under-deliver)
9. **Virtual Locations**: 7 mandatory (RECEIVING, QC_HOLD, QUARANTINE, PRODUCTION, SHIPPING, SCRAP, RETURN_TO_SUPPLIER)
10. **Barcode Policy**: Item.PrimaryBarcode + multi-barcode table, Location.Barcode required, global uniqueness enforced
11. **Handling Units**: Flat model, homogeneous only (HU.ItemId not null), defer nesting to Phase 2
12. **Lot Tracking**: Optional per item (Item.RequiresLotTracking flag)
13. **Serial Tracking**: Deferred to Phase 2 (table created but unused)
14. **QC Gate**: Optional per item (Item.RequiresQC flag)
15. **Mixed SKU in Location**: Allowed (multiple ItemIds per location)

### Workflow Decisions
16. **Picking Confirmation**: Mandatory barcode scan with manual fallback (reduce errors, allow damaged labels)
17. **Putaway Strategy**: Manual location selection (Phase 1), defer AI suggestions to Phase 2
18. **Picking Strategy**: Manual location selection (Phase 1), defer auto-FEFO to Phase 2
19. **Reservation Model**: Hard locks (decrement AvailableQty), expire after 24 hours

### Technical Decisions
20. **Projection Rebuild**: Zero-downtime using shadow tables (swap primary ↔ shadow)
21. **Concurrency**: Optimistic (Marten version-based), retry on conflict (3 attempts with jittered backoff)
22. **Error Handling**: RFC 7807 ProblemDetails with traceId for all API errors
23. **Audit Trail**: Automatic via event store (all operations immutable)
24. **Testing**: Docker-gated integration tests (skip if Docker unavailable), Testcontainers for Postgres

### Performance Targets
25. **Import**: 500 items in <5 minutes
26. **Receiving**: 10-item shipment in <15 minutes (with QC)
27. **Picking**: 5-item pick in <5 minutes
28. **Projection Lag**: <1 second (90th percentile)
29. **Stock Report**: 10k rows in <3 seconds
30. **API Response**: <500ms (p95)

### Invariants (Must Never Violate)
- Barcode uniqueness (global across all items and locations)
- InternalSKU uniqueness (immutable after creation)
- BaseUoM must exist in UnitOfMeasures table
- Virtual locations cannot be deleted (system-critical)
- Events are immutable (never update/delete, only append)
- Projection lag must not exceed 60 seconds (critical alert threshold)
- Stock adjustments require reason code (audit compliance)
- Lot tracking enforced for RequiresLotTracking items (no bypass)

---

## Success Criteria

### Functional
- Warehouse admin can import 500 items from Excel with validation in <5 minutes
- Operator can receive 10-item shipment with barcode scanning in <15 minutes
- Operator can perform putaway with location capacity warnings
- Operator can pick 5-item order with lot tracking in <5 minutes
- Manager can adjust stock with audit trail
- All users see real-time inventory (projection updates <1 second)

### Technical
- Zero critical bugs in UAT
- Test coverage >80% (unit + integration)
- Projection rebuild completes without 42P01 error
- API response time <500ms (p95)
- Database query performance <3 seconds for 10k row reports

### Operational
- Deployment to staging successful (no rollback)
- User training materials complete (screenshots + steps)
- Ops runbook validated (projection rebuild tested)
- Monitoring alerts configured (projection lag, API errors)

---

## Timeline Summary

**Total Duration**: 7-8 weeks

**Week 1-2**: Foundation (EF Core model, seed data, Blazor structure)
**Week 3**: Import APIs & validation
**Week 4**: Fix projections + implement event store (CRITICAL)
**Week 5**: Receiving & putaway workflows
**Week 6**: Picking & adjustments
**Week 7**: UI polish & testing
**Week 8**: E2E testing & deployment

**Critical Milestone**: Week 4 completion (projection rebuild issue resolved)
