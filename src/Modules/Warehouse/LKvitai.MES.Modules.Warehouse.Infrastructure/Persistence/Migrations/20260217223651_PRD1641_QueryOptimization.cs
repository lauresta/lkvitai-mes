using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1641_QueryOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RenameIndexIfNeeded(migrationBuilder, "IX_shipments_TrackingNumber", "idx_shipments_tracking_number");
            RenameIndexIfNeeded(migrationBuilder, "IX_sales_orders_OrderDate", "idx_sales_orders_order_date");
            RenameIndexIfNeeded(migrationBuilder, "IX_on_hand_value_CategoryId", "idx_on_hand_value_category_id");
            RenameIndexIfNeeded(migrationBuilder, "IX_items_CategoryId", "idx_items_category_id");

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
                DO $$
                BEGIN
                    IF to_regclass('warehouse_events.mt_events') IS NOT NULL THEN
                        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_mt_events_stream_id ON warehouse_events.mt_events (stream_id);';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('warehouse_events.mt_events') IS NOT NULL THEN
                        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_mt_events_type ON warehouse_events.mt_events (type);';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('warehouse_events.mt_events') IS NOT NULL THEN
                        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_mt_events_timestamp ON warehouse_events.mt_events ("timestamp");';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('warehouse_events.mt_doc_availablestockview') IS NOT NULL THEN
                        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_available_stock_item_location ON warehouse_events.mt_doc_availablestockview ((data ->> ''itemId''), (data ->> ''location''));';
                    END IF;
                END
                $$;
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

            RenameIndexIfNeeded(migrationBuilder, "idx_shipments_tracking_number", "IX_shipments_TrackingNumber");
            RenameIndexIfNeeded(migrationBuilder, "idx_sales_orders_order_date", "IX_sales_orders_OrderDate");
            RenameIndexIfNeeded(migrationBuilder, "idx_on_hand_value_category_id", "IX_on_hand_value_CategoryId");
            RenameIndexIfNeeded(migrationBuilder, "idx_items_category_id", "IX_items_CategoryId");
        }

        private static void RenameIndexIfNeeded(MigrationBuilder migrationBuilder, string oldName, string newName)
        {
            migrationBuilder.Sql(
                $$"""
                DO $$
                BEGIN
                    IF to_regclass('public."{{oldName}}"') IS NOT NULL
                       AND to_regclass('public."{{newName}}"') IS NULL THEN
                        ALTER INDEX public."{{oldName}}" RENAME TO "{{newName}}";
                    END IF;
                END
                $$;
                """);
        }
    }
}
