using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LKvitai.MES.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAndSalesOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "customer_code_seq",
                schema: "public");

            migrationBuilder.CreateSequence(
                name: "sales_order_number_seq",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'CUST-' || LPAD(nextval('customer_code_seq')::text, 4, '0')"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    billing_address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    billing_address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    billing_address_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    billing_address_zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    billing_address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_shipping_address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    default_shipping_address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    default_shipping_address_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    default_shipping_address_zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    default_shipping_address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaymentTerms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.CheckConstraint("ck_customers_credit_limit", "\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'SO-' || LPAD(nextval('sales_order_number_seq')::text, 4, '0')"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    shipping_address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    shipping_address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipping_address_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    shipping_address_zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    shipping_address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AllocatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ShippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InvoicedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboundOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.Id);
                    table.CheckConstraint("ck_sales_orders_total_amount", "\"TotalAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_sales_orders_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_lines",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    OrderedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AllocatedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    PickedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    ShippedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_lines", x => x.Id);
                    table.CheckConstraint("ck_sales_order_lines_allocated_qty", "\"AllocatedQty\" >= 0");
                    table.CheckConstraint("ck_sales_order_lines_line_amount", "\"LineAmount\" >= 0");
                    table.CheckConstraint("ck_sales_order_lines_ordered_qty", "\"OrderedQty\" > 0");
                    table.CheckConstraint("ck_sales_order_lines_picked_qty", "\"PickedQty\" >= 0");
                    table.CheckConstraint("ck_sales_order_lines_shipped_qty", "\"ShippedQty\" >= 0");
                    table.CheckConstraint("ck_sales_order_lines_unit_price", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_sales_order_lines_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "public",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_order_lines_sales_orders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalSchema: "public",
                        principalTable: "sales_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customers_CustomerCode",
                schema: "public",
                table: "customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_Name",
                schema: "public",
                table: "customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_customers_Status",
                schema: "public",
                table: "customers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_ItemId",
                schema: "public",
                table: "sales_order_lines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_lines_SalesOrderId",
                schema: "public",
                table: "sales_order_lines",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_CustomerId",
                schema: "public",
                table: "sales_orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_OrderDate",
                schema: "public",
                table: "sales_orders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_OrderNumber",
                schema: "public",
                table: "sales_orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_Status",
                schema: "public",
                table: "sales_orders",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_order_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sales_orders",
                schema: "public");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "customer_code_seq",
                schema: "public");

            migrationBuilder.DropSequence(
                name: "sales_order_number_seq",
                schema: "public");
        }
    }
}
