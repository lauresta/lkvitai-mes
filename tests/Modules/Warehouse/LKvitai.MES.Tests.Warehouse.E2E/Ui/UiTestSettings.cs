namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

public sealed class UiTestSettings
{
    private UiTestSettings(Uri baseUrl, bool headless, bool pwDebug, int slowMoMs, string artifactsDirectory)
    {
        BaseUrl = baseUrl;
        Headless = headless;
        PwDebug = pwDebug;
        SlowMoMs = slowMoMs;
        ArtifactsDirectory = artifactsDirectory;
    }

    public Uri BaseUrl { get; }

    public bool Headless { get; }

    public bool PwDebug { get; }

    public int SlowMoMs { get; }

    public string ArtifactsDirectory { get; }

    public static UiTestSettings FromEnvironment()
    {
        var rawBaseUrl = Environment.GetEnvironmentVariable("BASE_URL");
        if (!Uri.TryCreate(string.IsNullOrWhiteSpace(rawBaseUrl) ? "http://localhost:5124" : rawBaseUrl, UriKind.Absolute, out var baseUrl))
        {
            baseUrl = new Uri("http://localhost:5124");
        }

        var pwDebug = string.Equals(Environment.GetEnvironmentVariable("PWDEBUG")?.Trim(), "1", StringComparison.Ordinal);
        var headless = pwDebug ? false : ParseBooleanEnvironment("HEADLESS", fallback: true);
        var hasSlowMo = int.TryParse(Environment.GetEnvironmentVariable("SLOWMO_MS")?.Trim(), out var configuredSlowMo) &&
                        configuredSlowMo >= 0;
        var slowMoMs = hasSlowMo ? configuredSlowMo : (pwDebug ? 250 : 0);
        var artifactsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "playwright-artifacts"));
        return new UiTestSettings(baseUrl, headless, pwDebug, slowMoMs, artifactsDirectory);
    }

    private static bool ParseBooleanEnvironment(string key, bool fallback)
    {
        var rawValue = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        var normalized = rawValue.Trim().ToLowerInvariant();
        return normalized switch
        {
            "1" or "true" or "yes" => true,
            "0" or "false" or "no" => false,
            _ => fallback
        };
    }

}
