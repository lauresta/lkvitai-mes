namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

public sealed class UiTestSettings
{
    private UiTestSettings(Uri baseUrl, bool headless, string artifactsDirectory)
    {
        BaseUrl = baseUrl;
        Headless = headless;
        ArtifactsDirectory = artifactsDirectory;
    }

    public Uri BaseUrl { get; }

    public bool Headless { get; }

    public string ArtifactsDirectory { get; }

    public static UiTestSettings FromEnvironment()
    {
        var rawBaseUrl = Environment.GetEnvironmentVariable("BASE_URL");
        if (!Uri.TryCreate(string.IsNullOrWhiteSpace(rawBaseUrl) ? "http://localhost:5124" : rawBaseUrl, UriKind.Absolute, out var baseUrl))
        {
            baseUrl = new Uri("http://localhost:5124");
        }

        var headless = ParseBooleanEnvironment("HEADLESS", fallback: true);
        var artifactsDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "playwright-artifacts"));
        return new UiTestSettings(baseUrl, headless, artifactsDirectory);
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
