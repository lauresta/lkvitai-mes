using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

[Collection("ui-e2e")]
public sealed class MudBlazorGridFullFlowTests : PlaywrightUiTestBase
{
    public MudBlazorGridFullFlowTests(PlaywrightFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Lots_FullFlow()
    {
        await RunUiAsync(nameof(Lots_FullFlow), async page =>
        {
            var lotsUrl = new Uri(Fixture.Settings.BaseUrl, "/warehouse/admin/lots");
            await page.GotoAsync(lotsUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(ByTestId(page, "lots-page")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "lots-grid")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "lots-search")).ToBeVisibleAsync();

            await ByTestId(page, "lots-search").FillAsync("LOT");
            await ByTestId(page, "lots-search-btn").ClickAsync();
            await Expect(ByTestId(page, "lots-current-page")).ToContainTextAsync("Page");

            var lotHeader = ByTestId(page, "lots-grid").GetByRole(AriaRole.Columnheader, new() { Name = "Lot" });
            await lotHeader.ClickAsync();
            await Expect(ByTestId(page, "lots-grid")).ToBeVisibleAsync();

            await ByTestId(page, "lots-page-size").ClickAsync();
            await page.GetByRole(AriaRole.Option, new() { Name = "25" }).ClickAsync();
            await Expect(ByTestId(page, "lots-page-size")).ToContainTextAsync("25");

            await TryChangePageAsync(page, "lots-pager", "lots-current-page");
            await ByTestId(page, "lots-refresh").ClickAsync();
            await Expect(ByTestId(page, "lots-grid")).ToBeVisibleAsync();
            Assert.Equal(0, await page.GetByTestId("lots-error").CountAsync());
        });
    }

    [Fact]
    public async Task AvailableStock_FullFlow()
    {
        await RunUiAsync(nameof(AvailableStock_FullFlow), async page =>
        {
            await NavigateAsync(page, "/available-stock");

            await Expect(ByTestId(page, "stock-page")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "stock-before-search")).ToBeVisibleAsync();
            Assert.Equal(0, await page.GetByTestId("stock-grid").CountAsync());

            await WaitForStockFiltersReadyAsync(page);
            await SubmitStockSearchUntilGridVisibleAsync(page, "SKU");

            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "stock-summary")).ToContainTextAsync("Showing");

            await ByTestId(page, "stock-include-virtual").CheckAsync();
            await ByTestId(page, "stock-search-btn").ClickAsync();
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();

            var skuHeader = ByTestId(page, "stock-grid").GetByRole(AriaRole.Columnheader, new() { Name = "SKU" });
            await skuHeader.ClickAsync();
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();

            await ByTestId(page, "stock-page-size").ClickAsync();
            await page.GetByRole(AriaRole.Option, new() { Name = "25" }).ClickAsync();
            await Expect(ByTestId(page, "stock-page-size")).ToContainTextAsync("25");

            await TryChangePageAsync(page, "stock-pager", "stock-summary");

            await ByTestId(page, "stock-refresh").ClickAsync();
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();
            Assert.Equal(0, await page.GetByTestId("stock-error").CountAsync());

            var exportButton = ByTestId(page, "stock-export");
            if (await exportButton.IsEnabledAsync())
            {
                var download = await page.RunAndWaitForDownloadAsync(() => exportButton.ClickAsync());
                Assert.False(string.IsNullOrWhiteSpace(download.SuggestedFilename));
            }
        });
    }

    private static async Task TryChangePageAsync(IPage page, string pagerTestId, string pageIndicatorTestId)
    {
        var pager = ByTestId(page, pagerTestId);
        if (await pager.CountAsync() == 0)
        {
            return;
        }

        var before = await ByTestId(page, pageIndicatorTestId).InnerTextAsync();
        var buttons = pager.GetByRole(AriaRole.Button);
        var count = await buttons.CountAsync();

        for (var i = 0; i < count; i++)
        {
            var button = buttons.Nth(i);
            if (await button.IsDisabledAsync())
            {
                continue;
            }

            await button.ClickAsync();
            await page.WaitForTimeoutAsync(350);

            var after = await ByTestId(page, pageIndicatorTestId).InnerTextAsync();
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                Assert.NotEqual(before, after);
                return;
            }
        }
    }

    private static async Task SubmitStockSearchUntilGridVisibleAsync(IPage page, string searchValue)
    {
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await WaitForStockFiltersReadyAsync(page);

            var stockSearch = ByTestId(page, "stock-search");
            await Expect(stockSearch).ToBeVisibleAsync();
            await stockSearch.FillAsync(searchValue);
            await stockSearch.PressAsync("Tab");
            await ByTestId(page, "stock-search-btn").ClickAsync();

            var grid = ByTestId(page, "stock-grid");
            for (var i = 0; i < 12; i++)
            {
                if (await grid.CountAsync() > 0 && await grid.IsVisibleAsync())
                {
                    return;
                }

                var validation = ByTestId(page, "stock-validation-error");
                if (await validation.CountAsync() > 0 && await validation.IsVisibleAsync())
                {
                    break;
                }

                await page.WaitForTimeoutAsync(500);
            }
        }

        await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();
    }

    private static async Task WaitForStockFiltersReadyAsync(IPage page)
    {
        var loadingSpinner = page.Locator(".spinner-border");
        if (await loadingSpinner.CountAsync() > 0)
        {
            await Expect(loadingSpinner).ToBeHiddenAsync(new LocatorAssertionsToBeHiddenOptions
            {
                Timeout = 15_000
            });
        }

        await Expect(ByTestId(page, "stock-search")).ToBeVisibleAsync();
    }
}
