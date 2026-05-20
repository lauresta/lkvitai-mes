using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

public record DistributeAgnumBalanceCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public Guid VirtualBalanceId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string WarehouseId { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Guid OperatorId { get; init; }
}
