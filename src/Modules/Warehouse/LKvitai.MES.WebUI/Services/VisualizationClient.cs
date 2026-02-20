using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public class VisualizationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<VisualizationClient>? _logger;

    public VisualizationClient(IHttpClientFactory factory, ILogger<VisualizationClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<Visualization3dDto> GetVisualization3dAsync(string warehouseCode = "Main")
    {
        var encoded = Uri.EscapeDataString(warehouseCode);
        return GetAsync<Visualization3dDto>($"/api/warehouse/v1/visualization/3d?warehouseCode={encoded}");
    }

    private async Task<T> GetAsync<T>(string relativeUrl)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogError(
                "Warehouse API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
                response.RequestMessage?.Method.Method ?? "UNKNOWN",
                response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
                (int)response.StatusCode,
                problem?.ErrorCode ?? "UNKNOWN",
                problem?.TraceId ?? "UNKNOWN",
                problem?.Detail ?? "n/a");
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }
}
