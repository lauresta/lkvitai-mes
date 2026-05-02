using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using LKvitai.MES.Modules.Sales.Infrastructure.Stub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Sales.Api.Composition;

/// <summary>
/// Composition root for the Sales orders read-side. Always picks the real
/// <see cref="SqlOrdersQueryService"/> (S-2 SQL Server adapter over the legacy
/// <c>weblb_*</c> stored procedures); the in-memory
/// <see cref="StubOrdersQueryService"/> is only selectable through an
/// explicit <c>Sales:OrdersDataSource = "Stub"</c> opt-in and is intended
/// for tests / dev environments that genuinely cannot reach the legacy DB.
/// </summary>
/// <remarks>
/// <para>
/// <b>Selection rules (post-S-2).</b>
/// </para>
/// <list type="bullet">
///   <item><c>"Sql"</c> (default when key is missing or set to <c>Auto</c>) —
///   require <c>ConnectionStrings:LKvitaiDb</c>; throw at startup if it is
///   missing, regardless of environment. There is no automatic stub fallback
///   any more.</item>
///   <item><c>"Stub"</c> — explicit opt-in only; logs a loud warning at startup
///   to make it obvious the running process is serving sample data.</item>
/// </list>
/// <para>
/// <b>Why no auto-fallback.</b> Pre-S-2 the WebUI silently surfaced seven
/// hand-coded sample rows whenever the connection string was missing. That made
/// missing config invisible in test/staging and risked shipping fake orders to
/// real users. After S-2 the Sales surface either talks to the legacy SQL
/// Server or refuses to start.
/// </para>
/// </remarks>
public static class SalesOrdersDataSource
{
    private const string DataSourceConfigKey   = "Sales:OrdersDataSource";
    private const string ConnectionStringName  = "LKvitaiDb";
    private const string CommandTimeoutKey     = "Sales:Sql:CommandTimeoutSeconds";

    private const string ModeAuto = "Auto";
    private const string ModeSql  = "Sql";
    private const string ModeStub = "Stub";

    public static IServiceCollection AddSalesOrdersDataSource(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var requestedMode    = configuration[DataSourceConfigKey];
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        var hasConnection    = !string.IsNullOrWhiteSpace(connectionString);

        var mode = ResolveMode(requestedMode);

        if (mode == ModeStub)
        {
            // Explicit opt-in only — never the default code path. Used by unit
            // / WebUI smoke tests that exercise the toolbar without spinning
            // up a SQL Server. Logs a loud warning so a misconfigured env is
            // visible in the startup banner.
            services.AddSingleton<IOrdersQueryService>(sp =>
            {
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Sales.OrdersDataSource");
                logger?.LogWarning(
                    "Sales orders read-side is using the in-memory STUB (explicit Sales:OrdersDataSource=Stub). " +
                    "Environment={Environment}. This must never be the case in Test or Production.",
                    environment.EnvironmentName);
                return new StubOrdersQueryService();
            });
            return services;
        }

        // Sql / Auto — both require the legacy connection string. Fail fast
        // (at composition time, before the host opens any sockets) so the
        // operator sees a clear startup error instead of a 500 on the first
        // /api/sales/orders request.
        if (!hasConnection)
        {
            throw new InvalidOperationException(
                $"Sales orders SQL adapter requires ConnectionStrings:{ConnectionStringName} to be set " +
                $"(environment '{environment.EnvironmentName}'). Provide the LKvitaiDb connection string via " +
                "environment variable (ConnectionStrings__LKvitaiDb) or user-secrets, " +
                "or set Sales:OrdersDataSource=Stub explicitly for a stub-only test/dev run.");
        }

        var commandTimeout = configuration.GetValue(CommandTimeoutKey, defaultValue: 30);
        services.AddSingleton(new SalesSqlOptions
        {
            ConnectionString      = connectionString!,
            CommandTimeoutSeconds = commandTimeout <= 0 ? 30 : commandTimeout,
        });
        services.AddSingleton<IOrdersQueryService, SqlOrdersQueryService>();

        return services;
    }

    /// <summary>
    /// Treats missing / empty / "Auto" as the default <c>Sql</c> path. The
    /// only other accepted value is <c>"Stub"</c> (case-insensitive); anything
    /// else is rejected so a typo in <c>appsettings</c> cannot silently fall
    /// through to a stubbed runtime.
    /// </summary>
    private static string ResolveMode(string? requestedMode)
    {
        if (string.IsNullOrWhiteSpace(requestedMode)) return ModeSql;
        if (string.Equals(requestedMode, ModeSql,  StringComparison.OrdinalIgnoreCase)) return ModeSql;
        if (string.Equals(requestedMode, ModeAuto, StringComparison.OrdinalIgnoreCase)) return ModeSql;
        if (string.Equals(requestedMode, ModeStub, StringComparison.OrdinalIgnoreCase)) return ModeStub;

        throw new InvalidOperationException(
            $"Unknown Sales:OrdersDataSource '{requestedMode}'. Expected one of: '{ModeSql}', '{ModeAuto}', '{ModeStub}'.");
    }
}
