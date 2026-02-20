using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class LotTraceabilityServiceTests
{
    [Fact]
    public async Task BuildAsync_Backward_ShouldIncludeShipmentAndSupplier()
    {
        await using var db = CreateDbContext();
        var item = SeedItem(db);
        SeedLot(db, item.Id, "LOT-1", productionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)));
        SeedInbound(db, item.Id, supplierName: "ACME Corp");
        await db.SaveChangesAsync();

        var sut = new LotTraceabilityService(db);
        var result = await sut.BuildAsync("LOT-1", LotTraceDirection.Backward);

        result.Succeeded.Should().BeTrue();
        result.Report.Should().NotBeNull();
        result.Report!.IsApproximate.Should().BeFalse();
        result.Report.Root.Children.Should().ContainSingle(node => node.NodeType == "SHIPMENT");
        result.Report.Root.Children.Single().Children.Should().ContainSingle(node => node.NodeType == "SUPPLIER" && node.NodeName == "ACME Corp");
    }

    [Fact]
    public async Task BuildAsync_Forward_ShouldIncludeReservationAndCustomer()
    {
        await using var db = CreateDbContext();
        var item = SeedItem(db);
        var lot = SeedLot(db, item.Id, "LOT-2", productionDate: null);
        var customer = SeedCustomer(db, "CUST-1", "Customer A");
        SeedSalesOrderWithShipment(db, item.Id, lot.Id, customer.Id, "SO-001");
        await db.SaveChangesAsync();

        var sut = new LotTraceabilityService(db);
        var result = await sut.BuildAsync("LOT-2", LotTraceDirection.Forward);

        result.Succeeded.Should().BeTrue();
        result.Report.Should().NotBeNull();
        result.Report!.IsApproximate.Should().BeFalse();
        var reservation = result.Report.Root.Children.Should().ContainSingle(n => n.NodeType == "RESERVATION").Subject;
        var outbound = reservation.Children.Should().ContainSingle(n => n.NodeType == "OUTBOUND_ORDER").Subject;
        outbound.Children.Should().Contain(n => n.NodeType == "CUSTOMER" && n.NodeName == "Customer A");
    }

    [Fact]
    public async Task BuildAsync_WhenLotMissing_ShouldReturn404()
    {
        await using var db = CreateDbContext();
        var sut = new LotTraceabilityService(db);

        var result = await sut.BuildAsync("UNKNOWN", LotTraceDirection.Forward);

        result.Succeeded.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task BuildAsync_WhenLotEmpty_ShouldReturn400()
    {
        await using var db = CreateDbContext();
        var sut = new LotTraceabilityService(db);

        var result = await sut.BuildAsync("   ", LotTraceDirection.Forward);

        result.Succeeded.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task BuildAsync_WhenNoLinks_ShouldMarkApproximate()
    {
        await using var db = CreateDbContext();
        var item = SeedItem(db);
        SeedLot(db, item.Id, "LOT-NO-LINK", null);
        await db.SaveChangesAsync();

        var sut = new LotTraceabilityService(db);
        var result = await sut.BuildAsync("LOT-NO-LINK", LotTraceDirection.Backward);

        result.Succeeded.Should().BeTrue();
        result.Report!.IsApproximate.Should().BeTrue();
        result.Report.Root.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_ShouldAcceptCaseInsensitiveDirection()
    {
        await using var db = CreateDbContext();
        var item = SeedItem(db);
        var lot = SeedLot(db, item.Id, "LOT-CASE", null);
        var customer = SeedCustomer(db, "C-1", "Cust");
        SeedSalesOrderWithShipment(db, item.Id, lot.Id, customer.Id, "SO-CASE");
        await db.SaveChangesAsync();

        var sut = new LotTraceabilityService(db);
        var result = await sut.BuildAsync("lot-case", (LotTraceDirection)Enum.Parse(typeof(LotTraceDirection), "Forward", true));

        result.Succeeded.Should().BeTrue();
        result.Report!.Root.Children.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BuildCsv_ShouldFlattenTree()
    {
        await using var db = CreateDbContext();
        var item = SeedItem(db);
        SeedLot(db, item.Id, "LOT-CSV", null);
        await db.SaveChangesAsync();

        var sut = new LotTraceabilityService(db);
        var build = await sut.BuildAsync("LOT-CSV", LotTraceDirection.Forward);

        build.Succeeded.Should().BeTrue();
        var csv = sut.BuildCsv(build.Report!);

        csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Should().Contain(line => line.Contains("LOT-CSV"));
    }

    [Fact]
    public void BuildCsv_ShouldEscapeQuotes()
    {
        var report = new LotTraceReport(
            Guid.NewGuid(),
            "LOT-Q",
            LotTraceDirection.Backward,
            new LotTraceNode("LOT", "LOT-Q", "Test \"Lot\"", DateTimeOffset.UtcNow, []),
            true,
            DateTimeOffset.UtcNow);

        var csv = new LotTraceabilityService(CreateDbContext()).BuildCsv(report);

        csv.Should().Contain("\"Test \"\"Lot\"\"\"");
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"lot-trace-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static Item SeedItem(WarehouseDbContext db)
    {
        var item = new Item
        {
            InternalSKU = "SKU-1",
            Name = "Test Item",
            CategoryId = 1,
            BaseUoM = "EA"
        };
        db.Items.Add(item);
        return item;
    }

    private static Lot SeedLot(WarehouseDbContext db, int itemId, string lotNumber, DateOnly? productionDate)
    {
        var lot = new Lot
        {
            ItemId = itemId,
            LotNumber = lotNumber,
            ProductionDate = productionDate
        };
        db.Lots.Add(lot);
        return lot;
    }

    private static Customer SeedCustomer(WarehouseDbContext db, string code, string name)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CustomerCode = code,
            Name = name,
            Email = "customer@example.com"
        };
        db.Customers.Add(customer);
        return customer;
    }

    private static InboundShipment SeedInbound(
        WarehouseDbContext db,
        int itemId,
        string supplierName,
        DateTimeOffset? updatedAt = null)
    {
        var supplier = new Supplier
        {
            Code = $"SUP-{Guid.NewGuid():N}".Substring(0, 8),
            Name = supplierName,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Suppliers.Add(supplier);

        var inbound = new InboundShipment
        {
            ReferenceNumber = $"ISH-{Guid.NewGuid():N}".Substring(0, 8),
            SupplierId = supplier.Id,
            Supplier = supplier,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow.AddDays(-1)
        };
        inbound.Lines.Add(new InboundShipmentLine
        {
            ItemId = itemId,
            ExpectedQty = 10,
            ReceivedQty = 5,
            BaseUoM = "EA"
        });
        db.InboundShipments.Add(inbound);
        return inbound;
    }

    private static void SeedSalesOrderWithShipment(
        WarehouseDbContext db,
        int itemId,
        int lotId,
        Guid customerId,
        string orderNumber,
        string outboundOrderNumber = "OUT-1",
        bool includeOutbound = true)
    {
        var so = new SalesOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CustomerId = customerId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        so.Lines.Add(new SalesOrderLine
        {
            ItemId = itemId,
            OrderedQty = 1,
            ShippedQty = 1,
            UnitPrice = 10,
            LineAmount = 10
        });
        db.SalesOrders.Add(so);

        if (includeOutbound)
        {
            var outbound = new OutboundOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = outboundOrderNumber,
                SalesOrderId = so.Id,
                ReservationId = Guid.NewGuid(),
                OrderDate = DateTimeOffset.UtcNow.AddDays(-1)
            };
            db.OutboundOrders.Add(outbound);
        }
    }
}
