using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// C8c: virtual transit cell for the one-off warehouse consolidation. It is virtual, so it never counts
/// as physical stock, and (like AGNUM) the consolidation writes it without a non-negative balance check.
/// </summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20261112090000_AddWarehouseMergeLocation")]
public sealed class AddWarehouseMergeLocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO public.locations ("Code", "Barcode", "Type", "IsVirtual", "Status", "CreatedAt", "CreatedBy")
        VALUES ('WAREHOUSE_MERGE', 'VIRTUAL-WAREHOUSE_MERGE', 'Zone', true, 'Active', CURRENT_TIMESTAMP, 'migration:C8c-WAREHOUSE_MERGE')
        ON CONFLICT ("Code") DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM public.locations WHERE "Code" = 'WAREHOUSE_MERGE' AND "CreatedBy" = 'migration:C8c-WAREHOUSE_MERGE';
        """);
}
