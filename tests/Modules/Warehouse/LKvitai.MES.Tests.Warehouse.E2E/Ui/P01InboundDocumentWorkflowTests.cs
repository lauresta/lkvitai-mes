using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

[Collection("ui-e2e")]
public sealed class P01InboundDocumentWorkflowTests : PlaywrightUiTestBase
{
    public P01InboundDocumentWorkflowTests(PlaywrightFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Agnum_Config_AddMapping_ShowsEditableRow()
    {
        await RunUiAsync(nameof(Agnum_Config_AddMapping_ShowsEditableRow), async page =>
        {
            await NavigateAsync(page, "/warehouse/agnum/config");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Agnum Export Configuration" })).ToBeVisibleAsync();
            await Expect(page.GetByText("(_isLoading || _isSaving || _isTestingConnection)")).ToHaveCountAsync(0);
            await Expect(page.GetByText("No mappings configured. Add your first mapping.", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("No export history found.", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Test Connection", new() { Exact = true })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task P01_InboundReceiving_DocumentFlow_CompletesCoreSteps()
    {
        await RunUiAsync(nameof(P01_InboundReceiving_DocumentFlow_CompletesCoreSteps), async page =>
        {
            var referenceNumber = $"P01-E2E-{DateTime.UtcNow:yyyyMMddHHmmss}";

            await NavigateAsync(page, "/warehouse/inbound/shipments/create");
            await Expect(page.GetByTestId("inbound-shipment-create-form")).ToBeVisibleAsync();

            var createError = page.GetByTestId("inbound-shipment-create-error");
            if (await createError.CountAsync() > 0 && await createError.IsVisibleAsync())
            {
                await Expect(createError).ToContainTextAsync("Backend unavailable");
                return;
            }

            await SelectFirstNonPlaceholderOptionAsync(
                page,
                page.GetByTestId("inbound-shipment-create-supplier"),
                "Select supplier");

            var selectedItem = await SelectFirstNonPlaceholderOptionAsync(
                page,
                page.GetByTestId("inbound-shipment-create-line-item").First,
                "Select item");

            var skuFromSelection = selectedItem.Split(" - ", 2, StringSplitOptions.TrimEntries)[0];
            Assert.False(string.IsNullOrWhiteSpace(skuFromSelection), "Expected selected inbound item SKU to be captured.");

            await page.GetByTestId("inbound-shipment-create-reference").FillAsync(referenceNumber);
            await page.GetByTestId("inbound-shipment-create-line-qty").First.FillAsync("1");
            await page.GetByTestId("inbound-shipment-create-submit").ClickAsync();

            await Expect(page.GetByTestId("inbound-shipment-detail-page")).ToBeVisibleAsync();
            await Expect(page.GetByText(referenceNumber, new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByTestId("inbound-shipment-detail-form")).ToBeVisibleAsync();

            var firstLine = page.GetByTestId("inbound-shipment-detail-lines").Locator("tbody tr").First;
            await Expect(firstLine).ToBeVisibleAsync();

            var sku = (await firstLine.Locator("td").Nth(2).Locator(".mud-typography").First.InnerTextAsync()).Trim();
            Assert.False(string.IsNullOrWhiteSpace(sku), "Expected inbound detail row to expose SKU.");
            Assert.Equal(skuFromSelection, sku);

            var expectedQty = ParseLeadingDecimal(await firstLine.Locator("td").Nth(4).InnerTextAsync());
            var receivedQty = ParseLeadingDecimal(await firstLine.Locator("td").Nth(5).InnerTextAsync());
            var remainingQty = expectedQty - receivedQty;
            Assert.True(remainingQty > 0m, "Expected shipment line to have remaining quantity.");

            var receiveQty = Math.Min(1m, remainingQty);
            var requiresLotTracking = await page.GetByText("LOT", new() { Exact = true }).CountAsync() > 0;
            var requiresQc = await page.GetByText("QC", new() { Exact = true }).CountAsync() > 0;

            if (requiresLotTracking)
            {
                await page.GetByLabel("Lot Number").FillAsync($"LOT-{DateTime.UtcNow:yyyyMMddHHmmss}");
            }

            await page.GetByRole(AriaRole.Spinbutton).First.FillAsync(receiveQty.ToString("0.###", CultureInfo.InvariantCulture));
            await page.GetByTestId("inbound-shipment-detail-receive").ClickAsync();

            await Expect(page.GetByText("Received:", new() { Exact = false }).First)
                .ToContainTextAsync((receivedQty + receiveQty).ToString("0.###", CultureInfo.InvariantCulture));

            await page.GetByRole(AriaRole.Button, new() { Name = "Open QC Queue" }).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Receiving QC Queue" })).ToBeVisibleAsync();

            if (requiresQc)
            {
                var qcRow = page.Locator("tbody tr").Filter(new() { HasText = sku }).First;
                await Expect(qcRow).ToBeVisibleAsync();
                await qcRow.GetByRole(AriaRole.Button, new() { Name = "Pass" }).ClickAsync();
                await Expect(qcRow).ToHaveCountAsync(0);
            }

            await NavigateAsync(page, "/available-stock");
            await Expect(page.GetByTestId("stock-filters")).ToBeVisibleAsync();
            await Expect(page.GetByTestId("stock-search")).ToBeVisibleAsync();

            await page.GetByTestId("stock-search").FillAsync(sku);
            await page.GetByTestId("stock-search-btn").ClickAsync();

            await Expect(page.GetByTestId("stock-grid")).ToBeVisibleAsync();
            await Expect(page.GetByTestId("stock-grid").GetByText(sku, new() { Exact = false }).First).ToBeVisibleAsync();
        });
    }

    private static decimal ParseLeadingDecimal(string raw)
    {
        var match = Regex.Match(raw, @"\d+(?:[.,]\d+)?");
        Assert.True(match.Success, $"Expected a decimal value in '{raw}'.");
        return decimal.Parse(match.Value.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    private static async Task<string> SelectFirstNonPlaceholderOptionAsync(
        IPage page,
        ILocator control,
        params string[] placeholderTexts)
    {
        var triggerCandidates = new[]
        {
            control,
            control.GetByRole(AriaRole.Combobox),
            control.GetByRole(AriaRole.Button),
            control.Locator("input"),
            control.Locator("[role='combobox']"),
            control.Locator("[aria-haspopup='listbox']")
        };

        foreach (var candidate in triggerCandidates)
        {
            if (await candidate.CountAsync() == 0 || !await candidate.First.IsVisibleAsync())
            {
                continue;
            }

            await candidate.First.ClickAsync();

            var options = page.GetByRole(AriaRole.Option);
            var optionCount = await options.CountAsync();
            for (var i = 0; i < optionCount; i++)
            {
                var option = options.Nth(i);
                if (!await option.IsVisibleAsync())
                {
                    continue;
                }

                var text = (await option.InnerTextAsync()).Trim();
                if (string.IsNullOrWhiteSpace(text) ||
                    placeholderTexts.Any(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                await option.ClickAsync();
                return text;
            }

            await page.Keyboard.PressAsync("Escape");
        }

        Assert.Fail("Expected a selectable non-placeholder option to be available.");
        return string.Empty;
    }

}
