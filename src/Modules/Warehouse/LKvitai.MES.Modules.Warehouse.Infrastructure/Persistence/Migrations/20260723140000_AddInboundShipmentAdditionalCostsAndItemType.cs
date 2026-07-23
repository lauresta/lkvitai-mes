using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundShipmentAdditionalCostsAndItemType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_inbound_shipments_costs",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "FreightCost",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "DutyCost",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "InsuranceCost",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "OtherCost",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                schema: "public",
                table: "items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Stock");

            migrationBuilder.AddColumn<string>(
                name: "CostType",
                schema: "public",
                table: "items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_type",
                schema: "public",
                table: "items",
                sql: "\"ItemType\" IN ('Stock','Service')");

            migrationBuilder.CreateIndex(
                name: "IX_items_ItemType",
                schema: "public",
                table: "items",
                column: "ItemType");

            migrationBuilder.CreateTable(
                name: "inbound_shipment_additional_costs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipmentId = table.Column<int>(type: "integer", nullable: false),
                    CostType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_shipment_additional_costs", x => x.Id);
                    table.CheckConstraint("ck_inbound_shipment_additional_costs_amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_inbound_shipment_additional_costs_inbound_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "inbound_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipment_additional_costs_ShipmentId",
                schema: "public",
                table: "inbound_shipment_additional_costs",
                column: "ShipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbound_shipment_additional_costs",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "ck_items_type",
                schema: "public",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_ItemType",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "ItemType",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "CostType",
                schema: "public",
                table: "items");

            migrationBuilder.AddColumn<decimal>(
                name: "FreightCost",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DutyCost",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceCost",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherCost",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_inbound_shipments_costs",
                schema: "public",
                table: "inbound_shipments",
                sql: "(\"FreightCost\" IS NULL OR \"FreightCost\" >= 0) AND (\"DutyCost\" IS NULL OR \"DutyCost\" >= 0) AND (\"InsuranceCost\" IS NULL OR \"InsuranceCost\" >= 0) AND (\"OtherCost\" IS NULL OR \"OtherCost\" >= 0)");
        }
    }
}
