# Events and Projections

## Event Sourcing Architecture

### Event Store (Marten)
- **Database**: PostgreSQL (same instance as EF Core, separate schema)
- **Event Streams**: Aggregate-based (one stream per logical entity, e.g., shipment, pick task)
- **Projections**: Read models derived from events (AvailableStock, LocationBalance, etc.)
- **Consistency**: Eventual (projection lag <1 second under normal load)

### Event Metadata (Standard for All Events)
```json
{
  "eventType": "GoodsReceived",
  "eventId": "uuid",
  "aggregateId": "uuid",
  "aggregateType": "InboundShipment",
  "sequenceNumber": 1,
  "timestamp": "2026-02-09T14:30:00Z",
  "userId": "operator-456",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00",
  "causationId": "uuid",  // Parent event that caused this
  "correlationId": "uuid"  // Original command/request
}
```

---

## Phase 1 Event Contracts

### GoodsReceived

**Aggregate**: InboundShipment  
**Trigger**: Operator completes receiving via API  
**Purpose**: Record goods arrival, create lot if needed, update stock projection  

**Event Payload**:
```json
{
  "eventType": "GoodsReceived",
  "eventId": "uuid",
  "aggregateId": "shipment-uuid",
  "timestamp": "2026-02-09T14:30:00Z",
  "userId": "operator-456",
  "payload": {
    "shipmentId": "uuid",
    "lineId": "uuid",
    "itemId": 42,
    "internalSKU": "RM-0001",
    "receivedQty": 1000,
    "baseUoM": "PCS",
    "destinationLocationId": 5,
    "destinationLocationCode": "QC_HOLD",
    "lotId": "uuid",
    "lotNumber": "LOT-2026-02-001",
    "productionDate": "2026-02-01",
    "expiryDate": "2027-02-01",
    "supplierId": 10,
    "notes": "Partial receipt"
  }
}
```

**Projection Updates**:
- `AvailableStock`: Add qty to destination location
- `InboundShipmentSummary`: Increment ReceivedQty for line
- `LocationBalance`: Update total weight/volume (if Item.Weight/Volume set)

**Downstream Events** (Published to Message Queue):
- ERP Module: Update procurement status
- Production Module: Notify material available
- Analytics: Log lead time (ExpectedDate vs actual)

---

### StockMoved

**Aggregate**: StockMovementTask  
**Trigger**: Putaway, internal transfer, QC pass/fail  
**Purpose**: Move stock between locations, maintain audit trail  

**Event Payload**:
```json
{
  "eventType": "StockMoved",
  "eventId": "uuid",
  "aggregateId": "movement-uuid",
  "timestamp": "2026-02-09T15:00:00Z",
  "userId": "operator-456",
  "payload": {
    "itemId": 42,
    "qty": 1000,
    "fromLocationId": 1,
    "fromLocationCode": "RECEIVING",
    "toLocationId": 15,
    "toLocationCode": "WH01-A-12-03",
    "lotId": "uuid",
    "movementType": "Putaway",  // Putaway/Transfer/QCPass/QCFail
    "notes": "Placed on shelf"
  }
}
```

**Projection Updates**:
- `AvailableStock`: Subtract from source, add to destination
- `LocationBalance`: Update both locations

**Business Rules**:
- Source location must have sufficient stock (validated before event append)
- Virtual → Physical: Allowed (putaway)
- Physical → Virtual: Allowed (QC, return to production)
- Physical → Physical: Allowed (transfer)

---

### PickCompleted

**Aggregate**: PickTask  
**Trigger**: Operator completes pick task  
**Purpose**: Record stock removal for order fulfillment  

