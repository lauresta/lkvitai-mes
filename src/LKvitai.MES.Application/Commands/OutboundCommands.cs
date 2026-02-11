using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Application.Commands;

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
