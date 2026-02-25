using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

public abstract class PlaywrightUiTestBase
{
    private static readonly Regex InvalidFileNameChars = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);

    protected PlaywrightUiTestBase(PlaywrightFixture fixture)
    {
        Fixture = fixture;
    }

    protected PlaywrightFixture Fixture { get; }

    protected static ILocator ByTestId(IPage page, string testId) => page.GetByTestId(testId);

    internal static BrowserTypeLaunchOptions BuildLaunchOptionsFromEnvironment()
    {
        var pwDebug = string.Equals(Environment.GetEnvironmentVariable("PWDEBUG")?.Trim(), "1", StringComparison.Ordinal);
        var headless = pwDebug ? false : ParseBooleanEnvironment("HEADLESS", fallback: true);

        var hasSlowMo = int.TryParse(Environment.GetEnvironmentVariable("SLOWMO_MS")?.Trim(), out var configuredSlowMo) &&
                        configuredSlowMo >= 0;
        var slowMoMs = hasSlowMo ? configuredSlowMo : (pwDebug ? 250 : 0);

        return new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = slowMoMs
        };
    }

    protected async Task RunUiAsync(string testName, Func<IPage, Task> run)
    {
        await using var context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true
        });

        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(Fixture.Settings.PwDebug ? 60_000 : 15_000);

        try
        {
            await run(page);
            await context.Tracing.StopAsync();
        }
        catch
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var artifactDirectory = Path.Combine(Fixture.Settings.ArtifactsDirectory, $"{Sanitize(testName)}-{timestamp}");
            Directory.CreateDirectory(artifactDirectory);

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDirectory, "failure.png"),
                FullPage = true
            });

            await context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = Path.Combine(artifactDirectory, "trace.zip")
            });

            throw;
        }
    }

    protected async Task NavigateAsync(IPage page, string route)
    {
        var url = new Uri(Fixture.Settings.BaseUrl, route.StartsWith('/') ? route : $"/{route}");
        await page.GotoAsync(url.ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });
    }

    private static string Sanitize(string name)
    {
        return InvalidFileNameChars.Replace(name, "-");
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
