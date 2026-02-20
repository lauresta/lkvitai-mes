using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseVisualizationLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aisle",
                schema: "public",
                table: "locations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bin",
                schema: "public",
                table: "locations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapacityVolume",
                schema: "public",
                table: "locations",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapacityWeight",
                schema: "public",
                table: "locations",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoordinateX",
                schema: "public",
                table: "locations",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoordinateY",
                schema: "public",
                table: "locations",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoordinateZ",
                schema: "public",
                table: "locations",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                schema: "public",
                table: "locations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rack",
                schema: "public",
                table: "locations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "warehouse_layouts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WidthMeters = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    LengthMeters = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    HeightMeters = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_layouts", x => x.Id);
                    table.CheckConstraint("ck_warehouse_layouts_height", "\"HeightMeters\" > 0");
                    table.CheckConstraint("ck_warehouse_layouts_length", "\"LengthMeters\" > 0");
                    table.CheckConstraint("ck_warehouse_layouts_width", "\"WidthMeters\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "zone_definitions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseLayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    X1 = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    Y1 = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    X2 = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    Y2 = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zone_definitions", x => x.Id);
                    table.CheckConstraint("ck_zone_definitions_bounds_x", "\"X2\" > \"X1\"");
                    table.CheckConstraint("ck_zone_definitions_bounds_y", "\"Y2\" > \"Y1\"");
                    table.CheckConstraint("ck_zone_definitions_type", "\"ZoneType\" IN ('RECEIVING','STORAGE','SHIPPING','QUARANTINE')");
                    table.ForeignKey(
                        name: "FK_zone_definitions_warehouse_layouts_WarehouseLayoutId",
                        column: x => x.WarehouseLayoutId,
                        principalSchema: "public",
                        principalTable: "warehouse_layouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_capacity_volume",
                schema: "public",
                table: "locations",
                sql: "\"CapacityVolume\" IS NULL OR \"CapacityVolume\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_capacity_weight",
                schema: "public",
                table: "locations",
                sql: "\"CapacityWeight\" IS NULL OR \"CapacityWeight\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_layouts_WarehouseCode",
                schema: "public",
                table: "warehouse_layouts",
                column: "WarehouseCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zone_definitions_WarehouseLayoutId",
                schema: "public",
                table: "zone_definitions",
                column: "WarehouseLayoutId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zone_definitions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_layouts",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_capacity_volume",
                schema: "public",
                table: "locations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_capacity_weight",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Aisle",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Bin",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "CapacityVolume",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "CapacityWeight",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "CoordinateX",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "CoordinateY",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "CoordinateZ",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "public",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Rack",
                schema: "public",
                table: "locations");
        }
    }
}
