using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationBinDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HeightMeters",
                schema: "public",
                table: "locations",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LengthMeters",
                schema: "public",
                table: "locations",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WidthMeters",
                schema: "public",
                table: "locations",
                type: "numeric(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_height_meters",
                schema: "public",
                table: "locations",
                sql: "\"HeightMeters\" IS NULL OR \"HeightMeters\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_length_meters",
                schema: "public",
                table: "locations",
                sql: "\"LengthMeters\" IS NULL OR \"LengthMeters\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_width_meters",
                schema: "public",
                table: "locations",
                sql: "\"WidthMeters\" IS NULL OR \"WidthMeters\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_height_meters",
                schema: "public",
                table: "locations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_length_meters",
                schema: "public",
                table: "locations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_width_meters",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "HeightMeters",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "LengthMeters",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "WidthMeters",
                schema: "public",
                table: "locations");
        }
    }
}
