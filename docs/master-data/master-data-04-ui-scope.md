# Master Data UI Scope (Phase 1)

## UI Technology Stack

- **Framework**: Blazor Server (ASP.NET Core)
- **Styling**: Bootstrap 5
- **Icons**: Bootstrap Icons
- **Barcode Scanning**: Native input field + WebRTC (camera-based, optional)
- **Charts**: Chart.js (for dashboard)
- **Export**: CSV generation client-side (JavaScript)

---

## UI Design Principles

### 1. Reuse Existing Patterns

**From Current Implementation** (Dashboard, AvailableStock, Reservations):
- Pagination component: 50 items per page, "Previous/Next" buttons
- Filter bar: Collapsible panel with filter fields
- Data grid: Sortable columns, row actions dropdown
- CSV export: Button triggers client-side CSV generation
- Empty state: Centered message with icon + "Add First Item" button
- Loading state: Spinner overlay on grid

**Consistency Requirements**:
- All list pages use same pagination component
- All forms use same validation message pattern (red text below field)
- All modals use same size (large: 800px width, medium: 600px)

### 2. Scanning-First UX

**Barcode Input Pattern**:
```
┌─────────────────────────────────────┐
│ [Scan Barcode] 📷                   │  ← Auto-focus input
│ ▼ Enter barcode or click camera    │  ← Helper text
│ ┌─────────────────────────────┐    │
│ │ [Input Field]               │    │  ← Supports keyboard + scanner
│ └─────────────────────────────┘    │
│ □ Manual Entry (damaged label)     │  ← Checkbox reveals extra fields
└─────────────────────────────────────┘
```

**Behavior**:
- Auto-focus on input field when page loads
- Auto-submit on Enter key or scanner EOL character
- Validation: Look up barcode → show item info → confirm
- Manual entry: Checkbox reveals dropdown (select item by name/SKU)

### 3. Error Handling with TraceId

**Error Banner Pattern**:
```
┌──────────────────────────────────────────────────────┐
│ ⚠️ Error: Item RM-0001 not found                     │
│ Please verify the barcode and try again.            │
│ TraceId: 00-4bf92f3577b34da6a3ce929d0e0e4736-00     │  ← Copy button
│ [Dismiss]                                            │
└──────────────────────────────────────────────────────┘
```

**Toast Notification Pattern** (Success):
```
┌──────────────────────────────────┐
│ ✅ Pick completed successfully   │  ← Auto-dismiss after 3 seconds
└──────────────────────────────────┘
```

### 4. Empty States

**Pattern**:
```
┌──────────────────────────────────────────────────┐
│              📦                                   │
│                                                   │
│         No items found                            │
│                                                   │
│  Import your first items using the template      │
│                                                   │
│  [Download Template]  [Import Items]             │
└──────────────────────────────────────────────────┘
```

---

## Page Structure

### Navigation Menu (Left Sidebar)

```
Warehouse
├── 📊 Dashboard
├── 📦 Stock
│   ├── Available Stock
│   └── Reservations
├── 📥 Receiving
│   ├── Inbound Shipments
│   ├── Receive Goods
│   └── QC Panel
├── 📤 Putaway
│   └── Putaway Tasks
├── 🎯 Picking
│   ├── Pick Tasks
│   └── Pick History
├── ⚙️ Adjustments
│   ├── Create Adjustment
│   └── Adjustment History
├── 🔧 Admin
│   ├── Items
│   ├── Suppliers
│   ├── Locations
│   ├── Categories
│   └── Import Data
└── 📈 Reports
    ├── Stock Level
    ├── Receiving History
    └── Pick History
```

---

## Phase 1 Pages

### 1. Admin: Items Management

**URL**: `/admin/items`

