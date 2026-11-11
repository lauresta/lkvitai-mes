using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Contracts.Events;
using Marten;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>One committed balance of a (warehouse, location, SKU) computed from events, not projections.</summary>
public sealed record CommittedBalance(string Warehouse, string Location, string Sku, decimal Quantity);

public static class CommittedLocationStock
{
    /// <summary>
    /// Quantity at one location of one warehouse. Receipts and QC events carry the warehouse in their data;
    /// stock movements (including Agnum distribution) only in the stream id.
    /// </summary>
    public static async Task<decimal> ReadAsync(IDocumentStore store, string warehouseCode, string sku, string location, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var balances = await ReadBalancesAsync(session, sku, ct);
        return balances
            .Where(x => Same(x.Warehouse, warehouseCode) && Same(x.Location, location) && Same(x.Sku, sku))
            .Sum(x => x.Quantity);
    }

    /// <summary>All non-aggregated balances of every warehouse for one SKU, or for all SKUs when <paramref name="sku"/> is null.</summary>
    public static async Task<IReadOnlyList<CommittedBalance>> ReadBalancesAsync(IQuerySession session, string? sku, CancellationToken ct)
    {
        var deltas = new List<(string Warehouse, string Location, string Sku, decimal Delta)>();
        void Add(string? warehouse, string? location, string eventSku, decimal delta)
        {
            if (string.IsNullOrWhiteSpace(warehouse) || string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(eventSku)) return;
            deltas.Add((warehouse.Trim(), location.Trim(), eventSku.Trim(), delta));
        }
        foreach (var e in await CommittedEventData.ReadAsync<GoodsReceivedEvent>(session, sku, ct))
            Add(e.WarehouseId, e.DestinationLocation, e.SKU, e.ReceivedQty);
        foreach (var e in await CommittedEventData.ReadAsync<StockAdjustedEvent>(session, sku, ct))
            Add(e.WarehouseId, e.Location, e.SKU, e.QtyDelta);
        foreach (var e in await CommittedEventData.ReadAsync<PickCompletedEvent>(session, sku, ct))
        {
            Add(e.WarehouseId, e.ToLocation, e.SKU, e.PickedQty);
            Add(e.WarehouseId, e.FromLocation, e.SKU, -e.PickedQty);
        }
        foreach (var e in await CommittedEventData.ReadAsync<QCPassedEvent>(session, sku, ct))
        {
            Add(e.WarehouseId, e.ToLocation, e.SKU, e.Qty);
            Add(e.WarehouseId, e.FromLocation, e.SKU, -e.Qty);
        }
        foreach (var e in await CommittedEventData.ReadAsync<QCFailedEvent>(session, sku, ct))
        {
            Add(e.WarehouseId, e.ToLocation, e.SKU, e.Qty);
            Add(e.WarehouseId, e.FromLocation, e.SKU, -e.Qty);
        }
        foreach (var moved in await CommittedEventData.ReadStockMovedAsync(session, sku, ct))
        {
            Add(moved.Warehouse, moved.Event.ToLocation, moved.Event.SKU, moved.Event.Quantity);
            Add(moved.Warehouse, moved.Event.FromLocation, moved.Event.SKU, -moved.Event.Quantity);
        }

        return deltas
            .GroupBy(x => (W: x.Warehouse.ToUpperInvariant(), L: x.Location.ToUpperInvariant(), S: x.Sku.ToUpperInvariant()))
            .Select(g => new CommittedBalance(g.First().Warehouse, g.First().Location, g.First().Sku, g.Sum(x => x.Delta)))
            .ToList();
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
