using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemPricingAndPriceGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                schema: "public",
                table: "items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                schema: "public",
                table: "items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceGroupId",
                schema: "public",
                table: "customers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "price_groups",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "item_price_history",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PriceGroupId = table.Column<int>(type: "integer", nullable: true),
                    OldAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NewAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_price_history", x => x.Id);
                    table.CheckConstraint("ck_item_price_history_type", "\"PriceType\" IN ('Base','Purchase','GroupOverride')");
                    table.ForeignKey(
                        name: "FK_item_price_history_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_price_history_price_groups_PriceGroupId",
                        column: x => x.PriceGroupId,
                        principalSchema: "public",
                        principalTable: "price_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_price_overrides",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PriceGroupId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_price_overrides", x => x.Id);
                    table.CheckConstraint("ck_item_price_overrides_amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_item_price_overrides_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_price_overrides_price_groups_PriceGroupId",
                        column: x => x.PriceGroupId,
                        principalSchema: "public",
                        principalTable: "price_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_base_price",
                schema: "public",
                table: "items",
                sql: "\"BasePrice\" IS NULL OR \"BasePrice\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_purchase_price",
                schema: "public",
                table: "items",
                sql: "\"PurchasePrice\" IS NULL OR \"PurchasePrice\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_customers_PriceGroupId",
                schema: "public",
                table: "customers",
                column: "PriceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_item_price_history_ChangedAt",
                schema: "public",
                table: "item_price_history",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_item_price_history_ItemId",
                schema: "public",
                table: "item_price_history",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_price_history_PriceGroupId",
                schema: "public",
                table: "item_price_history",
                column: "PriceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_item_price_overrides_ItemId_PriceGroupId",
                schema: "public",
                table: "item_price_overrides",
                columns: new[] { "ItemId", "PriceGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_price_overrides_PriceGroupId",
                schema: "public",
                table: "item_price_overrides",
                column: "PriceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_price_groups_Code",
                schema: "public",
                table: "price_groups",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_price_groups_PriceGroupId",
                schema: "public",
                table: "customers",
                column: "PriceGroupId",
                principalSchema: "public",
                principalTable: "price_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_price_groups_PriceGroupId",
                schema: "public",
                table: "customers");

            migrationBuilder.DropTable(
                name: "item_price_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_price_overrides",
                schema: "public");

            migrationBuilder.DropTable(
                name: "price_groups",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "ck_items_base_price",
                schema: "public",
                table: "items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_items_purchase_price",
                schema: "public",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_customers_PriceGroupId",
                schema: "public",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                schema: "public",
                table: "items");

            migrationBuilder.DropColumn(
                name: "PriceGroupId",
                schema: "public",
                table: "customers");
        }
    }
}
