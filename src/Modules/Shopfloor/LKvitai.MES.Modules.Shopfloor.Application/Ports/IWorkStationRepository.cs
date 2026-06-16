using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Ports;

/// <summary>A work station joined with its owning work center's name.</summary>
public sealed record WorkStationWithCenter(WorkStation Station, string WorkCenterName);

public interface IWorkStationRepository
{
    Task<IReadOnlyList<WorkStationWithCenter>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkStationWithCenter>> ListActiveAsync(CancellationToken cancellationToken);

    Task<WorkStationWithCenter?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    void Add(WorkStation workStation);

    void Remove(WorkStation workStation);
}
