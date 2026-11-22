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
    public bool IsZeroReport { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public class IntrastatDeclarantDto
{
    public string? VatCode { get; set; }
    public string? Title { get; set; }
    public string? Street { get; set; }
    public string? ZipCode { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public bool IsComplete => !string.IsNullOrWhiteSpace(VatCode) && !string.IsNullOrWhiteSpace(Title);
}

public record IntrastatXmlVersionDto
{
    public int Id { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public int Version { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public string GeneratedBy { get; init; } = string.Empty;
    public bool IsZeroReport { get; init; }
    public int RowCount { get; init; }
    public decimal TotalValueEur { get; init; }
    public decimal TotalNetMassKg { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public DateTimeOffset? FiledAt { get; init; }
    public string? FiledBy { get; init; }
}
