namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record AgnumMappingDto
{
    public Guid? Id { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public string SourceValue { get; init; } = string.Empty;
    public string AgnumAccountCode { get; init; } = string.Empty;
}

public sealed record AgnumConfigDto
{
    public Guid Id { get; init; }
    public string Scope { get; init; } = "BY_CATEGORY";
    public string Schedule { get; init; } = "0 23 * * *";
    public string Format { get; init; } = "CSV";
    public string? ApiEndpoint { get; init; }
    public bool ApiKeyConfigured { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<AgnumMappingDto> Mappings { get; init; } = Array.Empty<AgnumMappingDto>();
}

public sealed record PutAgnumConfigRequestDto
{
    public Guid? ConfigId { get; init; }
    public string Scope { get; init; } = "BY_CATEGORY";
    public string Schedule { get; init; } = "0 23 * * *";
    public string Format { get; init; } = "CSV";
    public string? ApiEndpoint { get; init; }
    public string? ApiKey { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<PutAgnumMappingRequestDto> Mappings { get; init; } = Array.Empty<PutAgnumMappingRequestDto>();
}

public sealed record PutAgnumMappingRequestDto
{
    public string SourceType { get; init; } = string.Empty;
    public string SourceValue { get; init; } = string.Empty;
    public string AgnumAccountCode { get; init; } = string.Empty;
}

public sealed record AgnumConfigSavedResponseDto
{
    public Guid Id { get; init; }
    public string Schedule { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public int MappingCount { get; init; }
}

public sealed record TestAgnumConnectionRequestDto
{
    public string? ApiEndpoint { get; init; }
    public string? ApiKey { get; init; }
}

public sealed record TestAgnumConnectionResponseDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record AgnumReconciliationLineDto
{
    public string AccountCode { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal WarehouseQty { get; init; }
    public decimal WarehouseCost { get; init; }
    public decimal WarehouseValue { get; init; }
    public decimal AgnumBalance { get; init; }
    public decimal Variance { get; init; }
    public decimal VariancePercent { get; init; }
}

public sealed record AgnumReconciliationSummaryDto
{
    public decimal TotalVariance { get; init; }
    public int ItemsWithVariance { get; init; }
    public string? LargestVarianceSku { get; init; }
    public decimal LargestVarianceAmount { get; init; }
}

public sealed record AgnumReconciliationReportDto
{
    public Guid ReportId { get; init; }
    public string Date { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<AgnumReconciliationLineDto> Lines { get; init; } = Array.Empty<AgnumReconciliationLineDto>();
    public AgnumReconciliationSummaryDto Summary { get; init; } = new();
}

public sealed record AgnumExportHistoryDto
{
    public Guid Id { get; init; }
    public Guid ExportConfigId { get; init; }
    public string ExportNumber { get; init; } = string.Empty;
    public DateTimeOffset ExportedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public string Trigger { get; init; } = string.Empty;
}

public sealed record AgnumVirtualWarehouseDto
{
    public int SndId { get; init; }
    public string AgnumName { get; init; } = string.Empty;
    public string MesVirtualWarehouseCode { get; init; } = string.Empty;
    public string ApiKeyConfigName { get; init; } = string.Empty;
    public bool IsImportEnabled { get; init; }
}

public sealed record AgnumBalanceRowDto
{
    public Guid Id { get; init; }
    public int AgnumProductId { get; init; }
    public string? Sku { get; init; }
    public decimal Quantity { get; init; }
    public decimal DistributedQty { get; init; }
    public decimal RemainingQty { get; init; }
    public string Uom { get; init; } = string.Empty;
    public int? ItemId { get; init; }
    public DateTime ImportedAt { get; init; }
}

public sealed record AgnumBalancesResponseDto
{
    public int SndId { get; init; }
    public Guid? RunId { get; init; }
    public DateTime? ImportedAt { get; init; }
    public IReadOnlyList<AgnumBalanceRowDto> Balances { get; init; } = Array.Empty<AgnumBalanceRowDto>();
}

public sealed record DistributeAgnumBalanceRequestDto(
    string LocationCode,
    string WarehouseId,
    decimal Quantity,
    Guid OperatorId);

public sealed record AgnumBalanceDistributionDto
{
    public Guid Id { get; init; }
    public Guid VirtualBalanceId { get; init; }
    public int SndId { get; init; }
    public int AgnumProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string LocationCode { get; init; } = string.Empty;
    public string WarehouseId { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public Guid StockMovementCommandId { get; init; }
    public DateTime DistributedAt { get; init; }
    public string DistributedBy { get; init; } = string.Empty;
}
