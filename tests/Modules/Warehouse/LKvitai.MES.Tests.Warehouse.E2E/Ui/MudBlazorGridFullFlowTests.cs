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

            var lotsSearchInput = ByTestId(page, "lots-search").GetByRole(AriaRole.Textbox).First;
            await Expect(lotsSearchInput).ToBeVisibleAsync();
            await lotsSearchInput.FillAsync("LOT");
            await ByTestId(page, "lots-search-btn").ClickAsync();
            await Expect(ByTestId(page, "lots-current-page")).ToContainTextAsync("Page");

            var lotHeader = ByTestId(page, "lots-grid").GetByRole(AriaRole.Columnheader, new() { Name = "Lot" });
            await lotHeader.ClickAsync();
            await Expect(ByTestId(page, "lots-grid")).ToBeVisibleAsync();

            await TrySelectMudSelectOptionAsync(page, "lots-page-size", "25");
            await EnsureMudOverlayClosedAsync(page);

            await TryChangePageAsync(page, "lots-pager", "lots-current-page");
            await EnsureMudOverlayClosedAsync(page);
            await ByTestId(page, "lots-refresh").ClickAsync();
            await Expect(ByTestId(page, "lots-grid")).ToBeVisibleAsync();
            Assert.Equal(0, await page.GetByTestId("lots-error").CountAsync());
        });
    }

    [Fact]
    public async Task AdminItems_FullFlow()
    {
        await RunUiAsync(nameof(AdminItems_FullFlow), async page =>
        {
            var itemsUrl = new Uri(Fixture.Settings.BaseUrl, "/warehouse/admin/items");
            await page.GotoAsync(itemsUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(ByTestId(page, "admin-items-page")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "admin-items-grid")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "admin-items-search")).ToBeVisibleAsync();

            var searchInput = ByTestId(page, "admin-items-search").GetByRole(AriaRole.Textbox).First;
            await Expect(searchInput).ToBeVisibleAsync();
            await searchInput.FillAsync("SKU");
            await ByTestId(page, "admin-items-search-btn").ClickAsync();
            await Expect(ByTestId(page, "admin-items-current-page")).ToContainTextAsync("Page");

            var skuHeader = ByTestId(page, "admin-items-grid").GetByRole(AriaRole.Columnheader, new() { Name = "SKU" });
            await skuHeader.ClickAsync();
            await Expect(ByTestId(page, "admin-items-grid")).ToBeVisibleAsync();

            await TrySelectMudSelectOptionAsync(page, "admin-items-page-size", "25");
            await EnsureMudOverlayClosedAsync(page);
            await TryChangePageAsync(page, "admin-items-pager", "admin-items-current-page");
            await EnsureMudOverlayClosedAsync(page);

            await ByTestId(page, "admin-items-create").ClickAsync();
            await Expect(page.GetByText("Create Item").First).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
            await EnsureMudOverlayClosedAsync(page);

            var actionButtons = ByTestId(page, "admin-items-grid").Locator("button[aria-label^='Actions for']");
            if (await actionButtons.CountAsync() > 0)
            {
                await actionButtons.First.ClickAsync();
                await Expect(page.GetByText("Photos")).ToBeVisibleAsync();
                await Expect(page.GetByText("Edit")).ToBeVisibleAsync();
                await page.GetByText("Edit").ClickAsync();
                await Expect(page.GetByText("Edit Item").First).ToBeVisibleAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
                await EnsureMudOverlayClosedAsync(page);
            }

            await ByTestId(page, "admin-items-refresh").ClickAsync();
            await Expect(ByTestId(page, "admin-items-grid")).ToBeVisibleAsync();
            Assert.Equal(0, await page.GetByTestId("admin-items-error").CountAsync());
        });
    }

    [Fact]
    public async Task AdminSuppliers_FullFlow()
    {
        await RunUiAsync(nameof(AdminSuppliers_FullFlow), async page =>
        {
            var suppliersUrl = new Uri(Fixture.Settings.BaseUrl, "/warehouse/admin/suppliers");
            await page.GotoAsync(suppliersUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(ByTestId(page, "admin-suppliers-page")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "admin-suppliers-grid")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "admin-suppliers-search")).ToBeVisibleAsync();

            var searchInput = ByTestId(page, "admin-suppliers-search").GetByRole(AriaRole.Textbox).First;
            await Expect(searchInput).ToBeVisibleAsync();
            await searchInput.FillAsync("SUP");
            await ByTestId(page, "admin-suppliers-search-btn").ClickAsync();
            await Expect(ByTestId(page, "admin-suppliers-current-page")).ToContainTextAsync("Page");

            var codeHeader = ByTestId(page, "admin-suppliers-grid").GetByRole(AriaRole.Columnheader, new() { Name = "Code", Exact = true });
            await codeHeader.ClickAsync();
            await Expect(ByTestId(page, "admin-suppliers-grid")).ToBeVisibleAsync();

            await TrySelectMudSelectOptionAsync(page, "admin-suppliers-page-size", "25");
            await EnsureMudOverlayClosedAsync(page);
            await TryChangePageAsync(page, "admin-suppliers-pager", "admin-suppliers-current-page");
            await EnsureMudOverlayClosedAsync(page);

            await ByTestId(page, "admin-suppliers-create").ClickAsync();
            await Expect(page.GetByText("Create Supplier").First).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
            await EnsureMudOverlayClosedAsync(page);

            var actionButtons = ByTestId(page, "admin-suppliers-grid").Locator("button[aria-label^='Actions for']");
            if (await actionButtons.CountAsync() > 0)
            {
                await actionButtons.First.ClickAsync();
                await Expect(page.GetByText("View mappings")).ToBeVisibleAsync();
                await Expect(page.GetByText("Edit")).ToBeVisibleAsync();
                await page.GetByText("Edit").ClickAsync();
                await Expect(page.GetByText("Edit Supplier").First).ToBeVisibleAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
                await EnsureMudOverlayClosedAsync(page);
            }

            await Expect(ByTestId(page, "admin-suppliers-mappings")).ToBeVisibleAsync();

            await ByTestId(page, "admin-suppliers-refresh").ClickAsync();
            await Expect(ByTestId(page, "admin-suppliers-grid")).ToBeVisibleAsync();
            Assert.Equal(0, await page.GetByTestId("admin-suppliers-error").CountAsync());
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

            await TrySetCheckboxAsync(page, "stock-include-virtual", true);
            await ByTestId(page, "stock-search-btn").ClickAsync();
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();

            var skuHeader = ByTestId(page, "stock-grid").GetByRole(AriaRole.Columnheader, new() { Name = "SKU" });
            await skuHeader.ClickAsync();
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();

            await TrySelectMudSelectOptionAsync(page, "stock-page-size", "25");
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();

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

            await EnsureMudOverlayClosedAsync(page);
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

            var stockSearch = StockSearchInput(page);
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

        await Expect(StockSearchInput(page)).ToBeVisibleAsync();
    }

    private static ILocator StockSearchInput(IPage page)
        => ByTestId(page, "stock-search").GetByRole(AriaRole.Textbox).First;

    private static async Task TrySelectMudSelectOptionAsync(IPage page, string testId, string optionText)
    {
        var scope = ByTestId(page, testId);
        if (await scope.CountAsync() == 0)
        {
            return;
        }

        var triggerCandidates = new[]
        {
            scope.GetByRole(AriaRole.Combobox),
            scope.GetByRole(AriaRole.Button),
            scope.Locator("[aria-haspopup='listbox']"),
            scope.Locator("input"),
            scope.Locator(".mud-select-input")
        };

        ILocator? trigger = null;
        foreach (var candidate in triggerCandidates)
        {
            if (await candidate.CountAsync() > 0 && await candidate.First.IsVisibleAsync())
            {
                trigger = candidate.First;
                break;
            }
        }

        if (trigger is null)
        {
            return;
        }

        await trigger.ClickAsync();

        var option = page.GetByRole(AriaRole.Option, new() { Name = optionText }).First;
        if (await option.CountAsync() > 0)
        {
            await option.ClickAsync();
            await EnsureMudOverlayClosedAsync(page);
            return;
        }

        var textOption = page.GetByText(optionText, new() { Exact = true }).First;
        if (await textOption.CountAsync() > 0)
        {
            await textOption.ClickAsync();
            await EnsureMudOverlayClosedAsync(page);
            return;
        }

        await EnsureMudOverlayClosedAsync(page);
    }

    private static async Task TrySetCheckboxAsync(IPage page, string testId, bool isChecked)
    {
        var scope = ByTestId(page, testId);
        if (await scope.CountAsync() == 0)
        {
            return;
        }

        var target = scope.GetByRole(AriaRole.Checkbox);
        if (await target.CountAsync() == 0)
        {
            target = scope.Locator("input[type='checkbox']");
        }

        if (await target.CountAsync() == 0)
        {
            target = scope;
        }

        await target.First.SetCheckedAsync(isChecked);
    }

    private static async Task EnsureMudOverlayClosedAsync(IPage page)
    {
        var visibleOverlay = page.Locator(".mud-overlay:visible");
        if (await visibleOverlay.CountAsync() == 0)
        {
            return;
        }

        await page.Keyboard.PressAsync("Escape");
        await Expect(visibleOverlay).ToHaveCountAsync(0, new LocatorAssertionsToHaveCountOptions
        {
            Timeout = 5_000
        });
    }
}
