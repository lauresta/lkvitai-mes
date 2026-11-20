using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgnumOutboxW4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgnumKey",
                schema: "public",
                table: "inbound_shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualReason",
                schema: "public",
                table: "inbound_shipments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostingSnapshotJson",
                schema: "public",
                table: "inbound_shipments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgnumKey",
                schema: "public",
                table: "additional_cost_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualReason",
                schema: "public",
                table: "additional_cost_documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostingRevision",
                schema: "public",
                table: "additional_cost_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PostingSnapshotJson",
                schema: "public",
                table: "additional_cost_documents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agnum_document_outbox",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LeasedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AgnumDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    AgnumOperationId = table.Column<long>(type: "bigint", nullable: true),
                    AppliedPostingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_document_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agnum_reconciliation_findings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DocumentId = table.Column<int>(type: "integer", nullable: true),
                    Key = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_reconciliation_findings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agnum_sending_settings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastReconciledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReconciliationFindings = table.Column<int>(type: "integer", nullable: false),
                    LastReconciliationError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agnum_sending_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_document_outbox_DocumentType_DocumentId",
                schema: "public",
                table: "agnum_document_outbox",
                columns: new[] { "DocumentType", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_document_outbox_Key_Revision_Operation",
                schema: "public",
                table: "agnum_document_outbox",
                columns: new[] { "Key", "Revision", "Operation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agnum_document_outbox_Status_NextAttemptAt",
                schema: "public",
                table: "agnum_document_outbox",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agnum_document_outbox_SupplierId_Id",
                schema: "public",
                table: "agnum_document_outbox",
                columns: new[] { "SupplierId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agnum_document_outbox",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_reconciliation_findings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "agnum_sending_settings",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "AgnumKey",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "ManualReason",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "PostingSnapshotJson",
                schema: "public",
                table: "inbound_shipments");

            migrationBuilder.DropColumn(
                name: "AgnumKey",
                schema: "public",
                table: "additional_cost_documents");

            migrationBuilder.DropColumn(
                name: "ManualReason",
                schema: "public",
                table: "additional_cost_documents");

            migrationBuilder.DropColumn(
                name: "PostingRevision",
                schema: "public",
                table: "additional_cost_documents");

            migrationBuilder.DropColumn(
                name: "PostingSnapshotJson",
                schema: "public",
                table: "additional_cost_documents");
        }
    }
}