**Event Payload**:
```json
{
  "eventType": "PickCompleted",
  "eventId": "uuid",
  "aggregateId": "pick-task-uuid",
  "timestamp": "2026-02-09T16:15:00Z",
  "userId": "operator-456",
  "payload": {
    "pickTaskId": "uuid",
    "orderId": "order-789",
    "itemId": 42,
    "pickedQty": 50,
    "fromLocationId": 15,
    "fromLocationCode": "WH01-A-12-03",
    "toLocationId": 20,
    "toLocationCode": "SHIPPING",
    "lotId": "uuid",
    "scannedBarcode": "8594156780187",
    "notes": "Pick completed"
  }
}
```

**Projection Updates**:
- `AvailableStock`: Subtract from source, add to SHIPPING
- `ActiveReservations`: Mark reservation as Completed (if exists)
- `LocationBalance`: Update source location

**Downstream Events**:
- Order Module: Update order status → ReadyToShip
- Logistics Module: Create shipment
- ERP: Trigger invoice generation

---

### StockAdjusted

**Aggregate**: StockAdjustment  
**Trigger**: Manager creates manual adjustment  
**Purpose**: Correct discrepancies (damage, theft, inventory count)  

**Event Payload**:
```json
{
  "eventType": "StockAdjusted",
  "eventId": "uuid",
  "aggregateId": "adjustment-uuid",
  "timestamp": "2026-02-09T17:00:00Z",
  "userId": "manager-001",
  "payload": {
    "adjustmentId": "uuid",
    "itemId": 42,
    "locationId": 15,
    "qtyDelta": -10,  // Negative for decrease, positive for increase
    "reasonCode": "DAMAGE",
    "notes": "Water damage from roof leak",
    "lotId": "uuid"
  }
}
```

**Projection Updates**:
- `AvailableStock`: Add qtyDelta (can go negative if insufficient stock - business decision)
- `AdjustmentHistory`: Append record

**Downstream Events**:
- ERP: Record loss/gain for accounting
- Alerts: If qtyDelta < -100, notify manager

**Business Rule**: Negative adjustments that result in negative stock should trigger warning but NOT block (investigation needed).

---

### ReservationCreated

**Aggregate**: Reservation  
**Trigger**: Order Module requests hard lock on stock  
**Purpose**: Prevent overselling, allocate stock to specific orders  

**Event Payload**:
```json
{
  "eventType": "ReservationCreated",
  "eventId": "uuid",
  "aggregateId": "reservation-uuid",
  "timestamp": "2026-02-09T12:00:00Z",
  "userId": "system",
  "payload": {
    "reservationId": "uuid",
    "itemId": 42,
    "reservedQty": 100,
    "orderId": "order-789",
    "locationId": 15,  // Optional: reserve from specific location
    "lotId": "uuid",    // Optional: reserve specific lot
    "expiresAt": "2026-02-10T12:00:00Z"
  }
}
```

**Projection Updates**:
- `ActiveReservations`: Add reservation
- `AvailableStock`: Increment ReservedQty

**Business Rule**: If insufficient AvailableQty - ReservedQty, emit `ReservationFailed` event instead.

---

### ReservationReleased

**Aggregate**: Reservation  
**Trigger**: Order cancelled, pick completed, reservation expired  
**Purpose**: Free up reserved stock  

**Event Payload**:
```json
{
  "eventType": "ReservationReleased",
  "eventId": "uuid",
  "aggregateId": "reservation-uuid",
  "timestamp": "2026-02-09T18:00:00Z",
  "userId": "system",
  "payload": {
    "reservationId": "uuid",
    "itemId": 42,
    "releasedQty": 100,
    "releaseReason": "OrderCancelled"  // OrderCancelled/PickCompleted/Expired
  }
}
```

**Projection Updates**:
- `ActiveReservations`: Remove or mark as Released
- `AvailableStock`: Decrement ReservedQty

---

### QCPassed / QCFailed

**Aggregate**: QCTask  
**Trigger**: QC inspector approves/rejects goods  
**Purpose**: Move stock from QC_HOLD to RECEIVING or QUARANTINE  

