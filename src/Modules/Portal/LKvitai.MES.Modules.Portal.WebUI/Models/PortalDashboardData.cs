namespace LKvitai.MES.Modules.Portal.WebUI.Models;

/// <summary>
/// Tile status enum mirroring the legacy <c>script.js</c> contract:
///   * <see cref="Active"/>     — green chip, fully navigable.
///   * <see cref="Scaffolded"/> — amber/grey chip, navigable (the
///     module shell exists end-to-end but has no business logic yet).
///   * <see cref="Planned"/>    — grey, click shows a toast with the
///     target quarter; no <c>Url</c> set.
/// Issue #93 will move the source of truth for this list out of code
/// into a Portal API endpoint; the enum stays as the public contract
/// the dashboard renders against.
/// </summary>
public enum ModuleStatus
{
    Active,
    Scaffolded,
    Planned,
}

public sealed record ModuleCard(
    string Key,
    string Title,
    string Category,
    string Description,
    ModuleStatus Status,
    string? Url = null,
    string? Quarter = null);

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
/// Static seed data for the Portal dashboard. Lives in code as a
/// transitional step from the previous JS-hardcoded arrays (see
/// <c>wwwroot/script.js</c> on the pre-Blazor branch). Issue #93
/// replaces every consumer with API/config-backed data; until then
/// this class is the single editable source of dashboard content.
/// </summary>
public static class PortalDashboardData
{
    public static IReadOnlyList<ModuleCard> Modules { get; } = new[]
    {
        new ModuleCard("warehouse",    "Warehouse",    "Operations",   "Inventory locations, stock movements, reservations, handling units, 3D warehouse layout.", ModuleStatus.Active,     Url: "/warehouse/"),
        new ModuleCard("sales",        "Sales",        "Commercial",   "Customer orders.",                                                                          ModuleStatus.Scaffolded, Url: "/sales/"),
        new ModuleCard("frontline",    "Frontline",    "Field",        "Field availability lookup.",                                                                ModuleStatus.Scaffolded, Url: "/frontline/"),
        new ModuleCard("scanning",     "Scanning",     "Mobile",       "Mobile barcode scan.",                                                                      ModuleStatus.Scaffolded, Url: "/scan/"),
        new ModuleCard("orders",       "Orders",       "Commercial",   "Order lifecycle, product composition, workflow planning.",                                  ModuleStatus.Planned,    Quarter: "Q3 2026"),
        new ModuleCard("shopfloor",    "Shopfloor",    "Operations",   "Workstation tasks, WIP routing, operator kiosk execution.",                                 ModuleStatus.Planned,    Quarter: "Q3 2026"),
        new ModuleCard("quality",      "Quality",      "Operations",   "Inspections, defect tracking, rework and returns.",                                         ModuleStatus.Planned,    Quarter: "Q4 2026"),
        new ModuleCard("delivery",     "Delivery",     "Logistics",    "Route planning, driver tasks, proof of delivery, tracking.",                                ModuleStatus.Planned,    Quarter: "Q4 2026"),
        new ModuleCard("installation", "Installation", "Logistics",    "Installer visits, acceptance acts, customer sign-off.",                                     ModuleStatus.Planned,    Quarter: "Q1 2027"),
        new ModuleCard("reporting",    "Reporting",    "Intelligence", "Dashboards, KPIs, production and warehouse analytics.",                                     ModuleStatus.Planned,    Quarter: "Q1 2027"),
        new ModuleCard("finance",      "Finance",      "Intelligence", "Accounting exports, payments, posting events.",                                             ModuleStatus.Planned,    Quarter: "Q2 2027"),
        new ModuleCard("audit",        "Audit",        "Compliance",   "Immutable event log, traceability, compliance reports.",                                    ModuleStatus.Planned,    Quarter: "Q2 2027"),
    };

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

    public static IReadOnlyList<NewsItem> News { get; } = new[]
    {
        new NewsItem("SHIPPED", "oklch(42% 0.14 155)", "oklch(94% 0.04 155)", "Warehouse 3D layout viewer",  "Rack-level zoom, stock density heatmap, keyboard navigation.",                "Apr 22"),
        new NewsItem("SHIPPED", "oklch(42% 0.14 155)", "oklch(94% 0.04 155)", "Stock movement audit trail",  "Every transfer now includes operator, reason code and timestamp.",            "Apr 18"),
        new NewsItem("IN PROG", "var(--accent-700)",   "var(--accent-50)",    "Orders module - alpha",       "Order lifecycle + product composition. Internal QA, Q3 rollout.",             "ongoing"),
        new NewsItem("PLANNED", "oklch(50% 0.015 240)","var(--n-100)",        "Shopfloor operator kiosk",    "Workstation task list, WIP routing. Design review next week.",                "Q3 2026"),
    };
}
