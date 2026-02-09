# Master Data API Contracts

## API Design Principles

### REST Conventions
- Base URL: `/api/warehouse/v1`
- HTTP methods: GET (read), POST (create), PUT (update/upsert), DELETE (soft delete)
- Response format: JSON
- Error format: RFC 7807 ProblemDetails with `traceId`

### Authentication
- All endpoints require Bearer token (JWT)
- User context available via `ICurrentUserService`
- Role-based authorization: WarehouseAdmin, WarehouseManager, Operator

### Pagination
- Query params: `pageNumber` (default 1), `pageSize` (default 50, max 500)
- Response: `{ items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }`

### Error Handling
- 400 Bad Request: Validation errors
- 401 Unauthorized: Missing or invalid token
- 403 Forbidden: Insufficient permissions
- 404 Not Found: Entity not found
- 409 Conflict: Uniqueness constraint violation
- 422 Unprocessable Entity: Business rule violation
- 500 Internal Server Error: Unhandled exceptions

**Standard Error Response** (RFC 7807):
```json
{
  "type": "https://api.warehouse.com/errors/validation-error",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00",
  "errors": {
    "InternalSKU": ["The InternalSKU field is required."],
    "BaseUoM": ["UoM 'PIECE' does not exist."]
  }
}
```

---

## Master Data CRUD APIs

### Items

#### **GET /api/warehouse/v1/items**

**Purpose**: List items with filtering and pagination

**Authorization**: WarehouseOperator, WarehouseManager, WarehouseAdmin

