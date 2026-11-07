using LKvitai.MES.Contracts.ReadModels;

namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

public interface IItemPriceSnapshotService
{
    Task<IReadOnlyList<ItemPriceSnapshot>> GetAsync(IReadOnlyCollection<int> itemIds,
        IReadOnlyCollection<string> skus, CancellationToken ct);
}
