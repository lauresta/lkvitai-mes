using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;

/// <summary>Authoritative physical on-hand quantity, independent of asynchronous projections and reservations.</summary>
public static class StockEventQuantity
{
    public static decimal Calculate(IEnumerable<object> events, string sku, IEnumerable<Location> locations)
    {
        var physical = locations.Where(PhysicalLocation.IsPhysical).Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CalculateAt(events, sku, location => physical.Contains(location));
    }

    public static decimal CalculateAtLocation(IEnumerable<object> events, string sku, string location)
        => CalculateAt(events, sku, value => Same(value, location));

    private static decimal CalculateAt(IEnumerable<object> events, string sku, Func<string, bool> includes)
    {
        decimal total = 0m;
        decimal At(string? location, decimal qty) => location is not null && includes(location) ? qty : 0m;
        foreach (var evt in events)
        {
            total += evt switch
            {
                GoodsReceivedEvent e when Same(e.SKU, sku) => At(e.DestinationLocation, e.ReceivedQty),
                StockAdjustedEvent e when Same(e.SKU, sku) => At(e.Location, e.QtyDelta),
                StockMovedEvent e when Same(e.SKU, sku) => At(e.ToLocation, e.Quantity) - At(e.FromLocation, e.Quantity),
                PickCompletedEvent e when Same(e.SKU, sku) => At(e.ToLocation, e.PickedQty) - At(e.FromLocation, e.PickedQty),
                QCPassedEvent e when Same(e.SKU, sku) => At(e.ToLocation, e.Qty) - At(e.FromLocation, e.Qty),
                QCFailedEvent e when Same(e.SKU, sku) => At(e.ToLocation, e.Qty) - At(e.FromLocation, e.Qty),
                _ => 0m
            };
        }
        return total;
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
