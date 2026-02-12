using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public sealed class ReceivingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ReceivingClient>? _logger;

    public ReceivingClient(IHttpClientFactory factory, ILogger<ReceivingClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<PagedApiResponse<InboundShipmentSummaryDto>> GetShipmentsAsync(
        int? supplierId,
        string? status,
        DateOnly? expectedDateFrom,
        DateOnly? expectedDateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("supplierId", supplierId?.ToString()),
            ("status", status),
            ("expectedDateFrom", expectedDateFrom?.ToString("yyyy-MM-dd")),
            ("expectedDateTo", expectedDateTo?.ToString("yyyy-MM-dd")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<PagedApiResponse<InboundShipmentSummaryDto>>($"/api/warehouse/v1/inbound-shipments{query}", cancellationToken);
    }

    public Task<InboundShipmentDetailDto> GetShipmentByIdAsync(int id, CancellationToken cancellationToken = default)
        => GetAsync<InboundShipmentDetailDto>($"/api/warehouse/v1/inbound-shipments/{id}", cancellationToken);

    public Task<ShipmentCreatedResponseDto> CreateShipmentAsync(CreateInboundShipmentRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<ShipmentCreatedResponseDto>("/api/warehouse/v1/inbound-shipments", request, cancellationToken);

    public Task<ReceiveGoodsResponseDto> ReceiveItemsAsync(int shipmentId, ReceiveShipmentLineRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<ReceiveGoodsResponseDto>($"/api/warehouse/v1/inbound-shipments/{shipmentId}/receive-items", request, cancellationToken);

    public Task<IReadOnlyList<QcPendingRowDto>> GetPendingQcAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<QcPendingRowDto>>("/api/warehouse/v1/qc/pending", cancellationToken);

    public Task<QcActionResponseDto> PassQcAsync(QcActionRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<QcActionResponseDto>("/api/warehouse/v1/qc/pass", request, cancellationToken);

    public Task<QcActionResponseDto> FailQcAsync(QcActionRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<QcActionResponseDto>("/api/warehouse/v1/qc/fail", request, cancellationToken);

    private Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.GetAsync(relativeUrl, cancellationToken);
        });

    private Task<T> PostAsync<T>(string relativeUrl, object payload, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.PostAsJsonAsync(relativeUrl, payload, cancellationToken);
        });

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogError(
                "Receiving API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
                response.RequestMessage?.Method.Method ?? "UNKNOWN",
                response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
                (int)response.StatusCode,
                problem?.ErrorCode ?? "UNKNOWN",
                problem?.TraceId ?? "UNKNOWN",
                problem?.Detail ?? "n/a");

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