**QCPassed Payload**:
```json
{
  "eventType": "QCPassed",
  "eventId": "uuid",
  "aggregateId": "qc-task-uuid",
  "timestamp": "2026-02-09T15:30:00Z",
  "userId": "qc-inspector-001",
  "payload": {
    "itemId": 42,
    "qty": 1000,
    "lotId": "uuid",
    "fromLocationId": 6,  // QC_HOLD
    "toLocationId": 1,    // RECEIVING
    "inspectorNotes": "Quality acceptable"
  }
}
```

**QCFailed Payload**: Same structure, `toLocationId` = QUARANTINE

**Projection Updates**:
- Triggers `StockMoved` event (composite event)
- `AvailableStock`: Move qty between locations

---

## Phase 1 Projections (Read Models)

### AvailableStock

**Purpose**: Real-time stock availability per Item/Location/Lot

**Table Schema** (Marten Projection Table):
```sql
CREATE TABLE mt_doc_availablestock (
    id uuid PRIMARY KEY,
    item_id int NOT NULL,
    internal_sku varchar(50) NOT NULL,
    item_name varchar(200) NOT NULL,
    location_id int NOT NULL,
    location_code varchar(50) NOT NULL,
    lot_id uuid NULL,
    lot_number varchar(100) NULL,
    expiry_date date NULL,
    qty decimal(18,3) NOT NULL DEFAULT 0,
    reserved_qty decimal(18,3) NOT NULL DEFAULT 0,
    available_qty decimal(18,3) GENERATED ALWAYS AS (qty - reserved_qty) STORED,
    base_uom varchar(10) NOT NULL,
    last_updated timestamptz NOT NULL,
    mt_last_modified timestamptz NOT NULL
);

CREATE INDEX ix_availablestock_item ON mt_doc_availablestock(item_id);
CREATE INDEX ix_availablestock_location ON mt_doc_availablestock(location_id);
CREATE INDEX ix_availablestock_lot ON mt_doc_availablestock(lot_id);
CREATE INDEX ix_availablestock_expiry ON mt_doc_availablestock(expiry_date) WHERE expiry_date IS NOT NULL;
CREATE INDEX ix_availablestock_available ON mt_doc_availablestock(available_qty);
```

**Event Handlers**:
- `GoodsReceived` → Add qty to (ItemId, LocationId, LotId)
- `StockMoved` → Subtract from source, add to destination
- `PickCompleted` → Subtract from source, add to destination
- `StockAdjusted` → Add qtyDelta
- `ReservationCreated` → Increment reserved_qty
- `ReservationReleased` → Decrement reserved_qty

**Query Patterns**:
```sql
-- Get stock by item (all locations)
SELECT * FROM mt_doc_availablestock WHERE item_id = 42;

-- Get stock by location (all items)
SELECT * FROM mt_doc_availablestock WHERE location_id = 15;

-- Get expiring stock (FEFO)
SELECT * FROM mt_doc_availablestock 
WHERE expiry_date < CURRENT_DATE + INTERVAL '30 days'
ORDER BY expiry_date ASC;

-- Get low stock items
SELECT item_id, SUM(available_qty) as total_available
FROM mt_doc_availablestock
GROUP BY item_id
HAVING SUM(available_qty) < 100;
```

**Projection Update Logic** (Pseudo-code):
```csharp
public void Apply(GoodsReceived evt)
{
    var key = (evt.ItemId, evt.DestinationLocationId, evt.LotId);
    var stock = GetOrCreate(key);
    
    stock.Qty += evt.ReceivedQty;
    stock.LastUpdated = evt.Timestamp;
    
    // Denormalize item info for query performance
    stock.InternalSKU = GetItemSKU(evt.ItemId);
    stock.ItemName = GetItemName(evt.ItemId);
    stock.LocationCode = GetLocationCode(evt.DestinationLocationId);
    stock.LotNumber = evt.LotNumber;
    stock.ExpiryDate = evt.ExpiryDate;
    stock.BaseUoM = GetItemBaseUoM(evt.ItemId);
}

public void Apply(StockMoved evt)
{
    // Subtract from source
    var sourceKey = (evt.ItemId, evt.FromLocationId, evt.LotId);
    var sourceStock = GetOrCreate(sourceKey);
    sourceStock.Qty -= evt.Qty;
    
    // Add to destination
    var destKey = (evt.ItemId, evt.ToLocationId, evt.LotId);
    var destStock = GetOrCreate(destKey);
    destStock.Qty += evt.Qty;
    
    sourceStock.LastUpdated = destStock.LastUpdated = evt.Timestamp;
}
```

