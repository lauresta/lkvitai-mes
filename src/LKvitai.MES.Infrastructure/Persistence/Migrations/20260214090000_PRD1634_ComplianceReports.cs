using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    public partial class PRD1634_ComplianceReports : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scheduled_reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Schedule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmailRecipients = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "generated_report_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledReportId = table.Column<int>(type: "integer", nullable: true),
                    ReportType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_report_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_generated_report_history_scheduled_reports_ScheduledReportId",
                        column: x => x.ScheduledReportId,
                        principalTable: "scheduled_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generated_report_history_GeneratedAt",
                table: "generated_report_history",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_generated_report_history_ReportType",
                table: "generated_report_history",
                column: "ReportType");

            migrationBuilder.CreateIndex(
                name: "IX_generated_report_history_ScheduledReportId",
                table: "generated_report_history",
                column: "ScheduledReportId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reports_Active",
                table: "scheduled_reports",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_reports_ReportType",
                table: "scheduled_reports",
                column: "ReportType");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_report_history");

            migrationBuilder.DropTable(
                name: "scheduled_reports");
        }
    }
}
