using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class SupplierCreditsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Base = "/api/warehouse/v1/supplier-credits";
    private readonly IHttpClientFactory _factory;

    public SupplierCreditsClient(IHttpClientFactory factory) => _factory = factory;

    private HttpClient Http => _factory.CreateClient("WarehouseApi");

    public async Task<SupplierCreditPrefillDto> GetPrefillAsync(int shipmentId, CancellationToken ct = default)
        => await ReadAsync<SupplierCreditPrefillDto>(await Http.GetAsync($"{Base}/prefill/{shipmentId}", ct));

    public async Task<IReadOnlyList<SupplierCreditDto>> ListAsync(int originalShipmentId, CancellationToken ct = default)
        => await ReadAsync<List<SupplierCreditDto>>(await Http.GetAsync($"{Base}?originalShipmentId={originalShipmentId}", ct));

    public async Task<SupplierCreditDto> GetAsync(int id, CancellationToken ct = default)
        => await ReadAsync<SupplierCreditDto>(await Http.GetAsync($"{Base}/{id}", ct));

    public async Task<SupplierCreditDto> CreateAsync(SupplierCreditRequestDto request, CancellationToken ct = default)
        => await ReadAsync<SupplierCreditDto>(await Http.PostAsJsonAsync(Base, request, ct));

    public async Task<SupplierCreditDto> PostAsync(int id, CancellationToken ct = default)
        => await ReadAsync<SupplierCreditDto>(await Http.PostAsync($"{Base}/{id}/post", null, ct));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(body, JsonOptions) ?? throw new JsonException($"Unable to read {typeof(T).Name}.");
    }
}
