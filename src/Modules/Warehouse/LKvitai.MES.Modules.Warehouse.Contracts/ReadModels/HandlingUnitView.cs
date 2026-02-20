namespace LKvitai.MES.Contracts.ReadModels;

/// <summary>
/// Read model for Handling Unit state (flat doc keyed by HU ID).
/// Updated by HandlingUnitProjection (ASYNC) from HU lifecycle events
/// and StockMoved events (design spec: HU derives lines from StockMoved).
///
/// Lives in Contracts so that both Projections (writer) and Infrastructure (reader)
/// can reference it without creating a circular dependency.
/// </summary>
public class HandlingUnitView
{
    /// <summary>
    /// Document key: HU ID as string (Guid.ToString()).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public Guid HuId { get; set; }
    public string LPN { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string CurrentLocation { get; set; } = string.Empty;

    /// <summary>
    /// Lines: each entry is (SKU → quantity).
    /// Updated by StockMoved events (receipt adds lines, pick removes lines)
    /// and by LineAdded/LineRemoved events (split/merge scenarios).
    /// </summary>
    public List<HandlingUnitLineItem> Lines { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? SealedAt { get; set; }
    public DateTime LastUpdated { get; set; }

    // ── Mutation helpers ────────────────────────────────────────────

    /// <summary>
    /// Adds (or increases) a line for the given SKU.
    /// </summary>
    public void AddLine(string sku, decimal quantity)
    {
        var existing = Lines.FirstOrDefault(l => l.SKU == sku);
        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            Lines.Add(new HandlingUnitLineItem { SKU = sku, Quantity = quantity });
        }
    }

    /// <summary>
    /// Removes (or decreases) a line for the given SKU.
    /// If quantity drops to 0, the line is removed.
    /// </summary>
    public void RemoveLine(string sku, decimal quantity)
    {
        var existing = Lines.FirstOrDefault(l => l.SKU == sku);
        if (existing != null)
        {
            existing.Quantity -= quantity;
            if (existing.Quantity <= 0)
            {
                Lines.Remove(existing);
            }
        }
    }

    /// <summary>
    /// Checks whether all lines have been removed (HU is empty).
    /// </summary>
    public bool IsEmpty => Lines.Count == 0 || Lines.All(l => l.Quantity <= 0);
}

/// <summary>
/// A single line item within a HandlingUnitView.
/// </summary>
public class HandlingUnitLineItem
{
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
