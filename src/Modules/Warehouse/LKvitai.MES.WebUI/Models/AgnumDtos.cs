namespace LKvitai.MES.WebUI.Models;

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
