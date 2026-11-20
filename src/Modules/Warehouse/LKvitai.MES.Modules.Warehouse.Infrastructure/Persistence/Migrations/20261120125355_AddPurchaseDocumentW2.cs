using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseDocumentW2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountingRoute",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Auto");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EUR");

            migrationBuilder.AddColumn<string>(
                name: "DocumentKind",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Invoice");

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossEur",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetEur",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                schema: "public",
                table: "inbound_shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostedBy",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostingRevision",
                schema: "public",
                table: "inbound_shipments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PostingStatus",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotPosted");

            migrationBuilder.AddColumn<DateOnly>(
                name: "RateDate",
                schema: "public",
                table: "inbound_shipments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateReason",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RateSource",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RateUnitsPerEur",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatEur",
                schema: "public",
                table: "inbound_shipments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatTreatment",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BookedValueEur",
                schema: "public",
                table: "inbound_shipment_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalValueEur",
                schema: "public",
                table: "inbound_shipment_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceQty",
                schema: "public",
                table: "inbound_shipment_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceUnitPrice",
                schema: "public",
                table: "inbound_shipment_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountEur",
                schema: "public",
                table: "inbound_shipment_additional_costs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "additional_cost_documents",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RateUnitsPerEur = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RateSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NetEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VatTreatment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountingRoute = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BasisShipmentId = table.Column<int>(type: "integer", nullable: false),
                    CostTypeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PostingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional_cost_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_additional_cost_documents_inbound_shipments_BasisShipmentId",
                        column: x => x.BasisShipmentId,
                        principalSchema: "public",
                        principalTable: "inbound_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_additional_cost_documents_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "public",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "additional_cost_types",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IncludedInCost = table.Column<bool>(type: "boolean", nullable: false),
                    ExpenseAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_additional_cost_types", x => x.Id);
                    table.CheckConstraint("ck_additional_cost_types_expense_account", "(\"IncludedInCost\" AND \"ExpenseAccount\" IS NULL) OR (NOT \"IncludedInCost\" AND \"ExpenseAccount\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "fx_rates",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UnitsPerEur = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    LoadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fx_rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inbound_shipment_receipts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipmentId = table.Column<int>(type: "integer", nullable: false),
                    LineId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RateEurPerUnit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ValueEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_shipment_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inbound_shipment_receipts_inbound_shipment_lines_LineId",
                        column: x => x.LineId,
                        principalSchema: "public",
                        principalTable: "inbound_shipment_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inbound_shipment_receipts_inbound_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "inbound_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "additional_cost_types",
                columns: new[] { "Id", "Code", "ExpenseAccount", "IncludedInCost", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "Freight", null, true, true, "Freight" },
                    { 2, "Duty", null, true, true, "Duty" },
                    { 3, "Insurance", null, true, true, "Insurance" },
                    { 4, "Other", null, true, true, "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_additional_cost_documents_BasisShipmentId",
                schema: "public",
                table: "additional_cost_documents",
                column: "BasisShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_additional_cost_documents_SupplierId",
                schema: "public",
                table: "additional_cost_documents",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_additional_cost_types_Code",
                schema: "public",
                table: "additional_cost_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fx_rates_RateDate_Currency",
                schema: "public",
                table: "fx_rates",
                columns: new[] { "RateDate", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipment_receipts_LineId",
                schema: "public",
                table: "inbound_shipment_receipts",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipment_receipts_ShipmentId",
                schema: "public",
                table: "inbound_shipment_receipts",
                column: "ShipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "additional_cost_documents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "additional_cost_types",
                schema: "public");

            migrationBuilder.DropTable(
                name: "fx_rates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inbound_shipment_receipts",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "AccountingRoute",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "DocumentKind",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "GrossEur",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "NetEur",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "PostedBy",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "PostingRevision",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "PostingStatus",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "RateDate",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "RateReason",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "RateSource",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "RateUnitsPerEur",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "VatEur",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "VatTreatment",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "BookedValueEur",
                schema: "public",
                table: "inbound_shipment_lines");

            migrationBuilder.DropColumn(
                name: "FinalValueEur",
                schema: "public",
                table: "inbound_shipment_lines");

            migrationBuilder.DropColumn(
                name: "InvoiceQty",
                schema: "public",
                table: "inbound_shipment_lines");

            migrationBuilder.DropColumn(
                name: "InvoiceUnitPrice",
                schema: "public",
                table: "inbound_shipment_lines");

            migrationBuilder.DropColumn(
                name: "AmountEur",
                schema: "public",
                table: "inbound_shipment_additional_costs");
        }
    }
}
