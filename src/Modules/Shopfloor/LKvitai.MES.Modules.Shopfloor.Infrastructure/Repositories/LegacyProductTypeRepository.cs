using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Contracts.Common;
using LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Repositories;

public sealed class LegacyProductTypeRepository : ILegacyProductTypeRepository
{
    private readonly ShopfloorDbContext _db;

    public LegacyProductTypeRepository(ShopfloorDbContext db) => _db = db;

    public async Task<PagedResult<LegacyProductTypeDto>> QueryAsync(
        LegacyProductTypesQuery query,
        CancellationToken cancellationToken)
    {
        // Single LEFT JOIN to the mapping. Filters run against the joined
        // entities (EF cannot translate a Where over a projected record
        // constructor), and the flat projection happens only in the final
        // materialized Select. Template code/name is resolved in a second query.
        var joined =
            from lpt in _db.LegacyProductTypes.AsNoTracking()
            join map in _db.ProductTypeWorkflowMaps.AsNoTracking()
                on lpt.Code equals map.LegacyProductTypeCode into maps
            from map in maps.DefaultIfEmpty()
            select new { Legacy = lpt, Map = map };

        if (!query.Removed)
        {
            joined = joined.Where(x => x.Legacy.RemovedAt == null);
        }

        if (query.Mapped is { } mapped)
        {
            joined = mapped
                ? joined.Where(x => x.Map != null)
                : joined.Where(x => x.Map == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            joined = joined.Where(x =>
                EF.Functions.ILike(x.Legacy.Code, term) ||
                EF.Functions.ILike(x.Legacy.Name, term) ||
                EF.Functions.ILike(x.Legacy.KindName, term));
        }

        var total = await joined.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await joined
            .OrderBy(x => x.Legacy.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new ProjectedRow(
                x.Legacy.Code,
                x.Legacy.KindName,
                x.Legacy.Name,
                x.Legacy.RemovedAt,
                x.Map == null ? (Guid?)null : x.Map.WorkflowTemplateId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var templateIds = rows
            .Where(x => x.MappedTemplateId.HasValue)
            .Select(x => x.MappedTemplateId!.Value)
            .Distinct()
            .ToList();

        var templates = templateIds.Count == 0
            ? new Dictionary<Guid, TemplateRef>()
            : await _db.WorkflowTemplates.AsNoTracking()
                .Where(t => templateIds.Contains(t.Id))
                .Select(t => new TemplateRef(t.Id, t.Code, t.Name))
                .ToDictionaryAsync(t => t.Id, cancellationToken)
                .ConfigureAwait(false);

        var items = rows
            .Select(x =>
            {
                string? templateCode = null;
                string? templateName = null;
                if (x.MappedTemplateId is { } id && templates.TryGetValue(id, out var tpl))
                {
                    templateCode = tpl.Code;
                    templateName = tpl.Name;
                }

                return new LegacyProductTypeDto(
                    x.Code,
                    x.KindName,
                    x.Name,
                    x.RemovedAt != null,
                    x.MappedTemplateId,
                    templateCode,
                    templateName);
            })
            .ToList();

        return new PagedResult<LegacyProductTypeDto>(items, total, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<LegacyProductType>> ListAllAsync(CancellationToken cancellationToken)
        => await _db.LegacyProductTypes.ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        return _db.LegacyProductTypes.AnyAsync(x => x.Code == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetExistingCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return Array.Empty<string>();
        }

        return await _db.LegacyProductTypes.AsNoTracking()
            .Where(x => codes.Contains(x.Code))
            .Select(x => x.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountActiveAsync(CancellationToken cancellationToken)
        => _db.LegacyProductTypes.CountAsync(x => x.RemovedAt == null, cancellationToken);

    public Task<int> CountActiveMappedAsync(CancellationToken cancellationToken)
        => (from lpt in _db.LegacyProductTypes
            join map in _db.ProductTypeWorkflowMaps on lpt.Code equals map.LegacyProductTypeCode
            where lpt.RemovedAt == null
            select lpt.Code).CountAsync(cancellationToken);

    public void Add(LegacyProductType productType) => _db.LegacyProductTypes.Add(productType);

    private sealed record ProjectedRow(
        string Code,
        string KindName,
        string Name,
        DateTimeOffset? RemovedAt,
        Guid? MappedTemplateId);

    private sealed record TemplateRef(Guid Id, string Code, string Name);
}
