using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

[Collection("ui-e2e")]
public sealed class P05CycleCountNavigationTests : PlaywrightUiTestBase
{
    public P05CycleCountNavigationTests(PlaywrightFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task CycleCount_List_ToSchedule_AndBack_Works()
    {
        await RunUiAsync(nameof(CycleCount_List_ToSchedule_AndBack_Works), async page =>
        {
            await NavigateAsync(page, "/warehouse/cycle-counts");

            await Expect(ByTestId(page, "cycle-counts-title")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Schedule Cycle Count" }).ClickAsync();

            await Expect(ByTestId(page, "cycle-counts-schedule-title")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "cycle-counts-schedule-date")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "cycle-counts-schedule-abc")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "cycle-counts-schedule-operator")).ToBeVisibleAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Back" }).ClickAsync();
            await Expect(ByTestId(page, "cycle-counts-title")).ToBeVisibleAsync();
            Assert.Contains("/warehouse/cycle-counts", page.Url, StringComparison.OrdinalIgnoreCase);
        });
    }
}
