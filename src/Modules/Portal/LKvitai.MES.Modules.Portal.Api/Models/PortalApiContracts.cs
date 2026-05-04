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
    string DisplayVersion,
    string? Version,
    string? ReleaseTag,
    string? GitSha,
    DateTimeOffset? BuildDate,
    string Environment,
    string Channel,
    string? BranchName,
    int? PullRequestNumber);

/// <summary>
/// Module card shape returned by GET /api/portal/v1/modules. Mirrors the
/// pre-#93 hardcoded list in Portal.WebUI/Models/PortalDashboardData.cs but
/// adds <see cref="RequiredRoles"/> as the first hook for permission-aware
/// hiding (the dashboard renders every entry today; gating ships in a later
/// slice).
/// </summary>
public sealed record PortalModuleResponse(
    int? Id,
    string Key,
    string Title,
    string Category,
    string Description,
    string Status,
    string? Url,
    string? Quarter,
    string? IconKey,
    int? SortOrder,
    bool? IsVisible,
    IReadOnlyList<string>? RequiredRoles);

public sealed record PortalTileUpsertRequest(
    string Key,
    string Title,
    string Category,
    string Description,
    string Status,
    string? Url,
    string? Quarter,
    string IconKey,
    int SortOrder,
    bool IsVisible,
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

// ---------------------------------------------------------------------------
// Operations summary  (GET /api/portal/v1/operations-summary?period=this|last)
// ---------------------------------------------------------------------------

/// <summary>
/// Full operations summary response for one calendar month.
/// Returned by <c>GET /api/portal/v1/operations-summary?period=this|last</c>.
/// </summary>
public sealed record PortalOperationsSummaryResponse(
    PortalPeriodInfo Period,
    IReadOnlyList<PortalStageDto> Stages,
    IReadOnlyList<PortalStatusCountDto> Statuses,
    IReadOnlyList<PortalDayCountDto> CreatedByDay,
    IReadOnlyList<PortalDayCountDto> CompletedByDay,
    IReadOnlyList<PortalBranchOnTrackDto> BranchesOnTrack);

/// <summary>Calendar period described by the summary.</summary>
public sealed record PortalPeriodInfo(
    string Key,
    string From,
    string To);

/// <summary>One pipeline stage with its order count for the period.</summary>
public sealed record PortalStageDto(
    string Key,
    string Label,
    int Count);

/// <summary>Raw localized status with its count — for debugging / drill-down.</summary>
public sealed record PortalStatusCountDto(
    string Status,
    int Count);

/// <summary>Per-day order count (ISO date string + integer).</summary>
public sealed record PortalDayCountDto(
    string Date,
    int Count);

/// <summary>
/// Per-branch readiness metric for one calendar month period.
/// <para>
/// <see cref="ReadyBasis"/> is the localized Lithuanian status name used
/// as the readiness threshold: <c>Išsiųstas į filialą</c> for normal branches
/// and <c>Pagamintas</c> for Klaipėda.
/// </para>
/// <para>
/// <see cref="OnTrackPercent"/> is <c>null</c> when <see cref="Ready"/> is zero
/// (no orders have reached the readiness threshold yet in the period).
/// </para>
/// </summary>
public sealed record PortalBranchOnTrackDto(
    string Branch,
    string ReadyBasis,
    int Ready,
    int Issued,
    int? OnTrackPercent);
