using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public class ReservationsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;

    public ReservationsClient(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public Task<PagedResult<ReservationDto>> SearchReservationsAsync(string? status, int page = 1, int pageSize = 50)
    {
        var url = $"/api/reservations?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"&status={Uri.EscapeDataString(status)}";
        }

        return GetAsync<PagedResult<ReservationDto>>(url);
    }

    public Task<StartPickingResponseDto> StartPickingAsync(Guid reservationId)
    {
        return PostAsync<StartPickingResponseDto>($"/api/reservations/{reservationId}/start-picking", new { reservationId });
    }

    public Task<PickResponseDto> PickAsync(Guid reservationId, Guid huId, string sku, decimal quantity)
    {
        return PostAsync<PickResponseDto>($"/api/reservations/{reservationId}/pick", new
        {
            reservationId,
            huId,
            sku,
            quantity
        });
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

    private async Task<T> PostAsync<T>(string relativeUrl, object payload)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(relativeUrl, payload);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }
}
