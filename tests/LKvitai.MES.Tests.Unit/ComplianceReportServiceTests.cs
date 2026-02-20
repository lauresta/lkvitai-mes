using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

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
