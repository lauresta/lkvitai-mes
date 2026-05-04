namespace LKvitai.MES.Modules.Portal.Api.Persistence;

public static class PortalSeedData
{
    public static IReadOnlyList<PortalTile> Tiles(DateTimeOffset now) =>
    [
        Tile("warehouse", "Warehouse", "Operations", "Inventory locations, stock movements, reservations, handling units, 3D warehouse layout.", "Live", "/warehouse/", null, "warehouse", 10, now),
        Tile("sales", "Sales", "Commercial", "Customer orders.", "Pilot", "/sales/", null, "sales", 20, now),
        Tile("frontline", "Frontline", "Field", "Field availability lookup.", "Pilot", "/frontline/", null, "frontline", 30, now),
        Tile("scanning", "Scanning", "Mobile", "Mobile barcode scan.", "Pilot", "/scan/", null, "scanning", 40, now),
        Tile("orders", "Orders", "Commercial", "Order lifecycle, product composition, workflow planning.", "Planned", null, "Q3 2026", "orders", 50, now),
        Tile("shopfloor", "Shopfloor", "Operations", "Workstation tasks, WIP routing, operator kiosk execution.", "Planned", null, "Q3 2026", "shopfloor", 60, now),
        Tile("quality", "Quality", "Operations", "Inspections, defect tracking, rework and returns.", "Planned", null, "Q4 2026", "quality", 70, now),
        Tile("delivery", "Delivery", "Logistics", "Route planning, driver tasks, proof of delivery, tracking.", "Planned", null, "Q4 2026", "delivery", 80, now),
        Tile("installation", "Installation", "Logistics", "Installer visits, acceptance acts, customer sign-off.", "Planned", null, "Q1 2027", "installation", 90, now),
        Tile("reporting", "Reporting", "Intelligence", "Dashboards, KPIs, production and warehouse analytics.", "Planned", null, "Q1 2027", "reporting", 100, now),
        Tile("finance", "Finance", "Intelligence", "Accounting exports, payments, posting events.", "Planned", null, "Q2 2027", "finance", 110, now),
        Tile("audit", "Audit", "Compliance", "Immutable event log, traceability, compliance reports.", "Planned", null, "Q2 2027", "audit", 120, now),
    ];

    private static PortalTile Tile(
        string key,
        string title,
        string category,
        string description,
        string status,
        string? url,
        string? quarter,
        string iconKey,
        int sortOrder,
        DateTimeOffset now) =>
        new()
        {
            Key = key,
            Title = title,
            Category = category,
            Description = description,
            Status = status,
            Url = url,
            Quarter = quarter,
            IconKey = iconKey,
            SortOrder = sortOrder,
            IsVisible = true,
            RequiredRoles = [],
            CreatedAt = now,
            UpdatedAt = now
        };
}
