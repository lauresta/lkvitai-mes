using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1619_TransferWorkflowEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutedBy",
                schema: "public",
                table: "transfers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmitCommandId",
                schema: "public",
                table: "transfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                schema: "public",
                table: "transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LotId",
                schema: "public",
                table: "transfer_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbcClass",
                schema: "public",
                table: "cycle_counts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "ALL");

            migrationBuilder.AddColumn<string>(
                name: "AssignedOperator",
                schema: "public",
                table: "cycle_counts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AdjustmentApprovedAt",
                schema: "public",
                table: "cycle_count_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentApprovedBy",
                schema: "public",
                table: "cycle_count_lines",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CountedAt",
                schema: "public",
                table: "cycle_count_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountedBy",
                schema: "public",
                table: "cycle_count_lines",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_LotId",
                schema: "public",
                table: "transfer_lines",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_counts_AssignedOperator",
                schema: "public",
                table: "cycle_counts",
                column: "AssignedOperator");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transfer_lines_LotId",
                schema: "public",
                table: "transfer_lines");

            migrationBuilder.DropIndex(
                name: "IX_cycle_counts_AssignedOperator",
                schema: "public",
                table: "cycle_counts");

            migrationBuilder.DropColumn(
                name: "ExecutedBy",
                schema: "public",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "SubmitCommandId",
                schema: "public",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                schema: "public",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "LotId",
                schema: "public",
                table: "transfer_lines");

            migrationBuilder.DropColumn(
                name: "AbcClass",
                schema: "public",
                table: "cycle_counts");

            migrationBuilder.DropColumn(
                name: "AssignedOperator",
                schema: "public",
                table: "cycle_counts");

            migrationBuilder.DropColumn(
                name: "AdjustmentApprovedAt",
                schema: "public",
                table: "cycle_count_lines");

            migrationBuilder.DropColumn(
                name: "AdjustmentApprovedBy",
                schema: "public",
                table: "cycle_count_lines");

            migrationBuilder.DropColumn(
                name: "CountedAt",
                schema: "public",
                table: "cycle_count_lines");

            migrationBuilder.DropColumn(
                name: "CountedBy",
                schema: "public",
                table: "cycle_count_lines");
        }
    }
}
