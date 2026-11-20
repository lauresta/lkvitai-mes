using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

public sealed record AgnumSchemeRow(
    [property: JsonPropertyName("no")] int No,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("debit")] string? Debit,
    [property: JsonPropertyName("credit")] string? Credit);

public sealed record AgnumScheme(
    [property: JsonPropertyName("id_sch")] int IdSch,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("rows")] IReadOnlyList<AgnumSchemeRow>? Rows);

public sealed record AgnumWarehouseRef(
    [property: JsonPropertyName("id_snd")] int IdSnd,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("name")] string? Name);

public sealed record AgnumAccountRef(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string? Name);

public sealed record AgnumVatCodeRef([property: JsonPropertyName("pvm_kod")] string Code);

/// <summary>Read routes of the Agnum service (blueprint 5.2) plus the clients list used by the supplier sync.</summary>
public interface IAgnumReferenceClient
{
    Task<IReadOnlyList<AgnumScheme>> GetSchemesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgnumWarehouseRef>> GetWarehousesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgnumAccountRef>> GetAccountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgnumVatCodeRef>> GetVatCodesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgnumClientDto>> GetClientsAsync(CancellationToken ct = default);
}

public sealed class AgnumReferenceClient : IAgnumReferenceClient
{
    public const string HttpClientName = "AgnumReference";
    private const string ClientsQuery = "api/clients/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private readonly HttpClient _http;

    public AgnumReferenceClient(HttpClient http) => _http = http;

    public Task<IReadOnlyList<AgnumScheme>> GetSchemesAsync(CancellationToken ct = default)
        => GetListAsync<AgnumScheme>("api/reference/schemes", ct);

    public Task<IReadOnlyList<AgnumWarehouseRef>> GetWarehousesAsync(CancellationToken ct = default)
        => GetListAsync<AgnumWarehouseRef>("api/reference/warehouses", ct);

    public Task<IReadOnlyList<AgnumAccountRef>> GetAccountsAsync(CancellationToken ct = default)
        => GetListAsync<AgnumAccountRef>("api/reference/accounts", ct);

    public Task<IReadOnlyList<AgnumVatCodeRef>> GetVatCodesAsync(CancellationToken ct = default)
        => GetListAsync<AgnumVatCodeRef>("api/reference/vat-codes", ct);

    public Task<IReadOnlyList<AgnumClientDto>> GetClientsAsync(CancellationToken ct = default)
        => GetListAsync<AgnumClientDto>(ClientsQuery, ct);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string uri, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
                throw new AgnumApiUnavailableException($"Agnum API GET {uri} returned HTTP {(int)response.StatusCode}.");

            var list = await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, ct);
            return list ?? new List<T>();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new AgnumApiUnavailableException($"Agnum API GET {uri} failed.", ex);
        }
    }
}
