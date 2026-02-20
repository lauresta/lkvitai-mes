using ClosedXML.Excel;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Models;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Imports;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ImportControllerIntegrationTests : IAsyncLifetime
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
    public async Task ImportItems_DryRunThenCommit_HappyPathThroughApiController()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        await SeedMasterDataAsync(db);
        var controller = CreateController(db);

        using var dryRunWorkbook = BuildWorkbook(
            ExcelTemplateService.ItemHeaders,
            [["RM-1101", "Bolt", "", "RAW", "PCS", "", "", "false", "false", "Active", "BAR-1101", ""]]);

        var dryRun = await controller.ImportAsync(
            entityType: "items",
            dryRun: true,
            file: CreateFormFile(dryRunWorkbook, "items-dry-run.xlsx"));

        var dryRunOk = dryRun.Should().BeOfType<OkObjectResult>().Subject;
        var dryRunPayload = dryRunOk.Value.Should().BeOfType<ImportExecutionResult>().Subject;
        dryRunPayload.DryRun.Should().BeTrue();
        dryRunPayload.Inserted.Should().Be(1);
        dryRunPayload.Errors.Should().BeEmpty();

        (await db.Items.CountAsync()).Should().Be(0);

        using var commitWorkbook = BuildWorkbook(
            ExcelTemplateService.ItemHeaders,
            [["RM-1101", "Bolt", "", "RAW", "PCS", "", "", "false", "false", "Active", "BAR-1101", ""]]);

        var commit = await controller.ImportAsync(
            entityType: "items",
            dryRun: false,
            file: CreateFormFile(commitWorkbook, "items-commit.xlsx"));

        var commitOk = commit.Should().BeOfType<OkObjectResult>().Subject;
        var commitPayload = commitOk.Value.Should().BeOfType<ImportExecutionResult>().Subject;
        commitPayload.DryRun.Should().BeFalse();
        commitPayload.Inserted.Should().Be(1);
        commitPayload.Errors.Should().BeEmpty();

        var item = await db.Items.AsNoTracking().SingleAsync();
        item.InternalSKU.Should().Be("RM-1101");
        item.PrimaryBarcode.Should().Be("BAR-1101");
    }

    [SkippableFact]
    public async Task ImportItems_DuplicateBarcode_ShouldReturnValidationErrors()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        await SeedMasterDataAsync(db);
        var controller = CreateController(db);

        using var workbook = BuildWorkbook(
            ExcelTemplateService.ItemHeaders,
            [
                ["RM-2101", "Bolt", "", "RAW", "PCS", "", "", "false", "false", "Active", "BAR-DUP-1", ""],
                ["RM-2102", "Nut", "", "RAW", "PCS", "", "", "false", "false", "Active", "BAR-DUP-1", ""]
            ]);

        var result = await controller.ImportAsync(
            entityType: "items",
            dryRun: false,
            file: CreateFormFile(workbook, "items-duplicate-barcode.xlsx"));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ImportExecutionResult>().Subject;

        payload.Inserted.Should().Be(0);
        payload.Errors.Should().Contain(x => x.Column == "PrimaryBarcode");
        (await db.Items.CountAsync()).Should().Be(0);
    }

    [SkippableFact]
    public async Task ImportItems_MissingForeignKey_ShouldReturnValidationErrors()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        await SeedMasterDataAsync(db);
        var controller = CreateController(db);

        using var workbook = BuildWorkbook(
            ExcelTemplateService.ItemHeaders,
            [["RM-3101", "Bolt", "", "UNKNOWN", "PCS", "", "", "false", "false", "Active", "BAR-3101", ""]]);

        var result = await controller.ImportAsync(
            entityType: "items",
            dryRun: false,
            file: CreateFormFile(workbook, "items-missing-fk.xlsx"));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ImportExecutionResult>().Subject;

        payload.Inserted.Should().Be(0);
        payload.Errors.Should().Contain(x => x.Column == "CategoryCode");
        (await db.Items.CountAsync()).Should().Be(0);
    }

    private WarehouseDbContext CreateDbContext()
        => new(_options!, new StaticCurrentUserService("import-admin"));

    private ImportController CreateController(WarehouseDbContext db)
    {
        var controller = new ImportController(
            new ExcelTemplateService(db),
            new MasterDataImportService(db, new SkuGenerationService(db)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    private static async Task SeedMasterDataAsync(WarehouseDbContext db)
    {
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw Materials" });
        db.ItemCategories.Add(new ItemCategory { Id = 2, Code = "FINISHED", Name = "Finished Goods" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        await db.SaveChangesAsync();
    }

    private static IFormFile CreateFormFile(MemoryStream stream, string fileName)
    {
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
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

    private sealed class StaticCurrentUserService : ICurrentUserService
    {
        private readonly string _userId;

        public StaticCurrentUserService(string userId)
        {
            _userId = userId;
        }

        public string GetCurrentUserId() => _userId;
    }
}
