using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class AdvancedWarehouseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<AdvancedWarehouseClient>? _logger;

    public AdvancedWarehouseClient(IHttpClientFactory factory, ILogger<AdvancedWarehouseClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<WaveDto>> GetWavesAsync(string? status = null)
        => GetAsync<IReadOnlyList<WaveDto>>($"/api/warehouse/v1/waves{BuildQuery(("status", status))}");

    public Task<WaveDto> CreateWaveAsync(WaveCreateRequestDto request)
        => PostAsync<WaveDto>("/api/warehouse/v1/waves", request);

    public Task<WaveDto> AssignWaveAsync(Guid id, string assignedOperator)
        => PostAsync<WaveDto>($"/api/warehouse/v1/waves/{id}/assign", new AssignWaveRequestDto(assignedOperator));

    public Task<WaveDto> StartWaveAsync(Guid id)
        => PostAsync<WaveDto>($"/api/warehouse/v1/waves/{id}/start", new { });

    public Task<WaveDto> CompleteWaveLinesAsync(Guid id, int lines)
        => PostAsync<WaveDto>($"/api/warehouse/v1/waves/{id}/complete-lines", new CompleteWaveLinesRequestDto(lines));

    public Task<IReadOnlyList<CrossDockDto>> GetCrossDocksAsync()
        => GetAsync<IReadOnlyList<CrossDockDto>>("/api/warehouse/v1/cross-dock");

    public Task<CrossDockDto> CreateCrossDockAsync(CrossDockCreateRequestDto request)
        => PostAsync<CrossDockDto>("/api/warehouse/v1/cross-dock", request);

    public Task<CrossDockDto> UpdateCrossDockStatusAsync(Guid id, string status)
        => PostAsync<CrossDockDto>($"/api/warehouse/v1/cross-dock/{id}/status", new CrossDockStatusUpdateRequestDto(status));

    public Task<IReadOnlyList<RmaDto>> GetRmasAsync()
        => GetAsync<IReadOnlyList<RmaDto>>("/api/warehouse/v1/rmas");

    public Task<RmaDto> CreateRmaAsync(RmaCreateDto request)
        => PostAsync<RmaDto>("/api/warehouse/v1/rmas", request);

    public Task<RmaDto> ReceiveRmaAsync(Guid id)
        => PostAsync<RmaDto>($"/api/warehouse/v1/rmas/{id}/receive", new { });

    public Task<RmaDto> InspectRmaAsync(Guid id, string disposition, decimal? creditAmount)
        => PostAsync<RmaDto>($"/api/warehouse/v1/rmas/{id}/inspect", new InspectRmaRequestDto(disposition, creditAmount));

    public Task<FulfillmentKpiDto> GetFulfillmentKpisAsync()
        => GetAsync<FulfillmentKpiDto>("/api/warehouse/v1/analytics/fulfillment-kpis");

    public Task<QcLateShipmentAnalyticsDto> GetQcLateShipmentAnalyticsAsync()
        => GetAsync<QcLateShipmentAnalyticsDto>("/api/warehouse/v1/analytics/qc-late-shipments");

    public async Task<QcDefectDto> UploadQcAttachmentAsync(Guid defectId, string fileName, string contentType, byte[] content)
    {
        using var payload = new MultipartFormDataContent();
        using var stream = new MemoryStream(content);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        payload.Add(fileContent, "file", fileName);

        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsync($"/api/warehouse/v1/advanced/qc/defects/{defectId}/attachments", payload);
        return await DeserializeResponse<QcDefectDto>(response);
    }

    private Task<T> GetAsync<T>(string relativeUrl) => SendAndReadAsync<T>(() =>
    {
        var client = _factory.CreateClient("WarehouseApi");
        return client.GetAsync(relativeUrl);
    });

    private Task<T> PostAsync<T>(string relativeUrl, object payload) => SendAndReadAsync<T>(() =>
    {
        var client = _factory.CreateClient("WarehouseApi");
        return client.PostAsJsonAsync(relativeUrl, payload);
    });

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        return await DeserializeResponse<T>(response);
    }

    private async Task<T> DeserializeResponse<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogWarning(
                "Advanced warehouse API failed. Status={Status} Code={Code} TraceId={TraceId} Detail={Detail}",
                (int)response.StatusCode,
                problem?.ErrorCode,
                problem?.TraceId,
                problem?.Detail);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private static string BuildQuery(params (string Name, string? Value)[] pairs)
    {
        var included = pairs
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Name)}={Uri.EscapeDataString(x.Value!)}")
            .ToArray();

        return included.Length == 0 ? string.Empty : $"?{string.Join("&", included)}";
    }
}
