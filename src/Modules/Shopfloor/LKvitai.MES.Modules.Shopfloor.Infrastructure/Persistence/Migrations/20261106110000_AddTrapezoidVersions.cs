using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ShopfloorDbContext))]
[Migration("20261106110000_AddTrapezoidVersions")]
public sealed class AddTrapezoidVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE shopfloor.trapezoid_versions (
            task_id integer NOT NULL,
            product_id bigint NOT NULL,
            version integer NOT NULL,
            request_id uuid NOT NULL,
            source_revision char(64) NOT NULL,
            snapshot_json jsonb NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            created_by text NOT NULL,
            pdf_bytes bytea,
            PRIMARY KEY (task_id,product_id,version),
            UNIQUE (task_id,product_id,request_id)
        );
        CREATE INDEX trapezoid_versions_product_idx
            ON shopfloor.trapezoid_versions(task_id,product_id,version DESC);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DROP TABLE shopfloor.trapezoid_versions;");
}
