using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.Ports;
public interface ILegacyProductPreview
{
    Task<LegacyProductPreviewDto> CalculateAsync(LegacyProductPreviewRequest request,CancellationToken cancellationToken);
}
