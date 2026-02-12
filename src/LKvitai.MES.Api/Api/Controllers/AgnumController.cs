using Hangfire;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/agnum")]
public sealed class AgnumController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;
    private readonly AgnumExportRecurringJob _agnumExportRecurringJob;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IAgnumSecretProtector _secretProtector;
    private readonly IHttpClientFactory _httpClientFactory;

    public AgnumController(
        WarehouseDbContext dbContext,
        AgnumExportRecurringJob agnumExportRecurringJob,
        IRecurringJobManager recurringJobManager,
        IAgnumSecretProtector secretProtector,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _agnumExportRecurringJob = agnumExportRecurringJob;
        _recurringJobManager = recurringJobManager;
        _secretProtector = secretProtector;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("config")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AgnumExportConfigs
            .AsNoTracking()
            .Include(x => x.Mappings)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.UpdatedAt);
        var config = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            query,
            cancellationToken);

        if (config is null)
        {
            return Ok((AgnumConfigResponse?)null);
        }

        return Ok(new AgnumConfigResponse(
            config.Id,
            config.Scope.ToString(),
            config.Schedule,
            config.Format.ToString(),
            config.ApiEndpoint,
            !string.IsNullOrWhiteSpace(config.ApiKey),
            config.IsActive,
            config.UpdatedAt,
            config.Mappings
                .OrderBy(x => x.SourceType)
                .ThenBy(x => x.SourceValue)
                .Select(x => new AgnumMappingResponse(x.Id, x.SourceType, x.SourceValue, x.AgnumAccountCode))
                .ToList()));
    }

    [HttpPut("config")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> PutConfigAsync(
        [FromBody] PutAgnumConfigRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        if (!Enum.TryParse<AgnumExportScope>(request.Scope, true, out var scope))
        {
            return ValidationFailure($"Invalid scope '{request.Scope}'.");
        }

        if (!Enum.TryParse<AgnumExportFormat>(request.Format, true, out var format))
        {
            return ValidationFailure($"Invalid format '{request.Format}'.");
        }

        if (string.IsNullOrWhiteSpace(request.Schedule))
        {
            return ValidationFailure("Schedule is required.");
        }

        var configQuery = _dbContext.AgnumExportConfigs
            .Include(x => x.Mappings)
            .AsQueryable();
        var config = request.ConfigId.HasValue
            ? await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                configQuery,
                x => x.Id == request.ConfigId.Value,
                cancellationToken)
            : await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                configQuery.Where(x => x.IsActive).OrderByDescending(x => x.UpdatedAt),
                cancellationToken);

        if (config is null)
        {
            config = new AgnumExportConfig();
            _dbContext.AgnumExportConfigs.Add(config);
        }

        config.Scope = scope;
        config.Schedule = request.Schedule.Trim();
        config.Format = format;
        config.ApiEndpoint = string.IsNullOrWhiteSpace(request.ApiEndpoint) ? null : request.ApiEndpoint.Trim();
        config.IsActive = request.IsActive;
        config.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            config.ApiKey = _secretProtector.Protect(request.ApiKey.Trim());
        }

        config.Mappings.Clear();
        foreach (var mapping in request.Mappings ?? Array.Empty<PutAgnumMappingRequest>())
        {
            if (string.IsNullOrWhiteSpace(mapping.SourceType) ||
                string.IsNullOrWhiteSpace(mapping.SourceValue) ||
                string.IsNullOrWhiteSpace(mapping.AgnumAccountCode))
            {
                return ValidationFailure("Each mapping requires sourceType, sourceValue, and agnumAccountCode.");
            }

            config.Mappings.Add(new AgnumMapping
            {
                SourceType = mapping.SourceType.Trim().ToUpperInvariant(),
                SourceValue = mapping.SourceValue.Trim(),
                AgnumAccountCode = mapping.AgnumAccountCode.Trim().ToUpperInvariant()
            });
        }

        if (scope != AgnumExportScope.TotalOnly && config.Mappings.Count == 0)
        {
            return ValidationFailure($"At least 1 mapping required for scope {scope.ToString().ToUpperInvariant()}.");
        }

        try
        {
            _recurringJobManager.AddOrUpdate<AgnumExportRecurringJob>(
                AgnumRecurringJobs.DailyExportJobId,
                x => x.ExecuteAsync("SCHEDULED", null, 0),
                config.Schedule,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                });
        }
        catch (Exception ex)
        {
            return ValidationFailure($"Invalid schedule: {ex.Message}");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AgnumConfigSavedResponse(config.Id, config.Schedule, config.Format.ToString(), config.Mappings.Count));
    }

    [HttpPost("test-connection")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> TestConnectionAsync(
        [FromBody] TestAgnumConnectionRequest? request,
        CancellationToken cancellationToken = default)
    {
        var activeConfig = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _dbContext.AgnumExportConfigs
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.UpdatedAt),
            cancellationToken);

        var endpoint = request?.ApiEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = activeConfig?.ApiEndpoint;
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return ValidationFailure("ApiEndpoint is required for connection test.");
        }

        endpoint = endpoint.Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return ValidationFailure("ApiEndpoint must be a valid absolute URL.");
        }

        var apiKey = request?.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = _secretProtector.Unprotect(activeConfig?.ApiKey);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var client = _httpClientFactory.CreateClient("AgnumExportApi");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpointUri);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    apiKey.Trim());
            }

            using var response = await client.SendAsync(httpRequest, timeoutCts.Token);
            if (response.IsSuccessStatusCode)
            {
                return Ok(new AgnumTestConnectionResponse(true, "Connection successful"));
            }

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                return StatusCode(StatusCodes.Status400BadRequest, new AgnumTestConnectionResponse(
                    false,
                    "Connection failed: Invalid API key"));
            }

            return StatusCode(StatusCodes.Status400BadRequest, new AgnumTestConnectionResponse(
                false,
                $"Connection failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}"));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new AgnumTestConnectionResponse(
                false,
                "Connection failed: request timed out"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new AgnumTestConnectionResponse(
                false,
                $"Connection failed: {ex.Message}"));
        }
    }

    [HttpPost("export")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> TriggerExportAsync()
    {
        var result = await _agnumExportRecurringJob.ExecuteAsync("MANUAL", null, 0);

        if (result.IsSuccess)
        {
            return Ok(new AgnumExportTriggerResponse(
                result.HistoryId,
                result.ExportNumber,
                result.Status.ToString().ToUpperInvariant(),
                result.RowCount,
                result.FilePath,
                null));
        }

        if (result.Status == AgnumExportStatus.Retrying)
        {
            return StatusCode(StatusCodes.Status202Accepted, new AgnumExportTriggerResponse(
                result.HistoryId,
                result.ExportNumber,
                result.Status.ToString().ToUpperInvariant(),
                result.RowCount,
                result.FilePath,
                result.ErrorMessage));
        }

        return Failure(Result.Fail(
            DomainErrorCodes.InternalError,
            result.ErrorMessage ?? "Agnum export failed."));
    }

    [HttpGet("history")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AgnumExportHistories
            .AsNoTracking()
            .OrderByDescending(x => x.ExportedAt)
            .Take(200);

        var rows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            query,
            cancellationToken);

        return Ok(rows.Select(x => new AgnumExportHistoryResponse(
            x.Id,
            x.ExportConfigId,
            x.ExportNumber,
            x.ExportedAt,
            x.Status.ToString().ToUpperInvariant(),
            x.RowCount,
            x.FilePath,
            x.ErrorMessage,
            x.RetryCount,
            x.Trigger)));
    }

    [HttpGet("history/{exportId:guid}")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> GetHistoryByIdAsync(
        Guid exportId,
        CancellationToken cancellationToken = default)
    {
        var history = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _dbContext.AgnumExportHistories.AsNoTracking(),
            x => x.Id == exportId,
            cancellationToken);

        if (history is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Agnum export history '{exportId}' not found."));
        }

        return Ok(new AgnumExportHistoryResponse(
            history.Id,
            history.ExportConfigId,
            history.ExportNumber,
            history.ExportedAt,
            history.Status.ToString().ToUpperInvariant(),
            history.RowCount,
            history.FilePath,
            history.ErrorMessage,
            history.RetryCount,
            history.Trigger));
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    public sealed record PutAgnumConfigRequest(
        Guid? ConfigId,
        string Scope,
        string Schedule,
        string Format,
        string? ApiEndpoint,
        string? ApiKey,
        bool IsActive,
        IReadOnlyList<PutAgnumMappingRequest> Mappings);

    public sealed record PutAgnumMappingRequest(
        string SourceType,
        string SourceValue,
        string AgnumAccountCode);

    public sealed record TestAgnumConnectionRequest(
        string? ApiEndpoint,
        string? ApiKey);

    public sealed record AgnumMappingResponse(
        Guid Id,
        string SourceType,
        string SourceValue,
        string AgnumAccountCode);

    public sealed record AgnumConfigResponse(
        Guid Id,
        string Scope,
        string Schedule,
        string Format,
        string? ApiEndpoint,
        bool ApiKeyConfigured,
        bool IsActive,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<AgnumMappingResponse> Mappings);

    public sealed record AgnumConfigSavedResponse(
        Guid Id,
        string Schedule,
        string Format,
        int MappingCount);

    public sealed record AgnumTestConnectionResponse(
        bool Success,
        string Message);

    public sealed record AgnumExportTriggerResponse(
        Guid? HistoryId,
        string ExportNumber,
        string Status,
        int RowCount,
        string? FilePath,
        string? ErrorMessage);

    public sealed record AgnumExportHistoryResponse(
        Guid Id,
        Guid ExportConfigId,
        string ExportNumber,
        DateTimeOffset ExportedAt,
        string Status,
        int RowCount,
        string? FilePath,
        string? ErrorMessage,
        int RetryCount,
        string Trigger);
}
