using Marten.Events.Aggregation;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;

namespace LKvitai.MES.Projections;

/// <summary>
/// LocationBalance projection per blueprint
/// Subscribes to StockMoved events and maintains current balance per (location, SKU)
/// CRITICAL: Must be MultiStreamProjection (flat table across all StockLedger streams)
/// CRITICAL: Must be Async lifecycle (uses Marten async daemon)
/// </summary>
public class LocationBalanceProjection : MultiStreamProjection<LocationBalanceView, string>
{
    public LocationBalanceProjection()
    {
        // Identity: composite key (Location, SKU)
        Identity<StockMovedEvent>(e => $"{e.FromLocation}:{e.SKU}");
        Identity<StockMovedEvent>(e => $"{e.ToLocation}:{e.SKU}");
    }
    
    /// <summary>
    /// Update FROM and TO location balances
    /// V-5 Rule B: Uses only self-contained event data (no external queries)
    /// </summary>
    public void Apply(StockMovedEvent evt, LocationBalanceView view)
    {
        // Handle FROM location (decrease balance)
        if (view.Location == evt.FromLocation && view.SKU == evt.SKU)
        {
            view.Quantity -= evt.Quantity;
            view.LastUpdated = evt.Timestamp;
        }
        
        // Handle TO location (increase balance)
        if (view.Location == evt.ToLocation && view.SKU == evt.SKU)
        {
            view.Quantity += evt.Quantity;
            view.LastUpdated = evt.Timestamp;
        }
    }
}

public class LocationBalanceView
{
    public Guid Id { get; set; }
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime LastUpdated { get; set; }
}
