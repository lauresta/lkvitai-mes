using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using LKvitai.MES.Modules.Frontline.Contracts.Common;
using LKvitai.MES.Modules.Frontline.Contracts.Fabric;

namespace LKvitai.MES.Modules.Frontline.WebUI.Services;

/// <summary>
/// Outcome of a single Frontline API call. Lets the calling Razor page tell
/// "the fabric genuinely does not exist" (NotFound) apart from "the API call
/// itself failed" (Failed) so the user sees an accurate message instead of a
/// misleading "Fabric not found" on a 5xx / transport error.
/// </summary>
public enum FrontlineApiOutcome
{
    Ok,
    NotFound,
    Failed,
}

/// <summary>Result wrapper for a single-resource fetch.</summary>
public sealed record FrontlineApiResult<T>(FrontlineApiOutcome Outcome, T? Value);

/// <summary>
/// Thin Frontline WebUI HTTP wrapper around the named "FrontlineApi"
/// <see cref="HttpClient"/> registered by <c>AddScaffoldWebUiCore</c>. Each
/// public method handles its own dispatch + timing + logging inline so the
/// per-endpoint perf logs can carry endpoint-specific structured fields
/// without a generic helper. Frontline does not yet enforce auth, so there
/// is no per-request <c>DelegatingHandler</c>; once F-2.4 lands, the named
/// client picks up <c>AddHttpMessageHandler</c> in <c>Program.cs</c>, no
/// change required here.
/// </summary>
public sealed class FrontlineApiClient
{
    private const string HttpClientName = "FrontlineApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FrontlineApiClient> _logger;

    public FrontlineApiClient(IHttpClientFactory httpClientFactory, ILogger<FrontlineApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Fetches a single fabric mobile-lookup card from
    /// <c>GET /api/frontline/fabric/{code}</c>. Returns a wrapped outcome so
    /// the lookup page can render different copy for "not found" vs "API
    /// error".
    /// </summary>
    public async Task<FrontlineApiResult<FabricCardDto>> GetFabricCardAsync(
        string code,
        int? width,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new FrontlineApiResult<FabricCardDto>(FrontlineApiOutcome.NotFound, null);
        }

        var url = width is { } w
            ? $"/api/frontline/fabric/{Uri.EscapeDataString(code)}?width={w}"
            : $"/api/frontline/fabric/{Uri.EscapeDataString(code)}";

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "[FrontlinePerf] webui-http GET fabric not-found code={Code} elapsedMs={ElapsedMs}",
                    code, sw.ElapsedMilliseconds);
                return new FrontlineApiResult<FabricCardDto>(FrontlineApiOutcome.NotFound, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[FrontlinePerf] webui-http GET fabric failed code={Code} status={StatusCode} elapsedMs={ElapsedMs}",
                    code, response.StatusCode, sw.ElapsedMilliseconds);
                return new FrontlineApiResult<FabricCardDto>(FrontlineApiOutcome.Failed, null);
            }

            var card = await response.Content
                .ReadFromJsonAsync<FabricCardDto>(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "[FrontlinePerf] webui-http GET fabric ok code={Code} widths={Widths} alts={Alternatives} elapsedMs={ElapsedMs}",
                code, card?.Widths.Count ?? 0, card?.Alternatives.Count ?? 0, sw.ElapsedMilliseconds);

            return card is null
                ? new FrontlineApiResult<FabricCardDto>(FrontlineApiOutcome.Failed, null)
                : new FrontlineApiResult<FabricCardDto>(FrontlineApiOutcome.Ok, card);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex,
                "[FrontlinePerf] webui-http GET fabric exception code={Code} elapsedMs={ElapsedMs}",
                code, sw.ElapsedMilliseconds);
            return new FrontlineApiResult<FabricCardDto>(FrontlineApiOutcome.Failed, null);
        }
    }

    /// <summary>
    /// Fetches a page of low-stock rows from
    /// <c>GET /api/frontline/fabric/low-stock</c>. Returns <c>null</c> on
    /// any non-success / transport failure so the page can render a
    /// "Frontline API unreachable" banner without crashing.
    /// </summary>
    public async Task<PagedResult<FabricLowStockDto>?> GetLowStockAsync(
        FabricLowStockQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var url = BuildLowStockUrl(query);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[FrontlinePerf] webui-http GET low-stock failed status={StatusCode} elapsedMs={ElapsedMs}",
                    response.StatusCode, sw.ElapsedMilliseconds);
                return null;
            }

            var page = await response.Content
                .ReadFromJsonAsync<PagedResult<FabricLowStockDto>>(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "[FrontlinePerf] webui-http GET low-stock ok page={Page} pageSize={PageSize} returned={Returned} total={Total} elapsedMs={ElapsedMs}",
                query.Page, query.PageSize, page?.Items.Count ?? 0, page?.Total ?? 0, sw.ElapsedMilliseconds);

            return page;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex,
                "[FrontlinePerf] webui-http GET low-stock exception elapsedMs={ElapsedMs}",
                sw.ElapsedMilliseconds);
            return null;
        }
    }

    private static string BuildLowStockUrl(FabricLowStockQueryParams query)
    {
        var parts = new List<string>(7);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"search={Uri.EscapeDataString(query.Search)}");
        }
        if (query.ThresholdMeters is { } threshold)
        {
            parts.Add($"threshold={threshold}");
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            parts.Add($"status={Uri.EscapeDataString(query.Status)}");
        }
        if (query.WidthMm is { } width)
        {
            parts.Add($"width={width}");
        }
        if (!string.IsNullOrWhiteSpace(query.Supplier))
        {
            parts.Add($"supplier={Uri.EscapeDataString(query.Supplier)}");
        }
        if (query.Page > 1)
        {
            parts.Add($"page={query.Page}");
        }
        if (query.PageSize is > 0 and not 50)
        {
            parts.Add($"pageSize={query.PageSize}");
        }

        return parts.Count == 0
            ? "/api/frontline/fabric/low-stock"
            : "/api/frontline/fabric/low-stock?" + string.Join("&", parts);
    }
}
