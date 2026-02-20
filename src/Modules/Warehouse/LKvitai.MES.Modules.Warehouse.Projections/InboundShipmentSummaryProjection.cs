using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using Marten.Events.Aggregation;

namespace LKvitai.MES.Modules.Warehouse.Projections;

/// <summary>
/// Receiving dashboard projection with expected/received quantities per shipment.
/// </summary>
public sealed class InboundShipmentSummaryProjection : SingleStreamProjection<InboundShipmentSummaryView>
{
    public InboundShipmentSummaryView Create(InboundShipmentCreatedEvent evt)
    {
        var completion = evt.TotalExpectedQty <= 0m
            ? 0m
            : 0m;

        return new InboundShipmentSummaryView
        {
            Id = InboundShipmentSummaryView.ComputeId(evt.ShipmentId),
            ShipmentId = evt.ShipmentId,
            ReferenceNumber = evt.ReferenceNumber,
            SupplierId = evt.SupplierId,
            SupplierName = evt.SupplierName,
            TotalExpectedQty = evt.TotalExpectedQty,
            TotalReceivedQty = 0m,
            CompletionPercent = completion,
            TotalLines = evt.TotalLines,
            ExpectedDate = evt.ExpectedDate,
            Status = "Draft",
            CreatedAt = evt.Timestamp,
            LastUpdated = evt.Timestamp
        };
    }

    public void Apply(GoodsReceivedEvent evt, InboundShipmentSummaryView view)
    {
        view.TotalReceivedQty += evt.ReceivedQty;
        view.CompletionPercent = view.TotalExpectedQty <= 0m
            ? 0m
            : Math.Min(1m, view.TotalReceivedQty / view.TotalExpectedQty);

        view.Status = view.TotalReceivedQty switch
        {
            <= 0m => "Draft",
            _ when view.TotalReceivedQty >= view.TotalExpectedQty => "Complete",
            _ => "Partial"
        };

        view.LastUpdated = evt.Timestamp;
    }
}
