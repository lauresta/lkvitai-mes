using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboundOrderAndShipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "outbound_order_number_seq",
                schema: "public");

            migrationBuilder.CreateSequence(
                name: "shipment_number_seq",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "outbound_orders",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'OUT-' || LPAD(nextval('outbound_order_number_seq')::text, 4, '0')"),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedShipDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PickedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbound_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_outbound_orders_sales_orders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "public",
                        principalTable: "sales_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbound_order_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutboundOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PickedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m),
                    ShippedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbound_order_lines", x => x.Id);
                    table.CheckConstraint("ck_outbound_order_lines_picked_qty", "\"PickedQty\" >= 0");
                    table.CheckConstraint("ck_outbound_order_lines_qty", "\"Qty\" > 0");
                    table.CheckConstraint("ck_outbound_order_lines_shipped_qty", "\"ShippedQty\" >= 0");
                    table.ForeignKey(
                        name: "FK_outbound_order_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outbound_order_lines_outbound_orders_OutboundOrderId",
                        column: x => x.OutboundOrderId,
                        principalSchema: "public",
                        principalTable: "outbound_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'SHIP-' || LPAD(nextval('shipment_number_seq')::text, 4, '0')"),
                    OutboundOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Carrier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InTransitAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliverySignature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliveryPhotoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ShippingHandlingUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shipments_outbound_orders_OutboundOrderId",
                        column: x => x.OutboundOrderId,
                        principalSchema: "public",
                        principalTable: "outbound_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    HandlingUnitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_lines", x => x.Id);
                    table.CheckConstraint("ck_shipment_lines_qty", "\"Qty\" > 0");
                    table.ForeignKey(
                        name: "FK_shipment_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipment_lines_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbound_order_lines_ItemId",
                schema: "public",
                table: "outbound_order_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_order_lines_OutboundOrderId",
                schema: "public",
                table: "outbound_order_lines",
                column: "OutboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_OrderDate",
                schema: "public",
                table: "outbound_orders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_OrderNumber",
                schema: "public",
                table: "outbound_orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_ReservationId",
                schema: "public",
                table: "outbound_orders",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_SalesOrderId",
                schema: "public",
                table: "outbound_orders",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_orders_Status",
                schema: "public",
                table: "outbound_orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_lines_ItemId",
                schema: "public",
                table: "shipment_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_lines_ShipmentId",
                schema: "public",
                table: "shipment_lines",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_OutboundOrderId",
                schema: "public",
                table: "shipments",
                column: "OutboundOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_ShipmentNumber",
                schema: "public",
                table: "shipments",
                column: "ShipmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_Status",
                schema: "public",
                table: "shipments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_TrackingNumber",
                schema: "public",
                table: "shipments",
                column: "TrackingNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbound_order_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "shipment_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "outbound_orders",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "outbound_order_number_seq",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "shipment_number_seq",
                schema: "public");
        }
    }
}