**Access**: WarehouseManager, WarehouseAdmin

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Items Management                                     [Import]│
├─────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────┐    │
│ │ Search: [____________]  Category: [All ▼]  [Filter] │    │
│ │ Status: [Active ▼]  Lot Tracked: [All ▼]            │    │
│ └──────────────────────────────────────────────────────┘    │
│                                                              │
│ ┌────┬────────────┬─────────────┬──────────┬────────┐      │
│ │ ID │ SKU        │ Name        │ Category │ Status │ ⋮    │
│ ├────┼────────────┼─────────────┼──────────┼────────┤      │
│ │ 1  │ RM-0001    │ Steel Bolt  │ Fastener │ Active │ ⋮    │
│ │ 2  │ RM-0002    │ Hex Nut     │ Fastener │ Active │ ⋮    │
│ └────┴────────────┴─────────────┴──────────┴────────┘      │
│                                                              │
│ [1] 2 3 ... 10  [Next]             487 items  [Export CSV] │
└─────────────────────────────────────────────────────────────┘
```

**Actions Dropdown** (⋮):
- Edit → Modal form
- View Stock → Navigate to `/stock?itemId=X`
- View Details → Navigate to `/admin/items/{id}`
- Deactivate → Confirm dialog → Set Status=Discontinued

**Add Item Button** → Modal form:
```
┌──────────────────────────────────────┐
│ Add New Item                    [X]  │
├──────────────────────────────────────┤
│ Internal SKU: [________] (optional)  │
│ Name*: [___________________________] │
│ Description: [___________________]   │
│ Category*: [Select... ▼]            │
│ Base UoM*: [Select... ▼]            │
│ Weight (kg): [____]                  │
│ Volume (m³): [____]                  │
│ □ Requires Lot Tracking              │
│ □ Requires QC                        │
│ Status: [Active ▼]                   │
│ Primary Barcode: [__] 📷            │
│                                      │
│ [Cancel]              [Save Item]   │
└──────────────────────────────────────┘
```

**Validation**:
- Name: Required, max 200 chars
- Category: Required dropdown
- BaseUoM: Required dropdown
- Barcode: Validate uniqueness on blur (async)

---

### 2. Admin: Import Data

**URL**: `/admin/import`

**Access**: WarehouseAdmin

**Layout**: Tabbed interface
```
┌─────────────────────────────────────────────────────────────┐
│ Import Master Data                                           │
├─────────────────────────────────────────────────────────────┤
│ [Items] [Suppliers] [Mappings] [Barcodes] [Locations]      │
├─────────────────────────────────────────────────────────────┤
│ Import Items                                                 │
│                                                              │
│ Step 1: Download Template                                   │
│ [Download Items Template] (.xlsx)                           │
│                                                              │
│ Step 2: Upload File                                         │
│ ┌──────────────────────────────────────────┐               │
│ │ Drag & drop file here or click to browse│               │
│ │             📄                            │               │
│ └──────────────────────────────────────────┘               │
│                                                              │
│ Options:                                                     │
│ ☑ Dry Run (validation only)                                │
│ ☐ Skip Errors (continue on row errors)                     │
│                                                              │
│ [Import]                                                     │
│                                                              │
│ ─────────────────────────────────────────                  │
│ Import Results:                                              │
│ Total Rows: 500                                             │
│ ✅ Inserted: 450                                            │
│ 🔄 Updated: 48                                              │
│ ⚠️ Errors: 2                                                │
│                                                              │
│ Error Details:                                               │
│ Row 15: BaseUoM 'PIECE' does not exist                     │
│ Row 127: Duplicate SKU 'RM-0050'                           │
│                                                              │
│ [Download Error Report] [Commit Changes]                    │
└─────────────────────────────────────────────────────────────┘
```

**Flow**:
1. User downloads template (Excel)
2. User fills template offline
3. User uploads file
4. System validates (dry run by default)
5. Display validation results (errors table)
6. If no errors: [Commit Changes] button enabled
7. Commit → Insert/update records → Show success toast

---

### 3. Receiving: Inbound Shipments List

**URL**: `/receiving/shipments`

**Access**: WarehouseOperator, WarehouseManager

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Inbound Shipments                         [Create Shipment] │
├─────────────────────────────────────────────────────────────┤
│ Supplier: [All ▼]  Status: [All ▼]  Expected: [Date Range] │
│                                                              │
│ ┌──────┬────────┬──────────┬──────────┬────────┬────────┐  │
│ │ ID   │ Ref #  │ Supplier │ Expected │ Status │ Action │  │
│ ├──────┼────────┼──────────┼──────────┼────────┼────────┤  │
│ │ ...6 │ PO-001 │ ABC Ltd  │ Feb 10   │ Draft  │ [Go ▶] │  │
│ └──────┴────────┴──────────┴──────────┴────────┴────────┘  │
│                                                              │
│ [1] 2 3 ... 5  [Next]                 23 shipments         │
└─────────────────────────────────────────────────────────────┘
```

