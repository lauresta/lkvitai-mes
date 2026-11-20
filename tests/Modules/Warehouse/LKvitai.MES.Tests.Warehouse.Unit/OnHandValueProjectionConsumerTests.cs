using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class OnHandValueProjectionConsumerTests
{
    [Fact]
    [Trait("Category", "OnHandValueProjection")]
    public async Task StockValueChanged_ShouldBuildRowFromValuedQtyAndValue_AndIgnoreRedelivery()
    {
        await using var db = CreateDbContext();
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        db.ItemCategories.Add(new ItemCategory { Id = 11, Code = "RAW", Name = "Raw Materials" });
        db.Items.Add(new Item { Id = 101, InternalSKU = "RM-0001", Name = "Raw Material A", CategoryId = 11, BaseUoM = "PCS", Status = "Active" });
        await db.SaveChangesAsync();
        var sut = new OnHandValueProjectionConsumer(db, NullLoggerFactory.Instance.CreateLogger<OnHandValueProjectionConsumer>());

        var receipt = new StockValueChanged { ItemId = 101, Kind = StockValueKinds.Receipt, QtyDelta = 10m, ValueDeltaEur = 35.55m };
        await sut.Consume(CreateContext(receipt));
        await sut.Consume(CreateContext(receipt));
        await sut.Consume(CreateContext(new StockValueChanged { ItemId = 101, Kind = StockValueKinds.Issue, QtyDelta = -4m, ValueDeltaEur = -14.22m }));

        db.ChangeTracker.Clear();
        var row = await db.OnHandValues.SingleAsync(x => x.ItemId == 101);
        row.ItemSku.Should().Be("RM-0001");
        row.CategoryName.Should().Be("Raw Materials");
        row.Qty.Should().Be(6m);
        row.TotalValue.Should().Be(21.33m);
        row.UnitCost.Should().Be(3.555m);
    }

    [Fact]
    [Trait("Category", "OnHandValueProjection")]
    public async Task EmptyingTheStock_ShouldKeepLastUnitCostAndZeroValue()
    {
        await using var db = CreateDbContext();
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        db.Items.Add(new Item { Id = 5, InternalSKU = "S", Name = "S", BaseUoM = "PCS", Status = "Active" });
        await db.SaveChangesAsync();
        var sut = new OnHandValueProjectionConsumer(db, NullLoggerFactory.Instance.CreateLogger<OnHandValueProjectionConsumer>());

        await sut.Consume(CreateContext(new StockValueChanged { ItemId = 5, Kind = StockValueKinds.Receipt, QtyDelta = 2m, ValueDeltaEur = 7m }));
        await sut.Consume(CreateContext(new StockValueChanged { ItemId = 5, Kind = StockValueKinds.Issue, QtyDelta = -2m, ValueDeltaEur = -7m }));

        var row = await db.OnHandValues.SingleAsync();
        row.Qty.Should().Be(0m);
        row.TotalValue.Should().Be(0m);
        row.UnitCost.Should().Be(3.5m);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"on-hand-projection-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static ConsumeContext<T> CreateContext<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }
}
