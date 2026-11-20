namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record IntrastatRowDto
{
    public string CommodityCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public decimal ValueEur { get; init; }
    public decimal NetMassKg { get; init; }
    public decimal Quantity { get; init; }
    public int Lines { get; init; }
}

public record IntrastatReportDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public IReadOnlyList<IntrastatRowDto> Rows { get; init; } = [];
    public decimal TotalValueEur { get; init; }
    public decimal TotalNetMassKg { get; init; }
    public decimal TotalQuantity { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
