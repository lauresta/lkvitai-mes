using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Ports;

public interface IProductTypeMappingRepository
{
    Task<ProductTypeWorkflowMap?> GetAsync(string legacyProductTypeCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductTypeWorkflowMap>> GetManyAsync(
        IReadOnlyCollection<string> legacyProductTypeCodes,
        CancellationToken cancellationToken);

    Task<int> CountForTemplateAsync(Guid workflowTemplateId, CancellationToken cancellationToken);

    Task<int> CountDistinctMappedTemplatesAsync(CancellationToken cancellationToken);

    void Add(ProductTypeWorkflowMap map);

    void Remove(ProductTypeWorkflowMap map);
}
