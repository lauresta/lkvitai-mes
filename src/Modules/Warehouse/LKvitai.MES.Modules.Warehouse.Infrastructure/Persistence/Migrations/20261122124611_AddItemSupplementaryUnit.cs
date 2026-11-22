using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemSupplementaryUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplementaryUnit",
                schema: "public",
                table: "items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplementaryUnitFactor",
                schema: "public",
                table: "items",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_supplementary_unit_factor",
                schema: "public",
                table: "items",
                sql: "\"SupplementaryUnitFactor\" IS NULL OR \"SupplementaryUnitFactor\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_items_supplementary_unit_factor",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "SupplementaryUnit",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "SupplementaryUnitFactor",
                schema: "public",
                table: "items");
        }
    }
}
