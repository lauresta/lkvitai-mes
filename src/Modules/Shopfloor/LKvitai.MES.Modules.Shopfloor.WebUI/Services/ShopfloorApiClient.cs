using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.Contracts.Common;
using LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;
using LKvitai.MES.Modules.Shopfloor.Contracts.Mappings;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reports;
using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Services;

/// <summary>Outcome of a Shopfloor API write/read call with a friendly error message.</summary>
public sealed record ApiResult<T>(bool Success, T? Value, string? Error)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null);
    public static ApiResult<T> Fail(string error) => new(false, default, error);
}

public sealed record ApiResult(bool Success, string? Error)
{
    public static ApiResult Ok() => new(true, null);
    public static ApiResult Fail(string error) => new(false, error);
}

public sealed class ProductionQueuePageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
    public List<ProductionQueueItemDto> Items { get; set; } = [];
}

public sealed class ProductionQueueItemDto
{
    public long ProductId { get; set; }
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public string Department { get; set; } = "";
    public string ProductType { get; set; } = "";
    public string Priority { get; set; } = "";
    public string State { get; set; } = "";
    public string? Colour { get; set; }
    public string? Barcode { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public sealed class ProductionQueueFacetDto
{
    public string Kind { get; set; } = "";
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
    public int Rows { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class ProductionTaskPageDto
{
    public int Page { get; set; }
    public bool HasMore { get; set; }
    public List<ProductionTaskSummaryDto> Items { get; set; } = [];
}

public sealed class IssueProductionPortionResultDto
{
    public int TaskId { get; set; }
    public string Number { get; set; } = "";
    public int IssuedRows { get; set; }
}

public sealed class ProductionDocumentPageDto
{
    public int Page { get; set; }
    public bool HasMore { get; set; }
    public List<ProductionDocumentSummaryDto> Items { get; set; } = [];
}

public sealed class ProductionDocumentSummaryDto
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public string Author { get; set; } = "";
    public string Operation { get; set; } = "";
    public int Rows { get; set; }
}

public sealed class ProductionDocumentDetailDto
{
    public ProductionDocumentSummaryDto Document { get; set; } = new();
    public List<ProductionDocumentLineDto> Items { get; set; } = [];
}

public sealed class ProductionDocumentLineDto
{
    public string Barcode { get; set; } = "";
    public string? Description { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? InsertedBy { get; set; }
    public DateTime? InsertedAt { get; set; }
}

public sealed class ProductionTaskSummaryDto
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime? Created { get; set; }
    public string? Author { get; set; }
    public int ItemRows { get; set; }
    public int OrderCount { get; set; }
}

public sealed class ProductionTaskDetailDto
{
    public ProductionTaskSummaryDto Task { get; set; } = new();
    public List<ProductionTaskLineDto> Items { get; set; } = [];
}

public sealed class ProductionTaskLineDto
{
    public long ProductId { get; set; }
    public string Barcode { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string ProductType { get; set; } = "";
    public string? MaterialCode { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public decimal Quantity { get; set; }
    public string State { get; set; } = "";
}

public sealed class ProductionHistoryDocumentDto
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime? Date { get; set; }
    public string? Author { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? Operation { get; set; }
}

public sealed class ProductionHistoryRepairDto
{
    public int Id { get; set; }
    public DateTime Registered { get; set; }
    public string? Reason { get; set; }
    public string? Claimed { get; set; }
    public string? Actual { get; set; }
}

public sealed class ProductionProductHistoryDto
{
    public long ProductId { get; set; }
    public string Barcode { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public string Department { get; set; } = "";
    public string? Customer { get; set; }
    public string Category { get; set; } = "";
    public string ProductType { get; set; } = "";
    public string? Control { get; set; }
    public string? MaterialCode { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
    public string State { get; set; } = "";
    public List<ProductionHistoryDocumentDto> Documents { get; set; } = [];
    public List<ProductionHistoryRepairDto> Repairs { get; set; } = [];
}

public sealed class ProductionRepairPageDto
{
    public int Page { get; set; }
    public bool HasMore { get; set; }
    public List<ProductionRepairDto> Items { get; set; } = [];
}

public sealed class ProductionRepairDto
{
    public int Id { get; set; }
    public long ProductId { get; set; }
    public string Barcode { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public string ProductType { get; set; } = "";
    public decimal? WidthMm { get; set; }
    public decimal? HeightMm { get; set; }
    public DateTime Registered { get; set; }
    public string Reason { get; set; } = "";
    public string? Claimed { get; set; }
    public string? Actual { get; set; }
    public string ProductState { get; set; } = "";
    public bool Unfinished { get; set; }
    public bool MissingActual { get; set; }
}

public sealed class ProductionReportCatalogDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
}

public sealed class TrapezoidItemListDto
{
    public int TaskId { get; set; }
    public string TaskNumber { get; set; } = "";
    public bool CanEdit { get; set; }
    public List<TrapezoidItemDto> Items { get; set; } = [];
}

public sealed class TrapezoidRecentOrderDto
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public int TaskId { get; set; }
    public string TaskNumber { get; set; } = "";
}

public sealed class TrapezoidItemDto
{
    public long ProductId { get; set; }
    public string Barcode { get; set; } = "";
    public string OrderNumber { get; set; } = "";
    public int Position { get; set; }
    public int TypeId { get; set; }
    public string ProductType { get; set; } = "";
    public string? MaterialCode { get; set; }
    public string? MaterialName { get; set; }
    public decimal Quantity { get; set; }
    public string? Control { get; set; }
    public string? ControlType { get; set; }
    public int? SlatWidthItemMm { get; set; }
    public decimal? WidthMm { get; set; }
    public decimal? TallHeightMm { get; set; }
    public decimal? ShortHeightMm { get; set; }
    public decimal? OverhangMm { get; set; }
    public string Revision { get; set; } = "";
    public bool Supported { get; set; }
}

public sealed class TrapezoidInputsDto
{
    public string? ControlType { get; set; }
    public int? SlatWidthMm { get; set; }
    public double? WidthMm { get; set; }
    public double? ShortHeightMm { get; set; }
    public double? TallHeightMm { get; set; }
    public double? OverhangMm { get; set; }
    public string? HighSide { get; set; }
}

public sealed class TrapezoidSlatDto
{
    public int Number { get; set; }
    public double FinishedLengthRawMm { get; set; }
    public double CutLengthRawMm { get; set; }
    public double CutLengthDisplayMm { get; set; }
    public double AlongRailMm { get; set; }
    public double HorizontalMm { get; set; }
}

public sealed class TrapezoidResultDto
{
    public double Angle { get; set; }
    public double DiagonalMm { get; set; }
    public double RailBlankMm { get; set; }
    public double RailCutMm { get; set; }
    public double FixedOffsetMm { get; set; }
    public double FreeOffsetMm { get; set; }
    public double FixedToMechanismMm { get; set; }
    public double SpanMm { get; set; }
    public double LookupWidthMm { get; set; }
    public int BaseCount { get; set; }
    public int ExtraCount { get; set; }
    public int SlatCount { get; set; }
    public double PitchMm { get; set; }
    public double SpacerCutMm { get; set; }
    public double ExtenderMm { get; set; }
    public double FabricUsagePerProductRawM { get; set; }
    public List<TrapezoidSlatDto> Slats { get; set; } = [];
}

public sealed class TrapezoidCalculationDto
{
    public TrapezoidResultDto Result { get; set; } = new();
    public string ProfileVersion { get; set; } = "";
    public string TableVersion { get; set; } = "";
    public string AlgorithmVersion { get; set; } = "";
}

public sealed class TrapezoidVersionDto
{
    public int TaskId { get; set; }
    public string TaskNumber { get; set; } = "";
    public TrapezoidItemDto Item { get; set; } = new();
    public int Version { get; set; }
    public TrapezoidInputsDto Inputs { get; set; } = new();
    public Dictionary<string, string> Sources { get; set; } = [];
    public TrapezoidCalculationDto Calculation { get; set; } = new();
    public DateTimeOffset SavedAt { get; set; }
    public string SavedBy { get; set; } = "";
    public string Language { get; set; } = "en";
}

public sealed class TrapezoidVersionSummaryDto
{
    public int Version { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string SavedBy { get; set; } = "";
    public bool HasPdf { get; set; }
    public Dictionary<string, string> Sources { get; set; } = [];
}

public sealed class ProductionReportDataDto
{
    public int TaskId { get; set; }
    public int ReportId { get; set; }
    public string ReportName { get; set; } = "";
    public List<string> Columns { get; set; } = [];
    public List<List<JsonElement>> Rows { get; set; } = [];
    public bool NoData { get; set; }
    public List<ProductionReportAccessoryDto> Accessories { get; set; } = [];
}

public sealed class ProductionReportAccessoryDto
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? AccessoryName { get; set; }
}

public sealed class RoletaiPaperlessDataDto
{
    public string OrderStatus { get; set; } = "default";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public List<RoletaiPaperlessRowDto> Rows { get; set; } = [];
    public List<ProductionReportAccessoryDto> Accessories { get; set; } = [];
    public List<RoletaiPaperlessMarkDto> MyMarks { get; set; } = [];
    public List<RoletaiPaperlessSignOffDto> SignOffs { get; set; } = [];
}

public sealed class RoletaiPaperlessRowDto
{
    public string Uzsakymas { get; set; } = "";
    public DateTime UzsakymoData { get; set; }
    public long ParduotoGaminioID { get; set; }
    public string TipoTrPavadinimas { get; set; } = "";
    public string? Valdymas { get; set; }
    public long Rusis { get; set; }
    public decimal? Plotis { get; set; }
    public decimal? Aukstis { get; set; }
    public decimal Kiekis { get; set; }
    public string? Spalva { get; set; }
    public string? Audinys { get; set; }
    public string? Kasete { get; set; }
    public string? Velenas { get; set; }
    public string? Atsvaras { get; set; }
    public string? Kreipianciosios { get; set; }
    public string? KrepianciujuTipas { get; set; }
    public string? Dekoras { get; set; }
    public string? AudinioSunaudojimas { get; set; }
    public string? Pastabos { get; set; }
    public string? Cechas { get; set; }
    public string? GamibinesUzduotiesNumeris { get; set; }
    public bool InProduction { get; set; }
}

public sealed class RoletaiPaperlessMarkDto
{
    public long ProductId { get; set; }
    public int Step { get; set; }
    public bool Marked { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RoletaiPaperlessSignOffDto
{
    public long ProductId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset SignedAt { get; set; }
    public bool Mine { get; set; }
}

/// <summary>
/// Typed wrapper around the named <c>ShopfloorApi</c> HttpClient. Returns
/// <see cref="ApiResult{T}"/> so pages can surface API validation/conflict
/// messages (400/409) instead of generic failures.
/// </summary>
public sealed class ShopfloorApiClient
{
    private const string HttpClientName = "ShopfloorApi";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShopfloorApiClient> _logger;

    public ShopfloorApiClient(IHttpClientFactory httpClientFactory, ILogger<ShopfloorApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient Client => _httpClientFactory.CreateClient(HttpClientName);

    public Task<ApiResult<ProductionQueuePageDto>> GetProductionQueueAsync(int page,
        string? orderNumber, long? departmentId, long? seriesId, long? categoryId,
        long? typeId, long? priorityId, CancellationToken ct)
    {
        var query = new List<string> { $"page={page}", "pageSize=100" };
        if (orderNumber is not null) query.Add("orderNumber=" + Uri.EscapeDataString(orderNumber));
        if (departmentId is not null) query.Add($"departmentId={departmentId}");
        if (seriesId is not null) query.Add($"seriesId={seriesId}");
        if (categoryId is not null) query.Add($"categoryId={categoryId}");
        if (typeId is not null) query.Add($"typeId={typeId}");
        if (priorityId is not null) query.Add($"priorityId={priorityId}");
        return SendAsync<ProductionQueuePageDto>(HttpMethod.Get,
            "/api/shopfloor/production-queue?" + string.Join("&", query), null, ct);
    }

    public Task<ApiResult<List<ProductionQueueFacetDto>>> GetProductionQueueFacetsAsync(CancellationToken ct)
        => SendAsync<List<ProductionQueueFacetDto>>(HttpMethod.Get,
            "/api/shopfloor/production-queue/facets", null, ct);

    public Task<ApiResult<ProductionTaskPageDto>> GetProductionTasksAsync(int page, CancellationToken ct)
        => SendAsync<ProductionTaskPageDto>(HttpMethod.Get,
            $"/api/shopfloor/production-tasks?page={page}&pageSize=50", null, ct);

    public Task<ApiResult<IssueProductionPortionResultDto>> IssueProductionPortionAsync(
        int? taskId, IReadOnlyCollection<long> productIds, CancellationToken ct)
        => SendAsync<IssueProductionPortionResultDto>(HttpMethod.Post,
            "/api/shopfloor/production-tasks/issue", new { taskId, productIds }, ct);

    public Task<ApiResult<ProductionDocumentPageDto>> GetProductionDocumentsAsync(int page, CancellationToken ct)
        => SendAsync<ProductionDocumentPageDto>(HttpMethod.Get,
            $"/api/shopfloor/production-documents?page={page}&pageSize=50", null, ct);

    public Task<ApiResult<ProductionDocumentDetailDto>> GetProductionDocumentAsync(int id, CancellationToken ct)
        => SendAsync<ProductionDocumentDetailDto>(HttpMethod.Get,
            $"/api/shopfloor/production-documents/{id}", null, ct);

    public Task<ApiResult<ProductionTaskDetailDto>> GetProductionTaskAsync(int id, CancellationToken ct)
        => SendAsync<ProductionTaskDetailDto>(HttpMethod.Get,
            $"/api/shopfloor/production-tasks/{id}", null, ct);

    public Task<ApiResult<ProductionProductHistoryDto>> GetProductionHistoryAsync(string barcode, CancellationToken ct)
        => SendAsync<ProductionProductHistoryDto>(HttpMethod.Get,
            $"/api/shopfloor/production-products/{Uri.EscapeDataString(barcode)}/history", null, ct);

    public Task<ApiResult<ProductionRepairPageDto>> GetProductionRepairsAsync(int page,
        string filter, CancellationToken ct)
        => SendAsync<ProductionRepairPageDto>(HttpMethod.Get,
            $"/api/shopfloor/production-repairs?page={page}&filter={Uri.EscapeDataString(filter)}", null, ct);

    public Task<ApiResult<List<ProductionReportCatalogDto>>> GetProductionReportCatalogAsync(CancellationToken ct)
        => SendAsync<List<ProductionReportCatalogDto>>(HttpMethod.Get,
            "/api/shopfloor/production-reports", null, ct);

    public Task<ApiResult<ProductionReportDataDto>> GetProductionReportDataAsync(int taskId,
        int reportId, CancellationToken ct)
        => SendAsync<ProductionReportDataDto>(HttpMethod.Get,
            $"/api/shopfloor/production-tasks/{taskId}/report-data/{reportId}", null, ct);

    public Task<ApiResult<RoletaiPaperlessDataDto>> GetRoletaiPaperlessAsync(
        string? order, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(order)) query.Add("order=" + Uri.EscapeDataString(order.Trim()));
        else
        {
            if (from is not null) query.Add("from=" + from.Value.ToString("yyyy-MM-dd"));
            if (to is not null) query.Add("to=" + to.Value.ToString("yyyy-MM-dd"));
        }
        return SendAsync<RoletaiPaperlessDataDto>(HttpMethod.Get,
            "/api/shopfloor/roletai" + (query.Count == 0 ? "" : "?" + string.Join("&", query)), null, ct);
    }

    public Task<ApiResult<RoletaiPaperlessMarkDto>> SetRoletaiMarkAsync(
        long productId, int step, bool marked, CancellationToken ct)
        => SendAsync<RoletaiPaperlessMarkDto>(HttpMethod.Put,
            $"/api/shopfloor/roletai/items/{productId}/marks/{step}", new { marked }, ct);

    public Task<ApiResult<RoletaiPaperlessSignOffDto>> SignRoletaiAsync(long productId, CancellationToken ct)
        => SendAsync<RoletaiPaperlessSignOffDto>(HttpMethod.Post,
            $"/api/shopfloor/roletai/items/{productId}/signoff", null, ct);

    public Task<ApiResult> WithdrawRoletaiAsync(long productId, CancellationToken ct)
        => SendAsync(HttpMethod.Delete,
            $"/api/shopfloor/roletai/items/{productId}/signoff", null, ct);

    public Task<ApiResult<TrapezoidItemListDto>> GetTrapezoidItemsAsync(int taskId, CancellationToken ct)
        => SendAsync<TrapezoidItemListDto>(HttpMethod.Get,
            $"/api/shopfloor/production-tasks/{taskId}/trapezoid-items", null, ct);

    public Task<ApiResult<List<TrapezoidRecentOrderDto>>> GetRecentTrapezoidOrdersAsync(CancellationToken ct)
        => SendAsync<List<TrapezoidRecentOrderDto>>(HttpMethod.Get,
            "/api/shopfloor/trapezoid/recent-orders", null, ct);

    public Task<ApiResult<TrapezoidCalculationDto>> CalculateTrapezoidAsync(
        TrapezoidInputsDto inputs, CancellationToken ct)
        => SendAsync<TrapezoidCalculationDto>(HttpMethod.Post,
            "/api/shopfloor/trapezoid/calculate", inputs, ct);

    public Task<ApiResult<TrapezoidVersionDto>> SaveTrapezoidAsync(int taskId, long productId,
        Guid requestId, string expectedRevision, TrapezoidInputsDto inputs,
        IReadOnlyDictionary<string, string> sources, string language, CancellationToken ct)
        => SendAsync<TrapezoidVersionDto>(HttpMethod.Post,
            "/api/shopfloor/trapezoid/versions",
            new { taskId, productId, requestId, expectedRevision, inputs, sources, language }, ct);

    public Task<ApiResult<List<TrapezoidVersionSummaryDto>>> GetTrapezoidVersionsAsync(
        int taskId, long productId, CancellationToken ct)
        => SendAsync<List<TrapezoidVersionSummaryDto>>(HttpMethod.Get,
            $"/api/shopfloor/production-tasks/{taskId}/trapezoid-items/{productId}/versions", null, ct);

    public Task<ApiResult<TrapezoidVersionDto>> GetTrapezoidVersionAsync(
        int taskId, long productId, int version, CancellationToken ct)
        => SendAsync<TrapezoidVersionDto>(HttpMethod.Get,
            $"/api/shopfloor/production-tasks/{taskId}/trapezoid-items/{productId}/versions/{version}", null, ct);

    public Task<ApiResult<JsonElement>> GenerateTrapezoidPdfAsync(int taskId, long productId,
        int version, CancellationToken ct)
        => SendAsync<JsonElement>(HttpMethod.Post,
            $"/api/shopfloor/production-tasks/{taskId}/trapezoid-items/{productId}/versions/{version}/pdf",
            null, ct);

    public Task<ApiResult<RoletaiTemplateState>> GetRoletaiTemplateAsync(CancellationToken ct)
        => SendAsync<RoletaiTemplateState>(HttpMethod.Get,
            "/api/shopfloor/production-reports/roletai/template", null, ct);

    public Task<ApiResult<RoletaiTemplateState>> SaveRoletaiDraftAsync(RoletaiTemplate template, CancellationToken ct)
        => SendAsync<RoletaiTemplateState>(HttpMethod.Put,
            "/api/shopfloor/production-reports/roletai/template/draft", template, ct);

    public Task<ApiResult<RoletaiTemplateState>> PublishRoletaiTemplateAsync(int expectedVersion, CancellationToken ct)
        => SendAsync<RoletaiTemplateState>(HttpMethod.Post,
            "/api/shopfloor/production-reports/roletai/template/publish",
            new { expectedVersion }, ct);

    // ---- Work centers -------------------------------------------------------

    public Task<IReadOnlyList<WorkCenterDto>?> GetWorkCentersAsync(CancellationToken ct)
        => GetListAsync<WorkCenterDto>("/api/shopfloor/work-centers", ct);

    public Task<ApiResult<WorkCenterDto>> CreateWorkCenterAsync(CreateWorkCenterRequest request, CancellationToken ct)
        => SendAsync<WorkCenterDto>(HttpMethod.Post, "/api/shopfloor/work-centers", request, ct);

    public Task<ApiResult<WorkCenterDto>> UpdateWorkCenterAsync(Guid id, UpdateWorkCenterRequest request, CancellationToken ct)
        => SendAsync<WorkCenterDto>(HttpMethod.Put, $"/api/shopfloor/work-centers/{id}", request, ct);

    public Task<ApiResult> DeleteWorkCenterAsync(Guid id, CancellationToken ct)
        => SendAsync(HttpMethod.Delete, $"/api/shopfloor/work-centers/{id}", null, ct);

    // ---- Work stations ------------------------------------------------------

    public Task<IReadOnlyList<WorkStationDto>?> GetWorkStationsAsync(bool activeOnly, CancellationToken ct)
        => GetListAsync<WorkStationDto>($"/api/shopfloor/work-stations?activeOnly={activeOnly.ToString().ToLowerInvariant()}", ct);

    public Task<ApiResult<WorkStationDto>> CreateWorkStationAsync(CreateWorkStationRequest request, CancellationToken ct)
        => SendAsync<WorkStationDto>(HttpMethod.Post, "/api/shopfloor/work-stations", request, ct);

    public Task<ApiResult<WorkStationDto>> UpdateWorkStationAsync(Guid id, UpdateWorkStationRequest request, CancellationToken ct)
        => SendAsync<WorkStationDto>(HttpMethod.Put, $"/api/shopfloor/work-stations/{id}", request, ct);

    public Task<ApiResult> DeleteWorkStationAsync(Guid id, CancellationToken ct)
        => SendAsync(HttpMethod.Delete, $"/api/shopfloor/work-stations/{id}", null, ct);

    // ---- Workflows ----------------------------------------------------------

    public Task<IReadOnlyList<WorkflowTemplateSummaryDto>?> GetWorkflowsAsync(CancellationToken ct)
        => GetListAsync<WorkflowTemplateSummaryDto>("/api/shopfloor/workflows", ct);

    public async Task<WorkflowTemplateDto?> GetWorkflowAsync(Guid id, CancellationToken ct)
    {
        var result = await SendAsync<WorkflowTemplateDto>(HttpMethod.Get, $"/api/shopfloor/workflows/{id}", null, ct).ConfigureAwait(false);
        return result.Value;
    }

    public Task<ApiResult<WorkflowTemplateDto>> CreateWorkflowAsync(CreateWorkflowTemplateRequest request, CancellationToken ct)
        => SendAsync<WorkflowTemplateDto>(HttpMethod.Post, "/api/shopfloor/workflows", request, ct);

    public Task<ApiResult<WorkflowTemplateDto>> UpdateWorkflowAsync(Guid id, UpdateWorkflowTemplateRequest request, CancellationToken ct)
        => SendAsync<WorkflowTemplateDto>(HttpMethod.Put, $"/api/shopfloor/workflows/{id}", request, ct);

    public Task<ApiResult> DeleteWorkflowAsync(Guid id, CancellationToken ct)
        => SendAsync(HttpMethod.Delete, $"/api/shopfloor/workflows/{id}", null, ct);

    public Task<ApiResult<WorkflowTemplateDto>> SaveWorkflowGraphAsync(Guid id, SaveWorkflowGraphRequest request, CancellationToken ct)
        => SendAsync<WorkflowTemplateDto>(HttpMethod.Put, $"/api/shopfloor/workflows/{id}/graph", request, ct);

    public Task<ApiResult<WorkflowTemplateDto>> PublishWorkflowAsync(Guid id, CancellationToken ct)
        => SendAsync<WorkflowTemplateDto>(HttpMethod.Post, $"/api/shopfloor/workflows/{id}/publish", null, ct);

    public Task<ApiResult<ValidationReportDto>> ValidateWorkflowGraphAsync(SaveWorkflowGraphRequest request, CancellationToken ct)
        => SendAsync<ValidationReportDto>(HttpMethod.Post, "/api/shopfloor/workflows/validate", request, ct);

    public Task<ApiResult<WorkflowTemplateDto>> CloneWorkflowAsync(Guid id, CloneWorkflowTemplateRequest request, CancellationToken ct)
        => SendAsync<WorkflowTemplateDto>(HttpMethod.Post, $"/api/shopfloor/workflows/{id}/clone", request, ct);

    // ---- Legacy product types ----------------------------------------------

    public async Task<PagedResult<LegacyProductTypeDto>?> GetLegacyProductTypesAsync(
        string? search, bool? mapped, bool removed, int page, int pageSize, CancellationToken ct)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search)}");
        if (mapped is { } m) parts.Add($"mapped={m.ToString().ToLowerInvariant()}");
        if (removed) parts.Add("removed=true");
        if (page > 1) parts.Add($"page={page}");
        if (pageSize != 100) parts.Add($"pageSize={pageSize}");
        var url = "/api/shopfloor/legacy-product-types" + (parts.Count == 0 ? "" : "?" + string.Join("&", parts));

        var result = await SendAsync<PagedResult<LegacyProductTypeDto>>(HttpMethod.Get, url, null, ct).ConfigureAwait(false);
        return result.Value;
    }

    public Task<ApiResult<JsonElement>> SyncLegacyProductTypesAsync(CancellationToken ct)
        => SendAsync<JsonElement>(HttpMethod.Post, "/api/shopfloor/legacy-product-types/sync", null, ct);

    // ---- Mappings -----------------------------------------------------------

    public async Task<CoverageSummaryDto?> GetCoverageAsync(CancellationToken ct)
    {
        var result = await SendAsync<CoverageSummaryDto>(HttpMethod.Get, "/api/shopfloor/product-type-mappings/coverage", null, ct).ConfigureAwait(false);
        return result.Value;
    }

    public Task<ApiResult> BulkAssignAsync(BulkAssignMappingRequest request, CancellationToken ct)
        => SendAsync(HttpMethod.Post, "/api/shopfloor/product-type-mappings/bulk-assign", request, ct);

    public Task<ApiResult> DeleteMappingAsync(string legacyCode, CancellationToken ct)
        => SendAsync(HttpMethod.Delete, $"/api/shopfloor/product-type-mappings/{Uri.EscapeDataString(legacyCode)}", null, ct);

    // ---- transport ----------------------------------------------------------

    private async Task<IReadOnlyList<T>?> GetListAsync<T>(string url, CancellationToken ct)
    {
        var result = await SendAsync<List<T>>(HttpMethod.Get, url, null, ct).ConfigureAwait(false);
        return result.Value;
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            using var response = await Client.SendAsync(request, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    return ApiResult<T>.Ok(default!);
                }

                var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
                return ApiResult<T>.Ok(value!);
            }

            return ApiResult<T>.Fail(await ReadErrorAsync(response, ct).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "[Shopfloor] webui-http {Method} {Url} failed", method, url);
            return ApiResult<T>.Fail("Shopfloor API is unreachable.");
        }
    }

    private async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var result = await SendAsync<JsonElement>(method, url, body, ct).ConfigureAwait(false);
        return result.Success ? ApiResult.Ok() : ApiResult.Fail(result.Error!);
    }

    private async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var doc = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, ct).ConfigureAwait(false);
            if (doc is not null && !string.IsNullOrWhiteSpace(doc.Message))
            {
                return doc.Message;
            }
            if (doc is not null && !string.IsNullOrWhiteSpace(doc.Detail))
                return doc.Detail;
            if (doc is not null && !string.IsNullOrWhiteSpace(doc.Code))
                return doc.Code;
        }
        catch
        {
            // Non-JSON body; fall through to a status-based message.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => "Not found.",
            HttpStatusCode.Conflict => "The operation conflicts with the current state.",
            HttpStatusCode.BadRequest => "The request was invalid.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Not authorized.",
            _ => $"Request failed ({(int)response.StatusCode}).",
        };
    }

    private sealed record ApiError(int Status, string? Message, string? Detail, string? Code,
        IReadOnlyList<string>? Errors);
}
