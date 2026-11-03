using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ShopfloorDbContext))]
[Migration("20261103120000_AddRoletaiReportTemplates")]
public sealed class AddRoletaiReportTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE shopfloor.production_report_templates (
                report_name text PRIMARY KEY,
                draft_json text NOT NULL,
                published_json text NOT NULL,
                published_version integer NOT NULL,
                updated_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE shopfloor.production_report_template_versions (
                report_name text NOT NULL,
                version integer NOT NULL,
                template_json text NOT NULL,
                published_at timestamptz NOT NULL,
                published_by text NOT NULL,
                PRIMARY KEY(report_name, version),
                FOREIGN KEY(report_name) REFERENCES shopfloor.production_report_templates(report_name)
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE shopfloor.production_report_template_versions;
            DROP TABLE shopfloor.production_report_templates;
            """);
    }
}
