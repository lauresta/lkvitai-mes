using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

public sealed record AdjustCostCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public int ItemId { get; init; }
    public decimal NewUnitCost { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid? ApproverId { get; init; }
}

public sealed record InitializeValuationCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public int ItemId { get; init; }
    public decimal InitialCost { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record AdjustValuationCostCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public int ItemId { get; init; }
    public decimal NewCost { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
}

public sealed record ApplyLandedCostCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid ShipmentId { get; init; }
    public decimal FreightCost { get; init; }
    public decimal DutyCost { get; init; }
    public decimal InsuranceCost { get; init; }
}

public sealed record WriteDownCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public int ItemId { get; init; }
    public decimal NewValue { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
}
