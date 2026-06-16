using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public interface IWorkCenterService
{
    Task<IReadOnlyList<WorkCenterDto>> ListAsync(CancellationToken cancellationToken);

    Task<WorkCenterDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkCenterDto> CreateAsync(CreateWorkCenterRequest request, CancellationToken cancellationToken);

    Task<WorkCenterDto> UpdateAsync(Guid id, UpdateWorkCenterRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
