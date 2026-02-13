using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1630_SecurityAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_audit_logs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Resource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_Action_Resource",
                schema: "public",
                table: "security_audit_logs",
                columns: new[] { "Action", "Resource" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_Timestamp",
                schema: "public",
                table: "security_audit_logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_UserId",
                schema: "public",
                table: "security_audit_logs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_audit_logs",
                schema: "public");
        }
    }
}
