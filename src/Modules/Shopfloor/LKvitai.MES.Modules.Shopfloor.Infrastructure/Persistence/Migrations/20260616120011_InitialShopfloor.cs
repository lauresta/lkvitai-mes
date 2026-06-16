using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialShopfloor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shopfloor");

            migrationBuilder.CreateTable(
                name: "legacy_product_types",
                schema: "shopfloor",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_product_types", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "work_centers",
                schema: "shopfloor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_centers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_templates",
                schema: "shopfloor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    graph_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_stations",
                schema: "shopfloor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    work_center_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wip_limit = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_stations", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_stations_work_centers_work_center_id",
                        column: x => x.work_center_id,
                        principalSchema: "shopfloor",
                        principalTable: "work_centers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_type_workflow_maps",
                schema: "shopfloor",
                columns: table => new
                {
                    legacy_product_type_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    workflow_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_type_workflow_maps", x => x.legacy_product_type_code);
                    table.ForeignKey(
                        name: "FK_product_type_workflow_maps_legacy_product_types_legacy_prod~",
                        column: x => x.legacy_product_type_code,
                        principalSchema: "shopfloor",
                        principalTable: "legacy_product_types",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_type_workflow_maps_workflow_templates_workflow_temp~",
                        column: x => x.workflow_template_id,
                        principalSchema: "shopfloor",
                        principalTable: "workflow_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_product_types_removed_at",
                schema: "shopfloor",
                table: "legacy_product_types",
                column: "removed_at");

            migrationBuilder.CreateIndex(
                name: "IX_product_type_workflow_maps_workflow_template_id",
                schema: "shopfloor",
                table: "product_type_workflow_maps",
                column: "workflow_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_centers_code",
                schema: "shopfloor",
                table: "work_centers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_stations_code",
                schema: "shopfloor",
                table: "work_stations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_stations_work_center_id",
                schema: "shopfloor",
                table: "work_stations",
                column: "work_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_templates_code",
                schema: "shopfloor",
                table: "workflow_templates",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_type_workflow_maps",
                schema: "shopfloor");

            migrationBuilder.DropTable(
                name: "work_stations",
                schema: "shopfloor");

            migrationBuilder.DropTable(
                name: "legacy_product_types",
                schema: "shopfloor");

            migrationBuilder.DropTable(
                name: "workflow_templates",
                schema: "shopfloor");

            migrationBuilder.DropTable(
                name: "work_centers",
                schema: "shopfloor");
        }
    }
}
