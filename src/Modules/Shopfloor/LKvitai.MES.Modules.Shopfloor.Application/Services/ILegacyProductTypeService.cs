using LKvitai.MES.Modules.Shopfloor.Contracts.Common;
using LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

/// <summary>Outcome of a legacy product type sync run.</summary>
public sealed record LegacyProductTypeSyncResult(int Fetched, int Inserted, int Updated, int Removed);

public interface ILegacyProductTypeService
{
    Task<PagedResult<LegacyProductTypeDto>> QueryAsync(LegacyProductTypesQuery query, CancellationToken cancellationToken);

    Task<LegacyProductTypeSyncResult> SyncAsync(CancellationToken cancellationToken);
}
