using ClosedXML.Excel;
using FluentAssertions;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Imports;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ImportIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private DbContextOptions<WarehouseDbContext>? _options;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();

        _options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task ImportItems_DryRun_ShouldNotWriteToDatabase()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = new WarehouseDbContext(_options!);
        await SeedMasterDataAsync(db);
        var service = new MasterDataImportService(db, new SkuGenerationService(db));

        using var stream = BuildWorkbook(
            ExcelTemplateService.ItemHeaders,
            [
                ["", "Steel Bolt", "Desc", "RAW", "PCS", "1", "1", "false", "false", "Active", "BAR-001", ""]
            ]);

        var result = await service.ImportAsync("items", stream, dryRun: true);

        result.DryRun.Should().BeTrue();
        result.Inserted.Should().Be(1);
        result.Errors.Should().BeEmpty();
        db.Items.Count().Should().Be(0);
    }

    [SkippableFact]
    public async Task ImportItems_Commit_ThenReimport_ShouldInsertThenUpdate()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = new WarehouseDbContext(_options!);
        await SeedMasterDataAsync(db);
        var service = new MasterDataImportService(db, new SkuGenerationService(db));

        using (var first = BuildWorkbook(
                   ExcelTemplateService.ItemHeaders,
                   [["RM-0001", "Bolt", "", "RAW", "PCS", "", "", "false", "false", "Active", "BAR-002", ""]]))
        {
            var firstResult = await service.ImportAsync("items", first, dryRun: false);
            firstResult.Inserted.Should().Be(1);
            firstResult.Updated.Should().Be(0);
        }

        using (var second = BuildWorkbook(
                   ExcelTemplateService.ItemHeaders,
                   [["RM-0001", "Bolt Updated", "", "RAW", "PCS", "", "", "false", "false", "Active", "BAR-002", ""]]))
        {
            var secondResult = await service.ImportAsync("items", second, dryRun: false);
            secondResult.Inserted.Should().Be(0);
            secondResult.Updated.Should().Be(1);
        }
    }

    [SkippableFact]
    public async Task ImportItems_InvalidForeignKey_ShouldReturnErrors()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = new WarehouseDbContext(_options!);
        await SeedMasterDataAsync(db);
        var service = new MasterDataImportService(db, new SkuGenerationService(db));

        using var stream = BuildWorkbook(
            ExcelTemplateService.ItemHeaders,
            [["RM-0001", "Bolt", "", "UNKNOWN", "PCS", "", "", "false", "false", "Active", "BAR-003", ""]]);

        var result = await service.ImportAsync("items", stream, dryRun: false);

        result.Errors.Should().NotBeEmpty();
        result.Inserted.Should().Be(0);
    }

    [SkippableFact]
    public async Task ImportItems_DuplicateSkuInFile_ShouldReturnValidationErrors()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = new WarehouseDbContext(_options!);
        await SeedMasterDataAsync(db);
        var service = new MasterDataImportService(db, new SkuGenerationService(db));

        using var stream = BuildWorkbook(
            ExcelTemplateService.ItemHeaders,
            [
                ["RM-0100", "Bolt", "", "RAW", "PCS", "", "", "false", "false", "Active", "", ""],
                ["RM-0100", "Nut", "", "RAW", "PCS", "", "", "false", "false", "Active", "", ""]
            ]);

        var result = await service.ImportAsync("items", stream, dryRun: false);

        result.Errors.Should().Contain(x => x.Column == "InternalSKU");
        result.Inserted.Should().Be(0);
    }

    [SkippableFact]
    public async Task ImportSuppliers_WithMoreThan1000Rows_ShouldUseBulkMode()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = new WarehouseDbContext(_options!);
        await SeedMasterDataAsync(db);
        var service = new MasterDataImportService(db, new SkuGenerationService(db));

        var rows = new List<string[]>();
        for (var i = 1; i <= 1001; i++)
        {
            rows.Add([$"SUP-{i:D4}", $"Supplier {i}", ""]);
        }

        using var stream = BuildWorkbook(ExcelTemplateService.SupplierHeaders, rows);
        var result = await service.ImportAsync("suppliers", stream, dryRun: false);

        result.UsedBulk.Should().BeTrue();
        result.Inserted.Should().Be(1001);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var db = new WarehouseDbContext(_options!);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    private static async Task SeedMasterDataAsync(WarehouseDbContext db)
    {
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw Materials" });
        db.ItemCategories.Add(new ItemCategory { Id = 2, Code = "FINISHED", Name = "Finished Goods" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        await db.SaveChangesAsync();
    }

    private static MemoryStream BuildWorkbook(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");

        for (var i = 0; i < headers.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var col = 0; col < rows[row].Length; col++)
            {
                sheet.Cell(row + 2, col + 1).Value = rows[row][col];
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
