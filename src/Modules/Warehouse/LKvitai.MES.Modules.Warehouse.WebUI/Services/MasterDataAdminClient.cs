using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class MasterDataAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<MasterDataAdminClient> _logger;

    public MasterDataAdminClient(
        IHttpClientFactory factory,
        ILogger<MasterDataAdminClient> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<PagedApiResponse<AdminItemDto>> GetItemsAsync(
        string? search,
        int? categoryId,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (categoryId.HasValue)
        {
            query.Add($"categoryId={categoryId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        return GetAsync<PagedApiResponse<AdminItemDto>>(
            $"/api/warehouse/v1/items?{string.Join("&", query)}",
            cancellationToken);
    }

    public Task CreateItemAsync(CreateOrUpdateItemRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, "/api/warehouse/v1/items", request, cancellationToken);

    public Task<ItemDetailsDto> GetItemByIdAsync(int id, CancellationToken cancellationToken = default)
        => GetAsync<ItemDetailsDto>($"/api/warehouse/v1/items/{id}", cancellationToken);

    public Task UpdateItemAsync(int id, CreateOrUpdateItemRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Put, $"/api/warehouse/v1/items/{id}", request, cancellationToken);

    public Task DeactivateItemAsync(int id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, $"/api/warehouse/v1/items/{id}/deactivate", null, cancellationToken);

    public Task<ItemPhotosResponseDto> GetItemPhotosAsync(int id, CancellationToken cancellationToken = default)
        => GetAsync<ItemPhotosResponseDto>($"/api/warehouse/v1/items/{id}/photos", cancellationToken);

    public Task MakePrimaryPhotoAsync(int itemId, Guid photoId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(
            HttpMethod.Post,
            $"/api/warehouse/v1/items/{itemId}/photos/{photoId}/make-primary",
            null,
            cancellationToken);

    public Task DeletePhotoAsync(int itemId, Guid photoId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(
            HttpMethod.Delete,
            $"/api/warehouse/v1/items/{itemId}/photos/{photoId}",
            null,
            cancellationToken);

    public async Task UploadPhotoAsync(
        int itemId,
        string fileName,
        byte[] fileBytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync($"/api/warehouse/v1/items/{itemId}/photos", content, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public async Task<int> RecomputeEmbeddingsAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsync(
            $"/api/warehouse/v1/items/{itemId}/photos/recompute-embeddings",
            null,
            cancellationToken);
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
        return result.TryGetProperty("updatedCount", out var prop) ? prop.GetInt32() : 0;
    }

    public async Task<ImageSearchResponseDto> SearchByImageAsync(
        string fileName,
        byte[] fileBytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync("/api/warehouse/v1/items/search-by-image", content, cancellationToken);
        await EnsureSuccessAsync(response);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ImageSearchResponseDto>(body, JsonOptions)
               ?? new ImageSearchResponseDto();
    }

    public Task<PagedApiResponse<AdminSupplierDto>> GetSuppliersAsync(
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        return GetAsync<PagedApiResponse<AdminSupplierDto>>(
            $"/api/warehouse/v1/suppliers?{string.Join("&", query)}",
            cancellationToken);
    }

    public Task CreateSupplierAsync(CreateOrUpdateSupplierRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, "/api/warehouse/v1/suppliers", request, cancellationToken);

    public Task UpdateSupplierAsync(int id, CreateOrUpdateSupplierRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Put, $"/api/warehouse/v1/suppliers/{id}", request, cancellationToken);

    public Task<PagedApiResponse<AdminSupplierMappingDto>> GetSupplierMappingsAsync(
        string? search,
        int? supplierId,
        int? itemId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (supplierId.HasValue)
        {
            query.Add($"supplierId={supplierId.Value}");
        }

        if (itemId.HasValue)
        {
            query.Add($"itemId={itemId.Value}");
        }

        return GetAsync<PagedApiResponse<AdminSupplierMappingDto>>(
            $"/api/warehouse/v1/supplier-item-mappings?{string.Join("&", query)}",
            cancellationToken);
    }

    public Task CreateSupplierMappingAsync(
        CreateOrUpdateSupplierMappingRequest request,
        CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, "/api/warehouse/v1/supplier-item-mappings", request, cancellationToken);

    public Task UpdateSupplierMappingAsync(
        int id,
        CreateOrUpdateSupplierMappingRequest request,
        CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Put, $"/api/warehouse/v1/supplier-item-mappings/{id}", request, cancellationToken);

    public Task<PagedApiResponse<AdminLocationDto>> GetLocationsAsync(
        string? search,
        string? status,
        bool includeVirtual,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"includeVirtual={includeVirtual.ToString().ToLowerInvariant()}",
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        return GetAsync<PagedApiResponse<AdminLocationDto>>(
            $"/api/warehouse/v1/locations?{string.Join("&", query)}",
            cancellationToken);
    }

    public Task CreateLocationAsync(CreateOrUpdateLocationRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, "/api/warehouse/v1/locations", request, cancellationToken);

    public Task UpdateLocationAsync(int id, CreateOrUpdateLocationRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Put, $"/api/warehouse/v1/locations/{id}", request, cancellationToken);

    public Task<PagedApiResponse<AdminWarehouseDto>> GetWarehousesAsync(
        string? search,
        string? status,
        bool includeVirtual,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"includeVirtual={includeVirtual.ToString().ToLowerInvariant()}",
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        return GetAsync<PagedApiResponse<AdminWarehouseDto>>(
            $"/api/warehouse/v1/warehouses?{string.Join("&", query)}",
            cancellationToken);
    }

    public Task CreateWarehouseAsync(
        CreateOrUpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, "/api/warehouse/v1/warehouses", request, cancellationToken);

    public Task UpdateWarehouseAsync(
        Guid id,
        CreateOrUpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Put, $"/api/warehouse/v1/warehouses/{id}", request, cancellationToken);

    public Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<AdminCategoryDto>>("/api/warehouse/v1/categories", cancellationToken);

    public Task CreateCategoryAsync(CreateOrUpdateCategoryRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Post, "/api/warehouse/v1/categories", request, cancellationToken);

    public Task UpdateCategoryAsync(int id, CreateOrUpdateCategoryRequest request, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Put, $"/api/warehouse/v1/categories/{id}", request, cancellationToken);

    public async Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.DeleteAsync($"/api/warehouse/v1/categories/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public async Task<byte[]> DownloadTemplateAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync($"/api/warehouse/v1/admin/import/{entityType}/template", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<ImportExecutionResultDto> ImportAsync(
        string entityType,
        string fileName,
        byte[] fileBytes,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        content.Add(fileContent, "file", fileName);

        var response = await client.PostAsync(
            $"/api/warehouse/v1/admin/import/{entityType}?dryRun={dryRun.ToString().ToLowerInvariant()}",
            content,
            cancellationToken);

        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var model = JsonSerializer.Deserialize<ImportExecutionResultDto>(body, JsonOptions);
        return model ?? new ImportExecutionResultDto();
    }

    public async Task<byte[]> DownloadErrorReportAsync(
        IReadOnlyList<ImportErrorDto> errors,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(
            "/api/warehouse/v1/admin/import/error-report",
            errors,
            cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
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

    private async Task SendNoContentAsync(
        HttpMethod method,
        string relativeUrl,
        object? payload,
        CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        using var request = new HttpRequestMessage(method, relativeUrl);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await ProblemDetailsParser.ParseAsync(response);
        _logger.LogError(
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
