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

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Create Inbound Shipment" })).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Create Shipment" }).ClickAsync();

            await Expect(page.GetByText("Supplier is required.")).ToBeVisibleAsync();
            Assert.Contains("/warehouse/inbound/shipments/create", page.Url, StringComparison.OrdinalIgnoreCase);
        });
    }
}
