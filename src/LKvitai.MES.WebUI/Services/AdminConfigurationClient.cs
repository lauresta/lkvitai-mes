using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public sealed class AdminConfigurationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<AdminConfigurationClient>? _logger;

    public AdminConfigurationClient(IHttpClientFactory factory, ILogger<AdminConfigurationClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<WarehouseSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
        => GetAsync<WarehouseSettingsDto>("/api/warehouse/v1/admin/settings", cancellationToken);

    public Task<WarehouseSettingsDto> UpdateSettingsAsync(
        UpdateWarehouseSettingsRequestDto request,
        CancellationToken cancellationToken = default)
        => PutAsync<WarehouseSettingsDto>("/api/warehouse/v1/admin/settings", request, cancellationToken);

    public Task<IReadOnlyList<ReasonCodeDto>> GetReasonCodesAsync(
        string? category,
        bool? active,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query.Add($"category={Uri.EscapeDataString(category)}");
        }

        if (active.HasValue)
        {
            query.Add($"active={active.Value.ToString().ToLowerInvariant()}");
        }

        var url = query.Count > 0
            ? $"/api/warehouse/v1/admin/reason-codes?{string.Join("&", query)}"
            : "/api/warehouse/v1/admin/reason-codes";

        return GetAsync<IReadOnlyList<ReasonCodeDto>>(url, cancellationToken);
    }

    public Task<ReasonCodeDto> CreateReasonCodeAsync(
        UpsertReasonCodeRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync<ReasonCodeDto>("/api/warehouse/v1/admin/reason-codes", request, cancellationToken);

    public Task<ReasonCodeDto> UpdateReasonCodeAsync(
        int id,
        UpsertReasonCodeRequestDto request,
        CancellationToken cancellationToken = default)
        => PutAsync<ReasonCodeDto>($"/api/warehouse/v1/admin/reason-codes/{id}", request, cancellationToken);

    public async Task DeleteReasonCodeAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.DeleteAsync($"/api/warehouse/v1/admin/reason-codes/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<IReadOnlyList<ApprovalRuleDto>> GetApprovalRulesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ApprovalRuleDto>>("/api/warehouse/v1/admin/approval-rules", cancellationToken);

    public Task<ApprovalRuleDto> CreateApprovalRuleAsync(
        UpsertApprovalRuleRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync<ApprovalRuleDto>("/api/warehouse/v1/admin/approval-rules", request, cancellationToken);

    public Task<ApprovalRuleDto> UpdateApprovalRuleAsync(
        int id,
        UpsertApprovalRuleRequestDto request,
        CancellationToken cancellationToken = default)
        => PutAsync<ApprovalRuleDto>($"/api/warehouse/v1/admin/approval-rules/{id}", request, cancellationToken);

    public async Task DeleteApprovalRuleAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.DeleteAsync($"/api/warehouse/v1/admin/approval-rules/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<EvaluateApprovalRuleResponseDto> EvaluateApprovalRuleAsync(
        EvaluateApprovalRuleRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync<EvaluateApprovalRuleResponseDto>("/api/warehouse/v1/admin/approval-rules/evaluate", request, cancellationToken);

    public Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<RoleDto>>("/api/warehouse/v1/admin/roles", cancellationToken);

    public Task<RoleDto> CreateRoleAsync(UpsertRoleRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<RoleDto>("/api/warehouse/v1/admin/roles", request, cancellationToken);

    public Task<RoleDto> UpdateRoleAsync(int id, UpsertRoleRequestDto request, CancellationToken cancellationToken = default)
        => PutAsync<RoleDto>($"/api/warehouse/v1/admin/roles/{id}", request, cancellationToken);

    public async Task DeleteRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.DeleteAsync($"/api/warehouse/v1/admin/roles/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<UserRoleAssignmentDto> AssignRoleAsync(
        Guid userId,
        AssignUserRoleRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync<UserRoleAssignmentDto>($"/api/warehouse/v1/admin/users/{userId}/roles", request, cancellationToken);

    private Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.GetAsync(relativeUrl, cancellationToken);
        });

    private Task<T> PostAsync<T>(string relativeUrl, object payload, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.PostAsJsonAsync(relativeUrl, payload, cancellationToken);
        });

    private Task<T> PutAsync<T>(string relativeUrl, object payload, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.PutAsJsonAsync(relativeUrl, payload, cancellationToken);
        });

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        await EnsureSuccessAsync(response);

        var body = await response.Content.ReadAsStringAsync();
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
            "Admin config API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");

        throw new ApiException(problem, (int)response.StatusCode);
    }
}
