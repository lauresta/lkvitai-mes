namespace LKvitai.MES.WebUI.Models;

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
    string? ZoneType);

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
