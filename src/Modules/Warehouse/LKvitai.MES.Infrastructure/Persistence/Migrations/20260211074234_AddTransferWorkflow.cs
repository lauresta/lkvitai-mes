using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_type",
                schema: "public",
                table: "locations");

            migrationBuilder.CreateSequence(
                name: "transfer_number_seq",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "transfers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'TRF-' || LPAD(nextval('transfer_number_seq')::text, 4, '0')"),
                    FromWarehouse = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToWarehouse = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreateCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproveCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecuteCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.Id);
                    table.CheckConstraint("ck_transfers_from_to_not_equal", "\"FromWarehouse\" <> \"ToWarehouse\"");
                    table.CheckConstraint("ck_transfers_from_warehouse", "\"FromWarehouse\" <> ''");
                    table.CheckConstraint("ck_transfers_to_warehouse", "\"ToWarehouse\" <> ''");
                });

            migrationBuilder.CreateTable(
                name: "transfer_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    FromLocationId = table.Column<int>(type: "integer", nullable: false),
                    ToLocationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_lines", x => x.Id);
                    table.CheckConstraint("ck_transfer_lines_locations", "\"FromLocationId\" <> \"ToLocationId\"");
                    table.CheckConstraint("ck_transfer_lines_qty", "\"Qty\" > 0");
                    table.ForeignKey(
                        name: "FK_transfer_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_lines_locations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalSchema: "public",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_lines_locations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalSchema: "public",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfer_lines_transfers_TransferId",
                        column: x => x.TransferId,
                        principalSchema: "public",
                        principalTable: "transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_type",
                schema: "public",
                table: "locations",
                sql: "\"Type\" IN ('Warehouse','Zone','Aisle','Rack','Shelf','Bin','Virtual')");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_FromLocationId",
                schema: "public",
                table: "transfer_lines",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_ItemId",
                schema: "public",
                table: "transfer_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_ToLocationId",
                schema: "public",
                table: "transfer_lines",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_transfer_lines_TransferId",
                schema: "public",
                table: "transfer_lines",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_FromWarehouse",
                schema: "public",
                table: "transfers",
                column: "FromWarehouse");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_RequestedAt",
                schema: "public",
                table: "transfers",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_Status",
                schema: "public",
                table: "transfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_ToWarehouse",
                schema: "public",
                table: "transfers",
                column: "ToWarehouse");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_TransferNumber",
                schema: "public",
                table: "transfers",
                column: "TransferNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfer_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "transfers",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "ck_locations_type",
                schema: "public",
                table: "locations");

            migrationBuilder.DropSequence(
                name: "transfer_number_seq",
                schema: "public");

            migrationBuilder.AddCheckConstraint(
                name: "ck_locations_type",
                schema: "public",
                table: "locations",
                sql: "\"Type\" IN ('Warehouse','Zone','Aisle','Rack','Shelf','Bin')");
        }
    }
}
