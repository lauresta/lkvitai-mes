using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class AgnumPriceSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_UpdatesOnlyLinkedSellingPricesAndWritesHistoryOnce()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new WarehouseDbContext(options);
        db.Items.AddRange(
            new Item { Id = 1, InternalSKU = "A", Name = "A", BasePrice = 12m, PurchasePrice = 5m },
            new Item { Id = 2, InternalSKU = "B", Name = "B", BasePrice = 7m, PurchasePrice = 3m });
        db.AgnumProductLinks.Add(new AgnumProductLink { ItemId = 1, SndId = 493, AgnumProductId = 11, AgnumCode = "A" });
        await db.SaveChangesAsync();
        var client = new Mock<IAgnumApiClient>();
        client.Setup(x => x.GetPricesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AgnumPriceDto>(StringComparer.OrdinalIgnoreCase) {
                ["a"] = new(9m, 10m), ["B"] = new(4m, 6m)
            });
        var factory = new Mock<IAgnumApiClientFactory>();
        factory.Setup(x => x.GetForSndId(493)).Returns(client.Object);
        var sut = new AgnumPriceSyncService(db, factory.Object);

        (await sut.SyncAsync(493)).Should().Be(2);
        (await sut.SyncAsync(493)).Should().Be(0);

        (await db.Items.FindAsync(1))!.BasePrice.Should().Be(10m);
        (await db.Items.FindAsync(1))!.PurchasePrice.Should().Be(5m);
        (await db.Items.FindAsync(2))!.BasePrice.Should().Be(7m);
        (await db.ItemPriceOverrides.SingleAsync()).Amount.Should().Be(9m);
        var history = await db.ItemPriceHistories.ToListAsync();
        history.Should().HaveCount(2).And.OnlyContain(x => x.Reason == "agnum-sync");
        history.Should().ContainSingle(x => x.PriceType == ItemPriceTypes.Base && x.OldAmount == 12m && x.NewAmount == 10m);
        history.Should().ContainSingle(x => x.PriceType == ItemPriceTypes.GroupOverride && x.OldAmount == null && x.NewAmount == 9m);
    }
}
