using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class SerialNumbersClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<SerialNumbersClient>? _logger;

    public SerialNumbersClient(IHttpClientFactory factory, ILogger<SerialNumbersClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<SerialDto>> GetSerialsAsync(string? serial, int? itemId, string? status, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("serial", serial),
            ("itemId", itemId?.ToString()),
            ("status", status));

        return GetAsync<IReadOnlyList<SerialDto>>($"/api/warehouse/v1/serials{query}", cancellationToken);
    }

    public Task<SerialDto> CreateSerialAsync(SerialRegisterDto request, CancellationToken cancellationToken = default)
        => PostAsync<SerialDto>("/api/warehouse/v1/serials", request, cancellationToken);

    public Task<SerialDto> UpdateStatusAsync(Guid id, string status, string? location, CancellationToken cancellationToken = default)
        => PostAsync<SerialDto>($"/api/warehouse/v1/serials/{id}/status", new SerialTransitionDto(status, location), cancellationToken);

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
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await ProblemDetailsParser.ParseAsync(response);
        _logger?.LogError(
            "Serial numbers request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");

        throw new ApiException(problem, (int)response.StatusCode);
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
