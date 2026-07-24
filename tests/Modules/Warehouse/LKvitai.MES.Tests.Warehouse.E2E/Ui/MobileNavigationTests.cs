using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

[Collection("ui-e2e")]
public sealed class MobileNavigationTests : PlaywrightUiTestBase
{
    public MobileNavigationTests(PlaywrightFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task MobileNavigation_OpensExpanded_AndClosesAfterNavigation()
    {
        await RunUiAsync(nameof(MobileNavigation_OpensExpanded_AndClosesAfterNavigation), async page =>
        {
            await page.SetViewportSizeAsync(390, 844);
            await NavigateAsync(page, "/warehouse/dashboard");

            var toggle = page.Locator(".topbar__menu-toggle");
            var drawer = page.Locator("#warehouse-navigation");

            await Expect(toggle).ToBeVisibleAsync();
            await Expect(toggle).ToHaveAttributeAsync("aria-label", "Open Warehouse navigation");
            await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");

            await toggle.ClickAsync();

            await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");
            await Expect(drawer).ToHaveClassAsync(new Regex(@"\bmud-drawer--open\b"));
            await Expect(drawer).ToHaveCSSAsync("width", "240px");
            await Expect(drawer.GetByText("Warehouse", new() { Exact = true })).ToBeVisibleAsync();

            await page.Keyboard.PressAsync("Escape");
            await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
            await toggle.ClickAsync();

            await drawer
                .Locator("button.nav-section-toggle")
                .Filter(new() { HasText = "Stock" })
                .ClickAsync();
            await drawer
                .GetByRole(AriaRole.Link, new() { Name = "Available Stock", Exact = true })
                .ClickAsync();

            await Expect(page).ToHaveURLAsync(new Regex(@"/warehouse/available-stock$"));
            await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
            await Expect(drawer).ToHaveClassAsync(new Regex(@"\bmud-drawer--closed\b"));
        });
    }

    [Fact]
    public async Task MobileNavigation_IgnoresPersistedDesktopCollapse()
    {
        await RunUiAsync(nameof(MobileNavigation_IgnoresPersistedDesktopCollapse), async page =>
        {
            await page.SetViewportSizeAsync(1280, 900);
            await NavigateAsync(page, "/warehouse/dashboard");

            var drawer = page.Locator("#warehouse-navigation");
            await drawer
                .GetByRole(AriaRole.Button, new() { Name = "Collapse sidebar", Exact = true })
                .ClickAsync();
            await Expect(drawer).ToHaveClassAsync(new Regex(@"\bis-collapsed\b"));

            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.NetworkIdle });
            drawer = page.Locator("#warehouse-navigation");
            await Expect(drawer).ToHaveClassAsync(new Regex(@"\bis-collapsed\b"));

            await page.SetViewportSizeAsync(390, 844);

            var toggle = page.Locator(".topbar__menu-toggle");
            await Expect(toggle).ToBeVisibleAsync();
            await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
            await toggle.ClickAsync();

            await Expect(drawer).ToHaveCSSAsync("width", "240px");
            await Expect(drawer.GetByText("Warehouse", new() { Exact = true })).ToBeVisibleAsync();

            var drawerClasses = await drawer.GetAttributeAsync("class");
            Assert.DoesNotContain("is-collapsed", drawerClasses ?? string.Empty, StringComparison.Ordinal);
        });
    }
}
