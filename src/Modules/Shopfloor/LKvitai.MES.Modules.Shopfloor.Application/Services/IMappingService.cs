using LKvitai.MES.Modules.Shopfloor.Contracts.Mappings;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public interface IMappingService
{
    Task<CoverageSummaryDto> GetCoverageAsync(CancellationToken cancellationToken);

    Task BulkAssignAsync(BulkAssignMappingRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(string legacyProductTypeCode, CancellationToken cancellationToken);
}
