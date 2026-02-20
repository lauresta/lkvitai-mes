using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class OnHandValueProjectionConsumerTests
{
    [Fact]
    [Trait("Category", "OnHandValueProjection")]
    public async Task ValuationInitialized_ShouldCreateProjectionRow()
    {
        await using var db = CreateDbContext();
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        db.ItemCategories.Add(new ItemCategory { Id = 11, Code = "RAW", Name = "Raw Materials" });
        db.Items.Add(new Item
        {
            Id = 101,
            InternalSKU = "RM-0001",
            Name = "Raw Material A",
            CategoryId = 11,
            BaseUoM = "PCS",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var resolver = new FakeAvailableStockQuantityResolver();
        resolver.TotalQtyByItemId[101] = 100m;
        var sut = new OnHandValueProjectionConsumer(db, resolver, CreateLogger<OnHandValueProjectionConsumer>());

        var message = new ValuationInitialized
        {
            EventId = Guid.NewGuid(),
            ItemId = Valuation.ToValuationItemId(101),
            InitialUnitCost = 10m,
            Source = "Manual",
            InitializedBy = "finance",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        };

        await sut.Consume(CreateContext(message));

        var row = await db.OnHandValues.SingleAsync(x => x.ItemId == 101);
        row.ItemSku.Should().Be("RM-0001");
        row.Qty.Should().Be(100m);
        row.UnitCost.Should().Be(10m);
        row.TotalValue.Should().Be(1000m);
    }

    [Fact]
    [Trait("Category", "OnHandValueProjection")]
    public async Task CostAdjusted_ShouldUpdateUnitCostAndTotalValue()
    {
        await using var db = CreateDbContext();
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        db.ItemCategories.Add(new ItemCategory { Id = 12, Code = "FG", Name = "Finished Goods" });
        db.Items.Add(new Item
        {
            Id = 102,
            InternalSKU = "FG-0001",
            Name = "Finished Good A",
            CategoryId = 12,
            BaseUoM = "PCS",
            Status = "Active"
        });
        db.OnHandValues.Add(new OnHandValue
        {
            Id = Valuation.ToValuationItemId(102),
            ItemId = 102,
            ItemSku = "FG-0001",
            ItemName = "Finished Good A",
            Qty = 50m,
            UnitCost = 20m,
            TotalValue = 1000m,
            LastUpdated = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await db.SaveChangesAsync();

        var resolver = new FakeAvailableStockQuantityResolver();
        resolver.TotalQtyByItemId[102] = 50m;
        var sut = new OnHandValueProjectionConsumer(db, resolver, CreateLogger<OnHandValueProjectionConsumer>());

        var message = new CostAdjusted
        {
            EventId = Guid.NewGuid(),
            ItemId = Valuation.ToValuationItemId(102),
            OldUnitCost = 20m,
            NewUnitCost = 25m,
            Reason = "Market revaluation",
            AdjustedBy = "manager",
            AdjustedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        };

        await sut.Consume(CreateContext(message));

        var row = await db.OnHandValues.SingleAsync(x => x.ItemId == 102);
        row.UnitCost.Should().Be(25m);
        row.TotalValue.Should().Be(1250m);
    }

    [Fact]
    [Trait("Category", "OnHandValueProjection")]
    public async Task StockMoved_ShouldRecalculateQtyAndTotalValue()
    {
        await using var db = CreateDbContext();
        db.OnHandValues.Add(new OnHandValue
        {
            Id = Valuation.ToValuationItemId(103),
            ItemId = 103,
            ItemSku = "RM-0003",
            ItemName = "Raw Material C",
            Qty = 30m,
            UnitCost = 12m,
            TotalValue = 360m,
            LastUpdated = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var resolver = new FakeAvailableStockQuantityResolver();
        resolver.TotalQtyByItemId[103] = 70m;
        var sut = new OnHandValueProjectionConsumer(db, resolver, CreateLogger<OnHandValueProjectionConsumer>());

        var message = new StockMovedEvent
        {
            EventId = Guid.NewGuid(),
            MovementId = Guid.NewGuid(),
            SKU = "RM-0003",
            Quantity = 40m,
            FromLocation = "BIN-A",
            ToLocation = "BIN-B",
            MovementType = "Transfer",
            OperatorId = Guid.NewGuid()
        };

        await sut.Consume(CreateContext(message));

        var row = await db.OnHandValues.SingleAsync(x => x.ItemId == 103);
        row.Qty.Should().Be(70m);
        row.TotalValue.Should().Be(840m);
    }

    [Fact]
    [Trait("Category", "OnHandValueProjection")]
    public async Task DuplicateValuationEvent_ShouldBeIgnoredByCheckpoint()
    {
        await using var db = CreateDbContext();
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        db.ItemCategories.Add(new ItemCategory { Id = 14, Code = "RAW2", Name = "Raw Materials 2" });
        db.Items.Add(new Item
        {
            Id = 104,
            InternalSKU = "RM-0004",
            Name = "Raw Material D",
            CategoryId = 14,
            BaseUoM = "PCS",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var resolver = new FakeAvailableStockQuantityResolver();
        resolver.TotalQtyByItemId[104] = 5m;
        var sut = new OnHandValueProjectionConsumer(db, resolver, CreateLogger<OnHandValueProjectionConsumer>());

        var message = new ValuationInitialized
        {
            EventId = Guid.NewGuid(),
            ItemId = Valuation.ToValuationItemId(104),
            InitialUnitCost = 2m,
            Source = "Manual",
            InitializedBy = "seed",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        };

        var context = CreateContext(message);
        await sut.Consume(context);
        await sut.Consume(context);

        (await db.OnHandValues.CountAsync(x => x.ItemId == 104)).Should().Be(1);
        (await db.EventProcessingCheckpoints
            .CountAsync(x => x.HandlerName == nameof(OnHandValueProjectionConsumer)))
            .Should().Be(1);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"on-hand-projection-tests-{Guid.NewGuid():N}")
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

    private sealed class FakeAvailableStockQuantityResolver : IAvailableStockQuantityResolver
    {
        public Dictionary<int, decimal> TotalQtyByItemId { get; } = new();
        public Dictionary<string, decimal> QtyBySku { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, decimal> LocationQtyBySku { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<decimal> ResolveTotalQtyAsync(int itemId, string itemSku, CancellationToken cancellationToken)
        {
            if (TotalQtyByItemId.TryGetValue(itemId, out var qtyById))
            {
                return Task.FromResult(qtyById);
            }

            if (QtyBySku.TryGetValue(itemSku, out var qtyBySku))
            {
                return Task.FromResult(qtyBySku);
            }

            return Task.FromResult(0m);
        }

        public Task<IReadOnlyDictionary<string, decimal>> ResolveQtyBySkuForLocationAsync(
            string locationCode,
            CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyDictionary<string, decimal>)LocationQtyBySku);
        }
    }
}