**[Go ▶] Button** → Navigate to `/receiving/shipments/{id}`

---

### 4. Receiving: Shipment Detail & Receive Goods

**URL**: `/receiving/shipments/{id}`

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Inbound Shipment: PO-2024-001                         Draft │
├─────────────────────────────────────────────────────────────┤
│ Supplier: ABC Fasteners Ltd                                 │
│ Expected Date: 2026-02-10                                   │
│ Reference: PO-2024-001                                      │
│                                                              │
│ Lines (3 / 5 received):                                     │
│ ┌──────┬───────────┬──────────┬──────────┬────────────┐    │
│ │ Item │ Expected  │ Received │ Status   │ Action     │    │
│ ├──────┼───────────┼──────────┼──────────┼────────────┤    │
│ │ RM-1 │ 1000 PCS  │ 1000     │ Complete │            │    │
│ │ RM-2 │  500 PCS  │  500     │ Complete │            │    │
│ │ RM-3 │  300 PCS  │    0     │ Pending  │ [Receive]  │    │
│ └──────┴───────────┴──────────┴──────────┴────────────┘    │
│                                                              │
│ [Complete Shipment] [Mark as Partial] [Cancel]             │
└─────────────────────────────────────────────────────────────┘
```

**[Receive] Button** → Modal:
```
┌────────────────────────────────────────┐
│ Receive Goods: RM-0003              [X]│
├────────────────────────────────────────┤
│ Item: Washer M8                        │
│ Expected Qty: 300 PCS                  │
│                                        │
│ Scan Barcode: [___________] 📷        │
│ □ Manual Entry                         │
│                                        │
│ Received Qty*: [____] PCS              │
│                                        │
│ ── Lot Tracking (if required) ──      │
│ Lot Number*: [___________]             │
│ Production Date: [Date Picker]         │
│ Expiry Date: [Date Picker]             │
│                                        │
│ Notes: [_______________________]       │
│                                        │
│ [Cancel]           [Confirm Receipt]  │
└────────────────────────────────────────┘
```

**Workflow**:
1. Click [Receive] → Modal opens
2. Scan barcode → Validate item matches expected
3. Enter received qty (prefill from expected)
4. If item RequiresLotTracking → show lot fields (required)
5. Submit → API creates GoodsReceived event
6. Modal closes → Line status updates to "Complete"
7. If item RequiresQC → Toast: "Sent to QC for inspection"

---

### 5. QC Panel

**URL**: `/receiving/qc`

**Access**: QCInspector, WarehouseManager

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Quality Control Panel                                       │
├─────────────────────────────────────────────────────────────┤
│ ┌────────┬─────────┬─────────┬──────────┬─────────────┐    │
│ │ Item   │ Qty     │ Lot     │ Received │ Action      │    │
│ ├────────┼─────────┼─────────┼──────────┼─────────────┤    │
│ │ RM-001 │ 1000    │ LOT-001 │ Feb 9    │ [Pass][Fail]│    │
│ │ RM-005 │ 500     │ LOT-002 │ Feb 9    │ [Pass][Fail]│    │
│ └────────┴─────────┴─────────┴──────────┴─────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

**[Pass] Button** → Confirm dialog:
```
Pass QC for RM-0001 (1000 PCS)?
Goods will be moved to RECEIVING location.

