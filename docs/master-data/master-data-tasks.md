# Master Data Implementation Tasks

## Epic 0: Fix Projections Rebuild Reliability (CRITICAL - MUST BE FIRST)

### Task 0.1: Root Cause Analysis and Schema Separation (1 day)
**Goal**: Identify why 42P01 error occurs and implement schema separation to prevent conflicts

**Steps**:
1. Review error logs and stack traces for 42P01 occurrences
2. Analyze Marten projection lifecycle (shadow table creation/swap/drop)
3. Check for EF Core vs Marten schema conflicts
4. Implement schema separation: EF Core in `public`, Marten in `warehouse_events`
5. Update DbContext configuration to use `public` schema
6. Update Marten configuration to use `warehouse_events` schema
7. Test on clean database (no existing tables)

**Acceptance**:
- Schema separation documented in code comments
- EF Core migrations target `public` schema only
- Marten tables created in `warehouse_events` schema only
- No table name conflicts between EF and Marten

**Tests**:
- Integration test: Create EF entities and Marten events in same database
- Verify no schema conflicts (query pg_tables for both schemas)

**Files**:
- `src/LKvitai.MES.Infrastructure/Data/WarehouseDbContext.cs` (EF Core)
- `src/LKvitai.MES.Infrastructure/Events/MartenConfiguration.cs` (Marten)
- Ref: `docs/master-data/master-data-06-ops-runbook-projections.md` (Root Cause Analysis section)

---

### Task 0.2: Orphaned Shadow Table Cleanup Automation (0.5 day)
**Goal**: Detect and clean orphaned shadow tables left by failed rebuilds

**Steps**:
1. Create SQL script to detect shadow tables (`mt_doc_%_shadow`)
2. Create cleanup procedure to drop orphaned shadow tables
3. Add safety check (only drop if no active rebuild lock)
4. Create admin API endpoint `/api/warehouse/v1/admin/projections/cleanup-shadows`
5. Add to ops runbook as manual procedure

**Acceptance**:
- SQL script detects all shadow tables
- Cleanup procedure drops only orphaned tables (not active rebuilds)
- API endpoint requires admin authorization
- Ops runbook updated with cleanup procedure

**Tests**:
- Create fake shadow table, run cleanup, verify dropped
- Simulate active rebuild (lock exists), verify shadow table NOT dropped

**Files**:
- `src/LKvitai.MES.Infrastructure/Events/ProjectionCleanupService.cs`
- `src/LKvitai.MES.Api/Api/Controllers/ProjectionsController.cs`
- Ref: `docs/master-data/master-data-06-ops-runbook-projections.md` (Step 2: Clean Orphaned Shadow Tables)

---


### Task 0.3: Pre-Deployment Schema Validation (0.5 day)
**Goal**: Add CI/CD checks to prevent deployment if Marten schema not initialized

**Steps**:
1. Create bash script `scripts/validate-schema.sh`
2. Check if `warehouse_events` schema exists
3. Check if core Marten tables exist (mt_events, mt_streams, mt_event_progression)
4. Check if projection tables exist (mt_doc_availablestock, etc.)
5. Add script to CI/CD pipeline (run before deployment)
6. Document in deployment checklist

**Acceptance**:
- Script exits with error code 1 if schema missing
- Script logs clear error messages (which table missing)
- CI/CD pipeline fails if script fails
- Deployment checklist includes schema validation step

**Tests**:
- Run script on empty database → should fail
- Run script on initialized database → should pass

**Files**:
- `scripts/validate-schema.sh`
- `.github/workflows/deploy.yml` (or equivalent CI/CD config)
- Ref: `docs/master-data/master-data-06-ops-runbook-projections.md` (Preventive Measure 2)

---

### Task 0.4: Distributed Lock on Rebuild Operations (1 day)
**Goal**: Prevent concurrent projection rebuilds using distributed lock

**Steps**:
1. Add Redis dependency (or use database-based lock if Redis unavailable)
2. Create `IDistributedLock` interface
3. Implement Redis-based lock (acquire/release with expiry)
4. Wrap projection rebuild logic with lock acquisition
5. Add lock timeout (30 minutes max)
6. Add lock status endpoint `/api/warehouse/v1/admin/projections/rebuild-status`

**Acceptance**:
- Only one rebuild can run at a time per projection
- Lock expires after 30 minutes (prevents deadlock)
- Second rebuild attempt returns 409 Conflict with lock holder info
- Lock released on rebuild completion or failure

**Tests**:
- Start rebuild, attempt second rebuild → should fail with 409
- Simulate rebuild crash, verify lock expires after 30 minutes
- Verify lock released on successful rebuild

**Files**:
- `src/LKvitai.MES.Infrastructure/Locking/IDistributedLock.cs`
- `src/LKvitai.MES.Infrastructure/Locking/RedisDistributedLock.cs`
- `src/LKvitai.MES.Infrastructure/Events/ProjectionRebuildService.cs`
- Ref: `docs/master-data/master-data-06-ops-runbook-projections.md` (Preventive Measure 3)

---

### Task 0.5: Startup Schema Validation Service (0.5 day)
**Goal**: Validate Marten schema on application startup, fail fast if missing

**Steps**:
1. Create `SchemaValidationService` implementing `IHostedService`
2. On startup, check if `warehouse_events` schema exists
3. Check if projection tables exist
4. Log critical error and throw exception if validation fails
5. Register service in DI container
6. Add configuration flag to disable validation (for fresh deployments)

**Acceptance**:
- Application fails to start if schema missing (unless validation disabled)
- Clear error message logged (which table missing)
- Configuration flag `SkipSchemaValidation` available in appsettings.json

**Tests**:
- Start app with missing schema → should fail
- Start app with complete schema → should succeed
- Start app with SkipSchemaValidation=true → should succeed even if schema missing

**Files**:
- `src/LKvitai.MES.Infrastructure/Events/SchemaValidationService.cs`
- `src/LKvitai.MES.Api/Program.cs` (register service)
- `src/LKvitai.MES.Api/appsettings.json` (add SkipSchemaValidation flag)
- Ref: `docs/master-data/master-data-06-ops-runbook-projections.md` (Preventive Measure 4)

---

### Task 0.6: Zero-Downtime Rebuild with Shadow Tables (1 day)
**Goal**: Implement projection rebuild using shadow tables (Marten built-in)

**Steps**:
1. Review Marten shadow table rebuild strategy
2. Implement rebuild API endpoint `/api/warehouse/v1/admin/projections/rebuild`
3. Add `resetProgress` parameter (start from event 0 or resume)
4. Monitor rebuild progress (log every 1000 events)
5. Atomic swap: shadow → primary on completion
6. Add rebuild duration metric (track in Application Insights)

