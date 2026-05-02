namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// Connection settings the Sales SQL Server adapter (S-2) needs to talk to the
/// legacy <c>LKvitaiDb</c> database. Resolved at composition time from
/// <c>ConnectionStrings:LKvitaiDb</c> (and optional <c>Sales:Sql</c> section)
/// and registered as a singleton — see
/// <c>SalesOrdersDataSourceServiceCollectionExtensions</c> in <c>Sales.Api</c>.
/// </summary>
/// <remarks>
/// Kept intentionally framework-light (no <c>IOptions&lt;T&gt;</c>) so the
/// Infrastructure project does not have to take a hard dependency on
/// <c>Microsoft.Extensions.Options</c> just to ferry two scalar values.
/// </remarks>
public sealed class SalesSqlOptions
{
    /// <summary>
    /// SQL Server connection string for the legacy <c>LKvitaiDb</c> database.
    /// Must be non-empty when the SQL adapter is in use; the dev/stub fallback
    /// in <c>Sales.Api</c> is responsible for handling the missing / empty case.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Per-command timeout in seconds. The Sales <c>weblb_*</c> stored
    /// procedures historically run sub-second; the paged orders SP scales
    /// with page size rather than table size, so 30 s is comfortable headroom.
    /// </summary>
    public int CommandTimeoutSeconds { get; init; } = 30;
}