[Cancel]  [Confirm Pass]
```

**[Fail] Button** → Modal:
```
┌────────────────────────────────────┐
│ Fail QC: RM-0001               [X] │
├────────────────────────────────────┤
│ Qty: 1000 PCS (all)                │
│                                    │
│ Reason*: [Damage ▼]               │
│          - Damage                  │
│          - Wrong Item              │
│          - Expired                 │
│          - Other                   │
│                                    │
│ Notes*: [___________________]      │
│                                    │
│ Goods will be moved to QUARANTINE. │
│                                    │
│ [Cancel]          [Confirm Fail]  │
└────────────────────────────────────┘
```

---

### 6. Putaway Tasks

**URL**: `/putaway/tasks`

**Access**: WarehouseOperator, WarehouseManager

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Putaway Tasks                                               │
├─────────────────────────────────────────────────────────────┤
│ Items in RECEIVING location (12):                           │
│                                                              │
│ ┌────────┬──────────┬─────────┬──────────┬─────────────┐   │
│ │ Item   │ Qty      │ Lot     │ Received │ Action      │   │
│ ├────────┼──────────┼─────────┼──────────┼─────────────┤   │
│ │ RM-001 │ 1000 PCS │ LOT-001 │ 10:30 AM │ [Putaway]   │   │
│ │ RM-002 │  500 PCS │ LOT-002 │ 11:00 AM │ [Putaway]   │   │
│ └────────┴──────────┴─────────┴──────────┴─────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

**[Putaway] Button** → Modal:
```
┌──────────────────────────────────────────┐
│ Putaway: RM-0001 (1000 PCS)          [X] │
├──────────────────────────────────────────┤
│ From Location: RECEIVING (read-only)     │
│                                          │
│ To Location*:                            │
│ Scan Location: [___________] 📷          │
│ □ Manual Selection                       │
│                                          │
│ Or Select: [WH01-A-12-03 ▼]             │
│                                          │
│ ── Location Info ──                      │
│ Type: Bin                                │
│ Capacity: 300kg / 500kg max (60%)       │
│ Status: Active                           │
│                                          │
│ Qty to Putaway*: [1000] PCS              │
│                                          │
│ [Cancel]            [Confirm Putaway]   │
└──────────────────────────────────────────┘
```

**Validation**:
- Location barcode scan → Lookup → Display location info
- Check capacity: Warning if exceeds max (not blocking in Phase 1)
- Submit → API creates StockMoved event → Success toast

---

### 7. Pick Tasks List

**URL**: `/picking/tasks`

**Access**: WarehouseOperator, WarehouseManager

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Pick Tasks                             [Create Manual Task] │
├─────────────────────────────────────────────────────────────┤
│ Assigned To: [Me ▼]  Status: [Pending ▼]                   │
│                                                              │
│ ┌────────┬──────────┬────────┬────────┬──────────────────┐ │
│ │ Task # │ Order    │ Item   │ Qty    │ Status    │Action│ │
│ ├────────┼──────────┼────────┼────────┼──────────────────┤ │
│ │ ...ab  │ ORD-789  │ RM-001 │ 50 PCS │ Pending   │[Pick]│ │
│ │ ...ac  │ ORD-790  │ RM-002 │ 30 PCS │ Pending   │[Pick]│ │
│ └────────┴──────────┴────────┴────────┴──────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

**[Pick] Button** → Navigate to `/picking/execute/{taskId}`

---

### 8. Pick Execution

**URL**: `/picking/execute/{taskId}`

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Pick Task: ...ab                                     [Back] │
├─────────────────────────────────────────────────────────────┤
│ Order: ORD-789                                              │
│ Item: RM-0001 - Steel Bolt M8                              │
│ Qty to Pick: 50 PCS                                         │
│                                                              │
│ ── Step 1: Select Location ──                              │
│ Available Stock Locations (3):                              │
│ ┌──────────────┬────────┬─────────┬───────────────────┐    │
│ │ Location     │ Qty    │ Lot     │ Expiry    │Select│    │
│ ├──────────────┼────────┼─────────┼───────────────────┤    │
│ │ WH01-A-12-03 │ 990    │ LOT-001 │ Feb 2027  │ [→]  │    │
│ │ WH01-B-05-01 │ 200    │ LOT-002 │ Jan 2027  │ [→]  │    │
│ └──────────────┴────────┴─────────┴───────────────────┘    │
│                                                              │
│ ── Step 2: Scan & Confirm ──                               │
│ Location: WH01-A-12-03 ✓                                    │
│                                                              │
│ Scan Location Barcode: [___________] 📷                     │
│ Scan Item Barcode: [___________] 📷                         │
│ □ Manual Entry (damaged label)                              │
│                                                              │
│ Picked Qty*: [50] PCS                                       │
│                                                              │
│ [Cancel]                      [Confirm Pick]               │
└─────────────────────────────────────────────────────────────┘
```