**Acceptance**:
- Rebuild completes without 42P01 error
- Primary projection table remains queryable during rebuild
- Atomic swap on completion (no downtime)
- Rebuild duration logged and tracked

**Tests**:
- Rebuild projection with 10k events, verify no downtime
- Query projection during rebuild, verify returns stale data (not empty)
- Verify atomic swap (no partial data visible)

**Files**:
- `src/LKvitai.MES.Infrastructure/Events/ProjectionRebuildService.cs`
- `src/LKvitai.MES.Api/Api/Controllers/ProjectionsController.cs`
- Ref: `docs/master-data/master-data-06-ops-runbook-projections.md` (Operation 2: Rebuild Single Projection)

---

## Epic 1: Master Data Foundation (EF Core)

### Task 1.1: Entity Models with Constraints and Indexes (1.5 days)
**Goal**: Create EF Core entity models for all master data tables

**Steps**:
1. Create entity classes: Item, ItemCategory, Supplier, SupplierItemMapping, Location, UnitOfMeasure, ItemUoMConversion, ItemBarcode, HandlingUnitType, HandlingUnit, Lot, InboundShipment, InboundShipmentLine, AdjustmentReasonCode, SerialNumber (Phase 2 placeholder)
2. Configure relationships (FK, navigation properties)
3. Configure constraints (unique, check, default values)
4. Configure indexes (PK, FK, unique, query optimization)
5. Add IAuditable interface (CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
6. Configure EF Core conventions (table names, column types)

**Acceptance**:
- All entities match schema in `master-data-01-domain-model.md`
- All constraints enforced (unique SKU, barcode, etc.)
- All indexes created (FK, unique, query optimization)
- IAuditable applied to Item, Supplier, Location

**Tests**:
- Unit test: Validate entity constraints (required fields, check constraints)
- Integration test: Insert duplicate SKU → should fail with unique constraint violation
- Integration test: Insert item with invalid CategoryId → should fail with FK violation

**Files**:
- `src/LKvitai.MES.Domain/Entities/*.cs` (all entity classes)
- `src/LKvitai.MES.Infrastructure/Data/Configurations/*.cs` (EF Core configurations)
- Ref: `docs/master-data/master-data-01-domain-model.md` (all entity definitions)

---

### Task 1.2: Seed Data (0.5 day)
**Goal**: Create SQL script to seed reference data (UoM, virtual locations, reason codes, categories, HU types)

**Steps**:
1. Create seed data SQL script `scripts/seed-master-data.sql`
2. Insert 8 UnitOfMeasures (KG, G, L, ML, PCS, M, BOX, PKG)
3. Insert 7 virtual locations (RECEIVING, QC_HOLD, QUARANTINE, PRODUCTION, SHIPPING, SCRAP, RETURN_TO_SUPPLIER)
4. Insert 3 HandlingUnitTypes (PALLET, BOX, BAG)
5. Insert 8 AdjustmentReasonCodes (DAMAGE, THEFT, EVAPORATION, INVENTORY, SYSTEM_ERROR, EXPIRED, QC_REJECTED, PRODUCTION_SCRAP)
6. Insert 4 ItemCategories (RAW, FINISHED, FASTENERS, CHEMICALS)
7. Add script to EF Core migration (run on database creation)

**Acceptance**:
- Seed script runs without errors
- All reference data inserted
- Script idempotent (can run multiple times without duplicates)

**Tests**:
- Run seed script twice, verify no duplicate key errors
- Query each table, verify expected row counts

**Files**:
- `scripts/seed-master-data.sql`
- `src/LKvitai.MES.Infrastructure/Data/Migrations/*.cs` (call seed script in migration)
- Ref: `docs/master-data/master-data-01-domain-model.md` (Seed Data Summary section)

---

### Task 1.3: Audit Fields Pattern (0.5 day)
**Goal**: Implement automatic audit field population on SaveChanges

**Steps**:
1. Create IAuditable interface (CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
2. Override DbContext.SaveChanges to populate audit fields
3. Get current user from ICurrentUserService (injected)
4. On INSERT: Set CreatedBy, CreatedAt
5. On UPDATE: Set UpdatedBy, UpdatedAt
6. Publish MasterDataChanged event (for downstream systems)

**Acceptance**:
- Audit fields automatically populated on save
- CreatedAt/UpdatedAt use UTC timestamps
- CreatedBy/UpdatedBy populated from current user context
- MasterDataChanged event published on update

**Tests**:
- Insert entity, verify CreatedBy and CreatedAt populated
- Update entity, verify UpdatedBy and UpdatedAt populated
- Verify MasterDataChanged event published with correct payload

**Files**:
- `src/LKvitai.MES.Domain/Common/IAuditable.cs`
- `src/LKvitai.MES.Infrastructure/Data/WarehouseDbContext.cs` (override SaveChanges)
- `src/LKvitai.MES.Application/Services/ICurrentUserService.cs`
- Ref: `docs/master-data/master-data-01-domain-model.md` (Audit Fields Pattern section)

---

### Task 1.4: InternalSKU Auto-Generation Logic (1 day)
**Goal**: Implement SKU auto-generation with prefix (RM-0001, FG-0001)

**Steps**:
1. Create SKUGenerationService
2. Determine prefix based on CategoryId (RAW → RM, FINISHED → FG, default → ITEM)
3. Get next sequence from database (use separate SKUSequences table or app-level counter)
4. Format: `{Prefix}-{Sequence:D4}` (4-digit zero-padded)
5. Handle concurrent requests (optimistic concurrency on sequence table)
6. Add to Item creation logic (if InternalSKU blank, auto-generate)

**Acceptance**:
- SKU auto-generated if not provided
- Prefix determined by category (RM for raw materials, FG for finished goods)
- Sequence increments correctly (no gaps or duplicates)
- Concurrent requests handled (no duplicate SKUs)

**Tests**:
- Create item without SKU, verify auto-generated (e.g., RM-0001)
- Create 10 items concurrently, verify no duplicate SKUs
- Create item with explicit SKU, verify not overwritten

**Files**:
- `src/LKvitai.MES.Application/Services/SKUGenerationService.cs`
- `src/LKvitai.MES.Domain/Entities/SKUSequence.cs` (sequence table)
- `src/LKvitai.MES.Application/Commands/CreateItemCommandHandler.cs`
- Ref: `docs/master-data/master-data-01-domain-model.md` (InternalSKU Generation Rule section)

---

### Task 1.5: Database Migrations (0.5 day)
**Goal**: Create initial EF Core migration for all master data tables

**Steps**:
1. Run `dotnet ef migrations add InitialMasterData`
2. Review generated migration (verify all tables, constraints, indexes)
3. Add seed data script call to migration (Up method)
4. Test migration on clean database
5. Test rollback (Down method)

**Acceptance**:
- Migration creates all master data tables
- All constraints and indexes created
- Seed data inserted on migration
- Rollback drops all tables cleanly

**Tests**:
- Apply migration to empty database, verify all tables created
- Run seed data, verify reference data inserted
- Rollback migration, verify all tables dropped

**Files**:
- `src/LKvitai.MES.Infrastructure/Data/Migrations/*.cs`
- Ref: `docs/master-data/master-data-01-domain-model.md` (all entity schemas)

---

## Epic 2: Bulk Import System

### Task 2.1: Excel Template Generation (1 day)
**Goal**: Generate Excel templates for Items, Suppliers, Mappings, Barcodes, Locations

**Steps**:
1. Add EPPlus or ClosedXML NuGet package
2. Create ExcelTemplateService
3. Implement template generation for each entity type (5 templates)
4. Add header row with column names (match entity properties)
5. Add example row with sample data
6. Add data validation (dropdowns for FK fields like CategoryCode, BaseUoM)
7. Create API endpoint `/api/warehouse/v1/admin/import/{entityType}/template`

**Acceptance**:
- 5 templates generated (Items, Suppliers, Mappings, Barcodes, Locations)
- Header row matches expected column names
- Example row provides guidance
- Data validation added for FK fields (dropdowns)

**Tests**:
- Download each template, verify header row correct
- Verify example row present
- Open in Excel, verify dropdowns work

**Files**:
- `src/LKvitai.MES.Application/Services/ExcelTemplateService.cs`
- `src/LKvitai.MES.Api/Api/Controllers/ImportController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (GET /api/warehouse/v1/admin/import/items/template)

---


### Task 2.2: Excel Parsing and Validation Engine (1.5 days)
**Goal**: Parse uploaded Excel files and validate all rows before import

**Steps**:
1. Create ExcelParsingService
2. Implement header validation (column names match template)
3. Implement row parsing (map columns to entity properties)
4. Implement data type validation (int, decimal, date, bool)
5. Implement required field validation
6. Implement FK validation (CategoryCode exists, BaseUoM exists, etc.)
7. Implement uniqueness validation (SKU, Barcode within file and against DB)
8. Collect all errors (row, column, value, message)
9. Return validation result (success/failure, error list)

**Acceptance**:
- Header validation catches missing/extra columns
- Data type validation catches invalid formats
- FK validation catches non-existent references
- Uniqueness validation catches duplicates (within file and DB)
- All errors collected (not fail-fast)

**Tests**:
- Upload file with missing column → should fail with header error
- Upload file with invalid BaseUoM → should fail with FK error
- Upload file with duplicate SKU → should fail with uniqueness error
- Upload valid file → should pass validation

**Files**:
- `src/LKvitai.MES.Application/Services/ExcelParsingService.cs`
- `src/LKvitai.MES.Application/Services/ImportValidationService.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/admin/import/items)

---

### Task 2.3: Upsert Logic (1 day)
**Goal**: Insert new records, update existing records based on identity key

**Steps**:
1. Create ImportService for each entity type
2. Implement identity matching logic (Items by InternalSKU, Suppliers by Code, etc.)
3. For each row: Check if entity exists (by identity key)
4. If exists: Update all fields except PK, immutable fields (InternalSKU, CreatedAt, CreatedBy)
5. If not exists: Insert new record
6. Handle FK resolution (CategoryCode → CategoryId, BaseUoM → UoM.Code)
7. Track insert/update counts
8. Return import result (inserted, updated, skipped, errors)

**Acceptance**:
- Existing records updated (matched by identity key)
- New records inserted
- Immutable fields not overwritten (InternalSKU, CreatedAt)
- FK resolution works (codes mapped to IDs)
- Import result accurate (counts match actual DB changes)

**Tests**:
- Import file with 10 new items → verify 10 inserted
- Import same file again → verify 10 updated (not duplicated)
- Import file with mix of new and existing → verify correct insert/update counts

**Files**:
- `src/LKvitai.MES.Application/Services/ItemImportService.cs`
- `src/LKvitai.MES.Application/Services/SupplierImportService.cs`
- (similar for other entity types)
- Ref: `docs/master-data/master-data-01-domain-model.md` (Upsert Behavior section)

---

### Task 2.4: Dry-Run Mode (0.5 day)
**Goal**: Validate import without writing to database

**Steps**:
1. Add `dryRun` parameter to import API
2. If dryRun=true: Run validation, return errors, do NOT call SaveChanges
3. If dryRun=false: Run validation, if no errors, call SaveChanges
4. Return validation result in both cases (errors, warnings, counts)

**Acceptance**:
- Dry-run mode validates without DB writes
- Dry-run result shows what WOULD be inserted/updated
- Non-dry-run mode writes to DB only if validation passes

**Tests**:
- Upload file with dryRun=true, verify no DB changes
- Upload file with dryRun=false, verify DB changes
- Upload invalid file with dryRun=false, verify no DB changes (validation failed)

**Files**:
- `src/LKvitai.MES.Application/Services/ItemImportService.cs` (add dryRun parameter)
- `src/LKvitai.MES.Api/Api/Controllers/ImportController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (dryRun query parameter)

---

### Task 2.5: Error Reporting (0.5 day)
**Goal**: Generate detailed error report for failed imports

**Steps**:
1. Create ImportErrorReport model (row, column, value, message)
2. Collect errors during validation (all errors, not fail-fast)
3. Return errors in API response (JSON array)
4. Add "Download Error Report" button in UI (generates Excel with errors)
5. Error report includes: Row number, Column name, Invalid value, Error message

**Acceptance**:
- All validation errors collected (not just first error)
- Error report includes row/column/value/message
- Error report downloadable as Excel
- UI displays error count and first 10 errors

**Tests**:
- Upload file with 5 errors, verify all 5 reported
- Download error report, verify Excel format correct

**Files**:
- `src/LKvitai.MES.Application/Models/ImportErrorReport.cs`
- `src/LKvitai.MES.Application/Services/ExcelParsingService.cs`
- `src/LKvitai.MES.Api/Api/Controllers/ImportController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/admin/import/items response)

