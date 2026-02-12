using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public sealed class AgnumClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<AgnumClient>? _logger;

    public AgnumClient(IHttpClientFactory factory, ILogger<AgnumClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<AgnumConfigDto?> GetConfigAsync(CancellationToken cancellationToken = default)
        => GetAsync<AgnumConfigDto?>("/api/warehouse/v1/agnum/config", cancellationToken);

    public Task<AgnumConfigSavedResponseDto> SaveConfigAsync(
        PutAgnumConfigRequestDto request,
        CancellationToken cancellationToken = default)
        => PutAsync<AgnumConfigSavedResponseDto>("/api/warehouse/v1/agnum/config", request, cancellationToken);

    public Task<TestAgnumConnectionResponseDto> TestConnectionAsync(
        TestAgnumConnectionRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync<TestAgnumConnectionResponseDto>("/api/warehouse/v1/agnum/test-connection", request, cancellationToken);

    public async Task<AgnumReconciliationReportDto> GenerateReconciliationAsync(
        DateOnly date,
        string fileName,
        Stream csvStream,
        string? accountCode = null,
        decimal? varianceThresholdAmount = null,
        decimal? varianceThresholdPercent = null,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");

        using var payload = new MultipartFormDataContent();
        payload.Add(new StringContent(date.ToString("yyyy-MM-dd")), "Date");

        if (!string.IsNullOrWhiteSpace(accountCode))
        {
            payload.Add(new StringContent(accountCode.Trim()), "AccountCode");
        }

        if (varianceThresholdAmount.HasValue)
        {
            payload.Add(new StringContent(varianceThresholdAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "VarianceThresholdAmount");
        }

        if (varianceThresholdPercent.HasValue)
        {
            payload.Add(new StringContent(varianceThresholdPercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "VarianceThresholdPercent");
        }

        using var fileContent = new StreamContent(csvStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        payload.Add(fileContent, "AgnumBalanceCsv", string.IsNullOrWhiteSpace(fileName) ? "agnum-balance.csv" : fileName);

        var response = await client.PostAsync("/api/warehouse/v1/agnum/reconcile", payload, cancellationToken);
        return await DeserializeResponseAsync<AgnumReconciliationReportDto>(response);
    }

    public Task<AgnumReconciliationReportDto> GetReconciliationReportAsync(
        Guid reportId,
        string? accountCode = null,
        decimal? varianceThresholdAmount = null,
        decimal? varianceThresholdPercent = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQueryString(accountCode, varianceThresholdAmount, varianceThresholdPercent);
        return GetAsync<AgnumReconciliationReportDto>($"/api/warehouse/v1/agnum/reconcile/{reportId}{query}", cancellationToken);
    }

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

    private Task<T> PutAsync<T>(string relativeUrl, object payload, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.PutAsJsonAsync(relativeUrl, payload, cancellationToken);
        });

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        return await DeserializeResponseAsync<T>(response);
    }

    private async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogError(
                "Agnum API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
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

    private static string BuildQueryString(
        string? accountCode,
        decimal? varianceThresholdAmount,
        decimal? varianceThresholdPercent)
    {
        var pairs = new List<string>();
        if (!string.IsNullOrWhiteSpace(accountCode))
        {
            pairs.Add($"accountCode={Uri.EscapeDataString(accountCode.Trim())}");
        }

        if (varianceThresholdAmount.HasValue)
        {
            pairs.Add($"varianceThresholdAmount={varianceThresholdAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (varianceThresholdPercent.HasValue)
        {
            pairs.Add($"varianceThresholdPercent={varianceThresholdPercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        return pairs.Count == 0 ? string.Empty : $"?{string.Join("&", pairs)}";
    }
}
