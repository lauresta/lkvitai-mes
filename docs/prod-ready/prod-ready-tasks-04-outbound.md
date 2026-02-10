# Production-Ready Warehouse Tasks - Part 4: Epic A - Outbound/Shipment

**Version:** 1.0  
**Date:** February 10, 2026  
**Source:** prod-ready-universe.md

---

## Epic A Task Index

| TaskId | Title | Est | Dependencies | OwnerType | SourceRefs |
|--------|-------|-----|--------------|-----------|------------|
| PRD-0300 | OutboundOrder Entity & Schema | M | PRD-0001 | Backend/API | Universe §4.Epic A |
| PRD-0301 | Shipment Entity & Schema | M | PRD-0300 | Backend/API | Universe §4.Epic A |
| PRD-0302 | OutboundOrder State Machine | M | PRD-0300 | Backend/API | Universe §4.Epic A |
| PRD-0303 | Shipment State Machine | M | PRD-0301 | Backend/API | Universe §4.Epic A |
| PRD-0304 | Pack Order Command & Handler | L | PRD-0302 | Backend/API | Universe §4.Epic A |
| PRD-0305 | Generate Shipping Label Command | M | PRD-0303 | Backend/API | Universe §4.Epic A |
| PRD-0306 | Dispatch Shipment Command | M | PRD-0303 | Backend/API | Universe §4.Epic A |
| PRD-0307 | Confirm Delivery Command | S | PRD-0303 | Backend/API | Universe §4.Epic A |
| PRD-0308 | Outbound Events | M | PRD-0304, PRD-0306 | Backend/API | Universe §4.Epic A |
| PRD-0309 | Carrier API Integration (FedEx) | L | PRD-0305 | Integration | Universe §4.Epic A |
| PRD-0310 | Shipping Label Generation (ZPL/PDF) | M | PRD-0305 | Backend/API | Universe §4.Epic A |
| PRD-0311 | Proof of Delivery Storage (Blob) | S | PRD-0307 | Infra/DevOps | Universe §4.Epic A |
| PRD-0312 | Outbound API Endpoints | M | PRD-0304, PRD-0306 | Backend/API | Universe §4.Epic A |
| PRD-0313 | Outbound UI - Orders List | M | PRD-0312 | UI | Universe §4.Epic A |
| PRD-0314 | Outbound UI - Create Order Form | M | PRD-0312 | UI | Universe §4.Epic A |
| PRD-0315 | Outbound UI - Packing Station | L | PRD-0312 | UI | Universe §4.Epic A |
| PRD-0316 | Outbound UI - Dispatch Confirmation | M | PRD-0312 | UI | Universe §4.Epic A |

---

## Task PRD-0300: OutboundOrder Entity & Schema

**Epic:** A - Outbound/Shipment  
**Phase:** 1.5  
**Estimate:** M (1 day)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-0001 (Idempotency)  
**SourceRefs:** Universe §4.Epic A (Entities & Data Model Changes)

### Context

- Phase 1 ends at picking → stock "stuck" in SHIPPING location
- Need complete outbound lifecycle: Pack → Label → Dispatch → Deliver
- OutboundOrder links SalesOrder (or ProductionOrder) to physical Shipment
- State-based entity (EF Core), not event-sourced
- Supports B2B (pallet/case) and B2C (piece picking)

### Scope

**In Scope:**
- OutboundOrder entity (header + lines)
- OutboundOrderLine entity (items to ship)
- Database schema (tables, indexes, constraints)
- EF Core configuration (entity mapping, relationships)
- Enums: OutboundOrderType, OutboundOrderStatus

**Out of Scope:**
- Commands/handlers (separate tasks)
- API endpoints (PRD-0312)
- UI (PRD-0313 to PRD-0316)
- Multi-parcel shipments (1 order = 1 shipment for Phase 1.5)

### Requirements

**Functional:**
1. OutboundOrder MUST have unique OrderNumber (auto-generated: OUT-0001, OUT-0002, ...)
2. OutboundOrder MUST link to SalesOrder (nullable: not all outbound tied to sales)
3. OutboundOrder MUST have Type: SALES, TRANSFER, PRODUCTION_RETURN
4. OutboundOrder MUST have Status lifecycle (see state machine in PRD-0302)
5. OutboundOrderLine MUST track: OrderedQty, PickedQty, PackedQty
6. OutboundOrder MUST link to Reservation (for stock allocation)
7. OutboundOrder MUST link to Shipment (once packed)

