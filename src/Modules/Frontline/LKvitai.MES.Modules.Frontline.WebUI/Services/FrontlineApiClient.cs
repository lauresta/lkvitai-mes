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
public sealed record FrontlineApiResult<T>(FrontlineApiOutcome Outcome, T? Value)
{
    public static FrontlineApiResult<T> Ok(T value) => new(FrontlineApiOutcome.Ok, value);
    public static FrontlineApiResult<T> NotFound() => new(FrontlineApiOutcome.NotFound, default);
    public static FrontlineApiResult<T> Failed() => new(FrontlineApiOutcome.Failed, default);
}

/// <summary>
/// Thin Frontline WebUI HTTP wrapper around the named "FrontlineApi"
/// <see cref="HttpClient"/> registered by <c>AddScaffoldWebUiCore</c>. The
/// list call returns <c>null</c> on any non-success / transport failure so
/// the toolbar can render its "Frontline API unreachable" state. The lookup
/// call returns a structured <see cref="FrontlineApiResult{T}"/> so the
/// mobile lookup page can distinguish a real 404 ("Fabric not found") from a
/// transport failure.
/// </summary>
/// <remarks>
/// Intentionally mirrors <c>SalesApiClient</c> shape (named client, fetch
/// envelope, perf logging) so the two modules stay easy to compare. Frontline
/// does not yet enforce auth, so there is no per-request <c>DelegatingHandler</c>;
/// once auth lands the named client gets <c>AddHttpMessageHandler</c> in
/// <c>Program.cs</c>, no change required here.
/// </remarks>
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
            return FrontlineApiResult<FabricCardDto>.NotFound();
        }

        var url = width is { } w
            ? $"/api/frontline/fabric/{Uri.EscapeDataString(code)}?width={w}"
            : $"/api/frontline/fabric/{Uri.EscapeDataString(code)}";

        var fetch = await SendJsonGetAsync<FabricCardDto>(url, opLabel: "fabric", cancellationToken)
            .ConfigureAwait(false);

        if (fetch.Status == HttpStatusCode.NotFound)
        {
            _logger.LogInformation(
                "[FrontlinePerf] webui-http GET fabric not-found code={Code} httpMs={HttpMs} totalMs={TotalMs}",
                code, fetch.HttpMs, fetch.TotalMs);
            return FrontlineApiResult<FabricCardDto>.NotFound();
        }

        if (!fetch.IsSuccess) return FrontlineApiResult<FabricCardDto>.Failed();

        _logger.LogInformation(
            "[FrontlinePerf] webui-http GET fabric ok code={Code} widths={Widths} alts={Alternatives} httpMs={HttpMs} deserMs={DeserMs} totalMs={TotalMs} contentLength={ContentLength}",
            code, fetch.Body?.Widths.Count ?? 0, fetch.Body?.Alternatives.Count ?? 0,
            fetch.HttpMs, fetch.DeserMs, fetch.TotalMs, fetch.ContentLength);

        return fetch.Body is null
            ? FrontlineApiResult<FabricCardDto>.Failed()
            : FrontlineApiResult<FabricCardDto>.Ok(fetch.Body);
    }

    /// <summary>
    /// Fetches a page of low-stock rows from
    /// <c>GET /api/frontline/fabric/low-stock</c>. Returns <c>null</c> on
    /// any non-success / transport failure so the page can render a "Low-stock
    /// API unreachable" banner without crashing.
    /// </summary>
    public async Task<PagedResult<FabricLowStockDto>?> GetLowStockAsync(
        FabricLowStockQueryParams query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var url = BuildLowStockUrl(query);
        var fetch = await SendJsonGetAsync<PagedResult<FabricLowStockDto>>(url, opLabel: "low-stock", cancellationToken)
            .ConfigureAwait(false);

        if (!fetch.IsSuccess) return null;

        _logger.LogInformation(
            "[FrontlinePerf] webui-http GET low-stock ok page={Page} pageSize={PageSize} returned={Returned} total={Total} " +
            "httpMs={HttpMs} deserMs={DeserMs} totalMs={TotalMs} contentLength={ContentLength}",
            query.Page, query.PageSize, fetch.Body?.Items.Count ?? 0, fetch.Body?.Total ?? 0,
            fetch.HttpMs, fetch.DeserMs, fetch.TotalMs, fetch.ContentLength);

        return fetch.Body;
    }

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
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    _logger.LogWarning(
                        "[FrontlinePerf] webui-http GET {Op} failed status={StatusCode} url={Url} httpMs={HttpMs} totalMs={TotalMs}",
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
                "[FrontlinePerf] webui-http GET {Op} exception url={Url} totalMs={TotalMs}",
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
