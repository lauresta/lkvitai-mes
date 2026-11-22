using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LKvitai.MES.BuildingBlocks.Search;

/// <summary>
/// <c>GET {PathBase}/shell/search</c> for a module that contributes only its screens to the portal
/// "Search everything" box (issue #375). The caller must be signed in with the shared Portal cookie;
/// the answer is the module's screens matching the query, as kind <see cref="SearchKinds.Screen"/>.
/// </summary>
public static class ShellSearchEndpoints
{
    public const string Path = "/shell/search";

    public static IEndpointConventionBuilder MapShellScreenSearch(
        this IEndpointRouteBuilder app, Func<HttpContext, Task<IReadOnlyList<ScreenEntry>>> screens)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(screens);

        return app.MapGet(Path, async (HttpContext context, string? q, int? limit) =>
        {
            var authenticated = context.User.Identity?.IsAuthenticated == true;
            if (ShellSearchRelay.Reject(context, authenticated, q, limit, out var input) is { } rejected) return rejected;

            return Results.Ok(ScreenSearch.Match(await screens(context), input!.Query, input.Limit, input.PathBase));
        });
    }

    public static IEndpointConventionBuilder MapShellScreenSearch(
        this IEndpointRouteBuilder app, Func<HttpContext, IReadOnlyList<ScreenEntry>> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);
        return app.MapShellScreenSearch(context => Task.FromResult(screens(context)));
    }
}
