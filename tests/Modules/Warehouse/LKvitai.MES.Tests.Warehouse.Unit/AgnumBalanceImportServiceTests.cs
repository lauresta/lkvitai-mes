using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumBalanceImportServiceTests
{
    [Fact]
    public async Task StartImportAsync_WhenProductsAreReturned_ShouldCreateRunAndBalances()
    {
        await using var db = CreateDbContext();
        var item = new Item { InternalSKU = "SKU-001", Name = "Item One", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.AgnumProductLinks.Add(new AgnumProductLink
        {
            ItemId = item.Id,
            SndId = 493,
            AgnumProductId = 1,
            AgnumCode = "SKU-001",
            AgnumEnabled = true,
            LastImportedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 1,
            Code = "SKU-001",
            Name = "Item One",
            Pcs = "vnt",
            Enabled = true,
            Balance = 20
        };

        var service = CreateService(db, product);

        var runId = await service.StartImportAsync(493);

        var run = await db.AgnumBalanceImportRuns.FirstOrDefaultAsync(x => x.Id == runId);
        run.Should().NotBeNull();
        run!.Status.Should().Be("Completed");
        run.ProductCount.Should().Be(1);
        run.BalanceCount.Should().Be(1);

        var balance = await db.AgnumVirtualWarehouseBalances.FirstOrDefaultAsync(x => x.ImportRunId == runId);
        balance.Should().NotBeNull();
        balance!.ItemId.Should().Be(item.Id);
        balance.Quantity.Should().Be(20);
        balance.AgnumProductId.Should().Be(1);
    }

    [Fact]
    public async Task StartImportAsync_WhenNoLinkExists_ShouldStillCreateBalanceWithNullItemId()
    {
        await using var db = CreateDbContext();
        var product = new AgnumProductDto
        {
            Id = 2,
            Code = "SKU-002",
            Name = "Unlinked Item",
            Pcs = "vnt",
            Enabled = true,
            Balance = 5
        };

        var service = CreateService(db, product);

        var runId = await service.StartImportAsync(493);

        var balance = await db.AgnumVirtualWarehouseBalances.FirstOrDefaultAsync(x => x.ImportRunId == runId);
        balance.Should().NotBeNull();
        balance!.ItemId.Should().BeNull();
        balance.Sku.Should().Be("SKU-002");
        balance.Quantity.Should().Be(5);
    }

    private static AgnumBalanceImportService CreateService(WarehouseDbContext db, params AgnumProductDto[] products)
    {
        var clientMock = new Mock<IAgnumApiClient>();
        clientMock.Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products.ToList());

        var factoryMock = new Mock<IAgnumApiClientFactory>();
        factoryMock.Setup(x => x.GetForSndId(It.IsAny<int>())).Returns(clientMock.Object);

        return new AgnumBalanceImportService(db, factoryMock.Object, new LoggerFactory().CreateLogger<AgnumBalanceImportService>());
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WarehouseDbContext(options);
    }

    private static async Task<int> GetOrCreateCategoryIdAsync(WarehouseDbContext db)
    {
        if (await db.ItemCategories.AnyAsync())
        {
            return await db.ItemCategories.Select(x => x.Id).FirstAsync();
        }

        var category = new ItemCategory { Code = "DEFAULT", Name = "Default" };
        db.ItemCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }
}
