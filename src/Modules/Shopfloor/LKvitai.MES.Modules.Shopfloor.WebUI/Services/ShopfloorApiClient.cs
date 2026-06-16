using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.Contracts.Common;
using LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;
using LKvitai.MES.Modules.Shopfloor.Contracts.Mappings;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;
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

    private sealed record ApiError(int Status, string Message, IReadOnlyList<string>? Errors);
}
