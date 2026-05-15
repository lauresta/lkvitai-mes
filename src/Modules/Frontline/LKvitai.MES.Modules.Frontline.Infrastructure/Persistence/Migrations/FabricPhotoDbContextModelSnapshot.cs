using LKvitai.MES.Modules.Frontline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(FabricPhotoDbContext))]
    partial class FabricPhotoDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasDefaultSchema("public")
                .HasAnnotation("ProductVersion", "8.0.13");

            modelBuilder.Entity("LKvitai.MES.Modules.Frontline.Infrastructure.Media.FabricPhoto", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("uuid");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamp with time zone");

                b.Property<string>("FabricCode")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("character varying(50)");

                b.Property<long?>("FileSizeBytes")
                    .HasColumnType("bigint");

                b.Property<int?>("ImageHeight")
                    .HasColumnType("integer");

                b.Property<int?>("ImageWidth")
                    .HasColumnType("integer");

                b.Property<bool>("IsPrimary")
                    .HasColumnType("boolean");

                b.Property<string>("OriginalObjectKey")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)");

                b.Property<string>("Sha256")
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)");

                b.Property<string>("SourceImageFileName")
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)");

                b.Property<string>("SourceImageUrl")
                    .HasMaxLength(1000)
                    .HasColumnType("character varying(1000)");

                b.Property<string>("SourcePageUrl")
                    .HasMaxLength(1000)
                    .HasColumnType("character varying(1000)");

                b.Property<string>("ThumbObjectKey")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("character varying(500)");

                b.Property<DateTimeOffset?>("UpdatedAt")
                    .HasColumnType("timestamp with time zone");

                b.HasKey("Id");

                b.HasIndex("FabricCode");

                b.HasIndex("FabricCode", "IsPrimary")
                    .IsUnique()
                    .HasFilter("\"IsPrimary\" = true");

                b.HasIndex("FabricCode", "Sha256")
                    .IsUnique()
                    .HasFilter("\"Sha256\" IS NOT NULL");

                b.ToTable("fabric_photos", "public");
            });
        }
    }
}
