using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1627_MfaImplementation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_mfa",
                schema: "public",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotpSecret = table.Column<string>(type: "text", nullable: false),
                    MfaEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MfaEnrolledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BackupCodes = table.Column<string>(type: "text", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_mfa", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_mfa_LockedUntil",
                schema: "public",
                table: "user_mfa",
                column: "LockedUntil");

            migrationBuilder.CreateIndex(
                name: "IX_user_mfa_MfaEnabled",
                schema: "public",
                table: "user_mfa",
                column: "MfaEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_mfa",
                schema: "public");
        }
    }
}
