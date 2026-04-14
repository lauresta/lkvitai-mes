using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRackPlacementToLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RacksJson",
                schema: "public",
                table: "warehouse_layouts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationRole",
                schema: "public",
                table: "locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RackRowId",
                schema: "public",
                table: "locations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLevelIndex",
                schema: "public",
                table: "locations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlotSpan",
                schema: "public",
                table: "locations",
                type: "integer",
                nullable: true,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SlotStart",
                schema: "public",
                table: "locations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_RackPlacement",
                schema: "public",
                table: "locations",
                columns: new[] { "RackRowId", "ShelfLevelIndex", "SlotStart" },
                filter: "\"RackRowId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_rack_placement",
                schema: "public",
                table: "locations",
                sql: "(\"RackRowId\" IS NULL AND \"ShelfLevelIndex\" IS NULL AND \"SlotStart\" IS NULL) OR (\"RackRowId\" IS NOT NULL AND \"ShelfLevelIndex\" IS NOT NULL AND \"SlotStart\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_role",
                schema: "public",
                table: "locations",
                sql: "\"LocationRole\" IN ('Cell', 'Bulk', 'EndCap', 'Overflow', 'GroundSlot') OR \"LocationRole\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_RackPlacement",
                schema: "public",
                table: "locations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_rack_placement",
                schema: "public",
                table: "locations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_role",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "RacksJson",
                schema: "public",
                table: "warehouse_layouts");

            migrationBuilder.DropColumn(
                name: "LocationRole",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "RackRowId",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "ShelfLevelIndex",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "SlotSpan",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "SlotStart",
                schema: "public",
                table: "locations");
        }
    }
}