**Workflow**:
1. Click [→] to select location → Expands Step 2
2. Scan location barcode → Validate matches selected location
3. Scan item barcode → Validate matches expected item
4. Enter picked qty (prefill from task qty)
5. Submit → API creates PickCompleted event
6. Success toast → Navigate back to task list

**Validation**:
- Location barcode must match selected location
- Item barcode must match expected item
- Picked qty must be <= available qty at location

---

### 9. Adjustments

**URL**: `/adjustments`

**Access**: WarehouseManager, WarehouseAdmin

**Layout**: Two tabs
```
┌─────────────────────────────────────────────────────────────┐
│ Stock Adjustments                                           │
├─────────────────────────────────────────────────────────────┤
│ [Create Adjustment] [Adjustment History]                    │
├─────────────────────────────────────────────────────────────┤
│ Create Adjustment                                           │
│                                                              │
│ Item*: [Search item... ▼]                                  │
│                                                              │
│ Location*: [Select location... ▼]                          │
│                                                              │
│ Current Qty: 990 PCS (read-only)                           │
│                                                              │
│ Adjustment Qty*: [____]                                     │
│ (Positive to increase, negative to decrease)               │
│                                                              │
│ New Qty: 990 PCS (calculated)                              │
│                                                              │
│ Reason*: [Damage ▼]                                        │
│                                                              │
│ Notes: [_________________________________]                  │
│                                                              │
│ [Cancel]                        [Submit Adjustment]        │
└─────────────────────────────────────────────────────────────┘
```

**Confirmation Dialog** (after Submit):
```
Adjust Stock?

Item: RM-0001 - Steel Bolt M8
Location: WH01-A-12-03
Change: -10 PCS (990 → 980)
Reason: Damage

This action cannot be undone.

[Cancel]  [Confirm]
```

---

### 10. Adjustment History

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Adjustment History                           [Export CSV]   │
├─────────────────────────────────────────────────────────────┤
│ Item: [All ▼]  Reason: [All ▼]  Date: [Last 30 Days ▼]    │
│                                                              │
│ ┌──────┬────────┬──────────┬──────┬────────┬──────────┐    │
│ │ Date │ Item   │ Location │ Δ    │ Reason │ User     │    │
│ ├──────┼────────┼──────────┼──────┼────────┼──────────┤    │
│ │ 5PM  │ RM-001 │ WH01-A   │ -10  │ Damage │ Manager  │    │
│ │ 3PM  │ RM-002 │ WH01-B   │ +50  │ Invent │ Manager  │    │
│ └──────┴────────┴──────────┴──────┴────────┴──────────┘    │
│                                                              │
│ [1] 2 3 ... 8  [Next]                 78 records           │
└─────────────────────────────────────────────────────────────┘
```

**Click Row** → Modal with full details (notes, traceId)

---

### 11. Reports: Stock Level

**URL**: `/reports/stock-level`

**Layout**:
```
┌─────────────────────────────────────────────────────────────┐
│ Stock Level Report                          [Export CSV]    │
├─────────────────────────────────────────────────────────────┤
│ Category: [All ▼]  Location: [All ▼]                       │
│ □ Include Zero Stock  □ Include Reserved                    │
│ Expiring Before: [Date Picker]  [Apply Filters]            │
│                                                              │
│ ┌──────┬────────────┬──────────┬──────┬──────┬──────────┐  │
│ │ SKU  │ Name       │ Location │ Qty  │ Res  │ Avail    │  │
│ ├──────┼────────────┼──────────┼──────┼──────┼──────────┤  │
│ │ RM-1 │ Steel Bolt │ WH01-A   │ 990  │  50  │  940     │  │
│ │ RM-1 │ Steel Bolt │ WH01-B   │ 200  │   0  │  200     │  │
│ └──────┴────────────┴──────────┴──────┴──────┴──────────┘  │
│                                                              │
│ Total Records: 487                                          │
│ Projection Timestamp: 16:15:32 (refresh ↻)                 │
└─────────────────────────────────────────────────────────────┘
```

**Projection Timestamp**: Show data freshness, refresh button polls API

---

## Common UI Components

### 1. Pagination Component

**Props**: `currentPage`, `totalPages`, `onPageChange`

**Markup**:
```html
<div class="pagination">
  <button disabled={currentPage === 1} @onclick="PreviousPage">Previous</button>
  <span>Page @currentPage of @totalPages</span>
  <button disabled={currentPage === totalPages} @onclick="NextPage">Next</button>
