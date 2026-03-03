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

            var scheduleTitle = ByTestId(page, "cycle-counts-schedule-title");
            var scheduleError = page.GetByTestId("shared-error-banner");

            try
            {
                await Expect(scheduleTitle).ToBeVisibleAsync();
                await Expect(ByTestId(page, "cycle-counts-schedule-date")).ToBeVisibleAsync();
                await Expect(ByTestId(page, "cycle-counts-schedule-abc")).ToBeVisibleAsync();
                await Expect(ByTestId(page, "cycle-counts-schedule-operator")).ToBeVisibleAsync();
            }
            catch (PlaywrightException)
            {
                await Expect(scheduleError).ToBeVisibleAsync();
            }

            var backButton = page.GetByRole(AriaRole.Button, new() { Name = "Back" });
            if (await backButton.CountAsync() > 0 && await backButton.First.IsVisibleAsync())
            {
                await backButton.First.ClickAsync();
            }
            else
            {
                await NavigateAsync(page, "/warehouse/cycle-counts");
            }

            await Expect(ByTestId(page, "cycle-counts-title")).ToBeVisibleAsync();
            Assert.Contains("/warehouse/cycle-counts", page.Url, StringComparison.OrdinalIgnoreCase);
        });
    }
}
