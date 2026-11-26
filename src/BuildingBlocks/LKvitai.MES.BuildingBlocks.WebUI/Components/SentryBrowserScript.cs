using System.Text.Json;
using System.Text.Json.Serialization;
using LKvitai.MES.BuildingBlocks.Observability;

namespace LKvitai.MES.BuildingBlocks.WebUI.Components;

/// <summary>
/// The inline script of <see cref="SentryBrowser"/>: starts Sentry in the browser once the CDN bundle has loaded.
/// Every value is JSON-encoded (System.Text.Json also escapes &lt;, &gt;, &amp; and quotes), so neither a configured
/// value nor a username can end the script element or break out of a string literal.
/// </summary>
public static class SentryBrowserScript
{
    /// <summary>The id of the CDN script element the init waits for (it is loaded with <c>defer</c>).</summary>
    public const string BundleElementId = "sentry-browser-sdk";

    private static readonly JsonSerializerOptions SkipNulls = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static string Build(CikadaSentrySettings settings, string? username)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var tracing = settings.TracesSampleRate > 0;
        var options = JsonSerializer.Serialize(new
        {
            dsn = settings.Dsn,
            environment = settings.Environment,
            release = settings.Release,
            replaysSessionSampleRate = settings.ReplaySessionSampleRate,
            replaysOnErrorSampleRate = settings.ReplayOnErrorSampleRate,
            tracesSampleRate = tracing ? settings.TracesSampleRate : (double?)null,
            initialScope = new
            {
                tags = new { module = settings.Module },
                user = username is null ? null : new { username },
            },
        }, SkipNulls);
        var replay = JsonSerializer.Serialize(new
        {
            maskAllText = settings.ReplayMaskAllText,
            maskAllInputs = true,
            blockAllMedia = settings.ReplayBlockAllMedia,
        });

        var lines = new List<string>
        {
            "document.getElementById(" + JsonSerializer.Serialize(BundleElementId) + ").addEventListener('load', function () {",
            "  var options = " + options + ";",
            "  options.integrations = [Sentry.replayIntegration(" + replay + ")];",
        };
        if (tracing)
        {
            // Trace headers on same-origin requests only: for those Sentry matches the pattern against the path.
            lines.Add("  options.integrations.push(Sentry.browserTracingIntegration());");
            lines.Add("  options.tracePropagationTargets = [/^\\//];");
        }
        lines.AddRange(new[]
        {
            "  Sentry.init(options);",
            // A Blazor Server failure happens on the server (reported there); the browser only shows #blazor-error-ui.
            // Report that as a browser event as well, so the replay of the moments before it is kept (on-error replay).
            "  var errorUi = document.getElementById('blazor-error-ui');",
            "  if (errorUi) new MutationObserver(function (changes, observer) {",
            "    if (errorUi.style.display !== 'block') return;",
            "    observer.disconnect();",
            "    Sentry.captureMessage('Blazor error UI shown', 'error');",
            "  }).observe(errorUi, { attributes: true, attributeFilter: ['style'] });",
            "});",
        });
        return string.Join('\n', lines);
    }
}
