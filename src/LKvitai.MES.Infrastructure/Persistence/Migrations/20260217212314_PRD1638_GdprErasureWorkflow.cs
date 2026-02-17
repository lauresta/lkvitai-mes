using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1638_GdprErasureWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gdpr_erasure_requests",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gdpr_erasure_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gdpr_erasure_requests_CustomerId",
                schema: "public",
                table: "gdpr_erasure_requests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_gdpr_erasure_requests_RequestedAt",
                schema: "public",
                table: "gdpr_erasure_requests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_gdpr_erasure_requests_Status",
                schema: "public",
                table: "gdpr_erasure_requests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gdpr_erasure_requests",
                schema: "public");
        }
    }
}
