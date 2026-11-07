using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ShopfloorDbContext))]
[Migration("20261107190000_AddRoletaiPaperlessMarks")]
public sealed class AddRoletaiPaperlessMarks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE shopfloor.roletai_item_marks (
            product_id bigint NOT NULL,
            user_id uuid NOT NULL,
            step smallint NOT NULL CHECK (step BETWEEN 1 AND 3),
            is_marked boolean NOT NULL,
            updated_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (product_id,user_id,step)
        );
        CREATE TABLE shopfloor.roletai_item_signoffs (
            id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            product_id bigint NOT NULL,
            user_id uuid NOT NULL,
            user_display_name text NOT NULL,
            signed_at timestamptz NOT NULL DEFAULT now(),
            withdrawn_at timestamptz
        );
        CREATE UNIQUE INDEX roletai_item_signoffs_one_active
            ON shopfloor.roletai_item_signoffs(product_id,user_id) WHERE withdrawn_at IS NULL;
        CREATE INDEX roletai_item_signoffs_product
            ON shopfloor.roletai_item_signoffs(product_id,signed_at) WHERE withdrawn_at IS NULL;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE shopfloor.roletai_item_signoffs;
        DROP TABLE shopfloor.roletai_item_marks;
        """);
}
