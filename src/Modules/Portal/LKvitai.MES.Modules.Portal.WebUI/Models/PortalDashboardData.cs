namespace LKvitai.MES.Modules.Portal.WebUI.Models;

/// <summary>
/// Tile status enum mirroring the legacy <c>script.js</c> contract:
///   * <see cref="Live"/>       — production work system.
///   * <see cref="Pilot"/>      — available for trial/pilot use.
///   * <see cref="Developing"/> — in build, not ready for users yet.
///   * <see cref="Planned"/>    — roadmap item.
/// The string contract over the wire is the enum name — see
/// <c>PortalApiClient.ParseStatus</c>. Adding a new value requires updating
/// both ends.
/// </summary>
public enum ModuleStatus
{
    Planned,
    Developing,
    Pilot,
    Live,
}

public sealed record ModuleCard(
    int? Id,
    string Key,
    string Title,
    string Category,
    string Description,
    ModuleStatus Status,
    string? Url = null,
    string? Quarter = null,
    string? IconKey = null,
    int SortOrder = 0,
    bool IsVisible = true,
    IReadOnlyList<string>? RequiredRoles = null);

public sealed record OperationsStage(
    string Key,
    string Label,
    int ThisMonth,
    int LastMonth);

public sealed record BranchOnTrack(string Name, int PercentOnTrack);

public sealed record NewsItem(
    string Tag,
    string TagColor,
    string TagBackground,
    string Title,
    string Excerpt,
    string Date);

/// <summary>
/// Operations summary loaded from GET /api/portal/v1/operations-summary.
/// Mirrors the API wire shape as a WebUI-side view model so Home.razor
/// does not reference Portal.Api types directly.
/// </summary>
public sealed record OperationsSummary(
    OperationsPeriod Period,
    IReadOnlyList<OperationsStageData> Stages,
    IReadOnlyList<OperationsStatusCount> Statuses,
    IReadOnlyList<OperationsDayCount> CreatedByDay,
    IReadOnlyList<OperationsDayCount> CompletedByDay);

public sealed record OperationsPeriod(string Key, string From, string To);

public sealed record OperationsStageData(string Key, string Label, int Count);

public sealed record OperationsStatusCount(string Status, int Count);

public sealed record OperationsDayCount(string Date, int Count);

/// <summary>
/// Static seed data for the Portal dashboard sections that haven't been
/// API-ified yet. After issue #93 (this PR) <c>Modules</c> and <c>News</c>
/// come from the Portal API; the remaining fields are still hardcoded:
///   * <see cref="Stages"/>, <see cref="Daily"/>, <see cref="DailyTotal"/>
///     — Operations preview. Replaced by an Operations Summary API in #94.
///   * <see cref="Branches"/> — branches-on-track strip. Replaced by a real
///     "issued / readyForCustomer" metric in #95.
/// Edit here as a transitional step until those slices ship.
/// </summary>
public static class PortalDashboardData
{
    public static IReadOnlyList<OperationsStage> Stages { get; } = new[]
    {
        new OperationsStage("reg",  "Registered",    248, 221),
        new OperationsStage("acc",  "Accepted",      140, 132),
        new OperationsStage("mfg",  "Manufacturing", 480, 455),
        new OperationsStage("ship", "Shipped",        56,  48),
        new OperationsStage("done", "Completed",     150, 142),
    };

    /// <summary>
    /// 30-day "completed per day" series for the daily-bars sparkline.
    /// Indexes 5 + 6 mod 7 are weekend (matches <c>script.js</c> exactly
    /// so visual cadence is unchanged after the migration).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<int>> Daily { get; } = new Dictionary<string, IReadOnlyList<int>>
    {
        ["this"] = new[] { 5, 6, 7, 6, 8, 2, 1, 6, 7, 8, 7, 9, 3, 2, 7, 9, 8, 7, 6, 3, 2, 8, 9, 7, 8, 6, 2, 1, 8, 9 },
        ["last"] = new[] { 4, 5, 6, 6, 7, 1, 1, 5, 6, 7, 6, 8, 2, 2, 6, 8, 7, 6, 5, 2, 1, 7, 8, 6, 7, 5, 1, 1, 7, 8 },
    };

    public static IReadOnlyDictionary<string, int> DailyTotal { get; } = new Dictionary<string, int>
    {
        ["this"] = 150,
        ["last"] = 142,
    };

    public static IReadOnlyList<BranchOnTrack> Branches { get; } = new[]
    {
        new BranchOnTrack("Vilnius",  94),
        new BranchOnTrack("Kaunas",   89),
        new BranchOnTrack("Klaipeda", 91),
        new BranchOnTrack("Siauliai", 84),
    };
}
