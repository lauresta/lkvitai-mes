using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Domain.Entities;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AdminComplianceControllerTests
{
    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task ExportTransactionsAsync_WhenFormatInvalid_ShouldReturnBadRequest()
    {
        var service = new Mock<ITransactionExportService>(MockBehavior.Strict);
        var controller = new AdminComplianceController(
            service.Object,
            Mock.Of<ILotTraceabilityService>(),
            Mock.Of<ILotTraceStore>(),
            Mock.Of<IComplianceReportService>(),
            Mock.Of<IElectronicSignatureService>());

        var result = await controller.ExportTransactionsAsync(new AdminComplianceController.ExportTransactionsRequest(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            "XML"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    [Trait("Category", "ComplianceExport")]
    public async Task GetExportsAsync_ShouldReturnOk()
    {
        var service = new Mock<ITransactionExportService>(MockBehavior.Strict);
        service.Setup(x => x.GetHistoryAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TransactionExportHistoryDto(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow.AddDays(-1),
                    DateTimeOffset.UtcNow,
                    TransactionExportFormat.Csv,
                    2,
                    ["file.csv"],
                    TransactionExportStatus.Completed,
                    null,
                    "admin",
                    DateTimeOffset.UtcNow)
            ]);

        var controller = new AdminComplianceController(
            service.Object,
            Mock.Of<ILotTraceabilityService>(),
            new InMemoryLotTraceStore(),
            Mock.Of<IComplianceReportService>(),
            Mock.Of<IElectronicSignatureService>());

        var result = await controller.GetExportsAsync();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task BuildLotTraceAsync_WhenDirectionInvalid_ShouldReturnBadRequest()
    {
        var controller = new AdminComplianceController(
            Mock.Of<ITransactionExportService>(),
            Mock.Of<ILotTraceabilityService>(),
            new InMemoryLotTraceStore(),
            Mock.Of<IComplianceReportService>(),
            Mock.Of<IElectronicSignatureService>());

        var result = await controller.BuildLotTraceAsync(new AdminComplianceController.LotTraceRequest("LOT-1", "SIDEWAYS"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BuildLotTraceAsync_WhenServiceReturnsNotFound_ShouldReturnNotFound()
    {
        var traceService = new Mock<ILotTraceabilityService>(MockBehavior.Strict);
        traceService.Setup(x => x.BuildAsync("LOT-404", LotTraceDirection.Backward, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LotTraceResult(false, null, "not found", 404));

        var controller = new AdminComplianceController(
            Mock.Of<ITransactionExportService>(),
            traceService.Object,
            new InMemoryLotTraceStore(),
            Mock.Of<IComplianceReportService>(),
            Mock.Of<IElectronicSignatureService>());

        var result = await controller.BuildLotTraceAsync(new AdminComplianceController.LotTraceRequest("LOT-404", "BACKWARD"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetLotTraceAsync_WhenMissing_ShouldReturnNotFound()
    {
        var controller = new AdminComplianceController(
            Mock.Of<ITransactionExportService>(),
            Mock.Of<ILotTraceabilityService>(),
            new InMemoryLotTraceStore(),
            Mock.Of<IComplianceReportService>(),
            Mock.Of<IElectronicSignatureService>());

        var result = controller.GetLotTraceAsync(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task BuildLotTraceAsync_WhenCsvRequested_ShouldReturnFile()
    {
        var report = new LotTraceReport(
            Guid.NewGuid(),
            "LOT-CSV",
            LotTraceDirection.Backward,
            new LotTraceNode("LOT", "LOT-CSV", "Sample", DateTimeOffset.UtcNow, []),
            false,
            DateTimeOffset.UtcNow);

        var traceService = new Mock<ILotTraceabilityService>(MockBehavior.Strict);
        traceService.Setup(x => x.BuildAsync("LOT-CSV", LotTraceDirection.Backward, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LotTraceResult(true, report, null, 200));
        traceService.Setup(x => x.BuildCsv(report)).Returns("csv-content");

        var controller = new AdminComplianceController(
            Mock.Of<ITransactionExportService>(),
            traceService.Object,
            new InMemoryLotTraceStore(),
            Mock.Of<IComplianceReportService>(),
            Mock.Of<IElectronicSignatureService>());

        var result = await controller.BuildLotTraceAsync(new AdminComplianceController.LotTraceRequest("LOT-CSV", "BACKWARD", "CSV"));

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
    }
}
