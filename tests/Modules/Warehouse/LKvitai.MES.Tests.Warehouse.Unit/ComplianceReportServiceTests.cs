using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ComplianceReportServiceTests
{
    [Fact]
    [Trait("Category", "ComplianceReports")]
    public async Task TriggerAsync_VarianceAnalysis_ShouldWriteCsv()
    {
        await using var db = CreateDbContext();
        db.CycleCountLines.Add(new CycleCountLine
        {
            LocationId = 1,
            ItemId = 100,
            Delta = 5,
            PhysicalQty = 15,
            SystemQty = 10
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var history = await service.TriggerAsync(
            ComplianceReportType.VarianceAnalysis,
            ComplianceReportFormat.Csv,
            "TEST");

        history.Status.Should().Be(ComplianceReportStatus.Completed);
        File.Exists(history.FilePath).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "ComplianceReports")]
    public async Task ProcessDueSchedules_ShouldTriggerWhenCronMatches()
    {
        await using var db = CreateDbContext();
        db.ScheduledReports.Add(new ScheduledReport
        {
            ReportType = ComplianceReportType.VarianceAnalysis,
            Format = ComplianceReportFormat.Csv,
            Schedule = "*/1 * * * *",
            Active = true,
            CreatedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            LastStatus = ComplianceReportStatus.Pending,
            EmailRecipients = string.Empty
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var processed = await service.ProcessDueSchedulesAsync();

        processed.Should().BeGreaterThan(0);
        db.GeneratedReportHistories.Any().Should().BeTrue();
    }

    [Fact]
    public async Task LotTraceDashboardAndCsv_UseSharedStoreContract()
    {
        await using var db = CreateDbContext();
        LotTraceReport? report = new(Guid.NewGuid(), "QA-C7-shared-lot", LotTraceDirection.Forward,
            new LotTraceNode("LOT", "QA-C7", "QA-C7", DateTimeOffset.UtcNow, []), false, DateTimeOffset.UtcNow);
        var traces = new Mock<ILotTraceStore>();
        traces.SetupGet(x => x.Count).Returns(1);
        traces.Setup(x => x.TryGetAny(out report)).Returns(true);
        var directory = Path.Combine(Path.GetTempPath(), "warehouse-c7-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ComplianceReportService(db, Mock.Of<ITransactionExportService>(), traces.Object,
                Options.Create(new ComplianceReportOptions { ReportRootPath = directory }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ComplianceReportService>.Instance);
            (await service.GetDashboardAsync()).RecentTraces.Should().Be(1);
            var history = await service.TriggerAsync(ComplianceReportType.LotTrace, ComplianceReportFormat.Csv, "QA-C7");
            history.Status.Should().Be(ComplianceReportStatus.Completed);
            (await File.ReadAllTextAsync(history.FilePath!)).Should().Contain("QA-C7-shared-lot");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new WarehouseDbContext(options);
    }

    private static ComplianceReportService CreateService(WarehouseDbContext dbContext)
    {
        var transactionExport = new Mock<ITransactionExportService>();
        transactionExport
            .Setup(x => x.ExportAsync(
                It.IsAny<TransactionExportExecuteCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionExportExecutionResult(
                true,
                Guid.NewGuid(),
                0,
                new List<string> { Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv") },
                TransactionExportStatus.Completed,
                null));

        var options = Options.Create(new ComplianceReportOptions
        {
            ReportRootPath = Path.Combine(Path.GetTempPath(), "compliance-tests")
        });

        var logger = new Mock<ILogger<ComplianceReportService>>().Object;

        return new ComplianceReportService(
            dbContext,
            transactionExport.Object,
            new InMemoryLotTraceStore(),
            options,
            logger);
    }
}
