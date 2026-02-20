using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class OutboundProjectionConsumersTests
{
    [Fact]
    [Trait("Category", "Projections")]
    public async Task OutboundOrderCreated_ShouldCreateOutboundOrderSummaryAndCheckpoint()
    {
        await using var db = CreateDbContext();
        var sut = new OutboundOrderSummaryConsumer(db, CreateLogger<OutboundOrderSummaryConsumer>());

        var message = new OutboundOrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            Id = Guid.NewGuid(),
            OrderNumber = "OUT-0001",
            Type = "SALES",
            Status = "ALLOCATED",
            CustomerName = "Acme Corp",
            OrderDate = DateTime.UtcNow,
            Lines = new List<ShipmentLineSnapshot>
            {
                new() { ItemId = 1, ItemSku = "SKU-1", Qty = 2m },
                new() { ItemId = 2, ItemSku = "SKU-2", Qty = 1m }
            }
        };

        await sut.Consume(CreateContext(message));

        var summary = await db.OutboundOrderSummaries.SingleAsync(x => x.Id == message.Id);
        summary.OrderNumber.Should().Be("OUT-0001");
        summary.CustomerName.Should().Be("Acme Corp");
        summary.ItemCount.Should().Be(2);

        var checkpoints = await db.EventProcessingCheckpoints
            .CountAsync(x => x.HandlerName == nameof(OutboundOrderSummaryConsumer));
        checkpoints.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Projections")]
    public async Task DuplicateOutboundOrderCreated_ShouldBeIgnoredByCheckpoint()
    {
        await using var db = CreateDbContext();
        var sut = new OutboundOrderSummaryConsumer(db, CreateLogger<OutboundOrderSummaryConsumer>());

        var message = new OutboundOrderCreatedEvent
        {
            EventId = Guid.NewGuid(),
            Id = Guid.NewGuid(),
            OrderNumber = "OUT-0002",
            Type = "SALES",
            Status = "ALLOCATED",
            CustomerName = "Beta Corp",
            OrderDate = DateTime.UtcNow,
            Lines = new List<ShipmentLineSnapshot> { new() { ItemId = 1, ItemSku = "SKU-1", Qty = 1m } }
        };

        var context = CreateContext(message);
        await sut.Consume(context);
        await sut.Consume(context);

        (await db.OutboundOrderSummaries.CountAsync(x => x.Id == message.Id)).Should().Be(1);

        var checkpoints = await db.EventProcessingCheckpoints
            .CountAsync(x => x.HandlerName == nameof(OutboundOrderSummaryConsumer));
        checkpoints.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Projections")]
    public async Task ShipmentPackedAndDispatched_ShouldCreateAndUpdateShipmentSummary()
    {
        await using var db = CreateDbContext();
        var sut = new ShipmentSummaryConsumer(db, CreateLogger<ShipmentSummaryConsumer>());

        var shipmentId = Guid.NewGuid();
        var packed = new ShipmentPackedEvent
        {
            EventId = Guid.NewGuid(),
            ShipmentId = shipmentId,
            ShipmentNumber = "SHIP-0001",
            OutboundOrderId = Guid.NewGuid(),
            OutboundOrderNumber = "OUT-1001",
            PackedAt = DateTime.UtcNow,
            PackedBy = "packer",
            Lines = new List<ShipmentLineSnapshot> { new() { ItemId = 1, ItemSku = "SKU-1", Qty = 1m } }
        };

        await sut.Consume(CreateContext(packed));

        var dispatched = new ShipmentDispatchedEvent
        {
            EventId = Guid.NewGuid(),
            ShipmentId = shipmentId,
            ShipmentNumber = "SHIP-0001",
            OutboundOrderId = packed.OutboundOrderId,
            OutboundOrderNumber = packed.OutboundOrderNumber,
            Carrier = "FEDEX",
            TrackingNumber = "TRK-123",
            DispatchedAt = DateTime.UtcNow,
            DispatchedBy = "dispatcher"
        };

        await sut.Consume(CreateContext(dispatched));

        var summary = await db.ShipmentSummaries.SingleAsync(x => x.Id == shipmentId);
        summary.Status.Should().Be("DISPATCHED");
        summary.Carrier.Should().Be("FEDEX");
        summary.TrackingNumber.Should().Be("TRK-123");
        summary.DispatchedBy.Should().Be("dispatcher");
    }

    [Fact]
    [Trait("Category", "Projections")]
    public async Task ShipmentDispatched_ShouldUpdateOutboundOrderSummaryToShipped()
    {
        await using var db = CreateDbContext();
        var sut = new OutboundOrderSummaryConsumer(db, CreateLogger<OutboundOrderSummaryConsumer>());

        var shipmentId = Guid.NewGuid();
        var outboundOrderId = Guid.NewGuid();
        db.OutboundOrderSummaries.Add(new OutboundOrderSummary
        {
            Id = outboundOrderId,
            OrderNumber = "OUT-3001",
            Type = "SALES",
            Status = "PACKED",
            OrderDate = DateTimeOffset.UtcNow,
            ItemCount = 1,
            ShipmentId = shipmentId,
            ShipmentNumber = "SHIP-3001"
        });
        await db.SaveChangesAsync();

        var dispatched = new ShipmentDispatchedEvent
        {
            EventId = Guid.NewGuid(),
            ShipmentId = shipmentId,
            ShipmentNumber = "SHIP-3001",
            OutboundOrderId = outboundOrderId,
            OutboundOrderNumber = "OUT-3001",
            Carrier = "UPS",
            TrackingNumber = "TRACK-3001",
            DispatchedAt = DateTime.UtcNow,
            DispatchedBy = "ops.user"
        };

        await sut.Consume(CreateContext(dispatched));

        var summary = await db.OutboundOrderSummaries.SingleAsync(x => x.Id == outboundOrderId);
        summary.Status.Should().Be("SHIPPED");
        summary.TrackingNumber.Should().Be("TRACK-3001");
        summary.ShippedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Projections")]
    public async Task ShipmentDispatched_ShouldAppendDispatchHistory()
    {
        await using var db = CreateDbContext();
        var sut = new DispatchHistoryConsumer(db, CreateLogger<DispatchHistoryConsumer>());

        var message = new ShipmentDispatchedEvent
        {
            EventId = Guid.NewGuid(),
            ShipmentId = Guid.NewGuid(),
            ShipmentNumber = "SHIP-0005",
            OutboundOrderId = Guid.NewGuid(),
            OutboundOrderNumber = "OUT-0005",
            Carrier = "DHL",
            TrackingNumber = "DHL-123",
            VehicleId = "VAN-1",
            DispatchedAt = DateTime.UtcNow,
            DispatchedBy = "john.doe",
            ManualTracking = true
        };

        await sut.Consume(CreateContext(message));

        var history = await db.DispatchHistories.SingleAsync(x => x.ShipmentId == message.ShipmentId);
        history.ShipmentNumber.Should().Be("SHIP-0005");
        history.Carrier.Should().Be("DHL");
        history.ManualTracking.Should().BeTrue();

        var checkpoints = await db.EventProcessingCheckpoints
            .CountAsync(x => x.HandlerName == nameof(DispatchHistoryConsumer));
        checkpoints.Should().Be(1);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"projection-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static ILogger<T> CreateLogger<T>()
    {
        return NullLoggerFactory.Instance.CreateLogger<T>();
    }

    private static ConsumeContext<T> CreateContext<T>(T message)
        where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }
}
