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

public class AgnumImportConflictDetectionTests
{
    [Fact]
    public async Task PreviewAsync_WhenPcsIsUnknown_ShouldReturnUnknownUoM()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 1,
            Code = "SKU-UNKNOWN",
            Name = "Unknown UoM",
            Pcs = "unknown",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.Conflicts.Should().ContainSingle();
        preview.Conflicts[0].Reason.Should().Be("UnknownUoM");
    }

    [Fact]
    public async Task PreviewAsync_WhenDuplicateItemExistsWithoutLink_ShouldReturnDuplicateSku()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        db.Items.Add(new Item { InternalSKU = "SKU-001", Name = "Existing Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 2,
            Code = "SKU-001",
            Name = "Duplicate SKU",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.Conflicts.Should().ContainSingle();
        preview.Conflicts[0].Reason.Should().Be("DuplicateSku");
    }

    [Fact]
    public async Task PreviewAsync_WhenLinkPointsToDifferentItem_ShouldReturnLinkedToDifferentItem()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var linkedItem = new Item { InternalSKU = "ITEM-ONE", Name = "Linked Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) };
        var conflictingItem = new Item { InternalSKU = "SKU-002", Name = "Conflicting Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) };
        db.Items.AddRange(linkedItem, conflictingItem);
        await db.SaveChangesAsync();

        db.AgnumProductLinks.Add(new AgnumProductLink
        {
            ItemId = linkedItem.Id,
            SndId = 493,
            AgnumProductId = 3,
            AgnumCode = "SKU-001",
            AgnumEnabled = true,
            LastImportedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 3,
            Code = "SKU-002",
            Name = "Linked to wrong item",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.Conflicts.Should().ContainSingle();
        preview.Conflicts[0].Reason.Should().Be("LinkedToDifferentItem");
    }

    [Fact]
    public async Task PreviewAsync_WhenProductHasValidPcsAndNoExistingLink_ShouldReturnToCreate()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var product = new AgnumProductDto
        {
            Id = 4,
            Code = "SKU-NEW",
            Name = "New Product",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.ToCreate.Should().ContainSingle(x => x.AgnumProductId == 4 && x.Code == "SKU-NEW");
    }

    [Fact]
    public async Task PreviewAsync_WhenExistingLinkExists_ShouldReturnToUpdate()
    {
        await using var db = CreateDbContext();
        await SeedUnitOfMeasuresAsync(db, "vnt");

        var existingItem = new Item { InternalSKU = "SKU-EXIST", Name = "Existing Item", BaseUoM = "vnt", CategoryId = await GetOrCreateCategoryIdAsync(db) };
        db.Items.Add(existingItem);
        await db.SaveChangesAsync();

        db.AgnumProductLinks.Add(new AgnumProductLink
        {
            ItemId = existingItem.Id,
            SndId = 493,
            AgnumProductId = 5,
            AgnumCode = "SKU-EXIST",
            AgnumEnabled = true,
            LastImportedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var product = new AgnumProductDto
        {
            Id = 5,
            Code = "SKU-EXIST",
            Name = "Existing Product",
            Pcs = "vnt",
            Enabled = true,
            Balance = 1
        };

        var service = CreateService(db, product);

        var preview = await service.PreviewAsync(493);

        preview.ToUpdate.Should().ContainSingle(x => x.AgnumProductId == 5);
    }

    private static AgnumNomenclatureImportService CreateService(WarehouseDbContext db, params AgnumProductDto[] products)
    {
        var clientMock = new Mock<IAgnumApiClient>();
        clientMock.Setup(x => x.GetProductsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products.ToList());

        var factoryMock = new Mock<IAgnumApiClientFactory>();
        factoryMock.Setup(x => x.GetForSndId(It.IsAny<int>())).Returns(clientMock.Object);

        return new AgnumNomenclatureImportService(db, factoryMock.Object, new LoggerFactory().CreateLogger<AgnumNomenclatureImportService>());
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

    private static async Task SeedUnitOfMeasuresAsync(WarehouseDbContext db, params string[] codes)
    {
        foreach (var code in codes)
        {
            db.UnitOfMeasures.Add(new UnitOfMeasure { Code = code, Name = code, Type = "Piece" });
        }

        await db.SaveChangesAsync();
    }
}
