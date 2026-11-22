using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.BuildingBlocks.Manuals;

/// <summary>
/// Serves the user manuals (a static MkDocs site in its own container) under <c>/manuals/</c> of the
/// public host, behind the shared portal cookie: the manuals contain screenshots of internal screens,
/// so only a signed-in user may read them. The host streams the files from <c>Manuals:BaseUrl</c>;
/// when that is not configured the endpoint answers 404.
/// </summary>
public static class ManualsProxy
{
    public const string HttpClientName = "Manuals";
    public const string RoutePrefix = "/manuals";
    public const string BaseUrlConfigKey = "Manuals:BaseUrl";

    /// <summary>Registers the named HttpClient that talks to the manuals container.</summary>
    public static IServiceCollection AddManualsProxy(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baseUrl = configuration[BaseUrlConfigKey];
        services.AddHttpClient(HttpClientName, client =>
            {
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)) client.BaseAddress = uri;
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        return services;
    }

    /// <summary>Maps <c>GET /manuals/{**path}</c> for signed-in users (and <c>/manuals</c> to <c>/manuals/</c>).</summary>
    public static IEndpointRouteBuilder MapManualsProxy(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // No RequireAuthorization(): its cookie challenge would redirect to /login.html at the host root
        // (/manuals has no /portal path base). The host sends anonymous users to its own sign-in first;
        // ServeAsync still refuses them.
        endpoints.MapGet(RoutePrefix + "/{**path}", ServeAsync);
        return endpoints;
    }

    /// <summary>
    /// Turns the requested path into a relative path that is safe to append to the manuals base URL.
    /// Returns <c>null</c> for anything that could leave the manuals site: dot segments, backslashes,
    /// percent signs (double-encoded dots), colons, control characters and empty middle segments.
    /// An empty path is the site root.
    /// </summary>
    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        if (path.Any(ch => char.IsControl(ch) || ch is '\\' or '%' or ':')) return null;

        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var last = i == segments.Length - 1;
            if (segments[i] is "." or ".." || (segments[i].Length == 0 && !last)) return null;
        }

        return path;
    }

    /// <summary>True for "a/b" - a directory URL of the MkDocs site written without its trailing slash.</summary>
    public static bool NeedsTrailingSlash(string path)
    {
        if (path.Length == 0 || path.EndsWith('/')) return false;
        var name = path[(path.LastIndexOf('/') + 1)..];
        return !name.Contains('.');
    }

    private static string Escape(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static async Task<IResult> ServeAsync(HttpContext context, IHttpClientFactory factory, ILoggerFactory loggers, string? path)
    {
        if (context.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();

        if (context.Request.Path.Value?.EndsWith('/') == false && string.IsNullOrEmpty(path))
        {
            return Results.Redirect($"{context.Request.PathBase}{RoutePrefix}/{context.Request.QueryString}");
        }

        var normalized = NormalizePath(path);
        if (normalized is null) return Results.NotFound();
        if (NeedsTrailingSlash(normalized))
        {
            return Results.Redirect($"{context.Request.PathBase}{RoutePrefix}/{Escape(normalized)}/{context.Request.QueryString}", permanent: true);
        }

        var client = factory.CreateClient(HttpClientName);
        if (client.BaseAddress is null) return Results.NotFound();

        var logger = loggers.CreateLogger("LKvitai.MES.BuildingBlocks.Manuals");
        try
        {
            var upstream = await client.GetAsync(Escape(normalized), HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            if (!upstream.IsSuccessStatusCode)
            {
                var missing = upstream.StatusCode == System.Net.HttpStatusCode.NotFound;
                upstream.Dispose();
                return missing ? Results.NotFound() : Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            context.Response.Headers.CacheControl = "private, max-age=300";
            context.Response.RegisterForDispose(upstream);
            var contentType = upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var stream = await upstream.Content.ReadAsStreamAsync(context.RequestAborted);
            return Results.Stream(stream, contentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !context.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Manuals container is not reachable for {Path}", normalized);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
