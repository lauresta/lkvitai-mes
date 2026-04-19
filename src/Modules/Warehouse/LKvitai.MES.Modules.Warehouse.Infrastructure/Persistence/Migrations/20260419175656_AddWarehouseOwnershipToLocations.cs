using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseOwnershipToLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                schema: "public",
                table: "locations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE locations l
SET ""WarehouseId"" = w.""WarehouseId""
FROM warehouse_layouts wl
JOIN warehouses w ON w.""Code"" = wl.""WarehouseCode""
WHERE l.""RackRowId"" IS NOT NULL
  AND wl.""RacksJson"" IS NOT NULL
  AND wl.""RacksJson""::text LIKE '%' || l.""RackRowId"" || '%';
");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_WarehouseCode",
                schema: "public",
                table: "locations",
                columns: new[] { "WarehouseId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_WarehouseId",
                schema: "public",
                table: "locations",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_locations_warehouses_WarehouseId",
                schema: "public",
                table: "locations",
                column: "WarehouseId",
                principalSchema: "public",
                principalTable: "warehouses",
                principalColumn: "WarehouseId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_locations_warehouses_WarehouseId",
                schema: "public",
                table: "locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_WarehouseCode",
                schema: "public",
                table: "locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_WarehouseId",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                schema: "public",
                table: "locations");
        }
    }
}
