namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// How the Sales SQL adapter satisfies <c>GetOrdersAsync</c>.
/// </summary>
public enum OrdersQueryMode
{
    /// <summary>
    /// Original behaviour: read <c>dbo.weblb_Orders</c> in full once per TTL,
    /// keep the result set in-process, then filter / sort / page in C# on every
    /// list call. Cheap to roll back to and easy to reason about, but the cache
    /// has bounded staleness (see <see cref="SalesSqlOptions.OrdersListCacheTtlSeconds"/>).
    /// </summary>
    Snapshot,

    /// <summary>
    /// Paged behaviour: each list call invokes <c>dbo.weblb_Orders_Paged</c>
    /// with the page / filter parameters. The DB returns only the requested
    /// page plus a windowed <c>TotalRows</c>, so there is no in-process cache
    /// for the orders list itself. Filter dropdowns and details lookup-by-number
    /// still use the snapshot (small distinct projections / per-id lookup —
    /// not on the page-change hot path).
    /// </summary>
    Paged,
}

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
    /// Per-command timeout in seconds. The legacy <c>weblb_*</c> stored procedures
    /// historically run sub-second, but the orders list materialises the entire
    /// table in-memory so we leave headroom for bigger result sets.
    /// </summary>
    public int CommandTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// In-process snapshot TTL for <c>dbo.weblb_Orders</c>, in seconds. While the
    /// snapshot is fresh, every <c>GetOrders</c> / <c>GetFilterOptions</c>
    /// page-change reuses the same in-memory list and pays only the
    /// filter / sort / page cost (~10 ms), instead of re-running the 600 ms
    /// stored procedure. Set to <c>0</c> to disable the cache and force a
    /// fresh SQL read on every call (useful for diagnostics or once a paged
    /// proc wrapper makes the cache obsolete).
    /// </summary>
    /// <remarks>
    /// Trade-off: a value of 30 s means a freshly inserted order can stay
    /// invisible for up to 30 s. The existing list is already eventually
    /// consistent with the legacy SP (no real-time invalidation), so this
    /// only adds a bounded, configurable delay on top. When
    /// <see cref="QueryMode"/> is <see cref="OrdersQueryMode.Paged"/>,
    /// <c>GetOrdersAsync</c> bypasses the snapshot entirely; the cache is
    /// then only used by filter dropdowns and the details lookup-by-number.
    /// </remarks>
    public int OrdersListCacheTtlSeconds { get; init; } = 30;

    /// <summary>
    /// How <c>GetOrdersAsync</c> resolves a page of orders. Defaults to
    /// <see cref="OrdersQueryMode.Snapshot"/> for backwards compatibility —
    /// flip to <see cref="OrdersQueryMode.Paged"/> via
    /// <c>Sales:Sql:OrdersQueryMode = "Paged"</c> in <c>appsettings</c> once
    /// <c>dbo.weblb_Orders_Paged</c> has been deployed to the target database.
    /// </summary>
    public OrdersQueryMode QueryMode { get; init; } = OrdersQueryMode.Snapshot;
}
