using System.Net.Http.Json;
using LKvitai.MES.Modules.Warehouse.Api.Observability;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface IAlertEscalationService
{
    Task<AlertEscalationResult> ProcessAsync(AlertManagerWebhookRequest request, CancellationToken cancellationToken);
}

public sealed class PagerDutyAlertEscalationService : IAlertEscalationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly PagerDutyOptions _pagerDutyOptions;
    private readonly AlertEscalationOptions _alertEscalationOptions;
    private readonly ILogger<PagerDutyAlertEscalationService> _logger;

    public PagerDutyAlertEscalationService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IOptions<PagerDutyOptions> pagerDutyOptions,
        IOptions<AlertEscalationOptions> alertEscalationOptions,
        ILogger<PagerDutyAlertEscalationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _pagerDutyOptions = pagerDutyOptions.Value;
        _alertEscalationOptions = alertEscalationOptions.Value;
        _logger = logger;
    }

    public async Task<AlertEscalationResult> ProcessAsync(AlertManagerWebhookRequest request, CancellationToken cancellationToken)
    {
        var processed = 0;
        var deduplicated = 0;

        foreach (var alert in request.Alerts)
        {
            var severity = (alert.Labels.GetValueOrDefault("severity") ?? "warning").ToLowerInvariant();
            var alertName = alert.Labels.GetValueOrDefault("alertname") ?? "UnknownAlert";
            var route = ResolveRoute(severity);
            var dedupKey = $"{alertName}:{severity}";
            var isResolved = string.Equals(alert.Status, "resolved", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(request.Status, "resolved", StringComparison.OrdinalIgnoreCase) ||
                             (alert.EndsAt.HasValue && alert.EndsAt.Value <= DateTimeOffset.UtcNow);

            if (!isResolved && IsDuplicateWithinWindow(dedupKey, DateTimeOffset.UtcNow))
            {
                deduplicated++;
                continue;
            }

            if (string.Equals(route, "pagerduty", StringComparison.OrdinalIgnoreCase))
            {
                await SendPagerDutyEventAsync(alert, alertName, severity, dedupKey, isResolved, cancellationToken);
            }

            processed++;
            CacheAlertOccurrence(dedupKey, DateTimeOffset.UtcNow, isResolved);
        }

        return new AlertEscalationResult(processed, deduplicated);
    }

    private string ResolveRoute(string severity)
    {
        return severity switch
        {
            "critical" => _alertEscalationOptions.Routing.Critical,
            "warning" => _alertEscalationOptions.Routing.Warning,
            _ => _alertEscalationOptions.Routing.Info
        };
    }

    private bool IsDuplicateWithinWindow(string key, DateTimeOffset now)
    {
        if (!_memoryCache.TryGetValue<AlertOccurrence>(key, out var occurrence))
        {
            return false;
        }
        if (occurrence is null)
        {
            return false;
        }

        var window = TimeSpan.FromMinutes(Math.Max(1, _alertEscalationOptions.DeduplicationWindowMinutes));
        return !occurrence.Resolved && now - occurrence.LastSeenAt < window;
    }

    private void CacheAlertOccurrence(string key, DateTimeOffset now, bool resolved)
    {
        var window = TimeSpan.FromMinutes(Math.Max(1, _alertEscalationOptions.DeduplicationWindowMinutes));
        _memoryCache.Set(key, new AlertOccurrence(now, resolved), window);
    }

    private async Task SendPagerDutyEventAsync(
        AlertManagerAlert alert,
        string alertName,
        string severity,
        string dedupKey,
        bool isResolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_pagerDutyOptions.ApiKey))
        {
            _logger.LogWarning("PagerDuty API key is not configured. Skipping alert {AlertName}.", alertName);
            return;
        }

        var client = _httpClientFactory.CreateClient("PagerDuty");
        var eventAction = isResolved ? "resolve" : "trigger";

        var payload = new PagerDutyEventRequest
        {
            RoutingKey = _pagerDutyOptions.ApiKey,
            EventAction = eventAction,
            DedupKey = dedupKey,
            Payload = new PagerDutyPayload
            {
                Summary = alert.Annotations.GetValueOrDefault("summary") ?? alertName,
                Severity = severity,
                Source = _pagerDutyOptions.Source,
                Timestamp = DateTimeOffset.UtcNow,
                CustomDetails = new Dictionary<string, string>
                {
                    ["route"] = ResolveRoute(severity),
                    ["escalation_l1_minutes"] = _alertEscalationOptions.EscalationPolicy.L1Minutes.ToString(),
                    ["escalation_l2_minutes"] = _alertEscalationOptions.EscalationPolicy.L2Minutes.ToString(),
                    ["escalation_l3_minutes"] = _alertEscalationOptions.EscalationPolicy.L3Minutes.ToString(),
                    ["on_call_rotation"] = _alertEscalationOptions.OnCallSchedule.Rotation,
                    ["runbook_url"] = string.IsNullOrWhiteSpace(_alertEscalationOptions.RunbookBaseUrl)
                        ? ""
                        : $"{_alertEscalationOptions.RunbookBaseUrl.TrimEnd('/')}/{alertName}"
                }
            }
        };

        using var response = await client.PostAsJsonAsync(
            _pagerDutyOptions.EventsApiUrl,
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "PagerDuty event request failed for {AlertName} ({StatusCode}): {Body}",
                alertName,
                (int)response.StatusCode,
                body);
            return;
        }

        _logger.LogInformation("PagerDuty event {Action} sent for alert {AlertName}", eventAction, alertName);
    }

    private sealed record AlertOccurrence(DateTimeOffset LastSeenAt, bool Resolved);
}

public sealed record AlertEscalationResult(int Processed, int Deduplicated);

public sealed class AlertManagerWebhookRequest
{
    public string Status { get; init; } = "firing";

    public List<AlertManagerAlert> Alerts { get; init; } = [];
}

public sealed class AlertManagerAlert
{
    public string Status { get; init; } = "firing";

    public Dictionary<string, string> Labels { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Annotations { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }
}

internal sealed class PagerDutyEventRequest
{
    public string RoutingKey { get; init; } = string.Empty;

    public string EventAction { get; init; } = "trigger";

    public string DedupKey { get; init; } = string.Empty;

    public PagerDutyPayload Payload { get; init; } = new();
}

internal sealed class PagerDutyPayload
{
    public string Summary { get; init; } = string.Empty;

    public string Severity { get; init; } = "warning";

    public string Source { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public Dictionary<string, string> CustomDetails { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
