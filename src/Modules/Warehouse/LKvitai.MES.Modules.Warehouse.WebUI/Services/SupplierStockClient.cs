using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

/// <summary>Issue to production and the stock report by supplier.</summary>
public sealed class SupplierStockClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<SupplierStockClient>? _logger;

    public SupplierStockClient(IHttpClientFactory factory, ILogger<SupplierStockClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<SupplierStockReportDto> GetReportAsync(int supplierId, int months, bool onlyShortage, CancellationToken cancellationToken = default)
        => SendAndReadAsync<SupplierStockReportDto>(() => Client().GetAsync(ReportUrl(supplierId, months, onlyShortage, false), cancellationToken));

    public async Task<byte[]> DownloadReportCsvAsync(int supplierId, int months, bool onlyShortage, CancellationToken cancellationToken = default)
    {
        var response = await Client().GetAsync(ReportUrl(supplierId, months, onlyShortage, true), cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public Task<PagedApiResponse<ProductionIssueDto>> GetIssuesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => SendAndReadAsync<PagedApiResponse<ProductionIssueDto>>(() => Client().GetAsync(
            $"/api/warehouse/v1/production-issues?pageNumber={Math.Max(1, pageNumber)}&pageSize={Math.Clamp(pageSize, 1, 500)}", cancellationToken));

    public Task<ProductionIssueDto> IssueAsync(ProductionIssueRequestDto request, CancellationToken cancellationToken = default)
        => SendAndReadAsync<ProductionIssueDto>(() => Client().PostAsJsonAsync("/api/warehouse/v1/production-issues", request, cancellationToken));

    private static string ReportUrl(int supplierId, int months, bool onlyShortage, bool csv)
        => $"/api/warehouse/v1/reports/supplier-stock?supplierId={supplierId}&months={months}&onlyShortage={(onlyShortage ? "true" : "false")}{(csv ? "&exportCsv=true" : string.Empty)}";

    private HttpClient Client() => _factory.CreateClient("WarehouseApi");

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(body, JsonOptions) ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var problem = await ProblemDetailsParser.ParseAsync(response);
        _logger?.LogError(
            "Warehouse API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");
        throw new ApiException(problem, (int)response.StatusCode);
    }
}
