using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Application.Commands;

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