---

### Task 2.6: Batch Insert Optimization (0.5 day)
**Goal**: Optimize import for >1000 rows using bulk insert

**Steps**:
1. Add EFCore.BulkExtensions NuGet package
2. Detect row count in import file
3. If rows > 1000: Use BulkInsert/BulkUpdate
4. If rows <= 1000: Use standard AddRange/SaveChanges
5. Measure import duration, log performance metrics

**Acceptance**:
- Import 500 items completes in <5 minutes
- Import 5000 items completes in <15 minutes
- Bulk insert used for large files (>1000 rows)

**Tests**:
- Import 100 rows, verify standard insert used
- Import 2000 rows, verify bulk insert used
- Measure duration for 500 rows, verify <5 minutes

**Files**:
- `src/LKvitai.MES.Application/Services/ItemImportService.cs`
- Ref: `docs/master-data/master-data-05-implementation-plan-and-tests.md` (Import Optimization section)

---

## Epic 3: Event Store & Projections (Marten)

### Task 3.1: Event Contracts (1 day)
**Goal**: Define event schemas for all operational events

**Steps**:
1. Create event classes: GoodsReceived, StockMoved, PickCompleted, StockAdjusted, ReservationCreated, ReservationReleased, QCPassed, QCFailed
2. Add event metadata (eventId, aggregateId, timestamp, userId, traceId)
3. Add event payload (specific to each event type)
4. Configure Marten to serialize events as JSON
5. Add event versioning support (for future schema changes)

