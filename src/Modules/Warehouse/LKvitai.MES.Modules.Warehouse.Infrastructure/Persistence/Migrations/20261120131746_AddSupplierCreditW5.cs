using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCreditW5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier_credit_documents",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalShipmentId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    CreditNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreditDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalInvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OriginalInvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RateUnitsPerEur = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    RateDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RateSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NetEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VatEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    GrossEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AgnumNetEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AgnumVatEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AgnumGrossEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VatTreatment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AccountingRoute = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PostingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PostingRevision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_credit_documents_inbound_shipments_OriginalShipmen~",
                        column: x => x.OriginalShipmentId,
                        principalSchema: "public",
                        principalTable: "inbound_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_documents_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "public",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditId = table.Column<int>(type: "integer", nullable: false),
                    OriginalLineId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ReturnQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FromLocation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetEur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_credit_lines_inbound_shipment_lines_OriginalLineId",
                        column: x => x.OriginalLineId,
                        principalSchema: "public",
                        principalTable: "inbound_shipment_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_lines_supplier_credit_documents_CreditId",
                        column: x => x.CreditId,
                        principalSchema: "public",
                        principalTable: "supplier_credit_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_documents_OriginalShipmentId",
                schema: "public",
                table: "supplier_credit_documents",
                column: "OriginalShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_documents_SupplierId",
                schema: "public",
                table: "supplier_credit_documents",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_lines_CreditId",
                schema: "public",
                table: "supplier_credit_lines",
                column: "CreditId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_lines_ItemId",
                schema: "public",
                table: "supplier_credit_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_lines_OriginalLineId",
                schema: "public",
                table: "supplier_credit_lines",
                column: "OriginalLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_credit_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "supplier_credit_documents",
                schema: "public");
        }
    }
}
