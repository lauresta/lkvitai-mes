using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

public sealed record TransferLineCommand
{
    public int ItemId { get; init; }
    public decimal Qty { get; init; }
    public int FromLocationId { get; init; }
    public int ToLocationId { get; init; }
    public Guid? LotId { get; init; }
}

public sealed record CreateTransferCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public string FromWarehouse { get; init; } = string.Empty;
    public string ToWarehouse { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public List<TransferLineCommand> Lines { get; init; } = new();
}

public sealed record ApproveTransferCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid TransferId { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;
}

public sealed record SubmitTransferCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid TransferId { get; init; }
}

public sealed record ExecuteTransferCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid TransferId { get; init; }
}
