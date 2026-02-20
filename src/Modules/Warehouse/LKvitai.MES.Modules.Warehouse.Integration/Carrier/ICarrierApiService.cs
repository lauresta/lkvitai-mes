using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Integration.Carrier;

public interface ICarrierApiService
{
    Task<Result<string>> GenerateTrackingNumberAsync(
        Guid shipmentId,
        string carrier,
        CancellationToken cancellationToken = default);
}
