using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.Ports;
public interface ISalesOrderFinancialCommands
{
    Task<OrderPaymentReferences> GetPaymentReferencesAsync(long orderId,CancellationToken ct,OrderPaymentReferenceSelection? selection=null);
    Task<OrderPaymentBankTransferPage> SearchPaymentBankTransfersAsync(long orderId,string? search,int page,int pageSize,int? selectedId,CancellationToken ct);
    Task<OrderEntryCommandResult> PostPaymentAsync(Guid actorId,long orderId,PostOrderPaymentCommand command,CancellationToken ct);
    Task<OrderEntryCommandResult> ConfirmProductsAsync(Guid actorId,long orderId,ConfirmOrderProductsCommand command,CancellationToken ct);
}
