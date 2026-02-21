using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// StartPicking command - Transitions reservation from SOFT to HARD lock
/// [MITIGATION R-3] Atomic HARD lock acquisition with balance re-validation
/// </summary>
public record StartPickingCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    
    public Guid ReservationId { get; init; }
    public Guid OperatorId { get; init; }
}
