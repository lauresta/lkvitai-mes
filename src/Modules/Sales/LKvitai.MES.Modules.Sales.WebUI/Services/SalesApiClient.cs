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
    /// Fetches a page of order summaries from <c>GET /api/sales/orders</c>.
    /// Returns <c>null</c> when the API is unreachable or returns a non-success status.
    /// </summary>
    public async Task<PagedResult<OrderSummaryDto>?> GetOrdersAsync(
        OrdersQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var url = BuildOrdersListUrl(query);

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Sales API returned {StatusCode} for {Url}",
                    response.StatusCode,
                    url);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<PagedResult<OrderSummaryDto>>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Sales API call to {Url} failed", url);
            return null;
        }
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

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var url = $"/api/sales/orders/{Uri.EscapeDataString(number)}";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return SalesApiResult<OrderDetailsDto>.NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Sales API returned {StatusCode} for {Url}",
                    response.StatusCode,
                    url);
                return SalesApiResult<OrderDetailsDto>.Failed();
            }

            var body = await response.Content
                .ReadFromJsonAsync<OrderDetailsDto>(cancellationToken)
                .ConfigureAwait(false);

            return body is null
                ? SalesApiResult<OrderDetailsDto>.Failed()
                : SalesApiResult<OrderDetailsDto>.Ok(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Sales API call to {Url} failed", url);
            return SalesApiResult<OrderDetailsDto>.Failed();
        }
    }

    /// <summary>
    /// Fetches the toolbar filter options (distinct Statuses + Stores currently
    /// present in the data) from <c>GET /api/sales/orders/filters</c>.
    /// Returns <c>null</c> when the API is unreachable so the toolbar can fall
    /// back to a single "All" option without crashing the page.
    /// </summary>
    public async Task<OrdersFilterOptionsDto?> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        const string url = "/api/sales/orders/filters";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Sales API returned {StatusCode} for {Url}",
                    response.StatusCode,
                    url);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<OrdersFilterOptionsDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Sales API call to {Url} failed", url);
            return null;
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
