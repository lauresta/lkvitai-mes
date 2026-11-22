using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.Modules.Warehouse.WebUI.Resources;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Shared;

/// <summary>
/// The Warehouse sidebar as data: sections and screens with their resource keys. <c>NavMenu</c> renders it
/// and the portal "Search everything" box (issue #375) searches it, so a screen added here shows up in both.
/// </summary>
public static class WarehouseNavigation
{
    /// <summary>Href that <c>NavMenu</c> replaces with the configured Hangfire dashboard url.</summary>
    public const string HangfireHref = "__hangfire__";

    public sealed record Item(string Href, string Icon, string LabelKey, bool External = false);

    public sealed record Section(string Key, string LabelKey, string Icon, Item[] Items);

    /// <summary>The dashboard link rendered above the sections.</summary>
    public static Item Dashboard { get; } = new("dashboard", "bi-speedometer2", "Nav.Dashboard");

    public static IReadOnlyList<Section> Sections { get; } =
    [
        new("stock", "Nav.Section.Stock", "bi-box-seam", [
            new("available-stock", "bi-box-seam", "Nav.AvailableStock"),
            new("search-by-image", "bi-camera", "Nav.SearchByImage"),
            new("stock/dashboard", "bi-grid", "Nav.StockDashboard"),
            new("stock/location-balance", "bi-columns-gap", "Nav.LocationBalance"),
            new("stock/adjustments", "bi-journal-minus", "Nav.Adjustments"),
            new("reservations", "bi-clipboard-check", "Nav.Reservations"),
        ]),

        new("inbound", "Nav.Section.Inbound", "bi-box-arrow-in-down", [
            new("inbound/shipments", "bi-box-arrow-in-down", "Nav.InboundShipments"),
            new("inbound/qc", "bi-clipboard2-pulse", "Nav.ReceivingQC"),
            new("putaway", "bi-box-arrow-up-right", "Nav.Putaway"),
            new("production-issues", "bi-box-arrow-right", "Nav.ProductionIssues"),
        ]),

        new("outbound", "Nav.Section.Outbound", "bi-box-arrow-right", [
            new("sales/orders", "bi-file-earmark-text", "Nav.SalesOrders"),
            new("sales/allocations", "bi-sliders", "Nav.Allocations"),
            new("outbound/orders", "bi-box-arrow-right", "Nav.OutboundOrders"),
            new("outbound/dispatch", "bi-truck", "Nav.Dispatch"),
            new("waves", "bi-layers", "Nav.WavePicking"),
            new("picking/tasks", "bi-list-task", "Nav.PickingTasks"),
            new("labels", "bi-upc-scan", "Nav.Labels"),
            new("cross-dock", "bi-arrow-left-right", "Nav.CrossDock"),
            new("rmas", "bi-arrow-counterclockwise", "Nav.RMAs"),
        ]),

        new("operations", "Nav.Section.Operations", "bi-gear", [
            new("transfers", "bi-arrow-left-right", "Nav.Transfers"),
            new("cycle-counts", "bi-clipboard-check", "Nav.CycleCounts"),
            new("visualization/3d", "bi-bounding-box-circles", "Nav.WarehouseMap"),
            new("projections", "bi-database-gear", "Nav.Projections"),
        ]),

        new("finance", "Nav.Section.Finance", "bi-cash-stack", [
            new("valuation/dashboard", "bi-cash-stack", "Nav.Valuation"),
            new("valuation/opening-balances", "bi-box-seam", "Nav.OpeningBalances"),
            new("valuation/intrastat", "bi-globe-europe-africa", "Nav.Intrastat"),
            new("valuation/month-end", "bi-calendar-check", "Nav.MonthEnd"),
            new("agnum/config", "bi-journal-text", "Nav.AgnumConfig"),
            new("agnum/accounting", "bi-diagram-3", "Nav.AgnumAccounting"),
            new("agnum/outbox", "bi-send-check", "Nav.AgnumOutbox"),
            new("additional-costs", "bi-receipt", "Nav.AdditionalCosts"),
            new("agnum/balances", "bi-boxes", "Nav.AgnumBalances"),
            new("agnum/reconcile", "bi-calculator", "Nav.AgnumReconcile"),
        ]),

        new("admin", "Nav.Section.Admin", "bi-shield-lock", [
            new("admin/users", "bi-people", "Nav.Users"),
            new("admin/settings", "bi-sliders", "Nav.AdminSettings"),
            new("admin/reason-codes", "bi-list-check", "Nav.ReasonCodes"),
            new("admin/approval-rules", "bi-ui-checks-grid", "Nav.ApprovalRules"),
            new("admin/roles", "bi-person-gear", "Nav.Roles"),
            new("admin/api-keys", "bi-key", "Nav.APIKeys"),
            new("admin/gdpr-erasure", "bi-shield-exclamation", "Nav.GDPRErasure"),
            new("admin/audit-logs", "bi-journal-text", "Nav.AuditLogs"),
            new("admin/retention-policies", "bi-hourglass-split", "Nav.RetentionPolicies"),
            new("admin/serial-numbers", "bi-upc-scan", "Nav.SerialNumbers"),
            new("admin/lots", "bi-box2", "Nav.Lots"),
            new("admin/uom", "bi-rulers", "Nav.UnitsOfMeasure"),
            new("admin/items", "bi-boxes", "Nav.Items"),
            new("admin/suppliers", "bi-truck", "Nav.Suppliers"),
            new("admin/supplier-mappings", "bi-link-45deg", "Nav.SupplierMappings"),
            new("admin/customers", "bi-person-badge", "Nav.Customers"),
            new("admin/locations", "bi-geo-alt", "Nav.Locations"),
            new("admin/slots", "bi-grid-3x3-gap", "Nav.Slots"),
            new("admin/warehouses", "bi-building", "Nav.Warehouses"),
            new("admin/categories", "bi-diagram-3", "Nav.Categories"),
            new("admin/price-groups", "bi-currency-euro", "Nav.PriceGroups"),
            new("admin/import", "bi-upload", "Nav.ImportWizard"),
            new("admin/layout-editor", "bi-layout-text-window-reverse", "Nav.LayoutEditor"),
        ]),

        new("reports", "Nav.Section.Reports", "bi-clipboard-data", [
            new("reports/stock-level", "bi-clipboard-data", "Nav.StockLevel"),
            new("reports/supplier-stock", "bi-truck", "Nav.SupplierStock"),
            new("reports/receiving-history", "bi-box-arrow-in-down", "Nav.ReceivingHistory"),
            new("reports/pick-history", "bi-list-check", "Nav.PickHistory"),
            new("reports/dispatch-history", "bi-truck-flatbed", "Nav.DispatchHistory"),
            new("reports/stock-movements", "bi-arrow-repeat", "Nav.StockMovements"),
            new("reports/traceability", "bi-diagram-3", "Nav.Traceability"),
            new("compliance/lot-trace", "bi-diagram-3-fill", "Nav.LotTraceability"),
            new("reports/compliance-audit", "bi-shield-check", "Nav.ComplianceAudit"),
            new("compliance/dashboard", "bi-clock-history", "Nav.ComplianceDashboard"),
        ]),

        new("analytics", "Nav.Section.Analytics", "bi-graph-up-arrow", [
            new("analytics/fulfillment", "bi-graph-up-arrow", "Nav.FulfillmentKPIs"),
            new("analytics/quality", "bi-shield-exclamation", "Nav.QualityAnalytics"),
        ]),

        new("system", "Nav.Section.System", "bi-terminal", [
            new(HangfireHref, "bi-clock-history", "Nav.JobQueue", External: true),
        ]),
    ];

    /// <summary>Every in-app screen of the sidebar as a search entry: label in the current UI language, matched in all of them.</summary>
    public static IReadOnlyList<ScreenEntry> Screens(IStringLocalizer<WarehouseResources> text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var cultures = CikadaLocalization.Languages.Select(x => x.Code).ToArray();
        var module = text["Common.Warehouse"].Value;
        var screens = new List<ScreenEntry>
        {
            new(Dashboard.Href, text[Dashboard.LabelKey].Value, module, ScreenLabels.InCultures(text, Dashboard.LabelKey, cultures)),
        };

        foreach (var section in Sections)
        {
            var subtitle = $"{module} · {text[section.LabelKey].Value}";
            screens.AddRange(section.Items
                .Where(item => !item.External)
                .Select(item => new ScreenEntry(item.Href, text[item.LabelKey].Value, subtitle, ScreenLabels.InCultures(text, item.LabelKey, cultures))));
        }

        return screens;
    }
}
