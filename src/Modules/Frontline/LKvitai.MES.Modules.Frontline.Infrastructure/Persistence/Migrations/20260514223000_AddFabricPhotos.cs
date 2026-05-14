using LKvitai.MES.Modules.Frontline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Frontline.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(FabricPhotoDbContext))]
    [Migration("20260514223000_AddFabricPhotos")]
    public partial class AddFabricPhotos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fabric_photos",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FabricCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ThumbObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourcePageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceImageFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ImageWidth = table.Column<int>(type: "integer", nullable: true),
                    ImageHeight = table.Column<int>(type: "integer", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fabric_photos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fabric_photos_FabricCode",
                schema: "public",
                table: "fabric_photos",
                column: "FabricCode");

            migrationBuilder.CreateIndex(
                name: "IX_fabric_photos_FabricCode_IsPrimary_unique",
                schema: "public",
                table: "fabric_photos",
                columns: new[] { "FabricCode", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_fabric_photos_FabricCode_Sha256",
                schema: "public",
                table: "fabric_photos",
                columns: new[] { "FabricCode", "Sha256" },
                unique: true,
                filter: "\"Sha256\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fabric_photos",
                schema: "public");
        }
    }
}