**Non-Functional:**
1. OrderNumber generation: thread-safe (DB sequence or GUID-based)
2. Audit fields: CreatedBy, CreatedAt, UpdatedBy, UpdatedAt (all entities)
3. Soft delete: IsDeleted flag (retain for audit, don't hard delete)
4. Indexes: status, customer name, order date (for fast queries)

**Data Model:**
```csharp
// OutboundOrder (state-based, EF Core)
public class OutboundOrder
{
  public Guid Id { get; set; }
  public string OrderNumber { get; set; } // Auto-generated: OUT-0001
  public OutboundOrderType Type { get; set; }
  public Guid? SalesOrderId { get; set; } // Nullable
  public string CustomerName { get; set; }
  public Address ShippingAddress { get; set; } // Value object
  public OutboundOrderStatus Status { get; set; }
  public DateTime RequestedShipDate { get; set; }
  public DateTime? ActualShipDate { get; set; }
  public List<OutboundOrderLine> Lines { get; set; }
  public Guid? ReservationId { get; set; }
  public Guid? ShipmentId { get; set; }

  // Audit
  public string CreatedBy { get; set; }
  public DateTime CreatedAt { get; set; }
  public string UpdatedBy { get; set; }
  public DateTime UpdatedAt { get; set; }
  public bool IsDeleted { get; set; }
}

public class OutboundOrderLine
{
  public Guid Id { get; set; }
  public Guid OutboundOrderId { get; set; }
  public Guid ItemId { get; set; }
  public decimal OrderedQty { get; set; }
  public decimal PickedQty { get; set; }
  public decimal PackedQty { get; set; }
  public Guid? HandlingUnitId { get; set; } // HU picked from
  public Guid? LotId { get; set; }

  // Navigation
  public OutboundOrder OutboundOrder { get; set; }
  public Item Item { get; set; }
}

// Enums
public enum OutboundOrderType
{
  SALES,              // B2B/B2C customer order
  TRANSFER,           // Inter-warehouse transfer
  PRODUCTION_RETURN   // Return from production floor
}

public enum OutboundOrderStatus
{
  DRAFT,      // Order created, not yet allocated
  ALLOCATED,  // Stock allocated (SOFT lock)
  PICKING,    // Picking in progress (HARD lock)
  PACKED,     // Packed, ready to ship
  SHIPPED,    // Dispatched to customer
  DELIVERED,  // Delivered to customer
  CANCELLED   // Order cancelled
}

// Value Object: Address
public class Address
{
  public string Street { get; set; }
  public string City { get; set; }
  public string State { get; set; }
  public string ZipCode { get; set; }
  public string Country { get; set; }
}
```

**Database Schema (SQL):**
```sql
CREATE TABLE outbound_orders (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  order_number VARCHAR(50) NOT NULL UNIQUE,
  type VARCHAR(50) NOT NULL, -- SALES, TRANSFER, PRODUCTION_RETURN
  sales_order_id UUID NULL,
  customer_name VARCHAR(200) NOT NULL,
  shipping_address_street VARCHAR(200),
  shipping_address_city VARCHAR(100),
  shipping_address_state VARCHAR(50),
  shipping_address_zip_code VARCHAR(20),
  shipping_address_country VARCHAR(100),
  status VARCHAR(50) NOT NULL, -- DRAFT, ALLOCATED, PICKING, PACKED, SHIPPED, DELIVERED, CANCELLED
  requested_ship_date DATE NOT NULL,
  actual_ship_date DATE NULL,
  reservation_id UUID NULL,
  shipment_id UUID NULL,
  created_by VARCHAR(200) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_by VARCHAR(200) NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX idx_outbound_orders_status ON outbound_orders(status) WHERE is_deleted = FALSE;
CREATE INDEX idx_outbound_orders_customer_name ON outbound_orders(customer_name) WHERE is_deleted = FALSE;
CREATE INDEX idx_outbound_orders_order_number ON outbound_orders(order_number);
CREATE INDEX idx_outbound_orders_requested_ship_date ON outbound_orders(requested_ship_date) WHERE is_deleted = FALSE;

CREATE TABLE outbound_order_lines (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  outbound_order_id UUID NOT NULL REFERENCES outbound_orders(id) ON DELETE CASCADE,
  item_id UUID NOT NULL REFERENCES items(id),
  ordered_qty DECIMAL(18,4) NOT NULL,
  picked_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  packed_qty DECIMAL(18,4) NOT NULL DEFAULT 0,
  handling_unit_id UUID NULL REFERENCES handling_units(id),
  lot_id UUID NULL REFERENCES lots(id)
);

CREATE INDEX idx_outbound_order_lines_outbound_order_id ON outbound_order_lines(outbound_order_id);
CREATE INDEX idx_outbound_order_lines_item_id ON outbound_order_lines(item_id);
```

**EF Core Configuration:**
```csharp
public class OutboundOrderConfiguration : IEntityTypeConfiguration<OutboundOrder>
{
  public void Configure(EntityTypeBuilder<OutboundOrder> builder)
  {
    builder.ToTable("outbound_orders");
    builder.HasKey(o => o.Id);
    builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
    builder.HasIndex(o => o.OrderNumber).IsUnique();
    builder.HasIndex(o => o.Status);
    builder.HasIndex(o => o.CustomerName);
    builder.Property(o => o.Type).HasConversion<string>();
    builder.Property(o => o.Status).HasConversion<string>();
    builder.OwnsOne(o => o.ShippingAddress, a => {
      a.Property(x => x.Street).HasColumnName("shipping_address_street");
      a.Property(x => x.City).HasColumnName("shipping_address_city");
      a.Property(x => x.State).HasColumnName("shipping_address_state");
      a.Property(x => x.ZipCode).HasColumnName("shipping_address_zip_code");
      a.Property(x => x.Country).HasColumnName("shipping_address_country");
    });
    builder.HasMany(o => o.Lines).WithOne(l => l.OutboundOrder).HasForeignKey(l => l.OutboundOrderId);
    builder.HasQueryFilter(o => !o.IsDeleted); // Global query filter (soft delete)
  }
}
```

### Acceptance Criteria

```gherkin
Scenario: Create OutboundOrder entity
  Given OutboundOrder entity defined with all properties
  When EF Core migration generated
  Then outbound_orders table created
  And outbound_order_lines table created
  And indexes created (status, customer_name, order_number)
  And foreign keys enforced (item_id, handling_unit_id, lot_id)

Scenario: OrderNumber auto-generation
  Given OutboundOrder created without OrderNumber
  When entity saved to database
  Then OrderNumber generated: OUT-0001
  And subsequent orders: OUT-0002, OUT-0003, ...
  And OrderNumber is unique (DB constraint)

Scenario: Address value object mapping
  Given OutboundOrder with ShippingAddress
  When entity saved
  Then address stored in 5 columns (street, city, state, zip, country)
  And address retrieved as single Address object

Scenario: Soft delete behavior
  Given OutboundOrder with IsDeleted = true
  When querying outbound_orders
  Then soft-deleted orders NOT returned (global query filter)
  And can query with IgnoreQueryFilters() to see deleted

Scenario: Audit fields auto-populated
  Given OutboundOrder created by user "john@example.com"
  When entity saved
  Then CreatedBy = "john@example.com"
  And CreatedAt = current timestamp
  And UpdatedBy = "john@example.com"
  And UpdatedAt = current timestamp
```

### Implementation Notes

- Use EF Core value converters for enums (store as strings, not ints)
- Address is value object (owned entity, no separate table)
- OrderNumber generation: use DB sequence or GUID-based (e.g., "OUT-" + ShortGuid)
- Soft delete: global query filter prevents accidental hard deletes
- Audit fields: populate via SaveChanges interceptor (see Phase 1 implementation)

### Validation / Checks

**Local Testing:**
```bash
# Generate migration
dotnet ef migrations add AddOutboundOrder --project src/LKvitai.MES.Infrastructure

# Apply migration
dotnet ef database update --project src/LKvitai.MES.Api

# Verify schema
psql -d warehouse -c "\d outbound_orders"
psql -d warehouse -c "\d outbound_order_lines"
psql -d warehouse -c "\di outbound_orders*"

# Test entity creation
dotnet test --filter "FullyQualifiedName~OutboundOrderTests"
```

**Metrics:**
- N/A (entity definition, no runtime metrics)

**Logs:**
- N/A (entity definition, no logs)

**Backwards Compatibility:**
- New tables, no breaking changes
- Ensure migration is reversible (Down() method)

### Definition of Done

- [ ] OutboundOrder entity class created (OutboundOrder.cs)
- [ ] OutboundOrderLine entity class created
- [ ] Enums defined (OutboundOrderType, OutboundOrderStatus)
- [ ] Address value object defined
- [ ] EF Core configuration created (OutboundOrderConfiguration.cs)
- [ ] Migration generated (AddOutboundOrder)
- [ ] Migration applied to local DB
- [ ] Schema verified (tables, indexes, constraints)
- [ ] Unit tests: entity creation, validation, relationships (10+ tests)
- [ ] Soft delete tested (global query filter)
- [ ] Audit fields tested (auto-populated)
- [ ] Code review completed
- [ ] Documentation: entity diagram added to docs/

---

## Task PRD-0304: Pack Order Command & Handler

**Epic:** A - Outbound/Shipment  
**Phase:** 1.5  
**Estimate:** L (2 days)  
**OwnerType:** Backend/API  
**Dependencies:** PRD-0302 (OutboundOrder State Machine)  
**SourceRefs:** Universe §4.Epic A (Commands/APIs), Universe §3.Workflow 2 (Step 5: Pack Order)

### Context

- Packing station workflow: operator scans order, scans items, verifies, packs into shipping HU
- Pack command transitions OutboundOrder: PICKING → PACKED
- Creates Shipment entity (status: PACKED)
- Emits StockMoved event (PICKING_STAGING → SHIPPING)
- Generates shipping HU (consolidates picked items)
- Validates: all order items scanned, quantities match

### Scope

**In Scope:**
- PackOrderCommand (MediatR command)
- PackOrderCommandHandler (business logic)
- Validation: all items scanned, quantities correct
- Create Shipment entity (status: PACKED)
- Create shipping HandlingUnit (consolidate items)
- Emit events: ShipmentPacked, StockMoved, HandlingUnitCreated
- Update OutboundOrder status: PACKED
- Idempotency: CommandId deduplication

**Out of Scope:**
- Label generation (PRD-0305)
- Dispatch (PRD-0306)
- Multi-parcel shipments (Phase 2)
- Packing UI (PRD-0315)

### Requirements

**Functional:**
1. Command MUST validate: OutboundOrder exists, status = PICKING
2. Command MUST validate: all order lines have scanned items (actualQty provided)
3. Command MUST validate: scanned barcode matches order item
4. Command MUST create Shipment entity (status: PACKED)
5. Command MUST create shipping HandlingUnit (type: BOX or PALLET)
6. Command MUST emit StockMoved event (PICKING_STAGING → SHIPPING) for each line
7. Command MUST update OutboundOrder.Status = PACKED, OutboundOrder.ShipmentId
8. Command MUST be idempotent (CommandId deduplication)

**Non-Functional:**
1. Command execution time: < 2 seconds (95th percentile)
2. Transactional: all changes committed atomically (DB transaction)
3. Idempotency: duplicate commands return cached result
4. Validation errors: return 400 Bad Request with details
5. Concurrency: optimistic locking (OutboundOrder version check)

**Command DTO:**
```csharp
public record PackOrderCommand(
  Guid CommandId,
  Guid OutboundOrderId,
  List<PackOrderLineDto> Lines,
  HandlingUnitType PackagingType, // BOX, PALLET
  string PackedBy
) : IRequest<Result<PackOrderResult>>;

public record PackOrderLineDto(
  Guid OutboundOrderLineId,
  decimal ActualQty,
  string ScannedBarcode
);

public record PackOrderResult(
  Guid ShipmentId,
  string ShipmentNumber,
  Guid ShippingHandlingUnitId
);
```

**Handler Logic:**
```csharp
public class PackOrderCommandHandler : IRequestHandler<PackOrderCommand, Result<PackOrderResult>>
{
  public async Task<Result<PackOrderResult>> Handle(PackOrderCommand cmd, CancellationToken ct)
  {
    // 1. Idempotency check
    var cached = await _idempotency.GetCachedResult<PackOrderResult>(cmd.CommandId);
    if (cached != null) return Result.Success(cached);

    // 2. Load OutboundOrder
    var order = await _context.OutboundOrders
      .Include(o => o.Lines)
      .FirstOrDefaultAsync(o => o.Id == cmd.OutboundOrderId, ct);
    if (order == null) return Result.Failure<PackOrderResult>("Order not found");
    if (order.Status != OutboundOrderStatus.PICKING)
      return Result.Failure<PackOrderResult>($"Order status must be PICKING, current: {order.Status}");

    // 3. Validate all lines scanned
    if (cmd.Lines.Count != order.Lines.Count)
      return Result.Failure<PackOrderResult>("Not all order lines scanned");

    // 4. Validate scanned barcodes match items
    foreach (var cmdLine in cmd.Lines)
    {
      var orderLine = order.Lines.FirstOrDefault(l => l.Id == cmdLine.OutboundOrderLineId);
      if (orderLine == null) return Result.Failure<PackOrderResult>($"Line {cmdLine.OutboundOrderLineId} not found");

      var item = await _context.Items.FindAsync(orderLine.ItemId);
      if (item.PrimaryBarcode != cmdLine.ScannedBarcode)
        return Result.Failure<PackOrderResult>($"Barcode mismatch: expected {item.PrimaryBarcode}, got {cmdLine.ScannedBarcode}");

      if (cmdLine.ActualQty != orderLine.OrderedQty)
        return Result.Failure<PackOrderResult>($"Quantity mismatch: expected {orderLine.OrderedQty}, got {cmdLine.ActualQty}");

      orderLine.PackedQty = cmdLine.ActualQty;
    }

    // 5. Create Shipment
    var shipment = new Shipment
    {
      Id = Guid.NewGuid(),
      ShipmentNumber = await _numberGenerator.GenerateShipmentNumber(),
      OutboundOrderId = order.Id,
      Status = ShipmentStatus.PACKED,
      PackedAt = DateTime.UtcNow,
      PackedBy = cmd.PackedBy
    };
    _context.Shipments.Add(shipment);

    // 6. Create shipping HandlingUnit
    var shippingHU = new HandlingUnit
    {
      Id = Guid.NewGuid(),
      HandlingUnitNumber = await _numberGenerator.GenerateHUNumber(),
      Type = cmd.PackagingType,
      Location = "SHIPPING",
      Status = HandlingUnitStatus.SEALED,
      CreatedBy = cmd.PackedBy
    };
    _context.HandlingUnits.Add(shippingHU);

    // 7. Update OutboundOrder
    order.Status = OutboundOrderStatus.PACKED;
    order.ShipmentId = shipment.Id;
    order.UpdatedBy = cmd.PackedBy;
    order.UpdatedAt = DateTime.UtcNow;

    // 8. Emit events
    await _eventBus.Publish(new ShipmentPacked(
      shipment.Id, order.Id, cmd.Lines.Select(l => new ShipmentLineDto(l.OutboundOrderLineId, l.ActualQty)).ToList(),
      shipment.PackedAt.Value, cmd.PackedBy
    ));

    foreach (var line in order.Lines)
    {
      await _eventBus.Publish(new StockMoved(
        line.ItemId, line.PackedQty, "PICKING_STAGING", "SHIPPING",
        StockMovementType.PACK, cmd.PackedBy, DateTime.UtcNow, shippingHU.Id
      ));
    }

    await _eventBus.Publish(new HandlingUnitCreated(
      shippingHU.Id, shippingHU.HandlingUnitNumber, shippingHU.Type, "SHIPPING", DateTime.UtcNow
    ));

    // 9. Save changes
    await _context.SaveChangesAsync(ct);

    // 10. Cache result
    var result = new PackOrderResult(shipment.Id, shipment.ShipmentNumber, shippingHU.Id);
    await _idempotency.CacheResult(cmd.CommandId, result);

    return Result.Success(result);
  }
}
```

### Acceptance Criteria

```gherkin
Scenario: Pack order successfully
  Given OutboundOrder "OUT-0001" with status "PICKING"
  And order has 2 lines: RM-0001 (qty 10), RM-0002 (qty 20)
  And all items picked to PICKING_STAGING location
  When PackOrderCommand executed:
    | LineId | ActualQty | ScannedBarcode |
    | line1  | 10        | BC-RM-0001     |
    | line2  | 20        | BC-RM-0002     |
  And PackagingType = BOX
  Then Shipment created (status: PACKED, number: SHIP-0001)
  And shipping HandlingUnit created (type: BOX, location: SHIPPING)
  And OutboundOrder status updated: PACKED
  And OutboundOrder.ShipmentId = SHIP-0001
  And ShipmentPacked event emitted
  And StockMoved events emitted (2 events: PICKING_STAGING → SHIPPING)
  And HandlingUnitCreated event emitted

Scenario: Pack fails - wrong barcode scanned
  Given OutboundOrder "OUT-0002" with status "PICKING"
  And order has 1 line: RM-0003 (barcode: BC-RM-0003)
  When PackOrderCommand executed with scanned barcode "BC-WRONG"
  Then command fails with error: "Barcode mismatch: expected BC-RM-0003, got BC-WRONG"
  And OutboundOrder status unchanged (PICKING)
  And no Shipment created
  And no events emitted

Scenario: Pack fails - quantity mismatch
  Given OutboundOrder "OUT-0003" with status "PICKING"
  And order has 1 line: RM-0004 (ordered qty: 50)
  When PackOrderCommand executed with actualQty: 40
  Then command fails with error: "Quantity mismatch: expected 50, got 40"
  And OutboundOrder status unchanged (PICKING)

Scenario: Pack fails - not all lines scanned
  Given OutboundOrder "OUT-0004" with status "PICKING"
  And order has 3 lines
  When PackOrderCommand executed with only 2 lines
  Then command fails with error: "Not all order lines scanned"

Scenario: Pack idempotency
  Given OutboundOrder "OUT-0005" with status "PICKING"
  When PackOrderCommand executed with CommandId "cmd-123"
  And command succeeds (Shipment SHIP-0005 created)
  And same command executed again with CommandId "cmd-123"
  Then cached result returned (Shipment SHIP-0005)
  And no duplicate Shipment created
  And no duplicate events emitted
```

### Implementation Notes

- Use MediatR for command handling
- Idempotency: check `processed_commands` table before execution
- Transaction: wrap all DB changes + event publishing in single transaction (transactional outbox)
- Number generation: use sequence or GUID-based (thread-safe)
- Validation: use FluentValidation for command DTO validation
- Concurrency: EF Core optimistic concurrency (version field on OutboundOrder)

### Validation / Checks

**Local Testing:**
```bash
# Run pack order tests
dotnet test --filter "FullyQualifiedName~PackOrderCommandHandlerTests"

# Test idempotency
curl -X POST /api/warehouse/v1/outbound-orders/{id}/pack \
  -H "Content-Type: application/json" \
  -d '{"commandId":"test-123","lines":[...],"packagingType":"BOX","packedBy":"operator1"}'
# Repeat same curl → should return cached result

# Check events emitted
psql -d warehouse -c "SELECT type, data FROM mt_events WHERE type IN ('shipment_packed', 'stock_moved') ORDER BY timestamp DESC LIMIT 10;"
```

**Metrics:**
- `pack_order_commands_total` (counter, labels: status=success|failure)
- `pack_order_duration_ms` (histogram)
- `pack_order_validation_errors_total` (counter, labels: error_type)

**Logs:**
- INFO: "Packing order {OrderId}, {LineCount} lines, PackedBy {Operator}"
- INFO: "Shipment {ShipmentId} created, HU {HUId}"
- WARN: "Pack validation failed: {ErrorMessage}"
- ERROR: "Pack command failed: {Exception}"

**Backwards Compatibility:**
- New command, no breaking changes
- Events: new event types (ShipmentPacked, HandlingUnitCreated)

### Definition of Done

- [ ] PackOrderCommand record defined
- [ ] PackOrderCommandHandler implemented
- [ ] Validation logic: all lines scanned, barcodes match, quantities correct
- [ ] Shipment entity created
- [ ] Shipping HandlingUnit created
- [ ] OutboundOrder status updated
- [ ] Events emitted (ShipmentPacked, StockMoved, HandlingUnitCreated)
- [ ] Idempotency implemented (CommandId check)
- [ ] Unit tests: 20+ scenarios (happy path, validation errors, idempotency, concurrency)
- [ ] Integration tests: end-to-end pack workflow
- [ ] Metrics exposed (commands, duration, errors)
- [ ] Logs added (INFO, WARN, ERROR)
- [ ] Code review completed
- [ ] Documentation: command flow diagram added

---

