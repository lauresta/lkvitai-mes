using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1622_ReasonCodeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "adjustment_reason_codes",
                schema: "public",
                newName: "adjustment_reason_codes_legacy",
                newSchema: "public");

            migrationBuilder.Sql(
                """
                ALTER TABLE public.adjustment_reason_codes_legacy
                RENAME CONSTRAINT "PK_adjustment_reason_codes" TO "PK_adjustment_reason_codes_legacy";
                """);

            migrationBuilder.CreateTable(
                name: "adjustment_reason_codes",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustment_reason_codes", x => x.Id);
                    table.CheckConstraint("ck_adjustment_reason_codes_category", "\"Category\" IN ('ADJUSTMENT','REVALUATION','WRITEDOWN','RETURN')");
                    table.CheckConstraint("ck_adjustment_reason_codes_usage_count", "\"UsageCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_adjustment_reason_codes_adjustment_reason_codes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "public",
                        principalTable: "adjustment_reason_codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO public.adjustment_reason_codes
                    ("Code", "Name", "Description", "ParentId", "Category", "Active", "UsageCount", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy")
                SELECT
                    UPPER("Code"),
                    "Name",
                    NULL,
                    NULL,
                    'ADJUSTMENT',
                    COALESCE("IsActive", TRUE),
                    0,
                    NOW(),
                    NULL,
                    'migration',
                    NULL
                FROM public.adjustment_reason_codes_legacy;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_reason_codes_Active",
                schema: "public",
                table: "adjustment_reason_codes",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_reason_codes_Category",
                schema: "public",
                table: "adjustment_reason_codes",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_reason_codes_Code",
                schema: "public",
                table: "adjustment_reason_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_reason_codes_ParentId",
                schema: "public",
                table: "adjustment_reason_codes",
                column: "ParentId");

            migrationBuilder.DropTable(
                name: "adjustment_reason_codes_legacy",
                schema: "public");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adjustment_reason_codes_legacy",
                schema: "public",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustment_reason_codes_legacy", x => x.Code);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO public.adjustment_reason_codes_legacy ("Code", "Name", "IsActive")
                SELECT
                    LEFT("Code", 50),
                    "Name",
                    "Active"
                FROM public.adjustment_reason_codes;
                """);

            migrationBuilder.DropTable(
                name: "adjustment_reason_codes",
                schema: "public");

            migrationBuilder.RenameTable(
                name: "adjustment_reason_codes_legacy",
                schema: "public",
                newName: "adjustment_reason_codes",
                newSchema: "public");
        }
    }
}
