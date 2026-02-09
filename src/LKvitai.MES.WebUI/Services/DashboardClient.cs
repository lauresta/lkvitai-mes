using System.Text.Json;
using System.Globalization;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public class DashboardClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;

    public DashboardClient(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public Task<HealthStatusDto> GetHealthAsync() => GetAsync<HealthStatusDto>("/api/dashboard/health");

    public Task<StockSummaryDto> GetStockSummaryAsync() => GetAsync<StockSummaryDto>("/api/dashboard/stock-summary");

    public Task<ReservationSummaryDto> GetReservationSummaryAsync()
        => GetAsync<ReservationSummaryDto>("/api/dashboard/reservation-summary");

    public Task<ProjectionHealthDto> GetProjectionHealthAsync()
        => GetProjectionHealthCoreAsync();

    public Task<IReadOnlyList<RecentMovementDto>> GetRecentActivityAsync(int limit = 10)
        => GetRecentActivityCoreAsync(limit);

    private async Task<ProjectionHealthDto> GetProjectionHealthCoreAsync()
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync("/api/dashboard/projection-health");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new ProjectionHealthDto();
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            return root.Deserialize<ProjectionHealthDto>(JsonOptions) ?? new ProjectionHealthDto();
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Unexpected projection health response payload.");
        }

        var mapped = new ProjectionHealthDto();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var projectionName = TryGetString(item, "projectionName");
            var lag = TryGetDouble(item, "lagSeconds");
            if (string.IsNullOrWhiteSpace(projectionName) || !lag.HasValue)
            {
                continue;
            }

            if (projectionName.Contains("location", StringComparison.OrdinalIgnoreCase))
            {
                mapped = mapped with { LocationBalanceLag = lag };
            }
            else if (projectionName.Contains("available", StringComparison.OrdinalIgnoreCase))
            {
                mapped = mapped with { AvailableStockLag = lag };
            }
        }

        return mapped;
    }

    private async Task<IReadOnlyList<RecentMovementDto>> GetRecentActivityCoreAsync(int limit)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync($"/api/dashboard/recent-activity?limit={Math.Max(1, limit)}");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<RecentMovementDto>();
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.Deserialize<IReadOnlyList<RecentMovementDto>>(JsonOptions) ?? Array.Empty<RecentMovementDto>();
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("movements", out var movements) &&
            movements.ValueKind == JsonValueKind.Array)
        {
            return movements.Deserialize<IReadOnlyList<RecentMovementDto>>(JsonOptions) ?? Array.Empty<RecentMovementDto>();
        }

        throw new JsonException("Unexpected recent activity response payload.");
    }

    private async Task<T> GetAsync<T>(string relativeUrl)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private static string? TryGetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static double? TryGetDouble(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var parsed) => parsed,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }
}
