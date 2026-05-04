using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Portal.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePortalTileStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE portal.portal_tiles
                SET "Status" = 'Live'
                WHERE "Status" = 'Active';
                """);

            migrationBuilder.Sql("""
                UPDATE portal.portal_tiles
                SET "Status" = 'Pilot'
                WHERE "Status" = 'Scaffolded';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE portal.portal_tiles
                SET "Status" = 'Active'
                WHERE "Status" = 'Live';
                """);

            migrationBuilder.Sql("""
                UPDATE portal.portal_tiles
                SET "Status" = 'Scaffolded'
                WHERE "Status" = 'Pilot';
                """);
        }
    }
}
