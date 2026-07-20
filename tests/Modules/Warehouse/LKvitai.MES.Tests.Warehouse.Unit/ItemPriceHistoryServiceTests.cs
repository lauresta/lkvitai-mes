using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ItemPriceHistoryServiceTests
{
    [Fact]
    [Trait("Category", "PriceHistory")]
    public async Task WriteAsync_ShouldPersistRecord()
    {
        await using var db = CreateDbContext();
        var itemId = await SeedItemAsync(db);
        var sut = new ItemPriceHistoryService(db);

        await sut.WriteAsync(new ItemPriceHistoryWriteRequest(
            itemId,
            ItemPriceTypes.Base,
            null,
            null,
            100m,
            "user-1",
            DateTimeOffset.UtcNow,
            null));

        var row = await db.ItemPriceHistories.SingleAsync();
        row.ItemId.Should().Be(itemId);
        row.PriceType.Should().Be(ItemPriceTypes.Base);
        row.OldAmount.Should().BeNull();
        row.NewAmount.Should().Be(100m);
        row.ChangedBy.Should().Be("user-1");
    }

    [Fact]
    [Trait("Category", "PriceHistory")]
    public async Task WriteAsync_WithMissingChangedBy_ShouldFallBackToUnknown()
    {
        await using var db = CreateDbContext();
        var itemId = await SeedItemAsync(db);
        var sut = new ItemPriceHistoryService(db);

        await sut.WriteAsync(new ItemPriceHistoryWriteRequest(
            ItemId: itemId,
            PriceType: ItemPriceTypes.Purchase,
            PriceGroupId: null,
            OldAmount: 10m,
            NewAmount: 12m,
            ChangedBy: "  ",
            ChangedAt: DateTimeOffset.UtcNow,
            Reason: null));

        var row = await db.ItemPriceHistories.SingleAsync();
        row.ChangedBy.Should().Be("unknown");
    }

    [Fact]
    [Trait("Category", "PriceHistory")]
    public async Task QueryAsync_ShouldReturnOnlyRowsForItem_OrderedByChangedAtDescending()
    {
        await using var db = CreateDbContext();
        var itemId = await SeedItemAsync(db);
        var otherItemId = await SeedItemAsync(db, sku: "OTHER-1");
        var sut = new ItemPriceHistoryService(db);

        await sut.WriteAsync(new ItemPriceHistoryWriteRequest(
            ItemId: itemId, PriceType: ItemPriceTypes.Base, PriceGroupId: null,
            OldAmount: null, NewAmount: 10m, ChangedBy: "u", ChangedAt: DateTimeOffset.UtcNow.AddMinutes(-10), Reason: null));
        await sut.WriteAsync(new ItemPriceHistoryWriteRequest(
            ItemId: itemId, PriceType: ItemPriceTypes.Base, PriceGroupId: null,
            OldAmount: 10m, NewAmount: 20m, ChangedBy: "u", ChangedAt: DateTimeOffset.UtcNow, Reason: null));
        await sut.WriteAsync(new ItemPriceHistoryWriteRequest(
            ItemId: otherItemId, PriceType: ItemPriceTypes.Base, PriceGroupId: null,
            OldAmount: null, NewAmount: 5m, ChangedBy: "u", ChangedAt: DateTimeOffset.UtcNow, Reason: null));

        var rows = await sut.QueryAsync(itemId);

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(x => x.ItemId == itemId);
        rows[0].NewAmount.Should().Be(20m);
        rows[1].NewAmount.Should().Be(10m);
    }

    [Fact]
    [Trait("Category", "PriceHistory")]
    public async Task QueryAsync_WhenGroupOverride_ShouldCarryPriceGroupId()
    {
        await using var db = CreateDbContext();
        var itemId = await SeedItemAsync(db);
        var sut = new ItemPriceHistoryService(db);

        await sut.WriteAsync(new ItemPriceHistoryWriteRequest(
            ItemId: itemId, PriceType: ItemPriceTypes.GroupOverride, PriceGroupId: 3,
            OldAmount: null, NewAmount: 85m, ChangedBy: "u", ChangedAt: DateTimeOffset.UtcNow, Reason: null));

        var row = (await sut.QueryAsync(itemId)).Single();
        row.PriceType.Should().Be(ItemPriceTypes.GroupOverride);
        row.PriceGroupId.Should().Be(3);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"item-price-history-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static async Task<int> SeedItemAsync(WarehouseDbContext db, string sku = "SKU-1")
    {
        var item = new Item
        {
            InternalSKU = sku,
            Name = "Test Item",
            BaseUoM = "PCS",
            Status = "Active"
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }
}
