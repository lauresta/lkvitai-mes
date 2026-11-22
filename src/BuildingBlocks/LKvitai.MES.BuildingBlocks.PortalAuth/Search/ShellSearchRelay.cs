using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.BuildingBlocks.Search;

/// <summary>A validated shell search request: trimmed query, clamped per-kind limit, WebUI path base.</summary>
public sealed record ShellSearchInput(string Query, int Limit, string? PathBase);

/// <summary>
/// The part of <c>GET {PathBase}/shell/search</c> every module WebUI shares: request checks, the call
/// to the module's own API and the mapping of its answer. The module supplies only who the caller is,
/// its screens and how its API is addressed and authorized.
/// </summary>
public static class ShellSearchRelay
{
    /// <summary>
    /// Applies the common request rules. Returns the response to send when the request must not go on
    /// (403 cross-site, 401 not signed in, an empty list for a query that is too short); otherwise
    /// <c>null</c> with <paramref name="input"/> set.
    /// </summary>
    public static IResult? Reject(HttpContext context, bool authenticated, string? q, int? limit, out ShellSearchInput? input)
    {
        ArgumentNullException.ThrowIfNull(context);

        input = null;
        context.Response.Headers.CacheControl = "no-store";
        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length != 0 && fetchSite != "same-origin") return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!authenticated) return Results.StatusCode(StatusCodes.Status401Unauthorized);

        var query = SearchRequest.NormalizeQuery(q);
        if (query is null) return Results.Ok(Array.Empty<SearchResultDto>());

        input = new ShellSearchInput(query, SearchRequest.ClampLimit(limit), context.Request.PathBase.Value);
        return null;
    }

    /// <summary>
    /// Calls <paramref name="apiPath"/> (<c>?q=&amp;limit=</c> is appended) and answers the shell:
    /// the API's hits rebased under the path base followed by <paramref name="screens"/>; 403 when the
    /// API says the user has no access (no group at all); the screens alone, flagged with
    /// <see cref="SearchRequest.PartialHeader"/>, when the API fails or cannot be reached.
    /// </summary>
    public static async Task<IResult> RelayAsync(
        HttpContext context,
        ShellSearchInput input,
        IReadOnlyList<SearchResultDto> screens,
        HttpClient client,
        string apiPath,
        string? bearer,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(client);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{apiPath}?q={Uri.EscapeDataString(input.Query)}&limit={input.Limit}");
            if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using var response = await client.SendAsync(request, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (response.IsSuccessStatusCode)
            {
                var hits = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(context.RequestAborted) ?? [];
                return Results.Ok(Rebase(hits, input.PathBase).Concat(screens));
            }

            logger.LogWarning("Search relay to {ApiPath} failed with status {StatusCode}", apiPath, (int)response.StatusCode);
        }
        catch (Exception ex) when (IsTransportFailure(ex, context))
        {
            logger.LogWarning(ex, "Search relay to {ApiPath} failed", apiPath);
        }

        context.Response.Headers[SearchRequest.PartialHeader] = "1";
        return Results.Ok(screens);
    }

    /// <summary>
    /// Makes module-relative urls absolute under the WebUI path base and drops hits that are not
    /// well formed (empty kind/title, or a url that is not a plain relative path).
    /// </summary>
    public static IReadOnlyList<SearchResultDto> Rebase(IEnumerable<SearchResultDto?>? hits, string? pathBase)
    {
        var result = new List<SearchResultDto>();
        foreach (var hit in hits ?? [])
        {
            if (hit is null || string.IsNullOrWhiteSpace(hit.Kind) || string.IsNullOrWhiteSpace(hit.Title)) continue;
            var url = SearchRequest.ToAbsolutePath(pathBase, hit.Url);
            if (url is not null) result.Add(hit with { Url = url });
        }

        return result;
    }

    private static bool IsTransportFailure(Exception ex, HttpContext context)
        => ex is HttpRequestException or JsonException
           || (ex is OperationCanceledException && !context.RequestAborted.IsCancellationRequested);
}
