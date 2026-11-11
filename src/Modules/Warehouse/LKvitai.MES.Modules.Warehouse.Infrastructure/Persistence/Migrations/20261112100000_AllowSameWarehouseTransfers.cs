using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// C8: one physical warehouse. Transfers move stock between cells of the same warehouse, so the
/// "from warehouse differs from to warehouse" rule is replaced by the per-line cell check in the API.
/// </summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20261112100000_AllowSameWarehouseTransfers")]
public sealed class AllowSameWarehouseTransfers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropCheckConstraint(
            name: "ck_transfers_from_to_not_equal",
            schema: "public",
            table: "transfers");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddCheckConstraint(
            name: "ck_transfers_from_to_not_equal",
            schema: "public",
            table: "transfers",
            sql: "\"FromWarehouse\" <> \"ToWarehouse\"");
}
