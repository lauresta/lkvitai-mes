using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Serilog;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace LKvitai.MES.BuildingBlocks.Observability;

/// <summary>
/// Sentry for every Cikada service: one call in each <c>Program.cs</c>
/// (<c>builder.AddCikadaSentry("finance-webui")</c>) plus <see cref="WriteToCikadaSentry"/> in the Serilog
/// configuration. Without <c>Sentry:Dsn</c> nothing is registered and nothing is sent.
/// </summary>
public static class CikadaSentryExtensions
{
    private static readonly Func<LogEvent, bool> FromRequestLogging =
        Matching.FromSource("Serilog.AspNetCore.RequestLoggingMiddleware");

    /// <summary>
    /// Initialises the Sentry SDK (unhandled request exceptions, Microsoft.Extensions.Logging errors of the services
    /// that do not use Serilog) and registers <see cref="CikadaSentrySettings"/> for the browser snippet. Every event
    /// carries the tag <c>module</c> and the signed-in username.
    /// </summary>
    public static WebApplicationBuilder AddCikadaSentry(this WebApplicationBuilder builder, string module)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = CikadaSentrySettings.From(builder.Configuration, module);
        builder.Services.AddSingleton(settings);
        if (!settings.Enabled)
        {
            return builder;
        }

        builder.UseSentry(options =>
        {
            options.Dsn = settings.Dsn;
            if (settings.Environment is not null)
            {
                options.Environment = settings.Environment;
            }
            if (settings.Release is not null)
            {
                options.Release = settings.Release;
            }

            // Internal staff application: the signed-in username on every event is wanted. The session cookie and
            // bearer tokens that come along with it are not (see WithoutCredentials).
            options.SendDefaultPii = true;
            options.SetBeforeSend(WithoutCredentials);
            options.DefaultTags["module"] = settings.Module;
            options.AddExceptionFilter(new ClientGoneExceptionFilter());
            options.UseSerilog();
            // The Serilog sink would also ship every Information+ line as a Sentry structured log (health checks
            // included). The log files already hold those; Sentry gets errors as events, the rest as breadcrumbs.
            options.SetBeforeSendLog(_ => null);

            var tracesRate = settings.TracesSampleRate;
            options.TracesSampleRate = tracesRate > 0 ? tracesRate : null;
            if (tracesRate > 0)
            {
                options.TracesSampler = context => IsHealthCheck(context.TryGetHttpPath()) ? 0 : tracesRate;
            }
        });
        return builder;
    }

    /// <summary>
    /// Sends Serilog Error+ to Sentry as events and Information+ as breadcrumbs. <c>builder.Host.UseSerilog()</c>
    /// replaces the Microsoft.Extensions.Logging providers, so without this sink everything logged only through
    /// <c>ILogger</c> - Blazor Server circuit exceptions above all - would never reach Sentry. The SDK itself is
    /// initialised by <see cref="AddCikadaSentry"/>; with Sentry off this adds nothing.
    /// </summary>
    public static LoggerConfiguration WriteToCikadaSentry(this LoggerConfiguration logger, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (!CikadaSentrySettings.IsConfigured(configuration))
        {
            return logger;
        }

        // Serilog's request log writes every 5xx response as an Error. The exception behind it is captured on its
        // own (Sentry middleware, or the module's own exception logging), so that line would only double the event.
        return logger.WriteTo.Conditional(
            logEvent => !(logEvent.Level >= LogEventLevel.Error && FromRequestLogging(logEvent)),
            sink => sink.Sentry(minimumEventLevel: LogEventLevel.Error, minimumBreadcrumbLevel: LogEventLevel.Information));
    }

    private static bool IsHealthCheck(string? path) =>
        path is not null &&
        (path.EndsWith("/health", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith("/healthz", StringComparison.OrdinalIgnoreCase));

    private static SentryEvent WithoutCredentials(SentryEvent sentryEvent)
    {
        var headers = sentryEvent.Request.Headers;
        foreach (var name in headers.Keys.Where(IsCredentialHeader).ToList())
        {
            headers.Remove(name);
        }
        sentryEvent.Request.Cookies = null;
        return sentryEvent;
    }

    private static bool IsCredentialHeader(string name) =>
        name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Authorization", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The user closed the tab or aborted the call (cancelled request, Blazor circuit gone during a JS call):
    /// nothing to fix on our side. A timeout - a TaskCanceledException wrapping a TimeoutException - is still reported.
    /// </summary>
    private sealed class ClientGoneExceptionFilter : IExceptionFilter
    {
        public bool Filter(Exception ex) =>
            ex is OperationCanceledException { InnerException: not TimeoutException } or JSDisconnectedException;
    }
}
