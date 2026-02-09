# Master Data Domain Model (EF Core)

## Entity Definitions

### Item (Nomenclature)

**Purpose**: Product and material master records

```
Table: Items

Primary Key: Id (int, auto-increment)

Columns:
- Id (int, PK, identity)
- InternalSKU (varchar(50), not null)
- Name (varchar(200), not null)
- Description (text, nullable)
- CategoryId (int, not null, FK → ItemCategories.Id)
- BaseUoM (varchar(10), not null, FK → UnitOfMeasures.Code)
- Weight (decimal(18,3), nullable) -- kg
- Volume (decimal(18,3), nullable) -- m³
- RequiresLotTracking (bit, not null, default 0)
- RequiresQC (bit, not null, default 0)
- Status (varchar(20), not null, default 'Active')
- PrimaryBarcode (varchar(100), nullable)
- ProductConfigId (varchar(50), nullable) -- FK to Product Config module
- CreatedAt (datetimeoffset, not null, default SYSUTCDATETIME())
- UpdatedAt (datetimeoffset, nullable)
- CreatedBy (varchar(100), nullable)
- UpdatedBy (varchar(100), nullable)

Constraints:
- UK_Items_InternalSKU UNIQUE (InternalSKU)
- FK_Items_Category FOREIGN KEY (CategoryId) REFERENCES ItemCategories(Id)
- FK_Items_BaseUoM FOREIGN KEY (BaseUoM) REFERENCES UnitOfMeasures(Code)
- CK_Items_Status CHECK (Status IN ('Active', 'Discontinued', 'Obsolete'))
- CK_Items_Weight CHECK (Weight IS NULL OR Weight > 0)
- CK_Items_Volume CHECK (Volume IS NULL OR Volume > 0)

Indexes:
- PK_Items CLUSTERED (Id)
- UK_Items_InternalSKU UNIQUE NONCLUSTERED (InternalSKU)
- IX_Items_CategoryId NONCLUSTERED (CategoryId)
- IX_Items_BaseUoM NONCLUSTERED (BaseUoM)
- IX_Items_Status NONCLUSTERED (Status)
```

**InternalSKU Generation Rule**:
- If provided on import: validate uniqueness
- If blank on import: auto-generate as `{Prefix}-{Sequence}`
  - Prefix rules:
    - `RM` = Raw Materials (CategoryId in raw materials tree)
    - `FG` = Finished Goods (CategoryId in finished goods tree)
    - Default = `ITEM`
  - Sequence: 4-digit zero-padded (e.g., `0001`)
  - Example: `RM-0001`, `FG-0042`

---

### ItemCategory

**Purpose**: Hierarchical classification for items

```
Table: ItemCategories

Primary Key: Id (int, auto-increment)

Columns:
- Id (int, PK, identity)
- Code (varchar(50), not null)
- Name (varchar(200), not null)
- ParentCategoryId (int, nullable, FK → ItemCategories.Id)

Constraints:
- UK_ItemCategories_Code UNIQUE (Code)
- FK_ItemCategories_Parent FOREIGN KEY (ParentCategoryId) REFERENCES ItemCategories(Id)

Indexes:
- PK_ItemCategories CLUSTERED (Id)
- UK_ItemCategories_Code UNIQUE NONCLUSTERED (Code)
- IX_ItemCategories_Parent NONCLUSTERED (ParentCategoryId)
```

**Seed Data**:
```
Code: RAW, Name: Raw Materials, Parent: NULL
Code: FINISHED, Name: Finished Goods, Parent: NULL
Code: FASTENERS, Name: Fasteners, Parent: RAW
Code: CHEMICALS, Name: Chemicals, Parent: RAW
```

---

### UnitOfMeasure

**Purpose**: Measurement units for quantities

```
Table: UnitOfMeasures

Primary Key: Code (varchar(10))

Columns:
- Code (varchar(10), PK)
- Name (varchar(50), not null)
- Type (varchar(20), not null)

Constraints:
- PK_UnitOfMeasures PRIMARY KEY (Code)
- CK_UoM_Type CHECK (Type IN ('Weight', 'Volume', 'Piece', 'Length'))

Indexes:
- PK_UnitOfMeasures CLUSTERED (Code)
```