---

### LocationBalance

**Purpose**: Current capacity utilization per location

**Table Schema**:
```sql
CREATE TABLE mt_doc_locationbalance (
    id uuid PRIMARY KEY,
    location_id int NOT NULL UNIQUE,
    location_code varchar(50) NOT NULL,
    location_type varchar(20) NOT NULL,
    max_weight decimal(18,3) NULL,
    max_volume decimal(18,3) NULL,
    total_weight decimal(18,3) NOT NULL DEFAULT 0,
    total_volume decimal(18,3) NOT NULL DEFAULT 0,
    utilization_weight decimal(5,4) NULL,  -- Percentage (0.0 to 1.0)
    utilization_volume decimal(5,4) NULL,
    item_count int NOT NULL DEFAULT 0,
    last_updated timestamptz NOT NULL
);

CREATE UNIQUE INDEX ix_locationbalance_location ON mt_doc_locationbalance(location_id);
```

**Event Handlers**:
- `GoodsReceived`, `StockMoved`, `PickCompleted`, `StockAdjusted` → Recalculate weight/volume

**Calculation Logic**:
```csharp
public void Apply(StockMoved evt)
{
    var item = GetItem(evt.ItemId);
    
    // Update source location
    var sourceBalance = GetOrCreate(evt.FromLocationId);
    sourceBalance.TotalWeight -= evt.Qty * item.Weight;
    sourceBalance.TotalVolume -= evt.Qty * item.Volume;
    sourceBalance.ItemCount = CountDistinctItems(evt.FromLocationId);
    sourceBalance.UtilizationWeight = sourceBalance.TotalWeight / sourceBalance.MaxWeight;
    sourceBalance.UtilizationVolume = sourceBalance.TotalVolume / sourceBalance.MaxVolume;
    
    // Update destination location (similar)
}
```

**Usage**: Putaway UI shows location utilization warnings.

---

### ActiveReservations

**Purpose**: Track hard locks on stock

**Table Schema**:
```sql
CREATE TABLE mt_doc_activereservations (
    id uuid PRIMARY KEY,
    reservation_id uuid NOT NULL UNIQUE,
    item_id int NOT NULL,
    location_id int NULL,
    lot_id uuid NULL,
    reserved_qty decimal(18,3) NOT NULL,
    order_id varchar(100) NOT NULL,
    status varchar(20) NOT NULL,  -- Active/Completed/Cancelled/Expired
    expires_at timestamptz NULL,
    created_at timestamptz NOT NULL
);

CREATE INDEX ix_reservations_item ON mt_doc_activereservations(item_id);
CREATE INDEX ix_reservations_order ON mt_doc_activereservations(order_id);
CREATE INDEX ix_reservations_status ON mt_doc_activereservations(status);
CREATE INDEX ix_reservations_expires ON mt_doc_activereservations(expires_at) WHERE status = 'Active';
```

**Event Handlers**:
- `ReservationCreated` → Insert record
- `PickCompleted` → Update status = Completed
- `ReservationReleased` → Update status = Released

**Saga**: Background job checks `expires_at` and emits `ReservationExpired` event.

---

### InboundShipmentSummary

**Purpose**: Denormalized view for receiving dashboard

