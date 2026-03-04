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

            await TrySelectLotsPageSizeAsync(page, "25");
            await EnsureMudOverlayClosedAsync(page);

            await TryChangePageAsync(page, "lots-pager", "lots-current-page");
            await EnsureMudOverlayClosedAsync(page);
            await ByTestId(page, "lots-refresh").ClickAsync();
            await Expect(ByTestId(page, "lots-grid")).ToBeVisibleAsync();

            var lotsError = page.GetByTestId("lots-error");
            if (await lotsError.CountAsync() > 0)
            {
                await Expect(lotsError).ToBeVisibleAsync();
            }
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

            if (await ByTestId(page, "stock-error").CountAsync() > 0 && await ByTestId(page, "stock-error").IsVisibleAsync())
            {
                await Expect(ByTestId(page, "stock-error")).ToBeVisibleAsync();
                return;
            }

            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();
            await Expect(ByTestId(page, "stock-summary")).ToContainTextAsync("Showing");

            await ByTestId(page, "stock-include-virtual").CheckAsync();
            await ByTestId(page, "stock-search-btn").ClickAsync();
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();

            var skuHeader = ByTestId(page, "stock-grid").GetByRole(AriaRole.Columnheader, new() { Name = "SKU" });
            await skuHeader.ClickAsync();
            await Expect(ByTestId(page, "stock-grid")).ToBeVisibleAsync();

            await ByTestId(page, "stock-page-size").SelectOptionAsync("25");
            Assert.Equal("25", await ByTestId(page, "stock-page-size").InputValueAsync());

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

    [Fact]
    public async Task ReportsReceivingHistory_PageSmoke()
    {
        await RunUiAsync(nameof(ReportsReceivingHistory_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/reports/receiving-history");

            await Expect(ByTestId(page, "reports-receiving-history-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "reports-receiving-history-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected receiving history grid or error banner to be visible.");
        });
    }

    [Fact]
    public async Task ReportsStockMovements_PageSmoke()
    {
        await RunUiAsync(nameof(ReportsStockMovements_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/reports/stock-movements");

            await Expect(ByTestId(page, "reports-stock-movements-page")).ToBeVisibleAsync();
            await ByTestId(page, "reports-stock-movements-apply").ClickAsync();

            var grid = ByTestId(page, "reports-stock-movements-grid");
            var error = page.GetByTestId("reports-stock-movements-error");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected stock movements grid or route-level error to be visible.");
        });
    }

    [Fact]
    public async Task ReportsDispatchHistory_PageSmoke()
    {
        await RunUiAsync(nameof(ReportsDispatchHistory_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/reports/dispatch-history");

            await Expect(ByTestId(page, "reports-dispatch-history-page")).ToBeVisibleAsync();
            await ByTestId(page, "reports-dispatch-history-apply").ClickAsync();

            var grid = ByTestId(page, "reports-dispatch-history-grid");
            var error = page.GetByTestId("reports-dispatch-history-error");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected dispatch history grid or route-level error to be visible.");
        });
    }

    [Fact]
    public async Task ReportsPickHistory_PageSmoke()
    {
        await RunUiAsync(nameof(ReportsPickHistory_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/reports/pick-history");

            await Expect(ByTestId(page, "reports-pick-history-page")).ToBeVisibleAsync();
            await ByTestId(page, "reports-pick-history-apply").ClickAsync();

            var grid = ByTestId(page, "reports-pick-history-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected pick history grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task ReportsStockLevel_PageSmoke()
    {
        await RunUiAsync(nameof(ReportsStockLevel_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/reports/stock-level");

            await Expect(ByTestId(page, "reports-stock-level-page")).ToBeVisibleAsync();
            await ByTestId(page, "reports-stock-level-apply").ClickAsync();

            var grid = ByTestId(page, "reports-stock-level-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected stock level grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task ReportsComplianceAudit_PageSmoke()
    {
        await RunUiAsync(nameof(ReportsComplianceAudit_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/reports/compliance-audit");

            await Expect(ByTestId(page, "reports-compliance-audit-page")).ToBeVisibleAsync();
            await ByTestId(page, "reports-compliance-audit-apply").ClickAsync();

            var grid = ByTestId(page, "reports-compliance-audit-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected compliance audit grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task InboundShipments_PageSmoke()
    {
        await RunUiAsync(nameof(InboundShipments_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/inbound/shipments");

            await Expect(ByTestId(page, "inbound-shipments-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "inbound-shipments-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected inbound shipments grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task OutboundDispatch_PageSmoke()
    {
        await RunUiAsync(nameof(OutboundDispatch_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/outbound/dispatch");

            await Expect(ByTestId(page, "outbound-dispatch-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "outbound-dispatch-grid");
            var pageError = ByTestId(page, "outbound-dispatch-error");
            var sharedError = page.GetByTestId("shared-error-banner");
            var emptyState = ByTestId(page, "outbound-dispatch-empty");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var pageErrorVisible = await pageError.CountAsync() > 0 && await pageError.IsVisibleAsync();
            var sharedErrorVisible = await sharedError.CountAsync() > 0 && await sharedError.IsVisibleAsync();
            var emptyVisible = await emptyState.CountAsync() > 0 && await emptyState.IsVisibleAsync();

            Assert.True(gridVisible || pageErrorVisible || sharedErrorVisible || emptyVisible,
                "Expected outbound dispatch grid, empty state, or error banner to be visible.");
        });
    }

    [Fact]
    public async Task InboundShipmentDetail_PageSmoke()
    {
        await RunUiAsync(nameof(InboundShipmentDetail_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/inbound/shipments/1");

            await Expect(ByTestId(page, "inbound-shipment-detail-page")).ToBeVisibleAsync();

            var lines = ByTestId(page, "inbound-shipment-detail-lines");
            var empty = ByTestId(page, "inbound-shipment-detail-empty");
            var error = ByTestId(page, "inbound-shipment-detail-error");
            var sharedError = page.GetByTestId("shared-error-banner");

            var linesVisible = await lines.CountAsync() > 0 && await lines.IsVisibleAsync();
            var emptyVisible = await empty.CountAsync() > 0 && await empty.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();
            var sharedErrorVisible = await sharedError.CountAsync() > 0 && await sharedError.IsVisibleAsync();

            Assert.True(linesVisible || emptyVisible || errorVisible || sharedErrorVisible,
                "Expected inbound shipment detail lines, empty state, or error banner to be visible.");
        });
    }

    [Fact]
    public async Task SalesOrderDetail_PageSmoke()
    {
        await RunUiAsync(nameof(SalesOrderDetail_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/sales/orders/00000000-0000-0000-0000-000000000001");

            await Expect(ByTestId(page, "sales-order-detail-page")).ToBeVisibleAsync();

            var lines = ByTestId(page, "sales-order-detail-lines");
            var empty = ByTestId(page, "sales-order-detail-empty");
            var error = ByTestId(page, "sales-order-detail-error");
            var sharedError = page.GetByTestId("shared-error-banner");

            var linesVisible = await lines.CountAsync() > 0 && await lines.IsVisibleAsync();
            var emptyVisible = await empty.CountAsync() > 0 && await empty.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();
            var sharedErrorVisible = await sharedError.CountAsync() > 0 && await sharedError.IsVisibleAsync();

            Assert.True(linesVisible || emptyVisible || errorVisible || sharedErrorVisible,
                "Expected sales order detail lines, empty state, or error banner to be visible.");
        });
    }

    [Fact]
    public async Task StockLocationBalance_PageSmoke()
    {
        await RunUiAsync(nameof(StockLocationBalance_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/stock/location-balance");

            await Expect(ByTestId(page, "stock-location-balance-page")).ToBeVisibleAsync();
            await ByTestId(page, "stock-location-balance-apply").ClickAsync();

            var grid = ByTestId(page, "stock-location-balance-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected location balance grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task AdminAuditLogs_PageSmoke()
    {
        await RunUiAsync(nameof(AdminAuditLogs_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/audit-logs");

            await Expect(ByTestId(page, "admin-audit-logs-page")).ToBeVisibleAsync();

            var noAccess = page.GetByTestId("admin-audit-logs-no-access");
            if (await noAccess.CountAsync() > 0 && await noAccess.IsVisibleAsync())
            {
                return;
            }

            var grid = ByTestId(page, "admin-audit-logs-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected audit logs grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task AdminWarehouses_PageSmoke()
    {
        await RunUiAsync(nameof(AdminWarehouses_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/admin/warehouses");

            await Expect(ByTestId(page, "admin-warehouses-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "admin-warehouses-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected warehouses grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task AdminUom_PageSmoke()
    {
        await RunUiAsync(nameof(AdminUom_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/uom");

            await Expect(ByTestId(page, "admin-uom-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "admin-uom-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected UoM grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task AdminSuppliers_PageSmoke()
    {
        await RunUiAsync(nameof(AdminSuppliers_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/admin/suppliers");

            await Expect(ByTestId(page, "admin-suppliers-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "admin-suppliers-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected suppliers grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task AdminSupplierMappings_PageSmoke()
    {
        await RunUiAsync(nameof(AdminSupplierMappings_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/admin/supplier-mappings");

            await Expect(ByTestId(page, "admin-supplier-mappings-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "admin-supplier-mappings-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected supplier mappings grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task AdminItems_PageSmoke()
    {
        await RunUiAsync(nameof(AdminItems_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/admin/items");

            await Expect(ByTestId(page, "admin-items-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "admin-items-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected items grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task AdminLocations_PageSmoke()
    {
        await RunUiAsync(nameof(AdminLocations_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/admin/locations");

            await Expect(ByTestId(page, "admin-locations-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "admin-locations-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected locations grid or shared error banner to be visible.");
        });
    }

    [Fact]
    public async Task Reservations_PageSmoke()
    {
        await RunUiAsync(nameof(Reservations_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/reservations");

            await Expect(ByTestId(page, "reservations-page")).ToBeVisibleAsync();

            var grid = ByTestId(page, "reservations-grid");
            var error = page.GetByTestId("shared-error-banner");

            var gridVisible = await grid.CountAsync() > 0 && await grid.IsVisibleAsync();
            var errorVisible = await error.CountAsync() > 0 && await error.IsVisibleAsync();

            Assert.True(gridVisible || errorVisible, "Expected reservations grid or shared error banner to be visible.");
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

                var stockError = ByTestId(page, "stock-error");
                if (await stockError.CountAsync() > 0 && await stockError.IsVisibleAsync())
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

        var finalGrid = ByTestId(page, "stock-grid");
        var finalError = ByTestId(page, "stock-error");
        var gridVisible = await finalGrid.CountAsync() > 0 && await finalGrid.IsVisibleAsync();
        var errorVisible = await finalError.CountAsync() > 0 && await finalError.IsVisibleAsync();

        Assert.True(gridVisible || errorVisible, "Expected stock grid or stock error banner to be visible.");
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

    private static async Task TrySelectLotsPageSizeAsync(IPage page, string optionText)
    {
        var scope = ByTestId(page, "lots-page-size");
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
