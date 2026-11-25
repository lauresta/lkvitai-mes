using ClosedXML.Excel;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Imports;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Excel import: optional Items.MinQty and Suppliers.Kind columns.</summary>
public class ImportMinQtyKindTests
{
    [Theory]
    [InlineData("5", 5)]
    [InlineData("2,5", 2.5)]
    [InlineData("0", 0)]
    public async Task Items_MinQtyIsInserted(string cell, double expected)
    {
        await using var db = CreateDb();

        var result = await ImportItemsAsync(db, [ItemRow("NEW-1", cell)]);

        result.Errors.Should().BeEmpty();
        result.Inserted.Should().Be(1);
        db.Items.AsNoTracking().Single().MinQty.Should().Be((decimal)expected);
    }

    [Fact]
    public async Task Items_MinQtyIsUpdated()
    {
        var name = Guid.NewGuid().ToString("N");
        await using (var seed = CreateDb(name))
        {
            seed.Items.Add(NewItem("OLD-1", 3m));
            await seed.SaveChangesAsync();
        }

        await using var db = CreateDb(name);
        var result = await ImportItemsAsync(db, [ItemRow("OLD-1", "7.5")]);

        result.Errors.Should().BeEmpty();
        result.Updated.Should().Be(1);
        db.Items.AsNoTracking().Single().MinQty.Should().Be(7.5m);
    }

