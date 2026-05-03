namespace LKvitai.MES.Modules.Portal.Api.Models;

/// <summary>
/// Build / runtime metadata returned by GET /api/portal/v1/status.
/// All fields are optional except <see cref="Module"/> + <see cref="Status"/>:
/// the build pipeline injects <see cref="Version"/>, <see cref="ReleaseTag"/>,
/// <see cref="GitSha"/>, <see cref="BuildDate"/> as env variables when known,
/// and the Portal API surfaces them as-is so the dashboard can stop hardcoding
/// the version string.
/// </summary>
public sealed record PortalStatusResponse(
    string Module,
    string Status,
    string? Version,
    string? ReleaseTag,
    string? GitSha,
    DateTimeOffset? BuildDate,
    string Environment,
    string Channel);

/// <summary>
/// Module card shape returned by GET /api/portal/v1/modules. Mirrors the
/// pre-#93 hardcoded list in Portal.WebUI/Models/PortalDashboardData.cs but
/// adds <see cref="RequiredRoles"/> as the first hook for permission-aware
/// hiding (the dashboard renders every entry today; gating ships in a later
/// slice).
/// </summary>
public sealed record PortalModuleResponse(
    string Key,
    string Title,
    string Category,
    string Description,
    string Status,
    string? Url,
    string? Quarter,
    IReadOnlyList<string>? RequiredRoles);

/// <summary>
/// News item shape returned by GET /api/portal/v1/news. Intentionally close
/// to a GitHub Releases payload (title, tag, date, body, url) so the next
/// slice can swap the static config-backed source for a cached GitHub API
/// proxy without changing the wire shape. <see cref="TagColor"/> +
/// <see cref="TagBackground"/> are presentation hints used by the dashboard's
/// status chip — they belong to the publisher, not the consumer, so they stay
/// in the API response instead of being recomputed in every WebUI consumer.
/// </summary>
public sealed record PortalNewsItemResponse(
    string Tag,
    string TagColor,
    string TagBackground,
    string Title,
    string Excerpt,
    string Date,
    string? Url);
