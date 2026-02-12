using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.Domain.Common;
using LKvitai.MES.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using HandlingUnitAggregate = LKvitai.MES.Domain.Aggregates.HandlingUnit;
using WarehouseLayoutAggregate = LKvitai.MES.Domain.Aggregates.WarehouseLayout;
using HandlingUnitTypeEntity = LKvitai.MES.Domain.Entities.HandlingUnitType;
using WarehouseLayoutEntity = LKvitai.MES.Domain.Entities.WarehouseLayout;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for state-based aggregates (HandlingUnit, WarehouseLayout)
/// </summary>
public class WarehouseDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IEventBus? _eventBus;

    public WarehouseDbContext(
        DbContextOptions<WarehouseDbContext> options,
        ICurrentUserService? currentUserService = null,
        IEventBus? eventBus = null)
        : base(options)
    {
        _currentUserService = currentUserService ?? new SystemCurrentUserService();
        _eventBus = eventBus;
    }

    public DbSet<HandlingUnitAggregate> HandlingUnits => Set<HandlingUnitAggregate>();
    public DbSet<WarehouseLayoutAggregate> Warehouses => Set<WarehouseLayoutAggregate>();

    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
    public DbSet<ItemUoMConversion> ItemUoMConversions => Set<ItemUoMConversion>();
    public DbSet<ItemBarcode> ItemBarcodes => Set<ItemBarcode>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<OutboundOrder> OutboundOrders => Set<OutboundOrder>();
    public DbSet<OutboundOrderLine> OutboundOrderLines => Set<OutboundOrderLine>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentLine> ShipmentLines => Set<ShipmentLine>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<TransferLine> TransferLines => Set<TransferLine>();
    public DbSet<CycleCount> CycleCounts => Set<CycleCount>();
    public DbSet<CycleCountLine> CycleCountLines => Set<CycleCountLine>();
    public DbSet<OutboundOrderSummary> OutboundOrderSummaries => Set<OutboundOrderSummary>();
    public DbSet<ShipmentSummary> ShipmentSummaries => Set<ShipmentSummary>();
    public DbSet<DispatchHistory> DispatchHistories => Set<DispatchHistory>();
    public DbSet<OnHandValue> OnHandValues => Set<OnHandValue>();
    public DbSet<AgnumExportConfig> AgnumExportConfigs => Set<AgnumExportConfig>();
    public DbSet<AgnumMapping> AgnumMappings => Set<AgnumMapping>();
    public DbSet<AgnumExportHistory> AgnumExportHistories => Set<AgnumExportHistory>();
    public DbSet<SupplierItemMapping> SupplierItemMappings => Set<SupplierItemMapping>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<WarehouseLayoutEntity> WarehouseLayouts => Set<WarehouseLayoutEntity>();
    public DbSet<ZoneDefinition> ZoneDefinitions => Set<ZoneDefinition>();
    public DbSet<HandlingUnitTypeEntity> HandlingUnitTypes => Set<HandlingUnitTypeEntity>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<InboundShipment> InboundShipments => Set<InboundShipment>();
    public DbSet<InboundShipmentLine> InboundShipmentLines => Set<InboundShipmentLine>();
    public DbSet<AdjustmentReasonCode> AdjustmentReasonCodes => Set<AdjustmentReasonCode>();
    public DbSet<SerialNumber> SerialNumbers => Set<SerialNumber>();
    public DbSet<SKUSequence> SKUSequences => Set<SKUSequence>();
    public DbSet<EventProcessingCheckpoint> EventProcessingCheckpoints => Set<EventProcessingCheckpoint>();
    public DbSet<PickTask> PickTasks => Set<PickTask>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Schema separation contract:
        // - EF Core state tables in `public`
        // - Marten event/projection tables in `warehouse_events`
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.HasSequence<long>("customer_code_seq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.HasSequence<long>("sales_order_number_seq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.HasSequence<long>("outbound_order_number_seq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.HasSequence<long>("shipment_number_seq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.HasSequence<long>("transfer_number_seq")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.HasSequence<long>("cycle_count_number_seq")
            .StartsAt(1)
            .IncrementsBy(1);
        
        // HandlingUnit configuration per blueprint
        modelBuilder.Entity<HandlingUnitAggregate>(entity =>
        {
            entity.ToTable("handling_units");
            entity.HasKey(hu => hu.HUId);
            
            entity.Property(hu => hu.LPN).IsRequired().HasMaxLength(100);
            entity.HasIndex(hu => hu.LPN).IsUnique();
            
            entity.Property(hu => hu.Location).IsRequired().HasMaxLength(200);
            entity.HasIndex(hu => hu.Location);
            
            entity.Property(hu => hu.Status).IsRequired().HasConversion<string>();
            entity.Property(hu => hu.Type).IsRequired().HasConversion<string>();
            
            // Optimistic concurrency per blueprint
            entity.Property(hu => hu.Version).IsConcurrencyToken();
            
            // Owned collection
            entity.OwnsMany(hu => hu.Lines, lines =>
            {
                lines.ToTable("handling_unit_lines");
                lines.WithOwner().HasForeignKey(l => l.HUId);
                lines.Property(l => l.SKU).IsRequired().HasMaxLength(100);
                lines.Property(l => l.Quantity).HasPrecision(18, 4);
            });
        });
        
        // WarehouseLayout configuration
        modelBuilder.Entity<WarehouseLayoutAggregate>(entity =>
        {
            entity.ToTable("warehouses");
            entity.HasKey(w => w.WarehouseId);
            entity.Property(w => w.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(w => w.Code).IsUnique();
        });

        modelBuilder.Entity<ItemCategory>(entity =>
        {
            entity.ToTable("item_categories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasOne(e => e.ParentCategory)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("unit_of_measures");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(10);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
            entity.ToTable(t => t.HasCheckConstraint("ck_unit_of_measures_type", "\"Type\" IN ('Weight','Volume','Piece','Length')"));
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InternalSKU).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.BaseUoM).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Weight).HasPrecision(18, 3);
            entity.Property(e => e.Volume).HasPrecision(18, 3);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
            entity.Property(e => e.PrimaryBarcode).HasMaxLength(100);
            entity.Property(e => e.ProductConfigId).HasMaxLength(50);
            entity.HasIndex(e => e.InternalSKU).IsUnique();
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.BaseUoM);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BaseUnit).WithMany().HasForeignKey(e => e.BaseUoM).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_items_status", "\"Status\" IN ('Active','Discontinued','Obsolete')");
                t.HasCheckConstraint("ck_items_weight", "\"Weight\" IS NULL OR \"Weight\" > 0");
                t.HasCheckConstraint("ck_items_volume", "\"Volume\" IS NULL OR \"Volume\" > 0");
            });
        });

        modelBuilder.Entity<ItemUoMConversion>(entity =>
        {
            entity.ToTable("item_uom_conversions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromUoM).HasMaxLength(10).IsRequired();
            entity.Property(e => e.ToUoM).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Factor).HasPrecision(18, 6).IsRequired();
            entity.Property(e => e.RoundingRule).HasMaxLength(20).HasDefaultValue("Up");
            entity.HasIndex(e => new { e.ItemId, e.FromUoM, e.ToUoM }).IsUnique();
            entity.HasOne(e => e.Item).WithMany(e => e.UomConversions).HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(e => e.FromUoM).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(e => e.ToUoM).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_item_uom_conversions_factor", "\"Factor\" > 0");
                t.HasCheckConstraint("ck_item_uom_conversions_rounding", "\"RoundingRule\" IN ('Up','Down','Nearest')");
            });
        });

        modelBuilder.Entity<ItemBarcode>(entity =>
        {
            entity.ToTable("item_barcodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Barcode).HasMaxLength(100).IsRequired();
            entity.Property(e => e.BarcodeType).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Barcode).IsUnique();
            entity.HasIndex(e => e.ItemId);
            entity.HasIndex(e => new { e.ItemId, e.IsPrimary })
                .HasFilter("\"IsPrimary\" = true");
            entity.HasOne(e => e.Item).WithMany(e => e.Barcodes).HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("ck_item_barcodes_type", "\"BarcodeType\" IN ('EAN13','Code128','QR','UPC','Other')"));
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactInfo).HasColumnType("text");
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerCode)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValueSql("'CUST-' || LPAD(nextval('customer_code_seq')::text, 4, '0')");
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.PaymentTerms).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreditLimit).HasPrecision(18, 2);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(e => e.CustomerCode).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Status);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.OwnsOne(e => e.BillingAddress, owned =>
            {
                owned.Property(p => p.Street).HasColumnName("billing_address_street").HasMaxLength(200);
                owned.Property(p => p.City).HasColumnName("billing_address_city").HasMaxLength(100);
                owned.Property(p => p.State).HasColumnName("billing_address_state").HasMaxLength(50);
                owned.Property(p => p.ZipCode).HasColumnName("billing_address_zip_code").HasMaxLength(20);
                owned.Property(p => p.Country).HasColumnName("billing_address_country").HasMaxLength(100);
            });

            entity.OwnsOne(e => e.DefaultShippingAddress, owned =>
            {
                owned.Property(p => p.Street).HasColumnName("default_shipping_address_street").HasMaxLength(200);
                owned.Property(p => p.City).HasColumnName("default_shipping_address_city").HasMaxLength(100);
                owned.Property(p => p.State).HasColumnName("default_shipping_address_state").HasMaxLength(50);
                owned.Property(p => p.ZipCode).HasColumnName("default_shipping_address_zip_code").HasMaxLength(20);
                owned.Property(p => p.Country).HasColumnName("default_shipping_address_country").HasMaxLength(100);
            });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_customers_credit_limit", "\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0");
            });
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("sales_orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValueSql("'SO-' || LPAD(nextval('sales_order_number_seq')::text, 4, '0')");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.OrderDate).IsRequired();
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.ReservationId);
            entity.Property(e => e.OutboundOrderId);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.SalesOrders)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.OwnsOne(e => e.ShippingAddress, owned =>
            {
                owned.Property(p => p.Street).HasColumnName("shipping_address_street").HasMaxLength(200);
                owned.Property(p => p.City).HasColumnName("shipping_address_city").HasMaxLength(100);
                owned.Property(p => p.State).HasColumnName("shipping_address_state").HasMaxLength(50);
                owned.Property(p => p.ZipCode).HasColumnName("shipping_address_zip_code").HasMaxLength(20);
                owned.Property(p => p.Country).HasColumnName("shipping_address_country").HasMaxLength(100);
            });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_sales_orders_total_amount", "\"TotalAmount\" >= 0");
            });
        });

        modelBuilder.Entity<SalesOrderLine>(entity =>
        {
            entity.ToTable("sales_order_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderedQty).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.AllocatedQty).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.PickedQty).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.ShippedQty).HasPrecision(18, 4).HasDefaultValue(0m);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.LineAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.HasIndex(e => e.SalesOrderId);
            entity.HasIndex(e => e.ItemId);
            entity.HasQueryFilter(e => !e.SalesOrder!.IsDeleted);
            entity.HasOne(e => e.SalesOrder)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_sales_order_lines_ordered_qty", "\"OrderedQty\" > 0");
                t.HasCheckConstraint("ck_sales_order_lines_allocated_qty", "\"AllocatedQty\" >= 0");
                t.HasCheckConstraint("ck_sales_order_lines_picked_qty", "\"PickedQty\" >= 0");
                t.HasCheckConstraint("ck_sales_order_lines_shipped_qty", "\"ShippedQty\" >= 0");
                t.HasCheckConstraint("ck_sales_order_lines_unit_price", "\"UnitPrice\" >= 0");
                t.HasCheckConstraint("ck_sales_order_lines_line_amount", "\"LineAmount\" >= 0");
            });
        });

        modelBuilder.Entity<OutboundOrder>(entity =>
        {
            entity.ToTable("outbound_orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValueSql("'OUT-' || LPAD(nextval('outbound_order_number_seq')::text, 4, '0')");
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.OrderDate).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.ReservationId);
            entity.HasIndex(e => e.SalesOrderId);
            entity.HasQueryFilter(e => !e.IsDeleted);
            entity.HasOne(e => e.SalesOrder)
                .WithMany()
                .HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Shipment)
                .WithOne(e => e.OutboundOrder)
                .HasForeignKey<Shipment>(e => e.OutboundOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OutboundOrderLine>(entity =>
        {
            entity.ToTable("outbound_order_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Qty).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.PickedQty).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.Property(e => e.ShippedQty).HasPrecision(18, 3).HasDefaultValue(0m);
            entity.HasIndex(e => e.OutboundOrderId);
            entity.HasOne(e => e.OutboundOrder)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.OutboundOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => !e.OutboundOrder!.IsDeleted);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_outbound_order_lines_qty", "\"Qty\" > 0");
                t.HasCheckConstraint("ck_outbound_order_lines_picked_qty", "\"PickedQty\" >= 0");
                t.HasCheckConstraint("ck_outbound_order_lines_shipped_qty", "\"ShippedQty\" >= 0");
            });
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.ToTable("shipments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ShipmentNumber)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValueSql("'SHIP-' || LPAD(nextval('shipment_number_seq')::text, 4, '0')");
            entity.Property(e => e.Carrier).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.TrackingNumber).HasMaxLength(200);
            entity.Property(e => e.DeliverySignature).HasMaxLength(500);
            entity.Property(e => e.DeliveryPhotoUrl).HasMaxLength(1000);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(e => e.ShipmentNumber).IsUnique();
            entity.HasIndex(e => e.OutboundOrderId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TrackingNumber);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<ShipmentLine>(entity =>
        {
            entity.ToTable("shipment_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Qty).HasPrecision(18, 3).IsRequired();
            entity.HasIndex(e => e.ShipmentId);
            entity.HasOne(e => e.Shipment)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => !e.Shipment!.IsDeleted);
            entity.ToTable(t => t.HasCheckConstraint("ck_shipment_lines_qty", "\"Qty\" > 0"));
        });

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.ToTable("transfers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransferNumber)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValueSql("'TRF-' || LPAD(nextval('transfer_number_seq')::text, 4, '0')");
            entity.Property(e => e.FromWarehouse).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ToWarehouse).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(e => e.RequestedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ApprovedBy).HasMaxLength(100);
            entity.Property(e => e.CreateCommandId).IsRequired();
            entity.HasIndex(e => e.TransferNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.FromWarehouse);
            entity.HasIndex(e => e.ToWarehouse);
            entity.HasIndex(e => e.RequestedAt);
            entity.HasMany(e => e.Lines)
                .WithOne(e => e.Transfer)
                .HasForeignKey(e => e.TransferId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_transfers_from_warehouse", "\"FromWarehouse\" <> ''");
                t.HasCheckConstraint("ck_transfers_to_warehouse", "\"ToWarehouse\" <> ''");
                t.HasCheckConstraint("ck_transfers_from_to_not_equal", "\"FromWarehouse\" <> \"ToWarehouse\"");
            });
        });

        modelBuilder.Entity<TransferLine>(entity =>
        {
            entity.ToTable("transfer_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Qty).HasPrecision(18, 3).IsRequired();
            entity.HasIndex(e => e.TransferId);
            entity.HasIndex(e => e.ItemId);
            entity.HasIndex(e => e.FromLocationId);
            entity.HasIndex(e => e.ToLocationId);
            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FromLocation)
                .WithMany()
                .HasForeignKey(e => e.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ToLocation)
                .WithMany()
                .HasForeignKey(e => e.ToLocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_transfer_lines_qty", "\"Qty\" > 0");
                t.HasCheckConstraint("ck_transfer_lines_locations", "\"FromLocationId\" <> \"ToLocationId\"");
            });
        });

        modelBuilder.Entity<CycleCount>(entity =>
        {
            entity.ToTable("cycle_counts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CountNumber)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValueSql("'CC-' || LPAD(nextval('cycle_count_number_seq')::text, 4, '0')");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(e => e.AbcClass).HasMaxLength(10).IsRequired().HasDefaultValue("ALL");
            entity.Property(e => e.AssignedOperator).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(e => e.CountedBy).HasMaxLength(100);
            entity.Property(e => e.ApprovedBy).HasMaxLength(100);
            entity.HasIndex(e => e.CountNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ScheduledDate);
            entity.HasIndex(e => e.AssignedOperator);
            entity.HasMany(e => e.Lines)
                .WithOne(e => e.CycleCount)
                .HasForeignKey(e => e.CycleCountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CycleCountLine>(entity =>
        {
            entity.ToTable("cycle_count_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SystemQty).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.PhysicalQty).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.Delta).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.HasIndex(e => e.CycleCountId);
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.ItemId);
            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_cycle_count_lines_system_qty", "\"SystemQty\" >= 0");
                t.HasCheckConstraint("ck_cycle_count_lines_physical_qty", "\"PhysicalQty\" >= 0");
            });
        });

        modelBuilder.Entity<OutboundOrderSummary>(entity =>
        {
            entity.ToTable("outbound_order_summary");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.ShipmentNumber).HasMaxLength(50);
            entity.Property(e => e.TrackingNumber).HasMaxLength(200);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.CustomerName);
        });

        modelBuilder.Entity<ShipmentSummary>(entity =>
        {
            entity.ToTable("shipment_summary");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ShipmentNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OutboundOrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.Carrier).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TrackingNumber).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PackedBy).HasMaxLength(200);
            entity.Property(e => e.DispatchedBy).HasMaxLength(200);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DispatchedAt);
            entity.HasIndex(e => e.TrackingNumber);
        });

        modelBuilder.Entity<DispatchHistory>(entity =>
        {
            entity.ToTable("dispatch_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ShipmentNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OutboundOrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Carrier).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TrackingNumber).HasMaxLength(200);
            entity.Property(e => e.VehicleId).HasMaxLength(100);
            entity.Property(e => e.DispatchedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.DispatchedAt);
            entity.HasIndex(e => e.ShipmentId);
        });

        modelBuilder.Entity<OnHandValue>(entity =>
        {
            entity.ToTable("on_hand_value");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemSku).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CategoryName).HasMaxLength(200);
            entity.Property(e => e.Qty).HasPrecision(18, 3).IsRequired();
            entity.Property(e => e.UnitCost).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.TotalValue).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired();
            entity.HasIndex(e => e.ItemId).IsUnique();
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.TotalValue);
            entity.HasIndex(e => e.LastUpdated);
            entity.HasOne<Item>()
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ItemCategory>()
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AgnumExportConfig>(entity =>
        {
            entity.ToTable("agnum_export_configs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Scope).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(e => e.Schedule).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Format).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.ApiEndpoint).HasMaxLength(500);
            entity.Property(e => e.ApiKey).HasMaxLength(1000);
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => e.IsActive);
            entity.HasMany(e => e.Mappings)
                .WithOne(e => e.Config)
                .HasForeignKey(e => e.AgnumExportConfigId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgnumMapping>(entity =>
        {
            entity.ToTable("agnum_mappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SourceValue).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AgnumAccountCode).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => new { e.AgnumExportConfigId, e.SourceType, e.SourceValue }).IsUnique();
        });

        modelBuilder.Entity<AgnumExportHistory>(entity =>
        {
            entity.ToTable("agnum_export_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExportNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(1000);
            entity.Property(e => e.ErrorMessage).HasColumnType("text");
            entity.Property(e => e.Trigger).HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.ExportedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExportNumber).IsUnique();
            entity.HasOne(e => e.ExportConfig)
                .WithMany()
                .HasForeignKey(e => e.ExportConfigId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplierItemMapping>(entity =>
        {
            entity.ToTable("supplier_item_mappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupplierSKU).HasMaxLength(100).IsRequired();
            entity.Property(e => e.MinOrderQty).HasPrecision(18, 3);
            entity.Property(e => e.PricePerUnit).HasPrecision(18, 2);
            entity.HasIndex(e => new { e.SupplierId, e.SupplierSKU }).IsUnique();
            entity.HasIndex(e => e.ItemId);
            entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_supplier_item_mappings_leadtime", "\"LeadTimeDays\" IS NULL OR \"LeadTimeDays\" >= 0");
                t.HasCheckConstraint("ck_supplier_item_mappings_moq", "\"MinOrderQty\" IS NULL OR \"MinOrderQty\" > 0");
                t.HasCheckConstraint("ck_supplier_item_mappings_price", "\"PricePerUnit\" IS NULL OR \"PricePerUnit\" > 0");
            });
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Barcode).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Active").IsRequired();
            entity.Property(e => e.ZoneType).HasMaxLength(20);
            entity.Property(e => e.MaxWeight).HasPrecision(18, 3);
            entity.Property(e => e.MaxVolume).HasPrecision(18, 3);
            entity.Property(e => e.CoordinateX).HasPrecision(9, 2);
            entity.Property(e => e.CoordinateY).HasPrecision(9, 2);
            entity.Property(e => e.CoordinateZ).HasPrecision(9, 2);
            entity.Property(e => e.WidthMeters).HasPrecision(9, 3);
            entity.Property(e => e.LengthMeters).HasPrecision(9, 3);
            entity.Property(e => e.HeightMeters).HasPrecision(9, 3);
            entity.Property(e => e.Aisle).HasMaxLength(30);
            entity.Property(e => e.Rack).HasMaxLength(30);
            entity.Property(e => e.Level).HasMaxLength(30);
            entity.Property(e => e.Bin).HasMaxLength(30);
            entity.Property(e => e.CapacityWeight).HasPrecision(18, 3);
            entity.Property(e => e.CapacityVolume).HasPrecision(18, 3);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Barcode).IsUnique();
            entity.HasIndex(e => e.ParentLocationId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsVirtual);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.ParentLocation).WithMany(e => e.Children).HasForeignKey(e => e.ParentLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_locations_type", "\"Type\" IN ('Warehouse','Zone','Aisle','Rack','Shelf','Bin','Virtual')");
                t.HasCheckConstraint("ck_locations_status", "\"Status\" IN ('Active','Blocked','Maintenance')");
                t.HasCheckConstraint("ck_locations_zone_type", "\"ZoneType\" IS NULL OR \"ZoneType\" IN ('General','Refrigerated','Hazmat','Quarantine')");
                t.HasCheckConstraint("ck_locations_max_weight", "\"MaxWeight\" IS NULL OR \"MaxWeight\" > 0");
                t.HasCheckConstraint("ck_locations_max_volume", "\"MaxVolume\" IS NULL OR \"MaxVolume\" > 0");
                t.HasCheckConstraint("ck_locations_capacity_weight", "\"CapacityWeight\" IS NULL OR \"CapacityWeight\" > 0");
                t.HasCheckConstraint("ck_locations_capacity_volume", "\"CapacityVolume\" IS NULL OR \"CapacityVolume\" > 0");
                t.HasCheckConstraint("ck_locations_width_meters", "\"WidthMeters\" IS NULL OR \"WidthMeters\" > 0");
                t.HasCheckConstraint("ck_locations_length_meters", "\"LengthMeters\" IS NULL OR \"LengthMeters\" > 0");
                t.HasCheckConstraint("ck_locations_height_meters", "\"HeightMeters\" IS NULL OR \"HeightMeters\" > 0");
            });
        });

        modelBuilder.Entity<WarehouseLayoutEntity>(entity =>
        {
            entity.ToTable("warehouse_layouts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WarehouseCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.WidthMeters).HasPrecision(9, 2);
            entity.Property(e => e.LengthMeters).HasPrecision(9, 2);
            entity.Property(e => e.HeightMeters).HasPrecision(9, 2);
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => e.WarehouseCode).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_warehouse_layouts_width", "\"WidthMeters\" > 0");
                t.HasCheckConstraint("ck_warehouse_layouts_length", "\"LengthMeters\" > 0");
                t.HasCheckConstraint("ck_warehouse_layouts_height", "\"HeightMeters\" > 0");
            });
        });

        modelBuilder.Entity<ZoneDefinition>(entity =>
        {
            entity.ToTable("zone_definitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ZoneType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(16).IsRequired();
            entity.Property(e => e.X1).HasPrecision(9, 2);
            entity.Property(e => e.Y1).HasPrecision(9, 2);
            entity.Property(e => e.X2).HasPrecision(9, 2);
            entity.Property(e => e.Y2).HasPrecision(9, 2);
            entity.HasIndex(e => e.WarehouseLayoutId);
            entity.HasOne(e => e.WarehouseLayout)
                .WithMany(e => e.Zones)
                .HasForeignKey(e => e.WarehouseLayoutId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_zone_definitions_bounds_x", "\"X2\" > \"X1\"");
                t.HasCheckConstraint("ck_zone_definitions_bounds_y", "\"Y2\" > \"Y1\"");
                t.HasCheckConstraint("ck_zone_definitions_type", "\"ZoneType\" IN ('RECEIVING','STORAGE','SHIPPING','QUARANTINE')");
            });
        });

        modelBuilder.Entity<HandlingUnitTypeEntity>(entity =>
        {
            entity.ToTable("handling_unit_types");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Lot>(entity =>
        {
            entity.ToTable("lots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LotNumber).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => new { e.ItemId, e.LotNumber }).IsUnique();
            entity.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InboundShipment>(entity =>
        {
            entity.ToTable("inbound_shipments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Draft").IsRequired();
            entity.HasIndex(e => e.ReferenceNumber).IsUnique();
            entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("ck_inbound_shipments_status", "\"Status\" IN ('Draft','Partial','Complete','Cancelled')"));
        });

        modelBuilder.Entity<InboundShipmentLine>(entity =>
        {
            entity.ToTable("inbound_shipment_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExpectedQty).HasPrecision(18, 3);
            entity.Property(e => e.ReceivedQty).HasPrecision(18, 3);
            entity.Property(e => e.BaseUoM).HasMaxLength(10).IsRequired();
            entity.HasIndex(e => new { e.ShipmentId, e.ItemId }).IsUnique();
            entity.HasOne(e => e.Shipment).WithMany(e => e.Lines).HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Item).WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(e => e.BaseUoM).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_inbound_shipment_lines_expected_qty", "\"ExpectedQty\" > 0");
                t.HasCheckConstraint("ck_inbound_shipment_lines_received_qty", "\"ReceivedQty\" >= 0");
            });
        });

        modelBuilder.Entity<AdjustmentReasonCode>(entity =>
        {
            entity.ToTable("adjustment_reason_codes");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<SerialNumber>(entity =>
        {
            entity.ToTable("serial_numbers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Value).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.HasIndex(e => e.Value).IsUnique();
            entity.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SKUSequence>(entity =>
        {
            entity.ToTable("sku_sequences");
            entity.HasKey(e => e.Prefix);
            entity.Property(e => e.Prefix).HasMaxLength(20);
            entity.Property(e => e.NextValue).IsRequired();
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<PickTask>(entity =>
        {
            entity.ToTable("pick_tasks");
            entity.HasKey(e => e.TaskId);
            entity.Property(e => e.OrderId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Qty).HasPrecision(18, 3);
            entity.Property(e => e.PickedQty).HasPrecision(18, 3);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.AssignedToUserId).HasMaxLength(100);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ItemId);
            entity.HasIndex(e => e.Status);
            entity.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Location>().WithMany().HasForeignKey(e => e.FromLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Location>().WithMany().HasForeignKey(e => e.ToLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lot>().WithMany().HasForeignKey(e => e.LotId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("ck_pick_tasks_status", "\"Status\" IN ('Pending','Completed','Cancelled')"));
        });

        modelBuilder.Entity<EventProcessingCheckpoint>(entity =>
        {
            entity.ToTable("event_processing_checkpoints");
            entity.HasKey(e => new { e.HandlerName, e.StreamId });
            entity.Property(e => e.HandlerName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.StreamId).HasMaxLength(500).IsRequired();
            entity.Property(e => e.LastEventNumber).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired();
            entity.HasIndex(e => e.ProcessedAt);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var notifications = ApplyAuditFields();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        PublishMasterDataChangesAsync(notifications, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var notifications = ApplyAuditFields();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await PublishMasterDataChangesAsync(notifications, cancellationToken);
        return result;
    }

    private List<MasterDataChangedMessage> ApplyAuditFields()
    {
        var now = DateTimeOffset.UtcNow;
        var currentUser = _currentUserService.GetCurrentUserId();
        var messages = new List<MasterDataChangedMessage>();

        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.Entity is IAuditable &&
                                 (e.State == EntityState.Added || e.State == EntityState.Modified)))
        {
            var auditable = (IAuditable)entry.Entity;
            var action = entry.State == EntityState.Added ? "Created" : "Updated";

            if (entry.State == EntityState.Added)
            {
                auditable.CreatedAt = now;
                auditable.CreatedBy = currentUser;
                auditable.UpdatedAt = null;
                auditable.UpdatedBy = null;
            }
            else
            {
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                auditable.UpdatedAt = now;
                auditable.UpdatedBy = currentUser;
            }

            var primaryKey = entry.Metadata.FindPrimaryKey();
            var entityId = primaryKey is null
                ? string.Empty
                : string.Join(":", primaryKey.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));

            messages.Add(new MasterDataChangedMessage
            {
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                Action = action,
                ChangedBy = currentUser,
                ChangedAtUtc = now
            });
        }

        return messages;
    }

    private async Task PublishMasterDataChangesAsync(
        IReadOnlyCollection<MasterDataChangedMessage> messages,
        CancellationToken cancellationToken)
    {
        if (_eventBus is null || messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            await _eventBus.PublishAsync(message, cancellationToken);
        }
    }

    private sealed class SystemCurrentUserService : ICurrentUserService
    {
        public string GetCurrentUserId() => "system";
    }
}