**Seed Data**:
```
Code: KG,   Name: Kilogram,    Type: Weight
Code: G,    Name: Gram,        Type: Weight
Code: L,    Name: Liter,       Type: Volume
Code: ML,   Name: Milliliter,  Type: Volume
Code: PCS,  Name: Pieces,      Type: Piece
Code: M,    Name: Meter,       Type: Length
Code: BOX,  Name: Box,         Type: Piece
Code: PKG,  Name: Package,     Type: Piece
```

---

### ItemUoMConversion

**Purpose**: Unit conversions per item

```
Table: ItemUoMConversions

Primary Key: Id (int, auto-increment)

Columns:
- Id (int, PK, identity)
- ItemId (int, not null, FK → Items.Id)
- FromUoM (varchar(10), not null, FK → UnitOfMeasures.Code)
- ToUoM (varchar(10), not null, FK → UnitOfMeasures.Code)
- Factor (decimal(18,6), not null)
- RoundingRule (varchar(20), not null, default 'Up')

Constraints:
- UK_Conversion UNIQUE (ItemId, FromUoM, ToUoM)
- FK_Conversion_Item FOREIGN KEY (ItemId) REFERENCES Items(Id)
- FK_Conversion_FromUoM FOREIGN KEY (FromUoM) REFERENCES UnitOfMeasures(Code)
- FK_Conversion_ToUoM FOREIGN KEY (ToUoM) REFERENCES UnitOfMeasures(Code)
- CK_Conversion_Factor CHECK (Factor > 0)
- CK_Conversion_Rule CHECK (RoundingRule IN ('Up', 'Down', 'Nearest'))

Indexes:
- PK_Conversion CLUSTERED (Id)
- UK_Conversion UNIQUE NONCLUSTERED (ItemId, FromUoM, ToUoM)
```

**Conversion Formula**: `ToUoM_Quantity = FromUoM_Quantity * Factor`

**Example**:
```
ItemId=1, FromUoM=KG, ToUoM=G, Factor=1000, RoundingRule=Nearest
  → 1.5 KG = 1500 G
ItemId=2, FromUoM=BOX, ToUoM=PCS, Factor=12, RoundingRule=Up
  → 1.3 BOX = 16 PCS (rounded up)
```

---

### ItemBarcode

**Purpose**: Multiple barcodes per item (supplier barcodes, packaging barcodes)

```
Table: ItemBarcodes

Primary Key: Id (int, auto-increment)

Columns:
- Id (int, PK, identity)
- ItemId (int, not null, FK → Items.Id)
- Barcode (varchar(100), not null)
- BarcodeType (varchar(20), not null)
- IsPrimary (bit, not null, default 0)

Constraints:
- UK_Barcode UNIQUE (Barcode) -- Global uniqueness across all items
- FK_Barcode_Item FOREIGN KEY (ItemId) REFERENCES Items(Id) ON DELETE CASCADE
- CK_Barcode_Type CHECK (BarcodeType IN ('EAN13', 'Code128', 'QR', 'UPC', 'Other'))

Indexes:
- PK_ItemBarcodes CLUSTERED (Id)
- UK_Barcode UNIQUE NONCLUSTERED (Barcode)
- IX_ItemBarcodes_Item NONCLUSTERED (ItemId)
- IX_ItemBarcodes_Primary NONCLUSTERED (IsPrimary) WHERE IsPrimary = 1
```

**Business Rule**: Only one IsPrimary=TRUE per ItemId (enforced post-import)

---

### Supplier

**Purpose**: Vendor master records

```
Table: Suppliers

Primary Key: Id (int, auto-increment)

Columns:
- Id (int, PK, identity)
- Code (varchar(50), not null)
- Name (varchar(200), not null)
- ContactInfo (text, nullable) -- JSON or plain text
- CreatedAt (datetimeoffset, not null, default SYSUTCDATETIME())
- CreatedBy (varchar(100), nullable)

Constraints:
- UK_Suppliers_Code UNIQUE (Code)

Indexes:
- PK_Suppliers CLUSTERED (Id)
- UK_Suppliers_Code UNIQUE NONCLUSTERED (Code)
```