**Acceptance**:
- All 8 event types defined
- Event metadata consistent across all events
- Event payload matches schema in `master-data-03-events-and-projections.md`
- Events serializable to JSON

**Tests**:
- Serialize/deserialize each event type, verify no data loss
- Append event to Marten, verify stored correctly

**Files**:
- `src/LKvitai.MES.Domain/Events/*.cs` (all event classes)
- `src/LKvitai.MES.Infrastructure/Events/MartenConfiguration.cs`
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (Phase 1 Event Contracts section)

---

### Task 3.2: AvailableStock Projection (1.5 days)
**Goal**: Implement projection for real-time stock availability per Item/Location/Lot

**Steps**:
1. Create AvailableStock projection class (Marten inline projection)
2. Implement event handlers: GoodsReceived (add qty), StockMoved (subtract from source, add to destination), PickCompleted (subtract), StockAdjusted (add delta), ReservationCreated (increment reserved_qty), ReservationReleased (decrement reserved_qty)
3. Configure projection table schema (item_id, location_id, lot_id, qty, reserved_qty, available_qty computed)
4. Add indexes (item_id, location_id, lot_id, expiry_date, available_qty)
5. Denormalize item info (internal_sku, item_name, location_code, lot_number) for query performance
6. Test projection rebuild (apply 1000 events, verify final state)

**Acceptance**:
- Projection updates on all relevant events
- Qty calculations correct (add/subtract logic)
- Reserved_qty tracked separately
- Available_qty computed as (qty - reserved_qty)
- Denormalized fields populated

**Tests**:
- Apply GoodsReceived event, verify qty added
- Apply StockMoved event, verify qty moved between locations
- Apply PickCompleted event, verify qty subtracted
- Apply ReservationCreated event, verify reserved_qty incremented
- Rebuild projection from 1000 events, verify final state matches expected

**Files**:
- `src/LKvitai.MES.Infrastructure/Projections/AvailableStockProjection.cs`
- `src/LKvitai.MES.Infrastructure/Events/MartenConfiguration.cs` (register projection)
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (AvailableStock section)

---

### Task 3.3: LocationBalance Projection (1 day)
**Goal**: Implement projection for location capacity utilization

**Steps**:
1. Create LocationBalance projection class
2. Implement event handlers: GoodsReceived, StockMoved, PickCompleted, StockAdjusted (recalculate weight/volume)
3. Calculate total_weight = SUM(qty * item.weight) for all items in location
4. Calculate total_volume = SUM(qty * item.volume) for all items in location
5. Calculate utilization_weight = total_weight / max_weight
6. Calculate utilization_volume = total_volume / max_volume
7. Count distinct items in location (item_count)

**Acceptance**:
- Projection updates on stock movement events
- Weight/volume calculations correct
- Utilization percentages calculated
- Item count accurate

**Tests**:
- Apply GoodsReceived event, verify weight/volume added
- Apply StockMoved event, verify weight/volume moved between locations
- Verify utilization calculation (e.g., 50kg / 100kg max = 0.5)

**Files**:
- `src/LKvitai.MES.Infrastructure/Projections/LocationBalanceProjection.cs`
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (LocationBalance section)

---

### Task 3.4: ActiveReservations Projection (0.5 day)
**Goal**: Implement projection for hard locks on stock

**Steps**:
1. Create ActiveReservations projection class
2. Implement event handlers: ReservationCreated (insert record), PickCompleted (update status=Completed), ReservationReleased (update status=Released)
3. Add expiry check (background job to mark expired reservations)
4. Track reservation status (Active/Completed/Cancelled/Expired)

**Acceptance**:
- Projection tracks all active reservations
- Status updated on pick completion or release
- Expired reservations marked (background job)

**Tests**:
- Apply ReservationCreated event, verify record inserted
- Apply PickCompleted event, verify status=Completed
- Simulate expiry, verify status=Expired

**Files**:
- `src/LKvitai.MES.Infrastructure/Projections/ActiveReservationsProjection.cs`
- `src/LKvitai.MES.Infrastructure/BackgroundJobs/ReservationExpiryJob.cs`
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (ActiveReservations section)

---

### Task 3.5: InboundShipmentSummary Projection (0.5 day)
**Goal**: Implement projection for receiving dashboard

**Steps**:
1. Create InboundShipmentSummary projection class
2. Implement event handlers: InboundShipmentCreated (insert record), GoodsReceived (increment total_received_qty, update status if complete)
3. Denormalize supplier info (supplier_name)
4. Calculate completion percentage (total_received_qty / total_expected_qty)

**Acceptance**:
- Projection shows shipment summary (expected vs received)
- Status updated when all lines received
- Supplier info denormalized

**Tests**:
- Apply InboundShipmentCreated event, verify record inserted
- Apply GoodsReceived event, verify total_received_qty incremented
- Verify status=Complete when all lines received

**Files**:
- `src/LKvitai.MES.Infrastructure/Projections/InboundShipmentSummaryProjection.cs`
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (InboundShipmentSummary section)

---

### Task 3.6: AdjustmentHistory Projection (0.5 day)
**Goal**: Implement projection for audit trail of stock adjustments

**Steps**:
1. Create AdjustmentHistory projection class
2. Implement event handler: StockAdjusted (insert record)
3. Denormalize item/location info (internal_sku, item_name, location_code)
4. Denormalize user info (user_name)
5. Add indexes (item_id, location_id, user_id, timestamp, reason_code)

**Acceptance**:
- Projection records all adjustments
- Denormalized fields populated
- Queryable by item, location, user, reason, date range

**Tests**:
- Apply StockAdjusted event, verify record inserted
- Query by item_id, verify correct records returned
- Query by date range, verify correct records returned

**Files**:
- `src/LKvitai.MES.Infrastructure/Projections/AdjustmentHistoryProjection.cs`
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (AdjustmentHistory section)

---

### Task 3.7: Projection Health Check API (0.5 day)
**Goal**: Expose projection lag and status via health check endpoint

