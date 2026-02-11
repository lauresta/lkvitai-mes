using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleCounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "cycle_count_number_seq",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "cycle_counts",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'CC-' || LPAD(nextval('cycle_count_number_seq')::text, 4, '0')"),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ScheduledDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CountedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScheduleCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplyAdjustmentCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_counts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cycle_count_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SystemQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PhysicalQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_count_lines", x => x.Id);
                    table.CheckConstraint("ck_cycle_count_lines_physical_qty", "\"PhysicalQty\" >= 0");
                    table.CheckConstraint("ck_cycle_count_lines_system_qty", "\"SystemQty\" >= 0");
                    table.ForeignKey(
                        name: "FK_cycle_count_lines_cycle_counts_CycleCountId",
                        column: x => x.CycleCountId,
                        principalSchema: "public",
                        principalTable: "cycle_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cycle_count_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cycle_count_lines_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "public",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cycle_count_lines_CycleCountId",
                schema: "public",
                table: "cycle_count_lines",
                column: "CycleCountId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_count_lines_ItemId",
                schema: "public",
                table: "cycle_count_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_count_lines_LocationId",
                schema: "public",
                table: "cycle_count_lines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_counts_CountNumber",
                schema: "public",
                table: "cycle_counts",
                column: "CountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_counts_ScheduledDate",
                schema: "public",
                table: "cycle_counts",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_counts_Status",
                schema: "public",
                table: "cycle_counts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cycle_count_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "cycle_counts",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "cycle_count_number_seq",
                schema: "public");
        }
    }
}
