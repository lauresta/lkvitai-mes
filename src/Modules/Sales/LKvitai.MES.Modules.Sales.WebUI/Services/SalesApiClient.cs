using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Outcome of a single Sales API call. Lets the calling Razor page tell
/// "the order genuinely does not exist" (NotFound) apart from
/// "the API call itself failed" (Failed) so the user sees an accurate
/// message instead of a misleading "Order not found" on a 401 / 5xx /
/// transport error.
/// </summary>
public enum SalesApiOutcome
{
    Ok,
    NotFound,
    Failed,
}

/// <summary>Result wrapper for a single-resource fetch.</summary>
public sealed record SalesApiResult<T>(SalesApiOutcome Outcome, T? Value)
{
    public static SalesApiResult<T> Ok(T value)        => new(SalesApiOutcome.Ok, value);
    public static SalesApiResult<T> NotFound()         => new(SalesApiOutcome.NotFound, default);
    public static SalesApiResult<T> Failed()           => new(SalesApiOutcome.Failed, default);
}

/// <summary>
/// Thin Sales WebUI HTTP wrapper around the named "SalesApi" <see cref="HttpClient"/>
/// registered by <c>AddScaffoldWebUiCore</c>. The list call returns <c>null</c> on
/// any non-success / transport failure so the toolbar can render its compact
/// "Sales API unreachable" state. The details call returns a structured
/// <see cref="SalesApiResult{T}"/> so the details page can distinguish a real
/// 404 ("Order not found") from an auth/transport failure ("Sales API
/// unreachable").
/// </summary>
public sealed class SalesApiClient
{
    private const string HttpClientName = "SalesApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SalesApiClient> _logger;

    public SalesApiClient(IHttpClientFactory httpClientFactory, ILogger<SalesApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Captures the outcome of one <see cref="HttpClient.GetAsync(string)"/>
    /// + <c>ReadFromJsonAsync</c> round-trip plus the per-phase timings the
    /// <c>[SalesPerf]</c> log lines surface. Designed so callers log the
    /// success line themselves with their own structured fields, while the
    /// generic transport / non-success branches stay in <see cref="SendJsonGetAsync{T}"/>.
    /// </summary>
    private readonly record struct ApiFetch<T>(
        HttpStatusCode Status,
        T? Body,
        long HttpMs,
        long DeserMs,
        long TotalMs,
        long ContentLength,
        bool TransportFailed)
    {
        public bool IsSuccess => !TransportFailed
            && (int)Status >= 200
            && (int)Status < 300;
    }

    /// <summary>
    /// Fetches a page of order summaries from <c>GET /api/sales/orders</c>.
    /// Returns <c>null</c> when the API is unreachable or returns a non-success status.
    /// </summary>
    public async Task<PagedResult<OrderSummaryDto>?> GetOrdersAsync(
        OrdersQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var url = BuildOrdersListUrl(query);
        var fetch = await SendJsonGetAsync<PagedResult<OrderSummaryDto>>(url, opLabel: "orders", cancellationToken)
            .ConfigureAwait(false);

        if (!fetch.IsSuccess) return null;

        _logger.LogInformation(
            "[SalesPerf] webui-http GET orders ok page={Page} pageSize={PageSize} returned={Returned} total={Total} " +
            "httpMs={HttpMs} deserMs={DeserMs} totalMs={TotalMs} contentLength={ContentLength}",
            query.Page, query.PageSize, fetch.Body?.Items.Count ?? 0, fetch.Body?.Total ?? 0,
            fetch.HttpMs, fetch.DeserMs, fetch.TotalMs, fetch.ContentLength);

        return fetch.Body;
    }

    /// <summary>
    /// Fetches the full details payload for a single order from
    /// <c>GET /api/sales/orders/{number}</c>. Returns a wrapped outcome so the
    /// details page can render different copy for "not found" vs "API error".
    /// </summary>
    public async Task<SalesApiResult<OrderDetailsDto>> GetOrderDetailsAsync(
        string number,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return SalesApiResult<OrderDetailsDto>.NotFound();
        }

        var url = $"/api/sales/orders/{Uri.EscapeDataString(number)}";
        var fetch = await SendJsonGetAsync<OrderDetailsDto>(url, opLabel: "order", cancellationToken)
            .ConfigureAwait(false);

        if (fetch.Status == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "[SalesPerf] webui-http GET order not-found number={Number} httpMs={HttpMs} totalMs={TotalMs}",
                number, fetch.HttpMs, fetch.TotalMs);
            return SalesApiResult<OrderDetailsDto>.NotFound();
        }

        if (!fetch.IsSuccess) return SalesApiResult<OrderDetailsDto>.Failed();

        _logger.LogInformation(
            "[SalesPerf] webui-http GET order ok number={Number} found={Found} httpMs={HttpMs} deserMs={DeserMs} totalMs={TotalMs} contentLength={ContentLength}",
            number, fetch.Body is not null, fetch.HttpMs, fetch.DeserMs, fetch.TotalMs, fetch.ContentLength);