**Steps**:
1. Create ProjectionHealthService
2. Query mt_event_progression table (last_seq_id per projection)
3. Query mt_events table (max seq_id = latest event)
4. Calculate lag: latest_event - last_processed_event
5. Calculate lag_seconds: current_time - last_updated_time
6. Return health status: Healthy (<1 sec), Degraded (1-60 sec), Unhealthy (>60 sec)
7. Add to health check endpoint `/api/warehouse/v1/health`

**Acceptance**:
- Health check returns projection status for all projections
- Lag calculated correctly (events and seconds)
- Status thresholds enforced (Healthy/Degraded/Unhealthy)

**Tests**:
- Query health check, verify projection status returned
- Simulate lag (stop projection daemon), verify status=Unhealthy

**Files**:
- `src/LKvitai.MES.Application/Services/ProjectionHealthService.cs`
- `src/LKvitai.MES.Api/Api/Controllers/HealthController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (GET /api/warehouse/v1/health)

---

## Epic 4: Receiving Workflow

### Task 4.1: Inbound Shipment Creation (1 day)
**Goal**: Create inbound shipment with lines (PO-based)

**Steps**:
1. Create InboundShipment entity (EF Core)
2. Create InboundShipmentLine entity (EF Core)
3. Create CreateInboundShipmentCommand (MediatR)
4. Validate: SupplierId exists, ItemIds exist, ExpectedQty > 0
5. Insert shipment and lines (EF Core transaction)
6. Return shipment ID
7. Create API endpoint POST `/api/warehouse/v1/receiving/shipments`

**Acceptance**:
- Shipment created with lines
- Validation enforced (supplier exists, items exist)
- Transaction ensures atomicity (shipment + lines)

**Tests**:
- Create shipment with 3 lines, verify inserted
- Create shipment with invalid SupplierId, verify fails
- Create shipment with invalid ItemId, verify fails

**Files**:
- `src/LKvitai.MES.Domain/Entities/InboundShipment.cs`
- `src/LKvitai.MES.Domain/Entities/InboundShipmentLine.cs`
- `src/LKvitai.MES.Application/Commands/CreateInboundShipmentCommand.cs`
- `src/LKvitai.MES.Api/Api/Controllers/ReceivingController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/receiving/shipments)

---


### Task 4.2: Receive Goods with Barcode Scanning (1.5 days)
**Goal**: Record goods receipt, create GoodsReceived event

**Steps**:
1. Create ReceiveGoodsCommand (MediatR)
2. Validate: ShipmentId exists, LineId exists, ReceivedQty > 0
3. If Item.RequiresLotTracking: Validate LotNumber provided
4. Create or get Lot (by ItemId + LotNumber)
5. Determine destination location: If Item.RequiresQC → QC_HOLD, else → RECEIVING
6. Append GoodsReceived event to Marten
7. Update InboundShipmentLine.ReceivedQty (denormalized, updated by projection)
8. Create API endpoint POST `/api/warehouse/v1/receiving/shipments/{id}/receive`

**Acceptance**:
- GoodsReceived event appended
- Lot created if not exists
- Destination location determined by RequiresQC flag
- ReceivedQty updated (via projection)

**Tests**:
- Receive goods for item without lot tracking, verify event appended
- Receive goods for item with lot tracking, verify lot created
- Receive goods for item requiring QC, verify destination=QC_HOLD
- Receive goods without lot number for lot-tracked item, verify fails

**Files**:
- `src/LKvitai.MES.Application/Commands/ReceiveGoodsCommand.cs`
- `src/LKvitai.MES.Api/Api/Controllers/ReceivingController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/receiving/shipments/{id}/receive)

---

### Task 4.3: Lot Tracking (0.5 day)
**Goal**: Enforce lot tracking for RequiresLotTracking items

**Steps**:
1. Add validation in ReceiveGoodsCommand: If Item.RequiresLotTracking and LotNumber blank → fail
2. Create Lot entity if not exists (by ItemId + LotNumber)
3. Store ProductionDate, ExpiryDate in Lot
4. Include LotId in GoodsReceived event
5. Display lot info in UI (lot number, expiry date)

**Acceptance**:
- Lot tracking enforced for RequiresLotTracking items
- Lot created on first receipt
- Lot reused on subsequent receipts (same ItemId + LotNumber)
- Expiry date tracked

**Tests**:
- Receive lot-tracked item without lot number, verify fails
- Receive lot-tracked item with lot number, verify lot created
- Receive same lot again, verify lot reused (not duplicated)

**Files**:
- `src/LKvitai.MES.Application/Commands/ReceiveGoodsCommand.cs`
- `src/LKvitai.MES.Domain/Entities/Lot.cs`
- Ref: `docs/master-data/master-data-01-domain-model.md` (Lot entity)

---

### Task 4.4: QC Pass/Fail Actions (1 day)
**Goal**: Move stock from QC_HOLD to RECEIVING or QUARANTINE

**Steps**:
1. Create QCPassCommand (MediatR)
2. Create QCFailCommand (MediatR)
3. Validate: ItemId exists, LotId exists, Qty > 0, sufficient stock in QC_HOLD
4. Append QCPassed or QCFailed event (triggers StockMoved event)
5. QCPassed: Move from QC_HOLD to RECEIVING
6. QCFailed: Move from QC_HOLD to QUARANTINE, require ReasonCode
7. Create API endpoints POST `/api/warehouse/v1/qc/pass` and `/api/warehouse/v1/qc/fail`

**Acceptance**:
- QC pass moves stock to RECEIVING
- QC fail moves stock to QUARANTINE
- Reason code required for QC fail
- Insufficient stock validation enforced

**Tests**:
- Pass QC for 1000 PCS, verify moved to RECEIVING
- Fail QC for 1000 PCS, verify moved to QUARANTINE
- Fail QC without reason code, verify fails

**Files**:
- `src/LKvitai.MES.Application/Commands/QCPassCommand.cs`
- `src/LKvitai.MES.Application/Commands/QCFailCommand.cs`
- `src/LKvitai.MES.Api/Api/Controllers/QCController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/qc/pass)

---

### Task 4.5: Receiving Dashboard (1 day)
**Goal**: Display inbound shipments with status tracking

**Steps**:
1. Create query endpoint GET `/api/warehouse/v1/receiving/shipments`
2. Query InboundShipmentSummary projection (filterable by supplier, status, date)
3. Display: ShipmentId, ReferenceNumber, Supplier, ExpectedDate, Status, TotalLines, TotalExpectedQty, TotalReceivedQty
4. Add pagination (50 items per page)
5. Add filters (supplier, status, date range)

**Acceptance**:
- Dashboard displays all shipments
- Filters work (supplier, status, date)
- Pagination works
- Status accurate (Draft/Partial/Complete)

