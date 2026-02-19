using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AgnumExportServicesTests
{
    [Fact]
    [Trait("Category", "AgnumExport")]
    [Trait("Category", "AgnumCSV")]
    public async Task ExecuteAsync_WhenConfigMissing_ShouldFail()
    {
        await using var db = CreateDbContext();
        var sut = CreateOrchestrator(db, new StubEventBus(), new StubHttpClientFactory(new HttpClient()));

        var result = await sut.ExecuteAsync("MANUAL", retryAttempt: 0);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AgnumExportStatus.Failed);
    }

    [Fact]
    [Trait("Category", "AgnumExport")]
    [Trait("Category", "AgnumCSV")]
    public async Task ExecuteAsync_WhenCsvExport_ShouldPersistHistoryAndFile()
    {
        var exportRoot = Path.Combine(Path.GetTempPath(), $"agnum-export-tests-{Guid.NewGuid():N}");
        await using var db = CreateDbContext();

        SeedCsvExportData(db);

        var sut = CreateOrchestrator(
            db,
            new StubEventBus(),
            new StubHttpClientFactory(new HttpClient()),
            exportRoot);

        var result = await sut.ExecuteAsync("MANUAL", retryAttempt: 0);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(AgnumExportStatus.Success);
        result.RowCount.Should().Be(1);
        result.FilePath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.FilePath!).Should().BeTrue();

        var history = await db.AgnumExportHistories.SingleAsync();
        history.Status.Should().Be(AgnumExportStatus.Success);
        history.RowCount.Should().Be(1);
        history.FilePath.Should().Be(result.FilePath);
    }

    [Fact]
    [Trait("Category", "AgnumExport")]
    [Trait("Category", "AgnumCSV")]
    public async Task ExecuteAsync_WhenJsonApiFails_ShouldReturnRetrying()
    {
        var exportRoot = Path.Combine(Path.GetTempPath(), $"agnum-export-tests-{Guid.NewGuid():N}");
        await using var db = CreateDbContext();
        SeedJsonApiExportData(db);

        var failingClient = new HttpClient(new AlwaysFailHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        var sut = CreateOrchestrator(
            db,
            new StubEventBus(),
            new StubHttpClientFactory(failingClient),
            exportRoot);

        var result = await sut.ExecuteAsync("MANUAL", retryAttempt: 0);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AgnumExportStatus.Retrying);
        result.HistoryId.Should().NotBeNull();
        result.FilePath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.FilePath!).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "AgnumExport")]
    [Trait("Category", "AgnumCSV")]
    public async Task ExecuteAsync_WhenJsonApiSucceeds_ShouldSendGroupedPayloadWithHeaders()
    {
        await using var db = CreateDbContext();
        SeedJsonApiGroupedData(db);

        var handler = new RecordingSuccessHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var sut = CreateOrchestrator(
            db,
            new StubEventBus(),
            new StubHttpClientFactory(client));

        var result = await sut.ExecuteAsync("MANUAL", retryAttempt: 0);

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(AgnumExportStatus.Success);
        handler.Requests.Should().HaveCount(2);

        foreach (var request in handler.Requests)
        {
            request.RequestUri!.AbsoluteUri.EndsWith("/api/v1/inventory/import", StringComparison.Ordinal)
                .Should().BeTrue();

            request.Headers.TryGetValues("X-Export-ID", out var values).Should().BeTrue();
            values!.Single().Should().Be(result.ExportNumber);

            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("token");
        }

        handler.Bodies.Should().Contain(x => x.Contains("\"accountCode\":\"1500-RAW\"", StringComparison.Ordinal));
        handler.Bodies.Should().Contain(x => x.Contains("\"accountCode\":\"1510-FG\"", StringComparison.Ordinal));
        handler.Bodies.Should().OnlyContain(x => x.Contains("\"items\":[", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "AgnumExport")]
    [Trait("Category", "AgnumCSV")]
    public async Task ExecuteAsync_WhenCsvPayloadExceedsLimit_ShouldWriteCompressedCsv()
    {
        var exportRoot = Path.Combine(Path.GetTempPath(), $"agnum-export-tests-{Guid.NewGuid():N}");
        await using var db = CreateDbContext();
        SeedCsvExportData(db, new string('X', 10 * 1024 * 1024));

        var sut = CreateOrchestrator(
            db,
            new StubEventBus(),
            new StubHttpClientFactory(new HttpClient()),
            exportRoot);

        var result = await sut.ExecuteAsync("MANUAL", retryAttempt: 0);

        result.IsSuccess.Should().BeTrue();
        result.FilePath.Should().EndWith(".csv.gz");
        File.Exists(result.FilePath!).Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "AgnumExport")]
    public async Task RecurringJob_WhenRetryingFailure_ShouldScheduleNextAttempt()
    {
        var historyId = Guid.NewGuid();
        var orchestrator = new Mock<IAgnumExportOrchestrator>();
        orchestrator
            .Setup(x => x.ExecuteAsync("SCHEDULED", 0, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgnumExportExecutionResult(
                false,
                historyId,
                "AGX-TEST-001",
                AgnumExportStatus.Retrying,
                0,
                null,
                "Downstream unavailable"));

        var eventBus = new Mock<IEventBus>();
        eventBus
            .Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var backgroundJobs = new Mock<IBackgroundJobClient>();
        backgroundJobs
            .Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-123");

        var sut = new AgnumExportRecurringJob(
            orchestrator.Object,
            backgroundJobs.Object,
            eventBus.Object,
            CreateLogger<AgnumExportRecurringJob>());

        var result = await sut.ExecuteAsync("SCHEDULED", null, 0);

        result.IsSuccess.Should().BeFalse();
        backgroundJobs.Verify(
            x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Once);
    }

    private static AgnumExportOrchestrator CreateOrchestrator(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        IHttpClientFactory httpClientFactory,
        string? exportRoot = null)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Agnum:ExportRootPath"] = exportRoot ?? Path.Combine(Path.GetTempPath(), "agnum-export-default")
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return new AgnumExportOrchestrator(
            dbContext,
            eventBus,
            httpClientFactory,
            new PassThroughSecretProtector(),
            configuration,
            CreateLogger<AgnumExportOrchestrator>());
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"agnum-export-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static ILogger<T> CreateLogger<T>()
    {
        return NullLoggerFactory.Instance.CreateLogger<T>();
    }

    private static void SeedCsvExportData(WarehouseDbContext db, string? itemName = null)
    {
        db.AgnumExportConfigs.Add(new AgnumExportConfig
        {
            Id = Guid.NewGuid(),
            Scope = AgnumExportScope.ByCategory,
            Schedule = "0 23 * * *",
            Format = AgnumExportFormat.Csv,
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            Mappings =
            [
                new AgnumMapping
                {
                    SourceType = "CATEGORY",
                    SourceValue = "Raw Materials",
                    AgnumAccountCode = "1500-RAW"
                }
            ]
        });

        db.OnHandValues.Add(new OnHandValue
        {
            Id = Guid.NewGuid(),
            ItemId = 5001,
            ItemSku = "RM-0500",
            ItemName = itemName ?? "Raw Material 500",
            CategoryName = "Raw Materials",
            Qty = 10m,
            UnitCost = 2.5m,
            TotalValue = 25m,
            LastUpdated = DateTimeOffset.UtcNow
        });

        db.SaveChanges();
    }

    private static void SeedJsonApiExportData(WarehouseDbContext db)
    {
        db.AgnumExportConfigs.Add(new AgnumExportConfig
        {
            Id = Guid.NewGuid(),
            Scope = AgnumExportScope.ByCategory,
            Schedule = "0 23 * * *",
            Format = AgnumExportFormat.JsonApi,
            ApiEndpoint = "http://localhost/agnum-api",
            ApiKey = "token",
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            Mappings =
            [
                new AgnumMapping
                {
                    SourceType = "CATEGORY",
                    SourceValue = "Raw Materials",
                    AgnumAccountCode = "1500-RAW"
                }
            ]
        });

        db.OnHandValues.Add(new OnHandValue
        {
            Id = Guid.NewGuid(),
            ItemId = 5002,
            ItemSku = "RM-0501",
            ItemName = "Raw Material 501",
            CategoryName = "Raw Materials",
            Qty = 15m,
            UnitCost = 3m,
            TotalValue = 45m,
            LastUpdated = DateTimeOffset.UtcNow
        });

        db.SaveChanges();
    }

    private static void SeedJsonApiGroupedData(WarehouseDbContext db)
    {
        db.AgnumExportConfigs.Add(new AgnumExportConfig
        {
            Id = Guid.NewGuid(),
            Scope = AgnumExportScope.ByCategory,
            Schedule = "0 23 * * *",
            Format = AgnumExportFormat.JsonApi,
            ApiEndpoint = "http://localhost/agnum-api/",
            ApiKey = "token",
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            Mappings =
            [
                new AgnumMapping
                {
                    SourceType = "CATEGORY",
                    SourceValue = "Raw Materials",
                    AgnumAccountCode = "1500-RAW"
                },
                new AgnumMapping
                {
                    SourceType = "CATEGORY",
                    SourceValue = "Finished Goods",
                    AgnumAccountCode = "1510-FG"
                }
            ]
        });

        db.OnHandValues.AddRange(
            new OnHandValue
            {
                Id = Guid.NewGuid(),
                ItemId = 5101,
                ItemSku = "RM-1001",
                ItemName = "Raw Material 1001",
                CategoryName = "Raw Materials",
                Qty = 5m,
                UnitCost = 2m,
                TotalValue = 10m,
                LastUpdated = DateTimeOffset.UtcNow
            },
            new OnHandValue
            {
                Id = Guid.NewGuid(),
                ItemId = 5102,
                ItemSku = "FG-2001",
                ItemName = "Finished Good 2001",
                CategoryName = "Finished Goods",
                Qty = 7m,
                UnitCost = 3m,
                TotalValue = 21m,
                LastUpdated = DateTimeOffset.UtcNow
            });

        db.SaveChanges();
    }

    private sealed class StubEventBus : IEventBus
    {
        public Task PublishAsync<T>(T message, CancellationToken ct = default)
            where T : class
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StubHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class PassThroughSecretProtector : IAgnumSecretProtector
    {
        public string Protect(string plainText) => plainText;

        public string? Unprotect(string? cipherText) => cipherText;
    }

    private sealed class AlwaysFailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("service unavailable")
            });
        }
    }

    private sealed class RecordingSuccessHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(CloneRequest(request));
            Bodies.Add(body);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\"}")
            };
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage source)
        {
            var clone = new HttpRequestMessage(source.Method, source.RequestUri);
            foreach (var header in source.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
