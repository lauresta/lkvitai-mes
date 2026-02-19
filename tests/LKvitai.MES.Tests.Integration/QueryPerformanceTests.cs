using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class QueryPerformanceTests
{
    [Fact]
    public void ItemIndexes_ShouldContainCategoryAndBarcodeIndexes()
    {
        using var db = CreateDbContext();
        var indexes = db.Model.FindEntityType(typeof(Item))!.GetIndexes();

        Assert.Contains(indexes, x => x.GetDatabaseName() == "idx_items_category_id");
        Assert.Contains(indexes, x => x.GetDatabaseName() == "idx_items_barcode");
    }

    [Fact]
    public void SalesOrderIndexes_ShouldContainCustomerStatusComposite()
    {
        using var db = CreateDbContext();
        var indexes = db.Model.FindEntityType(typeof(SalesOrder))!.GetIndexes();

        Assert.Contains(indexes, x =>
            x.GetDatabaseName() == "idx_sales_orders_customer_id_status" &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "CustomerId", "Status" }));
    }

    [Fact]
    public void OutboundOrderIndexes_ShouldContainStatusRequestedShipDateComposite()
    {
        using var db = CreateDbContext();
        var indexes = db.Model.FindEntityType(typeof(OutboundOrder))!.GetIndexes();

        Assert.Contains(indexes, x =>
            x.GetDatabaseName() == "idx_outbound_orders_status_requested_ship_date" &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "Status", "RequestedShipDate" }));
    }

    [Fact]
    public void ShipmentIndexes_ShouldContainTrackingAndDispatchedAt()
    {
        using var db = CreateDbContext();
        var indexes = db.Model.FindEntityType(typeof(Shipment))!.GetIndexes();

        Assert.Contains(indexes, x => x.GetDatabaseName() == "idx_shipments_tracking_number");
        Assert.Contains(indexes, x => x.GetDatabaseName() == "idx_shipments_dispatched_at");
    }

    [Fact]
    public void OnHandValueIndexes_ShouldContainCategoryIndex()
    {
        using var db = CreateDbContext();
        var indexes = db.Model.FindEntityType(typeof(OnHandValue))!.GetIndexes();

        Assert.Contains(indexes, x => x.GetDatabaseName() == "idx_on_hand_value_category_id");
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"query-performance-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }
}
