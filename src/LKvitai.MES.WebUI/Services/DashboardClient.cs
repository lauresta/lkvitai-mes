using System.Text.Json;
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

    public Task<ReservationSummaryDto> GetReservationSummaryAsync() => GetAsync<ReservationSummaryDto>("/api/dashboard/reservation-summary");

    public Task<ProjectionHealthDto> GetProjectionHealthAsync() => GetAsync<ProjectionHealthDto>("/api/dashboard/projection-health");

    public async Task<IReadOnlyList<RecentMovementDto>> GetRecentActivityAsync(int limit = 10)
    {
        var dto = await GetAsync<RecentActivityResponseDto>($"/api/dashboard/recent-activity?limit={limit}");
        return dto.Movements;
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

    private sealed record RecentActivityResponseDto
    {
        public IReadOnlyList<RecentMovementDto> Movements { get; init; } = Array.Empty<RecentMovementDto>();
    }
}
