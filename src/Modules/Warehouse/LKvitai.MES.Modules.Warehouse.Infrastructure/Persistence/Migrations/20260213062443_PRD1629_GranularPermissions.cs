using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1629_GranularPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "public",
                table: "permissions",
                columns: new[] { "Id", "Action", "Resource", "Scope" },
                values: new object[,]
                {
                    { 9, "READ", "ITEM", "OWN" },
                    { 10, "UPDATE", "ITEM", "OWN" },
                    { 11, "READ", "LOCATION", "OWN" },
                    { 12, "UPDATE", "LOCATION", "OWN" },
                    { 13, "READ", "ORDER", "OWN" },
                    { 14, "UPDATE", "ORDER", "OWN" },
                    { 15, "READ", "QC", "OWN" },
                    { 16, "UPDATE", "QC", "OWN" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "Id",
                keyValue: 16);
        }
    }
}
