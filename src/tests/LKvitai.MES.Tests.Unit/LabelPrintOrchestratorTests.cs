using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LKvitai.MES.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class LabelPrintOrchestratorTests
{
    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task PrintAsync_WhenPrinterOnline_ShouldReturnPrinted()
    {
        var backgroundJobs = CreateBackgroundJobs();
        var sut = CreateSut(new SuccessfulPrinterClient(), backgroundJobs.Object);

        var result = await sut.PrintAsync("LOCATION", CreateLocationData());

        result.Status.Should().Be("PRINTED");
        result.PdfUrl.Should().BeNull();
        backgroundJobs.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task PrintAsync_WhenPrinterOffline_ShouldQueueJob()
    {
        var backgroundJobs = CreateBackgroundJobs();
        backgroundJobs
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-1");

        var sut = CreateSut(new FailingPrinterClient(), backgroundJobs.Object);

        var result = await sut.PrintAsync("LOCATION", CreateLocationData());

        result.Status.Should().Be("QUEUED");
        backgroundJobs.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessQueuedAsync_WhenRetryAttemptFails_ShouldScheduleNextRetry()
    {
        var backgroundJobs = CreateBackgroundJobs();
        backgroundJobs
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-2");

        var sut = CreateSut(new FailingPrinterClient(), backgroundJobs.Object);
        var payload = new LabelPrintJobPayload(Guid.NewGuid(), "LOCATION", CreateLocationData());

        await sut.ProcessQueuedAsync(payload, 1);

        backgroundJobs.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task ProcessQueuedAsync_WhenFinalAttemptFails_ShouldCreatePdfFallback()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"label-print-tests-{Guid.NewGuid():N}");
        var backgroundJobs = CreateBackgroundJobs();
        var sut = CreateSut(new FailingPrinterClient(), backgroundJobs.Object, outputRoot);
        var payload = new LabelPrintJobPayload(Guid.NewGuid(), "LOCATION", CreateLocationData());

        await sut.ProcessQueuedAsync(payload, 3);

        var pdfDirectory = Path.Combine(outputRoot, "pdf");
        Directory.Exists(pdfDirectory).Should().BeTrue();
        Directory.GetFiles(pdfDirectory, "*.pdf").Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task GeneratePreviewAsync_ShouldReturnPdf()
    {
        var sut = CreateSut(new SuccessfulPrinterClient(), CreateBackgroundJobs().Object);

        var result = await sut.GeneratePreviewAsync("HU", new Dictionary<string, string>
        {
            ["Lpn"] = "HU-001",
            ["Sku"] = "RM-0001",
            ["Quantity"] = "50"
        });

        result.ContentType.Should().Be("application/pdf");
        result.Content.Length.Should().BeGreaterThan(64);
        result.Content[0].Should().Be((byte)'%');
        result.Content[1].Should().Be((byte)'P');
        result.Content[2].Should().Be((byte)'D');
        result.Content[3].Should().Be((byte)'F');
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task GetPdfAsync_WhenMissing_ShouldReturnNull()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"label-print-tests-{Guid.NewGuid():N}");
        var sut = CreateSut(new SuccessfulPrinterClient(), CreateBackgroundJobs().Object, outputRoot);

        var file = await sut.GetPdfAsync("missing.pdf");

        file.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public async Task PrintAsync_WhenLabelTypeInvalid_ShouldThrow()
    {
        var sut = CreateSut(new SuccessfulPrinterClient(), CreateBackgroundJobs().Object);

        var action = async () => await sut.PrintAsync("UNKNOWN", CreateLocationData());

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    private static Mock<IBackgroundJobClient> CreateBackgroundJobs()
    {
        return new Mock<IBackgroundJobClient>(MockBehavior.Loose);
    }

    private static LabelPrintOrchestrator CreateSut(
        ILabelPrinterClient printerClient,
        IBackgroundJobClient backgroundJobs,
        string? outputRootPath = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Labels:OutputRootPath"] = outputRootPath ?? Path.Combine(Path.GetTempPath(), "labels-default"),
                ["Labels:PrinterHost"] = "127.0.0.1",
                ["Labels:PrinterPort"] = "9100",
                ["Labels:Templates:HandlingUnit"] = null
            })
            .Build();

        var templateEngine = new LabelTemplateEngine(configuration);

        return new LabelPrintOrchestrator(
            printerClient,
            backgroundJobs,
            templateEngine,
            configuration,
            new Mock<ILogger<LabelPrintOrchestrator>>().Object);
    }

    private static Dictionary<string, string> CreateLocationData()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LocationCode"] = "R3-C6-L3",
            ["Barcode"] = "LOC-001",
            ["Capacity"] = "1000"
        };
    }

    private sealed class SuccessfulPrinterClient : ILabelPrinterClient
    {
        public Task SendAsync(string zplPayload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPrinterClient : ILabelPrinterClient
    {
        public Task SendAsync(string zplPayload, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Printer unavailable");
        }
    }
}
