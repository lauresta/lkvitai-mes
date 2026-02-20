using System.Net;
using System.Text;
using LKvitai.MES.Api.Observability;
using LKvitai.MES.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class AlertEscalationTests
{
    [Fact]
    public void AppSettings_ShouldDefinePagerDutyAndAlertEscalationSections()
    {
        var appsettings = File.ReadAllText(ResolvePathFromRepoRoot("src/Modules/Warehouse/LKvitai.MES.Api/appsettings.json"));

        Assert.Contains("\"PagerDuty\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"ApiKey\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"AlertEscalation\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"DeduplicationWindowMinutes\"", appsettings, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_ShouldDeduplicateCriticalAlertsWithinWindow()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(handler);

        var request = new AlertManagerWebhookRequest
        {
            Status = "firing",
            Alerts =
            [
                new AlertManagerAlert
                {
                    Status = "firing",
                    Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["alertname"] = "HighErrorRate",
                        ["severity"] = "critical"
                    },
                    Annotations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["summary"] = "Error rate > 5%"
                    }
                }
            ]
        };

        var first = await service.ProcessAsync(request, CancellationToken.None);
        var second = await service.ProcessAsync(request, CancellationToken.None);

        Assert.Equal(1, first.Processed);
        Assert.Equal(0, first.Deduplicated);
        Assert.Equal(0, second.Processed);
        Assert.Equal(1, second.Deduplicated);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRouteWarningWithoutPagerDutyCall()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(
            handler,
            alertOptions: new AlertEscalationOptions
            {
                DeduplicationWindowMinutes = 5,
                Routing = new RoutingOptions
                {
                    Critical = "pagerduty",
                    Warning = "email",
                    Info = "slack"
                }
            });

        var request = new AlertManagerWebhookRequest
        {
            Status = "firing",
            Alerts =
            [
                new AlertManagerAlert
                {
                    Status = "firing",
                    Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["alertname"] = "ApiLatency",
                        ["severity"] = "warning"
                    }
                }
            ]
        };

        var result = await service.ProcessAsync(request, CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Deduplicated);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ProcessAsync_ShouldSendResolveEventForResolvedAlert()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(handler);

        var request = new AlertManagerWebhookRequest
        {
            Status = "resolved",
            Alerts =
            [
                new AlertManagerAlert
                {
                    Status = "resolved",
                    Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["alertname"] = "HighErrorRate",
                        ["severity"] = "critical"
                    },
                    EndsAt = DateTimeOffset.UtcNow
                }
            ]
        };

        var result = await service.ProcessAsync(request, CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Deduplicated);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("\"eventAction\":\"resolve\"", handler.RequestBodies.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_ShouldIncludeEscalationPolicyInPayload()
    {
        var handler = new RecordingHttpMessageHandler();
        var service = CreateService(handler);

        var request = new AlertManagerWebhookRequest
        {
            Alerts =
            [
                new AlertManagerAlert
                {
                    Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["alertname"] = "ProjectionLagHigh",
                        ["severity"] = "critical"
                    }
                }
            ]
        };

        await service.ProcessAsync(request, CancellationToken.None);

        var body = handler.RequestBodies.Single();
        Assert.Contains("escalation_l1_minutes", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("escalation_l2_minutes", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("escalation_l3_minutes", body, StringComparison.OrdinalIgnoreCase);
    }

    private static PagerDutyAlertEscalationService CreateService(
        RecordingHttpMessageHandler handler,
        PagerDutyOptions? pagerDutyOptions = null,
        AlertEscalationOptions? alertOptions = null)
    {
        var client = new HttpClient(handler);
        var factory = new StubHttpClientFactory(client);

        return new PagerDutyAlertEscalationService(
            factory,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(pagerDutyOptions ?? new PagerDutyOptions
            {
                ApiKey = "pd-key",
                EventsApiUrl = "https://events.pagerduty.com/v2/enqueue",
                Source = "test"
            }),
            Options.Create(alertOptions ?? new AlertEscalationOptions
            {
                DeduplicationWindowMinutes = 5,
                EscalationPolicy = new EscalationPolicyOptions
                {
                    L1Minutes = 5,
                    L2Minutes = 15,
                    L3Minutes = 30
                },
                Routing = new RoutingOptions
                {
                    Critical = "pagerduty",
                    Warning = "email",
                    Info = "slack"
                },
                OnCallSchedule = new OnCallScheduleOptions
                {
                    Enabled = true,
                    Rotation = "weekly"
                },
                RunbookBaseUrl = "https://runbooks.local/alerts"
            }),
            new NullLogger<PagerDutyAlertEscalationService>());
    }

    private static string ResolvePathFromRepoRoot(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root (.git) from test runtime directory.");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"status\":\"success\"}", Encoding.UTF8, "application/json")
            };
        }
    }
}
