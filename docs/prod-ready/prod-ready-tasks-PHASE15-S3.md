# Production-Ready Warehouse Tasks - Phase 1.5 Sprint 3 (Execution Pack)

**Version:** 1.0
**Date:** February 11, 2026
**Sprint:** Phase 1.5 Sprint 3
**Source:** prod-ready-universe.md, prod-ready-tasks-progress.md, codex-suspicions.md
**Status:** Ready for Execution

---

## Sprint Overview

**Sprint Goal:** Complete operational UI gaps and fix critical validation/auth issues to enable end-to-end operator workflows (Receiving → Stock visibility → Movement → Sales Order → Pick/Pack/Dispatch).

**Sprint Duration:** 2 weeks
**Total Tasks:** 20
**Estimated Effort:** 17 days

**Focus Areas:**
1. **UI Completeness:** Receiving/Inbound invoice entry, Sales Order UI, Stock visibility enhancements
2. **Auth/Validation Fix:** Local dev auth flow to eliminate 403 errors
3. **Data Model Corrections:** Align Guid↔int inconsistencies from Sprint 1+2
4. **Operational Hardening:** Validation, error handling, audit trail visibility

**Dependencies:**
- Sprint 1 complete (PRD-1501 to PRD-1510)
- Sprint 2 complete (PRD-1511 to PRD-1520)

---

## Sprint 3 Task Index

| TaskId | Epic | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|------|-------|-----|--------------|-----------|------------|
| PRD-1521 | Foundation | Local Dev Auth & 403 Fix | M | None | Infra/DevOps | codex-suspicions.md:60-62 |
| PRD-1522 | Foundation | Data Model Type Alignment (Guid→int) | M | PRD-1504,1505,1506 | Backend/API | codex-suspicions.md:92-95,136-138 |
| PRD-1523 | Inbound | Receiving Invoice Entry UI | L | None | UI | Universe §3.Workflow 1 |
| PRD-1524 | Inbound | Receiving Scan & QC Workflow UI | L | PRD-1523 | UI | Universe §3.Workflow 1 |
| PRD-1525 | Stock | Stock Visibility Dashboard UI | M | None | UI | Universe §3.Workflow 1 |
| PRD-1526 | Stock | Stock Movement/Transfer UI | M | PRD-1519 | UI | Universe §3.Workflow 3 |
| PRD-1527 | Sales Orders | Create Sales Order UI | L | PRD-1504,1505 | UI | Universe §4.Epic B |
| PRD-1528 | Sales Orders | Sales Order List & Detail UI | M | PRD-1527 | UI | Universe §4.Epic B |
| PRD-1529 | Sales Orders | Allocation & Release UI | M | PRD-1505,1509 | UI | Universe §4.Epic B |
| PRD-1530 | Picking | Picking Workflow UI Enhancements | M | PRD-1505 | UI | Universe §3.Workflow 2 |
| PRD-1531 | Packing | Packing Station UI Enhancements | M | PRD-1507,1510 | UI | Universe §3.Workflow 2 |
| PRD-1532 | Dispatch | Dispatch UI Enhancements | S | PRD-1508,1510 | UI | Universe §3.Workflow 2 |
| PRD-1533 | Reports | Receiving History Report UI | S | None | UI | Universe §1.Reports |
| PRD-1534 | Reports | Dispatch History Report UI | S | PRD-1508 | UI | Universe §1.Reports |
| PRD-1535 | Validation | Stock Allocation Validation | M | PRD-1505 | Backend/API | codex-suspicions.md:108-113 |
| PRD-1536 | Validation | Optimistic Locking for Sales Orders | S | PRD-1505 | Backend/API | codex-suspicions.md:115-120 |
| PRD-1537 | Validation | Barcode Lookup Enhancement | S | PRD-1507 | Backend/API | codex-suspicions.md:146-150 |
| PRD-1538 | Integration | FedEx API Integration (Real) | M | PRD-1508 | Integration | codex-suspicions.md:159-163 |
| PRD-1539 | Observability | End-to-End Correlation Tracing | M | PRD-1503 | Infra/DevOps | Universe §5.Observability |
| PRD-1540 | Testing | Smoke E2E Integration Tests | L | All above | QA | Universe §5.Testing |

**Total Effort:** 17 days (1 developer, 3.5 weeks accounting for overlap)

---

## Task PRD-1521: Local Dev Auth & 403 Fix

**Epic:** Foundation
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** Infra/DevOps
**Dependencies:** None
**SourceRefs:** codex-suspicions.md:60-62, Universe §5.Security

### Context

- Current local API returns HTTP 403 for all endpoints when accessed without auth token
- No documented dev-only auth flow or seeded admin credentials
- Validation steps in Sprint 1+2 tasks cannot be executed locally
- Production security must remain intact (this is dev-only convenience)
- Need clear separation: dev mode (permissive) vs production (strict RBAC)

### Scope

**In Scope:**
- Dev-only authentication bypass middleware (controlled by `IsDevelopment()`)
- Seeded admin user credentials in dev environment (username/password in appsettings.Development.json)
- JWT token generation endpoint `/api/auth/dev-token` (dev only)
- Documentation: how to obtain token and use in curl/Postman
- Health check endpoint remains anonymous (no auth required)

**Out of Scope:**
- Production authentication changes (SSO/OAuth deferred to Phase 2)
- User management UI (deferred)
- Multi-tenant auth (single warehouse for Phase 1.5)

### Requirements

