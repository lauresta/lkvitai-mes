using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.Ports;
public interface ISalesOrderEntryService
{
    Task<OrderEditorSnapshot?> GetAsync(long orderId, CancellationToken ct);
    Task<CommandOutcome?> GetOutcomeAsync(Guid actorId, Guid commandId, CancellationToken ct);
    Task<OrderEntryCommandResult> CreateAsync(Guid actorId, CreateOrderCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> UpdateAsync(Guid actorId, long orderId, UpdateOrderCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> CreateProductAsync(Guid actorId, long orderId, CreateProductCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> ConfigureProductAsync(Guid actorId, long orderId, long productId, ConfigureProductCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> SaveAccessoryAsync(Guid actorId, long orderId, long productId, SaveAccessoryCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> DeleteAccessoryAsync(Guid actorId, long orderId, long productId, int accessoryId, DeleteAccessoryCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> DeleteProductAsync(Guid actorId, long orderId, long productId, DeleteProductCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> SaveAddressAsync(Guid actorId, long orderId, SaveOrderAddressCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> SaveDutyAsync(Guid actorId, long orderId, SaveOrderDutyCommand command, CancellationToken ct);
    Task<OrderEntryCommandResult> CancelAsync(Guid actorId,long orderId,CancelOrderCommand command,CancellationToken ct);
    Task<OrderEntryCommandResult> DeleteDutyAsync(Guid actorId,long orderId,int dutyId,int employeeId,DeleteOrderDutyCommand command,CancellationToken ct);
    Task<OrderEntryCommandResult> SetFinalPriceAsync(Guid actorId,long orderId,SetOrderFinalPriceCommand command,CancellationToken ct);
    Task<OrderEntryCommandResult> DuplicateProductAsync(Guid actorId,long orderId,long productId,DuplicateProductCommand command,CancellationToken ct);
}
