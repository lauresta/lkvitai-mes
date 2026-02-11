using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.Domain.Common;
using LKvitai.MES.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using HandlingUnitAggregate = LKvitai.MES.Domain.Aggregates.HandlingUnit;
using WarehouseLayoutAggregate = LKvitai.MES.Domain.Aggregates.WarehouseLayout;
using HandlingUnitTypeEntity = LKvitai.MES.Domain.Entities.HandlingUnitType;

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
    public DbSet<SupplierItemMapping> SupplierItemMappings => Set<SupplierItemMapping>();
    public DbSet<Location> Locations => Set<Location>();
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
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Barcode).IsUnique();
            entity.HasIndex(e => e.ParentLocationId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsVirtual);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.ParentLocation).WithMany(e => e.Children).HasForeignKey(e => e.ParentLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_locations_type", "\"Type\" IN ('Warehouse','Zone','Aisle','Rack','Shelf','Bin')");
                t.HasCheckConstraint("ck_locations_status", "\"Status\" IN ('Active','Blocked','Maintenance')");
                t.HasCheckConstraint("ck_locations_zone_type", "\"ZoneType\" IS NULL OR \"ZoneType\" IN ('General','Refrigerated','Hazmat','Quarantine')");
                t.HasCheckConstraint("ck_locations_max_weight", "\"MaxWeight\" IS NULL OR \"MaxWeight\" > 0");
                t.HasCheckConstraint("ck_locations_max_volume", "\"MaxVolume\" IS NULL OR \"MaxVolume\" > 0");
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
