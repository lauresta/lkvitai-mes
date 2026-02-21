using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPickTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pick_tasks",
                schema: "public",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PickedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    FromLocationId = table.Column<int>(type: "integer", nullable: true),
                    ToLocationId = table.Column<int>(type: "integer", nullable: true),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    AssignedToUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pick_tasks", x => x.TaskId);
                    table.CheckConstraint("ck_pick_tasks_status", "\"Status\" IN ('Pending','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_pick_tasks_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_tasks_locations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalSchema: "public",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_tasks_locations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalSchema: "public",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pick_tasks_lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "public",
                        principalTable: "lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_FromLocationId",
                schema: "public",
                table: "pick_tasks",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_ItemId",
                schema: "public",
                table: "pick_tasks",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_LotId",
                schema: "public",
                table: "pick_tasks",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_OrderId",
                schema: "public",
                table: "pick_tasks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_Status",
                schema: "public",
                table: "pick_tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_pick_tasks_ToLocationId",
                schema: "public",
                table: "pick_tasks",
                column: "ToLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pick_tasks",
                schema: "public");
        }
    }
}
