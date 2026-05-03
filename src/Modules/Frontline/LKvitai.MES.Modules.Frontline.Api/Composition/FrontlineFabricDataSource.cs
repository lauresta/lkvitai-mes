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
/// <b>F-2.3 reality.</b> The SQL adapter
/// (<see cref="SqlFabricQueryService"/>) implements both the lookup card
/// (<c>dbo.mes_Fabric_GetMobileCard</c>) and the desktop low-stock list
/// (<c>dbo.mes_Fabric_GetLowStockList</c>); audit-log writes go through
/// <see cref="SqlFabricLookupRecorder"/> over
/// <c>dbo.mes_Fabric_RecordLookup</c>. F-2.4 flips the default mode from
/// the old <c>"Stub"</c> to <c>"Auto"</c>, so any deploy that ships the
/// <c>LKvitaiDb</c> connection string automatically wires the legacy DB
/// without touching configuration.
/// </para>
/// <para>
/// <b>Selection rules.</b>
/// </para>
/// <list type="bullet">
///   <item><c>"Auto"</c> (default) — uses SQL when
///   <c>ConnectionStrings:LKvitaiDb</c> is set; otherwise falls back to
///   the in-memory stub with a startup-warning log line so the stub origin
///   of every payload is obvious. Same shape as
///   <c>SalesOrdersDataSource</c>.</item>
///   <item><c>"Sql"</c> — requires <c>ConnectionStrings:LKvitaiDb</c>;
///   throws at startup if the connection string is missing.</item>
///   <item><c>"Stub"</c> — explicit dev / test opt-in. Registers the in-memory
///   stub and the no-op recorder regardless of whether a connection string
///   is configured, so unit / integration tests can run hermetically.</item>
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

        // Auto = "Sql when we have the connection, Stub otherwise (with a
        // loud warning so the operator sees the fallback in the startup log)."
        // Same shape as SalesOrdersDataSource so the two modules stay in
        // lock-step.
        if (mode == ModeAuto)
        {
            mode = hasConnection ? ModeSql : ModeStub;
        }

        if (mode == ModeStub)
        {
            services.AddSingleton<IFabricQueryService>(sp =>
            {
                var logger = sp.GetService<ILoggerFactory>()?
                    .CreateLogger("Frontline.FabricDataSource");
                if (string.Equals(requestedMode, ModeStub, StringComparison.OrdinalIgnoreCase))
                {
                    logger?.LogInformation(
                        "Frontline fabric read-side is using the in-memory STUB " +
                        "(Frontline:FabricDataSource=Stub, environment={Environment}).",
                        environment.EnvironmentName);
                }
                else
                {
                    // Auto-fallback path. Real deploys MUST set ConnectionStrings:LKvitaiDb;
                    // logging at Warning makes the stub origin impossible to miss.
                    logger?.LogWarning(
                        "Frontline fabric read-side fell back to the in-memory STUB because " +
                        "ConnectionStrings:LKvitaiDb is missing (Frontline:FabricDataSource={Mode}, environment={Environment}). " +
                        "Set the connection string to use the legacy LKvitaiDb data, or set Frontline:FabricDataSource=Stub to silence this warning.",
                        requestedMode ?? "Auto (default)", environment.EnvironmentName);
                }
                return new StubFabricQueryService();
            });
            services.AddSingleton<IFabricLookupRecorder, NoOpFabricLookupRecorder>();
            return services;
        }

        // Sql — explicit (or Auto-resolved) legacy DB binding. Fail fast at
        // composition time so the operator sees a clear startup error
        // instead of a 500 on the first /api/frontline/fabric/{code} request.
        if (!hasConnection)
        {
            throw new InvalidOperationException(
                $"Frontline fabric SQL adapter requires ConnectionStrings:{ConnectionStringName} to be set " +
                $"(environment '{environment.EnvironmentName}', requested mode '{requestedMode ?? "(none)"}'). " +
                "Provide the LKvitaiDb connection string via environment variable " +
                "(ConnectionStrings__LKvitaiDb) or user-secrets, or set Frontline:FabricDataSource=Stub explicitly " +
                "for a stub-only test/dev run.");
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
        // F-2.4 flips the default from Stub → Auto so any deploy that ships
        // the LKvitaiDb connection string automatically uses real data
        // without touching configuration. The Auto branch above degrades
        // to Stub (with a startup warning) when the connection string is
        // missing, so dev machines without the legacy DB still boot.
        if (string.IsNullOrWhiteSpace(requestedMode)) return ModeAuto;
        if (string.Equals(requestedMode, ModeStub, StringComparison.OrdinalIgnoreCase)) return ModeStub;
        if (string.Equals(requestedMode, ModeSql,  StringComparison.OrdinalIgnoreCase)) return ModeSql;
        if (string.Equals(requestedMode, ModeAuto, StringComparison.OrdinalIgnoreCase)) return ModeAuto;

        throw new InvalidOperationException(
            $"Unknown Frontline:FabricDataSource '{requestedMode}'. Expected one of: '{ModeAuto}', '{ModeSql}', '{ModeStub}'.");
    }
}
