using Microsoft.Playwright;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public UiTestSettings Settings { get; } = UiTestSettings.FromEnvironment();

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Settings.ArtifactsDirectory);

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(PlaywrightUiTestBase.BuildLaunchOptionsFromEnvironment());
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}
