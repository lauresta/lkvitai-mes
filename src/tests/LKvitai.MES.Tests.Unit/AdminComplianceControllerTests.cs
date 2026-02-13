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
        var controller = new AdminComplianceController(service.Object);

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

        var controller = new AdminComplianceController(service.Object);

        var result = await controller.GetExportsAsync();

        result.Should().BeOfType<OkObjectResult>();
    }
}
