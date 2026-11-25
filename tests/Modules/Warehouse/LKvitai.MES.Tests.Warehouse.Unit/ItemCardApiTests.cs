using System.Net;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ItemCardApiTests
{
    [Theory]
    [InlineData("RECEIVING", "Receiving")]
    [InlineData("receiving", "Receiving")]
    [InlineData("QC_HOLD", "QcHold")]
    [InlineData("QUARANTINE", "Quarantine")]
    [InlineData("SHIPPING", "Shipping")]
    [InlineData("WAREHOUSE_MERGE", "Transit")]
    [InlineData("IN_TRANSIT_VLN-2", "Transit")]
    [InlineData("SUPPLIER", "Outside")]
    [InlineData("AGNUM", "Outside")]
    public void StockLocationRole_FixedCodes_GetFixedRole(string code, string expected)
    {
        StockLocationRole.Resolve(code, null).Should().Be(expected);
    }

    [Fact]
    public void StockLocationRole_Cell_UsesConfiguredRoleOrStorage()
    {
        var configured = new Location { Code = "A-03-2", LocationRole = " Bulk " };
        var plain = new Location { Code = "A-03-3" };

        StockLocationRole.Resolve("A-03-2", configured).Should().Be("Bulk");
        StockLocationRole.Resolve("A-03-3", plain).Should().Be(StockLocationRole.Storage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UNKNOWN-1")]
    public void StockLocationRole_UnknownOrEmpty_IsNull(string? code)
    {
        StockLocationRole.Resolve(code, null).Should().BeNull();
    }

    [Fact]
    public void ItemReservationRows_KeepsItemLines_GroupsByReservationAndLocation_AndJoinsOrders()
    {
        var withOrder = Guid.NewGuid();
        var withoutOrder = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var lines = new[]
        {
            new ReservationLineSource(withOrder, "SO", "ALLOCATED", new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc), "bav-ral9006", "A-03-2", 40m),
            new ReservationLineSource(withOrder, "SO", "ALLOCATED", new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc), "BAV-RAL9006", "A-03-2 ", 20m),
            new ReservationLineSource(withOrder, "SO", "ALLOCATED", new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc), "OTHER", "A-03-2", 99m),
            new ReservationLineSource(withoutOrder, "Production", "PICKING", new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc), "BAV-RAL9006", "A-03-3", 26m),
            new ReservationLineSource(withoutOrder, "Production", "PICKING", new DateTime(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc), "BAV-RAL9006", "A-03-4", 0m)
        };
        var orders = new Dictionary<Guid, ReservationOrderRef>
        {
            [withOrder] = new(orderId, "KVT-261124-031", "UAB Audenis", "Picking")
        };

        var rows = ItemReservationRows.Build("BAV-RAL9006", lines, orders);

        rows.Should().HaveCount(2);
        rows[0].ReservationId.Should().Be(withOrder);
        rows[0].Qty.Should().Be(60m);
        rows[0].Location.Should().Be("A-03-2");
        rows[0].OrderNumber.Should().Be("KVT-261124-031");
        rows[0].SalesOrderId.Should().Be(orderId);
        rows[0].CustomerName.Should().Be("UAB Audenis");
        rows[0].Status.Should().Be("Picking");
        rows[1].OrderNumber.Should().BeNull();
        rows[1].Status.Should().Be("PICKING");
        rows[1].Purpose.Should().Be("Production");
        rows[1].Qty.Should().Be(26m);
    }

    [Fact]
    public void ItemIncomingRows_ReturnsOpenQuantityOfOpenDeliveries_SortedByExpectedDate()
    {
        var supplier = new Supplier { Name = "Hosten Polska" };
        var shipments = new[]
        {
            Shipment(1, "Draft", "Invoice", new DateOnly(2026, 12, 17), supplier, (10275, 200m, 0m), (1, 50m, 0m)),
            Shipment(2, "Partial", "Invoice", new DateOnly(2026, 12, 3), supplier, (10275, 300m, 100m), (10275, 50m, 60m)),
            Shipment(3, "Draft", "Invoice", null, null, (10275, 5m, 0m)),
            Shipment(4, "Complete", "Invoice", new DateOnly(2026, 11, 1), supplier, (10275, 10m, 0m)),
            Shipment(5, "Draft", "CreditNote", new DateOnly(2026, 11, 2), supplier, (10275, 10m, 0m)),
            Shipment(6, "Partial", "Invoice", new DateOnly(2026, 11, 3), supplier, (10275, 10m, 10m))
        };

        var rows = ItemIncomingRows.Build(10275, shipments);

        rows.Select(x => x.ShipmentId).Should().Equal(2, 1, 3);
        rows[0].OpenQty.Should().Be(200m);
        rows[0].ExpectedQty.Should().Be(350m);
        rows[0].ReceivedQty.Should().Be(160m);
        rows[0].SupplierName.Should().Be("Hosten Polska");
        rows[1].OpenQty.Should().Be(200m);
        rows[2].SupplierName.Should().BeNull();
    }

    [Fact]
    public async Task StockClient_GetItemStockAsync_AsksForItemRowsWithReservedAndVirtual_AndMapsLocationFields()
    {
        string? captured = null;
        var factory = Factory(request =>
        {
            captured = request.RequestUri?.PathAndQuery;
            return Json("""
                {"items":[{"warehouseId":"VLN-1","itemId":10275,"locationId":101,"locationCode":"RECEIVING","locationRole":"Receiving",
                  "internalSku":"BAV-RAL9006","qty":30,"reservedQty":0,"availableQty":30,"lastUpdated":"2026-11-24T13:10:00Z"}],
                 "totalCount":1,"pageNumber":1,"pageSize":200}
                """);
        });

        var result = await new StockClient(factory).GetItemStockAsync(10275);

        captured.Should().Be("/api/warehouse/v1/stock/available?itemId=10275&includeReserved=true&includeVirtualLocations=true&pageNumber=1&pageSize=200");
        result.Items.Should().ContainSingle();
        result.Items[0].LocationId.Should().Be(101);
        result.Items[0].LocationRole.Should().Be("Receiving");
        result.Items[0].Location.Should().Be("RECEIVING");
        result.Items[0].PhysicalQty.Should().Be(30m);
    }

    [Fact]
    public async Task ReservationsClient_GetByItemAsync_CallsByItemRoute()
    {
        string? captured = null;
        var factory = Factory(request =>
        {
            captured = request.RequestUri?.PathAndQuery;
            return Json("""[{"reservationId":"6f1d4b6e-8c35-4c33-9d4b-0a0a0a0a0a0a","orderNumber":"KVT-1","customerName":"MB Namai","location":"A-03-3","qty":26,"status":"Allocated"}]""");
        });

        var rows = await new ReservationsClient(factory).GetByItemAsync(10275);

        captured.Should().Be("/api/warehouse/v1/reservations/by-item/10275");
        rows.Should().ContainSingle();
        rows[0].OrderNumber.Should().Be("KVT-1");
        rows[0].Qty.Should().Be(26m);
    }

    [Fact]
    public async Task ReceivingClient_GetIncomingByItemAsync_CallsByItemRoute()
    {
        string? captured = null;
        var factory = Factory(request =>
        {
            captured = request.RequestUri?.PathAndQuery;
            return Json("""[{"shipmentId":7,"referenceNumber":"IS-2611-007","supplierName":"Hosten","expectedDate":"2026-12-03","status":"Draft","openQty":300}]""");
        });

        var rows = await new ReceivingClient(factory).GetIncomingByItemAsync(10275);

        captured.Should().Be("/api/warehouse/v1/inbound-shipments/by-item/10275");
        rows.Should().ContainSingle();
        rows[0].ExpectedDate.Should().Be(new DateOnly(2026, 12, 3));
        rows[0].OpenQty.Should().Be(300m);
    }

    private static InboundShipment Shipment(
        int id,
        string status,
        string kind,
        DateOnly? expected,
        Supplier? supplier,
        params (int ItemId, decimal Expected, decimal Received)[] lines)
    {
        var shipment = new InboundShipment
        {
            Id = id,
            ReferenceNumber = $"IS-{id}",
            Status = status,
            DocumentKind = kind,
            ExpectedDate = expected,
            Supplier = supplier
        };

        foreach (var line in lines)
        {
            shipment.Lines.Add(new InboundShipmentLine { ItemId = line.ItemId, ExpectedQty = line.Expected, ReceivedQty = line.Received });
        }

        return shipment;
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static IHttpClientFactory Factory(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var client = new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://localhost") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("WarehouseApi")).Returns(client);
        return factory.Object;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
