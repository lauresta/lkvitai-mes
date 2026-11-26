using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace LKvitai.MES.BuildingBlocks.Observability;

/// <summary>
/// Sentry settings of one service, read once from the <c>Sentry</c> configuration section. The same instance feeds
/// the server SDK (<see cref="CikadaSentryExtensions.AddCikadaSentry"/>) and the browser snippet of the WebUIs
/// (BuildingBlocks.WebUI <c>SentryBrowser</c>), so both report the same environment, release and module.
/// <list type="bullet">
///   <item><c>Sentry:Dsn</c> - empty turns Sentry off completely (local dev, tests).</item>
///   <item><c>Sentry:Environment</c> - <c>test</c> / <c>production</c>, set by the compose files.</item>
///   <item><c>Sentry:Release</c> - the deployed image tag (a release tag on prod, a commit SHA on test); falls back to
///     the <c>RELEASE_TAG</c> / <c>APP_VERSION</c> build arguments baked into the WebUI images.</item>
///   <item><c>Sentry:TracesSampleRate</c> - performance tracing, default 0 (off).</item>
///   <item><c>Sentry:Replay:*</c> - browser Session Replay: <c>SessionSampleRate</c> (0.1), <c>OnErrorSampleRate</c>
///     (1.0), <c>MaskAllText</c>, <c>MaskAllInputs</c> and <c>BlockAllMedia</c> (false: an internal staff application,
///     the replay shows the screen as the user saw it; password fields stay masked by the Sentry SDK itself).</item>
/// </list>
/// </summary>
public sealed record CikadaSentrySettings(
    string Module,
    string? Dsn,
    string? Environment,
    string? Release,
    double TracesSampleRate,
    double ReplaySessionSampleRate,
    double ReplayOnErrorSampleRate,
    bool ReplayMaskAllText,
    bool ReplayMaskAllInputs,
    bool ReplayBlockAllMedia)
{
    public const string SectionName = "Sentry";

    public bool Enabled => Dsn is not null;

    /// <summary>True when a DSN is set, i.e. Sentry is on for this process.</summary>
    public static bool IsConfigured(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return TrimToNull(configuration[$"{SectionName}:Dsn"]) is not null;
    }

    public static CikadaSentrySettings From(IConfiguration configuration, string module)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        var section = configuration.GetSection(SectionName);
        return new CikadaSentrySettings(
            module,
            TrimToNull(section["Dsn"]),
            TrimToNull(section["Environment"]),
            TrimToNull(section["Release"]) ?? TrimToNull(configuration["RELEASE_TAG"]) ?? TrimToNull(configuration["APP_VERSION"]),
            Rate(section["TracesSampleRate"], 0),
            Rate(section["Replay:SessionSampleRate"], 0.1),
            Rate(section["Replay:OnErrorSampleRate"], 1),
            Flag(section["Replay:MaskAllText"]),
            Flag(section["Replay:MaskAllInputs"]),
            Flag(section["Replay:BlockAllMedia"]));
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // A malformed rate must not stop the service from starting: fall back to the default and keep it within 0..1.
    private static double Rate(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) && double.IsFinite(rate)
            ? Math.Clamp(rate, 0, 1)
            : fallback;

    // Off unless explicitly "true".
    private static bool Flag(string? value) => bool.TryParse(value, out var flag) && flag;
}