---

### SupplierItemMapping

**Purpose**: Supplier-specific SKUs, lead times, pricing

```
Table: SupplierItemMappings

Primary Key: Id (int, auto-increment)

Columns:
- Id (int, PK, identity)
- SupplierId (int, not null, FK → Suppliers.Id)
- SupplierSKU (varchar(100), not null)
- ItemId (int, not null, FK → Items.Id)
- LeadTimeDays (int, nullable)
- MinOrderQty (decimal(18,3), nullable)
- PricePerUnit (decimal(18,2), nullable)

Constraints:
- UK_SupplierItem UNIQUE (SupplierId, SupplierSKU)
- FK_Mapping_Supplier FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id)
- FK_Mapping_Item FOREIGN KEY (ItemId) REFERENCES Items(Id)
- CK_Mapping_LeadTime CHECK (LeadTimeDays IS NULL OR LeadTimeDays >= 0)
- CK_Mapping_MOQ CHECK (MinOrderQty IS NULL OR MinOrderQty > 0)
- CK_Mapping_Price CHECK (PricePerUnit IS NULL OR PricePerUnit > 0)

Indexes:
- PK_Mappings CLUSTERED (Id)
- UK_SupplierItem UNIQUE NONCLUSTERED (SupplierId, SupplierSKU)
- IX_Mappings_Item NONCLUSTERED (ItemId)
```

---

### Location

**Purpose**: Physical and virtual storage locations

```
Table: Locations

Primary Key: Id (int, auto-increment)

Columns:
- Id (int, PK, identity)
- Code (varchar(50), not null)
- Barcode (varchar(100), not null)
- Type (varchar(20), not null)
- ParentLocationId (int, nullable, FK → Locations.Id)
- IsVirtual (bit, not null, default 0)
- MaxWeight (decimal(18,3), nullable) -- kg
- MaxVolume (decimal(18,3), nullable) -- m³
- Status (varchar(20), not null, default 'Active')
- ZoneType (varchar(20), nullable)
- CreatedAt (datetimeoffset, not null, default SYSUTCDATETIME())

Constraints:
- UK_Locations_Code UNIQUE (Code)
- UK_Locations_Barcode UNIQUE (Barcode)
- FK_Locations_Parent FOREIGN KEY (ParentLocationId) REFERENCES Locations(Id)
- CK_Locations_Type CHECK (Type IN ('Warehouse', 'Zone', 'Aisle', 'Rack', 'Shelf', 'Bin'))
- CK_Locations_Status CHECK (Status IN ('Active', 'Blocked', 'Maintenance'))
- CK_Locations_ZoneType CHECK (ZoneType IS NULL OR ZoneType IN ('General', 'Refrigerated', 'Hazmat', 'Quarantine'))
- CK_Locations_MaxWeight CHECK (MaxWeight IS NULL OR MaxWeight > 0)
- CK_Locations_MaxVolume CHECK (MaxVolume IS NULL OR MaxVolume > 0)

Indexes:
- PK_Locations CLUSTERED (Id)
- UK_Locations_Code UNIQUE NONCLUSTERED (Code)
- UK_Locations_Barcode UNIQUE NONCLUSTERED (Barcode)
- IX_Locations_Parent NONCLUSTERED (ParentLocationId)
- IX_Locations_Type NONCLUSTERED (Type)
- IX_Locations_Virtual NONCLUSTERED (IsVirtual)
- IX_Locations_Status NONCLUSTERED (Status)
```

