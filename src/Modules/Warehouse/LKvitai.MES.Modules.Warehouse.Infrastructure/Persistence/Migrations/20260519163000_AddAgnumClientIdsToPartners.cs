using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgnumClientIdsToPartners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgnumClientId",
                schema: "public",
                table: "suppliers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgnumClientId",
                schema: "public",
                table: "customers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_AgnumClientId",
                schema: "public",
                table: "suppliers",
                column: "AgnumClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_AgnumClientId",
                schema: "public",
                table: "customers",
                column: "AgnumClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_suppliers_AgnumClientId",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_customers_AgnumClientId",
                schema: "public",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AgnumClientId",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "AgnumClientId",
                schema: "public",
                table: "customers");
        }
    }
}
