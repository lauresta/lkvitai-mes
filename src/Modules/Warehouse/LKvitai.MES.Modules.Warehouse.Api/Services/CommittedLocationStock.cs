using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;
using Marten;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

internal static class CommittedLocationStock
{
    // Receiving/QC/putaway currently operate in WH1. Include receipts and QC events,
    // which do not live in StockLedger streams, before subtracting committed movements.
    public static async Task<decimal> ReadAsync(IDocumentStore store, string sku, string location, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        var key = sku.ToLowerInvariant();
        var events = new List<object>();
        events.AddRange(await session.Events.QueryRawEventDataOnly<GoodsReceivedEvent>()
            .Where(x => x.SKU.ToLower() == key && x.WarehouseId == "WH1").ToListAsync(ct));
        events.AddRange(await session.Events.QueryRawEventDataOnly<StockAdjustedEvent>()
            .Where(x => x.SKU.ToLower() == key && x.WarehouseId == "WH1").ToListAsync(ct));
        events.AddRange(await session.Events.QueryRawEventDataOnly<StockMovedEvent>()
            .Where(x => x.SKU.ToLower() == key).ToListAsync(ct));
        events.AddRange(await session.Events.QueryRawEventDataOnly<PickCompletedEvent>()
            .Where(x => x.SKU.ToLower() == key && x.WarehouseId == "WH1").ToListAsync(ct));
        events.AddRange(await session.Events.QueryRawEventDataOnly<QCPassedEvent>()
            .Where(x => x.SKU.ToLower() == key && x.WarehouseId == "WH1").ToListAsync(ct));
        events.AddRange(await session.Events.QueryRawEventDataOnly<QCFailedEvent>()
            .Where(x => x.SKU.ToLower() == key && x.WarehouseId == "WH1").ToListAsync(ct));
        return StockEventQuantity.CalculateAtLocation(events, sku, location);
    }
}
