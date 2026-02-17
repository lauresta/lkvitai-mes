using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1640_DisasterRecoveryPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dr_drills",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DrillStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DrillCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Scenario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActualRTO = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IssuesIdentifiedJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dr_drills", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dr_drills_DrillStartedAt",
                schema: "public",
                table: "dr_drills",
                column: "DrillStartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_dr_drills_Scenario",
                schema: "public",
                table: "dr_drills",
                column: "Scenario");

            migrationBuilder.CreateIndex(
                name: "IX_dr_drills_Status",
                schema: "public",
                table: "dr_drills",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dr_drills",
                schema: "public");
        }
    }
}
