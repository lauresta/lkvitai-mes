using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1642_WarehouseDirectoryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "public",
                table: "warehouses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "public",
                table: "warehouses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "public",
                table: "warehouses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVirtual",
                schema: "public",
                table: "warehouses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "public",
                table: "warehouses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "public",
                table: "warehouses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE public.warehouses
                SET "CreatedAt" = NOW(),
                    "UpdatedAt" = NOW()
                WHERE "CreatedAt" IS NULL
                   OR "UpdatedAt" IS NULL;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "public",
                table: "warehouses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                schema: "public",
                table: "warehouses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_warehouses_status",
                schema: "public",
                table: "warehouses",
                sql: "\"Status\" IN ('Active','Inactive')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_warehouses_status",
                schema: "public",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "public",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "public",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "IsVirtual",
                schema: "public",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "public",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "public",
                table: "warehouses");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "public",
                table: "warehouses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
