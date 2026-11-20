using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgnumAccountingConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAgnumCreditor",
                schema: "public",
                table: "suppliers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "public",
                table: "suppliers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MissingInAgnum",
                schema: "public",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StockAccountProfileId",
                schema: "public",
                table: "item_categories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accounting_config_audit",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Entity = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BeforeJson = table.Column<string>(type: "jsonb", nullable: true),
                    AfterJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_config_audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_account_profiles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StockAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AgnumSndId = table.Column<int>(type: "integer", nullable: false),
                    PriceDifferenceAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WriteOffAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MarkdownAccount = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_account_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agnum_posting_rules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockAccountProfileId = table.Column<int>(type: "integer", nullable: false),
                    VatTreatment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SchemeId = table.Column<int>(type: "integer", nullable: false),
                    VatCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_posting_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agnum_posting_rules_stock_account_profiles_StockAccountProf~",
                        column: x => x.StockAccountProfileId,
                        principalSchema: "public",
                        principalTable: "stock_account_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_StockAccountProfileId",
                schema: "public",
                table: "item_categories",
                column: "StockAccountProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_config_audit_ChangedAt",
                schema: "public",
                table: "accounting_config_audit",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_posting_rules_StockAccountProfileId_VatTreatment",
                schema: "public",
                table: "agnum_posting_rules",
                columns: new[] { "StockAccountProfileId", "VatTreatment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_account_profiles_IsDefault",
                schema: "public",
                table: "stock_account_profiles",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_stock_account_profiles_Name",
                schema: "public",
                table: "stock_account_profiles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_item_categories_stock_account_profiles_StockAccountProfileId",
                schema: "public",
                table: "item_categories",
                column: "StockAccountProfileId",
                principalSchema: "public",
                principalTable: "stock_account_profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_item_categories_stock_account_profiles_StockAccountProfileId",
                schema: "public",
                table: "item_categories");

            migrationBuilder.DropTable(
                name: "accounting_config_audit",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_posting_rules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "stock_account_profiles",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_item_categories_StockAccountProfileId",
                schema: "public",
                table: "item_categories");

            migrationBuilder.DropColumn(
                name: "IsAgnumCreditor",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "MissingInAgnum",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "StockAccountProfileId",
                schema: "public",
                table: "item_categories");
        }
    }
}
