using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.Ports;
public interface IOrderEntryAddressLookup
{
    Task<IReadOnlyList<OrderAddressReferenceDto>> GetAsync(int? parentId, string? search, int? id, CancellationToken ct);
}
