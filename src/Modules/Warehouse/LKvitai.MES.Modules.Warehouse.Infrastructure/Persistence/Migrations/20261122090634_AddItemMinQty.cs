using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemMinQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinQty",
                schema: "public",
                table: "items",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_min_qty",
                schema: "public",
                table: "items",
                sql: "\"MinQty\" IS NULL OR \"MinQty\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_items_min_qty",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "MinQty",
                schema: "public",
                table: "items");
        }
    }
}