**Tests**:
- Query shipments, verify all returned
- Filter by supplier, verify correct shipments returned
- Filter by status=Complete, verify only complete shipments returned

**Files**:
- `src/LKvitai.MES.Application/Queries/GetInboundShipmentsQuery.cs`
- `src/LKvitai.MES.Api/Api/Controllers/ReceivingController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (GET /api/warehouse/v1/receiving/shipments)

---

## Epic 5: Putaway Workflow

### Task 5.1: Putaway Task List (0.5 day)
**Goal**: Display items in RECEIVING location awaiting putaway

**Steps**:
1. Create query endpoint GET `/api/warehouse/v1/putaway/tasks`
2. Query AvailableStock projection (filter by LocationId=RECEIVING)
3. Display: ItemId, InternalSKU, ItemName, Qty, LotNumber, ReceivedAt
4. Add pagination

**Acceptance**:
- Task list displays all items in RECEIVING
- Sorted by received timestamp (oldest first)
- Pagination works

**Tests**:
- Receive 3 items, verify all appear in task list
- Complete putaway for 1 item, verify removed from task list

**Files**:
- `src/LKvitai.MES.Application/Queries/GetPutawayTasksQuery.cs`
- `src/LKvitai.MES.Api/Api/Controllers/PutawayController.cs`
- Ref: `docs/master-data/master-data-04-ui-scope.md` (Putaway Tasks page)

---

### Task 5.2: Location Barcode Scanning (0.5 day)
**Goal**: Validate location barcode during putaway

**Steps**:
1. Create barcode lookup endpoint GET `/api/warehouse/v1/barcodes/lookup?code={barcode}&type=location`
2. Query Locations table by Barcode
3. Return location info (Id, Code, Type, MaxWeight, MaxVolume, Status)
4. Validate location status (must be Active, not Blocked/Maintenance)

**Acceptance**:
- Barcode lookup returns location info
- Invalid barcode returns 404
- Blocked location returns 422 (business rule violation)

**Tests**:
- Lookup valid location barcode, verify location returned
- Lookup invalid barcode, verify 404
- Lookup blocked location, verify 422

**Files**:
- `src/LKvitai.MES.Api/Api/Controllers/BarcodesController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (GET /api/warehouse/v1/barcodes/lookup)

---

### Task 5.3: Capacity Warning (0.5 day)
**Goal**: Display location capacity utilization during putaway

**Steps**:
1. Query LocationBalance projection for target location
2. Calculate new utilization after putaway: (current_weight + item_weight * qty) / max_weight
3. Display warning if utilization > 80% (not blocking in Phase 1)
4. Display error if utilization > 100% (blocking in Phase 2, warning only in Phase 1)

**Acceptance**:
- Capacity utilization displayed
- Warning shown if >80%
- Not blocking in Phase 1 (user can proceed)

**Tests**:
- Putaway to location at 50% capacity, verify no warning
- Putaway to location at 85% capacity, verify warning shown
- Putaway to location at 105% capacity, verify warning shown (not blocked)

**Files**:
- `src/LKvitai.MES.Application/Commands/PutawayCommand.cs`
- `src/LKvitai.MES.Application/Queries/GetLocationBalanceQuery.cs`
- Ref: `docs/master-data/master-data-04-ui-scope.md` (Putaway modal)

---

### Task 5.4: StockMoved Event Emission (0.5 day)
**Goal**: Emit StockMoved event on putaway

**Steps**:
1. Create PutawayCommand (MediatR)
2. Validate: ItemId exists, FromLocationId=RECEIVING, ToLocationId valid, Qty > 0, sufficient stock
3. Append StockMoved event to Marten
4. Event payload: ItemId, Qty, FromLocationId (RECEIVING), ToLocationId (storage), LotId, MovementType=Putaway
5. Create API endpoint POST `/api/warehouse/v1/putaway`

**Acceptance**:
- StockMoved event appended
- Stock moved from RECEIVING to storage (via projection)
- Insufficient stock validation enforced

**Tests**:
- Putaway 1000 PCS, verify StockMoved event appended
- Putaway more than available, verify fails
- Verify projection updated (stock moved)

**Files**:
- `src/LKvitai.MES.Application/Commands/PutawayCommand.cs`
- `src/LKvitai.MES.Api/Api/Controllers/PutawayController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/putaway)

---

## Epic 6: Picking Workflow

### Task 6.1: Pick Task Creation (0.5 day)
**Goal**: Create pick task (manual, Phase 1)

**Steps**:
1. Create PickTask entity (EF Core)
2. Create CreatePickTaskCommand (MediatR)
3. Validate: OrderId provided, ItemId exists, Qty > 0
4. Insert PickTask (Status=Pending)
5. Optionally assign to user (AssignedToUserId)
6. Create API endpoint POST `/api/warehouse/v1/picking/tasks`

**Acceptance**:
- Pick task created
- Status=Pending
- Assignable to user (optional)

**Tests**:
- Create pick task, verify inserted
- Create pick task with invalid ItemId, verify fails

**Files**:
- `src/LKvitai.MES.Domain/Entities/PickTask.cs`
- `src/LKvitai.MES.Application/Commands/CreatePickTaskCommand.cs`
- `src/LKvitai.MES.Api/Api/Controllers/PickingController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/picking/tasks)

---

### Task 6.2: Pick Execution (1.5 days)
**Goal**: Execute pick with location selection and barcode scan

**Steps**:
1. Query AvailableStock projection for ItemId (all locations with stock)
2. Display locations sorted by expiry date (FEFO - earliest first)
3. User selects location
4. Scan location barcode (validate matches selected location)
5. Scan item barcode (validate matches expected item)
6. Enter picked qty (validate <= available qty)
7. Create CompletePickTaskCommand (MediatR)
8. Append PickCompleted event to Marten
9. Update PickTask status=Completed
10. Create API endpoint POST `/api/warehouse/v1/picking/tasks/{id}/complete`

**Acceptance**:
- Location selection shows available stock
- FEFO sorting (earliest expiry first)
- Barcode validation enforced (location and item)
- PickCompleted event appended
- Task status updated

**Tests**:
- Complete pick with valid barcodes, verify event appended
- Complete pick with wrong location barcode, verify fails
- Complete pick with wrong item barcode, verify fails
- Complete pick with qty > available, verify fails