**Table Schema**:
```sql
CREATE TABLE mt_doc_inboundshipmentsummary (
    id uuid PRIMARY KEY,
    shipment_id uuid NOT NULL UNIQUE,
    reference_number varchar(100) NULL,
    supplier_id int NULL,
    supplier_name varchar(200) NULL,
    shipment_type varchar(20) NOT NULL,
    expected_date date NOT NULL,
    status varchar(20) NOT NULL,
    total_lines int NOT NULL DEFAULT 0,
    total_expected_qty decimal(18,3) NOT NULL DEFAULT 0,
    total_received_qty decimal(18,3) NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL,
    last_updated timestamptz NOT NULL
);

CREATE UNIQUE INDEX ix_shipmentsummary_id ON mt_doc_inboundshipmentsummary(shipment_id);
CREATE INDEX ix_shipmentsummary_supplier ON mt_doc_inboundshipmentsummary(supplier_id);
CREATE INDEX ix_shipmentsummary_status ON mt_doc_inboundshipmentsummary(status);
CREATE INDEX ix_shipmentsummary_expected ON mt_doc_inboundshipmentsummary(expected_date);
```

**Event Handlers**:
- `InboundShipmentCreated` → Insert record
- `GoodsReceived` → Increment total_received_qty, update status if complete

---

### AdjustmentHistory

**Purpose**: Audit trail for stock adjustments

**Table Schema**:
```sql
CREATE TABLE mt_doc_adjustmenthistory (
    id uuid PRIMARY KEY,
    adjustment_id uuid NOT NULL UNIQUE,
    item_id int NOT NULL,
    internal_sku varchar(50) NOT NULL,
    item_name varchar(200) NOT NULL,
    location_id int NOT NULL,
    location_code varchar(50) NOT NULL,
    qty_delta decimal(18,3) NOT NULL,
    reason_code varchar(50) NOT NULL,
    notes text NULL,
    user_id varchar(100) NOT NULL,
    user_name varchar(200) NULL,
    timestamp timestamptz NOT NULL
);

CREATE INDEX ix_adjustmenthistory_item ON mt_doc_adjustmenthistory(item_id);
CREATE INDEX ix_adjustmenthistory_location ON mt_doc_adjustmenthistory(location_id);
CREATE INDEX ix_adjustmenthistory_user ON mt_doc_adjustmenthistory(user_id);
CREATE INDEX ix_adjustmenthistory_timestamp ON mt_doc_adjustmenthistory(timestamp);
CREATE INDEX ix_adjustmenthistory_reason ON mt_doc_adjustmenthistory(reason_code);
```

**Event Handler**: `StockAdjusted` → Insert record

---

## Consistency Model

### Eventual Consistency Expectations

**Projection Lag Target**: <1 second (90th percentile)

**UI Handling**:
- Display projection timestamp: "Stock as of 16:15:32"
- Show spinner/loading state during projection updates
- Refresh button: re-query projection
- Optimistic UI updates: Assume success, show local update immediately

**Example UI Flow**:
```
1. User clicks "Complete Pick" button
2. UI immediately shows "Pick Completed" (optimistic)
3. API returns 200 OK with eventId
4. UI polls AvailableStock projection every 500ms
5. When projection updated (qty changed), show success toast
6. If projection not updated after 5 seconds, show warning + traceId
```

---

### Concurrency Handling

**Optimistic Concurrency** (Marten built-in):
- Each aggregate has version number
- Append event: `IF current_version == expected_version THEN append ELSE throw ConcurrencyException`

**Retry Logic** (Application Layer):
```csharp
public async Task<Result> CreateReservation(CreateReservationCommand cmd)
{
    int retries = 3;
    while (retries > 0)
    {
        try
        {
            var stream = _eventStore.FetchForWriting<Reservation>(cmd.ReservationId);
            
            // Business logic: check available stock
            var available = await GetAvailableStock(cmd.ItemId);
            if (available < cmd.Qty)
                return Result.Fail("Insufficient stock");
            
            stream.AppendEvent(new ReservationCreated { ... });
            await _eventStore.SaveChanges();
            return Result.Ok();
        }
        catch (ConcurrencyException ex)
        {
            retries--;
            if (retries == 0) throw;
            await Task.Delay(Random.Next(100, 500)); // Jittered backoff
        }
    }
}
```

