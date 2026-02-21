using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatch_history",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OutboundOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Carrier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VehicleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ManualTracking = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbound_order_summary",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedShipDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TrackingNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbound_order_summary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shipment_summary",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OutboundOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutboundOrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Carrier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PackedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DispatchedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_summary", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_history_DispatchedAt",
                schema: "public",
                table: "dispatch_history",
                column: "DispatchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_history_ShipmentId",
                schema: "public",
                table: "dispatch_history",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_order_summary_CustomerName",
                schema: "public",
                table: "outbound_order_summary",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_order_summary_OrderDate",
                schema: "public",
                table: "outbound_order_summary",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_outbound_order_summary_Status",
                schema: "public",
                table: "outbound_order_summary",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_summary_DispatchedAt",
                schema: "public",
                table: "shipment_summary",
                column: "DispatchedAt");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_summary_Status",
                schema: "public",
                table: "shipment_summary",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_summary_TrackingNumber",
                schema: "public",
                table: "shipment_summary",
                column: "TrackingNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "outbound_order_summary",
                schema: "public");

            migrationBuilder.DropTable(
                name: "shipment_summary",
                schema: "public");
        }
    }
}
