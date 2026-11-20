using System.Net.Http.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class IntrastatClient
{
    private const string Base = "/api/warehouse/v1/intrastat";
    private readonly IHttpClientFactory _factory;

    public IntrastatClient(IHttpClientFactory factory) => _factory = factory;

    public async Task<IntrastatReportDto> GetAsync(int year, int month, CancellationToken ct = default)
    {
        var response = await _factory.CreateClient("WarehouseApi").GetAsync($"{Base}/arrivals?year={year}&month={month}", ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
        return await response.Content.ReadFromJsonAsync<IntrastatReportDto>(cancellationToken: ct)
               ?? throw new System.Text.Json.JsonException("Unable to read Intrastat report.");
    }

    public async Task<byte[]> GetCsvAsync(int year, int month, CancellationToken ct = default)
    {
        var response = await _factory.CreateClient("WarehouseApi").GetAsync($"{Base}/arrivals.csv?year={year}&month={month}", ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
