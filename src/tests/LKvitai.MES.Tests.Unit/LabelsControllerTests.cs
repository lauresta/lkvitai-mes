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
    public async Task PrintAsync_WhenPrinterUnavailable_ShouldReturnServerErrorWithPdfUrl()
    {
        var orchestrator = new Mock<ILabelPrintOrchestrator>(MockBehavior.Strict);
        orchestrator
            .Setup(x => x.PrintAsync(
                "LOCATION",
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LabelPrintResult(
                "PDF_FALLBACK",
                "/api/warehouse/v1/labels/pdf/test.pdf",
                "Printer offline after 3 retries. Download PDF: /api/warehouse/v1/labels/pdf/test.pdf"));

        var sut = CreateController(orchestrator.Object);

        var result = await sut.PrintAsync(new LabelsController.PrintLabelRequest(
            "LOCATION",
            new Dictionary<string, System.Text.Json.JsonElement>()), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
        var payload = objectResult.Value.Should().BeOfType<LabelsController.PrintLabelResponse>().Subject;
        payload.PdfUrl.Should().Be("/api/warehouse/v1/labels/pdf/test.pdf");
        payload.Message.Should().Contain("Printer offline after 3 retries");
    }

    private static LabelsController CreateController(ILabelPrintOrchestrator orchestrator)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var engine = new LabelTemplateEngine(config);
        return new LabelsController(orchestrator, engine);
    }
}
