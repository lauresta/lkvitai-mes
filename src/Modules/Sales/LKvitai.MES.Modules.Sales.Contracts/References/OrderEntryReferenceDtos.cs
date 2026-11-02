namespace LKvitai.MES.Modules.Sales.Contracts.References;

/// <summary>Legacy IDs are opaque signed integers; negative IDs are valid.</summary>
public sealed record OrderSeriesReferenceDto(
    int Id,
    string? Code,
    int? BranchId,
    int? DefaultCustomerId,
    int? DefaultStoreId,
    int? DefaultManagerId,
    int? DefaultConsultantId,
    bool Hidden,
    bool IsTestWriteScope);

public sealed record ProductCategoryReferenceDto(
    int Id,
    string? Name,
    bool IsActive,
    bool? DiscountAllowed);

public sealed record ProductTypeReferenceDto(
    int Id,
    int? CategoryId,
    string? Name,
    bool IsActive,
    bool? IsManufactured);

public enum ProductParameterStorageKind
{
    Text,
    Decimal,
}

/// <summary>
/// Describes a legacy slot. A formula is reference text, never permission to
/// execute it in the browser or a general-purpose expression evaluator.
/// LegacyDefinitionId is the unmodified PPxx reference, whose business
/// semantics must be traced before enabling edits.
/// </summary>
public sealed record ProductParameterDefinitionDto(
    int Slot,
    string? Label,
    string? Formula,
    int? LegacyDefinitionId,
    ProductParameterStorageKind StorageKind);

public sealed record ProductTypeDefinitionDto(
    ProductTypeReferenceDto Type,
    string? CategoryName,
    bool CategoryIsActive,
    IReadOnlyList<ProductParameterDefinitionDto> Parameters,
    ProductPricingDefinitionDto Pricing);

/// <summary>
/// Pricing minima are chargeable dimensions/area, not technical minimum product
/// dimensions. Type-level manual entry support grants no permission to edit an
/// existing line: its current state and employee capabilities still apply.
/// </summary>
public sealed record ProductPricingDefinitionDto(
    string PriceEntryMode,
    int? UnitId,
    decimal? MinimumWidthM,
    decimal? MinimumHeightM,
    decimal? MinimumAreaM2,
    bool SupportsManualUnitPriceForType);
