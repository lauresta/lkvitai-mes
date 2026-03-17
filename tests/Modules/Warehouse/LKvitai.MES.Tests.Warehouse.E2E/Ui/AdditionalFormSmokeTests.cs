using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

[Collection("ui-e2e")]
public sealed class AdditionalFormSmokeTests : PlaywrightUiTestBase
{
    public AdditionalFormSmokeTests(PlaywrightFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task AgnumConfiguration_PageSmoke()
    {
        await RunUiAsync(nameof(AgnumConfiguration_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/agnum/config");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Agnum Export Configuration" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Export Scope", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Test Connection" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task AgnumReconciliation_PageSmoke()
    {
        await RunUiAsync(nameof(AgnumReconciliation_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/agnum/reconcile");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Agnum Reconciliation Report" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Generate Report" })).ToBeVisibleAsync();
            await Expect(page.GetByText("No reconciliation report generated. Upload Agnum balance and click Generate.", new() { Exact = true })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task ValuationAdjustCost_PageSmoke()
    {
        await RunUiAsync(nameof(ValuationAdjustCost_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/valuation/adjust-cost");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Adjust Item Cost" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Item", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Adjust Cost" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Back" })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task ValuationApplyLandedCost_PageSmoke()
    {
        await RunUiAsync(nameof(ValuationApplyLandedCost_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/valuation/apply-landed-cost");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Apply Landed Cost" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Shipment", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Apply Landed Cost" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Back" })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task ReceivingQc_PageSmoke()
    {
        await RunUiAsync(nameof(ReceivingQc_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/inbound/qc");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Receiving QC Queue" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Scan SKU / Lot", new() { Exact = true })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Refresh" }),
                page.GetByText("No QC pending rows", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task Putaway_PageSmoke()
    {
        await RunUiAsync(nameof(Putaway_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/putaway");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Putaway Tasks" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByText("Tasks from RECEIVING", new() { Exact = true }),
                page.GetByText("No putaway tasks available.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task Labels_PageSmoke()
    {
        await RunUiAsync(nameof(Labels_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/labels");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Labels Station" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Print / Preview", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Print" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Preview PDF" })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task SearchByImage_PageSmoke()
    {
        await RunUiAsync(nameof(SearchByImage_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/search-by-image");

            await Expect(page.GetByText("Search by Image", new() { Exact = true }).First).ToBeVisibleAsync();
            await Expect(page.GetByText("Tap to take a photo or choose from gallery", new() { Exact = true })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task PackingStation_PageSmoke()
    {
        await RunUiAsync(nameof(PackingStation_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/outbound/pack/00000000-0000-0000-0000-000000000002");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Packing Station" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByText("Order not found.", new() { Exact = true }),
                page.GetByText("Scan barcode", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task PickingTasks_PageSmoke()
    {
        await RunUiAsync(nameof(PickingTasks_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/picking/tasks");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Picking Tasks" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Create Task", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Create" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Complete Task" })).ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task CrossDock_PageSmoke()
    {
        await RunUiAsync(nameof(CrossDock_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/cross-dock");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Cross-Dock Tracking" })).ToBeVisibleAsync();
            await Expect(page.GetByText("Create Cross-Dock Match", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Create" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByText("Cross-Dock Queue", new() { Exact = true }),
                page.GetByText("No cross-dock records", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task WavePicking_PageSmoke()
    {
        await RunUiAsync(nameof(WavePicking_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/waves");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Wave Picking" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Create Wave" })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Create Wave" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByText("Active Waves", new() { Exact = true }),
                page.GetByText("No waves found", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminSettings_PageSmoke()
    {
        await RunUiAsync(nameof(AdminSettings_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/settings");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Warehouse Settings" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Save" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminApiKeys_PageSmoke()
    {
        await RunUiAsync(nameof(AdminApiKeys_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/api-keys");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "API Keys" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Create New Key" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminApprovalRules_PageSmoke()
    {
        await RunUiAsync(nameof(AdminApprovalRules_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/approval-rules");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Approval Rules" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Add Approval Rule" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminBackups_PageSmoke()
    {
        await RunUiAsync(nameof(AdminBackups_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/backups");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Backup Management" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Trigger Backup" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminGdprErasure_PageSmoke()
    {
        await RunUiAsync(nameof(AdminGdprErasure_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/gdpr-erasure");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "GDPR Erasure Requests" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByText("New Erasure Request", new() { Exact = true }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminSerialNumbers_PageSmoke()
    {
        await RunUiAsync(nameof(AdminSerialNumbers_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/serial-numbers");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Serial Numbers" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByText("Register Serial", new() { Exact = true }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminRetentionPolicies_PageSmoke()
    {
        await RunUiAsync(nameof(AdminRetentionPolicies_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/retention-policies");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Retention Policies" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Create Policy" }),
                page.GetByRole(AriaRole.Button, new() { Name = "Hide Form" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminRoles_PageSmoke()
    {
        await RunUiAsync(nameof(AdminRoles_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/roles");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Roles" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Add Role" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminReasonCodes_PageSmoke()
    {
        await RunUiAsync(nameof(AdminReasonCodes_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/reason-codes");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Reason Codes" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Add Reason Code" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    [Fact]
    public async Task AdminDisasterRecoveryDrills_PageSmoke()
    {
        await RunUiAsync(nameof(AdminDisasterRecoveryDrills_PageSmoke), async page =>
        {
            await NavigateAsync(page, "/warehouse/admin/dr-drills");

            await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Disaster Recovery Drills" })).ToBeVisibleAsync();
            await ExpectAnyVisibleAsync(
                page.GetByRole(AriaRole.Button, new() { Name = "Trigger Drill" }),
                page.GetByText("Admin role required.", new() { Exact = true }),
                page.GetByTestId("shared-error-banner"));
        });
    }

    private static async Task ExpectAnyVisibleAsync(params ILocator[] locators)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            foreach (var locator in locators)
            {
                if (await locator.CountAsync() > 0 && await locator.First.IsVisibleAsync())
                {
                    return;
                }
            }

            await Task.Delay(250);
        }

        Assert.Fail("Expected at least one locator to become visible.");
    }
}
