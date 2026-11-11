using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistWarehouseOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "warehouse_cross_dock_number_seq",
                schema: "public");

            migrationBuilder.CreateSequence(
                name: "warehouse_rma_number_seq",
                schema: "public");

            migrationBuilder.CreateSequence(
                name: "warehouse_wave_number_seq",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "agnum_reconciliation_reports",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_reconciliation_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "label_print_jobs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateType = table.Column<string>(type: "text", nullable: false),
                    DataJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_print_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "serial_status_history",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SerialNumberId = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousStatus = table.Column<string>(type: "text", nullable: true),
                    NewStatus = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    WarrantyExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serial_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_serial_status_history_serial_numbers_SerialNumberId",
                        column: x => x.SerialNumberId,
                        principalSchema: "public",
                        principalTable: "serial_numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_cross_dock_links",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('public.warehouse_cross_dock_number_seq')"),
                    InboundShipmentLineId = table.Column<int>(type: "integer", nullable: false),
                    OutboundOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_cross_dock_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_cross_dock_links_inbound_shipment_lines_InboundSh~",
                        column: x => x.InboundShipmentLineId,
                        principalSchema: "public",
                        principalTable: "inbound_shipment_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_cross_dock_links_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_cross_dock_links_outbound_orders_OutboundOrderId",
                        column: x => x.OutboundOrderId,
                        principalSchema: "public",
                        principalTable: "outbound_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_qc_defects",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LotNumber = table.Column<string>(type: "text", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    DefectType = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    AttachmentsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_qc_defects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_qc_defects_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_qc_defects_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "public",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_qc_templates",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CategoryCode = table.Column<string>(type: "text", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    ItemsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_qc_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_qc_templates_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "public",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_rmas",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('public.warehouse_rma_number_seq')"),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InspectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Disposition = table.Column<string>(type: "text", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_rmas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_rmas_sales_orders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "public",
                        principalTable: "sales_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_waves",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('public.warehouse_wave_number_seq')"),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AssignedOperator = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_waves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_rma_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RmaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReasonCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_rma_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouse_rma_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_rma_lines_sales_order_lines_SalesOrderLineId",
                        column: x => x.SalesOrderLineId,
                        principalSchema: "public",
                        principalTable: "sales_order_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_rma_lines_warehouse_rmas_RmaId",
                        column: x => x.RmaId,
                        principalSchema: "public",
                        principalTable: "warehouse_rmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_wave_orders",
                schema: "public",
                columns: table => new
                {
                    WaveId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutboundOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_wave_orders", x => new { x.WaveId, x.OutboundOrderId });
                    table.ForeignKey(
                        name: "FK_warehouse_wave_orders_outbound_orders_OutboundOrderId",
                        column: x => x.OutboundOrderId,
                        principalSchema: "public",
                        principalTable: "outbound_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouse_wave_orders_warehouse_waves_WaveId",
                        column: x => x.WaveId,
                        principalSchema: "public",
                        principalTable: "warehouse_waves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_reconciliation_reports_GeneratedAt",
                schema: "public",
                table: "agnum_reconciliation_reports",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_label_print_jobs_Status_CreatedAt",
                schema: "public",
                table: "label_print_jobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_serial_status_history_SerialNumberId_ChangedAt",
                schema: "public",
                table: "serial_status_history",
                columns: new[] { "SerialNumberId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_cross_dock_links_InboundShipmentLineId_OutboundOr~",
                schema: "public",
                table: "warehouse_cross_dock_links",
                columns: new[] { "InboundShipmentLineId", "OutboundOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_cross_dock_links_ItemId",
                schema: "public",
                table: "warehouse_cross_dock_links",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_cross_dock_links_Number",
                schema: "public",
                table: "warehouse_cross_dock_links",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_cross_dock_links_OutboundOrderId",
                schema: "public",
                table: "warehouse_cross_dock_links",
                column: "OutboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_qc_defects_CreatedAt",
                schema: "public",
                table: "warehouse_qc_defects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_qc_defects_DefectType",
                schema: "public",
                table: "warehouse_qc_defects",
                column: "DefectType");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_qc_defects_ItemId",
                schema: "public",
                table: "warehouse_qc_defects",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_qc_defects_SupplierId",
                schema: "public",
                table: "warehouse_qc_defects",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_qc_templates_Name",
                schema: "public",
                table: "warehouse_qc_templates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_qc_templates_SupplierId",
                schema: "public",
                table: "warehouse_qc_templates",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_rma_lines_ItemId",
                schema: "public",
                table: "warehouse_rma_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_rma_lines_RmaId",
                schema: "public",
                table: "warehouse_rma_lines",
                column: "RmaId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_rma_lines_SalesOrderLineId",
                schema: "public",
                table: "warehouse_rma_lines",
                column: "SalesOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_rmas_Number",
                schema: "public",
                table: "warehouse_rmas",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_rmas_SalesOrderId",
                schema: "public",
                table: "warehouse_rmas",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_rmas_Status",
                schema: "public",
                table: "warehouse_rmas",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_wave_orders_OutboundOrderId",
                schema: "public",
                table: "warehouse_wave_orders",
                column: "OutboundOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_waves_Number",
                schema: "public",
                table: "warehouse_waves",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_waves_Status",
                schema: "public",
                table: "warehouse_waves",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agnum_reconciliation_reports",
                schema: "public");

            migrationBuilder.DropTable(
                name: "label_print_jobs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "serial_status_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_cross_dock_links",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_qc_defects",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_qc_templates",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_rma_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_wave_orders",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_rmas",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_waves",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "warehouse_cross_dock_number_seq",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "warehouse_rma_number_seq",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "warehouse_wave_number_seq",
                schema: "public");
        }
    }
}
