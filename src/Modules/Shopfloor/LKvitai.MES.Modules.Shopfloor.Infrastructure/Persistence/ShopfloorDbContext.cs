using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;

public sealed class ShopfloorDbContext : DbContext
{
    public const string Schema = "shopfloor";

    public ShopfloorDbContext(DbContextOptions<ShopfloorDbContext> options)
        : base(options)
    {
    }

    public DbSet<LegacyProductType> LegacyProductTypes => Set<LegacyProductType>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<ProductTypeWorkflowMap> ProductTypeWorkflowMaps => Set<ProductTypeWorkflowMap>();
    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();
    public DbSet<WorkStation> WorkStations => Set<WorkStation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<LegacyProductType>(entity =>
        {
            entity.ToTable("legacy_product_types");
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(x => x.KindName).HasColumnName("kind_name").HasMaxLength(256).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(512).IsRequired();
            entity.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
            entity.Property(x => x.RemovedAt).HasColumnName("removed_at");
            entity.Ignore(x => x.Removed);
            entity.HasIndex(x => x.RemovedAt);
        });

        modelBuilder.Entity<WorkflowTemplate>(entity =>
        {
            entity.ToTable("workflow_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(x => x.GraphJson).HasColumnName("graph_json").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ProductTypeWorkflowMap>(entity =>
        {
            entity.ToTable("product_type_workflow_maps");
            entity.HasKey(x => x.LegacyProductTypeCode);
            entity.Property(x => x.LegacyProductTypeCode)
                .HasColumnName("legacy_product_type_code")
                .HasMaxLength(64);
            entity.Property(x => x.WorkflowTemplateId).HasColumnName("workflow_template_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.WorkflowTemplateId);

            entity.HasOne<LegacyProductType>()
                .WithMany()
                .HasForeignKey(x => x.LegacyProductTypeCode)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<WorkflowTemplate>()
                .WithMany()
                .HasForeignKey(x => x.WorkflowTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkCenter>(entity =>
        {
            entity.ToTable("work_centers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<WorkStation>(entity =>
        {
            entity.ToTable("work_stations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
            entity.Property(x => x.WorkCenterId).HasColumnName("work_center_id");
            entity.Property(x => x.WipLimit).HasColumnName("wip_limit");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.WorkCenterId);

            entity.HasOne<WorkCenter>()
                .WithMany()
                .HasForeignKey(x => x.WorkCenterId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
