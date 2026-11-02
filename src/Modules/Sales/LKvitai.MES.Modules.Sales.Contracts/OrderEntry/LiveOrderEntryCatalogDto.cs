namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

public sealed record LiveNamedReferenceDto(int Id, string Name);
public sealed record LiveSeriesDto(int Id, string Code, int? BranchId,
    int? CustomerId, string? CustomerName, int? StoreId, string? StoreName,
    int? ManagerId, string? ManagerName, int? ConsultantId, string? ConsultantName);
public sealed record LiveCategoryDto(int Id, string Name, bool IsActive, bool DiscountAllowed,
    string PriceEntryMode, IReadOnlyList<string?> ParameterLabels);
public sealed record LiveTypeDto(int Id, int CategoryId, string Name, bool IsActive, bool IsManufactured,
    int? UnitId, decimal? MinWidthM, decimal? MinHeightM, decimal? MinAreaM2,
    string PriceEntryMode, bool CanEnterManualPrice, IReadOnlyList<string?> ParameterFormulas,
    string? UnsupportedReason = null);
/// <summary>Access sp_cboSpalva quantities from view_Remains. Reserved is already negative;
/// free = warehouse + production + reserved. These are catalog snapshots, rechecked on save.</summary>
public sealed record LiveColourDto(int Id, string Code, string Name, IReadOnlyList<int> CategoryIds,
    decimal? FreeQuantity = null, decimal? WarehouseQuantity = null, decimal? ProductionQuantity = null,
    decimal? TotalQuantity = null, decimal? ReservedQuantity = null, string? Unit = null);
public sealed record LiveAccessoryDto(int Id, string Name, int? UnitId, string Unit, decimal? Price,
    IReadOnlyList<int> CategoryIds);
public sealed record LiveOrderEntryCatalogDto(DateTimeOffset LoadedAtUtc,
    IReadOnlyList<LiveSeriesDto> Series, IReadOnlyList<LiveCategoryDto> Categories,
    IReadOnlyList<LiveTypeDto> Types, IReadOnlyList<LiveColourDto> Colours,
    IReadOnlyList<LiveAccessoryDto> Accessories, IReadOnlyList<LiveNamedReferenceDto> Customers,
    IReadOnlyList<LiveNamedReferenceDto> Employees, IReadOnlyList<LiveNamedReferenceDto> Stores,
    IReadOnlyList<LiveNamedReferenceDto> Duties, IReadOnlyList<LiveNamedReferenceDto> Priorities);
