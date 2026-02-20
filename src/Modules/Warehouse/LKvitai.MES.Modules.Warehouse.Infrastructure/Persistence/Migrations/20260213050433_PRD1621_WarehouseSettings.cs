using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1621_WarehouseSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "warehouse_settings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CapacityThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    DefaultPickStrategy = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LowStockThreshold = table.Column<int>(type: "integer", nullable: false),
                    ReorderPoint = table.Column<int>(type: "integer", nullable: false),
                    AutoAllocateOrders = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_settings", x => x.Id);
                    table.CheckConstraint("ck_warehouse_settings_capacity", "\"CapacityThresholdPercent\" >= 0 AND \"CapacityThresholdPercent\" <= 100");
                    table.CheckConstraint("ck_warehouse_settings_low_stock", "\"LowStockThreshold\" >= 0");
                    table.CheckConstraint("ck_warehouse_settings_reorder_point", "\"ReorderPoint\" >= 0");
                    table.CheckConstraint("ck_warehouse_settings_singleton", "\"Id\" = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "warehouse_settings",
                schema: "public");
        }
    }
}
