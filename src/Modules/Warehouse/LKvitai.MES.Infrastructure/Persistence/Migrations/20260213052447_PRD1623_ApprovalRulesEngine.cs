using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1623_ApprovalRulesEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_rules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ThresholdType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ThresholdValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ApproverRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_rules", x => x.Id);
                    table.CheckConstraint("ck_approval_rules_priority", "\"Priority\" > 0");
                    table.CheckConstraint("ck_approval_rules_rule_type", "\"RuleType\" IN ('COST_ADJUSTMENT','WRITEDOWN','TRANSFER')");
                    table.CheckConstraint("ck_approval_rules_threshold", "\"ThresholdValue\" >= 0");
                    table.CheckConstraint("ck_approval_rules_threshold_type", "\"ThresholdType\" IN ('AMOUNT','PERCENTAGE')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_rules_Active",
                schema: "public",
                table: "approval_rules",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_approval_rules_Priority",
                schema: "public",
                table: "approval_rules",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_approval_rules_RuleType",
                schema: "public",
                table: "approval_rules",
                column: "RuleType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_rules",
                schema: "public");
        }
    }
}
