using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public interface IWorkStationService
{
    Task<IReadOnlyList<WorkStationDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<WorkStationDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkStationDto> CreateAsync(CreateWorkStationRequest request, CancellationToken cancellationToken);

    Task<WorkStationDto> UpdateAsync(Guid id, UpdateWorkStationRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
