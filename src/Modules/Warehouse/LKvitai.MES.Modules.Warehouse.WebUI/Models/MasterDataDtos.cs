namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record PagedApiResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}

public record AdminItemDto
{
    public int Id { get; init; }
    public string InternalSKU { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string BaseUoM { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool RequiresLotTracking { get; init; }
    public bool RequiresQC { get; init; }
    public string? PrimaryBarcode { get; init; }
    public decimal? Weight { get; init; }
    public decimal? Volume { get; init; }
    public decimal? BasePrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public string? ProductConfigId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? PrimaryThumbnailUrl { get; init; }
    public Guid? PrimaryPhotoId { get; init; }
}

public record ItemPhotoDto
{
    public Guid Id { get; init; }
    public int ItemId { get; init; }
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsPrimary { get; init; }
    public string? Tags { get; init; }
    public string OriginalUrl { get; init; } = string.Empty;
    public string ThumbUrl { get; init; } = string.Empty;
}

public record ItemDetailsDto
{
    public int Id { get; init; }
    public string InternalSKU { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryId { get; init; }
    public string BaseUoM { get; init; } = string.Empty;
    public decimal? Weight { get; init; }
    public decimal? Volume { get; init; }
    public decimal? BasePrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public bool RequiresLotTracking { get; init; }
    public bool RequiresQC { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? PrimaryBarcode { get; init; }
    public string? ProductConfigId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? PrimaryThumbnailUrl { get; init; }
    public Guid? PrimaryPhotoId { get; init; }
    public IReadOnlyList<ItemPhotoDto> Photos { get; init; } = Array.Empty<ItemPhotoDto>();
}

public record ItemPhotosResponseDto
{
    public int ItemId { get; init; }
    public IReadOnlyList<ItemPhotoDto> Photos { get; init; } = Array.Empty<ItemPhotoDto>();
}

public record ImageSearchResultDto
{
    public int ItemId { get; init; }
    public string SKU { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? PrimaryThumbnailUrl { get; init; }
    public double Score { get; init; }
}

public record ImageSearchResponseDto
{
    public IReadOnlyList<ImageSearchResultDto> Results { get; init; } = Array.Empty<ImageSearchResultDto>();
}

public record CreateOrUpdateItemRequest
{
    public string? InternalSKU { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryId { get; init; }
    public string BaseUoM { get; init; } = string.Empty;
    public decimal? Weight { get; init; }
    public decimal? Volume { get; init; }
    public decimal? BasePrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public bool RequiresLotTracking { get; init; }
    public bool RequiresQC { get; init; }
    public string? Status { get; init; }
    public string? PrimaryBarcode { get; init; }
    public string? ProductConfigId { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

public record ItemTagsResponseDto
{
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

public record AdminSupplierDto
{
    public int Id { get; init; }
    public int? AgnumClientId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? CompanyCode { get; init; }
    public string? VatCode { get; init; }
    public string? RegisteredAddress { get; init; }
    public string? PickupAddress { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? AdditionalInfo { get; init; }
    public string? ContactInfo { get; init; }
    public DateTimeOffset? LastAgnumSyncedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    public bool IsAgnumLinked => AgnumClientId is > 0;
}

public record CreateOrUpdateSupplierRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? CompanyCode { get; init; }
    public string? VatCode { get; init; }
    public string? RegisteredAddress { get; init; }
    public string? PickupAddress { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? AdditionalInfo { get; init; }
    public string? ContactInfo { get; init; }
}

public record AdminSupplierMappingDto
{
    public int Id { get; init; }
    public int SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string SupplierSKU { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string ItemSKU { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int? LeadTimeDays { get; init; }
    public decimal? MinOrderQty { get; init; }
    public decimal? PricePerUnit { get; init; }
}

public record CreateOrUpdateSupplierMappingRequest(
    int SupplierId,
    string SupplierSKU,
    int ItemId,
    int? LeadTimeDays,
    decimal? MinOrderQty,
    decimal? PricePerUnit);

public record AdminLocationDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Barcode { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int? ParentLocationId { get; init; }
    public bool IsVirtual { get; init; }
    public decimal? MaxWeight { get; init; }
    public decimal? MaxVolume { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ZoneType { get; init; }
    public decimal? CoordinateX { get; init; }
    public decimal? CoordinateY { get; init; }
    public decimal? CoordinateZ { get; init; }
    public decimal? WidthMeters { get; init; }
    public decimal? LengthMeters { get; init; }
    public decimal? HeightMeters { get; init; }
    public string? Aisle { get; init; }
    public string? Rack { get; init; }
    public string? Level { get; init; }
    public string? Bin { get; init; }
    public decimal? CapacityWeight { get; init; }
    public decimal? CapacityVolume { get; init; }
    public string? RackRowId { get; init; }
    public int? ShelfLevelIndex { get; init; }
    public int? SlotStart { get; init; }
    public int? SlotSpan { get; init; }
    public string? LocationRole { get; init; }
}

public record CreateOrUpdateLocationRequest(
    string Code,
    string Barcode,
    string Type,
    int? ParentLocationId,
    bool IsVirtual,
    decimal? MaxWeight,
    decimal? MaxVolume,
    string? Status,
    string? ZoneType,
    decimal? CoordinateX,
    decimal? CoordinateY,
    decimal? CoordinateZ,
    decimal? WidthMeters,
    decimal? LengthMeters,
    decimal? HeightMeters,
    string? Aisle,
    string? Rack,
    string? Level,
    string? Bin,
    decimal? CapacityWeight,
    decimal? CapacityVolume);

public record UpdateLocationRackPlacementRequest(
    string WarehouseCode,
    string RackRowId,
    int ShelfLevelIndex,
    int SlotStart,
    int? SlotSpan,
    string? LocationRole);

public record AdminWarehouseDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsVirtual { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

public record CreateOrUpdateWarehouseRequest(
    string Code,
    string Name,
    string? Description,
    bool IsVirtual,
    string Status);

public record AdminCategoryDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int? ParentCategoryId { get; init; }
}

public record CreateOrUpdateCategoryRequest(string Code, string Name, int? ParentCategoryId);

public record PriceGroupDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public record CreateOrUpdatePriceGroupRequest(string Code, string Name, bool IsActive);

public record PriceGroupCustomerDto
{
    public Guid Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public record ItemPriceOverrideDto
{
    public int PriceGroupId { get; init; }
    public string PriceGroupCode { get; init; } = string.Empty;
    public string PriceGroupName { get; init; } = string.Empty;
    public decimal? OverrideAmount { get; init; }
    public decimal? BasePrice { get; init; }
    public decimal? EffectivePrice => OverrideAmount ?? BasePrice;
}

public record SetPriceOverrideRequest(decimal? Amount);

public record ItemPriceHistoryDto
{
    public long Id { get; init; }
    public int ItemId { get; init; }
    public string PriceType { get; init; } = string.Empty;
    public int? PriceGroupId { get; init; }
    public decimal? OldAmount { get; init; }
    public decimal NewAmount { get; init; }
    public string ChangedBy { get; init; } = string.Empty;
    public DateTimeOffset ChangedAt { get; init; }
    public string? Reason { get; init; }
}

public record CustomerLookupDto
{
    public Guid Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? PriceGroupId { get; init; }
    public string? PriceGroupName { get; init; }
}

public record SetCustomerPriceGroupRequest(int? PriceGroupId);

public record CustomerDetailsDto
{
    public Guid Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Status { get; init; } = string.Empty;
    public string PaymentTerms { get; init; } = string.Empty;
    public decimal? CreditLimit { get; init; }
    public int? PriceGroupId { get; init; }
    public string? PriceGroupName { get; init; }
    public SalesOrderAddressDto BillingAddress { get; init; } = new();
    public SalesOrderAddressDto? DefaultShippingAddress { get; init; }
}

public record CreateOrUpdateCustomerRequest
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public SalesOrderAddressDto? BillingAddress { get; init; }
    public SalesOrderAddressDto? ShippingAddress { get; init; }
    public string? Status { get; init; }
    public string? PaymentTerms { get; init; }
    public decimal? CreditLimit { get; init; }
    public int? PriceGroupId { get; init; }
}

public record PriceGroupItemPriceDto(int ItemId, decimal Amount);

public record ImportExecutionResultDto
{
    public int Inserted { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public bool DryRun { get; init; }
    public bool UsedBulk { get; init; }
    public IReadOnlyList<ImportErrorDto> Errors { get; init; } = Array.Empty<ImportErrorDto>();
    public string? Duration { get; init; }
}

public record ImportErrorDto
{
    public int Row { get; init; }
    public string Column { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string Message { get; init; } = string.Empty;
}
