using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>C8e: the item list endpoint is what every item picker searches; WebUI parsing helpers.</summary>
public class ItemPickerSearchTests
{
    [Fact]
    public async Task Search_ShouldMatchSkuOrName_ReturnTopPageAndPutExactSkuFirst()
    {
        await using var db = CreateDb();
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "C", Name = "Cat" });
        // 30 items whose SKU contains the term, plus the exact match which sorts last by plain SKU order.
        for (var i = 1; i <= 30; i++)
        {
            db.Items.Add(NewItem(i, $"{i:00}1514251720", $"Fabric {i}"));
        }
        db.Items.Add(NewItem(100, "1514251720", "Target fabric"));
        db.Items.Add(NewItem(101, "ZZ-OTHER", "Contains 1514251720 in name"));
        db.Items.Add(NewItem(102, "BOLT-1", "Bolt"));
        await db.SaveChangesAsync();

        var page = await SearchAsync(db, "1514251720", pageSize: 20);

        page.PageSize.Should().Be(20);
        page.Items.Should().HaveCount(20);
        page.TotalCount.Should().BeGreaterThan(20);
        page.Items[0].InternalSKU.Should().Be("1514251720");
        page.Items.Should().NotContain(x => x.InternalSKU == "BOLT-1");
    }

    [Fact]
    public async Task Search_ShouldBeCaseInsensitiveAndFindByName()
    {
        await using var db = CreateDb();
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "C", Name = "Cat" });
        db.Items.Add(NewItem(1, "RM-BOLT-M8", "Hex Bolt M8"));
        db.Items.Add(NewItem(2, "RM-NUT-M8", "Hex Nut M8"));
        await db.SaveChangesAsync();

        (await SearchAsync(db, "hex bolt")).Items.Select(x => x.InternalSKU).Should().Equal("RM-BOLT-M8");
        (await SearchAsync(db, "rm-nut")).Items.Select(x => x.InternalSKU).Should().Equal("RM-NUT-M8");
        (await SearchAsync(db, "m8")).Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_ShouldHonourActiveStatusFilter()
    {
        await using var db = CreateDb();
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "C", Name = "Cat" });
        db.Items.Add(NewItem(1, "A-1", "Active one"));
        var inactive = NewItem(2, "A-2", "Inactive one");
        inactive.Status = "Inactive";
        db.Items.Add(inactive);
        await db.SaveChangesAsync();

        (await SearchAsync(db, "A-", status: "Active")).Items.Select(x => x.InternalSKU).Should().Equal("A-1");
    }

    [Theory]
    [InlineData("5.00", 5.00)]
    [InlineData("5,00", 5.00)]
    [InlineData("  5,5 ", 5.5)]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("-3.25", -3.25)]
    [InlineData("0,001", 0.001)]
    public void DecimalInputParser_ShouldAcceptDotAndCommaRegardlessOfCulture(string text, double expected)
    {
        DecimalInputParser.TryParse(text, out var value).Should().BeTrue();
        value.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DecimalInputParser_ShouldTreatBlankAsEmpty(string text)
    {
        DecimalInputParser.TryParse(text, out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("5.0.0x")]
    [InlineData("--5")]
    public void DecimalInputParser_ShouldRejectGarbage(string text)
        => DecimalInputParser.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void ValuationApprovedBy_ShouldResolveUserIdToUsernameElseKeepActor()
    {
        var id = Guid.NewGuid();
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [id.ToString()] = "anna" };

        ValuationController.ResolveUserName(id.ToString().ToUpperInvariant(), lookup).Should().Be("anna");
        ValuationController.ResolveUserName("manager-7", lookup).Should().Be("manager-7");
        ValuationController.ResolveUserName(null, lookup).Should().BeNull();
    }

    [Fact]
    public void ActorLabel_ShouldFlagOpaqueIdsAndShortenThem()
    {
        ActorLabel.LooksLikeOpaqueId(Guid.NewGuid().ToString()).Should().BeTrue();
        ActorLabel.LooksLikeOpaqueId("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b").Should().BeTrue();
        ActorLabel.LooksLikeOpaqueId("anna").Should().BeFalse();
        ActorLabel.LooksLikeOpaqueId("system").Should().BeFalse();
        ActorLabel.Short("9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B").Should().Be("bf4f1b");
    }

    private static async Task<ItemsController.PagedResponse<ItemsController.ItemListItemDto>> SearchAsync(
        WarehouseDbContext db, string search, int pageSize = 50, string? status = null)
    {
        var controller = new ItemsController(
            db,
            Mock.Of<ISkuGenerationService>(),
            Mock.Of<IItemPhotoService>(),
            Mock.Of<IItemImageSearchCapabilityService>(),
            Mock.Of<IItemPriceHistoryService>(),
            Mock.Of<ICurrentUserService>());

        var result = await controller.GetAsync(search, null, status, 1, pageSize);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeOfType<ItemsController.PagedResponse<ItemsController.ItemListItemDto>>().Subject;
    }

    private static Item NewItem(int id, string sku, string name) => new()
    {
        Id = id,
        InternalSKU = sku,
        Name = name,
        CategoryId = 1,
        BaseUoM = "PCS",
        Status = "Active"
    };

    private static WarehouseDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"item-picker-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }
}