**Virtual Locations Seed Data**:
```
Code: RECEIVING,           Barcode: VIRTUAL-RCV,   Type: Zone, IsVirtual: 1
Code: QC_HOLD,             Barcode: VIRTUAL-QC,    Type: Zone, IsVirtual: 1
Code: QUARANTINE,          Barcode: VIRTUAL-QTN,   Type: Zone, IsVirtual: 1
Code: PRODUCTION,          Barcode: VIRTUAL-PROD,  Type: Zone, IsVirtual: 1
Code: SHIPPING,            Barcode: VIRTUAL-SHIP,  Type: Zone, IsVirtual: 1
Code: SCRAP,               Barcode: VIRTUAL-SCRAP, Type: Zone, IsVirtual: 1
Code: RETURN_TO_SUPPLIER,  Barcode: VIRTUAL-RTS,   Type: Zone, IsVirtual: 1
```

**Barcode Format Convention**:
- Virtual: `VIRTUAL-{ABBR}`
- Physical: QR code with pattern `WH{WarehouseId}-{ZoneCode}-{BinCode}`
- Example: `WH01-A-12-03` for Warehouse 1, Zone A, Bin 12-03

---

### HandlingUnitType

**Purpose**: HU type definitions (pallet, box, bag)

```
Table: HandlingUnitTypes

Primary Key: Code (varchar(20))

Columns:
- Code (varchar(20), PK)
- Name (varchar(100), not null)
- MaxWeight (decimal(18,3), nullable) -- kg
- MaxVolume (decimal(18,3), nullable) -- m³

Constraints:
- PK_HUTypes PRIMARY KEY (Code)

Indexes:
- PK_HUTypes CLUSTERED (Code)
```

**Seed Data**:
```
Code: PALLET, Name: Standard Pallet,  MaxWeight: 1000, MaxVolume: 2.0
Code: BOX,    Name: Cardboard Box,    MaxWeight: 50,   MaxVolume: 0.1
Code: BAG,    Name: Bag,              MaxWeight: 25,   MaxVolume: 0.05
```

---

### HandlingUnit

**Purpose**: Physical handling units (Phase 1: flat, homogeneous)

```
Table: HandlingUnits

Primary Key: Id (uniqueidentifier)

Columns:
- Id (uniqueidentifier, PK, default NEWID())
- Barcode (varchar(100), not null)
- TypeCode (varchar(20), not null, FK → HandlingUnitTypes.Code)
- ItemId (int, not null, FK → Items.Id) -- Homogeneous only in Phase 1
- Qty (decimal(18,3), not null)
- LocationId (int, nullable, FK → Locations.Id)
- Status (varchar(20), not null, default 'Active')
- ParentHUId (uniqueidentifier, nullable) -- Reserved for Phase 2, unused
- CreatedAt (datetimeoffset, not null, default SYSUTCDATETIME())

Constraints:
- UK_HU_Barcode UNIQUE (Barcode)
- FK_HU_Type FOREIGN KEY (TypeCode) REFERENCES HandlingUnitTypes(Code)
- FK_HU_Item FOREIGN KEY (ItemId) REFERENCES Items(Id)
- FK_HU_Location FOREIGN KEY (LocationId) REFERENCES Locations(Id)
- FK_HU_Parent FOREIGN KEY (ParentHUId) REFERENCES HandlingUnits(Id) -- Phase 2
- CK_HU_Qty CHECK (Qty > 0)
- CK_HU_Status CHECK (Status IN ('Active', 'InTransit', 'Damaged', 'Scrapped'))

Indexes:
- PK_HU CLUSTERED (Id)
- UK_HU_Barcode UNIQUE NONCLUSTERED (Barcode)
- IX_HU_Item NONCLUSTERED (ItemId)
- IX_HU_Location NONCLUSTERED (LocationId)
- IX_HU_Status NONCLUSTERED (Status)
```

**Phase 1 Limitation**: ParentHUId always NULL (no nested HUs)

---

### Lot

**Purpose**: Batch tracking for lot-controlled items

