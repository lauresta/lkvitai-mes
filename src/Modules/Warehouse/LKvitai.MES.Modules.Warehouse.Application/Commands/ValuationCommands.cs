using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>Revaluation ("Уценка") document: lower the unit price of an item's valued stock.</summary>
public sealed record RevaluationCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public int ItemId { get; init; }
    public decimal NewUnitPrice { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
}
