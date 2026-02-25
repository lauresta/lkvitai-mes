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

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Cycle Counts" })).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Schedule Cycle Count" }).ClickAsync();

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Schedule Cycle Count" })).ToBeVisibleAsync();
            await Expect(page.GetByLabel("Scheduled Date")).ToBeVisibleAsync();
            await Expect(page.GetByLabel("ABC Class")).ToBeVisibleAsync();
            await Expect(page.GetByLabel("Assigned Operator")).ToBeVisibleAsync();

            await page.GetByRole(AriaRole.Button, new() { Name = "Back" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Cycle Counts" })).ToBeVisibleAsync();
            Assert.Contains("/warehouse/cycle-counts", page.Url, StringComparison.OrdinalIgnoreCase);
        });
    }
}