**Files**:
- `src/LKvitai.MES.Application/Commands/CompletePickTaskCommand.cs`
- `src/LKvitai.MES.Application/Queries/GetAvailableStockQuery.cs`
- `src/LKvitai.MES.Api/Api/Controllers/PickingController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/picking/tasks/{id}/complete)

---

### Task 6.3: FEFO Location Suggestion (0.5 day)
**Goal**: Sort locations by expiry date (earliest first)

**Steps**:
1. Query AvailableStock projection for ItemId
2. Filter by Qty > 0
3. Sort by ExpiryDate ASC (nulls last)
4. Display top 5 locations

**Acceptance**:
- Locations sorted by expiry date
- Earliest expiry shown first
- Null expiry dates shown last

**Tests**:
- Query stock with 3 lots (different expiry dates), verify sorted correctly
- Query stock with null expiry dates, verify shown last

**Files**:
- `src/LKvitai.MES.Application/Queries/GetAvailableStockQuery.cs`
- Ref: `docs/master-data/master-data-04-ui-scope.md` (Pick Execution page)

---

### Task 6.4: PickCompleted Event Emission (0.5 day)
**Goal**: Emit PickCompleted event on pick completion

**Steps**:
1. Append PickCompleted event to Marten
2. Event payload: PickTaskId, OrderId, ItemId, PickedQty, FromLocationId, ToLocationId (SHIPPING), LotId, ScannedBarcode
3. Projection updates: Subtract from source location, add to SHIPPING location

**Acceptance**:
- PickCompleted event appended
- Stock moved from source to SHIPPING (via projection)

**Tests**:
- Complete pick, verify event appended
- Verify projection updated (stock moved)

**Files**:
- `src/LKvitai.MES.Application/Commands/CompletePickTaskCommand.cs`
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (PickCompleted event)

---

### Task 6.5: Pick History Report (0.5 day)
**Goal**: Display completed picks with filters

**Steps**:
1. Create query endpoint GET `/api/warehouse/v1/picking/history`
2. Query PickTask table (Status=Completed)
3. Join with Items, Locations for display names
4. Add filters (ItemId, OrderId, UserId, DateRange)
5. Add pagination

**Acceptance**:
- History displays all completed picks
- Filters work
- Pagination works

**Tests**:
- Complete 3 picks, verify all in history
- Filter by ItemId, verify correct picks returned

**Files**:
- `src/LKvitai.MES.Application/Queries/GetPickHistoryQuery.cs`
- `src/LKvitai.MES.Api/Api/Controllers/PickingController.cs`
- Ref: `docs/master-data/master-data-04-ui-scope.md` (Pick History page)

---

## Epic 7: Stock Adjustments

### Task 7.1: Adjustment Creation (1 day)
**Goal**: Create manual stock adjustment with reason code

**Steps**:
1. Create CreateStockAdjustmentCommand (MediatR)
2. Validate: ItemId exists, LocationId exists, QtyDelta != 0, ReasonCode exists
3. Query current stock (AvailableStock projection)
4. Calculate new qty: current_qty + qty_delta
5. Warn if new qty < 0 (not blocking - investigation needed)
6. Append StockAdjusted event to Marten
7. Create API endpoint POST `/api/warehouse/v1/adjustments`

**Acceptance**:
- Adjustment creates StockAdjusted event
- Reason code required
- Negative stock warning (not blocking)

**Tests**:
- Create adjustment with positive delta, verify event appended
- Create adjustment with negative delta, verify event appended
- Create adjustment without reason code, verify fails
- Create adjustment resulting in negative stock, verify warning (not blocked)

**Files**:
- `src/LKvitai.MES.Application/Commands/CreateStockAdjustmentCommand.cs`
- `src/LKvitai.MES.Api/Api/Controllers/AdjustmentsController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (POST /api/warehouse/v1/adjustments)

---

### Task 7.2: Confirmation Dialog (0.5 day)
**Goal**: Require confirmation for irreversible adjustments

**Steps**:
1. Display confirmation dialog before submitting adjustment
2. Show: ItemSKU, ItemName, LocationCode, QtyDelta, NewQty, ReasonCode
3. Show warning: "This action cannot be undone"
4. Require explicit confirmation (button click)

**Acceptance**:
- Confirmation dialog shown
- Adjustment not submitted without confirmation
- Warning message displayed

**Tests**:
- UI test: Click adjust, verify confirmation dialog shown
- UI test: Cancel confirmation, verify adjustment not submitted
- UI test: Confirm, verify adjustment submitted

**Files**:
- `src/LKvitai.MES.UI/Pages/Adjustments/CreateAdjustment.razor`
- Ref: `docs/master-data/master-data-04-ui-scope.md` (Adjustments page)

---

### Task 7.3: StockAdjusted Event Emission (0.5 day)
**Goal**: Emit StockAdjusted event on adjustment

**Steps**:
1. Append StockAdjusted event to Marten
2. Event payload: AdjustmentId, ItemId, LocationId, QtyDelta, ReasonCode, Notes, UserId
3. Projection updates: Add qty_delta to AvailableStock

**Acceptance**:
- StockAdjusted event appended
- Projection updated (qty adjusted)

**Tests**:
- Create adjustment, verify event appended
- Verify projection updated (qty changed)

**Files**:
- `src/LKvitai.MES.Application/Commands/CreateStockAdjustmentCommand.cs`
- Ref: `docs/master-data/master-data-03-events-and-projections.md` (StockAdjusted event)

---

### Task 7.4: Adjustment History Report (0.5 day)
**Goal**: Display adjustment history with filters

**Steps**:
1. Create query endpoint GET `/api/warehouse/v1/adjustments`
2. Query AdjustmentHistory projection
3. Add filters (ItemId, LocationId, ReasonCode, UserId, DateRange)
4. Add pagination
5. Add CSV export

**Acceptance**:
- History displays all adjustments
- Filters work
- Pagination works
- CSV export works

**Tests**:
- Create 3 adjustments, verify all in history
- Filter by ReasonCode, verify correct adjustments returned
- Export CSV, verify format correct

**Files**:
- `src/LKvitai.MES.Application/Queries/GetAdjustmentHistoryQuery.cs`
- `src/LKvitai.MES.Api/Api/Controllers/AdjustmentsController.cs`
- Ref: `docs/master-data/master-data-02-api-contracts.md` (GET /api/warehouse/v1/adjustments)

---

## Open Questions

### Question 1: Projection Rebuild Downtime Tolerance
**Context**: Projection rebuild may take 5-30 minutes depending on event count
**Question**: Is 5-minute downtime acceptable for projection rebuild, or do we need zero-downtime rebuild (shadow tables)?
**Impact**: Zero-downtime adds complexity (shadow table swap logic)
**Recommendation**: Start with acceptable downtime (5 min), add zero-downtime in Phase 2 if needed

### Question 2: Barcode Scanner Hardware
**Context**: UI supports barcode scanning via input field (keyboard wedge) or camera (WebRTC)
**Question**: What barcode scanner hardware will be used? USB handheld? Built-in tablet camera?
**Impact**: Camera-based scanning requires WebRTC implementation, USB scanner works out-of-box
**Recommendation**: Support both (USB primary, camera fallback)