**Query Parameters**:
- `search` (string, optional): Search by SKU or Name (partial match, case-insensitive)
- `categoryId` (int, optional): Filter by category
- `status` (string, optional): Filter by status (Active/Discontinued/Obsolete)
- `requiresLotTracking` (bool, optional): Filter lot-tracked items
- `pageNumber` (int, default 1)
- `pageSize` (int, default 50, max 500)

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": 1,
      "internalSKU": "RM-0001",
      "name": "Steel Bolt M8",
      "description": "High-strength bolt",
      "categoryId": 3,
      "categoryName": "Fasteners",
      "baseUoM": "PCS",
      "weight": 0.015,
      "volume": 0.0001,
      "requiresLotTracking": false,
      "requiresQC": false,
      "status": "Active",
      "primaryBarcode": "8594156780187",
      "productConfigId": null,
      "createdAt": "2026-01-15T10:30:00Z",
      "updatedAt": null
    }
  ],
  "totalCount": 487,
  "pageNumber": 1,
  "pageSize": 50
}
```

---

#### **GET /api/warehouse/v1/items/{id}**

**Purpose**: Get item by ID

**Authorization**: WarehouseOperator, WarehouseManager, WarehouseAdmin

**Path Parameters**:
- `id` (int, required): Item ID

**Response** (200 OK):
```json
{
  "id": 1,
  "internalSKU": "RM-0001",
  "name": "Steel Bolt M8",
  "description": "High-strength bolt",
  "categoryId": 3,
  "categoryName": "Fasteners",
  "baseUoM": "PCS",
  "weight": 0.015,
  "volume": 0.0001,
  "requiresLotTracking": false,
  "requiresQC": false,
  "status": "Active",
  "primaryBarcode": "8594156780187",
  "productConfigId": null,
  "barcodes": [
    {"barcode": "8594156780187", "barcodeType": "EAN13", "isPrimary": true},
    {"barcode": "SUP-ABC-M8", "barcodeType": "Code128", "isPrimary": false}
  ],
  "uomConversions": [
    {"fromUoM": "PCS", "toUoM": "BOX", "factor": 0.083333, "roundingRule": "Up"}
  ],
  "createdAt": "2026-01-15T10:30:00Z",
  "updatedAt": null
}
```

**Response** (404 Not Found):
```json
{
  "type": "https://api.warehouse.com/errors/not-found",
  "title": "Item Not Found",
  "status": 404,
  "detail": "Item with ID 999 does not exist.",
  "traceId": "00-..."
}
```

---

#### **POST /api/warehouse/v1/items**

**Purpose**: Create new item

**Authorization**: WarehouseManager, WarehouseAdmin

**Request Body**:
```json
{
  "internalSKU": "RM-0002",  // Optional, auto-generated if blank
  "name": "Hex Nut M8",
  "description": "Stainless steel hex nut",
  "categoryId": 3,
  "baseUoM": "PCS",
  "weight": 0.010,
  "volume": 0.00005,
  "requiresLotTracking": false,
  "requiresQC": false,
  "status": "Active",
  "primaryBarcode": "8594156780194",
  "productConfigId": null
}
```

**Response** (201 Created):
```json
{
  "id": 2,
  "internalSKU": "RM-0002",
  "name": "Hex Nut M8",
  "createdAt": "2026-02-09T14:00:00Z"
}
```

**Response** (400 Bad Request):
```json
{
  "type": "https://api.warehouse.com/errors/validation-error",
  "title": "Validation Failed",
  "status": 400,
  "traceId": "00-...",
  "errors": {
    "BaseUoM": ["UoM 'PIECE' does not exist."]
  }
}
```

**Response** (409 Conflict):
```json
{
  "type": "https://api.warehouse.com/errors/conflict",
  "title": "Duplicate SKU",
  "status": 409,
  "detail": "Item with InternalSKU 'RM-0002' already exists.",
  "traceId": "00-..."
}
```

---

#### **PUT /api/warehouse/v1/items/{id}**

**Purpose**: Update existing item

**Authorization**: WarehouseManager, WarehouseAdmin

**Path Parameters**:
- `id` (int, required)

**Request Body**: Same as POST (InternalSKU immutable, ignored if provided)

**Response** (200 OK):
```json
{
  "id": 2,
  "internalSKU": "RM-0002",
  "name": "Hex Nut M8 (Updated)",
  "updatedAt": "2026-02-09T15:00:00Z"
}
```

---

#### **DELETE /api/warehouse/v1/items/{id}**

**Purpose**: Soft delete (set status to Obsolete)

**Authorization**: WarehouseAdmin

**Path Parameters**:
- `id` (int, required)

**Response** (204 No Content)

**Response** (422 Unprocessable Entity) - if item has active stock:
```json
{
  "type": "https://api.warehouse.com/errors/business-rule-violation",
  "title": "Cannot Delete Item",
  "status": 422,
  "detail": "Item RM-0002 has active stock (150 PCS). Cannot delete.",
  "traceId": "00-..."
}
```

---

### Suppliers

#### **GET /api/warehouse/v1/suppliers**

**Query Parameters**: `search`, `pageNumber`, `pageSize`

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": 1,
      "code": "SUP-001",
      "name": "ABC Fasteners Ltd",
      "contactInfo": "{\"email\":\"orders@abc.com\"}",
      "createdAt": "2026-01-10T09:00:00Z"
    }
  ],
  "totalCount": 42,
  "pageNumber": 1,
  "pageSize": 50
}
```

#### **POST /api/warehouse/v1/suppliers**

**Request Body**:
```json
{
  "code": "SUP-002",
  "name": "XYZ Hardware Co",
  "contactInfo": "{\"email\":\"sales@xyz.com\",\"phone\":\"+1234567890\"}"
}
```

**Response** (201 Created): Similar to Items

---

### Locations

#### **GET /api/warehouse/v1/locations**

