using System.Text.Json;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public interface IRetentionPolicyService
{
    Task<IReadOnlyList<RetentionPolicyDto>> GetAsync(CancellationToken cancellationToken = default);
    Task<RetentionPolicyDto> CreateAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task<RetentionPolicyDto?> UpdateAsync(int id, UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<RetentionExecutionDto> ExecuteAsync(CancellationToken cancellationToken = default);
    Task<bool> SetAuditLogLegalHoldAsync(long id, bool legalHold, CancellationToken cancellationToken = default);
}

public sealed class RetentionPolicyService : IRetentionPolicyService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISecurityAuditLogService _auditLogService;
    private readonly ILogger<RetentionPolicyService> _logger;

    public RetentionPolicyService(
        WarehouseDbContext dbContext,
        ICurrentUserService currentUserService,
        ISecurityAuditLogService auditLogService,
        ILogger<RetentionPolicyService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetentionPolicyDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.RetentionPolicies
            .AsNoTracking()
            .OrderBy(x => x.DataType)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<RetentionPolicyDto> CreateAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.DataType, request.RetentionPeriodDays, request.ArchiveAfterDays, request.DeleteAfterDays);

        var existing = await _dbContext.RetentionPolicies
            .AnyAsync(x => x.DataType == request.DataType, cancellationToken);

        if (existing)
        {
            throw new InvalidOperationException($"Retention policy for data type '{request.DataType}' already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new RetentionPolicy
        {
            DataType = request.DataType,
            RetentionPeriodDays = request.RetentionPeriodDays,
            ArchiveAfterDays = request.ArchiveAfterDays,
            DeleteAfterDays = request.DeleteAfterDays,
            Active = request.Active,
            CreatedBy = _currentUserService.GetCurrentUserId(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.RetentionPolicies.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync("RETENTION_POLICY_CREATED", entity.Id.ToString(), new
        {
            entity.DataType,
            entity.RetentionPeriodDays,
            entity.ArchiveAfterDays,
            entity.DeleteAfterDays,
            entity.Active
        }, cancellationToken);

        return ToDto(entity);
    }

    public async Task<RetentionPolicyDto?> UpdateAsync(int id, UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.DataType, request.RetentionPeriodDays, request.ArchiveAfterDays, request.DeleteAfterDays);

        var entity = await _dbContext.RetentionPolicies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var duplicateType = await _dbContext.RetentionPolicies
            .AnyAsync(x => x.Id != id && x.DataType == request.DataType, cancellationToken);

        if (duplicateType)
        {
            throw new InvalidOperationException($"Retention policy for data type '{request.DataType}' already exists.");
        }

        entity.DataType = request.DataType;
        entity.RetentionPeriodDays = request.RetentionPeriodDays;
        entity.ArchiveAfterDays = request.ArchiveAfterDays;
        entity.DeleteAfterDays = request.DeleteAfterDays;
        entity.Active = request.Active;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync("RETENTION_POLICY_UPDATED", entity.Id.ToString(), new
        {
            entity.DataType,
            entity.RetentionPeriodDays,
            entity.ArchiveAfterDays,
            entity.DeleteAfterDays,
            entity.Active
        }, cancellationToken);

        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RetentionPolicies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.RetentionPolicies.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync("RETENTION_POLICY_DELETED", id.ToString(), new { entity.DataType }, cancellationToken);
        return true;
    }

    public async Task<RetentionExecutionDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var execution = new RetentionExecution
        {
            ExecutedAt = DateTimeOffset.UtcNow,
            Status = RetentionExecutionStatus.Completed
        };

        try
        {
            var policy = await _dbContext.RetentionPolicies
                .AsNoTracking()
                .Where(x => x.Active && x.DataType == RetentionDataType.AuditLogs)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (policy is not null)
            {
                execution.RecordsArchived = await ArchiveAuditLogsAsync(policy, cancellationToken);
                execution.RecordsDeleted = await DeleteExpiredArchivedAuditLogsAsync(policy, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            execution.Status = RetentionExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Retention execution failed");
        }

        _dbContext.RetentionExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync("RETENTION_EXECUTED", execution.Id.ToString(), new
        {
            execution.Status,
            execution.RecordsArchived,
            execution.RecordsDeleted,
            execution.ErrorMessage
        }, cancellationToken);

        return new RetentionExecutionDto(
            execution.Id,
            execution.ExecutedAt,
            execution.RecordsArchived,
            execution.RecordsDeleted,
            execution.Status.ToString().ToUpperInvariant(),
            execution.ErrorMessage);
    }

    public async Task<bool> SetAuditLogLegalHoldAsync(long id, bool legalHold, CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.SecurityAuditLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (log is null)
        {
            var archived = await _dbContext.AuditLogArchives.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (archived is null)
            {
                return false;
            }

            archived.LegalHold = legalHold;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync("RETENTION_LEGAL_HOLD_UPDATED", id.ToString(), new { legalHold, archived = true }, cancellationToken);
            return true;
        }

        log.LegalHold = legalHold;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("RETENTION_LEGAL_HOLD_UPDATED", id.ToString(), new { legalHold, archived = false }, cancellationToken);
        return true;
    }

    private async Task<int> ArchiveAuditLogsAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        if (!policy.ArchiveAfterDays.HasValue)
        {
            return 0;
        }

        var archiveCutoff = DateTimeOffset.UtcNow.AddDays(-policy.ArchiveAfterDays.Value);

        var rows = await _dbContext.SecurityAuditLogs
            .Where(x => !x.LegalHold && x.Timestamp <= archiveCutoff)
            .OrderBy(x => x.Id)
            .Take(5000)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return 0;
        }

        var archived = rows.Select(x => new AuditLogArchive
        {
            Id = x.Id,
            UserId = x.UserId,
            Action = x.Action,
            Resource = x.Resource,
            ResourceId = x.ResourceId,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            Timestamp = x.Timestamp,
            LegalHold = x.LegalHold,
            Details = x.Details,
            ArchivedAt = DateTimeOffset.UtcNow
        });

        _dbContext.AuditLogArchives.AddRange(archived);
        _dbContext.SecurityAuditLogs.RemoveRange(rows);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return rows.Count;
    }

    private async Task<int> DeleteExpiredArchivedAuditLogsAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        var deleteAfterDays = policy.DeleteAfterDays ?? policy.RetentionPeriodDays;
        var deleteCutoff = DateTimeOffset.UtcNow.AddDays(-deleteAfterDays);

        var rows = await _dbContext.AuditLogArchives
            .Where(x => !x.LegalHold && x.Timestamp <= deleteCutoff)
            .OrderBy(x => x.Id)
            .Take(5000)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return 0;
        }

        _dbContext.AuditLogArchives.RemoveRange(rows);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private static void ValidateRequest(
        RetentionDataType dataType,
        int retentionPeriodDays,
        int? archiveAfterDays,
        int? deleteAfterDays)
    {
        _ = dataType;

        if (retentionPeriodDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionPeriodDays), "RetentionPeriodDays must be greater than zero.");
        }

        if (archiveAfterDays.HasValue && archiveAfterDays.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(archiveAfterDays), "ArchiveAfterDays cannot be negative.");
        }

        if (deleteAfterDays.HasValue && deleteAfterDays.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deleteAfterDays), "DeleteAfterDays cannot be negative.");
        }

        if (archiveAfterDays.HasValue && archiveAfterDays.Value > retentionPeriodDays)
        {
            throw new ArgumentException("ArchiveAfterDays cannot be greater than RetentionPeriodDays.", nameof(archiveAfterDays));
        }
    }

    private async Task WriteAuditAsync(string action, string resourceId, object payload, CancellationToken cancellationToken)
    {
        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                _currentUserService.GetCurrentUserId(),
                action,
                "RETENTION",
                resourceId,
                "system",
                "retention-policy-service",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(payload)),
            cancellationToken);
    }

    private static RetentionPolicyDto ToDto(RetentionPolicy row)
    {
        return new RetentionPolicyDto(
            row.Id,
            row.DataType.ToString().ToUpperInvariant(),
            row.RetentionPeriodDays,
            row.ArchiveAfterDays,
            row.DeleteAfterDays,
            row.Active,
            row.CreatedBy,
            row.CreatedAt,
            row.UpdatedAt);
    }
}

public sealed class RetentionPolicyRecurringJob
{
    private readonly IRetentionPolicyService _service;

    public RetentionPolicyRecurringJob(IRetentionPolicyService service)
    {
        _service = service;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _service.ExecuteAsync(cancellationToken);
    }
}

public sealed record CreateRetentionPolicyRequest(
    RetentionDataType DataType,
    int RetentionPeriodDays,
    int? ArchiveAfterDays,
    int? DeleteAfterDays,
    bool Active);

public sealed record UpdateRetentionPolicyRequest(
    RetentionDataType DataType,
    int RetentionPeriodDays,
    int? ArchiveAfterDays,
    int? DeleteAfterDays,
    bool Active);

public sealed record RetentionPolicyDto(
    int Id,
    string DataType,
    int RetentionPeriodDays,
    int? ArchiveAfterDays,
    int? DeleteAfterDays,
    bool Active,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record RetentionExecutionDto(
    Guid Id,
    DateTimeOffset ExecutedAt,
    int RecordsArchived,
    int RecordsDeleted,
    string Status,
    string? ErrorMessage);
