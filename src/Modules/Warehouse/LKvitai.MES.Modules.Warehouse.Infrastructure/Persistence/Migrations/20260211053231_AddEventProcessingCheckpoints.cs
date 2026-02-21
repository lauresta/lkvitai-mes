using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventProcessingCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_processing_checkpoints",
                schema: "public",
                columns: table => new
                {
                    HandlerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StreamId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LastEventNumber = table.Column<long>(type: "bigint", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_processing_checkpoints", x => new { x.HandlerName, x.StreamId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_processing_checkpoints_ProcessedAt",
                schema: "public",
                table: "event_processing_checkpoints",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_processing_checkpoints",
                schema: "public");
        }
    }
}