</div>
```

### 2. Filter Bar Component

**Props**: `filters`, `onFilterChange`

**Collapsible**: Click "Filters ▼" to expand/collapse

### 3. Barcode Scanner Component

**Props**: `onScan`, `itemId` (optional, for validation)

**Features**:
- Auto-focus input field
- Auto-submit on Enter key
- Camera button (WebRTC, optional)
- Manual entry checkbox

### 4. Empty State Component

**Props**: `message`, `icon`, `actionButton`

**Markup**:
```html
<div class="empty-state">
  <i class="bi bi-@icon"></i>
  <h4>@message</h4>
  <button @onclick="actionButton.OnClick">@actionButton.Text</button>
</div>
```

### 5. Error Banner Component

**Props**: `errorMessage`, `traceId`, `onDismiss`

**Markup**:
```html
<div class="alert alert-danger alert-dismissible">
  <i class="bi bi-exclamation-triangle"></i>
  <strong>Error:</strong> @errorMessage
  <br>
  <small>TraceId: @traceId <button @onclick="CopyTraceId">📋 Copy</button></small>
  <button class="btn-close" @onclick="onDismiss"></button>
</div>
```

### 6. Toast Notification Component

**Props**: `message`, `type` (success/error/warning), `autoDismiss`

**Markup**:
```html
<div class="toast show" role="alert">
  <div class="toast-header">
    <i class="bi bi-@icon"></i>
    <strong>@title</strong>
  </div>
  <div class="toast-body">@message</div>
</div>
```

---

## UX Rules

### Scanning Workflow

1. **Auto-focus**: Input field receives focus on page load
2. **Auto-submit**: Enter key or EOL character submits
3. **Validation**: Immediate feedback (green checkmark or red X)
4. **Fallback**: Manual entry checkbox reveals item/location dropdown

### Loading States

- **Grid Loading**: Spinner overlay with "Loading items..."
- **Button Loading**: Disable button + spinner icon "Processing..."
- **Page Loading**: Full-page spinner on navigation

### Validation Feedback

- **Field-level**: Red border + error text below field
- **Form-level**: Error banner at top of form
- **Success**: Green checkmark icon + success toast

### Confirmation Dialogs

- **Destructive actions**: Delete, cancel, fail QC → Confirm dialog
- **High-impact actions**: Stock adjustment > 100 qty → Confirm dialog
- **Irreversible actions**: Always show "This cannot be undone" warning

### Projection Staleness

- Display projection timestamp: "Stock as of 16:15:32"
- Refresh button: Polls API, shows spinner during refresh
- If projection lag > 10 seconds: Warning banner "Stock data may be delayed"

---

## Accessibility

- All form fields have labels (not just placeholders)
- Buttons have aria-labels
- Keyboard navigation: Tab order logical, Enter submits forms
- Screen reader support: aria-live regions for dynamic content (toast notifications)

---

## Mobile Considerations (Phase 1: Tablet Only)

- Minimum viewport: 768px (tablet portrait)
- Touch targets: 44x44px minimum
- Barcode scanner: Use device camera (WebRTC)
- Pinch-to-zoom disabled on input fields (prevent accidental zoom)

---

## Performance Targets

- Page load: <2 seconds (including data fetch)
- Data grid render: <500ms for 50 rows
- Filter apply: <1 second (includes API call)
- Export CSV: <3 seconds for 10k rows (client-side generation)

---

## Browser Support

- Chrome 90+ (primary)
- Edge 90+
- Firefox 88+
- Safari 14+ (limited testing)

---

## Summary

Phase 1 UI consists of **11 core pages** with **6 reusable components**. The design prioritizes **scanning-first workflows**, **traceId error handling**, and **projection staleness indicators**. All pages reuse existing patterns (pagination, filters, CSV export) from the current implementation (Dashboard, AvailableStock, Reservations).
