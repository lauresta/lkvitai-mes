using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.References;

namespace LKvitai.MES.Modules.Sales.Application.Ports;

/// <summary>
/// Live order-entry references for authenticated employees. All-branch reads
/// follow the agreed Sales policy. No method grants write capability or runs
/// legacy calculation/check procedures. Hidden/inactive entries can be read
/// explicitly to retain historical references when reopening an order.
/// </summary>
public interface ISalesOrderEntryReferences
{
    Task<PagedResult<OrderSeriesReferenceDto>> GetSeriesAsync(
        string? search, int page, int pageSize, bool includeHidden,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductCategoryReferenceDto>> GetCategoriesAsync(
        bool includeInactive, CancellationToken cancellationToken);

    Task<PagedResult<ProductTypeReferenceDto>> GetTypesAsync(
        int? categoryId, string? search, int page, int pageSize, bool includeInactive,
        CancellationToken cancellationToken);

    Task<ProductTypeDefinitionDto?> GetTypeDefinitionAsync(
        int typeId, CancellationToken cancellationToken);
}
