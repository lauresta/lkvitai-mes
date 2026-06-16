using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Ports;

public interface IWorkCenterRepository
{
    Task<IReadOnlyList<WorkCenter>> ListAsync(CancellationToken cancellationToken);

    Task<WorkCenter?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> HasStationsAsync(Guid workCenterId, CancellationToken cancellationToken);

    void Add(WorkCenter workCenter);

    void Remove(WorkCenter workCenter);
}
