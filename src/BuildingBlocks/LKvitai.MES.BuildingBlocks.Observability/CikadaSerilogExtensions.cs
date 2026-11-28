using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace LKvitai.MES.BuildingBlocks.Observability;

/// <summary>
/// The one Serilog setup of every Cikada .NET service: compact JSON on the console (one event = one line, the
/// exception inside it), no log files. Docker collects stdout; the log collector parses the JSON.
/// Minimum level comes from <c>Serilog:MinimumLevel:Default</c> (env <c>Serilog__MinimumLevel__Default</c>),
/// <c>Information</c> when unset.
/// </summary>
public static class CikadaSerilogExtensions
{
    /// <summary>
    /// Configures <paramref name="logger"/> for <paramref name="module"/>. Every event carries <c>Module</c>,
    /// <c>Environment</c>, <c>Release</c> (<c>Sentry:Release</c>) and, inside an Activity, <c>TraceId</c>.
    /// <paramref name="moreOverrides"/> adds per-service minimum-level overrides (e.g. Marten).
    /// </summary>
    public static LoggerConfiguration ConfigureCikadaSerilog(
        this LoggerConfiguration logger,
        IConfiguration configuration,
        string module,
        string hostEnvironmentName,
        Action<LoggerConfiguration>? moreOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        var sentry = CikadaSentrySettings.From(configuration, module);

        logger
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            // Kestrel lifecycle ("Now listening on:", "Application started.") stays visible.
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information);
        moreOverrides?.Invoke(logger);

        return logger
            .Enrich.FromLogContext()
            .Enrich.With<ActivityTraceIdEnricher>()
            .Enrich.WithProperty("Module", module)
            .Enrich.WithProperty("Environment", sentry.Environment ?? hostEnvironmentName)
            .Enrich.WithProperty("Release", sentry.Release ?? "unknown")
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .WriteToCikadaSentry(configuration);
    }

    /// <summary>
    /// Request logging with <c>UserName</c> (authentik login, claim <c>preferred_username</c>) and
    /// <c>RequestPath</c>. <c>/health*</c> and <c>/metrics</c> are logged at Debug (they were most of the volume).
    /// </summary>
    public static IApplicationBuilder UseCikadaRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP request completed. StatusCode={StatusCode}, ElapsedMs={Elapsed:0.0000}";
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex is not null || httpContext.Response.StatusCode >= 500)
                {
                    return LogEventLevel.Error;
                }

                if (IsProbePath(httpContext.Request.Path))
                {
                    return LogEventLevel.Debug;
                }

                return elapsed > 5000 ? LogEventLevel.Warning : LogEventLevel.Information;
            };
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.ToString());
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);

                var user = httpContext.User;
                var userName = user.FindFirst("preferred_username")?.Value ?? user.Identity?.Name;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    diagnosticContext.Set("UserName", userName);
                }
            };
        });
    }

    private static bool IsProbePath(PathString path) =>
        path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);

    // Adds the W3C trace id of the current Activity unless something (the Warehouse correlation middleware)
    // already pushed a TraceId into the LogContext.
    private sealed class ActivityTraceIdEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var activity = Activity.Current;
            if (activity is not null && activity.TraceId != default)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
            }
        }
    }
}
