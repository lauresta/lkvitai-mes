using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Repositories;

public sealed class ProductTypeMappingRepository : IProductTypeMappingRepository
{
    private readonly ShopfloorDbContext _db;

    public ProductTypeMappingRepository(ShopfloorDbContext db) => _db = db;

    public Task<ProductTypeWorkflowMap?> GetAsync(string legacyProductTypeCode, CancellationToken cancellationToken)
    {
        var code = legacyProductTypeCode.Trim();
        return _db.ProductTypeWorkflowMaps.FirstOrDefaultAsync(x => x.LegacyProductTypeCode == code, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductTypeWorkflowMap>> GetManyAsync(
        IReadOnlyCollection<string> legacyProductTypeCodes,
        CancellationToken cancellationToken)
    {
        if (legacyProductTypeCodes.Count == 0)
        {
            return Array.Empty<ProductTypeWorkflowMap>();
        }

        return await _db.ProductTypeWorkflowMaps
            .Where(x => legacyProductTypeCodes.Contains(x.LegacyProductTypeCode))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountForTemplateAsync(Guid workflowTemplateId, CancellationToken cancellationToken)
        => _db.ProductTypeWorkflowMaps.CountAsync(x => x.WorkflowTemplateId == workflowTemplateId, cancellationToken);

    public Task<int> CountDistinctMappedTemplatesAsync(CancellationToken cancellationToken)
        => _db.ProductTypeWorkflowMaps
            .Select(x => x.WorkflowTemplateId)
            .Distinct()
            .CountAsync(cancellationToken);

    public void Add(ProductTypeWorkflowMap map) => _db.ProductTypeWorkflowMaps.Add(map);

    public void Remove(ProductTypeWorkflowMap map) => _db.ProductTypeWorkflowMaps.Remove(map);
}
