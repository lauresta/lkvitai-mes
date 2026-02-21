using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

public sealed record ScheduleCycleCountCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public DateTimeOffset ScheduledDate { get; init; }
    public string AbcClass { get; init; } = "ALL";
    public IReadOnlyList<int> LocationIds { get; init; } = Array.Empty<int>();
    public string AssignedOperator { get; init; } = string.Empty;
}

public sealed record RecordCountCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid CycleCountId { get; init; }
    public int LocationId { get; init; }
    public int ItemId { get; init; }
    public decimal PhysicalQty { get; init; }
    public string? Reason { get; init; }
    public string CountedBy { get; init; } = string.Empty;
}

public sealed record ApplyAdjustmentCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid CycleCountId { get; init; }
    public string? ApproverId { get; init; }
}
