using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1636_RetentionPolicyEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LegalHold",
                schema: "public",
                table: "security_audit_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "audit_logs_archive",
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
                    LegalHold = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs_archive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "events_archive",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EventVersion = table.Column<long>(type: "bigint", nullable: false),
                    EventTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    LegalHold = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events_archive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retention_executions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordsArchived = table.Column<int>(type: "integer", nullable: false),
                    RecordsDeleted = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_executions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retention_policies",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RetentionPeriodDays = table.Column<int>(type: "integer", nullable: false),
                    ArchiveAfterDays = table.Column<int>(type: "integer", nullable: true),
                    DeleteAfterDays = table.Column<int>(type: "integer", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_policies", x => x.Id);
                    table.CheckConstraint("ck_retention_policies_archive_days", "\"ArchiveAfterDays\" IS NULL OR \"ArchiveAfterDays\" >= 0");
                    table.CheckConstraint("ck_retention_policies_delete_days", "\"DeleteAfterDays\" IS NULL OR \"DeleteAfterDays\" >= 0");
                    table.CheckConstraint("ck_retention_policies_retention_days", "\"RetentionPeriodDays\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_logs_LegalHold",
                schema: "public",
                table: "security_audit_logs",
                column: "LegalHold");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_archive_LegalHold",
                schema: "public",
                table: "audit_logs_archive",
                column: "LegalHold");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_archive_Timestamp",
                schema: "public",
                table: "audit_logs_archive",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_events_archive_EventTimestamp",
                schema: "public",
                table: "events_archive",
                column: "EventTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_events_archive_LegalHold",
                schema: "public",
                table: "events_archive",
                column: "LegalHold");

            migrationBuilder.CreateIndex(
                name: "IX_events_archive_StreamId",
                schema: "public",
                table: "events_archive",
                column: "StreamId");

            migrationBuilder.CreateIndex(
                name: "IX_retention_executions_ExecutedAt",
                schema: "public",
                table: "retention_executions",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_retention_policies_Active",
                schema: "public",
                table: "retention_policies",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_retention_policies_DataType",
                schema: "public",
                table: "retention_policies",
                column: "DataType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs_archive",
                schema: "public");

            migrationBuilder.DropTable(
                name: "events_archive",
                schema: "public");

            migrationBuilder.DropTable(
                name: "retention_executions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "retention_policies",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_security_audit_logs_LegalHold",
                schema: "public",
                table: "security_audit_logs");

            migrationBuilder.DropColumn(
                name: "LegalHold",
                schema: "public",
                table: "security_audit_logs");
        }
    }
}
