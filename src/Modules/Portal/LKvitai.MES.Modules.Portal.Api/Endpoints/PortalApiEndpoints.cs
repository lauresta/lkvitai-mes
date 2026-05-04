using LKvitai.MES.Modules.Portal.Api.Configuration;
using LKvitai.MES.Modules.Portal.Api.Models;
using LKvitai.MES.Modules.Portal.Api.Persistence;
using LKvitai.MES.Modules.Portal.Api;
using LKvitai.MES.Modules.Portal.Api.Services;
using Microsoft.EntityFrameworkCore;
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
        portal.MapGet("/operations-summary", GetOperationsSummary).RequireAuthorization();

        var admin = portal.MapGroup("/admin")
            .RequireAuthorization(PortalPolicies.AdminOnly);
        admin.MapGet("/tiles", GetAdminTiles);
        admin.MapPost("/tiles", CreateTile);
        admin.MapPut("/tiles/{id:int}", UpdateTile);
        admin.MapDelete("/tiles/{id:int}", HideTile);

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
        var branchName = ReadFirst(configuration, "BRANCH_NAME",  "Build:BranchName");
        var prNumber   = ReadInt(configuration, "PR_NUMBER",     "Build:PullRequestNumber");

        var environment = env.EnvironmentName ?? "Development";
        var channel = environment.Equals("Production", StringComparison.OrdinalIgnoreCase) ? "prod"
                    : environment.Equals("Test", StringComparison.OrdinalIgnoreCase)        ? "test"
                    : environment.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "dev"
                    : environment.ToLowerInvariant();
        var shortSha = string.IsNullOrWhiteSpace(gitSha)
            ? null
            : gitSha.Length <= 7 ? gitSha : gitSha[..7];
        var displayVersion = channel switch
        {
            "prod" when !string.IsNullOrWhiteSpace(releaseTag) => releaseTag,
            "test" when prNumber is not null && !string.IsNullOrWhiteSpace(shortSha) => $"PR-{prNumber} · {shortSha}",
            "test" when !string.IsNullOrWhiteSpace(branchName) && !string.IsNullOrWhiteSpace(shortSha) => $"{branchName} · {shortSha}",
            "dev" => "dev",
            _ when !string.IsNullOrWhiteSpace(version) => version,
            _ when !string.IsNullOrWhiteSpace(shortSha) => shortSha,
            _ => "—"
        };

        return new PortalStatusResponse(
            Module: "Portal",
            Status: "Online",
            DisplayVersion: displayVersion,
            Version: version,
            ReleaseTag: releaseTag,
            GitSha: gitSha,
            BuildDate: buildDate,
            Environment: environment.ToUpperInvariant(),
            Channel: channel,
            BranchName: branchName,
            PullRequestNumber: prNumber);
    }

    private static async Task<IReadOnlyList<PortalModuleResponse>> GetModules(
        PortalDbContext db,
        CancellationToken cancellationToken)
    {
        var tiles = await db.Tiles
            .AsNoTracking()
            .Where(t => t.IsVisible)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .ToListAsync(cancellationToken);

        return tiles.Select(ToResponse).ToList();
    }

    private static Task<IReadOnlyList<PortalNewsItemResponse>> GetNews(
        GitHubReleaseNewsService news,
        CancellationToken cancellationToken)
    {
        return news.GetAsync(cancellationToken);
    }

    private static async Task<IResult> GetOperationsSummary(
        [Microsoft.AspNetCore.Mvc.FromQuery] string? period,
        SqlOperationsSummaryService summaryService,
        CancellationToken cancellationToken)
    {
        var result = await summaryService.GetAsync(period ?? "this", cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? Results.StatusCode(503)
            : Results.Ok(result);
    }

    private static async Task<IReadOnlyList<PortalModuleResponse>> GetAdminTiles(
        PortalDbContext db,
        CancellationToken cancellationToken)
    {
        var tiles = await db.Tiles
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Title)
            .ToListAsync(cancellationToken);

        return tiles.Select(ToResponse).ToList();
    }

    private static async Task<IResult> CreateTile(
        PortalTileUpsertRequest request,
        PortalDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;

        var normalizedKey = request.Key.Trim().ToLowerInvariant();
        if (await db.Tiles.AnyAsync(t => t.Key == normalizedKey, cancellationToken))
        {
            return Results.Conflict(new { error = "tile_key_exists" });
        }

        var now = DateTimeOffset.UtcNow;
        var tile = new PortalTile { CreatedAt = now, UpdatedAt = now };
        Apply(tile, request, normalizedKey);
        db.Tiles.Add(tile);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/portal/v1/admin/tiles/{tile.Id}", ToResponse(tile));
    }

    private static async Task<IResult> UpdateTile(
        int id,
        PortalTileUpsertRequest request,
        PortalDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;

        var tile = await db.Tiles.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tile is null) return Results.NotFound();

        var normalizedKey = request.Key.Trim().ToLowerInvariant();
        if (await db.Tiles.AnyAsync(t => t.Id != id && t.Key == normalizedKey, cancellationToken))
        {
            return Results.Conflict(new { error = "tile_key_exists" });
        }

        Apply(tile, request, normalizedKey);
        tile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(tile));
    }

    private static async Task<IResult> HideTile(
        int id,
        PortalDbContext db,
        CancellationToken cancellationToken)
    {
        var tile = await db.Tiles.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tile is null) return Results.NotFound();

        tile.IsVisible = false;
        tile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static PortalModuleResponse ToResponse(PortalTile t) =>
        new(
            Id: t.Id,
            Key: t.Key,
            Title: t.Title,
            Category: t.Category,
            Description: t.Description,
            Status: t.Status,
            Url: t.Url,
            Quarter: t.Quarter,
            IconKey: t.IconKey,
            SortOrder: t.SortOrder,
            IsVisible: t.IsVisible,
            RequiredRoles: t.RequiredRoles);

    private static void Apply(PortalTile tile, PortalTileUpsertRequest request, string normalizedKey)
    {
        tile.Key = normalizedKey;
        tile.Title = request.Title.Trim();
        tile.Category = request.Category.Trim();
        tile.Description = request.Description.Trim();
        tile.Status = request.Status.Trim();
        tile.Url = TrimToNull(request.Url);
        tile.Quarter = TrimToNull(request.Quarter);
        tile.IconKey = request.IconKey.Trim();
        tile.SortOrder = request.SortOrder;
        tile.IsVisible = request.IsVisible;
        tile.RequiredRoles = request.RequiredRoles?
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private static IResult? Validate(PortalTileUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key) ||
            string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Category) ||
            string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.IconKey))
        {
            return Results.BadRequest(new { error = "required_fields_missing" });
        }

        var status = request.Status.Trim();
        if (status is not ("Planned" or "Developing" or "Pilot" or "Live"))
        {
            return Results.BadRequest(new { error = "invalid_status" });
        }

        return null;
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

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset? ReadDate(IConfiguration configuration, params string[] keys)
    {
        var raw = ReadFirst(configuration, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static int? ReadInt(IConfiguration configuration, params string[] keys)
    {
        var raw = ReadFirst(configuration, keys);
        return int.TryParse(raw, out var parsed) ? parsed : null;
    }
}
