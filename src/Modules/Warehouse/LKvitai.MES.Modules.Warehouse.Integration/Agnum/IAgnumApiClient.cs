using System.Text.Json.Serialization;

namespace LKvitai.MES.Modules.Warehouse.Integration.Agnum;

public interface IAgnumApiClient
{
    Task<IReadOnlyList<AgnumProductDto>> GetProductsAsync(CancellationToken ct = default);
}

public sealed class AgnumProductDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string Pcs { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public decimal? Netto { get; init; }
    public decimal? Brutto { get; init; }
    public string? Barcode { get; init; }

    [JsonPropertyName("barcodes")]
    public List<string>? Barcodes { get; set; }

    [JsonPropertyName("modify_date")]
    public DateTime? ModifyDate { get; init; }

    [JsonPropertyName("create_date")]
    public DateTime? CreateDate { get; init; }

    public string? Group { get; init; }
    public string? Category { get; init; }
    public string? Subgroup { get; init; }
    public string? Direction { get; init; }
    public string? Branch { get; init; }
    public string? Place { get; init; }
    public string? F1 { get; init; }
    public string? F2 { get; init; }
}
