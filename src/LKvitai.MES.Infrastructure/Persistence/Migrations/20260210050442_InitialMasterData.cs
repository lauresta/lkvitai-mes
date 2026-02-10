using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "adjustment_reason_codes",
                schema: "public",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustment_reason_codes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "handling_unit_types",
                schema: "public",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handling_unit_types", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "handling_units",
                schema: "public",
                columns: table => new
                {
                    HUId = table.Column<Guid>(type: "uuid", nullable: false),
                    LPN = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SealedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handling_units", x => x.HUId);
                });

            migrationBuilder.CreateTable(
                name: "item_categories",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentCategoryId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_categories_item_categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalSchema: "public",
                        principalTable: "item_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParentLocationId = table.Column<int>(type: "integer", nullable: true),
                    IsVirtual = table.Column<bool>(type: "boolean", nullable: false),
                    MaxWeight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    MaxVolume = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    ZoneType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.Id);
                    table.CheckConstraint("ck_locations_max_volume", "\"MaxVolume\" IS NULL OR \"MaxVolume\" > 0");
                    table.CheckConstraint("ck_locations_max_weight", "\"MaxWeight\" IS NULL OR \"MaxWeight\" > 0");
                    table.CheckConstraint("ck_locations_status", "\"Status\" IN ('Active','Blocked','Maintenance')");
                    table.CheckConstraint("ck_locations_type", "\"Type\" IN ('Warehouse','Zone','Aisle','Rack','Shelf','Bin')");
                    table.CheckConstraint("ck_locations_zone_type", "\"ZoneType\" IS NULL OR \"ZoneType\" IN ('General','Refrigerated','Hazmat','Quarantine')");
                    table.ForeignKey(
                        name: "FK_locations_locations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalSchema: "public",
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sku_sequences",
                schema: "public",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NextValue = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sku_sequences", x => x.Prefix);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactInfo = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unit_of_measures",
                schema: "public",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_of_measures", x => x.Code);
                    table.CheckConstraint("ck_unit_of_measures_type", "\"Type\" IN ('Weight','Volume','Piece','Length')");
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "public",
                columns: table => new
                {
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.WarehouseId);
                });

            migrationBuilder.CreateTable(
                name: "handling_unit_lines",
                schema: "public",
                columns: table => new
                {
                    HUId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SKU = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handling_unit_lines", x => new { x.HUId, x.Id });
                    table.ForeignKey(
                        name: "FK_handling_unit_lines_handling_units_HUId",
                        column: x => x.HUId,
                        principalSchema: "public",
                        principalTable: "handling_units",
                        principalColumn: "HUId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inbound_shipments",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_shipments", x => x.Id);
                    table.CheckConstraint("ck_inbound_shipments_status", "\"Status\" IN ('Draft','Partial','Complete','Cancelled')");
                    table.ForeignKey(
                        name: "FK_inbound_shipments_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "public",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InternalSKU = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    BaseUoM = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    Volume = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    RequiresLotTracking = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresQC = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    PrimaryBarcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductConfigId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.Id);
                    table.CheckConstraint("ck_items_status", "\"Status\" IN ('Active','Discontinued','Obsolete')");
                    table.CheckConstraint("ck_items_volume", "\"Volume\" IS NULL OR \"Volume\" > 0");
                    table.CheckConstraint("ck_items_weight", "\"Weight\" IS NULL OR \"Weight\" > 0");
                    table.ForeignKey(
                        name: "FK_items_item_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "public",
                        principalTable: "item_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_items_unit_of_measures_BaseUoM",
                        column: x => x.BaseUoM,
                        principalSchema: "public",
                        principalTable: "unit_of_measures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inbound_shipment_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShipmentId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    ExpectedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    BaseUoM = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbound_shipment_lines", x => x.Id);
                    table.CheckConstraint("ck_inbound_shipment_lines_expected_qty", "\"ExpectedQty\" > 0");
                    table.CheckConstraint("ck_inbound_shipment_lines_received_qty", "\"ReceivedQty\" >= 0");
                    table.ForeignKey(
                        name: "FK_inbound_shipment_lines_inbound_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "public",
                        principalTable: "inbound_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inbound_shipment_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inbound_shipment_lines_unit_of_measures_BaseUoM",
                        column: x => x.BaseUoM,
                        principalSchema: "public",
                        principalTable: "unit_of_measures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_barcodes",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BarcodeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_barcodes", x => x.Id);
                    table.CheckConstraint("ck_item_barcodes_type", "\"BarcodeType\" IN ('EAN13','Code128','QR','UPC','Other')");
                    table.ForeignKey(
                        name: "FK_item_barcodes_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_uom_conversions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    FromUoM = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ToUoM = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RoundingRule = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Up")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_uom_conversions", x => x.Id);
                    table.CheckConstraint("ck_item_uom_conversions_factor", "\"Factor\" > 0");
                    table.CheckConstraint("ck_item_uom_conversions_rounding", "\"RoundingRule\" IN ('Up','Down','Nearest')");
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_unit_of_measures_FromUoM",
                        column: x => x.FromUoM,
                        principalSchema: "public",
                        principalTable: "unit_of_measures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_unit_of_measures_ToUoM",
                        column: x => x.ToUoM,
                        principalSchema: "public",
                        principalTable: "unit_of_measures",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lots",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LotNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lots_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "serial_numbers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serial_numbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_serial_numbers_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_item_mappings",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierSKU = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: true),
                    MinOrderQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    PricePerUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_item_mappings", x => x.Id);
                    table.CheckConstraint("ck_supplier_item_mappings_leadtime", "\"LeadTimeDays\" IS NULL OR \"LeadTimeDays\" >= 0");
                    table.CheckConstraint("ck_supplier_item_mappings_moq", "\"MinOrderQty\" IS NULL OR \"MinOrderQty\" > 0");
                    table.CheckConstraint("ck_supplier_item_mappings_price", "\"PricePerUnit\" IS NULL OR \"PricePerUnit\" > 0");
                    table.ForeignKey(
                        name: "FK_supplier_item_mappings_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_item_mappings_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "public",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_handling_units_Location",
                schema: "public",
                table: "handling_units",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_handling_units_LPN",
                schema: "public",
                table: "handling_units",
                column: "LPN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipment_lines_BaseUoM",
                schema: "public",
                table: "inbound_shipment_lines",
                column: "BaseUoM");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipment_lines_ItemId",
                schema: "public",
                table: "inbound_shipment_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipment_lines_ShipmentId_ItemId",
                schema: "public",
                table: "inbound_shipment_lines",
                columns: new[] { "ShipmentId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipments_ReferenceNumber",
                schema: "public",
                table: "inbound_shipments",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbound_shipments_SupplierId",
                schema: "public",
                table: "inbound_shipments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_item_barcodes_Barcode",
                schema: "public",
                table: "item_barcodes",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_barcodes_ItemId",
                schema: "public",
                table: "item_barcodes",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_item_barcodes_ItemId_IsPrimary",
                schema: "public",
                table: "item_barcodes",
                columns: new[] { "ItemId", "IsPrimary" },
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_Code",
                schema: "public",
                table: "item_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_ParentCategoryId",
                schema: "public",
                table: "item_categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_FromUoM",
                schema: "public",
                table: "item_uom_conversions",
                column: "FromUoM");

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_ItemId_FromUoM_ToUoM",
                schema: "public",
                table: "item_uom_conversions",
                columns: new[] { "ItemId", "FromUoM", "ToUoM" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_ToUoM",
                schema: "public",
                table: "item_uom_conversions",
                column: "ToUoM");

            migrationBuilder.CreateIndex(
                name: "IX_items_BaseUoM",
                schema: "public",
                table: "items",
                column: "BaseUoM");

            migrationBuilder.CreateIndex(
                name: "IX_items_CategoryId",
                schema: "public",
                table: "items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_items_InternalSKU",
                schema: "public",
                table: "items",
                column: "InternalSKU",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_Status",
                schema: "public",
                table: "items",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_locations_Barcode",
                schema: "public",
                table: "locations",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_Code",
                schema: "public",
                table: "locations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_IsVirtual",
                schema: "public",
                table: "locations",
                column: "IsVirtual");

            migrationBuilder.CreateIndex(
                name: "IX_locations_ParentLocationId",
                schema: "public",
                table: "locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_locations_Status",
                schema: "public",
                table: "locations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_locations_Type",
                schema: "public",
                table: "locations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_lots_ItemId_LotNumber",
                schema: "public",
                table: "lots",
                columns: new[] { "ItemId", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_serial_numbers_ItemId",
                schema: "public",
                table: "serial_numbers",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_serial_numbers_Value",
                schema: "public",
                table: "serial_numbers",
                column: "Value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_item_mappings_ItemId",
                schema: "public",
                table: "supplier_item_mappings",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_item_mappings_SupplierId_SupplierSKU",
                schema: "public",
                table: "supplier_item_mappings",
                columns: new[] { "SupplierId", "SupplierSKU" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_Code",
                schema: "public",
                table: "suppliers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_Code",
                schema: "public",
                table: "warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO public.unit_of_measures ("Code", "Name", "Type") VALUES
                ('KG', 'Kilogram', 'Weight'),
                ('G', 'Gram', 'Weight'),
                ('L', 'Liter', 'Volume'),
                ('ML', 'Milliliter', 'Volume'),
                ('PCS', 'Pieces', 'Piece'),
                ('M', 'Meter', 'Length'),
                ('BOX', 'Box', 'Piece'),
                ('PKG', 'Package', 'Piece')
                ON CONFLICT ("Code") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "Type" = EXCLUDED."Type";
                
                INSERT INTO public.handling_unit_types ("Code", "Name") VALUES
                ('PALLET', 'Pallet'),
                ('BOX', 'Box'),
                ('BAG', 'Bag')
                ON CONFLICT ("Code") DO UPDATE
                SET "Name" = EXCLUDED."Name";
                
                INSERT INTO public.adjustment_reason_codes ("Code", "Name", "IsActive") VALUES
                ('DAMAGE', 'Damage', TRUE),
                ('THEFT', 'Theft', TRUE),
                ('EVAPORATION', 'Evaporation', TRUE),
                ('INVENTORY', 'Inventory Correction', TRUE),
                ('SYSTEM_ERROR', 'System Error', TRUE),
                ('EXPIRED', 'Expired', TRUE),
                ('QC_REJECTED', 'QC Rejected', TRUE),
                ('PRODUCTION_SCRAP', 'Production Scrap', TRUE)
                ON CONFLICT ("Code") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "IsActive" = EXCLUDED."IsActive";
                
                INSERT INTO public.item_categories ("Code", "Name", "ParentCategoryId") VALUES
                ('RAW', 'Raw Materials', NULL),
                ('FINISHED', 'Finished Goods', NULL)
                ON CONFLICT ("Code") DO UPDATE
                SET "Name" = EXCLUDED."Name";
                
                INSERT INTO public.item_categories ("Code", "Name", "ParentCategoryId")
                SELECT 'FASTENERS', 'Fasteners', parent."Id"
                FROM public.item_categories parent
                WHERE parent."Code" = 'RAW'
                ON CONFLICT ("Code") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "ParentCategoryId" = EXCLUDED."ParentCategoryId";
                
                INSERT INTO public.item_categories ("Code", "Name", "ParentCategoryId")
                SELECT 'CHEMICALS', 'Chemicals', parent."Id"
                FROM public.item_categories parent
                WHERE parent."Code" = 'RAW'
                ON CONFLICT ("Code") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "ParentCategoryId" = EXCLUDED."ParentCategoryId";
                
                INSERT INTO public.locations
                ("Code", "Barcode", "Type", "ParentLocationId", "IsVirtual", "MaxWeight", "MaxVolume", "Status", "ZoneType", "CreatedAt")
                VALUES
                ('RECEIVING', 'VIRTUAL-RCV', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
                ('QC_HOLD', 'VIRTUAL-QC', 'Zone', NULL, TRUE, NULL, NULL, 'Active', 'Quarantine', NOW()),
                ('QUARANTINE', 'VIRTUAL-QTN', 'Zone', NULL, TRUE, NULL, NULL, 'Active', 'Quarantine', NOW()),
                ('PRODUCTION', 'VIRTUAL-PROD', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
                ('SHIPPING', 'VIRTUAL-SHIP', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
                ('SCRAP', 'VIRTUAL-SCRAP', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
                ('RETURN_TO_SUPPLIER', 'VIRTUAL-RTS', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW())
                ON CONFLICT ("Code") DO UPDATE
                SET "Barcode" = EXCLUDED."Barcode",
                    "Type" = EXCLUDED."Type",
                    "IsVirtual" = EXCLUDED."IsVirtual",
                    "Status" = EXCLUDED."Status";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjustment_reason_codes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "handling_unit_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "handling_unit_types",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inbound_shipment_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_barcodes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_uom_conversions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "lots",
                schema: "public");

            migrationBuilder.DropTable(
                name: "serial_numbers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sku_sequences",
                schema: "public");

            migrationBuilder.DropTable(
                name: "supplier_item_mappings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "public");

            migrationBuilder.DropTable(
                name: "handling_units",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inbound_shipments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "item_categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "unit_of_measures",
                schema: "public");
        }
    }
}
