using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

public sealed record ScannedItemCommand
{
    public string Barcode { get; init; } = string.Empty;
    public decimal Qty { get; init; }
}

public sealed record PackOrderCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid OutboundOrderId { get; init; }
    public string PackagingType { get; init; } = "BOX";
    public List<ScannedItemCommand> ScannedItems { get; init; } = new();
}

public sealed record DispatchShipmentCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid ShipmentId { get; init; }
    public string Carrier { get; init; } = "FEDEX";
    public string? VehicleId { get; init; }
    public DateTimeOffset? DispatchTime { get; init; }
    public string? ManualTrackingNumber { get; init; }
}
