using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class LabelsControllerTests
{
    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task PreviewAsync_WhenRequestValid_ShouldReturnPdfFile()
    {
        var orchestrator = new Mock<ILabelPrintOrchestrator>(MockBehavior.Strict);
        orchestrator
            .Setup(x => x.GeneratePreviewAsync(
                "LOCATION",
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LabelPreviewResult([1, 2, 3], "application/pdf", "location-preview.pdf"));

        var sut = CreateController(orchestrator.Object);

        var result = await sut.PreviewAsync(new LabelsController.PreviewLabelRequest(
            "LOCATION",
            new Dictionary<string, System.Text.Json.JsonElement>()), CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("location-preview.pdf");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void GetTemplates_ShouldReturnThreeTemplates()
    {
        var orchestrator = new Mock<ILabelPrintOrchestrator>(MockBehavior.Loose);
        var sut = CreateController(orchestrator.Object);

        var result = sut.GetTemplates();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<IReadOnlyList<LabelsController.LabelTemplateResponse>>().Subject;
        payload.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task PrintAsync_WhenPrinterUnavailable_ShouldReturnServerErrorWithQueueMessage()
    {
        var orchestrator = new Mock<ILabelPrintOrchestrator>(MockBehavior.Strict);
        orchestrator
            .Setup(x => x.PrintAsync(
                "LOCATION",
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LabelPrintResult(
                "QUEUED",
                null,
                "Print failed. Job queued for retry. Queue ID: 70d58e41-9676-4f23-a3b6-c0f37f0f5c88"));

        var sut = CreateController(orchestrator.Object);

        var result = await sut.PrintAsync(new LabelsController.PrintLabelRequest(
            "LOCATION",
            new Dictionary<string, System.Text.Json.JsonElement>()), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
        var payload = objectResult.Value.Should().BeOfType<LabelsController.PrintLabelResponse>().Subject;
        payload.PdfUrl.Should().BeNull();
        payload.Message.Should().Contain("Queue ID");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void GetQueue_ShouldReturnQueueItems()
    {
        var orchestrator = new Mock<ILabelPrintOrchestrator>(MockBehavior.Loose);
        var queue = new Mock<ILabelPrintQueueProcessor>(MockBehavior.Strict);
        queue.Setup(x => x.GetPendingAndFailed()).Returns(
        [
            new PrintQueueItem
            {
                Id = Guid.NewGuid(),
                TemplateType = "LOCATION",
                DataJson = "{}",
                Status = PrintQueueStatus.Pending,
                RetryCount = 1,
                CreatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var sut = CreateController(orchestrator.Object, queue.Object);

        var result = sut.GetQueue();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<IReadOnlyList<LabelsController.PrintQueueItemResponse>>().Subject;
        payload.Should().ContainSingle();
        payload[0].Status.Should().Be("PENDING");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task RetryQueueItemAsync_WhenMissing_ShouldReturnNotFound()
    {
        var orchestrator = new Mock<ILabelPrintOrchestrator>(MockBehavior.Loose);
        var queue = new Mock<ILabelPrintQueueProcessor>(MockBehavior.Strict);
        queue.Setup(x => x.RetryNowAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrintQueueRetryResult(false, null, "Queue item not found."));

        var sut = CreateController(orchestrator.Object, queue.Object);

        var result = await sut.RetryQueueItemAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    private static LabelsController CreateController(ILabelPrintOrchestrator orchestrator)
    {
        return CreateController(orchestrator, new Mock<ILabelPrintQueueProcessor>(MockBehavior.Loose).Object);
    }

    private static LabelsController CreateController(
        ILabelPrintOrchestrator orchestrator,
        ILabelPrintQueueProcessor queueProcessor)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var engine = new LabelTemplateEngine(config);
        return new LabelsController(orchestrator, engine, queueProcessor);
    }
}
