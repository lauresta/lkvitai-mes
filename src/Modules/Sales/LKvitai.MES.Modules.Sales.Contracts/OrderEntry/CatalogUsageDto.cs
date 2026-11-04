namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

public sealed record CategoryUsageDto(int CategoryId, int Uses);
public sealed record TypeUsageDto(int TypeId, int Uses);
public sealed record CatalogUsageDto(IReadOnlyList<CategoryUsageDto> Categories, IReadOnlyList<TypeUsageDto> Types);
