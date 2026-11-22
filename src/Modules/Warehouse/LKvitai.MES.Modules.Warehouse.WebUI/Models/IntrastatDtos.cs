namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record IntrastatRowDto
{
    public string CommodityCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string OriginCountry { get; init; } = string.Empty;
    public string Nature { get; init; } = string.Empty;
    public string DeliveryTerms { get; init; } = string.Empty;
    public string TransportMode { get; init; } = string.Empty;
    public string? SupplementaryUnit { get; init; }
    public bool IsReturn { get; init; }
    public decimal ValueEur { get; init; }
    public decimal NetMassKg { get; init; }
    public decimal Quantity { get; init; }
    public decimal? SupplementaryQuantity { get; init; }
    public decimal? LandedValueEur { get; init; }
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
    public decimal TotalReturnValueEur { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
