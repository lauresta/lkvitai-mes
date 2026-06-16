using LKvitai.MES.Modules.Shopfloor.Contracts.Common;
using LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Ports;

public interface ILegacyProductTypeRepository
{
    /// <summary>Paged list joined with the current mapping (read model projection).</summary>
    Task<PagedResult<LegacyProductTypeDto>> QueryAsync(LegacyProductTypesQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegacyProductType>> ListAllAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetExistingCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    Task<int> CountActiveAsync(CancellationToken cancellationToken);

    Task<int> CountActiveMappedAsync(CancellationToken cancellationToken);

    void Add(LegacyProductType productType);
}
