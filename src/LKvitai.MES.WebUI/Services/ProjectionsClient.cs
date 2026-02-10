using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public class ProjectionsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ProjectionsClient>? _logger;

    public ProjectionsClient(
        IHttpClientFactory factory,
        ILogger<ProjectionsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<RebuildResultDto> RebuildAsync(string projectionName)
    {
        return PostAsync<RebuildResultDto>("/api/projections/rebuild", new { projectionName });
    }

    public Task<VerifyResultDto> VerifyAsync(string projectionName)
    {
        return PostAsync<VerifyResultDto>("/api/projections/verify", new { projectionName });
    }

    public async Task<IReadOnlyList<ProjectionLagStatusDto>> GetProjectionLagAsync()
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync("/api/warehouse/v1/health");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            LogFailure(response, problem);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<ProjectionLagStatusDto>();
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("projectionStatus", out var projectionStatus) ||
            projectionStatus.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<ProjectionLagStatusDto>();
        }

        var rows = new List<ProjectionLagStatusDto>();
        foreach (var property in projectionStatus.EnumerateObject())
        {
            var row = property.Value;
            rows.Add(new ProjectionLagStatusDto
            {
                ProjectionName = property.Name,
                Status = row.TryGetProperty("status", out var statusValue) ? statusValue.GetString() ?? "Unknown" : "Unknown",
                LagSeconds = row.TryGetProperty("lagSeconds", out var lagValue) && lagValue.ValueKind == JsonValueKind.Number
                    ? lagValue.GetDouble()
                    : null,
                LagEvents = row.TryGetProperty("lagEvents", out var lagEventsValue) && lagEventsValue.ValueKind == JsonValueKind.Number
                    ? lagEventsValue.GetInt64()
                    : null,
                LastUpdated = row.TryGetProperty("lastUpdated", out var lastUpdatedValue) && lastUpdatedValue.ValueKind == JsonValueKind.String
                              && DateTimeOffset.TryParse(lastUpdatedValue.GetString(), out var parsed)
                    ? parsed
                    : null
            });
        }

        return rows
            .OrderBy(x => x.ProjectionName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<T> PostAsync<T>(string relativeUrl, object payload)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(relativeUrl, payload);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            LogFailure(response, problem);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private void LogFailure(HttpResponseMessage response, ProblemDetailsModel? problem)
    {
        _logger?.LogError(
            "Warehouse API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");
    }
}
