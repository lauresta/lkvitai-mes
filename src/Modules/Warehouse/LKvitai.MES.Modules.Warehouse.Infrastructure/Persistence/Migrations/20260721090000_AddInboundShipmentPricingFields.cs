using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundShipmentPricingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "public",
                table: "inbound_shipment_lines",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "public",
                table: "inbound_shipment_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
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
                name: "FreightCost",
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

            migrationBuilder.AddColumn<DateOnly>(
                name: "InvoiceDate",
                schema: "public",
                table: "inbound_shipments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(100)",
                maxLength: 100,
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
                name: "ck_inbound_shipment_lines_unit_price",
                schema: "public",
                table: "inbound_shipment_lines",
                sql: "\"UnitPrice\" IS NULL OR \"UnitPrice\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inbound_shipments_costs",
                schema: "public",
                table: "inbound_shipments",
                sql: "(\"FreightCost\" IS NULL OR \"FreightCost\" >= 0) AND (\"DutyCost\" IS NULL OR \"DutyCost\" >= 0) AND (\"InsuranceCost\" IS NULL OR \"InsuranceCost\" >= 0) AND (\"OtherCost\" IS NULL OR \"OtherCost\" >= 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_inbound_shipment_lines_unit_price",
                schema: "public",
                table: "inbound_shipment_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inbound_shipments_costs",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "public",
                table: "inbound_shipment_lines");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "public",
                table: "inbound_shipment_lines");

            migrationBuilder.DropColumn(
                name: "DutyCost",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "FreightCost",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "InsuranceCost",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "InvoiceDate",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "OtherCost",
                schema: "public",
                table: "inbound_shipments");
        }
    }
}
