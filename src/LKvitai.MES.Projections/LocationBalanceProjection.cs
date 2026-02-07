using Marten.Events.Aggregation;

namespace LKvitai.MES.Projections;

/// <summary>
/// LocationBalance projection per blueprint
/// Subscribes to StockMoved events and maintains current balance per (location, SKU)
/// </summary>
public class LocationBalanceProjection : SingleStreamProjection<LocationBalanceView>
{
    // Projection placeholder - implementation per blueprint to be added
    // Uses Marten async daemon
    
    // Event handlers to be implemented:
    // public void Apply(StockMovedEvent evt, LocationBalanceView view) { }
}

public class LocationBalanceView
{
    public Guid Id { get; set; }
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime LastUpdated { get; set; }
}
