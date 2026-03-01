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
    public bool RequiresLotTracking { get; init; }
    public bool RequiresQC { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? PrimaryBarcode { get; init; }
    public string? ProductConfigId { get; init; }
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

public record CreateOrUpdateItemRequest
{
    public string? InternalSKU { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int CategoryId { get; init; }
    public string BaseUoM { get; init; } = string.Empty;
    public decimal? Weight { get; init; }
    public decimal? Volume { get; init; }
    public bool RequiresLotTracking { get; init; }
    public bool RequiresQC { get; init; }
    public string? Status { get; init; }
    public string? PrimaryBarcode { get; init; }
    public string? ProductConfigId { get; init; }
}

public record AdminSupplierDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ContactInfo { get; init; }
}

public record CreateOrUpdateSupplierRequest(string Code, string Name, string? ContactInfo);

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
