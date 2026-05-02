using LKvitai.MES.Modules.Frontline.Application.Ports;
using LKvitai.MES.Modules.Frontline.Infrastructure.Stub;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Frontline.Api.Composition;

/// <summary>
/// Composition root for the Frontline fabric-availability read-side. Mirrors
/// <c>SalesOrdersDataSource</c> so the two modules share the same operator
/// mental model (<c>"Stub"</c> = explicit dev opt-in, <c>"Sql"</c>/<c>"Auto"</c>
/// = legacy DB).
/// </summary>
/// <remarks>
/// <para>
/// <b>F-1 reality check.</b> Sales' SQL adapter (<c>SqlOrdersQueryService</c>)
/// already exists; for Sales the <c>"Sql"</c> branch is the production code
/// path. Frontline's SQL adapter does not exist yet — it lands in F-2 with
/// <c>SqlFabricQueryService</c> wrapping <c>dbo.weblb_Fabric_GetMobileCard</c>
/// and a new <c>weblb_Fabric_GetLowStockList</c> proc derived from the legacy
/// <c>web_RemainsAll</c> view. Until then the <c>"Sql"</c> / <c>"Auto"</c>
/// modes throw at startup with a "not yet implemented" message so a missing
/// override cannot silently fall back to stub data in test/prod.
/// </para>
/// <para>
/// <b>Selection rules.</b>
/// </para>
/// <list type="bullet">
///   <item><c>"Stub"</c> (current default in F-1) — registers
///   <see cref="StubFabricQueryService"/>; logs an informational line so the
///   stub origin of every payload is obvious in the startup banner.</item>
///   <item><c>"Sql"</c> / <c>"Auto"</c> — reserved for F-2; currently throws
///   <see cref="NotImplementedException"/> at composition time.</item>
/// </list>
/// </remarks>
public static class FrontlineFabricDataSource
{
    private const string DataSourceConfigKey = "Frontline:FabricDataSource";

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

        var requestedMode = configuration[DataSourceConfigKey];
        var mode = ResolveMode(requestedMode);

        if (mode == ModeStub)
        {
            services.AddSingleton<IFabricQueryService>(sp =>
            {
                var logger = sp.GetService<ILoggerFactory>()?
                    .CreateLogger("Frontline.FabricDataSource");
                logger?.LogInformation(
                    "Frontline fabric read-side is using the in-memory STUB " +
                    "(Frontline:FabricDataSource={Mode}). Environment={Environment}. " +
                    "F-2 will replace this with SqlFabricQueryService over the legacy SQL Server.",
                    requestedMode ?? "Stub (default)", environment.EnvironmentName);
                return new StubFabricQueryService();
            });
            return services;
        }

        // TODO(F-2): wire SqlFabricQueryService here once the adapter exists.
        // Until then, the "Sql" / "Auto" branches throw rather than silently
        // fall back to the stub — a misconfigured operator must see a startup
        // failure, not a screen full of fake meters in production.
        throw new NotImplementedException(
            $"Frontline:FabricDataSource='{requestedMode}' is reserved for F-2. " +
            "The SQL adapter (SqlFabricQueryService over weblb_Fabric_GetMobileCard " +
            "+ a new weblb_Fabric_GetLowStockList proc) is not implemented yet. " +
            "Set Frontline:FabricDataSource=Stub explicitly to run the F-1 design build.");
    }

    private static string ResolveMode(string? requestedMode)
    {
        // F-1 default is Stub — Frontline has no SQL adapter yet, so we cannot
        // mirror Sales' Sql-by-default behaviour. F-2 will flip the default
        // back to Sql/Auto once the adapter ships.
        if (string.IsNullOrWhiteSpace(requestedMode)) return ModeStub;
        if (string.Equals(requestedMode, ModeStub, StringComparison.OrdinalIgnoreCase)) return ModeStub;
        if (string.Equals(requestedMode, ModeSql,  StringComparison.OrdinalIgnoreCase)) return ModeSql;
        if (string.Equals(requestedMode, ModeAuto, StringComparison.OrdinalIgnoreCase)) return ModeAuto;

        throw new InvalidOperationException(
            $"Unknown Frontline:FabricDataSource '{requestedMode}'. Expected one of: '{ModeSql}', '{ModeAuto}', '{ModeStub}'.");
    }
}
