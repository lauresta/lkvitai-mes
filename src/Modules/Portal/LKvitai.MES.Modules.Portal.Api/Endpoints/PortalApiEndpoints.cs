using LKvitai.MES.Modules.Portal.Api.Configuration;
using LKvitai.MES.Modules.Portal.Api.Models;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Portal.Api.Endpoints;

public static class PortalApiEndpoints
{
    /// <summary>
    /// Maps GET /status (build metadata), /modules (configured module cards) and
    /// /news (configured release-shaped news) on the supplied <c>/api/portal/v1</c>
    /// route group. Status stays anonymous so the dashboard can render the
    /// version even before auth completes; modules + news are
    /// <c>RequireAuthorization()</c>-d so module/news content is never exposed
    /// to unauthenticated callers.
    /// </summary>
    public static IEndpointRouteBuilder MapPortalApiV1(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var portal = routes.MapGroup("/api/portal/v1");

        portal.MapGet("/status", BuildStatus).AllowAnonymous();
        portal.MapGet("/modules", GetModules).RequireAuthorization();
        portal.MapGet("/news", GetNews).RequireAuthorization();

        return routes;
    }

    private static PortalStatusResponse BuildStatus(IHostEnvironment env, IConfiguration configuration)
    {
        // Read build metadata from env vars first (CI injects them at container
        // build time via --build-arg → ENV in the Dockerfile), fall back to
        // appsettings (handy for local F5 from Visual Studio / dotnet run).
        var version    = ReadFirst(configuration, "APP_VERSION", "Build:Version");
        var releaseTag = ReadFirst(configuration, "RELEASE_TAG", "Build:ReleaseTag");
        var gitSha     = ReadFirst(configuration, "GIT_SHA",     "Build:GitSha");
        var buildDate  = ReadDate(configuration, "BUILD_DATE",   "Build:BuildDate");

        var environment = env.EnvironmentName ?? "Development";
        var channel = environment.Equals("Production", StringComparison.OrdinalIgnoreCase) ? "prod"
                    : environment.Equals("Test", StringComparison.OrdinalIgnoreCase)        ? "test"
                    : environment.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "dev"
                    : environment.ToLowerInvariant();

        return new PortalStatusResponse(
            Module: "Portal",
            Status: "Online",
            Version: version,
            ReleaseTag: releaseTag,
            GitSha: gitSha,
            BuildDate: buildDate,
            Environment: environment.ToUpperInvariant(),
            Channel: channel);
    }

    private static IReadOnlyList<PortalModuleResponse> GetModules(IOptionsSnapshot<PortalDashboardOptions> options)
    {
        return options.Value.Modules
            .Select(m => new PortalModuleResponse(
                Key: m.Key,
                Title: m.Title,
                Category: m.Category,
                Description: m.Description,
                Status: m.Status,
                Url: m.Url,
                Quarter: m.Quarter,
                RequiredRoles: m.RequiredRoles))
            .ToList();
    }

    private static IReadOnlyList<PortalNewsItemResponse> GetNews(IOptionsSnapshot<PortalDashboardOptions> options)
    {
        // News is config-backed today; the next slice can swap this method for
        // a cached GitHub Releases proxy without changing the wire shape (see
        // the docstring on PortalNewsItemResponse).
        return options.Value.News
            .Select(n => new PortalNewsItemResponse(
                Tag: n.Tag,
                TagColor: n.TagColor,
                TagBackground: n.TagBackground,
                Title: n.Title,
                Excerpt: n.Excerpt,
                Date: n.Date,
                Url: n.Url))
            .ToList();
    }

    private static string? ReadFirst(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }
        return null;
    }

    private static DateTimeOffset? ReadDate(IConfiguration configuration, params string[] keys)
    {
        var raw = ReadFirst(configuration, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }
}
