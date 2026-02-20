using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1641_QueryOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_shipments_TrackingNumber",
                schema: "public",
                table: "shipments",
                newName: "idx_shipments_tracking_number");

            migrationBuilder.RenameIndex(
                name: "IX_sales_orders_OrderDate",
                schema: "public",
                table: "sales_orders",
                newName: "idx_sales_orders_order_date");

            migrationBuilder.RenameIndex(
                name: "IX_on_hand_value_CategoryId",
                schema: "public",
                table: "on_hand_value",
                newName: "idx_on_hand_value_category_id");

            migrationBuilder.RenameIndex(
                name: "IX_items_CategoryId",
                schema: "public",
                table: "items",
                newName: "idx_items_category_id");

            migrationBuilder.CreateIndex(
                name: "idx_items_supplier_id",
                schema: "public",
                table: "supplier_item_mappings",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "idx_shipments_dispatched_at",
                schema: "public",
                table: "shipments",
                column: "DispatchedAt");

            migrationBuilder.CreateIndex(
                name: "idx_sales_orders_customer_id_status",
                schema: "public",
                table: "sales_orders",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "idx_outbound_orders_status_requested_ship_date",
                schema: "public",
                table: "outbound_orders",
                columns: new[] { "Status", "RequestedShipDate" });

            migrationBuilder.CreateIndex(
                name: "idx_items_barcode",
                schema: "public",
                table: "items",
                column: "PrimaryBarcode");

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS idx_mt_events_stream_id
                ON warehouse_events.mt_events (stream_id);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS idx_mt_events_type
                ON warehouse_events.mt_events (type);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS idx_mt_events_timestamp
                ON warehouse_events.mt_events ("timestamp");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS idx_available_stock_item_location
                ON warehouse_events.mt_doc_availablestockview ((data ->> 'itemId'), (data ->> 'location'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_items_supplier_id",
                schema: "public",
                table: "supplier_item_mappings");

            migrationBuilder.DropIndex(
                name: "idx_shipments_dispatched_at",
                schema: "public",
                table: "shipments");

            migrationBuilder.DropIndex(
                name: "idx_sales_orders_customer_id_status",
                schema: "public",
                table: "sales_orders");

            migrationBuilder.DropIndex(
                name: "idx_outbound_orders_status_requested_ship_date",
                schema: "public",
                table: "outbound_orders");

            migrationBuilder.DropIndex(
                name: "idx_items_barcode",
                schema: "public",
                table: "items");

            migrationBuilder.Sql("DROP INDEX IF EXISTS warehouse_events.idx_available_stock_item_location;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS warehouse_events.idx_mt_events_timestamp;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS warehouse_events.idx_mt_events_type;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS warehouse_events.idx_mt_events_stream_id;");

            migrationBuilder.RenameIndex(
                name: "idx_shipments_tracking_number",
                schema: "public",
                table: "shipments",
                newName: "IX_shipments_TrackingNumber");

            migrationBuilder.RenameIndex(
                name: "idx_sales_orders_order_date",
                schema: "public",
                table: "sales_orders",
                newName: "IX_sales_orders_OrderDate");

            migrationBuilder.RenameIndex(
                name: "idx_on_hand_value_category_id",
                schema: "public",
                table: "on_hand_value",
                newName: "IX_on_hand_value_CategoryId");

            migrationBuilder.RenameIndex(
                name: "idx_items_category_id",
                schema: "public",
                table: "items",
                newName: "IX_items_CategoryId");
        }
    }
}
