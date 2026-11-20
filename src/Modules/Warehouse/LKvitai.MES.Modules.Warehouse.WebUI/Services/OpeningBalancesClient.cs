using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class OpeningBalancesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const string Base = "/api/warehouse/v1/opening-balances";
    private readonly IHttpClientFactory _factory;

    public OpeningBalancesClient(IHttpClientFactory factory) => _factory = factory;

    public Task<OpeningBalancePreviewDto> PreviewAsync(string fileName, byte[] bytes, CancellationToken ct = default)
        => PostFileAsync<OpeningBalancePreviewDto>($"{Base}/preview", fileName, bytes, ct);

    public Task<OpeningBalanceConfirmDto> ConfirmAsync(string fileName, byte[] bytes, CancellationToken ct = default)
        => PostFileAsync<OpeningBalanceConfirmDto>($"{Base}/confirm", fileName, bytes, ct);

    public async Task<IReadOnlyList<OpeningBalanceDocumentDto>> ListAsync(CancellationToken ct = default)
    {
        var response = await _factory.CreateClient("WarehouseApi").GetAsync(Base, ct);
        return await ReadAsync<List<OpeningBalanceDocumentDto>>(response);
    }

    private async Task<T> PostFileAsync<T>(string url, string fileName, byte[] bytes, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", fileName);
        var response = await _factory.CreateClient("WarehouseApi").PostAsync(url, content, ct);
        return await ReadAsync<T>(response);
    }

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
