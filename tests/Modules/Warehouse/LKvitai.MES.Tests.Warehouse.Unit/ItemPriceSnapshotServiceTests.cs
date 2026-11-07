using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class ItemPriceSnapshotServiceTests
{
    [Fact]
    public async Task GetAsync_MatchesSkuCaseInsensitively_UsesLatestWac_AndFallsBackToPurchase()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new WarehouseDbContext(options);
        var category = new ItemCategory { Code = "TEST", Name = "Test" };
        db.ItemCategories.Add(category);
        await db.SaveChangesAsync();
        var first = new Item { InternalSKU = "CODE-1", Name = "First", BaseUoM = "pcs",
            CategoryId = category.Id, PurchasePrice = 8m, BasePrice = 15m };
        var second = new Item { InternalSKU = "CODE-2", Name = "Second", BaseUoM = "pcs",
            CategoryId = category.Id, PurchasePrice = 7m };
        db.Items.AddRange(first, second);
        await db.SaveChangesAsync();
        db.OnHandValues.AddRange(
            new OnHandValue { Id = Guid.NewGuid(), ItemId = first.Id, ItemSku = first.InternalSKU,
                UnitCost = 20m, LastUpdated = DateTimeOffset.UtcNow.AddDays(-1) },
            new OnHandValue { Id = Guid.NewGuid(), ItemId = first.Id, ItemSku = first.InternalSKU,
                UnitCost = 10m, LastUpdated = DateTimeOffset.UtcNow },
            new OnHandValue { Id = Guid.NewGuid(), ItemId = second.Id, ItemSku = second.InternalSKU,
                UnitCost = 0m, LastUpdated = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var rows = await new ItemPriceSnapshotService(db).GetAsync([], ["code-1", "CODE-2"], default);

        rows.Should().HaveCount(2);
        rows.Single(x => x.ItemId == first.Id).EffectiveCost.Should().Be(10m);
        rows.Single(x => x.ItemId == first.Id).CostSource.Should().Be("WAC");
        rows.Single(x => x.ItemId == first.Id).BasePrice.Should().Be(15m);
        rows.Single(x => x.ItemId == second.Id).EffectiveCost.Should().Be(7m);
        rows.Single(x => x.ItemId == second.Id).CostSource.Should().Be("Purchase");
    }
}
