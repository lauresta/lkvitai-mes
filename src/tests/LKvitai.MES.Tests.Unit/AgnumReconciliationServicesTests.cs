using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AgnumReconciliationServicesTests
{
    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public async Task GenerateAsync_ShouldCalculateVarianceAndSummary()
    {
        await using var db = CreateDbContext();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var exportPath = WriteExportCsv(
            "ExportDate,AccountCode,SKU,ItemName,Quantity,UnitCost,OnHandValue\n" +
            $"{date:yyyy-MM-dd},1500-RAW,RM-0001,Raw Material 1,10,2,20\n" +
            $"{date:yyyy-MM-dd},1510-FG,FG-0001,Finished Good 1,5,3,15\n");
        SeedHistory(db, date, exportPath);

        var sut = CreateService(db);
        await using var agnumCsv = BuildCsvStream(
            "SKU,AgnumBalance\n" +
            "RM-0001,18\n" +
            "FG-0001,20\n");

        var report = await sut.GenerateAsync(date, agnumCsv);

        report.Lines.Should().HaveCount(2);
        report.Summary.TotalVariance.Should().Be(-3m);
        report.Summary.ItemsWithVariance.Should().Be(2);
        report.Summary.LargestVarianceSku.Should().Be("FG-0001");
        report.Summary.LargestVarianceAmount.Should().Be(5m);
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public async Task GenerateAsync_WhenExportMissing_ShouldThrow()
    {
        await using var db = CreateDbContext();
        var sut = CreateService(db);
        await using var agnumCsv = BuildCsvStream("SKU,AgnumBalance\nRM-0001,20\n");

        var act = async () => await sut.GenerateAsync(DateOnly.FromDateTime(DateTime.UtcNow.Date), agnumCsv);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No successful Agnum export history found*");
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public async Task GenerateAsync_ShouldSupportAmountHeader()
    {
        await using var db = CreateDbContext();
        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        var exportPath = WriteExportCsv(
            "ExportDate,AccountCode,SKU,ItemName,Quantity,UnitCost,OnHandValue\n" +
            $"{date:yyyy-MM-dd},1500-RAW,RM-0001,Raw Material 1,10,2,20\n");
        SeedHistory(db, date, exportPath);

        var sut = CreateService(db);
        await using var agnumCsv = BuildCsvStream("SKU,Amount\nRM-0001,19\n");

        var report = await sut.GenerateAsync(date, agnumCsv);

        report.Lines.Should().ContainSingle();
        report.Lines.Single().Variance.Should().Be(1m);
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public void ApplyFilters_ShouldFilterByAccountCode()
    {
        var sut = CreateService(CreateDbContext());
        var report = BuildReport([
            new AgnumReconciliationLine("1500-RAW", "RM-0001", "Raw", 10m, 2m, 20m, 18m, 2m, 11.11m),
            new AgnumReconciliationLine("1510-FG", "FG-0001", "Fg", 5m, 3m, 15m, 20m, -5m, -25m)
        ]);

        var filtered = sut.ApplyFilters(report, "1500-RAW", null, null);

        filtered.Lines.Should().ContainSingle();
        filtered.Lines.Single().Sku.Should().Be("RM-0001");
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public void ApplyFilters_ShouldFilterByThresholdAmountOrPercent()
    {
        var sut = CreateService(CreateDbContext());
        var report = BuildReport([
            new AgnumReconciliationLine("1500-RAW", "RM-0001", "Raw", 10m, 2m, 20m, 19m, 1m, 5.26m),
            new AgnumReconciliationLine("1510-FG", "FG-0001", "Fg", 5m, 3m, 15m, 20m, -5m, -25m),
            new AgnumReconciliationLine("1520-OT", "OT-0001", "Other", 1m, 1m, 1m, 0.5m, 0.5m, 100m)
        ]);

        var filtered = sut.ApplyFilters(report, null, 4m, 80m);

        filtered.Lines.Select(x => x.Sku).Should().BeEquivalentTo(["FG-0001", "OT-0001"]);
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public void ApplyFilters_ShouldRecalculateSummary()
    {
        var sut = CreateService(CreateDbContext());
        var report = BuildReport([
            new AgnumReconciliationLine("1500-RAW", "RM-0001", "Raw", 10m, 2m, 20m, 18m, 2m, 11.11m),
            new AgnumReconciliationLine("1510-FG", "FG-0001", "Fg", 5m, 3m, 15m, 20m, -5m, -25m)
        ]);

        var filtered = sut.ApplyFilters(report, null, 3m, null);

        filtered.Lines.Should().ContainSingle();
        filtered.Summary.TotalVariance.Should().Be(-5m);
        filtered.Summary.ItemsWithVariance.Should().Be(1);
        filtered.Summary.LargestVarianceSku.Should().Be("FG-0001");
    }

    private static AgnumReconciliationReport BuildReport(IReadOnlyList<AgnumReconciliationLine> lines)
    {
        var totalVariance = lines.Sum(x => x.Variance);
        var largest = lines.OrderByDescending(x => Math.Abs(x.Variance)).FirstOrDefault();

        return new AgnumReconciliationReport(
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DateTimeOffset.UtcNow,
            lines,
            new AgnumReconciliationSummary(
                totalVariance,
                lines.Count(x => Math.Abs(x.Variance) > 0.01m),
                largest?.Sku,
                largest is null ? 0m : Math.Abs(largest.Variance)));
    }

    private static AgnumReconciliationService CreateService(WarehouseDbContext dbContext)
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<AgnumReconciliationService>();
        return new AgnumReconciliationService(dbContext, logger);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"agnum-recon-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static void SeedHistory(WarehouseDbContext db, DateOnly date, string filePath)
    {
        var exportedAt = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).AddHours(23);

        db.AgnumExportHistories.Add(new AgnumExportHistory
        {
            Id = Guid.NewGuid(),
            ExportConfigId = Guid.NewGuid(),
            ExportNumber = "AGX-TEST-001",
            ExportedAt = exportedAt,
            Status = AgnumExportStatus.Success,
            RowCount = 2,
            FilePath = filePath,
            Trigger = "MANUAL"
        });

        db.SaveChanges();
    }

    private static string WriteExportCsv(string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"agnum-export-{Guid.NewGuid():N}.csv");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private static MemoryStream BuildCsvStream(string content)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
    }
}
