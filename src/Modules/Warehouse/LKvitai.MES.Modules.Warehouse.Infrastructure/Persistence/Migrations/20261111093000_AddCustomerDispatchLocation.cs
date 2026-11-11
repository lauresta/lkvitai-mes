using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261111093000_AddCustomerDispatchLocation")]
public sealed class AddCustomerDispatchLocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO public.locations ("Code", "Barcode", "Type", "IsVirtual", "Status", "CreatedAt", "CreatedBy")
        VALUES ('CUSTOMER', 'VIRTUAL-CUSTOMER', 'Zone', true, 'Active', CURRENT_TIMESTAMP, 'migration:C2-CUSTOMER')
        ON CONFLICT ("Code") DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM public.locations WHERE "Code" = 'CUSTOMER' AND "CreatedBy" = 'migration:C2-CUSTOMER';
        """);
}