    [Fact]
    public async Task Items_EmptyMinQtyKeepsExistingValue()
    {
        var name = Guid.NewGuid().ToString("N");
        await using (var seed = CreateDb(name))
        {
            seed.Items.Add(NewItem("OLD-1", 3m));
            await seed.SaveChangesAsync();
        }

        await using var db = CreateDb(name);
        var result = await ImportItemsAsync(db, [ItemRow("OLD-1", "")]);

        result.Errors.Should().BeEmpty();
        result.Updated.Should().Be(1);
        db.Items.AsNoTracking().Single().MinQty.Should().Be(3m);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task Items_InvalidMinQtyIsReportedEvenOnDryRun(string cell)
    {
        await using var db = CreateDb();

        var result = await ImportItemsAsync(db, [ItemRow("NEW-1", cell)], dryRun: true);

        var error = result.Errors.Should().ContainSingle(e => e.Column == "MinQty").Subject;
        error.Message.Should().Be("MinQty must be a number greater than or equal to 0.");
        db.Items.Count().Should().Be(0);
    }

    [Fact]
    public async Task Items_FileWithoutMinQtyColumnStillImports()
    {
        await using var db = CreateDb();
        var headers = ExcelTemplateService.ItemHeaders.Take(ExcelTemplateService.ItemRequiredHeaderCount).ToArray();
        using var stream = BuildWorkbook(headers, [ItemRow("NEW-1", "").Take(headers.Length).ToArray()]);

        var result = await new MasterDataImportService(db, Mock.Of<ISkuGenerationService>()).ImportAsync("items", stream, dryRun: false);

        result.Errors.Should().BeEmpty();
        result.Inserted.Should().Be(1);
        db.Items.AsNoTracking().Single().MinQty.Should().BeNull();
    }

    [Theory]
    [InlineData("Domestic", SupplierKind.Domestic)]
    [InlineData("eu", SupplierKind.Eu)]
    [InlineData("NONEU", SupplierKind.NonEu)]
    public async Task Suppliers_KindIsSetOnInsertAndUpdate(string cell, SupplierKind expected)
    {
        var name = Guid.NewGuid().ToString("N");
        await using (var seed = CreateDb(name))
        {
            seed.Suppliers.Add(new Supplier { Code = "OLD", Name = "Old", Kind = SupplierKind.Domestic });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateDb(name);
        var result = await ImportSuppliersAsync(db, [SupplierRow("NEW", cell), SupplierRow("OLD", cell)]);

        result.Errors.Should().BeEmpty();
        result.Inserted.Should().Be(1);
        result.Updated.Should().Be(1);
        db.Suppliers.AsNoTracking().Select(s => s.Kind).ToList().Should().OnlyContain(k => k == expected);
    }

    [Fact]
    public async Task Suppliers_EmptyKindKeepsExistingValue()
    {
        var name = Guid.NewGuid().ToString("N");
        await using (var seed = CreateDb(name))
        {
            seed.Suppliers.Add(new Supplier { Code = "OLD", Name = "Old", Kind = SupplierKind.Eu });
            await seed.SaveChangesAsync();
        }

        await using var db = CreateDb(name);
        var result = await ImportSuppliersAsync(db, [SupplierRow("OLD", "")]);

        result.Errors.Should().BeEmpty();
        db.Suppliers.AsNoTracking().Single().Kind.Should().Be(SupplierKind.Eu);
    }

    [Theory]
    [InlineData("Foreign")]
    [InlineData("2")]
    public async Task Suppliers_InvalidKindIsReportedEvenOnDryRun(string cell)
    {
        await using var db = CreateDb();

        var result = await ImportSuppliersAsync(db, [SupplierRow("NEW", cell)], dryRun: true);

        var error = result.Errors.Should().ContainSingle(e => e.Column == "Kind").Subject;
        error.Message.Should().Be("Kind must be Domestic, Eu or NonEu.");
        db.Suppliers.Count().Should().Be(0);
    }

    [Fact]
    public async Task Suppliers_FileWithoutKindColumnStillImports()
    {
        await using var db = CreateDb();
        var headers = ExcelTemplateService.SupplierHeaders.Take(ExcelTemplateService.SupplierRequiredHeaderCount).ToArray();
        using var stream = BuildWorkbook(headers, [SupplierRow("NEW", "").Take(headers.Length).ToArray()]);

        var result = await new MasterDataImportService(db, Mock.Of<ISkuGenerationService>()).ImportAsync("suppliers", stream, dryRun: false);

        result.Errors.Should().BeEmpty();
        result.Inserted.Should().Be(1);
        db.Suppliers.AsNoTracking().Single().Kind.Should().BeNull();
    }

    [Fact]
    public void Headers_AppendNewColumnsAfterTheRequiredOnes()
    {
        ExcelTemplateService.ItemHeaders.Skip(ExcelTemplateService.ItemRequiredHeaderCount).Should().EndWith("MinQty");
        ExcelTemplateService.SupplierHeaders.Skip(ExcelTemplateService.SupplierRequiredHeaderCount).Should().Equal("Kind");
        ExcelTemplateService.SupplierHeaders[ExcelTemplateService.SupplierRequiredHeaderCount - 1].Should().Be("ContactInfo");
    }

    // ---- helpers ----

    private static string[] ItemRow(string sku, string minQty)
        => [sku, "Name", "", "RAW", "PCS", "", "", "false", "false", "Active", "", "", "", "", "", "", "", minQty];

    private static string[] SupplierRow(string code, string kind)
        => [code, $"Supplier {code}", "", "", "", "", "", "", "", "", "", "", "", "", "", kind];

    private static Task<ImportExecutionResult> ImportItemsAsync(WarehouseDbContext db, IReadOnlyList<string[]> rows, bool dryRun = false)
        => ImportAsync(db, "items", ExcelTemplateService.ItemHeaders, rows, dryRun);

    private static Task<ImportExecutionResult> ImportSuppliersAsync(WarehouseDbContext db, IReadOnlyList<string[]> rows, bool dryRun = false)
        => ImportAsync(db, "suppliers", ExcelTemplateService.SupplierHeaders, rows, dryRun);

    private static Task<ImportExecutionResult> ImportAsync(
        WarehouseDbContext db, string entity, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, bool dryRun)
    {
        using var stream = BuildWorkbook(headers, rows);
        return new MasterDataImportService(db, Mock.Of<ISkuGenerationService>()).ImportAsync(entity, stream, dryRun);
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

    private static Item NewItem(string sku, decimal? minQty) => new()
    {
        Id = 1,
        InternalSKU = sku,
        Name = sku,
        CategoryId = 1,
        BaseUoM = "PCS",
        Status = "Active",
        MinQty = minQty
    };

    private static WarehouseDbContext CreateDb(string? name = null)
    {
        var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString("N"))
            .Options);
        if (!db.ItemCategories.Any())
        {
            db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw" });
            db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Quantity" });
            db.SaveChanges();
        }

        return db;
    }
}
