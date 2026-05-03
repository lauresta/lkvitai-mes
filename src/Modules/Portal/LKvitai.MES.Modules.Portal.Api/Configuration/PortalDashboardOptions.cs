namespace LKvitai.MES.Modules.Portal.Api.Configuration;

/// <summary>
/// Bound from the <c>PortalDashboard</c> section of appsettings.*.json.
/// The defaults checked in to <c>appsettings.json</c> are the same module +
/// news lists the legacy <c>script.js</c> shipped, so a fresh deploy still
/// renders a meaningful Portal home before any per-environment override
/// is added. Environment-specific overrides (e.g. hiding "Quality" in test)
/// can override <c>PortalDashboard:Modules</c> wholesale via
/// <c>appsettings.Test.json</c>.
/// </summary>
public sealed class PortalDashboardOptions
{
    public const string SectionName = "PortalDashboard";

    public List<PortalModuleOption> Modules { get; init; } = new();
    public List<PortalNewsOption> News { get; init; } = new();
}

public sealed class PortalModuleOption
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = "Planned";
    public string? Url { get; init; }
    public string? Quarter { get; init; }
    public List<string>? RequiredRoles { get; init; }
}

public sealed class PortalNewsOption
{
    public string Tag { get; init; } = "PLANNED";
    public string TagColor { get; init; } = "oklch(50% 0.015 240)";
    public string TagBackground { get; init; } = "var(--n-100)";
    public string Title { get; init; } = string.Empty;
    public string Excerpt { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string? Url { get; init; }
}