**Query Parameters**: 
- `search`, `type`, `isVirtual`, `status`, `pageNumber`, `pageSize`

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": 1,
      "code": "RECEIVING",
      "barcode": "VIRTUAL-RCV",
      "type": "Zone",
      "parentLocationId": null,
      "parentLocationCode": null,
      "isVirtual": true,
      "maxWeight": null,
      "maxVolume": null,
      "status": "Active",
      "zoneType": null
    }
  ],
  "totalCount": 127,
  "pageNumber": 1,
  "pageSize": 50
}
```

#### **GET /api/warehouse/v1/locations/tree**

**Purpose**: Get hierarchical location tree

**Response** (200 OK):
```json
{
  "locations": [
    {
      "id": 10,
      "code": "WH01",
      "name": "Main Warehouse",
      "type": "Warehouse",
      "children": [
        {
          "id": 11,
          "code": "WH01-A",
          "name": "Zone A",
          "type": "Zone",
          "children": [
            {
              "id": 12,
              "code": "WH01-A-12",
              "name": "Aisle 12",
              "type": "Aisle",
              "children": []
            }
          ]
        }
      ]
    }
  ]
}
```

---

### Barcodes

#### **GET /api/warehouse/v1/items/{itemId}/barcodes**

**Purpose**: Get all barcodes for an item

**Response** (200 OK):
```json
{
  "itemId": 1,
  "barcodes": [
    {
      "id": 1,
      "barcode": "8594156780187",
      "barcodeType": "EAN13",
      "isPrimary": true
    },
    {
      "id": 2,
      "barcode": "SUP-ABC-M8",
      "barcodeType": "Code128",
      "isPrimary": false
    }
  ]
}
```

#### **POST /api/warehouse/v1/items/{itemId}/barcodes**

**Request Body**:
```json
{
  "barcode": "ALT-BARCODE-001",
  "barcodeType": "Code128",
  "isPrimary": false
}
```

**Response** (201 Created)

---

### Barcode Lookup

#### **GET /api/warehouse/v1/barcodes/lookup?code={barcode}**

**Purpose**: Resolve barcode to item (for scanning workflows)

**Query Parameters**:
- `code` (string, required): Barcode value

**Response** (200 OK):
```json
{
  "barcode": "8594156780187",
  "itemId": 1,
  "internalSKU": "RM-0001",
  "itemName": "Steel Bolt M8",
  "barcodeType": "EAN13",
  "isPrimary": true
}
```

**Response** (404 Not Found):
```json
{
  "type": "https://api.warehouse.com/errors/not-found",
  "title": "Barcode Not Found",
  "status": 404,
  "detail": "Barcode '8594156780999' does not exist in the system.",
  "traceId": "00-..."
}
```

---

## Import APIs

### Items Import

#### **POST /api/warehouse/v1/admin/import/items**

**Purpose**: Bulk import items from Excel/CSV

**Authorization**: WarehouseAdmin

**Request**:
- Content-Type: `multipart/form-data`
- Body: `file` (Excel .xlsx or CSV)
- Query Parameters:
  - `dryRun` (bool, default false): Validation only, no DB writes
  - `skipErrors` (bool, default false): Continue processing on row errors

**Response** (200 OK):
```json
{
  "totalRows": 500,
  "processedRows": 498,
  "insertedRows": 450,
  "updatedRows": 48,
  "skippedRows": 2,
  "errors": [
    {
      "row": 15,
      "column": "BaseUoM",
      "value": "PIECE",
      "message": "UoM 'PIECE' does not exist in UnitOfMeasures table."
    },
    {
      "row": 127,
      "column": "InternalSKU",
      "value": "RM-0050",
      "message": "Duplicate SKU 'RM-0050' (already exists in row 87)."
    }
  ],
  "warnings": [
    {
      "row": 203,
      "column": "Weight",
      "message": "Weight not provided, defaulting to null."
    }
  ],
  "importId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "completedAt": "2026-02-09T16:30:00Z"
}
```

**Response** (400 Bad Request) - file validation errors:
```json
{
  "type": "https://api.warehouse.com/errors/import-error",
  "title": "Import File Invalid",
  "status": 400,
  "detail": "File format is invalid. Expected columns: InternalSKU, Name, CategoryCode, BaseUoM. Found: SKU, ItemName.",
  "traceId": "00-..."
}
```

---

#### **GET /api/warehouse/v1/admin/import/items/template**

**Purpose**: Download Excel template for items import

**Response** (200 OK):
- Content-Type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- File: `Items_Import_Template.xlsx`

**Template Structure**:
```
Row 1 (Headers):
InternalSKU | Name | Description | CategoryCode | BaseUoM | Weight | Volume | RequiresLotTracking | RequiresQC | Status | PrimaryBarcode | ProductConfigId

