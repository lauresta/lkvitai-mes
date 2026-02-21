using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class ReportsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ReportsClient>? _logger;

    public ReportsClient(
        IHttpClientFactory factory,
        ILogger<ReportsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<StockLevelResponseDto> GetStockLevelAsync(
        int? itemId,
        int? locationId,
        int? categoryId,
        bool includeReserved,
        bool includeVirtualLocations,
        DateOnly? expiringBefore,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("itemId", itemId?.ToString()),
            ("locationId", locationId?.ToString()),
            ("categoryId", categoryId?.ToString()),
            ("includeReserved", includeReserved.ToString().ToLowerInvariant()),
            ("includeVirtualLocations", includeVirtualLocations.ToString().ToLowerInvariant()),
            ("expiringBefore", expiringBefore?.ToString("yyyy-MM-dd")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<StockLevelResponseDto>($"/api/warehouse/v1/stock/available{query}", cancellationToken);
    }

    public Task<byte[]> DownloadStockLevelCsvAsync(
        int? itemId,
        int? locationId,
        int? categoryId,
        bool includeReserved,
        bool includeVirtualLocations,
        DateOnly? expiringBefore,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("itemId", itemId?.ToString()),
            ("locationId", locationId?.ToString()),
            ("categoryId", categoryId?.ToString()),
            ("includeReserved", includeReserved.ToString().ToLowerInvariant()),
            ("includeVirtualLocations", includeVirtualLocations.ToString().ToLowerInvariant()),
            ("expiringBefore", expiringBefore?.ToString("yyyy-MM-dd")),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/stock/available{query}", cancellationToken);
    }

    public Task<PagedApiResponse<ReceivingHistoryRowDto>> GetReceivingHistoryAsync(
        int? supplierId,
        string? status,
        DateOnly? expectedDateFrom,
        DateOnly? expectedDateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("supplierId", supplierId?.ToString()),
            ("status", status),
            ("expectedDateFrom", expectedDateFrom?.ToString("yyyy-MM-dd")),
            ("expectedDateTo", expectedDateTo?.ToString("yyyy-MM-dd")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<PagedApiResponse<ReceivingHistoryRowDto>>(
            $"/api/warehouse/v1/receiving/shipments{query}",
            cancellationToken);
    }

    public Task<byte[]> DownloadReceivingHistoryCsvAsync(
        int? supplierId,
        string? status,
        DateOnly? expectedDateFrom,
        DateOnly? expectedDateTo,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("supplierId", supplierId?.ToString()),
            ("status", status),
            ("expectedDateFrom", expectedDateFrom?.ToString("yyyy-MM-dd")),
            ("expectedDateTo", expectedDateTo?.ToString("yyyy-MM-dd")),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/receiving/shipments{query}", cancellationToken);
    }

    public Task<PagedApiResponse<PickHistoryRowDto>> GetPickHistoryAsync(
        string? orderId,
        string? userId,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("orderId", orderId),
            ("userId", userId),
            ("dateFrom", dateFrom?.ToString("O")),
            ("dateTo", dateTo?.ToString("O")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<PagedApiResponse<PickHistoryRowDto>>(
            $"/api/warehouse/v1/picking/history{query}",
            cancellationToken);
    }

    public Task<byte[]> DownloadPickHistoryCsvAsync(
        string? orderId,
        string? userId,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("orderId", orderId),
            ("userId", userId),
            ("dateFrom", dateFrom?.ToString("O")),
            ("dateTo", dateTo?.ToString("O")),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/picking/history{query}", cancellationToken);
    }

    public Task<DispatchHistoryReportResponseDto> GetDispatchHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        string? carrier,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("from", from?.ToString("yyyy-MM-dd")),
            ("to", to?.ToString("yyyy-MM-dd")),
            ("carrier", carrier),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<DispatchHistoryReportResponseDto>($"/api/warehouse/v1/reports/dispatch-history{query}", cancellationToken);
    }

    public Task<byte[]> DownloadDispatchHistoryCsvAsync(
        DateOnly? from,
        DateOnly? to,
        string? carrier,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("from", from?.ToString("yyyy-MM-dd")),
            ("to", to?.ToString("yyyy-MM-dd")),
            ("carrier", carrier),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/reports/dispatch-history{query}", cancellationToken);
    }

    public Task<StockMovementsResponseDto> GetStockMovementsAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        int? itemId,
        int? locationId,
        string? operatorId,
        string? movementType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("startDate", startDate?.ToString("O")),
            ("endDate", endDate?.ToString("O")),
            ("itemId", itemId?.ToString()),
            ("locationId", locationId?.ToString()),
            ("operatorId", operatorId),
            ("movementType", movementType),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<StockMovementsResponseDto>($"/api/warehouse/v1/reports/stock-movements{query}", cancellationToken);
    }

    public Task<byte[]> DownloadStockMovementsCsvAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        int? itemId,
        int? locationId,
        string? operatorId,
        string? movementType,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("startDate", startDate?.ToString("O")),
            ("endDate", endDate?.ToString("O")),
            ("itemId", itemId?.ToString()),
            ("locationId", locationId?.ToString()),
            ("operatorId", operatorId),
            ("movementType", movementType),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/reports/stock-movements{query}", cancellationToken);
    }

    public Task<TraceabilityResponseDto> GetTraceabilityAsync(
        string? lotNumber,
        string? itemSku,
        string? salesOrder,
        string? supplier,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("lotNumber", lotNumber),
            ("itemSku", itemSku),
            ("salesOrder", salesOrder),
            ("supplier", supplier));

        return GetAsync<TraceabilityResponseDto>($"/api/warehouse/v1/reports/traceability{query}", cancellationToken);
    }

    public Task<ComplianceAuditResponseDto> GetComplianceAuditAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        string? reportType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("startDate", startDate?.ToString("O")),
            ("endDate", endDate?.ToString("O")),
            ("reportType", reportType),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<ComplianceAuditResponseDto>($"/api/warehouse/v1/reports/compliance-audit{query}", cancellationToken);
    }

    public Task<byte[]> DownloadComplianceAuditCsvAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        string? reportType,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("startDate", startDate?.ToString("O")),
            ("endDate", endDate?.ToString("O")),
            ("reportType", reportType),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/reports/compliance-audit{query}", cancellationToken);
    }

    public Task<byte[]> DownloadComplianceAuditPdfAsync(
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        string? reportType,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("startDate", startDate?.ToString("O")),
            ("endDate", endDate?.ToString("O")),
            ("reportType", reportType),
            ("exportPdf", "true"));

        return DownloadAsync($"/api/warehouse/v1/reports/compliance-audit{query}", cancellationToken);
    }

    public async Task<LotTraceResponseDto> BuildLotTraceAsync(
        string lotNumber,
        string direction,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var body = new { lotNumber, direction };
        var response = await client.PostAsJsonAsync(
            "/api/warehouse/v1/admin/compliance/lot-trace",
            body,
            cancellationToken);
        await EnsureSuccessAsync(response);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var model = JsonSerializer.Deserialize<LotTraceResponseDto>(json, JsonOptions);
        return model ?? throw new JsonException("Unable to deserialize lot trace response.");
    }

    public async Task<LotTraceResponseDto> GetLotTraceAsync(Guid traceId, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync($"/api/warehouse/v1/admin/compliance/lot-trace/{traceId}", cancellationToken);
        await EnsureSuccessAsync(response);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var model = JsonSerializer.Deserialize<LotTraceResponseDto>(json, JsonOptions);
        return model ?? throw new JsonException("Unable to deserialize lot trace response.");
    }

    public Task<byte[]> DownloadLotTraceCsvAsync(
        string lotNumber,
        string direction,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var body = new { lotNumber, direction, format = "CSV" };
        return PostDownloadAsync("/api/warehouse/v1/admin/compliance/lot-trace", body, cancellationToken);
    }

    public Task<ComplianceDashboardDto> GetComplianceDashboardAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<ComplianceDashboardDto>("/api/warehouse/v1/admin/compliance/dashboard", cancellationToken);
    }

    public Task<IReadOnlyList<ScheduledReportDto>> GetScheduledReportsAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<IReadOnlyList<ScheduledReportDto>>("/api/warehouse/v1/admin/compliance/scheduled-reports", cancellationToken);
    }

    public Task<ScheduledReportDto> CreateScheduledReportAsync(
        string reportType,
        string schedule,
        IEnumerable<string> emailRecipients,
        string format,
        bool active,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            reportType,
            schedule,
            emailRecipients = emailRecipients.ToList(),
            format,
            active
        };
        return PostJsonAsync<ScheduledReportDto>("/api/warehouse/v1/admin/compliance/scheduled-reports", body, cancellationToken);
    }

    public Task<ScheduledReportDto> UpdateScheduledReportAsync(
        int id,
        string reportType,
        string schedule,
        IEnumerable<string> emailRecipients,
        string format,
        bool active,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            reportType,
            schedule,
            emailRecipients = emailRecipients.ToList(),
            format,
            active
        };

        return PutJsonAsync<ScheduledReportDto>($"/api/warehouse/v1/admin/compliance/scheduled-reports/{id}", body, cancellationToken);
    }

    public async Task DeleteScheduledReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.DeleteAsync($"/api/warehouse/v1/admin/compliance/scheduled-reports/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<ReportHistoryDto> RunScheduledReportAsync(int id, CancellationToken cancellationToken = default)
    {
        return PostJsonAsync<ReportHistoryDto>(
            $"/api/warehouse/v1/admin/compliance/scheduled-reports/{id}/run",
            new { },
            cancellationToken);
    }

    public Task<IReadOnlyList<ReportHistoryDto>> GetReportHistoryAsync(int? limit, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("limit", limit?.ToString()));
        return GetAsync<IReadOnlyList<ReportHistoryDto>>($"/api/warehouse/v1/admin/compliance/scheduled-reports/history{query}", cancellationToken);
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

    private async Task<T> PostJsonAsync<T>(
        string relativeUrl,
        object body,
        CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(relativeUrl, body, cancellationToken);
        await EnsureSuccessAsync(response);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var model = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private async Task<T> PutJsonAsync<T>(
        string relativeUrl,
        object body,
        CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PutAsJsonAsync(relativeUrl, body, cancellationToken);
        await EnsureSuccessAsync(response);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var model = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private async Task<byte[]> DownloadAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<byte[]> PostDownloadAsync(
        string relativeUrl,
        object body,
        CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(relativeUrl, body, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

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

    private static string BuildQuery(params (string Key, string? Value)[] items)
    {
        var filtered = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value!)}")
            .ToList();

        return filtered.Count == 0 ? string.Empty : $"?{string.Join("&", filtered)}";
    }
}
