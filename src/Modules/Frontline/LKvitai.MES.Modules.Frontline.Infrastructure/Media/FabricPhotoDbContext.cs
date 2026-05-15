using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Media;

public sealed class FabricPhotoDbContext : DbContext
{
    public FabricPhotoDbContext(DbContextOptions<FabricPhotoDbContext> options)
        : base(options)
    {
    }

    public DbSet<FabricPhoto> FabricPhotos => Set<FabricPhoto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<FabricPhoto>(entity =>
        {
            entity.ToTable("fabric_photos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FabricCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OriginalObjectKey).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ThumbObjectKey).HasMaxLength(500).IsRequired();
            entity.Property(e => e.SourceImageUrl).HasMaxLength(1000);
            entity.Property(e => e.SourcePageUrl).HasMaxLength(1000);
            entity.Property(e => e.SourceImageFileName).HasMaxLength(500);
            entity.Property(e => e.Sha256).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.IsPrimary).IsRequired();

            entity.HasIndex(e => e.FabricCode);
            entity.HasIndex(e => new { e.FabricCode, e.IsPrimary });
            entity.HasIndex(e => new { e.FabricCode, e.Sha256 })
                .IsUnique()
                .HasFilter("\"Sha256\" IS NOT NULL");
            entity.HasIndex(e => new { e.FabricCode, e.IsPrimary })
                .IsUnique()
                .HasFilter("\"IsPrimary\" = true");
        });
    }
}
