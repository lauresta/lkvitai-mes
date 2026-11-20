using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntrastatW8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommodityCode",
                schema: "public",
                table: "items",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetWeightKg",
                schema: "public",
                table: "items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginCountry",
                schema: "public",
                table: "items",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTerms",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DispatchCountry",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionNature",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "11");

            migrationBuilder.AddColumn<string>(
                name: "TransportMode",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommodityCode",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "NetWeightKg",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "OriginCountry",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "DeliveryTerms",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "DispatchCountry",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "TransactionNature",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "TransportMode",
                schema: "public",
                table: "inbound_shipments");
        }
    }
}
