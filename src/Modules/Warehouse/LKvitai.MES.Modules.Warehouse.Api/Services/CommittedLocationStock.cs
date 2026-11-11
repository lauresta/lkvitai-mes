using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
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
        var events = new List<object>();
        events.AddRange((await CommittedEventData.ReadAsync<GoodsReceivedEvent>(session, sku, ct)).Where(x => x.WarehouseId == "WH1"));
        events.AddRange((await CommittedEventData.ReadAsync<StockAdjustedEvent>(session, sku, ct)).Where(x => x.WarehouseId == "WH1"));
        events.AddRange(await CommittedEventData.ReadAsync<StockMovedEvent>(session, sku, ct));
        events.AddRange((await CommittedEventData.ReadAsync<PickCompletedEvent>(session, sku, ct)).Where(x => x.WarehouseId == "WH1"));
        events.AddRange((await CommittedEventData.ReadAsync<QCPassedEvent>(session, sku, ct)).Where(x => x.WarehouseId == "WH1"));
        events.AddRange((await CommittedEventData.ReadAsync<QCFailedEvent>(session, sku, ct)).Where(x => x.WarehouseId == "WH1"));
        return StockEventQuantity.CalculateAtLocation(events, sku, location);
    }
}
