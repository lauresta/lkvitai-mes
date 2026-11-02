using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.Ports;
public interface ILiveOrderEntryCatalog
{
    Task<LiveOrderEntryCatalogDto> GetAsync(CancellationToken cancellationToken);
}
