using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class MonthEndActionException : Exception
{
    public IReadOnlyList<string> Blockers { get; }
    public MonthEndActionException(string message, IReadOnlyList<string> blockers) : base(message) => Blockers = blockers;
}

public sealed class MonthEndClient
{
    private readonly IHttpClientFactory _factory;

    public MonthEndClient(IHttpClientFactory factory) => _factory = factory;

    private static string Url(int year, int month, string tail) => $"/api/warehouse/v1/month-end/{year}/{month}/{tail}";

    public async Task<MonthEndReportDto> GetReportAsync(int year, int month, CancellationToken ct = default)
        => await GetAsync<MonthEndReportDto>(Url(year, month, "report"), ct);

    public async Task<PeriodStateDto> GetStateAsync(int year, int month, CancellationToken ct = default)
        => await GetAsync<PeriodStateDto>(Url(year, month, "state"), ct);

    public Task CloseAsync(int year, int month, CancellationToken ct = default) => PostAsync(Url(year, month, "close"), null, ct);

    public Task ReopenAsync(int year, int month, string reason, CancellationToken ct = default)
        => PostAsync(Url(year, month, "reopen"), new { reason }, ct);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        var response = await _factory.CreateClient("WarehouseApi").GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw new JsonException("Unable to read the month-end data.");
    }

    private async Task PostAsync(string url, object? body, CancellationToken ct)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = body is null ? await client.PostAsync(url, null, ct) : await client.PostAsJsonAsync(url, body, ct);
        if (response.IsSuccessStatusCode) return;
        if ((int)response.StatusCode == 409)
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var detail = doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            var blockers = doc.RootElement.TryGetProperty("blockers", out var b) && b.ValueKind == JsonValueKind.Array
                ? b.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                : new List<string>();
            throw new MonthEndActionException(detail, blockers);
        }

        throw new ApiException(await ProblemDetailsParser.ParseAsync(response), (int)response.StatusCode);
    }
}
