using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PRD1643_ItemPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS public.item_photos
                (
                    "Id" uuid NOT NULL,
                    "ItemId" integer NOT NULL,
                    "OriginalKey" character varying(500) NOT NULL,
                    "ThumbKey" character varying(500) NOT NULL,
                    "ContentType" character varying(100) NOT NULL,
                    "SizeBytes" bigint NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "IsPrimary" boolean NOT NULL,
                    "Tags" character varying(500) NULL,
                    "ImageEmbedding" text NULL,
                    CONSTRAINT "PK_item_photos" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_item_photos_items_ItemId" FOREIGN KEY ("ItemId")
                        REFERENCES public.items ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') THEN
                        ALTER TABLE public.item_photos
                            ALTER COLUMN "ImageEmbedding" TYPE vector(512)
                            USING "ImageEmbedding"::vector;
                    END IF;
                EXCEPTION
                    WHEN undefined_object THEN
                        NULL;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_item_photos_ItemId",
                schema: "public",
                table: "item_photos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_photos_ItemId_IsPrimary",
                schema: "public",
                table: "item_photos",
                columns: new[] { "ItemId", "IsPrimary" },
                unique: true,
                filter: "\"IsPrimary\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_photos",
                schema: "public");
        }
    }
}
