namespace LKvitai.MES.Modules.Frontline.Infrastructure.Sql;

/// <summary>
/// Connection settings the Frontline SQL Server adapter (F-2) needs to talk to
/// the legacy <c>LKvitaiDb</c> database. Resolved at composition time from
/// <c>ConnectionStrings:LKvitaiDb</c> (and optional <c>Frontline:Sql</c>
/// section) and registered as a singleton — see
/// <c>FrontlineFabricDataSource</c> in <c>Frontline.Api</c>.
/// </summary>
/// <remarks>
/// Intentionally framework-light (no <c>IOptions&lt;T&gt;</c>) so the
/// Infrastructure project does not have to depend on
/// <c>Microsoft.Extensions.Options</c> for two scalar values. Mirrors the
/// shape of <c>SalesSqlOptions</c> on purpose — both modules read the same
/// physical database through the same legacy <c>weblb_*</c>/<c>mes_*</c>
/// stored procedures, so an operator who already understands the Sales
/// settings does not have to relearn anything for Frontline.
/// </remarks>
public sealed class FrontlineSqlOptions
{
    /// <summary>
    /// SQL Server connection string for the legacy <c>LKvitaiDb</c> database.
    /// Must be non-empty when the SQL adapter is in use; the Stub fallback
    /// in <c>Frontline.Api</c> is responsible for handling the missing /
    /// empty case.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Per-command timeout in seconds. The Frontline <c>mes_Fabric_*</c>
    /// procs target single-fabric look-ups and a paged low-stock list, both
    /// historically sub-second; 30 s is comfortable headroom for the legacy
    /// box that hosts the <c>weblb_*</c> family today.
    /// </summary>
    public int CommandTimeoutSeconds { get; init; } = 30;
}