Row 2 (Example):
RM-0001 | Steel Bolt M8 | High-strength bolt | FASTENERS | PCS | 0.015 | 0.0001 | FALSE | FALSE | Active | 8594156780187 | 

Row 3 (Empty for user data)
```

---

### Suppliers Import

#### **POST /api/warehouse/v1/admin/import/suppliers**

**Request/Response**: Similar to Items import

**Template Columns**:
```
Code | Name | ContactInfo
SUP-001 | ABC Fasteners Ltd | {"email":"orders@abc.com"}
```

---

### Supplier-Item Mappings Import

#### **POST /api/warehouse/v1/admin/import/supplier-items**

**Template Columns**:
```
SupplierCode | SupplierSKU | InternalSKU | LeadTimeDays | MinOrderQty | PricePerUnit
SUP-001 | ABC-M8-BOLT | RM-0001 | 7 | 1000 | 0.15
```

**Validation**:
- SupplierCode must exist in Suppliers
- InternalSKU must exist in Items
- (SupplierCode, SupplierSKU) uniqueness

---

### Barcodes Import

#### **POST /api/warehouse/v1/admin/import/barcodes**

**Template Columns**:
```
InternalSKU | Barcode | BarcodeType | IsPrimary
RM-0001 | 8594156780187 | EAN13 | TRUE
RM-0001 | SUP-ABC-M8 | Code128 | FALSE
```

**Post-Processing**: Ensure only 1 IsPrimary=TRUE per ItemId

---

### Locations Import

#### **POST /api/warehouse/v1/admin/import/locations**

**Special Handling**: Topological sort by ParentLocationCode (parents before children)

**Template Columns**:
```
Code | Barcode | Type | ParentLocationCode | IsVirtual | MaxWeight | MaxVolume | Status | ZoneType
WH01 | QR:WH01 | Warehouse |  | FALSE |  |  | Active | General
WH01-A | QR:WH01-A | Zone | WH01 | FALSE |  |  | Active | General
WH01-A-12 | QR:WH01-A-12 | Aisle | WH01-A | FALSE |  |  | Active | General
```

---

## Receiving APIs (Minimal Phase 1)

### Inbound Shipments

#### **GET /api/warehouse/v1/receiving/shipments**

**Query Parameters**: `supplierId`, `status`, `expectedDateFrom`, `expectedDateTo`, `pageNumber`, `pageSize`

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "referenceNumber": "PO-2024-001",
      "supplierId": 1,
      "supplierName": "ABC Fasteners Ltd",
      "type": "PurchaseOrder",
      "expectedDate": "2026-02-10",
      "status": "Draft",
      "totalLines": 5,
      "totalExpectedQty": 5000,
      "totalReceivedQty": 0,
      "createdAt": "2026-02-09T10:00:00Z"
    }
  ],
  "totalCount": 23,
  "pageNumber": 1,
  "pageSize": 50
}
```

---

#### **POST /api/warehouse/v1/receiving/shipments**

**Purpose**: Create inbound shipment

**Request Body**:
```json
{
  "referenceNumber": "PO-2024-001",
  "supplierId": 1,
  "type": "PurchaseOrder",
  "expectedDate": "2026-02-10",
  "lines": [
    {
      "itemId": 1,
      "expectedQty": 1000
    },
    {
      "itemId": 2,
      "expectedQty": 500
    }
  ]
}
```

