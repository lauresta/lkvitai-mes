namespace LKvitai.MES.BuildingBlocks.WebUI.Services;

/// <summary>
/// Decides which environment-specific chrome the shared shell and modules show.
/// Production never carries the "not production data" strip or scaffold content;
/// Test, Staging and Development keep their warnings.
/// </summary>
public static class EnvironmentChrome
{
    public const string Production = "Production";
    public const string Test = "Test";
    public const string Development = "Development";

    public static bool IsProduction(string? environmentName) =>
        string.Equals(environmentName?.Trim(), Production, StringComparison.OrdinalIgnoreCase);

    /// <summary>The yellow "{env} ENVIRONMENT — not production data" strip is shown everywhere except Production.</summary>
    public static bool ShowEnvironmentStrip(string? environmentName) => !IsProduction(environmentName);

    /// <summary>Scaffold/sample content and infrastructure diagnostics are for Development and Test only.</summary>
    public static bool ShowDiagnostics(string? environmentName)
    {
        var name = environmentName?.Trim();
        return string.Equals(name, Development, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, Test, StringComparison.OrdinalIgnoreCase);
    }
}