```
Table: Lots

Primary Key: Id (uniqueidentifier)

Columns:
- Id (uniqueidentifier, PK, default NEWID())
- ItemId (int, not null, FK → Items.Id)
- LotNumber (varchar(100), not null)
- ProductionDate (date, nullable)
- ExpiryDate (date, nullable)
- SupplierId (int, nullable, FK → Suppliers.Id)
- CreatedAt (datetimeoffset, not null, default SYSUTCDATETIME())

Constraints:
- UK_Lot_ItemLotNumber UNIQUE (ItemId, LotNumber)
- FK_Lot_Item FOREIGN KEY (ItemId) REFERENCES Items(Id)
- FK_Lot_Supplier FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id)

Indexes:
- PK_Lots CLUSTERED (Id)
- UK_Lot_ItemLotNumber UNIQUE NONCLUSTERED (ItemId, LotNumber)
- IX_Lots_ExpiryDate NONCLUSTERED (ExpiryDate) -- For FEFO queries
```

---

### InboundShipment

**Purpose**: Expected receipts (ASN-like)

```
Table: InboundShipments

Primary Key: Id (uniqueidentifier)

Columns:
- Id (uniqueidentifier, PK, default NEWID())
- ReferenceNumber (varchar(100), nullable) -- PO number or ASN
- SupplierId (int, nullable, FK → Suppliers.Id)
- Type (varchar(20), not null)
- ExpectedDate (date, not null)
- Status (varchar(20), not null, default 'Draft')
- CreatedAt (datetimeoffset, not null, default SYSUTCDATETIME())
- CreatedBy (varchar(100), nullable)

Constraints:
- FK_Shipment_Supplier FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id)
- CK_Shipment_Type CHECK (Type IN ('PurchaseOrder', 'Transfer', 'CustomerReturn', 'Sample'))
- CK_Shipment_Status CHECK (Status IN ('Draft', 'Partial', 'Complete', 'Cancelled'))

Indexes:
- PK_Shipments CLUSTERED (Id)
- IX_Shipments_Supplier NONCLUSTERED (SupplierId)
- IX_Shipments_Status NONCLUSTERED (Status)
- IX_Shipments_ExpectedDate NONCLUSTERED (ExpectedDate)
```

---

### InboundShipmentLine

**Purpose**: Line items for inbound shipments

```
Table: InboundShipmentLines

Primary Key: Id (uniqueidentifier)

Columns:
- Id (uniqueidentifier, PK, default NEWID())
- ShipmentId (uniqueidentifier, not null, FK → InboundShipments.Id)
- ItemId (int, not null, FK → Items.Id)
- ExpectedQty (decimal(18,3), not null)
- ReceivedQty (decimal(18,3), not null, default 0)

Constraints:
- FK_Line_Shipment FOREIGN KEY (ShipmentId) REFERENCES InboundShipments(Id) ON DELETE CASCADE
- FK_Line_Item FOREIGN KEY (ItemId) REFERENCES Items(Id)
- CK_Line_ExpectedQty CHECK (ExpectedQty > 0)
- CK_Line_ReceivedQty CHECK (ReceivedQty >= 0)

Indexes:
- PK_Lines CLUSTERED (Id)
- IX_Lines_Shipment NONCLUSTERED (ShipmentId)
- IX_Lines_Item NONCLUSTERED (ItemId)
```

**Note**: ReceivedQty is a denormalized read model, updated by Marten projection from GoodsReceived events.

---

### AdjustmentReasonCode

**Purpose**: Predefined reasons for stock adjustments

```
Table: AdjustmentReasonCodes

Primary Key: Code (varchar(50))

Columns:
- Code (varchar(50), PK)
- Name (varchar(200), not null)
- RequiresApproval (bit, not null, default 0) -- Phase 2 feature, unused in Phase 1

Constraints:
- PK_ReasonCodes PRIMARY KEY (Code)

Indexes:
- PK_ReasonCodes CLUSTERED (Code)
```

**Seed Data**:
```
Code: DAMAGE,        Name: Physical Damage
Code: THEFT,         Name: Theft/Loss
Code: EVAPORATION,   Name: Evaporation/Shrinkage
Code: INVENTORY,     Name: Inventory Count Adjustment
Code: SYSTEM_ERROR,  Name: System Correction
Code: EXPIRED,       Name: Expiry Date Passed
Code: QC_REJECTED,   Name: Quality Control Rejection
Code: PRODUCTION_SCRAP, Name: Production Scrap
```