**Response** (201 Created):
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "referenceNumber": "PO-2024-001",
  "status": "Draft",
  "createdAt": "2026-02-09T10:00:00Z"
}
```

---

#### **POST /api/warehouse/v1/receiving/shipments/{id}/receive**

**Purpose**: Receive goods (creates GoodsReceived event)

**Request Body**:
```json
{
  "lineId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
  "receivedQty": 1000,
  "lotNumber": "LOT-2026-02-001",  // If Item.RequiresLotTracking
  "productionDate": "2026-02-01",  // Optional
  "expiryDate": "2027-02-01",      // Optional
  "notes": "Partial receipt - rest coming tomorrow"
}
```

**Response** (200 OK):
```json
{
  "shipmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "lineId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
  "itemId": 1,
  "receivedQty": 1000,
  "lotId": "4fa85f64-5717-4562-b3fc-2c963f66afa8",
  "destinationLocationId": 1,  // RECEIVING or QC_HOLD
  "destinationLocationCode": "QC_HOLD",
  "eventId": "5fa85f64-5717-4562-b3fc-2c963f66afa9",
  "timestamp": "2026-02-09T14:30:00Z"
}
```

**Response** (422 Unprocessable Entity) - if lot required but not provided:
```json
{
  "type": "https://api.warehouse.com/errors/business-rule-violation",
  "title": "Lot Number Required",
  "status": 422,
  "detail": "Item 'RM-0001' requires lot tracking. LotNumber must be provided.",
  "traceId": "00-..."
}
```

---

### QC Actions

#### **POST /api/warehouse/v1/qc/pass**

**Purpose**: Pass QC (move from QC_HOLD to RECEIVING)

**Request Body**:
```json
{
  "itemId": 1,
  "lotId": "4fa85f64-5717-4562-b3fc-2c963f66afa8",
  "qty": 1000,
  "inspectorNotes": "Quality acceptable"
}
```

**Response** (200 OK): Event `QCPassed` created

---

#### **POST /api/warehouse/v1/qc/fail**

**Purpose**: Fail QC (move from QC_HOLD to QUARANTINE)

**Request Body**:
```json
{
  "itemId": 1,
  "lotId": "4fa85f64-5717-4562-b3fc-2c963f66afa8",
  "qty": 1000,
  "reasonCode": "DAMAGE",
  "inspectorNotes": "Water damage observed"
}
```

**Response** (200 OK): Event `QCFailed` created

---

## Putaway APIs (Minimal Phase 1)

#### **POST /api/warehouse/v1/putaway**

**Purpose**: Move stock from RECEIVING to storage location

**Request Body**:
```json
{
  "itemId": 1,
  "qty": 1000,
  "fromLocationId": 1,  // RECEIVING
  "toLocationId": 15,   // Storage location
  "lotId": "4fa85f64-5717-4562-b3fc-2c963f66afa8",  // Optional
  "notes": "Placed on Shelf WH01-A-12-03"
}
```

**Response** (200 OK):
```json
{
  "eventId": "6fa85f64-5717-4562-b3fc-2c963f66afaa",
  "itemId": 1,
  "qty": 1000,
  "fromLocationId": 1,
  "fromLocationCode": "RECEIVING",
  "toLocationId": 15,
  "toLocationCode": "WH01-A-12-03",
  "timestamp": "2026-02-09T15:00:00Z"
}
```

**Response** (422 Unprocessable Entity) - if insufficient stock:
```json
{
  "type": "https://api.warehouse.com/errors/insufficient-stock",
  "title": "Insufficient Stock",
  "status": 422,
  "detail": "Item RM-0001 at location RECEIVING has only 500 PCS available. Cannot move 1000 PCS.",
  "traceId": "00-..."
}
```

---

## Picking APIs (Minimal Phase 1)

#### **POST /api/warehouse/v1/picking/tasks**

**Purpose**: Create pick task (manual, Phase 1)

**Request Body**:
```json
{
  "orderId": "order-789",
  "itemId": 1,
  "qty": 50,
  "assignedToUserId": "operator-456"  // Optional
}
```

**Response** (201 Created):
```json
{
  "taskId": "7fa85f64-5717-4562-b3fc-2c963f66afab",
  "orderId": "order-789",
  "itemId": 1,
  "qty": 50,
  "status": "Pending",
  "createdAt": "2026-02-09T16:00:00Z"
}
```

---

#### **POST /api/warehouse/v1/picking/tasks/{id}/complete**

**Purpose**: Complete pick task

**Request Body**:
```json
{
  "fromLocationId": 15,
  "pickedQty": 50,
  "lotId": "4fa85f64-5717-4562-b3fc-2c963f66afa8",  // Optional
  "scannedBarcode": "8594156780187",  // For validation
  "notes": "Pick completed"
}
```

**Response** (200 OK):
```json
{
  "taskId": "7fa85f64-5717-4562-b3fc-2c963f66afab",
  "eventId": "8fa85f64-5717-4562-b3fc-2c963f66afac",
  "itemId": 1,
  "pickedQty": 50,
  "fromLocationId": 15,
  "toLocationId": 20,  // SHIPPING
  "status": "Completed",
  "timestamp": "2026-02-09T16:15:00Z"
}
```

**Response** (422 Unprocessable Entity) - if barcode mismatch:
```json
{
  "type": "https://api.warehouse.com/errors/barcode-mismatch",
  "title": "Barcode Mismatch",
  "status": 422,
  "detail": "Scanned barcode '8594156780999' does not match expected item RM-0001.",
  "traceId": "00-..."
}
```

---

## Adjustments APIs

#### **POST /api/warehouse/v1/adjustments**

**Purpose**: Manual stock adjustment

**Authorization**: WarehouseManager, WarehouseAdmin

**Request Body**:
```json
{
  "itemId": 1,
  "locationId": 15,
  "qtyDelta": -10,  // Negative for decrease, positive for increase
  "reasonCode": "DAMAGE",
  "notes": "Water damage from roof leak",
  "lotId": "4fa85f64-5717-4562-b3fc-2c963f66afa8"  // Optional
}
```

**Response** (200 OK):
```json
{
  "adjustmentId": "9fa85f64-5717-4562-b3fc-2c963f66afad",
  "eventId": "afa85f64-5717-4562-b3fc-2c963f66afae",
  "itemId": 1,
  "locationId": 15,
  "qtyDelta": -10,
  "reasonCode": "DAMAGE",
  "userId": "manager-001",
  "timestamp": "2026-02-09T17:00:00Z"
}
```

---

#### **GET /api/warehouse/v1/adjustments**

**Purpose**: List adjustment history

**Query Parameters**: `itemId`, `locationId`, `reasonCode`, `userId`, `dateFrom`, `dateTo`, `pageNumber`, `pageSize`

**Response** (200 OK):
```json
{
  "items": [
    {
      "adjustmentId": "9fa85f64-5717-4562-b3fc-2c963f66afad",
      "itemId": 1,
      "itemSKU": "RM-0001",
      "itemName": "Steel Bolt M8",
      "locationId": 15,
      "locationCode": "WH01-A-12-03",
      "qtyDelta": -10,
      "reasonCode": "DAMAGE",
      "notes": "Water damage from roof leak",
      "userId": "manager-001",
      "userName": "John Manager",
      "timestamp": "2026-02-09T17:00:00Z"
    }
  ],
  "totalCount": 78,
  "pageNumber": 1,
  "pageSize": 50
}
```

---

## Stock Visibility APIs (Projections)

#### **GET /api/warehouse/v1/stock/available**

**Purpose**: Query AvailableStock projection

**Query Parameters**:
- `itemId` (int, optional)
- `locationId` (int, optional)
- `categoryId` (int, optional)
- `includeReserved` (bool, default false): Show reserved qty
- `includeVirtualLocations` (bool, default false)
- `expiringBefore` (date, optional): Filter by lot expiry date
- `pageNumber`, `pageSize`

**Response** (200 OK):
```json
{
  "items": [
    {
      "itemId": 1,
      "internalSKU": "RM-0001",
      "itemName": "Steel Bolt M8",
      "locationId": 15,
      "locationCode": "WH01-A-12-03",
      "lotId": "4fa85f64-5717-4562-b3fc-2c963f66afa8",
      "lotNumber": "LOT-2026-02-001",
      "expiryDate": "2027-02-01",
      "qty": 990,
      "reservedQty": 50,
      "availableQty": 940,
      "baseUoM": "PCS",
      "lastUpdated": "2026-02-09T16:15:30Z"
    }
  ],
  "totalCount": 487,
  "pageNumber": 1,
  "pageSize": 50,
  "projectionTimestamp": "2026-02-09T16:15:32Z"
}
```

**Note**: `projectionTimestamp` indicates projection freshness (eventual consistency)

---

#### **GET /api/warehouse/v1/stock/location-balance**

**Purpose**: Query LocationBalance projection

**Response** (200 OK):
```json
{
  "items": [
    {
      "locationId": 15,
      "locationCode": "WH01-A-12-03",
      "itemCount": 3,
      "totalWeight": 45.5,  // kg
      "totalVolume": 0.35,  // m³
      "maxWeight": 500,
      "maxVolume": 2.0,
      "utilizationWeight": 0.091,  // 9.1%
      "utilizationVolume": 0.175,  // 17.5%
      "status": "Active"
    }
  ],
  "totalCount": 127,
  "pageNumber": 1,
  "pageSize": 50
}
```

---

## Health Check APIs

#### **GET /api/warehouse/v1/health**

**Purpose**: System health status

**Response** (200 OK):
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "eventStore": "Healthy",
    "projectionLag": "Healthy",  // Lag < 1 second
    "messageQueue": "Healthy"
  },
  "projectionStatus": {
    "availableStock": {
      "lastUpdated": "2026-02-09T16:20:00Z",
      "lagSeconds": 0.5,
      "status": "Healthy"
    },
    "locationBalance": {
      "lastUpdated": "2026-02-09T16:20:00Z",
      "lagSeconds": 0.5,
      "status": "Healthy"
    }
  }
}
```

