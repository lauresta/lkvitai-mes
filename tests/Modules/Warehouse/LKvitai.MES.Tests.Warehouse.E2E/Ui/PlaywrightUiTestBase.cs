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
        page.SetDefaultTimeout(15_000);

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
}
