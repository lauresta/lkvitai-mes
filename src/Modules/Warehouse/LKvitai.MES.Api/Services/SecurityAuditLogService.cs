using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public interface ISecurityAuditLogService
{
    Task WriteAsync(SecurityAuditLogWriteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityAuditLogDto>> QueryAsync(SecurityAuditLogQuery query, CancellationToken cancellationToken = default);
}

public sealed class SecurityAuditLogService : ISecurityAuditLogService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<SecurityAuditLogService> _logger;

    public SecurityAuditLogService(
        WarehouseDbContext dbContext,
        ILogger<SecurityAuditLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task WriteAsync(SecurityAuditLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new SecurityAuditLog
        {
            UserId = NormalizeOptional(request.UserId),
            Action = NormalizeRequired(request.Action, "UNKNOWN"),
            Resource = NormalizeRequired(request.Resource, "SYSTEM"),
            ResourceId = NormalizeOptional(request.ResourceId),
            IpAddress = NormalizeRequired(request.IpAddress, "unknown"),
            UserAgent = NormalizeRequired(request.UserAgent, "unknown"),
            Timestamp = request.Timestamp == default ? DateTimeOffset.UtcNow : request.Timestamp,
            Details = string.IsNullOrWhiteSpace(request.DetailsJson) ? "{}" : request.DetailsJson
        };

        _dbContext.SecurityAuditLogs.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Security audit log persisted. Action={Action}, Resource={Resource}, UserId={UserId}, ResourceId={ResourceId}",
            entity.Action,
            entity.Resource,
            entity.UserId ?? "anonymous",
            entity.ResourceId ?? string.Empty);
    }

    public async Task<IReadOnlyList<SecurityAuditLogDto>> QueryAsync(SecurityAuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = _dbContext.SecurityAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            var normalizedUser = query.UserId.Trim();
            logs = logs.Where(x => x.UserId == normalizedUser);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var normalizedAction = query.Action.Trim().ToUpperInvariant();
            logs = logs.Where(x => x.Action.ToUpper() == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(query.Resource))
        {
            var normalizedResource = query.Resource.Trim().ToUpperInvariant();
            logs = logs.Where(x => x.Resource.ToUpper() == normalizedResource);
        }

        if (query.StartDate.HasValue)
        {
            logs = logs.Where(x => x.Timestamp >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            logs = logs.Where(x => x.Timestamp <= query.EndDate.Value);
        }

        var limit = query.Limit is > 0 and <= 5000 ? query.Limit.Value : 500;

        return await logs
            .OrderByDescending(x => x.Timestamp)
            .Take(limit)
            .Select(x => new SecurityAuditLogDto(
                x.Id,
                x.UserId,
                x.Action,
                x.Resource,
                x.ResourceId,
                x.IpAddress,
                x.UserAgent,
                x.Timestamp,
                x.Details))
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeRequired(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}

public sealed record SecurityAuditLogWriteRequest(
    string? UserId,
    string Action,
    string Resource,
    string? ResourceId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset Timestamp,
    string DetailsJson);

public sealed record SecurityAuditLogQuery(
    string? UserId,
    string? Action,
    string? Resource,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    int? Limit);

public sealed record SecurityAuditLogDto(
    long Id,
    string? UserId,
    string Action,
    string Resource,
    string? ResourceId,
    string IpAddress,
    string UserAgent,
    DateTimeOffset Timestamp,
    string DetailsJson);
