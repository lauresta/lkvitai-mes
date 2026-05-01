using System.Net;
using System.Net.Http.Json;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Thin Sales WebUI HTTP wrapper around the named "SalesApi" <see cref="HttpClient"/>
/// registered by <c>AddScaffoldWebUiCore</c>. Returns <c>null</c> on transport or
/// non-success responses so the calling Razor page can render the compact empty/error
/// state without crashing the page.
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
    /// <c>GET /api/sales/orders/{number}</c>. Returns <c>null</c> when the order
    /// does not exist (404) or the API is unreachable.
    /// </summary>
    public async Task<OrderDetailsDto?> GetOrderDetailsAsync(
        string number,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var url = $"/api/sales/orders/{Uri.EscapeDataString(number)}";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Sales API returned {StatusCode} for {Url}",
                    response.StatusCode,
                    url);
                return null;
            }

            return await response.Content
                .ReadFromJsonAsync<OrderDetailsDto>(cancellationToken)
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
