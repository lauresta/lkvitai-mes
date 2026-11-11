using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Projections;

namespace LKvitai.MES.Tests.Warehouse.Unit;

// Tests the real stock reducer; PostgreSQL transaction semantics are covered by integration tests.
internal sealed class ProjectedCycleCountAdjustmentWriter(WarehouseDbContext db) : ICycleCountAdjustmentWriter
{
    public List<StockAdjustedEvent> Events { get; } = [];
    public Dictionary<string, AvailableStockView> Stock { get; } = [];

    public async Task CommitAsync(IReadOnlyList<StockAdjustedEvent> adjustments, CancellationToken cancellationToken)
    {
        foreach (var adjustment in adjustments)
        {
            Events.Add(adjustment);
            var key = adjustment.Location + ":" + adjustment.SKU;
            if (!Stock.TryGetValue(key, out var view))
            {
                var line = db.CycleCountLines.Local.Single(x => x.Id == adjustment.AdjustmentId);
                view = new AvailableStockView { OnHandQty = line.SystemQty };
            }
            Stock[key] = new AvailableStockProjection().Apply(adjustment, view);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
