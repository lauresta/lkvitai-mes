namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record UnitOfMeasureDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}

public sealed record HandlingUnitTypeDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed record ItemUomConversionDto
{
    public int Id { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string FromUom { get; init; } = string.Empty;
    public string ToUom { get; init; } = string.Empty;
    public decimal Factor { get; init; }
    public string RoundingRule { get; init; } = string.Empty;
}