**Response** (503 Service Unavailable) - if projection lag > 10 seconds:
```json
{
  "status": "Degraded",
  "checks": {
    "projectionLag": "Unhealthy"
  },
  "projectionStatus": {
    "availableStock": {
      "lastUpdated": "2026-02-09T16:10:00Z",
      "lagSeconds": 600,
      "status": "Unhealthy"
    }
  }
}
```

---

## Error Code Reference

| HTTP Status | Error Type | When to Use |
|-------------|-----------|-------------|
| 400 | `validation-error` | Request validation failed (missing fields, invalid formats) |
| 401 | `unauthorized` | Missing or invalid JWT token |
| 403 | `forbidden` | User lacks required role/permission |
| 404 | `not-found` | Entity (Item, Location, etc.) not found by ID |
| 409 | `conflict` | Uniqueness constraint violation (duplicate SKU, barcode) |
| 422 | `business-rule-violation` | Business logic prevents operation (e.g., lot required, insufficient stock) |
| 422 | `barcode-mismatch` | Scanned barcode doesn't match expected item |
| 422 | `insufficient-stock` | Not enough stock for pick/putaway |
| 500 | `internal-server-error` | Unhandled exceptions |
| 503 | `service-unavailable` | Projection lag excessive, message queue down |

---

## TraceId Propagation

**All responses** include `traceId` header for distributed tracing:
```
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json
X-Trace-Id: 00-4bf92f3577b34da6a3ce929d0e0e4736-00
```

**UI Error Handling**: Display `traceId` to user for support tickets.

**Logging**: All API calls log `traceId` for correlation.