**Functional:**
1. In Development environment, add `/api/auth/dev-token` endpoint
2. Endpoint accepts `{ username, password }` and returns JWT token
3. Seeded credentials: `admin / Admin123!` (hardcoded in appsettings.Development.json)
4. JWT token includes claims: `sub=admin-dev`, `role=Admin,Manager,Operator,QCInspector` (all roles)
5. Token expiry: 24 hours
6. Middleware validates JWT token on all API routes (except /health, /api/auth/*)
7. Production mode: endpoint disabled, returns 404

**Non-Functional:**
1. Security: Dev token ONLY active when `ASPNETCORE_ENVIRONMENT=Development`
2. Logging: WARN log on startup: "Dev auth enabled - DO NOT USE IN PRODUCTION"
3. Documentation: Add `docs/dev-auth-guide.md` with curl examples
4. No database dependency for dev auth (in-memory validation)

**Data Model:**
```csharp
// Dev-only DTO
public record DevTokenRequest(string Username, string Password);
public record DevTokenResponse(string Token, DateTime ExpiresAt);
```

**API:**
```http
POST /api/auth/dev-token
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin123!"
}

Response 200:
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-02-12T10:00:00Z"
}
```

**Implementation Pattern:**
```csharp
// Program.cs
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IDevAuthService, DevAuthService>();
    app.MapPost("/api/auth/dev-token", async (DevTokenRequest req, IDevAuthService auth) => {
        var token = auth.GenerateToken(req.Username, req.Password);
        return token is not null ? Results.Ok(token) : Results.Unauthorized();
    });
    app.Logger.LogWarning("⚠️  DEV AUTH ENABLED - DO NOT USE IN PRODUCTION");
}

// Middleware: JWT validation on all routes
app.UseAuthentication();
app.UseAuthorization();
```

### Acceptance Criteria

```gherkin
Scenario: Dev token generation in development mode
  Given ASPNETCORE_ENVIRONMENT=Development
  When POST /api/auth/dev-token with {"username":"admin","password":"Admin123!"}
  Then response status 200
  And response includes valid JWT token
  And token claims include role=Admin,Manager,Operator,QCInspector
  And token expiry = now + 24 hours

Scenario: Dev token rejected in production mode
  Given ASPNETCORE_ENVIRONMENT=Production
  When POST /api/auth/dev-token with {"username":"admin","password":"Admin123!"}
  Then response status 404
  And endpoint not registered

Scenario: Use dev token for protected endpoint
  Given dev token obtained via /api/auth/dev-token
  When GET /api/warehouse/v1/items with Authorization: Bearer {token}
  Then response status 200
  And items list returned

Scenario: Invalid credentials rejected
  Given ASPNETCORE_ENVIRONMENT=Development
  When POST /api/auth/dev-token with {"username":"admin","password":"WrongPassword"}
  Then response status 401
  And error message: "Invalid credentials"

Scenario: Health endpoint remains anonymous
  Given no auth token
  When GET /health
  Then response status 200
  And no 401/403 error
```

### Validation / Checks

**Local Testing:**
```bash
# Start API in development mode
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/LKvitai.MES.Api

# Get dev token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

echo "Token: $TOKEN"

# Test protected endpoint
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/warehouse/v1/items

# Verify startup log
# Expected: "⚠️  DEV AUTH ENABLED - DO NOT USE IN PRODUCTION"

# Test production mode
export ASPNETCORE_ENVIRONMENT=Production
dotnet run --project src/LKvitai.MES.Api
curl -X POST http://localhost:5000/api/auth/dev-token  # Should return 404
```

**Metrics:**
- N/A (dev-only feature, no production metrics)

**Logs:**
- WARN on startup: "⚠️  DEV AUTH ENABLED - DO NOT USE IN PRODUCTION"
- INFO: "Dev token generated for user {Username}, expires {ExpiresAt}"
- WARN: "Dev token rejected: invalid credentials for user {Username}"

### Definition of Done

- [ ] `IDevAuthService` interface + implementation created
- [ ] `/api/auth/dev-token` endpoint registered (dev mode only)
- [ ] JWT generation logic implemented (System.IdentityModel.Tokens.Jwt)
- [ ] Seeded credentials in appsettings.Development.json
- [ ] JWT middleware configured in Program.cs
- [ ] Health endpoint exempted from auth
- [ ] Production mode check: endpoint returns 404
- [ ] Startup warning log added
- [ ] `docs/dev-auth-guide.md` created with examples
- [ ] Unit tests: token generation, claim validation, expiry
- [ ] Integration test: full auth flow (get token → call protected endpoint)
- [ ] Manual test: curl commands from docs work
- [ ] Code review completed

---

## Task PRD-1522: Data Model Type Alignment (Guid→int)

**Epic:** Foundation
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1504 (SalesOrder), PRD-1505 (SalesOrder APIs), PRD-1506 (OutboundOrder)
**SourceRefs:** codex-suspicions.md:92-95,136-138,198-202,232-236

### Context

- Sprint 1+2 task specs used `Guid` for ItemId, LocationId, CategoryId
- Repository master data uses `int` primary keys for Item, Location, Category
- Mismatch causes FK violations and query failures
- Need systematic alignment: keep `int` in database, document conversion boundaries
- Affects: SalesOrder, OutboundOrder, Shipment, Valuation, OnHandValue, Transfers, CycleCounts

### Scope

**In Scope:**
- Update SalesOrderLine: `ItemId` from Guid to int
- Update OutboundOrderLine, ShipmentLine: `ItemId` from Guid to int
- Update Transfer: `ItemId`, `FromLocationId`, `ToLocationId` from Guid to int
- Update CycleCountLine: `ItemId`, `LocationId` from Guid to int
- Update OnHandValue projection: `ItemId`, `CategoryId`, `LocationId` from Guid to int
- Valuation stream: keep Guid ItemId in events, add int-to-Guid mapping service
- Database migrations: change column types
- API contracts: align DTOs to `int`
- Documentation: ADR explaining int (relational) vs Guid (event streams)

**Out of Scope:**
- Changing StockLedger stream ID format (remains `stockledger-item-{int}`)
- Migrating existing event data (streams already use int-based IDs)
- Changing HandlingUnit ID (already int)

### Requirements

**Functional:**
1. All FK references to Item/Location/Category tables MUST use `int`
2. API DTOs MUST accept/return `int` for ItemId, LocationId, CategoryId
3. Valuation events: map `int itemId` → `Guid streamId` via deterministic function: `streamId = new Guid(itemId, 0, 0, ...)`
4. Projection queries: use `int` for joins with master data tables
5. Migrations: `ALTER TABLE` statements to change column types (with data preservation check)

**Non-Functional:**
1. Zero data loss during migration (validate FK constraints before apply)
2. Backwards compatibility: unapplied migrations fail with clear error message
3. Query performance: indexes on int columns faster than Guid
4. Documentation: `docs/adr/003-int-vs-guid-keys.md`

**Data Model Changes:**
```sql
-- Example migration
ALTER TABLE sales_order_lines ALTER COLUMN item_id TYPE INT USING item_id::INT;
ALTER TABLE outbound_order_lines ALTER COLUMN item_id TYPE INT;
ALTER TABLE shipment_lines ALTER COLUMN item_id TYPE INT;
ALTER TABLE transfer_lines ALTER COLUMN item_id TYPE INT;
ALTER TABLE transfer_lines ALTER COLUMN from_location_id TYPE INT;
ALTER TABLE transfer_lines ALTER COLUMN to_location_id TYPE INT;
ALTER TABLE cycle_count_lines ALTER COLUMN item_id TYPE INT;
ALTER TABLE cycle_count_lines ALTER COLUMN location_id TYPE INT;
ALTER TABLE on_hand_value ALTER COLUMN item_id TYPE INT;
ALTER TABLE on_hand_value ALTER COLUMN category_id TYPE INT;
ALTER TABLE on_hand_value ALTER COLUMN location_id TYPE INT;
```

**API Changes:**
```csharp
// Before (incorrect)
public class SalesOrderLineDto {
    public Guid ItemId { get; set; }
}

// After (correct)
public class SalesOrderLineDto {
    public int ItemId { get; set; }
}
```

**Valuation Stream ID Mapping:**
```csharp
public class ValuationStreamIdMapper {
    public static string GetStreamId(int itemId) {
        return $"valuation-item-{itemId}";  // NOT Guid, use int directly
    }
}
```

### Acceptance Criteria

```gherkin
Scenario: Sales order line references item by int
  Given Item with Id=1 (int)
  When creating SalesOrderLine with ItemId=1
  Then foreign key constraint satisfied
  And query joins work: SELECT * FROM sales_order_lines JOIN items ON item_id = items.id

Scenario: Valuation stream mapping
  Given Item with Id=42
  When initializing valuation
  Then stream created with name "valuation-item-42"
  And event payload includes ItemId=42 (int)
  And stream queryable by int item ID

Scenario: OnHandValue projection query
  Given on_hand_value row with item_id=5, category_id=2, location_id=10
  When querying GET /api/warehouse/v1/valuation/on-hand-value?itemId=5
  Then projection query uses int FK
  And joins with items/categories/locations tables succeed
  And response includes item details

Scenario: Migration preserves existing data
  Given sales_order_lines with item_id values [1,2,3]
  When migration applied
  Then item_id column type changed to INT
  And all rows retained with same values
  And FK constraints re-applied successfully
```

### Validation / Checks

**Local Testing:**
```bash
# Generate migration
dotnet ef migrations add AlignGuidsToInts --project src/LKvitai.MES.Infrastructure --startup-project src/LKvitai.MES.Api

# Review migration SQL
dotnet ef migrations script --project src/LKvitai.MES.Infrastructure --startup-project src/LKvitai.MES.Api

# Apply migration
dotnet ef database update --project src/LKvitai.MES.Infrastructure --startup-project src/LKvitai.MES.Api

# Verify schema
psql -d warehouse -c "\d sales_order_lines" | grep item_id  # Should show integer

# Test FK integrity
psql -d warehouse -c "
  INSERT INTO sales_order_lines (sales_order_id, item_id, qty) VALUES (1, 999);
" # Should fail with FK violation if item 999 doesn't exist

# Test API
curl -H "Authorization: Bearer $TOKEN" -X POST http://localhost:5000/api/warehouse/v1/sales-orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":1,"lines":[{"itemId":1,"qty":10}]}'
```

**Metrics:**
- N/A (schema change, no runtime metrics)

**Logs:**
- INFO: "Migration AlignGuidsToInts applied successfully"
- WARN: "FK constraint check: {RowCount} rows verified before migration"

### Definition of Done

- [ ] Migration script created (AlignGuidsToInts)
- [ ] All affected tables updated: sales_order_lines, outbound_order_lines, shipment_lines, transfer_lines, cycle_count_lines, on_hand_value
- [ ] All DTOs updated to use `int` for ItemId/LocationId/CategoryId
- [ ] Valuation stream ID mapper uses int directly
- [ ] FK constraints re-applied after column type change
- [ ] Migration tested on dev database (no data loss)
- [ ] API integration tests pass with int types
- [ ] Documentation: `docs/adr/003-int-vs-guid-keys.md`
- [ ] Code review completed

---

## Task PRD-1523: Receiving Invoice Entry UI

**Epic:** Inbound
**Phase:** 1.5
**Sprint:** 3
**Estimate:** L (2 days)
**OwnerType:** UI
**Dependencies:** None
**SourceRefs:** Universe §3.Workflow 1 (Inbound), Universe §1.Operational UI

### Context

- Phase 1 has basic receiving workflow (API exists)
- No UI for creating inbound shipments (invoice-like receiving document)
- Operators currently use Postman or manual SQL inserts (not production-ready)
- Need invoice-style entry form: supplier, expected items, quantities, lot info
- Must support both manual entry and Excel import

### Scope

**In Scope:**
- Inbound Shipments List page (`/warehouse/inbound/shipments`)
- Create Inbound Shipment form (`/warehouse/inbound/shipments/create`)
- Import from Excel button (reuse existing import wizard)
- Supplier dropdown with search
- Item selection with barcode lookup
- Expected delivery date picker
- Status badges (EXPECTED, PARTIAL, COMPLETED)

**Out of Scope:**
- QR code generation for shipment (deferred)
- EDI/API integration for auto-creating shipments from PO (deferred)
- Multi-warehouse receiving (single warehouse for Phase 1.5)

### Requirements

**Functional:**
1. List page shows all inbound shipments (filters: status, supplier, date range)
2. Create form fields: Supplier (dropdown), Expected Delivery Date (date picker), Lines (dynamic table)
3. Line fields: Item (dropdown with search), Expected Qty, Expected Lot (optional), Expected Expiry (optional if lot-tracked)
4. Add/Remove line buttons
5. Validation: Supplier required, at least 1 line, Item required, Qty > 0
6. Submit button: POST `/api/warehouse/v1/inbound-shipments`
7. Success: redirect to shipment detail, toast "Shipment ISH-0001 created"
8. Import button: open Excel import wizard (existing), template includes: Supplier, Item, Qty, Lot, Expiry

**Non-Functional:**
1. Responsive: works on tablet (warehouse receiving uses tablets)
2. Barcode scanner support: focus trap on Item input, auto-submit on Enter
3. Performance: Item dropdown lazy-loads (autocomplete after 2 chars)
4. Accessibility: keyboard navigation, screen reader labels

**UI Wireframe:**
```
┌─────────────────────────────────────────────────────────┐
│ Inbound Shipments                        [+ Create] [Import] │
├─────────────────────────────────────────────────────────┤
│ Filters: [Status ▼] [Supplier ▼] [Date Range]          │
├─────────────────────────────────────────────────────────┤
│ Shipment# │ Supplier │ Status │ Expected Date │ Actions │
│ ISH-0001 │ ACME     │ EXPECTED │ 2026-02-15   │ [View]  │
│ ISH-0002 │ GlobalCo │ PARTIAL  │ 2026-02-10   │ [View]  │
└─────────────────────────────────────────────────────────┘

Create Inbound Shipment:
┌─────────────────────────────────────────────────────────┐
│ Supplier: [ACME Corp ▼]                                 │
│ Expected Delivery Date: [📅 2026-02-15]                 │
│                                                         │
│ Items:                                                  │
│ ┌──────────────────────────────────────────────────┐  │
│ │ Item │ Expected Qty │ Lot │ Expiry │ [Remove]    │  │
│ │ RM-0001 │ 100      │ LOT-A │ 2027-01-01 │ [X]   │  │
│ │ RM-0002 │ 50       │       │            │ [X]   │  │
│ │ [+ Add Item]                                      │  │
│ └──────────────────────────────────────────────────┘  │
│                                                         │
│ [Cancel] [Create Shipment]                             │
└─────────────────────────────────────────────────────────┘
```

**API Contract:**
```csharp
// POST /api/warehouse/v1/inbound-shipments
public record CreateInboundShipmentRequest(
    Guid CommandId,
    int SupplierId,
    DateTime ExpectedDeliveryDate,
    List<InboundShipmentLineDto> Lines
);

public record InboundShipmentLineDto(
    int ItemId,
    decimal ExpectedQty,
    string? ExpectedLot,
    DateTime? ExpectedExpiry
);
```

### Acceptance Criteria

```gherkin
Scenario: Create inbound shipment via UI
  Given logged in as Receiving Clerk
  When navigate to /warehouse/inbound/shipments
  And click "Create"
  And select Supplier "ACME Corp"
  And set Expected Delivery Date "2026-02-15"
  And add line: Item "RM-0001", Qty 100, Lot "BATCH-A", Expiry "2027-01-01"
  And add line: Item "RM-0002", Qty 50
  And click "Create Shipment"
  Then API POST /api/warehouse/v1/inbound-shipments called
  And redirect to /warehouse/inbound/shipments/{id}
  And toast message "Shipment ISH-0001 created"

Scenario: Validation errors shown
  Given on Create Inbound Shipment page
  When click "Create Shipment" without selecting Supplier
  Then error message "Supplier is required"
  And form not submitted

Scenario: Barcode scanner input
  Given on Create Inbound Shipment page
  And Item input field focused
  When scan barcode "SKU-RM-0001"
  Then Item dropdown auto-selects "RM-0001"
  And cursor moves to Qty field
  And operator can continue scanning

Scenario: Import from Excel
  Given on Inbound Shipments list page
  When click "Import"
  Then Excel import wizard opens
  And template includes columns: Supplier, Item, ExpectedQty, Lot, Expiry
  When upload valid file
  Then shipments created (one per supplier group)
  And toast "3 shipments created from import"
```

### Validation / Checks

**Local Testing:**
```bash
# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to http://localhost:5001/warehouse/inbound/shipments
# Click "Create"
# Fill form and submit
# Verify redirect and toast

# Test barcode input (use keyboard wedge simulator or manual entry)
# Expected: auto-complete after Enter key
```

**Metrics:**
- `ui_inbound_shipment_created_total` (counter)
- `ui_page_load_duration_ms` (histogram, route: /warehouse/inbound/shipments)

**Logs:**
- INFO: "User {UserId} created inbound shipment {ShipmentId}"

### Definition of Done

- [ ] Blazor page created: `Pages/InboundShipments.razor`
- [ ] Blazor page created: `Pages/InboundShipmentCreate.razor`
- [ ] Service client: `InboundClient.CreateShipmentAsync()`
- [ ] DTO models aligned with API contract
- [ ] Validation logic implemented (client-side + server-side)
- [ ] Barcode scanner support (focus trap, auto-submit)
- [ ] Import button wired to existing Excel import wizard
- [ ] Status badges styled (EXPECTED=blue, PARTIAL=yellow, COMPLETED=green)
- [ ] Responsive design tested on tablet viewport
- [ ] Manual test: create shipment, verify in database
- [ ] Code review completed

---

## Task PRD-1524: Receiving Scan & QC Workflow UI

**Epic:** Inbound
**Phase:** 1.5
**Sprint:** 3
**Estimate:** L (2 days)
**OwnerType:** UI
**Dependencies:** PRD-1523
**SourceRefs:** Universe §3.Workflow 1 (Receive Goods, QC Inspection)

### Context

- Inbound shipment created via PRD-1523, now need receiving execution UI
- Operators scan barcodes to confirm receipt, assign lot/expiry
- Items requiring QC routed to QC_HOLD location automatically
- QC inspector approves/rejects via separate UI panel

### Scope

**In Scope:**
- Shipment Detail page with "Receive Items" panel
- Barcode scan input for items
- Actual qty confirmation (vs expected qty)
- Lot and expiry date entry (if required)
- QC Panel page (`/warehouse/qc/pending`)
- QC decision: Pass (move to RECEIVING) / Fail (move to QUARANTINE/SCRAP)

**Out of Scope:**
- Photo attachments for QC (deferred to Phase 2)
- Multi-level QC approvals (single-level for Phase 1.5)
- Barcode label printing during receiving (use PRD-1516 separately)

### Requirements

**Functional:**
1. Shipment Detail page includes "Receive Items" section
2. Scan Item Barcode input (auto-lookup item)
3. Confirm Actual Qty (default = Expected Qty, editable)
4. Lot input (required if Item.RequiresLotTracking)
5. Expiry input (required if Item.RequiresLotTracking)
6. Submit per line: POST `/api/warehouse/v1/inbound-shipments/{id}/receive-items`
7. Success: line marked RECEIVED, qty updated, toast "Item RM-0001 received"
8. QC Panel: shows all items at QC_HOLD location
9. QC decision buttons: Pass (green), Fail (red)
10. Fail reason dropdown (DAMAGED, EXPIRED, WRONG_ITEM, OTHER)

**Non-Functional:**
1. Barcode scanner support: continuous scanning (receive → next item)
2. Keyboard shortcuts: Enter = submit line, Esc = clear form
3. Error handling: duplicate scans, qty mismatch warnings
4. Audit: log every scan action (operator, timestamp, item, qty)

**UI Wireframe:**
```
Shipment ISH-0001 Detail:
┌─────────────────────────────────────────────────────────┐
│ Supplier: ACME Corp                                     │
│ Status: PARTIAL                                         │
│ Expected Date: 2026-02-15                               │
├─────────────────────────────────────────────────────────┤
│ Expected Items:                                         │
│ Item      │ Expected │ Received │ Status               │
│ RM-0001  │ 100      │ 80       │ PARTIAL              │
│ RM-0002  │ 50       │ 0        │ PENDING              │
├─────────────────────────────────────────────────────────┤
│ Receive Items:                                          │
│ [Scan Barcode] ________________                         │
│ Item: RM-0001 (auto-filled from scan)                   │
│ Expected Qty: 100 | Actual Qty: [100___]               │
│ Lot: [BATCH-B____] (required)                           │
│ Expiry: [📅 2027-06-01] (required)                      │
│ [Receive Item]                                          │
└─────────────────────────────────────────────────────────┘

QC Panel (/warehouse/qc/pending):
┌─────────────────────────────────────────────────────────┐
│ Item      │ Lot      │ Qty │ Location │ Actions        │
│ RM-0001  │ BATCH-A │ 50  │ QC_HOLD  │ [✓ Pass] [✗ Fail] │
│ FG-0002  │ LOT-XYZ │ 20  │ QC_HOLD  │ [✓ Pass] [✗ Fail] │
└─────────────────────────────────────────────────────────┘
```

**API Contracts:**
```csharp
// POST /api/warehouse/v1/inbound-shipments/{id}/receive-items
public record ReceiveItemsRequest(
    Guid CommandId,
    int ItemId,
    decimal ActualQty,
    string? Lot,
    DateTime? Expiry
);

// POST /api/warehouse/v1/qc/inspect
public record QCInspectRequest(
    Guid CommandId,
    Guid HandlingUnitId,
    QCDecision Decision,  // PASS | FAIL
    string? FailReason
);
```

### Acceptance Criteria

```gherkin
Scenario: Receive item via barcode scan
  Given Inbound Shipment ISH-0001 with expected item RM-0001 qty 100
  When operator navigates to shipment detail
  And scans barcode "RM-0001"
  Then Item field auto-fills "RM-0001"
  And Expected Qty shows 100
  And Actual Qty defaults to 100
  When operator enters Lot "BATCH-B", Expiry "2027-06-01"
  And clicks "Receive Item"
  Then API POST /api/warehouse/v1/inbound-shipments/{id}/receive-items called
  And toast "Item RM-0001 received: 100 units"
  And shipment line status updated to RECEIVED

Scenario: QC auto-routing
  Given Item RM-0001 has RequiresQC=true
  When receiving item RM-0001
  Then StockMoved event routes to QC_HOLD location
  And item appears in QC Panel (/warehouse/qc/pending)

Scenario: QC pass decision
  Given HandlingUnit HU-0001 at QC_HOLD (Item RM-0001, Qty 50)
  When QC inspector clicks "Pass"
  Then API POST /api/warehouse/v1/qc/inspect called with Decision=PASS
  And StockMoved event routes to RECEIVING
  And HU removed from QC Panel
  And toast "QC passed: HU-0001"

Scenario: QC fail decision
  Given HandlingUnit HU-0002 at QC_HOLD (Item FG-0002, Qty 20)
  When QC inspector clicks "Fail"
  Then modal opens: "Reason for failure?"
  When inspector selects "DAMAGED" and confirms
  Then API POST /api/warehouse/v1/qc/inspect called with Decision=FAIL, FailReason=DAMAGED
  And StockMoved event routes to QUARANTINE
  And toast "QC failed: HU-0002 moved to QUARANTINE"
```

### Validation / Checks

**Local Testing:**
```bash
# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/inbound/shipments/{id}
# Test barcode scan (keyboard wedge or manual entry)
# Submit receiving
# Verify QC panel shows item if RequiresQC=true

# QC Panel test
# Navigate to /warehouse/qc/pending
# Click Pass/Fail buttons
# Verify stock movements in database
```

**Metrics:**
- `ui_items_received_total` (counter)
- `ui_qc_decisions_total` (counter, labels: decision=PASS|FAIL)

**Logs:**
- INFO: "Item {ItemId} received by {OperatorId}, Qty {ActualQty}, Lot {Lot}"
- INFO: "QC decision {Decision} for HU {HandlingUnitId} by {InspectorId}"

### Definition of Done

- [ ] Shipment Detail page updated with "Receive Items" panel
- [ ] Barcode scan input implemented
- [ ] Lot/Expiry validation (required if Item.RequiresLotTracking)
- [ ] QC Panel page created: `Pages/QCPanel.razor`
- [ ] QC Pass/Fail buttons wired to API
- [ ] Fail reason modal implemented
- [ ] Toast notifications for success/error
- [ ] Keyboard shortcuts (Enter, Esc) implemented
- [ ] Responsive design tested
- [ ] Manual test: full receiving → QC flow
- [ ] Code review completed

---

## Task PRD-1525: Stock Visibility Dashboard UI

**Epic:** Stock
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** None
**SourceRefs:** Universe §1.Operational UI (Stock Level Report), Universe §3.Workflow 1

### Context

- Phase 1 has AvailableStock projection and basic stock list page
- Need enhanced dashboard: on-hand qty by location, low stock alerts, expiry warnings
- Operators need at-a-glance view of stock status
- Must integrate with existing AvailableStock.razor page

### Scope

**In Scope:**
- Stock Dashboard page (`/warehouse/stock/dashboard`)
- Summary cards: Total Items, Total Qty, Low Stock Count, Expiring Soon Count
- Stock by Location table (grouping by location, sortable)
- Low Stock Alerts section (configurable threshold)
- Expiring Soon section (within 30 days)
- CSV export button

**Out of Scope:**
- Real-time updates (SignalR deferred to Phase 2)
- Stock movement history graph (deferred)
- Multi-warehouse dashboard (single warehouse for Phase 1.5)

### Requirements

**Functional:**
1. Dashboard loads on `/warehouse/stock/dashboard`
2. Summary cards: 4 cards (Total Items, Total Qty, Low Stock, Expiring Soon)
3. Stock by Location table: columns (Location, Item Count, Total Qty, Utilization %)
4. Low Stock Alerts: show items where OnHandQty < MinStockLevel (configurable per item)
5. Expiring Soon: show lots with Expiry <= Today + 30 days
6. CSV export: downloads current stock view

**Non-Functional:**
1. Performance: dashboard loads < 2 seconds (aggregate queries)
2. Refresh button (manual refresh, no auto-refresh)
3. Filters: Location dropdown, Category dropdown, Search by Item

**UI Wireframe:**
```
Stock Dashboard:
┌─────────────────────────────────────────────────────────┐
│ [Total Items]  [Total Qty]  [Low Stock]  [Expiring Soon] │
│     1,234         45,678         23           12         │
├─────────────────────────────────────────────────────────┤
│ Stock by Location                      [Export CSV] [🔄] │
│ Location │ Items │ Total Qty │ Utilization              │
│ A1-B1    │ 50    │ 1,200     │ 75% ████████             │
│ A2-B3    │ 30    │ 800       │ 60% ██████               │
├─────────────────────────────────────────────────────────┤
│ Low Stock Alerts                                        │
│ Item     │ Location │ On-Hand │ Min │ Status            │
│ RM-0001 │ A1-B1    │ 10      │ 50  │ 🔴 CRITICAL        │
│ FG-0002 │ A2-B3    │ 30      │ 40  │ 🟡 LOW             │
├─────────────────────────────────────────────────────────┤
│ Expiring Soon (30 days)                                 │
│ Item     │ Lot      │ Expiry      │ Qty │ Location      │
│ RM-0001 │ BATCH-A │ 2026-03-01 │ 50  │ A1-B1          │
│ FG-0003 │ LOT-XYZ │ 2026-02-20 │ 20  │ A2-B3          │
└─────────────────────────────────────────────────────────┘
```

**API Contracts:**
```http
GET /api/warehouse/v1/stock/dashboard-summary
Response:
{
  "totalItems": 1234,
  "totalQty": 45678,
  "lowStockCount": 23,
  "expiringSoonCount": 12
}

GET /api/warehouse/v1/stock/by-location
Response: [
  { "locationCode": "A1-B1", "itemCount": 50, "totalQty": 1200, "utilization": 0.75 },
  ...
]

GET /api/warehouse/v1/stock/low-stock
Response: [
  { "itemId": 1, "itemCode": "RM-0001", "onHandQty": 10, "minStockLevel": 50, ... },
  ...
]

GET /api/warehouse/v1/stock/expiring-soon?days=30
Response: [
  { "itemId": 1, "lotNumber": "BATCH-A", "expiry": "2026-03-01", "qty": 50, ... },
  ...
]
```

### Acceptance Criteria

```gherkin
Scenario: Load stock dashboard
  Given logged in as Warehouse Operator
  When navigate to /warehouse/stock/dashboard
  Then summary cards display: Total Items, Total Qty, Low Stock Count, Expiring Soon Count
  And Stock by Location table shows all locations with stock
  And table sorted by utilization (highest first)

Scenario: Low stock alerts shown
  Given Item RM-0001 with OnHandQty=10, MinStockLevel=50
  When dashboard loads
  Then Low Stock Alerts section shows RM-0001 with status "CRITICAL"
  And item highlighted in red

Scenario: Expiring soon items
  Given Lot BATCH-A with Expiry=2026-03-01 (25 days from now)
  When dashboard loads
  Then Expiring Soon section shows BATCH-A
  And days remaining calculated: "25 days"

Scenario: CSV export
  Given on Stock Dashboard
  When click "Export CSV"
  Then browser downloads stock-dashboard-{timestamp}.csv
  And CSV includes: Item, Location, OnHandQty, ReservedQty, AvailableQty
```

### Validation / Checks

**Local Testing:**
```bash
# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/stock/dashboard
# Verify summary cards load
# Check Low Stock section (need test data with MinStockLevel set)
# Check Expiring Soon section (need lots with Expiry dates)
# Click Export CSV and verify download
```

**Metrics:**
- `ui_stock_dashboard_loads_total` (counter)
- `ui_stock_csv_exports_total` (counter)

**Logs:**
- INFO: "Stock dashboard loaded by {UserId}"

### Definition of Done

- [ ] Dashboard page created: `Pages/StockDashboard.razor`
- [ ] Summary cards component created
- [ ] Stock by Location table implemented
- [ ] Low Stock Alerts section implemented
- [ ] Expiring Soon section implemented
- [ ] CSV export functionality (reuse existing export logic)
- [ ] Filters implemented (Location, Category, Search)
- [ ] Refresh button implemented
- [ ] Responsive design tested
- [ ] Manual test: verify all sections load with real data
- [ ] Code review completed

---

## Task PRD-1526: Stock Movement/Transfer UI

**Epic:** Stock
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** PRD-1519 (Transfer Workflow API)
**SourceRefs:** Universe §3.Workflow 3, PRD-1519

### Context

- PRD-1519 implemented transfer workflow API (backend)
- Need UI for operators to create, approve, execute transfers
- Use case: move stock between storage locations, reassign logical warehouses
- Must support barcode scanning for HUs

### Scope

**In Scope:**
- Transfer List page (`/warehouse/transfers`)
- Create Transfer form (`/warehouse/transfers/create`)
- Transfer Detail page with Execute panel
- Approval workflow (Manager approves before execution)
- Barcode scan for HU selection

**Out of Scope:**
- Bulk transfer import (Excel, deferred)
- Transfer cancellation workflow (approve/reject only)
- Transfer history graph (deferred)

### Requirements

**Functional:**
1. List page filters: Status (DRAFT, PENDING_APPROVAL, APPROVED, IN_PROGRESS, COMPLETED), Date Range
2. Create form: From Location, To Location, Transfer Type (PHYSICAL, LOGICAL_WAREHOUSE), Lines (Item, Qty, HU)
3. Submit: POST `/api/warehouse/v1/transfers`
4. Approval: Manager clicks "Approve" → POST `/api/warehouse/v1/transfers/{id}/approve`
5. Execute: Operator scans HU barcode, confirms qty → POST `/api/warehouse/v1/transfers/{id}/execute`
6. Status badges: color-coded (DRAFT=gray, APPROVED=green, COMPLETED=blue)

**Non-Functional:**
1. Barcode scanner support during execution
2. Validation: From/To locations must differ, Qty > 0
3. Audit trail visible in detail page (who created, approved, executed)

**UI Wireframe:**
```
Transfers List:
┌─────────────────────────────────────────────────────────┐
│ Transfers                                [+ Create]     │
│ Filters: [Status ▼] [Date Range]                       │
├─────────────────────────────────────────────────────────┤
│ ID  │ From → To │ Status │ Created By │ Actions        │
│ T001 │ A1→A2    │ APPROVED │ Operator1 │ [Execute]     │
│ T002 │ A2→A3    │ PENDING  │ Operator2 │ [Approve]     │
└─────────────────────────────────────────────────────────┘

Create Transfer:
┌─────────────────────────────────────────────────────────┐
│ From Location: [A1-B1 ▼]                               │
│ To Location: [A2-B3 ▼]                                 │
│ Transfer Type: [⚪ PHYSICAL ⚪ LOGICAL_WAREHOUSE]       │
│ Items:                                                  │
│ ┌──────────────────────────────────────────────────┐  │
│ │ Item │ Qty │ HU (optional) │ [Remove]            │  │
│ │ RM-0001 │ 50 │ HU-0001      │ [X]                │  │
│ │ [+ Add Item]                                      │  │
│ └──────────────────────────────────────────────────┘  │
│ Reason: [Stock rebalancing_________________]           │
│ [Cancel] [Create Transfer]                             │
└─────────────────────────────────────────────────────────┘

Transfer Detail (Execute):
┌─────────────────────────────────────────────────────────┐
│ Transfer T001: A1-B1 → A2-B3                           │
│ Status: APPROVED                                        │
│ Created By: Operator1 | Approved By: Manager1          │
├─────────────────────────────────────────────────────────┤
│ Items:                                                  │
│ Item     │ Qty │ HU       │ Status                     │
│ RM-0001 │ 50  │ HU-0001 │ PENDING                     │
├─────────────────────────────────────────────────────────┤
│ Execute Transfer:                                       │
│ [Scan HU Barcode] ________________                      │
│ HU: HU-0001 (auto-filled)                              │
│ Qty to transfer: [50___]                               │
│ [Confirm Transfer]                                      │
└─────────────────────────────────────────────────────────┘
```

**API Contracts (from PRD-1519):**
```csharp
POST /api/warehouse/v1/transfers
POST /api/warehouse/v1/transfers/{id}/approve
POST /api/warehouse/v1/transfers/{id}/execute
```

### Acceptance Criteria

```gherkin
Scenario: Create transfer request
  Given logged in as Warehouse Operator
  When navigate to /warehouse/transfers
  And click "Create"
  And select From Location "A1-B1", To Location "A2-B3"
  And add line: Item "RM-0001", Qty 50, HU "HU-0001"
  And enter Reason "Stock rebalancing"
  And click "Create Transfer"
  Then API POST /api/warehouse/v1/transfers called
  And redirect to transfer detail
  And toast "Transfer T001 created, pending approval"

Scenario: Manager approves transfer
  Given Transfer T001 with status PENDING_APPROVAL
  When Manager navigates to transfer detail
  And clicks "Approve"
  Then API POST /api/warehouse/v1/transfers/{id}/approve called
  And status updated to APPROVED
  And toast "Transfer T001 approved"

Scenario: Operator executes transfer via barcode
  Given Transfer T001 with status APPROVED
  When Operator navigates to transfer detail
  And scans HU barcode "HU-0001"
  Then HU field auto-fills
  And Qty defaults to transfer line qty
  When click "Confirm Transfer"
  Then API POST /api/warehouse/v1/transfers/{id}/execute called
  And StockMoved event emitted (A1-B1 → A2-B3)
  And toast "Transfer executed: HU-0001 moved to A2-B3"
```

### Validation / Checks

**Local Testing:**
```bash
# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/transfers
# Create transfer, verify pending approval
# Login as Manager, approve transfer
# Login as Operator, execute transfer via barcode scan
# Verify stock locations updated in database
```

**Metrics:**
- `ui_transfers_created_total` (counter)
- `ui_transfers_executed_total` (counter)

**Logs:**
- INFO: "Transfer {TransferId} created by {OperatorId}"
- INFO: "Transfer {TransferId} approved by {ManagerId}"
- INFO: "Transfer {TransferId} executed by {OperatorId}"

### Definition of Done

- [ ] Transfers List page created: `Pages/Transfers.razor`
- [ ] Create Transfer page created: `Pages/TransferCreate.razor`
- [ ] Transfer Detail page created: `Pages/TransferDetail.razor`
- [ ] Approval button wired to API
- [ ] Execute panel with barcode scan implemented
- [ ] Status badges styled
- [ ] Validation logic (From ≠ To, Qty > 0)
- [ ] Toast notifications implemented
- [ ] Manual test: full create → approve → execute flow
- [ ] Code review completed

---

## Task PRD-1527: Create Sales Order UI

**Epic:** Sales Orders
**Phase:** 1.5
**Sprint:** 3
**Estimate:** L (2 days)
**OwnerType:** UI
**Dependencies:** PRD-1504 (SalesOrder Entity), PRD-1505 (SalesOrder APIs)
**SourceRefs:** Universe §4.Epic B, Universe §3.Workflow 2

### Context

- Phase 1 completed SalesOrder backend (PRD-1504, PRD-1505) but no UI for creating orders
- Operators currently use Postman or manual SQL inserts (not production-ready)
- Need full sales order entry form: Customer selection, Order lines (Item, Qty, Price), Shipping address
- Must support both B2B (pallet/case quantities) and B2C (piece picking)
- Auto-allocation on submit (if stock available) or manual approval workflow (if over credit limit)

### Scope

**In Scope:**
- Sales Orders List page (`/warehouse/sales/orders`)
- Create Sales Order form (`/warehouse/sales/orders/create`)
- Customer dropdown with search
- Dynamic order lines table (add/remove lines)
- Shipping address auto-fill from customer (editable)
- Submit actions: "Save Draft" vs "Submit for Allocation"
- Status badges (DRAFT, PENDING_APPROVAL, ALLOCATED, PICKING, PACKED, SHIPPED, DELIVERED)

**Out of Scope:**
- Customer CRUD (deferred to separate admin task)
- Order editing after submission (only cancel supported)
- Multi-currency pricing (single currency for Phase 1.5)
- Order templates or repeat orders (deferred)

### Requirements

**Functional:**
1. List page shows all sales orders (filters: status, customer, date range)
2. Create form fields: Customer (required dropdown with search), Shipping Address (auto-filled, editable), Requested Delivery Date (optional), Order Lines (dynamic table)
3. Line fields: Item (dropdown with search), Qty (number), Unit Price (number, optional), Remove button
4. Add Line button (max 50 lines per order)
5. Total Amount auto-calculated (sum of Qty × Unit Price)
6. Validation: Customer required, at least 1 line, Item required per line, Qty > 0
7. Submit options:
   - "Save Draft": status = DRAFT, no allocation trigger
   - "Submit for Allocation": check creditLimit → if totalAmount > creditLimit then status = PENDING_APPROVAL, else trigger allocation saga immediately
8. Success: redirect to order detail, toast "Order SO-0001 created"

**Non-Functional:**
1. Responsive: works on tablet (768px+)
2. Performance: Customer dropdown lazy-loads (autocomplete after 2 chars)
3. Accessibility: keyboard navigation, screen reader labels
4. Error handling: API errors shown inline + toast

**UI Wireframe:**
```
Sales Orders List:
┌─────────────────────────────────────────────────────────┐
│ Sales Orders                              [+ Create]    │
│ Filters: [Status ▼] [Customer ▼] [Date Range]          │
├─────────────────────────────────────────────────────────┤
│ Order# │ Customer │ Status │ Order Date │ Total │ Actions │
│ SO-0001│ ACME    │ ALLOCATED │ 2026-02-10 │ $1,250 │ [View] │
│ SO-0002│ GlobalCo│ PICKING   │ 2026-02-11 │ $3,400 │ [View] │
└─────────────────────────────────────────────────────────┘

Create Sales Order:
┌─────────────────────────────────────────────────────────┐
│ Customer: [ACME Corp ▼] *                               │
│ Shipping Address:                                       │
│ [123 Main St_____________________]                      │
│ [City____] [State__] [Zip____] [Country___]            │
│ Requested Delivery Date: [📅 2026-02-20]               │
│                                                         │
│ Order Lines:                                            │
│ ┌──────────────────────────────────────────────────┐  │
│ │ Item      │ Qty │ Unit Price │ Total │ [Remove] │  │
│ │ FG-0001  │ 50  │ $10.00    │ $500  │ [X]      │  │
│ │ FG-0002  │ 30  │ $25.00    │ $750  │ [X]      │  │
│ │ [+ Add Line]                                     │  │
│ └──────────────────────────────────────────────────┘  │
│                                                         │
│ Total Amount: $1,250.00                                │
│                                                         │
│ [Cancel] [Save Draft] [Submit for Allocation]          │
└─────────────────────────────────────────────────────────┘
```

**API Contract:**
```csharp
// POST /api/warehouse/v1/sales-orders
public record CreateSalesOrderRequest(
    Guid CommandId,
    int CustomerId,
    string ShippingAddress,
    DateTime? RequestedDeliveryDate,
    List<SalesOrderLineDto> Lines
);

public record SalesOrderLineDto(
    int ItemId,
    decimal Qty,
    decimal? UnitPrice
);

// Response
public record SalesOrderResponse(
    int Id,
    string OrderNumber,
    SalesOrderStatus Status,
    int CustomerId,
    string CustomerName,
    decimal TotalAmount,
    DateTime CreatedAt
);
```

### Acceptance Criteria

```gherkin
Scenario: Create sales order via UI
  Given logged in as Sales Admin
  When navigate to /warehouse/sales/orders
  And click "Create"
  And select Customer "ACME Corp"
  And shipping address auto-fills from customer
  And add line: Item "FG-0001", Qty 50, Unit Price $10
  And add line: Item "FG-0002", Qty 30, Unit Price $25
  Then Total Amount shows $1,250
  When click "Submit for Allocation"
  Then API POST /api/warehouse/v1/sales-orders called
  And redirect to /warehouse/sales/orders/{id}
  And toast message "Order SO-0001 created and allocated"

Scenario: Validation errors shown
  Given on Create Sales Order page
  When click "Submit for Allocation" without selecting Customer
  Then error message "Customer is required"
  And form not submitted
  When add line without selecting Item
  Then error message "Item is required"

Scenario: Credit limit triggers approval workflow
  Given Customer "ACME Corp" with CreditLimit $1,000
  When create order with Total Amount $1,500
  And click "Submit for Allocation"
  Then SalesOrder created with status PENDING_APPROVAL
  And toast "Order SO-0002 requires manager approval (over credit limit)"

Scenario: Save draft without allocation
  Given on Create Sales Order page
  When fill form with valid data
  And click "Save Draft"
  Then SalesOrder created with status DRAFT
  And no allocation triggered
  And toast "Order SO-0003 saved as draft"

Scenario: Max lines validation
  Given on Create Sales Order page with 50 lines
  When click "Add Line"
  Then error message "Maximum 50 lines per order"
  And Add Line button disabled
```

### Validation / Checks

**Local Testing:**
```bash
# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to http://localhost:5001/warehouse/sales/orders
# Click "Create"
# Fill form and submit
# Verify redirect and toast

# Test validation
# Expected: inline errors + toast notifications

# Test credit limit workflow
# Create order with amount > customer credit limit
# Expected: status = PENDING_APPROVAL
```

**Metrics:**
- `ui_sales_orders_created_total` (counter, labels: status=DRAFT|ALLOCATED|PENDING_APPROVAL)
- `ui_page_load_duration_ms` (histogram, route: /warehouse/sales/orders)

**Logs:**
- INFO: "User {UserId} created sales order {OrderNumber}, status {Status}, amount {TotalAmount}"

### Definition of Done

- [ ] Blazor page created: `Pages/SalesOrders.razor`
- [ ] Blazor page created: `Pages/SalesOrderCreate.razor`
- [ ] Service client: `SalesOrderClient.CreateOrderAsync()`
- [ ] DTO models aligned with API contract
- [ ] Customer dropdown with lazy-loading search
- [ ] Dynamic order lines table with add/remove
- [ ] Total amount auto-calculation
- [ ] Validation logic (client-side + server-side)
- [ ] Shipping address auto-fill from customer
- [ ] Credit limit check (UI logic to determine PENDING_APPROVAL vs immediate allocation)
- [ ] Status badges styled (DRAFT=gray, ALLOCATED=green, PENDING_APPROVAL=yellow)
- [ ] Responsive design tested on tablet viewport
- [ ] Manual test: create order, verify in database
- [ ] Manual test: credit limit workflow
- [ ] Code review completed

---

## Task PRD-1528: Sales Order List & Detail UI

**Epic:** Sales Orders
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** PRD-1527
**SourceRefs:** Universe §4.Epic B, Universe §3.Workflow 2

### Context

- PRD-1527 creates orders via UI, now need list view and detail page
- Operators need to view order details, track status, see allocated HUs
- Manager needs approval queue (orders over credit limit)
- Must show order lifecycle: DRAFT → ALLOCATED → PICKING → PACKED → SHIPPED → DELIVERED

### Scope

**In Scope:**
- Enhanced Sales Orders List page (already basic version in PRD-1527)
- Sales Order Detail page (`/warehouse/sales/orders/{id}`)
- Order info panel (customer, shipping address, total amount)
- Lines table (item, ordered qty, allocated qty, picked qty, shipped qty)
- Reservation info (if allocated: reservation ID, lock type, allocated HUs)
- Shipment info (if shipped: shipment number, tracking number, carrier)
- Actions: Cancel Order (if not SHIPPED), View Shipment (if shipped)
- Audit trail (created by, created at, updated by, updated at)

**Out of Scope:**
- Order editing (cancel only, no line modifications)
- Order history timeline view (deferred)
- Customer order portal (deferred)

### Requirements

**Functional:**
1. List page enhancements: sortable columns (Order Date, Total Amount), pagination (50 per page)
2. Detail page layout: Header (Order Number, Status badge, Order Date), Customer Info, Shipping Address, Lines Table, Reservation Info, Shipment Info, Audit Trail
3. Lines table columns: Item Code, Item Name, Ordered Qty, Allocated Qty, Picked Qty, Shipped Qty, Status (per line)
4. Reservation Info panel: Reservation ID (link), Lock Type (SOFT/HARD badge), Allocated HUs (list with locations)
5. Shipment Info panel: Shipment Number (link), Tracking Number (copyable), Carrier, Dispatch Date
6. Actions: "Cancel Order" button (confirmation modal), "View Shipment" button (navigate to shipment detail)
7. Validation: Cancel only allowed if status ≠ SHIPPED|DELIVERED|CANCELLED

**Non-Functional:**
1. Performance: Detail page loads < 2 seconds
2. Real-time status: Refresh button (manual refresh, no auto-polling yet)
3. Copyable tracking number (click-to-copy)
4. Error handling: 404 if order not found

**UI Wireframe:**
```
Sales Order Detail (SO-0001):
┌─────────────────────────────────────────────────────────┐
│ Order SO-0001                       [🔄 Refresh]        │
│ Status: ALLOCATED ●                                     │
│ Order Date: 2026-02-10 14:30                           │
├─────────────────────────────────────────────────────────┤
│ Customer: ACME Corp (CUST-0001)                        │
│ Shipping Address:                                       │
│ 123 Main St, City, State 12345, USA                    │
│ Requested Delivery Date: 2026-02-20                    │
│ Total Amount: $1,250.00                                │
├─────────────────────────────────────────────────────────┤
│ Order Lines:                                            │
│ Item      │ Ordered │ Allocated │ Picked │ Shipped      │
│ FG-0001  │ 50      │ 50        │ 0      │ 0            │
│ FG-0002  │ 30      │ 30        │ 0      │ 0            │
├─────────────────────────────────────────────────────────┤
│ Reservation Info:                                       │
│ Reservation ID: RES-0001 [View]                        │
│ Lock Type: SOFT ●                                       │
│ Allocated HUs:                                          │
│ - HU-0001 (FG-0001, Qty 50) at A1-B1                  │
│ - HU-0002 (FG-0002, Qty 30) at A2-B3                  │
├─────────────────────────────────────────────────────────┤
│ Shipment Info:                                          │
│ (Not yet shipped)                                       │
├─────────────────────────────────────────────────────────┤
│ Audit Trail:                                            │
│ Created By: admin-dev on 2026-02-10 14:30             │
│ Updated By: system on 2026-02-10 14:31 (allocated)    │
├─────────────────────────────────────────────────────────┤
│ [Cancel Order] [Close]                                  │
└─────────────────────────────────────────────────────────┘
```

**API Contracts:**
```http
GET /api/warehouse/v1/sales-orders/{id}
Response:
{
  "id": 1,
  "orderNumber": "SO-0001",
  "status": "ALLOCATED",
  "customerId": 1,
  "customerName": "ACME Corp",
  "shippingAddress": "123 Main St...",
  "requestedDeliveryDate": "2026-02-20",
  "totalAmount": 1250.00,
  "lines": [
    {
      "itemId": 1,
      "itemCode": "FG-0001",
      "orderedQty": 50,
      "allocatedQty": 50,
      "pickedQty": 0,
      "shippedQty": 0
    }
  ],
  "reservationId": "RES-0001",
  "lockType": "SOFT",
  "allocatedHUs": [
    { "huId": "HU-0001", "itemCode": "FG-0001", "qty": 50, "location": "A1-B1" }
  ],
  "shipmentNumber": null,
  "trackingNumber": null,
  "createdBy": "admin-dev",
  "createdAt": "2026-02-10T14:30:00Z"
}

POST /api/warehouse/v1/sales-orders/{id}/cancel
```

### Acceptance Criteria

```gherkin
Scenario: View sales order detail
  Given SalesOrder SO-0001 exists with status ALLOCATED
  When navigate to /warehouse/sales/orders/1
  Then page loads order details
  And customer info displayed
  And lines table shows ordered/allocated/picked/shipped quantities
  And reservation info panel shows RES-0001 with SOFT lock
  And allocated HUs listed with locations

Scenario: Copy tracking number
  Given SalesOrder SO-0002 with status SHIPPED, tracking "FDX123456789"
  When navigate to order detail
  And click tracking number
  Then tracking number copied to clipboard
  And toast "Tracking number copied"

Scenario: Cancel order
  Given SalesOrder SO-0001 with status ALLOCATED
  When click "Cancel Order"
  Then confirmation modal: "Cancel order SO-0001?"
  When confirm cancellation
  Then API POST /api/warehouse/v1/sales-orders/1/cancel called
  And status updated to CANCELLED
  And toast "Order SO-0001 cancelled"

Scenario: Cannot cancel shipped order
  Given SalesOrder SO-0003 with status SHIPPED
  When navigate to order detail
  Then "Cancel Order" button disabled
  And tooltip "Cannot cancel shipped order"

Scenario: Refresh order status
  Given on order detail page
  When click Refresh button
  Then API GET /api/warehouse/v1/sales-orders/{id} called
  And page data reloaded
  And toast "Order data refreshed"
```

### Validation / Checks

**Local Testing:**
```bash
# Get dev token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Test order detail API
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/warehouse/v1/sales-orders/1 | jq

# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/sales/orders/1
# Verify all panels load
# Test cancel order workflow
# Test copy tracking number (if order shipped)
```

**Metrics:**
- `ui_sales_order_detail_views_total` (counter)
- `ui_sales_orders_cancelled_total` (counter)

**Logs:**
- INFO: "User {UserId} viewed sales order {OrderNumber}"
- INFO: "User {UserId} cancelled sales order {OrderNumber}"

### Definition of Done

- [ ] Blazor page created: `Pages/SalesOrderDetail.razor`
- [ ] Service client: `SalesOrderClient.GetOrderAsync(id)`
- [ ] Service client: `SalesOrderClient.CancelOrderAsync(id)`
- [ ] Customer info panel component
- [ ] Lines table component
- [ ] Reservation info panel component
- [ ] Shipment info panel component
- [ ] Audit trail component
- [ ] Cancel order button with confirmation modal
- [ ] Copy tracking number functionality
- [ ] Refresh button
- [ ] Status badge styling (color-coded)
- [ ] Responsive design tested
- [ ] Manual test: view order, cancel order, refresh
- [ ] Code review completed

---

## Task PRD-1529: Allocation & Release UI

**Epic:** Sales Orders
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** PRD-1505 (SalesOrder APIs), PRD-1509 (Outbound Projections)
**SourceRefs:** Universe §4.Epic B, Universe §3.Workflow 2

### Context

- Sales orders created via PRD-1527, allocation happens automatically or via approval
- Managers need approval queue for orders over credit limit
- Operators need "Release to Picking" action to convert SOFT → HARD locks
- Must visualize allocation status: pending stock, allocated, released to picking

### Scope

**In Scope:**
- Allocation Dashboard (`/warehouse/sales/allocations`)
- Pending Approvals section (orders over credit limit)
- Pending Stock section (insufficient inventory)
- Allocated Orders section (ready to release)
- Approve button (Manager role only)
- Release to Picking button (triggers StartPicking command)
- Reallocate action (manual HU selection if auto-allocation failed)

**Out of Scope:**
- Manual allocation rules configuration (use default FEFO/FIFO for Phase 1.5)
- Wave picking (batch release multiple orders, deferred)
- Allocation optimization engine (deferred)

### Requirements

**Functional:**
1. Dashboard loads on `/warehouse/sales/allocations`
2. Three sections: Pending Approvals, Pending Stock, Allocated Orders
3. Pending Approvals: show orders with status PENDING_APPROVAL (totalAmount > creditLimit), display Customer, Total Amount, Credit Limit, "Approve" button
4. Pending Stock: show orders with status PENDING_STOCK (insufficient inventory), display Items, Available Stock, Expected Restock Date, "Reallocate" button
5. Allocated Orders: show orders with status ALLOCATED (ready to pick), display Order Number, Customer, Allocated HUs, "Release to Picking" button
6. Approve action: POST `/api/warehouse/v1/sales-orders/{id}/approve` → status ALLOCATED, trigger allocation saga
7. Release action: POST `/api/warehouse/v1/sales-orders/{id}/release` → reservation lock SOFT→HARD, status PICKING
8. Reallocate action: open modal with available HUs, manual selection, POST `/api/warehouse/v1/sales-orders/{id}/reallocate`

**Non-Functional:**
1. Authorization: Approve requires Manager role, Release requires Operator role
2. Performance: Dashboard loads < 3 seconds
3. Refresh button (manual refresh)
4. Error handling: Insufficient stock on release → show error, keep status ALLOCATED

**UI Wireframe:**
```
Allocation Dashboard:
┌─────────────────────────────────────────────────────────┐
│ Allocation & Release Dashboard          [🔄 Refresh]   │
├─────────────────────────────────────────────────────────┤
│ Pending Approvals (Over Credit Limit)                  │
│ Order    │ Customer │ Total  │ Credit Limit │ Actions  │
│ SO-0001 │ ACME    │ $1,500│ $1,000      │ [Approve] │
│ SO-0002 │ GlobalCo│ $5,000│ $3,000      │ [Approve] │
├─────────────────────────────────────────────────────────┤
│ Pending Stock (Insufficient Inventory)                  │
│ Order    │ Items          │ Available │ Actions        │
│ SO-0003 │ FG-0001 (need 100) │ 50/100 │ [Reallocate] │
├─────────────────────────────────────────────────────────┤
│ Allocated Orders (Ready to Pick)                       │
│ Order    │ Customer │ HUs Allocated │ Actions          │
│ SO-0004 │ ACME    │ 2 HUs        │ [Release to Picking]│
│ SO-0005 │ GlobalCo│ 1 HU         │ [Release to Picking]│
└─────────────────────────────────────────────────────────┘

Reallocate Modal:
┌─────────────────────────────────────────────────────────┐
│ Reallocate Stock for SO-0003                           │
├─────────────────────────────────────────────────────────┤
│ Item: FG-0001, Required: 100 units                     │
│                                                         │
│ Available HUs:                                          │
│ [ ] HU-0001 (Qty 50) at A1-B1, Lot: LOT-A, Exp: 2027-01│
│ [ ] HU-0002 (Qty 30) at A2-B3, Lot: LOT-B, Exp: 2027-03│
│ [ ] HU-0003 (Qty 25) at A3-B4, Lot: LOT-C, Exp: 2027-02│
│                                                         │
│ Selected: 80 / 100 units ⚠️ Insufficient              │
│                                                         │
│ [Cancel] [Confirm Allocation]                          │
└─────────────────────────────────────────────────────────┘
```

**API Contracts:**
```http
GET /api/warehouse/v1/sales-orders/pending-approvals
GET /api/warehouse/v1/sales-orders/pending-stock
GET /api/warehouse/v1/sales-orders/allocated

POST /api/warehouse/v1/sales-orders/{id}/approve
POST /api/warehouse/v1/sales-orders/{id}/release
POST /api/warehouse/v1/sales-orders/{id}/reallocate
Body: { "huIds": ["HU-0001", "HU-0002"] }
```

### Acceptance Criteria

```gherkin
Scenario: Manager approves order over credit limit
  Given SalesOrder SO-0001 with status PENDING_APPROVAL (total $1,500, credit $1,000)
  When Manager navigates to /warehouse/sales/allocations
  Then Pending Approvals section shows SO-0001
  When click "Approve"
  Then confirmation modal: "Approve SO-0001 for $1,500?"
  When confirm
  Then API POST /api/warehouse/v1/sales-orders/1/approve called
  And allocation saga triggered
  And order status → ALLOCATED
  And toast "Order SO-0001 approved and allocated"

Scenario: Operator releases order to picking
  Given SalesOrder SO-0004 with status ALLOCATED, reservation SOFT lock
  When Operator navigates to /warehouse/sales/allocations
  Then Allocated Orders section shows SO-0004
  When click "Release to Picking"
  Then API POST /api/warehouse/v1/sales-orders/4/release called
  And reservation lock → HARD
  And order status → PICKING
  And toast "Order SO-0004 released to picking"

Scenario: Reallocate order with insufficient stock
  Given SalesOrder SO-0003 with status PENDING_STOCK (need 100, available 50)
  When navigate to Pending Stock section
  And click "Reallocate"
  Then modal shows available HUs sorted by FEFO
  When select HU-0001 (50 units) and HU-0002 (30 units)
  Then selected total shows 80 / 100 ⚠️
  And "Confirm Allocation" button enabled (allows partial)
  When click "Confirm Allocation"
  Then API POST /api/warehouse/v1/sales-orders/3/reallocate called
  And order status → ALLOCATED (partial allocation allowed)
  And toast "Order SO-0003 partially allocated (80 / 100)"

Scenario: Authorization check for approve
  Given logged in as Operator (not Manager)
  When navigate to /warehouse/sales/allocations
  Then Pending Approvals section shows orders
  But "Approve" buttons disabled
  And tooltip "Manager role required"

Scenario: Release fails due to stock consumed
  Given SalesOrder SO-0005 with status ALLOCATED
  But allocated HU was consumed by another order (race condition)
  When click "Release to Picking"
  Then API returns 409 Conflict: "Insufficient stock"
  And error toast "Cannot release SO-0005: stock no longer available"
  And order status remains ALLOCATED
```

### Validation / Checks

**Local Testing:**
```bash
# Get dev token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Test allocation APIs
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/warehouse/v1/sales-orders/pending-approvals

curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/sales-orders/1/approve

curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/sales-orders/1/release

# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/sales/allocations
# Test approve, release, reallocate workflows
```

**Metrics:**
- `ui_sales_orders_approved_total` (counter)
- `ui_sales_orders_released_total` (counter)
- `ui_sales_orders_reallocated_total` (counter)

**Logs:**
- INFO: "User {UserId} approved sales order {OrderNumber}"
- INFO: "User {UserId} released sales order {OrderNumber} to picking"
- WARN: "Release failed for order {OrderNumber}: {ErrorReason}"

### Definition of Done

- [ ] Blazor page created: `Pages/AllocationDashboard.razor`
- [ ] Service client: `SalesOrderClient.GetPendingApprovalsAsync()`
- [ ] Service client: `SalesOrderClient.GetPendingStockAsync()`
- [ ] Service client: `SalesOrderClient.GetAllocatedOrdersAsync()`
- [ ] Service client: `SalesOrderClient.ApproveOrderAsync(id)`
- [ ] Service client: `SalesOrderClient.ReleaseOrderAsync(id)`
- [ ] Service client: `SalesOrderClient.ReallocateOrderAsync(id, huIds)`
- [ ] Pending Approvals section component
- [ ] Pending Stock section component
- [ ] Allocated Orders section component
- [ ] Approve button with confirmation modal
- [ ] Release button with error handling
- [ ] Reallocate modal with HU selection
- [ ] Authorization check (Manager for approve, Operator for release)
- [ ] Responsive design tested
- [ ] Manual test: full approve → release workflow
- [ ] Manual test: reallocate with partial stock
- [ ] Code review completed

---

## Task PRD-1530: Picking Workflow UI Enhancements

**Epic:** Picking
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** PRD-1505 (SalesOrder APIs)
**SourceRefs:** Universe §3.Workflow 2

### Context

- Phase 1 has basic picking UI (pick tasks list, FEFO suggestions, scan & execute)
- Need enhancements for sales order picking workflow: order-based pick view, multi-HU picks, error handling
- Must integrate with sales order lifecycle (after Release to Picking from PRD-1529)
- Barcode scanning critical: operators scan HU barcode, confirm qty, move to next item

### Scope

**In Scope:**
- Enhanced Pick Tasks page (`/warehouse/picking/tasks`)
- Order-based pick view (group picks by sales order)
- Multi-HU pick support (order requires items from multiple HUs)
- Barcode scan error handling (wrong HU, wrong qty)
- Pick completion status (per line + overall order progress)
- FEFO suggestions highlighted (next recommended HU)

**Out of Scope:**
- Wave picking (batch multiple orders, deferred)
- Route optimization (pick path through warehouse, deferred)
- Voice picking integration (deferred)

### Requirements

**Functional:**
1. Pick Tasks page shows all orders with status PICKING
2. Order card layout: Order Number, Customer, Items (qty), Assigned Picker (optional), "Start Picking" button
3. Pick execution view: Item list (Item, Qty Required, Qty Picked, HU Suggestions), Scan HU input, Confirm Qty input
4. HU suggestions: sorted by FEFO (expiry date), highlighted (next recommended = green, others = gray)
5. Scan HU barcode: validate HU matches order item, qty available ≥ qty required
6. Confirm qty: default = min(HU qty, remaining qty), editable
7. Submit: POST `/api/warehouse/v1/picks/execute` → StockMoved (Storage → PICKING_STAGING), Reservation.Consume(qty)
8. Multi-HU handling: if HU qty < required qty → partial pick, suggest next HU automatically
9. Pick completion: when all lines picked → order status PICKING → READY_FOR_PACKING

**Non-Functional:**
1. Barcode scanner support: continuous scanning (auto-submit on Enter)
2. Error feedback: visual + audio alert on scan mismatch
3. Keyboard shortcuts: Enter = confirm, Esc = cancel pick
4. Performance: pick execution < 1 second

**UI Wireframe:**
```
Pick Tasks:
┌─────────────────────────────────────────────────────────┐
│ Pick Tasks                               [🔄 Refresh]   │
├─────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────┐│
│ │ Order SO-0001 - ACME Corp                          ││
│ │ Items: FG-0001 (50), FG-0002 (30)                  ││
│ │ Assigned: Operator1                                ││
│ │ [Start Picking]                                     ││
│ └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘

Pick Execution (SO-0001):
┌─────────────────────────────────────────────────────────┐
│ Picking Order SO-0001                    [✓ 1/2 items] │
├─────────────────────────────────────────────────────────┤
│ Current Item: FG-0001                                   │
│ Required: 50 units | Picked: 0 / 50                    │
│                                                         │
│ Recommended HUs (FEFO):                                 │
│ ✅ HU-0001 (Qty 50) at A1-B1, Lot: LOT-A, Exp: 2027-01 │
│    HU-0002 (Qty 30) at A2-B3, Lot: LOT-B, Exp: 2027-03 │
│                                                         │
│ [Scan HU Barcode] ________________                      │
│ HU: HU-0001 (auto-filled from scan)                    │
│ Qty to pick: [50___] (max 50 available)                │
│ [Confirm Pick]                                          │
└─────────────────────────────────────────────────────────┘

Multi-HU Pick (Partial):
┌─────────────────────────────────────────────────────────┐
│ Current Item: FG-0003                                   │
│ Required: 100 units | Picked: 50 / 100 ⚠️              │
│                                                         │
│ ✅ HU-0005 picked: 50 units                            │
│                                                         │
│ Next recommended:                                       │
│ ✅ HU-0006 (Qty 30) at A3-B4, Lot: LOT-C, Exp: 2027-02 │
│    HU-0007 (Qty 25) at A4-B5, Lot: LOT-D, Exp: 2027-04 │
│                                                         │
│ [Scan HU Barcode] ________________                      │
└─────────────────────────────────────────────────────────┘
```

**API Contracts:**
```http
GET /api/warehouse/v1/picks/tasks
Response: [
  {
    "orderId": 1,
    "orderNumber": "SO-0001",
    "customer": "ACME Corp",
    "items": [
      { "itemCode": "FG-0001", "qtyRequired": 50, "qtyPicked": 0 }
    ],
    "assignedPicker": "Operator1",
    "suggestedHUs": [
      { "huId": "HU-0001", "itemCode": "FG-0001", "qty": 50, "location": "A1-B1", "lot": "LOT-A", "expiry": "2027-01-01" }
    ]
  }
]

POST /api/warehouse/v1/picks/execute
Body:
{
  "commandId": "uuid",
  "orderId": 1,
  "itemId": 1,
  "huId": "HU-0001",
  "qtyPicked": 50
}
```

### Acceptance Criteria

```gherkin
Scenario: Start picking order
  Given SalesOrder SO-0001 with status PICKING
  When Operator navigates to /warehouse/picking/tasks
  Then Pick Tasks page shows SO-0001
  When click "Start Picking"
  Then Pick execution view opens
  And first item FG-0001 shown (required 50)
  And HU suggestions sorted by FEFO
  And HU-0001 highlighted (earliest expiry)

Scenario: Pick item via barcode scan
  Given on Pick execution view for SO-0001, item FG-0001
  When operator scans HU barcode "HU-0001"
  Then HU field auto-fills
  And qty defaults to 50 (min of HU qty and required qty)
  When click "Confirm Pick"
  Then API POST /api/warehouse/v1/picks/execute called
  And StockMoved event (A1-B1 → PICKING_STAGING)
  And item status updated: Picked 50 / 50 ✓
  And UI advances to next item FG-0002

Scenario: Multi-HU pick (partial)
  Given item FG-0003 requires 100 units
  And HU-0005 has 50 units
  When scan HU-0005 and pick 50
  Then item status: Picked 50 / 100 ⚠️
  And next HU suggestion highlighted (HU-0006)
  When scan HU-0006 (30 units) and pick 30
  Then item status: Picked 80 / 100 ⚠️
  When scan HU-0007 (25 units) and pick 20 (partial from HU)
  Then item status: Picked 100 / 100 ✓
  And item marked complete

Scenario: Scan wrong HU (barcode mismatch)
  Given picking item FG-0001 (allocated HUs: HU-0001, HU-0002)
  When scan HU barcode "HU-0099" (different item)
  Then error alert "HU-0099 not allocated to this order"
  And audio beep (error)
  And HU field clears, focus returns to scan input

Scenario: All items picked, order complete
  Given picking SO-0001 with 2 items
  When pick FG-0001 (50 units) ✓
  And pick FG-0002 (30 units) ✓
  Then all items complete
  And order status → READY_FOR_PACKING
  And toast "Order SO-0001 picking complete"
  And redirect to pick tasks list
```

### Validation / Checks

**Local Testing:**
```bash
# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/picking/tasks
# Start picking order
# Test barcode scan (keyboard wedge or manual entry)
# Test multi-HU pick
# Verify stock movements in database

# Test error handling
# Scan wrong HU barcode
# Expected: error alert + audio beep
```

**Metrics:**
- `ui_picks_executed_total` (counter)
- `ui_pick_duration_seconds` (histogram, per order)
- `ui_pick_scan_errors_total` (counter)

**Logs:**
- INFO: "User {UserId} started picking order {OrderNumber}"
- INFO: "Picked {Qty} units of {ItemCode} from {HUId} by {OperatorId}"
- WARN: "Pick scan error: HU {HUId} not allocated to order {OrderNumber}"

### Definition of Done

- [ ] Enhanced Pick Tasks page with order-based cards
- [ ] Pick execution view component
- [ ] HU suggestions component (FEFO-sorted, highlighted)
- [ ] Barcode scan input with auto-submit (Enter key)
- [ ] Multi-HU pick logic (partial picks, auto-advance to next HU)
- [ ] Error handling: wrong HU, audio alert
- [ ] Keyboard shortcuts (Enter, Esc)
- [ ] Pick completion detection (all items picked → status change)
- [ ] Progress indicator (X / Y items picked)
- [ ] Responsive design tested
- [ ] Manual test: full pick workflow (single HU + multi-HU)
- [ ] Manual test: error scenarios (wrong HU, insufficient qty)
- [ ] Code review completed

---

## Task PRD-1531: Packing Station UI Enhancements

**Epic:** Packing
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** UI
**Dependencies:** PRD-1507 (Packing APIs), PRD-1510 (Outbound UI)
**SourceRefs:** Universe §3.Workflow 2

### Context

- PRD-1507 implemented packing backend, PRD-1510 basic UI
- Need enhancements: barcode verification (scan each item to confirm), mismatch alerts, packaging type selection, label preview
- Packing errors (wrong item scanned) are critical → must prevent before dispatch
- Label printing integration (preview, retry, manual fallback)

### Scope

**In Scope:**
- Enhanced Packing Queue page (`/warehouse/packing/queue`)
- Packing Station page (`/warehouse/packing/station/{orderId}`)
- Item-by-item barcode verification (scan each expected item)
- Mismatch detection (wrong item scanned → alert, reject pack)
- Packaging type dropdown (BOX, PALLET, ENVELOPE)
- Label preview (before print)
- Print retry mechanism (3 attempts)
- Manual print fallback (PDF download if printer offline)

**Out of Scope:**
- Multi-parcel packing (1 order = 1 shipment for Phase 1.5)
- Packing instructions/checklists (deferred)
- Dimensional weight calculation (deferred)

### Requirements

**Functional:**
1. Packing Queue shows orders with status READY_FOR_PACKING
2. Packing Station loads order details (items, picked quantities, customer, shipping address)
3. Item verification table: Item Code, Expected Qty, Scanned Qty, Status (PENDING, VERIFIED, MISMATCH)
4. Scan item barcode: increment Scanned Qty, if matches Expected Qty → status VERIFIED
5. Mismatch detection: if scanned item not in order → alert "Item {ItemCode} not in this order", reject scan
6. Packaging type selection: dropdown (BOX, PALLET, ENVELOPE), default = BOX
7. Generate label button: POST `/api/warehouse/v1/shipments/pack` → ZPL label generation
8. Label preview: show address, tracking number, barcode
9. Print label: send to printer, retry up to 3x on failure
10. Manual fallback: if printer offline after 3 retries → "Download PDF Label" button
11. Confirm pack: POST `/api/warehouse/v1/shipments/{id}/confirm-pack` → Shipment.Status = PACKED, Order.Status = PACKED

**Non-Functional:**
1. Barcode scanner support: continuous scanning
2. Error feedback: visual + audio alert on mismatch
3. Printer status indicator (online/offline)
4. Performance: label generation < 3 seconds

**UI Wireframe:**
```
Packing Station (SO-0001):
┌─────────────────────────────────────────────────────────┐
│ Packing Station - Order SO-0001                         │
│ Customer: ACME Corp                                     │
│ Shipping: 123 Main St, City, State 12345               │
├─────────────────────────────────────────────────────────┤
│ Verify Items (Scan each):                              │
│ Item      │ Expected │ Scanned │ Status                │
│ FG-0001  │ 50       │ 50      │ ✓ VERIFIED            │
│ FG-0002  │ 30       │ 0       │ ⏳ PENDING             │
│                                                         │
│ [Scan Item Barcode] ________________                    │
│                                                         │
│ Packaging Type: [BOX ▼]                                │
│                                                         │
│ [Generate Label] (disabled until all verified)         │
└─────────────────────────────────────────────────────────┘

Label Preview:
┌─────────────────────────────────────────────────────────┐
│ Shipping Label Preview                                  │
├─────────────────────────────────────────────────────────┤
│ FROM:                        TO:                        │
│ Warehouse Inc               ACME Corp                   │
│ 456 Warehouse Rd            123 Main St                 │
│ City, State 98765           City, State 12345          │
│                                                         │
│ Tracking: FDX123456789                                  │
│ │││││││││││││││││││││││                              │
│                                                         │
│ [Print Label] [Download PDF] [Cancel]                  │
└─────────────────────────────────────────────────────────┘

Packing Error:
┌─────────────────────────────────────────────────────────┐
│ ❌ Item Mismatch!                                       │
│ Scanned: FG-9999                                        │
│ Expected: FG-0001 or FG-0002                           │
│ This item is not in order SO-0001.                     │
│ [OK]                                                    │
└─────────────────────────────────────────────────────────┘
```

**API Contracts:**
```http
POST /api/warehouse/v1/shipments/pack
Body:
{
  "commandId": "uuid",
  "orderId": 1,
  "packagingType": "BOX",
  "verifiedItems": [
    { "itemId": 1, "scannedQty": 50 }
  ]
}
Response:
{
  "shipmentId": 1,
  "trackingNumber": "FDX123456789",
  "zplLabel": "^XA^FO50,50^A0N,50,50^FD...",
  "labelPreviewUrl": "/api/labels/preview/1"
}

POST /api/warehouse/v1/shipments/{id}/print-label
POST /api/warehouse/v1/shipments/{id}/confirm-pack
```

### Acceptance Criteria

```gherkin
Scenario: Pack order with barcode verification
  Given Order SO-0001 with status READY_FOR_PACKING (items: FG-0001 qty 50, FG-0002 qty 30)
  When Operator navigates to /warehouse/packing/station/1
  Then item verification table shows 2 items (status PENDING)
  When scan barcode "FG-0001" (50 times)
  Then FG-0001 scanned qty increments to 50
  And status → VERIFIED ✓
  When scan barcode "FG-0002" (30 times)
  Then FG-0002 scanned qty increments to 30
  And status → VERIFIED ✓
  And "Generate Label" button enabled

Scenario: Mismatch detection
  Given packing order SO-0001 (expected: FG-0001, FG-0002)
  When scan barcode "FG-9999" (not in order)
  Then error modal "Item FG-9999 not in this order"
  And audio beep (error)
  And scan rejected, scanned qty not incremented

Scenario: Generate and print label
  Given all items verified
  When select Packaging Type "BOX"
  And click "Generate Label"
  Then API POST /api/warehouse/v1/shipments/pack called
  And label preview modal opens (shows address, tracking, barcode)
  When click "Print Label"
  Then ZPL sent to printer (TCP 9100)
  And toast "Label printing..."
  And after 2 seconds: toast "Label printed successfully"
  And "Confirm Pack" button enabled

Scenario: Printer offline fallback
  Given label generated
  When click "Print Label"
  But printer offline (connection refused)
  Then retry 3 times (1 second interval)
  And after 3 failures: toast "Printer offline"
  And "Download PDF" button appears
  When click "Download PDF"
  Then browser downloads shipment-label-1.pdf

Scenario: Confirm pack
  Given label printed
  When click "Confirm Pack"
  Then API POST /api/warehouse/v1/shipments/1/confirm-pack called
  And shipment status → PACKED
  And order status → PACKED
  And toast "Order SO-0001 packed and ready for dispatch"
  And redirect to packing queue
```

### Validation / Checks

**Local Testing:**
```bash
# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/packing/queue
# Select order
# Test item verification (scan barcodes)
# Test mismatch detection (scan wrong item)
# Test label generation and preview
# Test print (if printer available) or PDF download

# Test printer retry
# Simulate printer offline (disconnect network)
# Expected: 3 retries, then PDF fallback
```

**Metrics:**
- `ui_packing_items_scanned_total` (counter)
- `ui_packing_mismatches_total` (counter)
- `ui_labels_printed_total` (counter)
- `ui_label_print_failures_total` (counter)

**Logs:**
- INFO: "User {UserId} started packing order {OrderNumber}"
- INFO: "Item {ItemCode} scanned and verified by {OperatorId}"
- WARN: "Item mismatch: scanned {ScannedItem}, expected {ExpectedItems}"
- ERROR: "Label print failed for shipment {ShipmentId}, retry {RetryCount}/3"

### Definition of Done

- [ ] Enhanced Packing Station page
- [ ] Item verification table with real-time scanned qty updates
- [ ] Barcode scan input (continuous scanning)
- [ ] Mismatch detection logic + error modal
- [ ] Packaging type dropdown
- [ ] Label generation API integration
- [ ] Label preview modal
- [ ] Print label functionality with retry (3 attempts)
- [ ] Manual PDF download fallback
- [ ] Confirm pack button
- [ ] Printer status indicator
- [ ] Audio alerts for errors
- [ ] Responsive design tested
- [ ] Manual test: full pack workflow (scan → generate → print → confirm)
- [ ] Manual test: mismatch detection
- [ ] Manual test: printer offline fallback
- [ ] Code review completed

---

## Task PRD-1532: Dispatch UI Enhancements

**Epic:** Dispatch
**Phase:** 1.5
**Sprint:** 3
**Estimate:** S (0.5 day)
**OwnerType:** UI
**Dependencies:** PRD-1508 (Dispatch APIs), PRD-1510 (Outbound UI)
**SourceRefs:** Universe §3.Workflow 2

### Context

- PRD-1508 implemented dispatch backend, PRD-1510 basic UI
- Need enhancements: carrier selection dropdown, vehicle ID input, dispatch confirmation modal, tracking number visibility
- Dispatch is final warehouse operation → must log carrier, vehicle, timestamp accurately
- Integration with carrier API (FedEx) to confirm pickup

### Scope

**In Scope:**
- Enhanced Dispatch Queue page (`/warehouse/dispatch/queue`)
- Dispatch confirmation modal (Carrier, Vehicle ID, Dispatch Time)
- Carrier dropdown (FedEx, UPS, DHL, USPS, OTHER)
- Vehicle ID input (truck license plate or delivery van number)
- Tracking number display (copyable)
- Dispatch confirmation: POST `/api/warehouse/v1/shipments/{id}/dispatch`
- Audit log visibility (who dispatched, when, which vehicle)

**Out of Scope:**
- Carrier API real-time pickup scheduling (use offline dispatch for Phase 1.5)
- Driver signature capture (deferred)
- Route optimization (deferred)

### Requirements

**Functional:**
1. Dispatch Queue shows shipments with status PACKED
2. Shipment card: Shipment Number, Order Number, Customer, Tracking Number (copyable), Carrier (if pre-assigned), "Dispatch" button
3. Dispatch modal: Carrier dropdown (required), Vehicle ID input (optional), Dispatch Time (default = now, editable)
4. Confirm dispatch: POST `/api/warehouse/v1/shipments/{id}/dispatch` → Shipment.Status = DISPATCHED, Order.Status = SHIPPED
5. Success: toast "Shipment SHIP-0001 dispatched via FedEx", remove from queue
6. Tracking number: click-to-copy functionality

**Non-Functional:**
1. Performance: Dispatch API < 1 second
2. Audit: log carrier, vehicle, operator, timestamp
3. Error handling: Carrier API failure → graceful degradation (dispatch recorded, carrier notified later)

**UI Wireframe:**
```
Dispatch Queue:
┌─────────────────────────────────────────────────────────┐
│ Dispatch Queue                           [🔄 Refresh]   │
├─────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────┐│
│ │ Shipment SHIP-0001 - Order SO-0001                 ││
│ │ Customer: ACME Corp                                 ││
│ │ Tracking: FDX123456789 [Copy]                       ││
│ │ Carrier: FedEx                                      ││
│ │ [Dispatch]                                          ││
│ └─────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────┘

Dispatch Confirmation Modal:
┌─────────────────────────────────────────────────────────┐
│ Dispatch Shipment SHIP-0001                            │
├─────────────────────────────────────────────────────────┤
│ Carrier: [FedEx ▼] *                                   │
│ Vehicle ID: [VAN-123_____] (optional)                  │
│ Dispatch Time: [📅 2026-02-11 10:30] (default: now)   │
│                                                         │
│ Confirm dispatch to carrier FedEx?                     │
│                                                         │
│ [Cancel] [Confirm Dispatch]                            │
└─────────────────────────────────────────────────────────┘
```

**API Contract:**
```http
POST /api/warehouse/v1/shipments/{id}/dispatch
Body:
{
  "commandId": "uuid",
  "carrier": "FedEx",
  "vehicleId": "VAN-123",
  "dispatchTime": "2026-02-11T10:30:00Z"
}
```

### Acceptance Criteria

```gherkin
Scenario: Dispatch shipment via UI
  Given Shipment SHIP-0001 with status PACKED
  When navigate to /warehouse/dispatch/queue
  Then Dispatch Queue shows SHIP-0001
  When click "Dispatch"
  Then dispatch modal opens
  And Carrier defaults to pre-assigned carrier (FedEx)
  When enter Vehicle ID "VAN-123"
  And click "Confirm Dispatch"
  Then API POST /api/warehouse/v1/shipments/1/dispatch called
  And shipment status → DISPATCHED
  And order SO-0001 status → SHIPPED
  And toast "Shipment SHIP-0001 dispatched via FedEx"
  And shipment removed from queue

Scenario: Copy tracking number
  Given Shipment SHIP-0001 with tracking "FDX123456789"
  When click tracking number
  Then tracking copied to clipboard
  And toast "Tracking number copied"

Scenario: Dispatch with manual time
  Given on dispatch modal
  When change Dispatch Time to "2026-02-11 08:00" (earlier time)
  And click "Confirm Dispatch"
  Then dispatch recorded with custom timestamp
  And audit log shows DispatchTime = 2026-02-11 08:00

Scenario: Carrier API failure (graceful degradation)
  Given Shipment SHIP-0002 with carrier FedEx
  When click "Dispatch"
  And confirm dispatch
  But FedEx API is down (network error)
  Then dispatch still recorded in warehouse system
  And toast "Shipment SHIP-0002 dispatched (carrier notification pending)"
  And background job retries carrier notification
```

### Validation / Checks

**Local Testing:**
```bash
# Get dev token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Test dispatch API
curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/shipments/1/dispatch \
  -H "Content-Type: application/json" \
  -d '{"commandId":"'$(uuidgen)'","carrier":"FedEx","vehicleId":"VAN-123","dispatchTime":"2026-02-11T10:30:00Z"}'

# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/dispatch/queue
# Test dispatch workflow
# Verify tracking copy functionality
```

**Metrics:**
- `ui_shipments_dispatched_total` (counter, labels: carrier)
- `ui_dispatch_duration_seconds` (histogram)

**Logs:**
- INFO: "Shipment {ShipmentNumber} dispatched by {OperatorId} via {Carrier}, vehicle {VehicleId}"

### Definition of Done

- [ ] Enhanced Dispatch Queue page
- [ ] Dispatch confirmation modal component
- [ ] Carrier dropdown (FedEx, UPS, DHL, USPS, OTHER)
- [ ] Vehicle ID input field
- [ ] Dispatch Time picker (default = now)
- [ ] Confirm dispatch button wired to API
- [ ] Tracking number copy functionality
- [ ] Success toast and queue refresh
- [ ] Error handling (carrier API failure)
- [ ] Responsive design tested
- [ ] Manual test: dispatch shipment, verify status change
- [ ] Manual test: copy tracking number
- [ ] Code review completed

---

## Task PRD-1533: Receiving History Report UI

**Epic:** Reports
**Phase:** 1.5
**Sprint:** 3
**Estimate:** S (0.5 day)
**OwnerType:** UI
**Dependencies:** None
**SourceRefs:** Universe §1.Reports

### Context

- Phase 1 has basic receiving history (shipments list)
- Need comprehensive report UI: date range filters, supplier filter, CSV export, summary metrics
- Business use case: monthly receiving volume analysis, supplier performance tracking
- Must integrate with existing InboundShipmentSummary projection

### Scope

**In Scope:**
- Receiving History Report page (`/warehouse/reports/receiving`)
- Date range filter (from/to dates)
- Supplier dropdown filter
- Summary cards (Total Shipments, Total Items Received, Average Lead Time)
- Detailed table (Shipment Number, Supplier, Received Date, Items, Quantities, Operator)
- CSV export button

**Out of Scope:**
- Real-time dashboard (static report for Phase 1.5)
- Chart visualizations (bar/line graphs, deferred)
- Drill-down to shipment detail (link to existing detail page is sufficient)

### Requirements

**Functional:**
1. Report page loads on `/warehouse/reports/receiving`
2. Filters: Date Range (default = last 30 days), Supplier dropdown (optional, default = all)
3. Summary cards: Total Shipments (count), Total Items Received (sum of quantities), Average Lead Time (expected date → received date)
4. Table columns: Shipment Number, Supplier, Expected Date, Received Date, Items Count, Total Qty, Operator, Status (COMPLETED, PARTIAL)
5. Sortable columns (Received Date, Total Qty)
6. Pagination (50 rows per page)
7. CSV export: downloads filtered results with all columns

**Non-Functional:**
1. Performance: Report loads < 3 seconds (for 1000 shipments)
2. Responsive: works on tablet
3. Accessibility: screen reader compatible

**UI Wireframe:**
```
Receiving History Report:
┌─────────────────────────────────────────────────────────┐
│ Receiving History Report                  [Export CSV]  │
│ Filters: [From: 2026-01-11] [To: 2026-02-11] [Supplier ▼: All]│
│          [Apply Filters]                                │
├─────────────────────────────────────────────────────────┤
│ Summary (Last 30 Days):                                 │
│ [Total Shipments] [Total Items] [Avg Lead Time]        │
│       45              12,500         3.2 days           │
├─────────────────────────────────────────────────────────┤
│ Shipment# │ Supplier │ Exp Date │ Rcv Date │ Items │ Qty │ Operator│
│ ISH-0001 │ ACME    │ 02-10   │ 02-11   │ 2    │ 150│ Op1    │
│ ISH-0002 │ GlobalCo│ 02-08   │ 02-09   │ 3    │ 300│ Op2    │
│ ...                                                      │
│                                    [1] [2] [3] ... [10] │
└─────────────────────────────────────────────────────────┘
```

**API Contract:**
```http
GET /api/warehouse/v1/reports/receiving-history?from=2026-01-11&to=2026-02-11&supplierId=1
Response:
{
  "summary": {
    "totalShipments": 45,
    "totalItemsReceived": 12500,
    "averageLeadTimeDays": 3.2
  },
  "shipments": [
    {
      "shipmentNumber": "ISH-0001",
      "supplierId": 1,
      "supplierName": "ACME Corp",
      "expectedDate": "2026-02-10",
      "receivedDate": "2026-02-11",
      "itemsCount": 2,
      "totalQty": 150,
      "operator": "Operator1",
      "status": "COMPLETED"
    }
  ]
}
```

### Acceptance Criteria

```gherkin
Scenario: Load receiving history report
  Given logged in as Manager
  When navigate to /warehouse/reports/receiving
  Then report loads with default filters (last 30 days, all suppliers)
  And summary cards show: Total Shipments, Total Items, Avg Lead Time
  And table shows shipments received in date range

Scenario: Filter by date range
  Given on Receiving History Report page
  When set From Date "2026-01-01" and To Date "2026-01-31"
  And click "Apply Filters"
  Then API called with from=2026-01-01&to=2026-01-31
  And table shows only shipments received in January
  And summary cards updated

Scenario: Filter by supplier
  Given on Receiving History Report page
  When select Supplier "ACME Corp"
  And click "Apply Filters"
  Then table shows only shipments from ACME Corp
  And summary cards updated (only ACME shipments)

Scenario: Export to CSV
  Given report loaded with 45 shipments
  When click "Export CSV"
  Then browser downloads receiving-history-2026-02-11.csv
  And CSV includes columns: Shipment Number, Supplier, Expected Date, Received Date, Items, Qty, Operator, Status
  And CSV contains all 45 rows (not paginated)

Scenario: Sort by column
  Given report loaded
  When click "Received Date" column header
  Then table sorted by Received Date descending (newest first)
  When click again
  Then table sorted by Received Date ascending (oldest first)
```

### Validation / Checks

**Local Testing:**
```bash
# Get dev token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Test receiving history API
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/receiving-history?from=2026-01-01&to=2026-02-11" | jq

# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/reports/receiving
# Test filters
# Test CSV export
# Test sorting
```

**Metrics:**
- `ui_reports_receiving_history_views_total` (counter)
- `ui_reports_csv_exports_total` (counter, labels: reportType=receiving)

**Logs:**
- INFO: "User {UserId} viewed receiving history report (from {From}, to {To}, supplier {SupplierId})"

### Definition of Done

- [ ] Blazor page created: `Pages/Reports/ReceivingHistory.razor`
- [ ] Service client: `ReportClient.GetReceivingHistoryAsync(from, to, supplierId)`
- [ ] Date range filter component
- [ ] Supplier dropdown filter
- [ ] Summary cards component
- [ ] Report table component (sortable, paginated)
- [ ] CSV export functionality
- [ ] Apply filters button logic
- [ ] Responsive design tested
- [ ] Manual test: load report, apply filters, export CSV
- [ ] Code review completed

---

## Task PRD-1534: Dispatch History Report UI

**Epic:** Reports
**Phase:** 1.5
**Sprint:** 3
**Estimate:** S (0.5 day)
**OwnerType:** UI
**Dependencies:** PRD-1508 (Dispatch APIs)
**SourceRefs:** Universe §1.Reports

### Context

- Similar to PRD-1533 but for outbound dispatch operations
- Business use case: daily dispatch volume tracking, carrier performance analysis
- Must show shipment dispatch details: carrier, tracking number, dispatch time, delivery status

### Scope

**In Scope:**
- Dispatch History Report page (`/warehouse/reports/dispatch`)
- Date range filter, carrier filter
- Summary cards (Total Shipments Dispatched, Total Orders, On-Time Delivery %)
- Detailed table (Shipment Number, Order Number, Customer, Carrier, Tracking Number, Dispatch Date, Delivery Date, Status)
- CSV export button

**Out of Scope:**
- Delivery confirmation tracking (external carrier integration, deferred)
- Performance SLA reports (deferred)
- Charts/graphs (deferred)

### Requirements

**Functional:**
1. Report page loads on `/warehouse/reports/dispatch`
2. Filters: Date Range (default = last 30 days), Carrier dropdown (optional)
3. Summary cards: Total Shipments, Total Orders, On-Time Delivery % (delivered by requested date)
4. Table columns: Shipment Number, Order Number, Customer, Carrier, Tracking Number (copyable), Dispatch Date, Delivery Date (if confirmed), Status (DISPATCHED, DELIVERED)
5. Sortable columns (Dispatch Date, Delivery Date)
6. Pagination (50 rows per page)
7. CSV export

**Non-Functional:**
1. Performance: Report loads < 3 seconds
2. Click tracking number → copy to clipboard
3. Link to order detail (Order Number clickable)

**UI Wireframe:**
```
Dispatch History Report:
┌─────────────────────────────────────────────────────────┐
│ Dispatch History Report                   [Export CSV]  │
│ Filters: [From: 2026-01-11] [To: 2026-02-11] [Carrier ▼: All]│
│          [Apply Filters]                                │
├─────────────────────────────────────────────────────────┤
│ Summary (Last 30 Days):                                 │
│ [Total Shipments] [Total Orders] [On-Time Delivery]    │
│       38               38              92.1%            │
├─────────────────────────────────────────────────────────┤
│ Shipment# │ Order  │ Customer │ Carrier │ Tracking │ Dispatch │ Delivered│ Status│
│ SHIP-0001│ SO-001│ ACME    │ FedEx  │ FDX123  │ 02-10   │ 02-12   │ ✓    │
│ SHIP-0002│ SO-002│ GlobalCo│ UPS    │ UPS456  │ 02-11   │ -       │ ⏳   │
│ ...                                                      │
└─────────────────────────────────────────────────────────┘
```

**API Contract:**
```http
GET /api/warehouse/v1/reports/dispatch-history?from=2026-01-11&to=2026-02-11&carrier=FedEx
Response:
{
  "summary": {
    "totalShipments": 38,
    "totalOrders": 38,
    "onTimeDeliveryPercent": 92.1
  },
  "shipments": [
    {
      "shipmentNumber": "SHIP-0001",
      "orderNumber": "SO-0001",
      "customerName": "ACME Corp",
      "carrier": "FedEx",
      "trackingNumber": "FDX123456789",
      "dispatchDate": "2026-02-10",
      "deliveryDate": "2026-02-12",
      "requestedDeliveryDate": "2026-02-12",
      "status": "DELIVERED"
    }
  ]
}
```

### Acceptance Criteria

```gherkin
Scenario: Load dispatch history report
  Given logged in as Manager
  When navigate to /warehouse/reports/dispatch
  Then report loads with default filters (last 30 days, all carriers)
  And summary cards show: Total Shipments, Total Orders, On-Time Delivery %
  And table shows dispatched shipments

Scenario: Filter by carrier
  Given on Dispatch History Report page
  When select Carrier "FedEx"
  And click "Apply Filters"
  Then table shows only FedEx shipments
  And summary cards updated (FedEx only)

Scenario: Copy tracking number
  Given report loaded with shipments
  When click tracking number "FDX123456789"
  Then tracking copied to clipboard
  And toast "Tracking number copied"

Scenario: Export to CSV
  Given report loaded with 38 shipments
  When click "Export CSV"
  Then browser downloads dispatch-history-2026-02-11.csv
  And CSV includes all columns
  And CSV contains all 38 rows

Scenario: Click order number
  Given report loaded
  When click Order Number "SO-0001"
  Then navigate to /warehouse/sales/orders/1
  And order detail page opens
```

### Validation / Checks

**Local Testing:**
```bash
# Get dev token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Test dispatch history API
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/reports/dispatch-history?from=2026-01-01&to=2026-02-11" | jq

# Start WebUI
dotnet run --project src/LKvitai.MES.WebUI

# Navigate to /warehouse/reports/dispatch
# Test filters, CSV export, tracking copy
```

**Metrics:**
- `ui_reports_dispatch_history_views_total` (counter)
- `ui_reports_csv_exports_total` (counter, labels: reportType=dispatch)

**Logs:**
- INFO: "User {UserId} viewed dispatch history report (from {From}, to {To}, carrier {Carrier})"

### Definition of Done

- [ ] Blazor page created: `Pages/Reports/DispatchHistory.razor`
- [ ] Service client: `ReportClient.GetDispatchHistoryAsync(from, to, carrier)`
- [ ] Date range and carrier filters
- [ ] Summary cards component
- [ ] Report table (sortable, paginated)
- [ ] Tracking number copy functionality
- [ ] Order number link (navigate to order detail)
- [ ] CSV export
- [ ] Responsive design tested
- [ ] Manual test: full report workflow
- [ ] Code review completed

---

## Task PRD-1535: Stock Allocation Validation

**Epic:** Validation
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1505 (SalesOrder APIs)
**SourceRefs:** codex-suspicions.md:108-113

### Context

- codex-suspicions.md identified gap: allocation handlers do not validate available stock before creating reservations
- Risk: orders allocated even when stock insufficient, causing 409 conflicts later during picking
- Need: query AvailableStock projection before transitioning order to ALLOCATED status
- Return 409 Conflict if requested qty exceeds (OnHandQty - ReservedQty)

### Scope

**In Scope:**
- Add available-stock validation to `SubmitSalesOrderHandler` (before allocation saga trigger)
- Add available-stock validation to `ApproveSalesOrderHandler` (Manager approval flow)
- Query AvailableStock projection per order line
- Calculation: AvailableQty = OnHandQty - ActiveReservations.Sum(ReservedQty)
- Return 409 Conflict with detailed error: "Item {ItemCode}: requested {Qty}, available {AvailableQty}"
- Order status → PENDING_STOCK if insufficient

**Out of Scope:**
- Real-time stock reservation locking (optimistic concurrency is sufficient for Phase 1.5)
- Multi-warehouse allocation (single warehouse only)
- Partial allocation (all-or-nothing for Phase 1.5)

### Requirements

**Functional:**
1. Before creating Reservation, query AvailableStock projection for each order line
2. For each line: calculate AvailableQty = AvailableStock[ItemId].OnHandQty - SUM(ActiveReservations[ItemId].ReservedQty)
3. If ANY line has RequestedQty > AvailableQty → return 409 Conflict, set order status PENDING_STOCK
4. If ALL lines have sufficient stock → proceed with allocation, create Reservation (SOFT lock), set order status ALLOCATED
5. Error response includes per-line details: ItemCode, RequestedQty, AvailableQty, Shortage

**Non-Functional:**
1. Performance: Validation adds < 50ms latency
2. Consistency: Use READ COMMITTED isolation (eventual consistency acceptable)
3. Logging: Log validation failures with details

**Implementation:**
```csharp
// In SubmitSalesOrderHandler or ApproveSalesOrderHandler
public async Task<Result> Handle(SubmitSalesOrder command)
{
    var order = await _orderRepo.GetByIdAsync(command.OrderId);

    // Validate available stock per line
    var stockValidation = await _stockValidator.ValidateAvailability(order.Lines);

    if (!stockValidation.IsValid)
    {
        order.Status = SalesOrderStatus.PENDING_STOCK;
        await _orderRepo.SaveAsync(order);

        return Result.Conflict(new {
            error = "Insufficient stock",
            details = stockValidation.Errors // [{ itemCode, requested, available, shortage }]
        });
    }

    // Proceed with allocation saga
    await _messageBus.Publish(new SalesOrderSubmitted { OrderId = order.Id });
    order.Status = SalesOrderStatus.ALLOCATED;
    await _orderRepo.SaveAsync(order);

    return Result.Success();
}

public class StockAvailabilityValidator
{
    public async Task<ValidationResult> ValidateAvailability(List<SalesOrderLine> lines)
    {
        var errors = new List<StockShortage>();

        foreach (var line in lines)
        {
            var availableStock = await _db.AvailableStock
                .Where(s => s.ItemId == line.ItemId)
                .SumAsync(s => s.OnHandQty);

            var reservedQty = await _db.ActiveReservations
                .Where(r => r.ItemId == line.ItemId)
                .SumAsync(r => r.ReservedQty);

            var availableQty = availableStock - reservedQty;

            if (line.Qty > availableQty)
            {
                errors.Add(new StockShortage {
                    ItemCode = line.ItemCode,
                    RequestedQty = line.Qty,
                    AvailableQty = availableQty,
                    Shortage = line.Qty - availableQty
                });
            }
        }

        return errors.Any()
            ? ValidationResult.Invalid(errors)
            : ValidationResult.Valid();
    }
}
```

### Acceptance Criteria

```gherkin
Scenario: Allocation succeeds with sufficient stock
  Given Item FG-0001 with OnHandQty = 100, ActiveReservations = 20 (available = 80)
  When submit SalesOrder with line (FG-0001, Qty 50)
  Then stock validation passes (50 <= 80)
  And Reservation created (SOFT lock, qty 50)
  And order status → ALLOCATED
  And response 200 OK

Scenario: Allocation fails with insufficient stock
  Given Item FG-0001 with OnHandQty = 100, ActiveReservations = 90 (available = 10)
  When submit SalesOrder with line (FG-0001, Qty 50)
  Then stock validation fails (50 > 10)
  And order status → PENDING_STOCK
  And response 409 Conflict:
    {
      "error": "Insufficient stock",
      "details": [
        { "itemCode": "FG-0001", "requested": 50, "available": 10, "shortage": 40 }
      ]
    }

Scenario: Multi-line order with partial shortage
  Given Item FG-0001 available = 80, Item FG-0002 available = 10
  When submit SalesOrder with lines (FG-0001 qty 50, FG-0002 qty 30)
  Then stock validation fails (FG-0002: 30 > 10)
  And order status → PENDING_STOCK
  And response 409 Conflict with both line details (FG-0001 OK, FG-0002 shortage 20)

Scenario: Validation logs shortage details
  Given stock validation fails
  Then log WARN: "Stock allocation failed for order {OrderNumber}: Item {ItemCode} shortage {Shortage}"
  And log includes correlation ID for traceability
```

### Validation / Checks

**Local Testing:**
```bash
# Get dev token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

# Create order with sufficient stock
curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/sales-orders \
  -H "Content-Type: application/json" \
  -d '{"commandId":"'$(uuidgen)'","customerId":1,"lines":[{"itemId":1,"qty":10}]}'
# Expected: 200 OK, status ALLOCATED

# Create order with insufficient stock
curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/sales-orders \
  -H "Content-Type: application/json" \
  -d '{"commandId":"'$(uuidgen)'","customerId":1,"lines":[{"itemId":1,"qty":9999}]}'
# Expected: 409 Conflict, status PENDING_STOCK

# Verify logs
# Expected: WARN log with shortage details
```

**Metrics:**
- `sales_order_allocation_failures_total` (counter, labels: reason=insufficient_stock)

**Logs:**
- WARN: "Stock allocation failed for order {OrderNumber}: {ItemCode} requested {Qty}, available {Available}, shortage {Shortage}"

### Definition of Done

- [ ] `StockAvailabilityValidator` service created
- [ ] `ValidateAvailability` method implemented (queries AvailableStock + ActiveReservations)
- [ ] Integration into `SubmitSalesOrderHandler`
- [ ] Integration into `ApproveSalesOrderHandler`
- [ ] 409 Conflict response with detailed error payload
- [ ] Order status → PENDING_STOCK on validation failure
- [ ] Unit tests: validation logic (sufficient stock, insufficient stock, multi-line)
- [ ] Integration tests: full allocation flow with stock checks
- [ ] Manual test: curl commands (sufficient/insufficient scenarios)
- [ ] Logging: WARN on validation failure
- [ ] Code review completed

---

## Task PRD-1536: Optimistic Locking for Sales Orders

**Epic:** Validation
**Phase:** 1.5
**Sprint:** 3
**Estimate:** S (0.5 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1505 (SalesOrder APIs)
**SourceRefs:** codex-suspicions.md:115-120

### Context

- codex-suspicions.md identified gap: SalesOrder entity lacks RowVersion concurrency token
- Risk: concurrent updates (e.g., two operators approve same order) can cause lost updates
- Need: Add `byte[] RowVersion` to SalesOrder EF mapping, generate migration, enforce optimistic concurrency

### Scope

**In Scope:**
- Add `[Timestamp]` attribute to `SalesOrder.RowVersion` property
- Generate EF Core migration to add `row_version` column (PostgreSQL `xmin` or custom timestamp)
- Update command handlers to catch `DbUpdateConcurrencyException`
- Return 409 Conflict with message "Order was modified by another user, please refresh and retry"

**Out of Scope:**
- Pessimistic locking (row-level locks, not needed for this use case)
- Retry logic in API (client handles 409 and retries after refresh)

### Requirements

**Functional:**
1. Add `public byte[] RowVersion { get; set; }` to `SalesOrder` entity
2. Decorate with `[Timestamp]` attribute (EF Core will manage automatically)
3. Generate migration: `ALTER TABLE sales_orders ADD COLUMN row_version BYTEA DEFAULT gen_random_bytes(8)`
4. On concurrent update: EF Core throws `DbUpdateConcurrencyException`
5. Catch exception in handler, return 409 Conflict: "Concurrency conflict: order modified by another user"

**Non-Functional:**
1. Zero data loss (existing orders get default RowVersion on migration)
2. Performance: No measurable impact (RowVersion checked automatically by EF Core)

**Implementation:**
```csharp
// SalesOrder entity
public class SalesOrder
{
    public int Id { get; set; }
    // ... other properties

    [Timestamp]
    public byte[] RowVersion { get; set; }
}

// Command handler
public async Task<Result> Handle(ApproveSalesOrder command)
{
    try
    {
        var order = await _orderRepo.GetByIdAsync(command.OrderId);
        order.Status = SalesOrderStatus.ALLOCATED;
        await _orderRepo.SaveAsync(order); // SaveChangesAsync throws if RowVersion mismatch

        return Result.Success();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        _logger.LogWarning(ex, "Concurrency conflict for order {OrderId}", command.OrderId);
        return Result.Conflict("Order was modified by another user, please refresh and retry");
    }
}
```

**Migration:**
```sql
-- PostgreSQL uses xmin system column for optimistic concurrency
-- Alternative: custom timestamp column
ALTER TABLE sales_orders ADD COLUMN row_version BYTEA DEFAULT gen_random_bytes(8);
```

### Acceptance Criteria

```gherkin
Scenario: Concurrent approval detected
  Given SalesOrder SO-0001 with RowVersion = v1
  When User A loads order (RowVersion v1)
  And User B loads order (RowVersion v1)
  And User A approves order (RowVersion v1 → v2)
  Then User A's update succeeds, RowVersion = v2
  When User B approves order (RowVersion v1, stale)
  Then DbUpdateConcurrencyException thrown
  And API returns 409 Conflict: "Order was modified by another user"
  And User B sees error message, must refresh page

Scenario: Normal update succeeds
  Given SalesOrder SO-0002 with RowVersion = v1
  When User A loads and approves order
  Then update succeeds, RowVersion = v2
  And no concurrency conflict

Scenario: Migration preserves existing data
  Given sales_orders table with 100 rows
  When migration applied
  Then row_version column added to all rows
  And all rows have non-null RowVersion
  And no data loss
```

### Validation / Checks

**Local Testing:**
```bash
# Generate migration
dotnet ef migrations add AddSalesOrderRowVersion --project src/LKvitai.MES.Infrastructure --startup-project src/LKvitai.MES.Api

# Review migration SQL
dotnet ef migrations script --project src/LKvitai.MES.Infrastructure --startup-project src/LKvitai.MES.Api

# Apply migration
dotnet ef database update --project src/LKvitai.MES.Infrastructure --startup-project src/LKvitai.MES.Api

# Verify schema
psql -d warehouse -c "\d sales_orders" | grep row_version

# Test concurrency conflict (use two terminal windows)
# Terminal 1: Create order, get ID
ORDER_ID=1

# Terminal 2: Approve order
curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/sales-orders/$ORDER_ID/approve

# Terminal 1: Approve same order (should fail with 409)
curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/sales-orders/$ORDER_ID/approve
# Expected: 409 Conflict
```

**Metrics:**
- `sales_order_concurrency_conflicts_total` (counter)

**Logs:**
- WARN: "Concurrency conflict for order {OrderId}: {ExceptionMessage}"

### Definition of Done

- [ ] `RowVersion` property added to `SalesOrder` entity
- [ ] `[Timestamp]` attribute applied
- [ ] Migration generated and reviewed
- [ ] Migration applied to dev database
- [ ] Schema verification (row_version column exists)
- [ ] Exception handling in all command handlers (Approve, Submit, Cancel, etc.)
- [ ] 409 Conflict response on `DbUpdateConcurrencyException`
- [ ] Unit tests: concurrency conflict scenario
- [ ] Manual test: simulate concurrent approval
- [ ] Code review completed

---

## Task PRD-1537: Barcode Lookup Enhancement

**Epic:** Validation
**Phase:** 1.5
**Sprint:** 3
**Estimate:** S (0.5 day)
**OwnerType:** Backend/API
**Dependencies:** PRD-1507 (Packing APIs)
**SourceRefs:** codex-suspicions.md:146-150

### Context

- codex-suspicions.md identified gap: packing barcode validation only checks Item.PrimaryBarcode
- Risk: alternate barcodes (from item_barcodes table) are rejected, causing false errors
- Need: Extend lookup to include all active barcodes per item (primary + alternates)

### Scope

**In Scope:**
- Update barcode lookup logic in `PackOrderHandler` to query `item_barcodes` table
- Match scanned barcode against Item.PrimaryBarcode OR ItemBarcode.BarcodeValue
- Return ItemId if any barcode matches
- Update packing validation logic

**Out of Scope:**
- Barcode generation (already handled by master data)
- Multi-barcode UI display (show alternate barcodes in packing UI, deferred)

### Requirements

**Functional:**
1. When packing order, operator scans item barcode
2. Lookup logic: query Item.PrimaryBarcode AND ItemBarcode.BarcodeValue WHERE ItemBarcode.IsActive = true
3. If match found → return ItemId, proceed with packing
4. If no match → return 404 "Barcode {Barcode} not found"
5. Log: which barcode was scanned (primary vs alternate)

**Non-Functional:**
1. Performance: Barcode lookup < 50ms
2. Caching: Cache barcode-to-ItemId mapping (refresh every 5 minutes)

**Implementation:**
```csharp
public class BarcodeService
{
    public async Task<int?> GetItemIdByBarcode(string barcode)
    {
        // Check primary barcode
        var itemByPrimary = await _db.Items
            .Where(i => i.PrimaryBarcode == barcode)
            .Select(i => i.Id)
            .FirstOrDefaultAsync();

        if (itemByPrimary.HasValue)
            return itemByPrimary;

        // Check alternate barcodes
        var itemByAlternate = await _db.ItemBarcodes
            .Where(b => b.BarcodeValue == barcode && b.IsActive)
            .Select(b => b.ItemId)
            .FirstOrDefaultAsync();

        return itemByAlternate;
    }
}

// In PackOrderHandler
var itemId = await _barcodeService.GetItemIdByBarcode(command.ScannedBarcode);
if (!itemId.HasValue)
{
    return Result.NotFound($"Barcode {command.ScannedBarcode} not found");
}

// Validate item is in order
if (!order.Lines.Any(l => l.ItemId == itemId.Value))
{
    return Result.BadRequest($"Item {itemId} not in order {order.OrderNumber}");
}
```

### Acceptance Criteria

```gherkin
Scenario: Scan primary barcode
  Given Item FG-0001 with PrimaryBarcode "123456"
  When operator scans "123456" during packing
  Then barcode lookup returns ItemId = 1
  And packing continues

Scenario: Scan alternate barcode
  Given Item FG-0001 with PrimaryBarcode "123456"
  And ItemBarcode entry: ItemId = 1, BarcodeValue = "ALT-001", IsActive = true
  When operator scans "ALT-001" during packing
  Then barcode lookup returns ItemId = 1
  And packing continues

Scenario: Scan inactive barcode
  Given ItemBarcode entry: ItemId = 1, BarcodeValue = "OLD-BARCODE", IsActive = false
  When operator scans "OLD-BARCODE"
  Then barcode lookup returns null
  And error: "Barcode OLD-BARCODE not found"

Scenario: Scan unknown barcode
  When operator scans "UNKNOWN-999"
  Then barcode lookup returns null
  And error: "Barcode UNKNOWN-999 not found"

Scenario: Log barcode type
  Given Item FG-0001 scanned with alternate barcode "ALT-001"
  Then log INFO: "Barcode ALT-001 (alternate) scanned for Item FG-0001 by {OperatorId}"
```

### Validation / Checks

**Local Testing:**
```bash
# Insert test data
psql -d warehouse -c "
  INSERT INTO items (id, code, name, primary_barcode) VALUES (99, 'TEST-ITEM', 'Test Item', '123456');
  INSERT INTO item_barcodes (item_id, barcode_value, is_active) VALUES (99, 'ALT-001', true);
  INSERT INTO item_barcodes (item_id, barcode_value, is_active) VALUES (99, 'OLD-BARCODE', false);
"

# Test barcode lookup API
curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/items/lookup-by-barcode?barcode=123456"
# Expected: { "itemId": 99, "itemCode": "TEST-ITEM", "barcodeType": "primary" }

curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/items/lookup-by-barcode?barcode=ALT-001"
# Expected: { "itemId": 99, "itemCode": "TEST-ITEM", "barcodeType": "alternate" }

curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/warehouse/v1/items/lookup-by-barcode?barcode=OLD-BARCODE"
# Expected: 404 Not Found

# Test packing with alternate barcode
# Create order with TEST-ITEM, pack with ALT-001 scan
# Expected: packing succeeds
```

**Metrics:**
- `barcode_lookups_total` (counter, labels: barcodeType=primary|alternate, result=success|notfound)

**Logs:**
- INFO: "Barcode {Barcode} ({BarcodeType}) scanned for Item {ItemCode} by {OperatorId}"

### Definition of Done

- [ ] `BarcodeService.GetItemIdByBarcode()` implemented
- [ ] Query logic includes Item.PrimaryBarcode + ItemBarcode.BarcodeValue
- [ ] IsActive filter applied to alternate barcodes
- [ ] Integration into `PackOrderHandler` and other barcode scan handlers
- [ ] Unit tests: primary barcode, alternate barcode, inactive barcode, unknown barcode
- [ ] Manual test: pack order with alternate barcode
- [ ] Logging: barcode type (primary/alternate)
- [ ] Code review completed

---

## Task PRD-1538: FedEx API Integration (Real)

**Epic:** Integration
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** Integration
**Dependencies:** PRD-1508 (Dispatch APIs)
**SourceRefs:** codex-suspicions.md:159-163

### Context

- codex-suspicions.md identified gap: FedExApiService is a stub (generates local tracking numbers, no external API call)
- Need: Real FedEx API integration for tracking number generation, label retrieval, shipment notification
- Use FedEx Ship API (REST, OAuth 2.0)

### Scope

**In Scope:**
- HttpClient-based FedEx API client
- OAuth 2.0 token acquisition (client_credentials grant)
- Create shipment endpoint: POST `/ship/v1/shipments`
- Retrieve label endpoint: GET `/ship/v1/shipments/{trackingNumber}/label`
- Configuration: FedEx account credentials in appsettings (ClientId, ClientSecret, AccountNumber)
- Error handling: HTTP failures, auth failures, API rate limits
- Retry logic: 3 retries on transient failures (503, timeout)

**Out of Scope:**
- Multi-carrier support (UPS, DHL, deferred)
- International shipping (customs forms, deferred)
- Real-time tracking updates (webhook integration, deferred)

### Requirements

**Functional:**
1. On dispatch, call FedEx Ship API to create shipment
2. Request payload: shipper address, recipient address, package dimensions/weight, service type (e.g., FEDEX_GROUND)
3. Response: tracking number, label URL (ZPL or PDF)
4. Store tracking number in Shipment entity
5. Retrieve label: download ZPL from label URL, return to client for printing
6. Error handling: if FedEx API fails → return 503 Service Unavailable, queue for retry

**Non-Functional:**
1. Security: ClientSecret stored in Azure Key Vault or environment variables (not appsettings.json)
2. Performance: API call < 3 seconds
3. Reliability: Retry on 503, timeout (3 attempts, exponential backoff)
4. Logging: Log all API requests/responses (correlation ID)

**Implementation:**
```csharp
public class FedExApiClient
{
    private readonly HttpClient _httpClient;
    private readonly FedExConfig _config;

    public async Task<FedExShipmentResponse> CreateShipment(FedExShipmentRequest request)
    {
        var token = await GetAccessToken();

        var response = await _httpClient.PostAsJsonAsync(
            "/ship/v1/shipments",
            request,
            headers: new { Authorization = $"Bearer {token}" }
        );

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FedExShipmentResponse>();
    }

    private async Task<string> GetAccessToken()
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/oauth/token",
            new {
                grant_type = "client_credentials",
                client_id = _config.ClientId,
                client_secret = _config.ClientSecret
            }
        );

        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        return tokenResponse.AccessToken;
    }
}

// appsettings.json (dev only, use Key Vault in production)
{
  "FedEx": {
    "ApiBaseUrl": "https://apis-sandbox.fedex.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "AccountNumber": "123456789"
  }
}
```

### Acceptance Criteria

```gherkin
Scenario: Create FedEx shipment
  Given Shipment SHIP-0001 ready for dispatch
  When call FedEx API to create shipment
  Then POST https://apis-sandbox.fedex.com/ship/v1/shipments
  And request includes shipper, recipient, package details
  And response 200 OK
  And tracking number returned: "FDX123456789"
  And label URL returned: "https://..."
  And tracking number stored in Shipment entity

Scenario: Retrieve label
  Given tracking number "FDX123456789"
  When call GET /ship/v1/shipments/FDX123456789/label
  Then response 200 OK
  And ZPL label returned
  And label sent to printer

Scenario: FedEx API failure with retry
  Given FedEx API returns 503 Service Unavailable
  When create shipment
  Then retry 3 times (exponential backoff: 1s, 2s, 4s)
  And if all retries fail → return 503 to client
  And log ERROR: "FedEx API unavailable after 3 retries"

Scenario: OAuth token acquisition
  Given no cached token
  When call create shipment
  Then acquire OAuth token first (POST /oauth/token)
  And use token in Authorization header
  And cache token for 1 hour (reuse for subsequent requests)
```

### Validation / Checks

**Local Testing:**
```bash
# Configure FedEx sandbox credentials in appsettings.Development.json
# (Get credentials from FedEx Developer Portal)

# Test OAuth token acquisition
curl -X POST https://apis-sandbox.fedex.com/oauth/token \
  -H "Content-Type: application/json" \
  -d '{"grant_type":"client_credentials","client_id":"YOUR_CLIENT_ID","client_secret":"YOUR_CLIENT_SECRET"}'

# Start API
dotnet run --project src/LKvitai.MES.Api

# Test dispatch with real FedEx integration
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}' | jq -r '.token')

curl -H "Authorization: Bearer $TOKEN" \
  -X POST http://localhost:5000/api/warehouse/v1/shipments/1/dispatch \
  -H "Content-Type: application/json" \
  -d '{"commandId":"'$(uuidgen)'","carrier":"FedEx","vehicleId":"VAN-123"}'

# Verify tracking number in response
# Verify FedEx API call logged
```

**Metrics:**
- `fedex_api_calls_total` (counter, labels: endpoint, statusCode)
- `fedex_api_duration_ms` (histogram)
- `fedex_api_failures_total` (counter, labels: endpoint, errorType)

**Logs:**
- INFO: "FedEx API call: POST /ship/v1/shipments, CorrelationId {CorrelationId}"
- INFO: "FedEx shipment created: TrackingNumber {TrackingNumber}"
- ERROR: "FedEx API failed: {StatusCode} {ErrorMessage}, retry {RetryCount}/3"

### Definition of Done

- [ ] `FedExApiClient` class created
- [ ] HttpClient configured with base URL
- [ ] OAuth 2.0 token acquisition implemented
- [ ] Create shipment endpoint integration
- [ ] Retrieve label endpoint integration
- [ ] Configuration: appsettings.json + environment variables
- [ ] Retry logic with exponential backoff (Polly library)
- [ ] Error handling (HTTP failures, auth failures)
- [ ] Unit tests: token acquisition, create shipment, retry logic
- [ ] Integration tests: real API call to FedEx sandbox
- [ ] Manual test: dispatch → verify tracking number from FedEx
- [ ] Logging: all API calls logged with correlation ID
- [ ] Code review completed

---

## Task PRD-1539: End-to-End Correlation Tracing

**Epic:** Observability
**Phase:** 1.5
**Sprint:** 3
**Estimate:** M (1 day)
**OwnerType:** Infra/DevOps
**Dependencies:** PRD-1503 (Telemetry/Logging Infrastructure)
**SourceRefs:** Universe §5.Observability

### Context

- PRD-1503 implemented basic telemetry (OpenTelemetry, structured logging)
- Need: End-to-end correlation ID flow through all layers (API → command handler → saga → event handler → projection → integrations)
- Use case: Trace sales order lifecycle from creation → allocation → picking → packing → dispatch → delivery
- Correlation ID must propagate through HTTP headers, MassTransit messages, database logs

### Scope

**In Scope:**
- Generate correlation ID at API entry (middleware)
- Propagate via HttpContext.TraceIdentifier
- Include in all log messages (Serilog enricher)
- Propagate via MassTransit message headers (CorrelationId)
- Propagate to external APIs (FedEx, Agnum) via HTTP headers
- Add correlation ID to all structured logs
- Add correlation ID to OpenTelemetry spans

**Out of Scope:**
- Distributed tracing UI (Jaeger/Zipkin, use logs for Phase 1.5)
- Trace visualization (deferred)
- Performance profiling (APM, deferred)

### Requirements

**Functional:**
1. API middleware generates correlation ID (GUID) on each request (or uses existing from `X-Correlation-ID` header)
2. Correlation ID stored in HttpContext.TraceIdentifier and AsyncLocal storage
3. Serilog enricher adds correlation ID to all log entries
4. MassTransit integration: set CorrelationId on all published messages
5. Event handlers: read CorrelationId from message context, include in logs
6. External API calls: add `X-Correlation-ID` header (FedEx, Agnum)
7. Database commands: include correlation ID in EF Core command logs (if possible)

**Non-Functional:**
1. Zero performance impact (correlation ID is lightweight GUID)
2. Consistent format: `correlation-{guid}` (e.g., `correlation-a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
3. Logs searchable by correlation ID (structured logging)

**Implementation:**
```csharp
// Middleware
public class CorrelationIdMiddleware
{
    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? $"correlation-{Guid.NewGuid()}";

        context.TraceIdentifier = correlationId;
        CorrelationContext.Current = correlationId; // AsyncLocal

        context.Response.Headers.Add("X-Correlation-ID", correlationId);

        using (logger.BeginScope(new { CorrelationId = correlationId }))
        {
            await _next(context);
        }
    }
}

// Serilog enricher
public class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var correlationId = CorrelationContext.Current;
        if (!string.IsNullOrEmpty(correlationId))
        {
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("CorrelationId", correlationId));
        }
    }
}

// MassTransit integration
public class CorrelationIdPublishFilter : IPublishFilter
{
    public async Task Send(PublishContext context, IPipe<PublishContext> next)
    {
        var correlationId = CorrelationContext.Current;
        if (!string.IsNullOrEmpty(correlationId))
        {
            context.CorrelationId = Guid.Parse(correlationId.Replace("correlation-", ""));
        }

        await next.Send(context);
    }
}

// External API calls
var response = await _httpClient.PostAsync(
    fedexApiUrl,
    content,
    headers: new { ["X-Correlation-ID"] = CorrelationContext.Current }
);
```

### Acceptance Criteria

```gherkin
Scenario: Correlation ID flows through API request
  Given client sends POST /api/warehouse/v1/sales-orders
  When API receives request
  Then correlation ID generated: "correlation-{guid}"
  And correlation ID added to response header: X-Correlation-ID
  And all logs include CorrelationId property

Scenario: Client-provided correlation ID used
  Given client sends request with header X-Correlation-ID: "correlation-abc123"
  When API receives request
  Then API uses "correlation-abc123" (not generated new)
  And all logs include CorrelationId: "correlation-abc123"

Scenario: Correlation ID propagates through saga
  Given sales order created with CorrelationId "correlation-xyz789"
  When allocation saga triggered
  Then saga logs include CorrelationId: "correlation-xyz789"
  And all event handlers include same CorrelationId
  And projection updates logged with same CorrelationId

Scenario: Correlation ID sent to external APIs
  Given dispatch shipment with CorrelationId "correlation-fed001"
  When call FedEx API
  Then HTTP request includes header X-Correlation-ID: "correlation-fed001"
  And FedEx API response logged with same CorrelationId

Scenario: Search logs by correlation ID
  Given sales order SO-0001 created with CorrelationId "correlation-search-test"
  When search logs: `jq '. | select(.CorrelationId == "correlation-search-test")'`
  Then all log entries returned: API request, command handler, saga, events, projections
  And full lifecycle traceable
```

### Validation / Checks

**Local Testing:**
```bash
# Start API
dotnet run --project src/LKvitai.MES.Api

# Send request with custom correlation ID
curl -H "Authorization: Bearer $TOKEN" \
  -H "X-Correlation-ID: correlation-test-123" \
  -X POST http://localhost:5000/api/warehouse/v1/sales-orders \
  -H "Content-Type: application/json" \
  -d '{"commandId":"'$(uuidgen)'","customerId":1,"lines":[{"itemId":1,"qty":10}]}'

# Verify response header includes correlation ID
# Expected: X-Correlation-ID: correlation-test-123

# Search logs
cat logs/app.log | jq '. | select(.CorrelationId == "correlation-test-123")'
# Expected: all log entries for this request (API, handler, saga, events)

# Verify MassTransit message headers
# Check RabbitMQ message properties: CorrelationId field
```

**Metrics:**
- N/A (observability feature, no business metrics)

**Logs:**
- All log entries MUST include CorrelationId property
- Example: `{"Timestamp":"2026-02-11T10:00:00Z","Level":"INFO","Message":"Sales order created","CorrelationId":"correlation-abc123","OrderNumber":"SO-0001"}`

### Definition of Done

- [ ] `CorrelationIdMiddleware` created and registered in Program.cs
- [ ] AsyncLocal `CorrelationContext` for cross-layer access
- [ ] Serilog enricher configured (adds CorrelationId to all logs)
- [ ] MassTransit publish/consume filters (propagate CorrelationId)
- [ ] External API client includes X-Correlation-ID header
- [ ] Response header includes X-Correlation-ID
- [ ] Manual test: trace full sales order lifecycle by correlation ID
- [ ] Manual test: search logs by correlation ID, verify all layers included
- [ ] Documentation: how to use correlation ID for troubleshooting
- [ ] Code review completed

---

## Task PRD-1540: Smoke E2E Integration Tests

**Epic:** Testing
**Phase:** 1.5
**Sprint:** 3
**Estimate:** L (2 days)
**OwnerType:** QA
**Dependencies:** All above (PRD-1521 through PRD-1539)
**SourceRefs:** Universe §5.Testing

### Context

- Sprint 3 delivers 20 tasks across UI, backend, validation, integration
- Need: End-to-end smoke tests to validate critical operator workflows
- Tests run against local API + database (integration test mode)
- Focus: Happy path scenarios (not exhaustive, just critical paths)

### Scope

**In Scope:**
- E2E test suite using xUnit + WebApplicationFactory (in-memory API)
- Smoke tests for 5 critical workflows:
  1. Inbound: Create shipment → Receive goods → QC pass → Putaway
  2. Sales Order: Create order → Allocate → Release → Pick → Pack → Dispatch
  3. Stock Movement: Create transfer → Approve → Execute
  4. Valuation: Adjust cost → Verify on-hand value
  5. Reports: Generate receiving history, dispatch history
- Test database: PostgreSQL test container (Testcontainers library)
- Assertions: Status transitions, projections updated, logs written

**Out of Scope:**
- Full regression test suite (deferred to Phase 2)
- Load testing (deferred)
- UI automation (Selenium/Playwright, deferred)

### Requirements

**Functional:**
1. Test setup: Spin up PostgreSQL test container, apply migrations, seed master data
2. Test teardown: Drop database, stop container
3. Each test: Complete workflow from start to finish (10-20 steps per workflow)
4. Assertions: Verify entity status, projection data, logs, events
5. Tests run in parallel (xUnit test collections)

**Non-Functional:**
1. Test execution time: < 5 minutes for full suite
2. Isolation: Each test uses separate database schema
3. Repeatability: Tests idempotent (can run multiple times)
4. CI-ready: Tests run in GitHub Actions / Azure DevOps pipeline

**Test Structure:**
```csharp
public class SalesOrderE2ETests : IClassFixture<WarehouseApiFactory>
{
    [Fact]
    public async Task FullSalesOrderWorkflow_CreateToDispatch_Success()
    {
        // Arrange: Seed customer, items, stock
        var customer = await CreateCustomer("ACME Corp");
        var item = await CreateItem("FG-0001", qty: 100);

        // Act 1: Create sales order
        var createOrderResponse = await _client.PostAsync("/api/warehouse/v1/sales-orders", new {
            customerId = customer.Id,
            lines = new[] { new { itemId = item.Id, qty = 50 } }
        });
        Assert.Equal(HttpStatusCode.OK, createOrderResponse.StatusCode);
        var order = await createOrderResponse.Content.ReadFromJsonAsync<SalesOrderResponse>();
        Assert.Equal(SalesOrderStatus.ALLOCATED, order.Status);

        // Act 2: Release to picking
        var releaseResponse = await _client.PostAsync($"/api/warehouse/v1/sales-orders/{order.Id}/release", null);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);

        // Act 3: Pick items
        var pickResponse = await _client.PostAsync("/api/warehouse/v1/picks/execute", new {
            orderId = order.Id,
            itemId = item.Id,
            huId = "HU-0001",
            qtyPicked = 50
        });
        Assert.Equal(HttpStatusCode.OK, pickResponse.StatusCode);

        // Act 4: Pack order
        var packResponse = await _client.PostAsync("/api/warehouse/v1/shipments/pack", new {
            orderId = order.Id,
            packagingType = "BOX"
        });
        Assert.Equal(HttpStatusCode.OK, packResponse.StatusCode);
        var shipment = await packResponse.Content.ReadFromJsonAsync<ShipmentResponse>();
        Assert.NotNull(shipment.TrackingNumber);

        // Act 5: Dispatch
        var dispatchResponse = await _client.PostAsync($"/api/warehouse/v1/shipments/{shipment.Id}/dispatch", new {
            carrier = "FedEx",
            vehicleId = "VAN-123"
        });
        Assert.Equal(HttpStatusCode.OK, dispatchResponse.StatusCode);

        // Assert: Verify final state
        var finalOrder = await _db.SalesOrders.FindAsync(order.Id);
        Assert.Equal(SalesOrderStatus.SHIPPED, finalOrder.Status);

        var finalShipment = await _db.Shipments.FindAsync(shipment.Id);
        Assert.Equal(ShipmentStatus.DISPATCHED, finalShipment.Status);

        // Assert: Verify projections
        var stock = await _db.AvailableStock.FirstAsync(s => s.ItemId == item.Id);
        Assert.Equal(50, stock.OnHandQty); // 100 - 50 picked

        // Assert: Verify logs
        var logs = _testLogger.GetLogs();
        Assert.Contains(logs, l => l.Message.Contains("Sales order created"));
        Assert.Contains(logs, l => l.Message.Contains("Shipment dispatched"));
    }
}
```

### Acceptance Criteria

```gherkin
Scenario: Inbound workflow E2E
  Given test database initialized with master data
  When create inbound shipment (POST /api/warehouse/v1/inbound-shipments)
  And receive goods (POST /api/warehouse/v1/inbound-shipments/{id}/receive-items)
  And QC pass (POST /api/warehouse/v1/qc/inspect)
  And putaway (POST /api/warehouse/v1/putaway/execute)
  Then shipment status = COMPLETED
  And stock visible in AvailableStock projection
  And logs include all workflow steps

Scenario: Sales order workflow E2E
  Given customer and stock seeded
  When create order → allocate → release → pick → pack → dispatch
  Then order status = SHIPPED
  And shipment status = DISPATCHED
  And stock reduced in AvailableStock
  And tracking number generated

Scenario: Stock movement workflow E2E
  Given stock at location A1
  When create transfer A1→A2 → approve → execute
  Then stock moved from A1 to A2
  And AvailableStock updated

Scenario: Valuation workflow E2E
  Given item with OnHandQty = 100, UnitCost = $10
  When adjust cost to $12
  Then OnHandValue = 100 × $12 = $1,200
  And projection updated

Scenario: Reports workflow E2E
  Given 5 shipments received, 3 dispatched
  When generate receiving history report
  Then report includes 5 shipments
  When generate dispatch history report
  Then report includes 3 shipments
```

### Validation / Checks

**Local Testing:**
```bash
# Run E2E tests
dotnet test src/LKvitai.MES.Tests.E2E --filter "Category=E2E"

# Expected output:
# Passed SalesOrderE2ETests.FullSalesOrderWorkflow_CreateToDispatch_Success [2.3s]
# Passed InboundE2ETests.FullInboundWorkflow_ReceiveToStock_Success [1.8s]
# Passed TransferE2ETests.FullTransferWorkflow_CreateToExecute_Success [1.2s]
# Passed ValuationE2ETests.FullValuationWorkflow_AdjustCost_Success [0.9s]
# Passed ReportsE2ETests.ReceivingAndDispatchReports_Success [1.1s]
# Total: 5 passed, 0 failed, 7.3s

# Run in CI pipeline
# .github/workflows/ci.yml
# - name: Run E2E Tests
#   run: dotnet test --filter "Category=E2E"
```

**Metrics:**
- N/A (test metrics tracked by CI system)

**Logs:**
- Test logger captures all logs during test execution
- Assert logs contain expected workflow events

### Definition of Done

- [ ] Test project created: `src/LKvitai.MES.Tests.E2E`
- [ ] Testcontainers configured (PostgreSQL)
- [ ] WebApplicationFactory configured (in-memory API)
- [ ] Test: Inbound workflow E2E
- [ ] Test: Sales order workflow E2E
- [ ] Test: Stock movement workflow E2E
- [ ] Test: Valuation workflow E2E
- [ ] Test: Reports workflow E2E
- [ ] All tests pass locally
- [ ] Tests run in parallel (xUnit test collections)
- [ ] Test execution time < 5 minutes
- [ ] CI pipeline configured (GitHub Actions or Azure DevOps)
- [ ] All tests pass in CI
- [ ] Documentation: how to run E2E tests locally
- [ ] Code review completed

---

## Sprint 3 Success Criteria

At the end of Sprint 3, the following must be true:

✅ **Auth Flow Fixed:** Operators can obtain dev token and execute all documented curl commands without 403 errors
✅ **Data Model Aligned:** All Guid→int inconsistencies resolved, FK constraints working
✅ **Inbound UI Complete:** Invoice entry + receiving + QC panels fully functional
✅ **Stock UI Complete:** Dashboard + movement/transfer UI operational
✅ **Sales Order UI Complete:** Create → Allocate → Release workflow usable via UI
✅ **Picking/Packing UI Enhanced:** Barcode scanning, validation, error handling polished
✅ **Reports Accessible:** Receiving and dispatch history reports available in UI
✅ **Validations Enforced:** Stock allocation checks, optimistic locking, barcode lookup working
✅ **Integration Ready:** FedEx API real integration (not stub)
✅ **Observability:** Correlation IDs flow through all requests, traceable in logs
✅ **E2E Tests:** Smoke tests validate critical operator workflows

---

## Notes for Implementation

1. **UI Technology:** All UI tasks target Blazor Server (`src/LKvitai.MES.WebUI`), NOT React
2. **API Auth:** Use dev token from PRD-1521 for all manual validation steps
3. **Data Types:** All master data FKs use `int`, event stream IDs use string-based identifiers
4. **Barcode Scanning:** Keyboard wedge pattern (focus trap + Enter key submit)
5. **Error Handling:** Every UI form must show validation errors inline + toast notifications
6. **Audit Trail:** All write operations log user, timestamp, correlation ID
7. **Responsive Design:** All UI pages must work on tablet viewport (768px+)
8. **Testing Priority:** UI manual testing > Integration tests > Unit tests (for this sprint)
9. **Documentation:** Update `docs/dev-auth-guide.md` with all new endpoints
10. **Deployment:** No production deployment until Sprint 4 hardening complete

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| UI development slower than backend | Sprint 3 incomplete | Parallelize: 1 dev on UI, 1 dev on backend/validation tasks |
| Auth fix breaks production security | Security vulnerability | Strict environment check (`IsDevelopment()`), code review mandatory |
| Data migration loses data | Data loss | Test migration on dev DB first, backup production before apply |
| Barcode scanner hardware unavailable | Cannot test scanning | Use keyboard input fallback, document keyboard shortcuts |
| FedEx API credentials missing | Integration blocked | Use FedEx test environment, fallback to manual tracking entry |

---

**End of Sprint 3 Task Pack**
