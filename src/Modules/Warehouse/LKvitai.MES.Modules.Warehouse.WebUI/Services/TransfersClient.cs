using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class TransfersClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<TransfersClient>? _logger;

    public TransfersClient(IHttpClientFactory factory, ILogger<TransfersClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<PagedApiResponse<TransferDto>> GetTransfersAsync(
        string? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("status", status),
            ("dateFrom", dateFrom?.ToString("O")),
            ("dateTo", dateTo?.ToString("O")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<PagedApiResponse<TransferDto>>($"/api/warehouse/v1/transfers{query}", cancellationToken);
    }

    public Task<TransferDto> GetTransferByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<TransferDto>($"/api/warehouse/v1/transfers/{id}", cancellationToken);

    public Task<TransferDto> CreateTransferAsync(CreateTransferRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<TransferDto>("/api/warehouse/v1/transfers", request, cancellationToken);

    public Task<TransferDto> SubmitTransferAsync(Guid id, SubmitTransferRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<TransferDto>($"/api/warehouse/v1/transfers/{id}/submit", request, cancellationToken);

    public Task<TransferDto> ApproveTransferAsync(Guid id, ApproveTransferRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<TransferDto>($"/api/warehouse/v1/transfers/{id}/approve", request, cancellationToken);

    public Task<TransferDto> ExecuteTransferAsync(Guid id, ExecuteTransferRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<TransferDto>($"/api/warehouse/v1/transfers/{id}/execute", request, cancellationToken);

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
                "Transfers API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
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
