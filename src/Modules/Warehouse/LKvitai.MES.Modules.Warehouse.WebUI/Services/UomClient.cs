using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class UomClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<UomClient>? _logger;

    public UomClient(IHttpClientFactory factory, ILogger<UomClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<UnitOfMeasureDto>> GetUnitsAsync(
        string? search,
        string? type,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("search", search),
            ("type", type));

        return GetAsync<IReadOnlyList<UnitOfMeasureDto>>($"/api/warehouse/v1/unit-of-measures{query}", cancellationToken);
    }

    public Task<IReadOnlyList<HandlingUnitTypeDto>> GetHandlingUnitTypesAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("search", search));
        return GetAsync<IReadOnlyList<HandlingUnitTypeDto>>($"/api/warehouse/v1/handling-unit-types{query}", cancellationToken);
    }

    public Task<PagedApiResponse<ItemUomConversionDto>> GetConversionsAsync(
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("search", search),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<PagedApiResponse<ItemUomConversionDto>>($"/api/warehouse/v1/item-uom-conversions{query}", cancellationToken);
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl, cancellationToken);
        await EnsureSuccessAsync(response);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
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
            "UoM request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
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
