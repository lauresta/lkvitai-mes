namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record ConsolidationRowDto(string Warehouse, string Location, string Sku, decimal Quantity, string Status, string? Reason);
public sealed record ConsolidationPreviewDto(string TargetWarehouse, IReadOnlyList<ConsolidationRowDto> Rows);
public sealed record ConsolidationResultDto(string TargetWarehouse, IReadOnlyList<ConsolidationRowDto> Moved, int SkippedRows);
