using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

public sealed record AgnumSchemeRow(int No, string? Name, string? Debit, string? Credit);

public sealed record AgnumScheme(int IdSch, string? Name, IReadOnlyList<AgnumSchemeRow>? Rows);

public sealed record AgnumWarehouseRef(int IdSnd, string? Code, string? Name);

public sealed record AgnumAccountRef(string Code, string? Name);

public sealed record AgnumVatCodeRef(string Code);

// Wire shapes of agnum-api-deploy docs/PURCHASE_POSTING_SPEC.md section 8.
internal sealed record WireSchemeRow(
    [property: JsonPropertyName("nr")] int Nr, [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("debit")] string? Debit, [property: JsonPropertyName("credit")] string? Credit);

internal sealed record WireScheme(
    [property: JsonPropertyName("id")] int Id, [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("rows")] List<WireSchemeRow>? Rows);

internal sealed record WireWarehouse(
    [property: JsonPropertyName("id")] int Id, [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record WireAccount([property: JsonPropertyName("id")] long Id, [property: JsonPropertyName("account")] string? Account);

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
        => MapAsync<WireScheme, AgnumScheme>("api/reference/schemes", s => new AgnumScheme(s.Id, s.Name, (s.Rows ?? new()).Select(r => new AgnumSchemeRow(r.Nr, r.Name, r.Debit, r.Credit)).ToList()), ct);

    public Task<IReadOnlyList<AgnumWarehouseRef>> GetWarehousesAsync(CancellationToken ct = default)
        => MapAsync<WireWarehouse, AgnumWarehouseRef>("api/reference/warehouses", w => new AgnumWarehouseRef(w.Id, w.Code, w.Name), ct);

    public Task<IReadOnlyList<AgnumAccountRef>> GetAccountsAsync(CancellationToken ct = default)
        => MapAsync<WireAccount, AgnumAccountRef>("api/reference/accounts", a => new AgnumAccountRef(a.Account ?? string.Empty, null), ct);

    public Task<IReadOnlyList<AgnumVatCodeRef>> GetVatCodesAsync(CancellationToken ct = default)
        => MapAsync<string, AgnumVatCodeRef>("api/reference/vat-codes", v => new AgnumVatCodeRef(v ?? string.Empty), ct);

    public Task<IReadOnlyList<AgnumClientDto>> GetClientsAsync(CancellationToken ct = default)
        => GetListAsync<AgnumClientDto>(ClientsQuery, ct);

    private async Task<IReadOnlyList<TOut>> MapAsync<TWire, TOut>(string uri, Func<TWire, TOut> map, CancellationToken ct)
        => (await GetListAsync<TWire>(uri, ct)).Select(map).ToList();

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
