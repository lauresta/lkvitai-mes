using System;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(WarehouseDbContext))]
    [Migration("20260520100000_AddAgnumBalanceDistributions")]
    public partial class AddAgnumBalanceDistributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agnum_balance_distributions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VirtualBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SndId = table.Column<int>(type: "integer", nullable: false),
                    AgnumProductId = table.Column<int>(type: "integer", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StockMovementCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistributedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DistributedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_balance_distributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agnum_balance_distributions_agnum_virtual_warehouse_bala~",
                        column: x => x.VirtualBalanceId,
                        principalSchema: "public",
                        principalTable: "agnum_virtual_warehouse_balances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_balance_distributions_SndId_AgnumProductId",
                schema: "public",
                table: "agnum_balance_distributions",
                columns: new[] { "SndId", "AgnumProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_balance_distributions_StockMovementCommandId",
                schema: "public",
                table: "agnum_balance_distributions",
                column: "StockMovementCommandId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agnum_balance_distributions_VirtualBalanceId",
                schema: "public",
                table: "agnum_balance_distributions",
                column: "VirtualBalanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agnum_balance_distributions",
                schema: "public");
        }
    }
}
