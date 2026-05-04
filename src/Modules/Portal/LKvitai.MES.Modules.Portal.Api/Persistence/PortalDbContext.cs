using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Portal.Api.Persistence;

public sealed class PortalDbContext : DbContext
{
    public const string Schema = "portal";

    public PortalDbContext(DbContextOptions<PortalDbContext> options)
        : base(options)
    {
    }

    public DbSet<PortalTile> Tiles => Set<PortalTile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<PortalTile>(entity =>
        {
            entity.ToTable("portal_tiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityByDefaultColumn();
            entity.Property(x => x.Key).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(600).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(300);
            entity.Property(x => x.Quarter).HasMaxLength(40);
            entity.Property(x => x.IconKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RequiredRoles).HasColumnType("text[]");
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
        });
    }
}
