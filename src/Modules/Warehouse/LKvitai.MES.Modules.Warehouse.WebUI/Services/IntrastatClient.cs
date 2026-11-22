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

    private HttpClient Http => _factory.CreateClient("WarehouseApi");

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
    }

    public async Task<IntrastatDeclarantDto> GetDeclarantAsync(CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"{Base}/declarant", ct);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<IntrastatDeclarantDto>(cancellationToken: ct) ?? new IntrastatDeclarantDto();
    }

    public async Task SaveDeclarantAsync(IntrastatDeclarantDto declarant, CancellationToken ct = default)
        => await EnsureSuccessAsync(await Http.PutAsJsonAsync($"{Base}/declarant", declarant, ct));

    /// <summary>Generates the XML of the month (a new version when the content changed) and returns the file.</summary>
    public async Task<byte[]> GenerateXmlAsync(int year, int month, CancellationToken ct = default)
    {
        var response = await Http.PostAsync($"{Base}/arrivals.xml?year={year}&month={month}", null, ct);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<IReadOnlyList<IntrastatXmlVersionDto>> GetXmlVersionsAsync(int year, int month, CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"{Base}/xml-versions?year={year}&month={month}", ct);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<IntrastatXmlVersionDto>>(cancellationToken: ct) ?? [];
    }

    public async Task<byte[]> GetXmlVersionFileAsync(int id, CancellationToken ct = default)
    {
        var response = await Http.GetAsync($"{Base}/xml-versions/{id}/file", ct);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task MarkXmlFiledAsync(int id, CancellationToken ct = default)
        => await EnsureSuccessAsync(await Http.PostAsync($"{Base}/xml-versions/{id}/filed", null, ct));
}
