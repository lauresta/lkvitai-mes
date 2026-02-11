using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgnumExportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agnum_export_configs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Schedule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApiEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApiKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_export_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agnum_export_history",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_export_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agnum_export_history_agnum_export_configs_ExportConfigId",
                        column: x => x.ExportConfigId,
                        principalSchema: "public",
                        principalTable: "agnum_export_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agnum_mappings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgnumExportConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AgnumAccountCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agnum_mappings_agnum_export_configs_AgnumExportConfigId",
                        column: x => x.AgnumExportConfigId,
                        principalSchema: "public",
                        principalTable: "agnum_export_configs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_export_configs_IsActive",
                schema: "public",
                table: "agnum_export_configs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_export_history_ExportConfigId",
                schema: "public",
                table: "agnum_export_history",
                column: "ExportConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_export_history_ExportedAt",
                schema: "public",
                table: "agnum_export_history",
                column: "ExportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_export_history_ExportNumber",
                schema: "public",
                table: "agnum_export_history",
                column: "ExportNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agnum_export_history_Status",
                schema: "public",
                table: "agnum_export_history",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_agnum_mappings_AgnumExportConfigId_SourceType_SourceValue",
                schema: "public",
                table: "agnum_mappings",
                columns: new[] { "AgnumExportConfigId", "SourceType", "SourceValue" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agnum_export_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_mappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_export_configs",
                schema: "public");
        }
    }
}
