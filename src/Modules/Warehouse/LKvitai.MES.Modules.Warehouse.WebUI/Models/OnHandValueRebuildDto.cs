namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record OnHandValueAmountsDto(decimal Qty, decimal UnitCost, decimal TotalValue);
public sealed record OnHandValueChangeDto(string Sku, string Change, OnHandValueAmountsDto? Before, OnHandValueAmountsDto? After);
public sealed record OnHandValuePreviewDto(string ProductionChecksum, string ShadowChecksum, IReadOnlyList<OnHandValueChangeDto> Rows);
