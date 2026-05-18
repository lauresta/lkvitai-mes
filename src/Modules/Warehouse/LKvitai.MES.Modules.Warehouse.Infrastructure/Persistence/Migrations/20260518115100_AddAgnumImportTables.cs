using System;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(WarehouseDbContext))]
    [Migration("20260518115100_AddAgnumImportTables")]
    public partial class AddAgnumImportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agnum_balance_import_runs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SndId = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductCount = table.Column<int>(type: "integer", nullable: false),
                    BalanceCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_balance_import_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agnum_warehouse_mappings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SndId = table.Column<int>(type: "integer", nullable: false),
                    AgnumName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MesVirtualWarehouseCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiKeyConfigName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsImportEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_warehouse_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agnum_product_links",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SndId = table.Column<int>(type: "integer", nullable: false),
                    AgnumProductId = table.Column<int>(type: "integer", nullable: false),
                    AgnumCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AgnumEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AgnumModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RawHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_product_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agnum_product_links_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agnum_virtual_warehouse_balances",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SndId = table.Column<int>(type: "integer", nullable: false),
                    AgnumProductId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Uom = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_virtual_warehouse_balances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agnum_virtual_warehouse_balances_agnum_balance_import_ru~",
                        column: x => x.ImportRunId,
                        principalSchema: "public",
                        principalTable: "agnum_balance_import_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agnum_virtual_warehouse_balances_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_external_attributes",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceContext = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValueText = table.Column<string>(type: "text", nullable: true),
                    ValueNumber = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_external_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_external_attributes_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "agnum_warehouse_mappings",
                columns: new[] { "Id", "AgnumName", "ApiKeyConfigName", "CreatedAt", "CreatedBy", "IsImportEnabled", "MesVirtualWarehouseCode", "SndId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "Sandėlys", "sandelys", new DateTimeOffset(new DateTime(2026, 2, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero), "system", true, "AGNUM-493", 493, null, null },
                    { 2, "Pardavimai", "pardavimai", new DateTimeOffset(new DateTime(2026, 2, 13, 0, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero), "system", true, "AGNUM-496", 496, null, null }
                });

            migrationBuilder.Sql(
                "SELECT setval(pg_get_serial_sequence('public.agnum_warehouse_mappings', 'Id'), 2, true);");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_product_links_ItemId",
                schema: "public",
                table: "agnum_product_links",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_product_links_SndId_AgnumCode",
                schema: "public",
                table: "agnum_product_links",
                columns: new[] { "SndId", "AgnumCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agnum_product_links_SndId_AgnumProductId",
                schema: "public",
                table: "agnum_product_links",
                columns: new[] { "SndId", "AgnumProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agnum_virtual_warehouse_balances_ImportRunId",
                schema: "public",
                table: "agnum_virtual_warehouse_balances",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_virtual_warehouse_balances_ImportRunId_SndId_Agnum~",
                schema: "public",
                table: "agnum_virtual_warehouse_balances",
                columns: new[] { "ImportRunId", "SndId", "AgnumProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agnum_virtual_warehouse_balances_ItemId",
                schema: "public",
                table: "agnum_virtual_warehouse_balances",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_virtual_warehouse_balances_SndId_AgnumProductId",
                schema: "public",
                table: "agnum_virtual_warehouse_balances",
                columns: new[] { "SndId", "AgnumProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_warehouse_mappings_SndId",
                schema: "public",
                table: "agnum_warehouse_mappings",
                column: "SndId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_external_attributes_ItemId",
                schema: "public",
                table: "item_external_attributes",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_external_attributes_ItemId_SourceSystem_SourceContext_Key",
                schema: "public",
                table: "item_external_attributes",
                columns: new[] { "ItemId", "SourceSystem", "SourceContext", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agnum_product_links",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_virtual_warehouse_balances",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_warehouse_mappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_external_attributes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_balance_import_runs",
                schema: "public");
        }
    }
}
