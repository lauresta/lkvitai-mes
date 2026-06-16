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
        var projected =
            from lpt in _db.LegacyProductTypes.AsNoTracking()
            join map in _db.ProductTypeWorkflowMaps.AsNoTracking()
                on lpt.Code equals map.LegacyProductTypeCode into maps
            from map in maps.DefaultIfEmpty()
            join tpl in _db.WorkflowTemplates.AsNoTracking()
                on map!.WorkflowTemplateId equals tpl.Id into templates
            from tpl in templates.DefaultIfEmpty()
            select new ProjectedRow(lpt, map, tpl);

        if (!query.Removed)
        {
            projected = projected.Where(x => x.Legacy.RemovedAt == null);
        }

        if (query.Mapped is { } mapped)
        {
            projected = mapped
                ? projected.Where(x => x.Map != null)
                : projected.Where(x => x.Map == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            projected = projected.Where(x =>
                EF.Functions.ILike(x.Legacy.Code, term) ||
                EF.Functions.ILike(x.Legacy.Name, term) ||
                EF.Functions.ILike(x.Legacy.KindName, term));
        }

        var total = await projected.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await projected
            .OrderBy(x => x.Legacy.Code)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(x => new LegacyProductTypeDto(
                x.Legacy.Code,
                x.Legacy.KindName,
                x.Legacy.Name,
                x.Legacy.RemovedAt != null,
                x.Map != null ? x.Map.WorkflowTemplateId : null,
                x.Template != null ? x.Template.Code : null,
                x.Template != null ? x.Template.Name : null))
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
        LegacyProductType Legacy,
        ProductTypeWorkflowMap? Map,
        WorkflowTemplate? Template);
}
