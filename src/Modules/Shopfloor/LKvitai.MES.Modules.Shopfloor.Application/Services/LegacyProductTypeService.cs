using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Contracts.Common;
using LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public sealed class LegacyProductTypeService : ILegacyProductTypeService
{
    private readonly ILegacyProductTypeRepository _repository;
    private readonly ILegacyProductTypeSource _source;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LegacyProductTypeService> _logger;

    public LegacyProductTypeService(
        ILegacyProductTypeRepository repository,
        ILegacyProductTypeSource source,
        IUnitOfWork unitOfWork,
        ILogger<LegacyProductTypeService> logger)
    {
        _repository = repository;
        _source = source;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task<PagedResult<LegacyProductTypeDto>> QueryAsync(LegacyProductTypesQuery query, CancellationToken cancellationToken)
        => _repository.QueryAsync(Normalize(query), cancellationToken);

    public async Task<LegacyProductTypeSyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        var rows = await _source.FetchAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        var existing = await _repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var existingByCode = existing.ToDictionary(x => x.Code, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var inserted = 0;
        var updated = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Code) || !seen.Add(row.Code))
            {
                continue;
            }

            if (existingByCode.TryGetValue(row.Code, out var entity))
            {
                entity.ApplySync(row.KindName, row.Name, now);
                updated++;
            }
            else
            {
                _repository.Add(new LegacyProductType(row.Code, row.KindName, row.Name, now));
                inserted++;
            }
        }

        var removed = 0;
        foreach (var entity in existing)
        {
            if (!seen.Contains(entity.Code) && !entity.Removed)
            {
                entity.MarkRemoved(now);
                removed++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "[Shopfloor] legacy product type sync fetched={Fetched} inserted={Inserted} updated={Updated} removed={Removed}",
            rows.Count, inserted, updated, removed);

        return new LegacyProductTypeSyncResult(rows.Count, inserted, updated, removed);
    }

    private static LegacyProductTypesQuery Normalize(LegacyProductTypesQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var size = query.PageSize is < 1 or > 500 ? 100 : query.PageSize;
        return query with { Page = page, PageSize = size };
    }
}
