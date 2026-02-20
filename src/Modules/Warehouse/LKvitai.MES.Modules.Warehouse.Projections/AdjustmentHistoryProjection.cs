using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using Marten.Events.Aggregation;

namespace LKvitai.MES.Modules.Warehouse.Projections;

/// <summary>
/// Append-only projection for stock adjustment audit history.
/// </summary>
public sealed class AdjustmentHistoryProjection : SingleStreamProjection<AdjustmentHistoryView>
{
    public AdjustmentHistoryView Create(StockAdjustedEvent evt)
    {
        return new AdjustmentHistoryView
        {
            Id = evt.AdjustmentId.ToString("N"),
            AdjustmentId = evt.AdjustmentId,
            ItemId = evt.ItemId,
            SKU = evt.SKU,
            ItemName = null,
            LocationId = evt.LocationId,
            Location = evt.Location,
            LocationCode = evt.Location,
            QtyDelta = evt.QtyDelta,
            ReasonCode = evt.ReasonCode,
            Notes = evt.Notes,
            UserId = evt.UserId,
            UserName = null,
            Timestamp = evt.Timestamp
        };
    }
}