        return fetch.Body is null
            ? SalesApiResult<OrderDetailsDto>.Failed()
            : SalesApiResult<OrderDetailsDto>.Ok(fetch.Body);
    }

    /// <summary>
    /// Fetches the toolbar filter options (distinct Statuses + Stores currently
    /// present in the data) from <c>GET /api/sales/orders/filters</c>.
    /// Returns <c>null</c> when the API is unreachable so the toolbar can fall
    /// back to a single "All" option without crashing the page.
    /// </summary>
    public async Task<OrdersFilterOptionsDto?> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        const string url = "/api/sales/orders/filters";
        var fetch = await SendJsonGetAsync<OrdersFilterOptionsDto>(url, opLabel: "filters", cancellationToken)
            .ConfigureAwait(false);

        if (!fetch.IsSuccess) return null;

        _logger.LogInformation(
            "[SalesPerf] webui-http GET filters ok statuses={StatusCount} stores={StoreCount} httpMs={HttpMs} deserMs={DeserMs} totalMs={TotalMs}",
            fetch.Body?.Statuses.Count ?? 0, fetch.Body?.Stores.Count ?? 0,
            fetch.HttpMs, fetch.DeserMs, fetch.TotalMs);

        return fetch.Body;
    }

    /// <summary>
    /// Common transport for every <c>GET /api/sales/...</c> call: dispatch the
    /// request, time the HTTP and JSON-deserialise phases, and log the failure
    /// branches once. Success logs stay with the calling method so each
    /// endpoint emits its own structured line with the fields it cares about.
    /// </summary>
    private async Task<ApiFetch<T>> SendJsonGetAsync<T>(
        string url,
        string opLabel,
        CancellationToken cancellationToken)
        where T : class
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var totalSw = Stopwatch.StartNew();
        var httpSw = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            httpSw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                totalSw.Stop();
                // 404 is a legitimate outcome for the by-number lookup, so
                // skip the warning log there — caller logs its own info line.
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    _logger.LogWarning(
                        "[SalesPerf] webui-http GET {Op} failed status={StatusCode} url={Url} httpMs={HttpMs} totalMs={TotalMs}",
                        opLabel, response.StatusCode, url, httpSw.ElapsedMilliseconds, totalSw.ElapsedMilliseconds);
                }
                return new ApiFetch<T>(
                    Status:           response.StatusCode,
                    Body:             null,
                    HttpMs:           httpSw.ElapsedMilliseconds,
                    DeserMs:          0,
                    TotalMs:          totalSw.ElapsedMilliseconds,
                    ContentLength:    response.Content.Headers.ContentLength ?? -1,
                    TransportFailed:  false);
            }

            var deserSw = Stopwatch.StartNew();
            var body = await response.Content
                .ReadFromJsonAsync<T>(cancellationToken)
                .ConfigureAwait(false);
            deserSw.Stop();
            totalSw.Stop();

            return new ApiFetch<T>(
                Status:           response.StatusCode,
                Body:             body,
                HttpMs:           httpSw.ElapsedMilliseconds,
                DeserMs:          deserSw.ElapsedMilliseconds,
                TotalMs:          totalSw.ElapsedMilliseconds,
                ContentLength:    response.Content.Headers.ContentLength ?? -1,
                TransportFailed:  false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            totalSw.Stop();
            _logger.LogError(ex,
                "[SalesPerf] webui-http GET {Op} exception url={Url} totalMs={TotalMs}",
                opLabel, url, totalSw.ElapsedMilliseconds);
            return new ApiFetch<T>(
                Status:           default,
                Body:             null,
                HttpMs:           httpSw.ElapsedMilliseconds,
                DeserMs:          0,
                TotalMs:          totalSw.ElapsedMilliseconds,
                ContentLength:    -1,
                TransportFailed:  true);
        }
    }

    private static string BuildOrdersListUrl(OrdersQueryParams query)
    {
        var parts = new List<string>(8);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"search={Uri.EscapeDataString(query.Search)}");
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            parts.Add($"status={Uri.EscapeDataString(query.Status)}");
        }
        if (!string.IsNullOrWhiteSpace(query.Store))
        {
            parts.Add($"store={Uri.EscapeDataString(query.Store)}");
        }
        if (!string.IsNullOrWhiteSpace(query.Date))
        {
            parts.Add($"date={Uri.EscapeDataString(query.Date)}");
        }
        if (query.HasDebt)
        {
            parts.Add("hasDebt=true");
        }
        if (query.Page > 1)
        {
            parts.Add($"page={query.Page}");
        }
        if (query.PageSize is > 0 and not 100)
        {
            parts.Add($"pageSize={query.PageSize}");
        }

        return parts.Count == 0
            ? "/api/sales/orders"
            : "/api/sales/orders?" + string.Join("&", parts);
    }
}
