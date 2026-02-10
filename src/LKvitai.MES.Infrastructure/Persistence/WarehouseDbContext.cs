using LKvitai.MES.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for state-based aggregates (HandlingUnit, WarehouseLayout)
/// </summary>
public class WarehouseDbContext : DbContext
{
    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : base(options)
    {
    }
    
    public DbSet<HandlingUnit> HandlingUnits => Set<HandlingUnit>();
    public DbSet<WarehouseLayout> Warehouses => Set<WarehouseLayout>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Schema separation contract:
        // - EF Core state tables in `public`
        // - Marten event/projection tables in `warehouse_events`
        modelBuilder.HasDefaultSchema("public");
        
        // HandlingUnit configuration per blueprint
        modelBuilder.Entity<HandlingUnit>(entity =>
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
        modelBuilder.Entity<WarehouseLayout>(entity =>
        {
            entity.ToTable("warehouses");
            entity.HasKey(w => w.WarehouseId);
            entity.Property(w => w.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(w => w.Code).IsUnique();
        });
    }
}
