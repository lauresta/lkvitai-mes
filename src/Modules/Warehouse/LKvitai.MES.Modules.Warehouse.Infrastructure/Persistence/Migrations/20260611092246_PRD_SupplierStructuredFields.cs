using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD_SupplierStructuredFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Absorbs the earlier 20260519163000_AddAgnumClientIdsToPartners migration, which never
            // executed under Database.Migrate() because it shipped without a .Designer.cs (no [Migration]
            // attribute, so EF never registered it). Idempotent so it is safe on databases that were
            // provisioned via EnsureCreated() and already have these columns.
            migrationBuilder.Sql(@"ALTER TABLE public.suppliers ADD COLUMN IF NOT EXISTS ""AgnumClientId"" integer;");
            migrationBuilder.Sql(@"ALTER TABLE public.customers ADD COLUMN IF NOT EXISTS ""AgnumClientId"" integer;");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_suppliers_AgnumClientId"" ON public.suppliers (""AgnumClientId"");");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_customers_AgnumClientId"" ON public.customers (""AgnumClientId"");");

            migrationBuilder.AddColumn<string>(
                name: "AdditionalInfo",
                schema: "public",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "public",
                table: "suppliers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCode",
                schema: "public",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                schema: "public",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "public",
                table: "suppliers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "public",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAgnumSyncedAt",
                schema: "public",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "public",
                table: "suppliers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupAddress",
                schema: "public",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisteredAddress",
                schema: "public",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                schema: "public",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatCode",
                schema: "public",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "public",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_Country",
                schema: "public",
                table: "suppliers",
                column: "Country");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_suppliers_Country",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "AdditionalInfo",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "CompanyCode",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "ContactName",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "LastAgnumSyncedAt",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "PickupAddress",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "RegisteredAddress",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "ShortName",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "VatCode",
                schema: "public",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "Website",
                schema: "public",
                table: "suppliers");

            migrationBuilder.Sql(@"DROP INDEX IF EXISTS public.""IX_suppliers_AgnumClientId"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS public.""IX_customers_AgnumClientId"";");
            migrationBuilder.Sql(@"ALTER TABLE public.suppliers DROP COLUMN IF EXISTS ""AgnumClientId"";");
            migrationBuilder.Sql(@"ALTER TABLE public.customers DROP COLUMN IF EXISTS ""AgnumClientId"";");
        }
    }
}
