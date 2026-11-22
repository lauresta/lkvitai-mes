using ClosedXML.Excel;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Imports;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static LKvitai.MES.Modules.Warehouse.Api.Controllers.ItemIntrastatBulkController;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Bulk Intrastat item data: shared rules, the bulk endpoint and the Excel import columns.</summary>
public class ItemIntrastatBulkTests
{
    // ---- rules ----

    [Theory]
    [InlineData("12345678", "DE", "p/s", 2.5, 0)]
    [InlineData("1234567", null, null, null, 1)]
    [InlineData("1234567A", null, null, null, 1)]
    [InlineData(null, "D", null, null, 1)]
    [InlineData(null, "D1", null, null, 1)]
    [InlineData(null, "DE", "m2", 1.0, 1)]
    [InlineData(null, null, "m2 ", null, 2)]
    [InlineData(null, null, "MTK", 0.0, 1)]
    [InlineData(null, null, null, 3.0, 1)]
    [InlineData("  ", " ", " ", null, 0)]
    public void Validate_ShouldCountProblems(string? code, string? country, string? unit, double? factor, int expected)
    {
        var problems = IntrastatItemRules.Validate(code, country, unit, factor is null ? null : (decimal)factor);

        problems.Should().HaveCount(expected);
    }

    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("0,5", 0.5)]
    [InlineData(" 12.250 ", 12.25)]
    [InlineData("1 5", null)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void TryParseDecimal_ShouldAcceptDotAndCommaRegardlessOfCulture(string text, double? expected)
    {
        var ok = IntrastatItemRules.TryParseDecimal(text, out var value);

        ok.Should().Be(expected is not null);
        if (expected is not null)
        {
            value.Should().Be((decimal)expected.Value);
        }
    }

    [Fact]
    public void Apply_ShouldChangeOnlyProvidedFieldsAndReportNoChangeForSameValues()
    {
        var item = new Item { CommodityCode = "11111111", OriginCountry = "LT", NetWeightKg = 2m };

        IntrastatItemRules.Apply(item, null, "de", null, null).Should().BeTrue();
        item.CommodityCode.Should().Be("11111111");
        item.OriginCountry.Should().Be("DE");
        item.NetWeightKg.Should().Be(2m);

        IntrastatItemRules.Apply(item, "11111111", "DE", null, null).Should().BeFalse();
    }

    // ---- bulk endpoint ----

    [Fact]
    public async Task Bulk_ByIds_ShouldSetOnlyFilledFieldsAndKeepOthers()
    {
        await using var db = CreateDb();
        db.Items.Add(NewItem(1, "A-1", commodity: "11111111", origin: "LT"));
        db.Items.Add(NewItem(2, "A-2"));
        db.Items.Add(NewItem(3, "A-3"));
        await db.SaveChangesAsync();

        var (status, result) = await RunAsync(db, new BulkIntrastatRequestDto([1, 2], null, null, "de", null, null));

        status.Should().Be(StatusCodes.Status200OK);
        result!.Should().Be(new BulkIntrastatResultDto(2, 2, 0, false));
        var items = db.Items.AsNoTracking().OrderBy(i => i.Id).ToList();
        items[0].CommodityCode.Should().Be("11111111");
        items[0].OriginCountry.Should().Be("DE");
        items[1].OriginCountry.Should().Be("DE");
        items[2].OriginCountry.Should().BeNull();
    }

    [Fact]
    public async Task Bulk_DryRun_ShouldReturnCountsWithoutSaving()
    {
        await using var db = CreateDb();
        db.Items.Add(NewItem(1, "A-1", origin: "DE"));
        db.Items.Add(NewItem(2, "A-2"));
        await db.SaveChangesAsync();

        var (_, result) = await RunAsync(db, new BulkIntrastatRequestDto([1, 2], null, null, "DE", null, null, DryRun: true));

        result.Should().Be(new BulkIntrastatResultDto(2, 1, 1, true));
        db.Items.AsNoTracking().Count(i => i.OriginCountry == "DE").Should().Be(1);
    }

    [Fact]
    public async Task Bulk_ByFilter_ShouldUseCategoryAndMissingDataFilter()
    {
        await using var db = CreateDb();
        db.Items.Add(NewItem(1, "A-1", category: 1));
        db.Items.Add(NewItem(2, "A-2", category: 1, commodity: "11111111", weight: 1m, origin: "LT"));
        db.Items.Add(NewItem(3, "A-3", category: 2));
        var service = NewItem(4, "A-4", category: 1);
        service.ItemType = ItemType.Service;
        db.Items.Add(service);
        await db.SaveChangesAsync();

        var filter = new ItemSelectionFilter(null, 1, null, IntrastatMissing: true);
        var (_, result) = await RunAsync(db, new BulkIntrastatRequestDto(null, filter, "73181500", null, null, null));

        result.Should().Be(new BulkIntrastatResultDto(1, 1, 0, false));
        db.Items.AsNoTracking().Single(i => i.Id == 1).CommodityCode.Should().Be("73181500");
        db.Items.AsNoTracking().Single(i => i.Id == 2).CommodityCode.Should().Be("11111111");
        db.Items.AsNoTracking().Single(i => i.Id == 3).CommodityCode.Should().BeNull();
    }

    [Fact]
    public async Task Bulk_SupplementaryUnit_ShouldBeSetWithFactor()
    {
        await using var db = CreateDb();
        db.Items.Add(NewItem(1, "A-1"));
        await db.SaveChangesAsync();

        await RunAsync(db, new BulkIntrastatRequestDto([1], null, null, null, "MTK", 1.5m));

        var item = db.Items.AsNoTracking().Single();
        item.SupplementaryUnit.Should().Be("MTK");
        item.SupplementaryUnitFactor.Should().Be(1.5m);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Bulk_ShouldRejectInvalidRequestsAndChangeNothing(BulkIntrastatRequestDto request)
    {
        await using var db = CreateDb();
        db.Items.Add(NewItem(1, "A-1"));
        await db.SaveChangesAsync();

        var (status, _) = await RunAsync(db, request);

        status.Should().Be(StatusCodes.Status400BadRequest);
        db.Items.AsNoTracking().Single().CommodityCode.Should().BeNull();
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [new BulkIntrastatRequestDto([1], null, "1234", null, null, null)];
        yield return [new BulkIntrastatRequestDto([1], null, null, "DEU", null, null)];
        yield return [new BulkIntrastatRequestDto([1], null, null, null, "MTK", null)];
        yield return [new BulkIntrastatRequestDto([1], null, null, null, "MT", 1m)];
        yield return [new BulkIntrastatRequestDto([1], null, null, null, null, null)];
        yield return [new BulkIntrastatRequestDto(null, null, "73181500", null, null, null)];
    }

    [Fact]
    public void Bulk_ShouldRequireManagerOrAdminLikeItemUpdate()
    {
        var bulk = typeof(ItemIntrastatBulkController).GetMethod(nameof(ItemIntrastatBulkController.SetAsync))!;
        var update = typeof(ItemsController).GetMethod(nameof(ItemsController.UpdateAsync))!;

        PolicyOf(bulk).Should().Be(PolicyOf(update));
    }

    // ---- items list filter ----

    [Fact]
    public async Task ItemsList_IntrastatMissingFilter_ShouldListOnlyStockItemsWithGaps()
    {
        await using var db = CreateDb();
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "C", Name = "Cat" });
        db.Items.Add(NewItem(1, "A-1", commodity: "11111111", weight: 1m, origin: "LT"));
        db.Items.Add(NewItem(2, "A-2", commodity: "11111111", origin: "LT"));
        db.Items.Add(NewItem(3, "A-3", commodity: "11111111", weight: 1m));
        db.Items.Add(NewItem(4, "A-4", weight: 1m, origin: "LT"));
        var service = NewItem(5, "A-5");
        service.ItemType = ItemType.Service;
        db.Items.Add(service);
        await db.SaveChangesAsync();

        var controller = new ItemsController(
            db, Mock.Of<ISkuGenerationService>(), Mock.Of<IItemPhotoService>(),
            Mock.Of<IItemImageSearchCapabilityService>(), Mock.Of<IItemPriceHistoryService>(), Mock.Of<ICurrentUserService>());
        var ok = (await controller.GetAsync(null, null, null, 1, 50, null, true)).Should().BeOfType<OkObjectResult>().Subject;
        var page = ok.Value.Should().BeOfType<ItemsController.PagedResponse<ItemsController.ItemListItemDto>>().Subject;

        page.Items.Select(x => x.InternalSKU).Should().Equal(["A-2", "A-3", "A-4"]);
    }

    // ---- Excel import ----

    [Fact]
    public async Task Import_ShouldSetIntrastatColumnsOnInsert()
    {
        await using var db = CreateImportDb();
        var (result, _) = await ImportAsync(db, [Row("NEW-1", "Fabric", "73181500", "0,25", "de", "MTK", "1.5")]);

        result.Errors.Should().BeEmpty();
        result.Inserted.Should().Be(1);
        var item = db.Items.AsNoTracking().Single();
        item.CommodityCode.Should().Be("73181500");
        item.NetWeightKg.Should().Be(0.25m);
        item.OriginCountry.Should().Be("DE");
        item.SupplementaryUnit.Should().Be("MTK");
        item.SupplementaryUnitFactor.Should().Be(1.5m);
    }

    [Fact]
    public async Task Import_EmptyIntrastatCells_ShouldLeaveExistingValuesOnUpdate()
    {
        var dbName = $"item-intrastat-import-{Guid.NewGuid():N}";
        await using var db = CreateImportDb(dbName);
        db.Items.Add(NewItem(1, "OLD-1", commodity: "11111111", weight: 3m, origin: "LT"));
        await db.SaveChangesAsync();

        await using var importDb = CreateDb(dbName);
        var (result, _) = await ImportAsync(importDb, [Row("OLD-1", "Renamed", "", "", "PL", "", "")]);

        result.Errors.Should().BeEmpty();
        result.Updated.Should().Be(1);
        var item = importDb.Items.AsNoTracking().Single();
        item.Name.Should().Be("Renamed");
        item.CommodityCode.Should().Be("11111111");
        item.NetWeightKg.Should().Be(3m);
        item.OriginCountry.Should().Be("PL");
    }

    [Theory]
    [InlineData("1234567", "", "", "", "", "CommodityCode")]
    [InlineData("", "0", "", "", "", "NetWeightKg")]
    [InlineData("", "abc", "", "", "", "NetWeightKg")]
    [InlineData("", "", "DEU", "", "", "OriginCountry")]
    [InlineData("", "", "", "MTK", "", "SupplementaryUnitFactor")]
    [InlineData("", "", "", "", "2", "SupplementaryUnit")]
    [InlineData("", "", "", "MT", "2", "SupplementaryUnit")]
    [InlineData("", "", "", "MTK", "0", "SupplementaryUnitFactor")]
    public async Task Import_InvalidIntrastatCells_ShouldBeReportedPerColumnAndWriteNothing(
        string code, string weight, string country, string unit, string factor, string expectedColumn)
    {
        await using var db = CreateImportDb();

        var (result, _) = await ImportAsync(db, [Row("NEW-1", "Fabric", code, weight, country, unit, factor)]);

        result.Errors.Should().ContainSingle(e => e.Column == expectedColumn);
        result.Inserted.Should().Be(0);
        db.Items.Count().Should().Be(0);
    }

    [Fact]
    public async Task Import_LegacyFileWithoutIntrastatColumns_ShouldStillImport()
    {
        await using var db = CreateImportDb();
        using var stream = BuildWorkbook(
            ExcelTemplateService.ItemHeaders.Take(ExcelTemplateService.ItemRequiredHeaderCount).ToArray(),
            [Row("NEW-1", "Fabric", "", "", "", "", "").Take(ExcelTemplateService.ItemRequiredHeaderCount).ToArray()]);

        var result = await new MasterDataImportService(db, Mock.Of<ISkuGenerationService>()).ImportAsync("items", stream, dryRun: false);

        result.Errors.Should().BeEmpty();
        result.Inserted.Should().Be(1);
    }

    [Fact]
    public async Task Template_ShouldCarryIntrastatColumnsAndTheyMatchTheImportHeaders()
    {
        await using var db = CreateImportDb();
        var bytes = await new ExcelTemplateService(db).GenerateTemplateAsync("items");

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Import");
        var headers = Enumerable.Range(1, ExcelTemplateService.ItemHeaders.Length).Select(c => sheet.Cell(1, c).GetString()).ToArray();

        headers.Should().Equal(ExcelTemplateService.ItemHeaders);
        headers.Should().Contain(["CommodityCode", "NetWeightKg", "OriginCountry", "SupplementaryUnit", "SupplementaryUnitFactor"]);
    }

    // ---- helpers ----

    private static string PolicyOf(System.Reflection.MethodInfo method)
        => ((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)method
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Single()).Policy!;

    private static async Task<(int Status, BulkIntrastatResultDto? Result)> RunAsync(WarehouseDbContext db, BulkIntrastatRequestDto request)
    {
        var controller = new ItemIntrastatBulkController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var action = await controller.SetAsync(request);
        return action switch
        {
            OkObjectResult ok => (StatusCodes.Status200OK, (BulkIntrastatResultDto)ok.Value!),
            ObjectResult other => (other.StatusCode ?? 0, null),
            _ => (0, null)
        };
    }

    private static string[] Row(string sku, string name, string code, string weight, string country, string unit, string factor)
        => [sku, name, "", "RAW", "PCS", "", "", "false", "false", "Active", "", "", code, weight, country, unit, factor];

    private static async Task<(ImportExecutionResult Result, int Rows)> ImportAsync(WarehouseDbContext db, IReadOnlyList<string[]> rows)
    {
        using var stream = BuildWorkbook(ExcelTemplateService.ItemHeaders, rows);
        var result = await new MasterDataImportService(db, Mock.Of<ISkuGenerationService>()).ImportAsync("items", stream, dryRun: false);
        return (result, rows.Count);
    }

    private static MemoryStream BuildWorkbook(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");
        for (var i = 0; i < headers.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static Item NewItem(
        int id, string sku, int category = 1, string? commodity = null, decimal? weight = null, string? origin = null) => new()
    {
        Id = id,
        InternalSKU = sku,
        Name = sku,
        CategoryId = category,
        BaseUoM = "PCS",
        Status = "Active",
        CommodityCode = commodity,
        NetWeightKg = weight,
        OriginCountry = origin
    };

    private static WarehouseDbContext CreateDb(string? name = null)
        => new(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(name ?? $"item-intrastat-bulk-{Guid.NewGuid():N}")
            .Options);

    private static WarehouseDbContext CreateImportDb(string? name = null)
    {
        var db = CreateDb(name);
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Quantity" });
        db.SaveChanges();
        return db;
    }
}