### Question 3: Concurrent Reservation Handling
**Context**: Multiple orders may try to reserve same stock simultaneously
**Question**: Should reservations use pessimistic locking (block concurrent requests) or optimistic (retry on conflict)?
**Impact**: Pessimistic locking reduces throughput, optimistic may cause retry storms
**Recommendation**: Optimistic with jittered backoff (3 retries max)

### Question 4: Negative Stock Policy
**Context**: Stock adjustments may result in negative stock (investigation needed)
**Question**: Should negative stock be blocked (hard constraint) or allowed with warning (soft constraint)?
**Impact**: Hard constraint may block legitimate corrections, soft constraint may hide data quality issues
**Recommendation**: Allow with warning in Phase 1, add approval workflow in Phase 2

### Question 5: Import Error Threshold
**Context**: Import may have partial errors (e.g., 5 errors out of 500 rows)
**Question**: Should import fail entirely on any error, or allow partial import (skip error rows)?
**Impact**: Fail-fast prevents partial data, skip-errors allows bulk import with manual fixes
**Recommendation**: Fail-fast by default, add `skipErrors` flag for advanced users


ADDITIONAL IMPORTANT APPEND
## Epic 1 (additions): Cross-cutting infrastructure

### Task 1.6 - Authorization / RBAC enforcement
- Goal: enforce role-based access for Admin vs Operator vs Manager screens and APIs.
- Scope:
  - Define roles: WarehouseAdmin, WarehouseManager, Operator, QCInspector (align with baseline).
  - Add authorization attributes/policies for API endpoints:
    - /api/admin/* -> WarehouseAdmin
    - adjustments endpoints -> WarehouseManager (or Admin)
    - receiving/qc actions -> QCInspector or Manager
    - read-only stock queries -> Operator+
  - Ensure WebUI routes/pages are protected consistently.
- Done when:
  - Unauthorized access returns 401/403 with ProblemDetails (traceId preserved).
  - Integration tests cover at least 2 roles and 2 endpoints.
- Estimate: 1.0d

### Task 1.7 - ProblemDetails + traceId standardization (server + clients)
- Goal: all APIs return consistent ProblemDetails + traceId; all typed clients parse and surface traceId; UI shows Error ID.
- Scope:
  - Ensure middleware/filters always include traceId in ProblemDetails extensions.
  - Normalize error codes where applicable (errorCode + traceId contract for WebUI).
  - Add/extend typed client tests for traceId parsing (pattern already used in UI scope).
- Done when:
  - Every failing API call in WebUI shows ErrorBanner with Error ID.
  - Unit tests cover ProblemDetails parsing for at least: admin import, adjustments, receiving.
- Estimate: 0.5d


## Epic 8 - Admin UI (Master Data CRUD + Import Wizard)

### Task 8.1 - Admin navigation + layout for master data
- Add Admin section in nav, routes:
  - /admin/items
  - /admin/suppliers
  - /admin/locations
  - /admin/categories
  - /admin/import
- Reuse existing patterns: loading/error banners, pagination, CSV export where relevant.
- Estimate: 1.0d

### Task 8.2 - Items management UI (list/search/create/edit/deactivate)
- List with filters: SKU/Name/Category/Status
- Create/Edit modal (minimal fields per spec)
- Deactivate sets Status=Discontinued (no delete)
- Estimate: 2.0d

### Task 8.3 - Suppliers management UI (list/search/create/edit)
- List with filters, CRUD modal
- Link to Supplier-Item mappings page
- Estimate: 1.0d

### Task 8.4 - Locations management UI (list/hierarchy + create/edit)
- Flat list with parent indicator (tree can be Phase 2)
- Virtual locations visible but protected from editing critical fields (barcode/code)
- Estimate: 1.5d

### Task 8.5 - Categories management UI (basic hierarchy CRUD)
- Minimal: create/edit, prevent delete if referenced/has children
- Estimate: 1.0d

### Task 8.6 - Import wizard UI (dry-run + commit + error report)
- Tabs: Items, Suppliers, Supplier mappings, Item barcodes, Locations
- Upload file, dryRun toggle, show validation results table (row/column/message)
- “Download template” buttons (static templates or generated)
- Estimate: 1.5d


## Epic 9 - Reports UI (Phase 1 minimal)

### Task 9.1 - Stock Level report page
- Reuse AvailableStock projection/table patterns
- Filters: Item, Location, Category, Include virtual, Include reserved
- Export CSV
- Estimate: 1.5d

### Task 9.2 - Receiving history report page
- Date range + supplier + status
- Export CSV
- Estimate: 1.0d

### Task 9.3 - Pick history report page
- Date range + orderId + operator
- Export CSV
- Estimate: 1.0d


## Epic 10 - Operational UI (Phase 1 minimal ops)

### Task 10.1 - Projections admin UX hardening
- Improve Projections page:
  - show friendly hint when rebuild fails (DB schema mismatch)
  - add “copy error” action (traceId + message)
- Estimate: 0.5d

### Task 10.2 - Projection rebuild/verify runbook surfaced in UI
- Add small “Runbook” link/section on Projections page (markdown block) with steps:
  - when to rebuild, what to check, where logs are, how to validate
- Estimate: 0.5d

### Task 10.3 - Health / projection lag visibility (UI)
- Extend dashboard or projections page with:
  - projection lag/staleness signal (reuse StaleBadge logic)
- Estimate: 1.0d

### Task 10.4 - Import operational safeguards
- On import wizard:
  - show “dry-run recommended” note
  - prevent commit if dry-run has errors
  - display traceId for server-side failures
- Estimate: 1.0d

### Task 10.5 - Operational smoke checklist + scripts
- Add docs/checklist in repo:
  - “seed + import + receive + putaway + pick + adjust” quick verification
- Estimate: 1.0d


## Epic 11 - Testing (coverage for missing areas)

### Task 11.1 - Unit tests: master data typed clients + ProblemDetails/traceId
- Items/Suppliers/Locations/Import clients parse ProblemDetails + traceId
- Estimate: 1.0d

### Task 11.2 - Integration tests: Admin imports (dry-run + commit)
- Items import happy path + duplicate barcode + FK missing
- Estimate: 1.5d

### Task 11.3 - Integration tests: RBAC
- Verify 401/403 for at least 2 endpoints across roles
- Estimate: 1.0d

### Task 11.4 - Integration tests: receiving/QC minimal flow
- Receive -> QC_HOLD -> pass/fail -> stock moves
- Estimate: 1.5d

### Task 11.5 - Integration tests: adjustments + audit
- Adjust stock, verify projection updated and record visible in history query
- Estimate: 1.0d

### Task 11.6 - CI wiring for docker-gated integration scope
- Ensure new integration tests run in the existing docker-gated pattern
- Estimate: 0.5d