---

## Reserved Entities (Phase 2)

### SerialNumber (Table Created, Unused)

**Purpose**: Individual serial tracking for high-value items

```
Table: SerialNumbers (Phase 2)

Columns:
- Id (uniqueidentifier, PK)
- ItemId (int, not null, FK → Items.Id)
- SerialNo (varchar(100), not null, unique)
- LotId (uniqueidentifier, nullable, FK → Lots.Id)
- Status (varchar(20), not null) -- Available/Reserved/Shipped/Scrapped
- LocationId (int, nullable, FK → Locations.Id)
- CreatedAt (datetimeoffset)

Constraints:
- UK_SerialNo UNIQUE (SerialNo)
```

**Phase 1 Action**: Create table structure, no UI or business logic.

---

## Seed Data Summary

### Must-Have Seed Data (Run on Initial Deployment)

**1. UnitOfMeasures** (8 records)
```sql
INSERT INTO UnitOfMeasures (Code, Name, Type) VALUES
('KG', 'Kilogram', 'Weight'),
('G', 'Gram', 'Weight'),
('L', 'Liter', 'Volume'),
('ML', 'Milliliter', 'Volume'),
('PCS', 'Pieces', 'Piece'),
('M', 'Meter', 'Length'),
('BOX', 'Box', 'Piece'),
('PKG', 'Package', 'Piece');
```

**2. Virtual Locations** (7 records)
```sql
INSERT INTO Locations (Code, Barcode, Type, IsVirtual, Status) VALUES
('RECEIVING', 'VIRTUAL-RCV', 'Zone', 1, 'Active'),
('QC_HOLD', 'VIRTUAL-QC', 'Zone', 1, 'Active'),
('QUARANTINE', 'VIRTUAL-QTN', 'Zone', 1, 'Active'),
('PRODUCTION', 'VIRTUAL-PROD', 'Zone', 1, 'Active'),
('SHIPPING', 'VIRTUAL-SHIP', 'Zone', 1, 'Active'),
('SCRAP', 'VIRTUAL-SCRAP', 'Zone', 1, 'Active'),
('RETURN_TO_SUPPLIER', 'VIRTUAL-RTS', 'Zone', 1, 'Active');
```

**3. HandlingUnitTypes** (3 records)
```sql
INSERT INTO HandlingUnitTypes (Code, Name, MaxWeight, MaxVolume) VALUES
('PALLET', 'Standard Pallet', 1000, 2.0),
('BOX', 'Cardboard Box', 50, 0.1),
('BAG', 'Bag', 25, 0.05);
```

**4. AdjustmentReasonCodes** (8 records)
```sql
INSERT INTO AdjustmentReasonCodes (Code, Name, RequiresApproval) VALUES
('DAMAGE', 'Physical Damage', 0),
('THEFT', 'Theft/Loss', 0),
('EVAPORATION', 'Evaporation/Shrinkage', 0),
('INVENTORY', 'Inventory Count Adjustment', 0),
('SYSTEM_ERROR', 'System Correction', 0),
('EXPIRED', 'Expiry Date Passed', 0),
('QC_REJECTED', 'Quality Control Rejection', 0),
('PRODUCTION_SCRAP', 'Production Scrap', 0);
```

**5. ItemCategories** (4 records, example)
```sql
INSERT INTO ItemCategories (Code, Name, ParentCategoryId) VALUES
('RAW', 'Raw Materials', NULL),
('FINISHED', 'Finished Goods', NULL),
('FASTENERS', 'Fasteners', (SELECT Id FROM ItemCategories WHERE Code = 'RAW')),
('CHEMICALS', 'Chemicals', (SELECT Id FROM ItemCategories WHERE Code = 'RAW'));
```

---

## Identity and Upsert Rules

### InternalSKU Generation

