using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// Command to allocate available stock to a reservation (SOFT lock).
/// Queries AvailableStock projection for candidates, then appends StockAllocatedEvent
/// to the reservation stream.
///
/// Idempotent via CommandId + ProcessedCommandStore.
/// </summary>
public record AllocateReservationCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid ReservationId { get; init; }
    public string WarehouseId { get; init; } = string.Empty;
}
