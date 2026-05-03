using LKvitai.MES.Modules.Frontline.Application.Ports;
using LKvitai.MES.Modules.Frontline.Infrastructure.Sql;
using LKvitai.MES.Modules.Frontline.Infrastructure.Stub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Frontline.Api.Composition;

/// <summary>
/// Composition root for the Frontline fabric-availability read-side. Mirrors
/// <c>SalesOrdersDataSource</c> so the two modules share the same operator
/// mental model (<c>"Stub"</c> = explicit dev opt-in, <c>"Sql"</c> /
/// <c>"Auto"</c> = legacy DB).
/// </summary>
/// <remarks>
/// <para>
/// <b>F-2.2 reality check.</b> The SQL adapter
/// (<see cref="SqlFabricQueryService"/>) implements the lookup card via
/// <c>dbo.mes_Fabric_GetMobileCard</c> and pairs with
/// <see cref="SqlFabricLookupRecorder"/> for audit-log writes via
/// <c>dbo.mes_Fabric_RecordLookup</c>. The low-stock list path is reserved
/// for F-2.3 — calling <c>GetLowStockListAsync</c> on the SQL adapter
/// throws <see cref="NotImplementedException"/> with a pointer to that
/// task. The default mode therefore stays <c>"Stub"</c> until F-2.4 flips
/// the switch.
/// </para>
/// <para>
/// <b>Selection rules.</b>
/// </para>
/// <list type="bullet">
///   <item><c>"Stub"</c> (current default) — registers
///   <see cref="StubFabricQueryService"/> +
///   <see cref="NoOpFabricLookupRecorder"/>; logs an informational line so
///   the stub origin of every payload is obvious in the startup banner.</item>
///   <item><c>"Sql"</c> / <c>"Auto"</c> — requires
///   <c>ConnectionStrings:LKvitaiDb</c>; throws at startup if it's missing.
///   Registers <see cref="SqlFabricQueryService"/> +
///   <see cref="SqlFabricLookupRecorder"/>.</item>
/// </list>
/// </remarks>
public static class FrontlineFabricDataSource
{
    private const string DataSourceConfigKey  = "Frontline:FabricDataSource";
    private const string ConnectionStringName = "LKvitaiDb";
    private const string CommandTimeoutKey    = "Frontline:Sql:CommandTimeoutSeconds";

    private const string ModeAuto = "Auto";
    private const string ModeSql  = "Sql";
    private const string ModeStub = "Stub";

    public static IServiceCollection AddFrontlineFabricDataSource(
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
            // Explicit dev / test path. Both ports are registered together
            // so the endpoint can call IFabricLookupRecorder unconditionally
            // — the no-op recorder makes the stub flow indistinguishable
            // from the SQL flow as far as the API surface is concerned.
            services.AddSingleton<IFabricQueryService>(sp =>
            {
                var logger = sp.GetService<ILoggerFactory>()?
                    .CreateLogger("Frontline.FabricDataSource");
                logger?.LogInformation(
                    "Frontline fabric read-side is using the in-memory STUB " +
                    "(Frontline:FabricDataSource={Mode}). Environment={Environment}. " +
                    "F-2.4 will flip the default to Sql once the low-stock adapter ships.",
                    requestedMode ?? "Stub (default)", environment.EnvironmentName);
                return new StubFabricQueryService();
            });
            services.AddSingleton<IFabricLookupRecorder, NoOpFabricLookupRecorder>();
            return services;
        }

        // Sql / Auto — both require the legacy connection string. Fail fast
        // (at composition time, before the host opens any sockets) so the
        // operator sees a clear startup error instead of a 500 on the first
        // /api/frontline/fabric/{code} request.
        if (!hasConnection)
        {
            throw new InvalidOperationException(
                $"Frontline fabric SQL adapter requires ConnectionStrings:{ConnectionStringName} to be set " +
                $"(environment '{environment.EnvironmentName}'). Provide the LKvitaiDb connection string via " +
                "environment variable (ConnectionStrings__LKvitaiDb) or user-secrets, " +
                "or set Frontline:FabricDataSource=Stub explicitly for a stub-only test/dev run.");
        }

        var commandTimeout = configuration.GetValue(CommandTimeoutKey, defaultValue: 30);
        services.AddSingleton(new FrontlineSqlOptions
        {
            ConnectionString      = connectionString!,
            CommandTimeoutSeconds = commandTimeout <= 0 ? 30 : commandTimeout,
        });
        services.AddSingleton<IFabricQueryService, SqlFabricQueryService>();
        services.AddSingleton<IFabricLookupRecorder, SqlFabricLookupRecorder>();

        return services;
    }

    private static string ResolveMode(string? requestedMode)
    {
        // F-2.2 default is still Stub — the SQL low-stock adapter lands in
        // F-2.3 and the data-source flip lands in F-2.4. Until then a
        // plain default has to keep the WebUI usable in dev without a
        // legacy connection string.
        if (string.IsNullOrWhiteSpace(requestedMode)) return ModeStub;
        if (string.Equals(requestedMode, ModeStub, StringComparison.OrdinalIgnoreCase)) return ModeStub;
        if (string.Equals(requestedMode, ModeSql,  StringComparison.OrdinalIgnoreCase)) return ModeSql;
        if (string.Equals(requestedMode, ModeAuto, StringComparison.OrdinalIgnoreCase)) return ModeAuto;

        throw new InvalidOperationException(
            $"Unknown Frontline:FabricDataSource '{requestedMode}'. Expected one of: '{ModeSql}', '{ModeAuto}', '{ModeStub}'.");
    }
}
