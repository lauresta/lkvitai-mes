using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class BackupsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<BackupsClient>? _logger;

    public BackupsClient(IHttpClientFactory factory, ILogger<BackupsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<BackupExecutionDto> TriggerBackupAsync(CancellationToken cancellationToken = default)
        => PostAsync<BackupExecutionDto>("/api/warehouse/v1/admin/backups/trigger", new { }, cancellationToken);

    public Task<IReadOnlyList<BackupExecutionDto>> GetBackupsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<BackupExecutionDto>>("/api/warehouse/v1/admin/backups", cancellationToken);

    public Task<BackupRestoreResultDto> RestoreAsync(Guid backupId, string targetEnvironment, CancellationToken cancellationToken = default)
        => PostAsync<BackupRestoreResultDto>(
            "/api/warehouse/v1/admin/backups/restore",
            new RestoreBackupRequestDto
            {
                BackupId = backupId,
                TargetEnvironment = targetEnvironment
            },
            cancellationToken);

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
            "Backups API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");

        throw new ApiException(problem, (int)response.StatusCode);
    }
}