**Auto-Generation Logic**:
1. Determine prefix:
   - If `Item.CategoryId` is in "RAW" tree → prefix = `RM`
   - If `Item.CategoryId` is in "FINISHED" tree → prefix = `FG`
   - Else → prefix = `ITEM`
2. Get next sequence for prefix from `SKUSequences` table (or app-level counter)
3. Format: `{Prefix}-{Sequence:D4}`
4. Example: `RM-0001`, `FG-0042`, `ITEM-0003`

**Import Upsert Logic**:
- If InternalSKU provided → check uniqueness → use as-is
- If InternalSKU blank → auto-generate → assign
- On update: InternalSKU never changes (immutable after creation)

### Import Identity Matching

**Items**: Match by `InternalSKU`  
**Suppliers**: Match by `Code`  
**SupplierItemMappings**: Match by `(SupplierId, SupplierSKU)`  
**ItemBarcodes**: Match by `Barcode` (global uniqueness)  
**Locations**: Match by `Code`  

### Upsert Behavior

**Insert**: Entity does not exist (identity key not found)  
**Update**: Entity exists (identity key found) → overwrite all fields except:
- Primary keys (Id, Code)
- Immutable fields (InternalSKU, CreatedAt, CreatedBy)
- Auto-updated fields (UpdatedAt = current timestamp, UpdatedBy = current user)

---

## Audit Fields Pattern

**IAuditable Interface** (applied to Items, Suppliers, Locations):
```csharp
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    string? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    string? UpdatedBy { get; set; }
}
```

**EF Core SaveChanges Override**:
- On INSERT: Set CreatedAt, CreatedBy
- On UPDATE: Set UpdatedAt, UpdatedBy
- Publish `MasterDataChanged` event for audit trail

---

## Validation Rules Summary

| Entity | Field | Validation |
|--------|-------|-----------|
| Item | Name | Required, max 200 chars |
| Item | InternalSKU | Unique, max 50 chars |
| Item | BaseUoM | Must exist in UnitOfMeasures |
| Item | CategoryId | Must exist in ItemCategories |
| Item | Weight/Volume | If provided, must be > 0 |
| ItemBarcode | Barcode | Global uniqueness across all items |
| Supplier | Code | Unique, max 50 chars, alphanumeric + dash/underscore |
| SupplierItemMapping | (SupplierId, SupplierSKU) | Unique composite |
| Location | Code | Unique, max 50 chars |
| Location | Barcode | Unique, max 100 chars |
| Lot | (ItemId, LotNumber) | Unique composite |
| ItemUoMConversion | Factor | Must be > 0 |

---

## Index Strategy

### Unique Constraints (Enforce Business Rules)
- `Items.InternalSKU`
- `ItemCategories.Code`
- `Suppliers.Code`
- `Locations.Code`
- `Locations.Barcode`
- `ItemBarcodes.Barcode`
- `HandlingUnits.Barcode`
- `(SupplierItemMappings.SupplierId, SupplierSKU)`
- `(Lots.ItemId, LotNumber)`

### Foreign Key Indexes (Performance)
- All FK columns (CategoryId, SupplierId, ItemId, LocationId, etc.)

### Query Optimization Indexes
- `Locations.IsVirtual` (filter virtual vs physical)
- `Locations.Status` (filter active locations)
- `Lots.ExpiryDate` (FEFO queries)
- `Items.Status` (filter active items)
- `InboundShipments.ExpectedDate` (receiving dashboard)

---

## Change Log Schema

**Master data changes** logged automatically via EF Core:
- Trigger: `SaveChanges()` override
- Event: `MasterDataChanged` published to message queue
- Payload: EntityType, EntityId, ChangedFields, OldValues, NewValues, Timestamp, UserId

**Example Event**:
```json
{
  "eventType": "MasterDataChanged",
  "entityType": "Item",
  "entityId": 42,
  "changedFields": ["Status"],
  "oldValues": {"Status": "Active"},
  "newValues": {"Status": "Discontinued"},
  "timestamp": "2026-02-09T12:00:00Z",
  "userId": "manager-001"
}
```

**Subscribers**: ERP, Analytics, Procurement modules
