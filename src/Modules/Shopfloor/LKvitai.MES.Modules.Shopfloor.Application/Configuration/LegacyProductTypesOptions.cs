namespace LKvitai.MES.Modules.Shopfloor.Application.Configuration;

/// <summary>
/// Configuration for the legacy product types sync. Bound from the
/// <c>Shopfloor:LegacyProductTypes</c> section.
/// </summary>
public sealed class LegacyProductTypesOptions
{
    public const string SectionName = "Shopfloor:LegacyProductTypes";

    /// <summary>"Sql" (default) reads from LKvitaiDb; "Stub" uses fixtures.</summary>
    public string DataSource { get; set; } = "Sql";

    public int CommandTimeoutSeconds { get; set; } = 30;
}
