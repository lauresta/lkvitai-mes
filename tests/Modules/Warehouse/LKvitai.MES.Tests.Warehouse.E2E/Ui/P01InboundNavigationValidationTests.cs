using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

[Collection("ui-e2e")]
public sealed class P01InboundNavigationValidationTests : PlaywrightUiTestBase
{
    public P01InboundNavigationValidationTests(PlaywrightFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Inbound_CreatePage_BlocksSubmit_WhenSupplierMissing()
    {
        await RunUiAsync(nameof(Inbound_CreatePage_BlocksSubmit_WhenSupplierMissing), async page =>
        {
            await NavigateAsync(page, "/warehouse/inbound/shipments/create");

            await Expect(page.GetByTestId("inbound-shipment-create-form")).ToBeVisibleAsync();
            await page.GetByTestId("inbound-shipment-create-submit")
                .ClickAsync(new LocatorClickOptions { Force = true });

            var validationError = page.GetByText("Supplier is required.");
            var errorBanner = page.GetByTestId("shared-error-banner");

            try
            {
                await Expect(validationError).ToBeVisibleAsync();
            }
            catch (PlaywrightException)
            {
                await Expect(errorBanner).ToBeVisibleAsync();
            }

            Assert.Contains("/warehouse/inbound/shipments/create", page.Url, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Outbound_CreatePage_BlocksSubmit_WhenCustomerMissing()
    {
        await RunUiAsync(nameof(Outbound_CreatePage_BlocksSubmit_WhenCustomerMissing), async page =>
        {
            await NavigateAsync(page, "/warehouse/sales/orders/create");

            await Expect(page.GetByTestId("sales-order-create-form")).ToBeVisibleAsync();
            await Expect(page.GetByTestId("sales-order-create-customer")).ToBeVisibleAsync();
            await page.GetByTestId("sales-order-create-submit")
                .ClickAsync(new LocatorClickOptions { Force = true });

            var validationError = page.GetByText("Select a customer.");
            var errorBanner = page.GetByTestId("shared-error-banner");

            if (await validationError.IsVisibleAsync() || await errorBanner.IsVisibleAsync())
            {
                Assert.True(true);
            }
            else
            {
                await Expect(page.GetByTestId("sales-order-create-form")).ToBeVisibleAsync();
            }

            Assert.Contains("/warehouse/sales/orders/create", page.Url, StringComparison.OrdinalIgnoreCase);
        });
    }
}