---

### Cross-Boundary Consistency

**Master Data → Operations**:
- Operations read master data via EF Core (strongly consistent)
- Master data changes do NOT invalidate existing events (events immutable)
- Example: Item renamed from "Bolt" to "Bolt M8" → old events still reference ItemId=42, projection denormalizes current name

**Operations → Master Data**:
- NO direct updates to master data from events
- Example: `InboundShipmentLine.ReceivedQty` is a denormalized read model, updated by projection

**Operations → Order Module**:
- Async via message queue (RabbitMQ)
- At-least-once delivery (idempotent handlers required)
- Example: `PickCompleted` event → Order Module marks order as Shipped (idempotency key = eventId)

---

## Projection Rebuild Strategy

### When to Rebuild
- After schema changes (new fields added)
- After projection logic bugs fixed
- Data corruption detected
- New environment setup (staging, prod)

### Rebuild Procedure
1. Stop projection daemon (prevent concurrent updates)
2. Truncate projection tables (delete all records)
3. Reset projection progress (Marten `mt_event_progression` table)
4. Restart projection daemon
5. Monitor rebuild progress (log: "Processed 10,000 / 50,000 events")
6. Verify data integrity (compare key metrics)

### Downtime Strategy
- **Zero-downtime rebuild**: Use shadow tables (see ops runbook for details)
- **Acceptable downtime**: 2-5 minutes for <100k events

---

## Event Versioning (Future)

**Phase 1**: No event versioning (single schema)

**Phase 2+**: Use event upcasters for backward compatibility
- Example: `GoodsReceivedV1` → `GoodsReceivedV2` (added `handlingUnitId` field)
- Upcaster: `if (evt.Version == 1) { evt.HandlingUnitId = null; evt.Version = 2; }`

---

## Performance Considerations

### Event Append Performance
- Target: <100ms for single event append
- Bottleneck: Postgres disk I/O
- Optimization: Batch append for import operations (500 events/batch)

### Projection Update Performance
- Target: <1 second lag for 90% of events
- Bottleneck: Projection rebuild on high event volume
- Optimization: Denormalize frequently queried fields (SKU, Name) to avoid joins

### Query Performance
- Target: <500ms for AvailableStock queries (10k rows)
- Bottleneck: Complex filters (category + expiry date + location)
- Optimization: Composite indexes on common query patterns

---

## Monitoring & Alerts

### Projection Lag Monitoring
```sql
-- Query Marten event progression
SELECT 
    name AS projection_name,
    last_seq_id AS last_processed_event,
    (SELECT MAX(seq_id) FROM mt_events) AS latest_event,
    (SELECT MAX(seq_id) FROM mt_events) - last_seq_id AS lag_events
FROM mt_event_progression;
```

**Alert Thresholds**:
- Warning: Lag > 1000 events
- Critical: Lag > 10,000 events OR lag age > 60 seconds

### Event Store Health
- Monitor event append rate (events/second)
- Monitor disk space (events table + projection tables)
- Monitor failed projection updates (exception logs)

---

## Event Retention Policy

**Phase 1**: Retain all events indefinitely (no cleanup)

**Phase 2+**: Archive events older than 2 years to cold storage (S3/Azure Blob)
- Keep projections in hot database
- Rebuild from archives if needed (rare)

---

## Summary

Phase 1 implements **6 core events** and **5 projections** to support receiving, putaway, picking, and adjustments. The consistency model is **eventual** (projection lag <1 second), with optimistic concurrency handling for high-contention scenarios (reservations). The architecture is designed for **horizontal scaling** (add more projection workers) and **zero-downtime deployments** (using shadow tables for projection rebuilds).
