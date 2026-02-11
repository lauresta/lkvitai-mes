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
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AgnumExportServicesTests
{
    [Fact]
    [Trait("Category", "AgnumExport")]
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
    public async Task ExecuteAsync_WhenJsonApiFails_ShouldReturnRetrying()
    {
        await using var db = CreateDbContext();
        SeedJsonApiExportData(db);

        var failingClient = new HttpClient(new AlwaysFailHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        var sut = CreateOrchestrator(
            db,
            new StubEventBus(),
            new StubHttpClientFactory(failingClient));

        var result = await sut.ExecuteAsync("MANUAL", retryAttempt: 0);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(AgnumExportStatus.Retrying);
        result.HistoryId.Should().NotBeNull();
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

    private static void SeedCsvExportData(WarehouseDbContext db)
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
            ItemName = "Raw Material 500",
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
}
