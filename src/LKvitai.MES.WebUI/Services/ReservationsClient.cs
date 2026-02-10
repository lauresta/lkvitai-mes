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
    private readonly ILogger<ReservationsClient>? _logger;

    public ReservationsClient(
        IHttpClientFactory factory,
        ILogger<ReservationsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<PagedResult<ReservationDto>> GetReservationsAsync(string? status, int page = 1, int pageSize = 50)
    {
        var url = $"/api/reservations?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"&status={Uri.EscapeDataString(status)}";
        }

        return GetAsync<PagedResult<ReservationDto>>(url);
    }

    public Task<PagedResult<ReservationDto>> SearchReservationsAsync(string? status, int page = 1, int pageSize = 50)
        => GetReservationsAsync(status, page, pageSize);

    public Task<StartPickingResponseDto> StartPickingAsync(Guid reservationId)
    {
        return PostAsync<StartPickingResponseDto>($"/api/reservations/{reservationId}/start-picking", new { reservationId });
    }

    public Task<PickResponseDto> PickAsync(Guid reservationId, PickRequestDto requestDto)
    {
        var payload = requestDto.ReservationId == Guid.Empty
            ? requestDto with { ReservationId = reservationId }
            : requestDto;

        return PostAsync<PickResponseDto>($"/api/reservations/{reservationId}/pick", payload);
    }

    private async Task<T> GetAsync<T>(string relativeUrl)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl);
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
