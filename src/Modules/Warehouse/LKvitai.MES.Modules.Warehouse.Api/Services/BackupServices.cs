using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface IBackupService
{
    Task<BackupExecutionDto> TriggerBackupAsync(string trigger, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupExecutionDto>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<BackupRestoreResultDto> RestoreAsync(Guid backupId, string targetEnvironment, CancellationToken cancellationToken = default);
    Task<BackupRestoreResultDto> RunMonthlyRestoreTestAsync(CancellationToken cancellationToken = default);
}

public sealed class BackupService : IBackupService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISecurityAuditLogService _auditLogService;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        WarehouseDbContext dbContext,
        ICurrentUserService currentUserService,
        ISecurityAuditLogService auditLogService,
        ILogger<BackupService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<BackupExecutionDto> TriggerBackupAsync(string trigger, CancellationToken cancellationToken = default)
    {
        var execution = new BackupExecution
        {
            BackupStartedAt = DateTimeOffset.UtcNow,
            Type = BackupType.Full,
            Status = BackupExecutionStatus.Pending,
            Trigger = string.IsNullOrWhiteSpace(trigger) ? "MANUAL" : trigger.Trim().ToUpperInvariant(),
            BlobPath = string.Empty
        };

        _dbContext.BackupExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var filePath = await GenerateBackupArtifactAsync(execution.Id, cancellationToken);
            var fileInfo = new FileInfo(filePath);

            execution.BlobPath = filePath;
            execution.BackupSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
            execution.Status = BackupExecutionStatus.Completed;
            execution.BackupCompletedAt = DateTimeOffset.UtcNow;
            execution.ErrorMessage = null;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync("BACKUP_COMPLETED", execution.Id.ToString(), new
            {
                execution.Trigger,
                execution.BlobPath,
                execution.BackupSizeBytes
            }, cancellationToken);

            return ToDto(execution);
        }
        catch (Exception ex)
        {
            execution.Status = BackupExecutionStatus.Failed;
            execution.BackupCompletedAt = DateTimeOffset.UtcNow;
            execution.ErrorMessage = ex.Message;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await WriteAuditAsync("BACKUP_FAILED", execution.Id.ToString(), new
            {
                execution.Trigger,
                Error = ex.Message
            }, cancellationToken);

            _logger.LogError(ex, "Backup execution failed for {ExecutionId}", execution.Id);
            return ToDto(execution);
        }
    }

    public async Task<IReadOnlyList<BackupExecutionDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BackupExecutions
            .AsNoTracking()
            .OrderByDescending(x => x.BackupStartedAt)
            .Take(200)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<BackupRestoreResultDto> RestoreAsync(Guid backupId, string targetEnvironment, CancellationToken cancellationToken = default)
    {
        var backup = await _dbContext.BackupExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == backupId, cancellationToken);

        if (backup is null)
        {
            return new BackupRestoreResultDto(false, "Backup not found.");
        }

        if (backup.Status != BackupExecutionStatus.Completed)
        {
            return new BackupRestoreResultDto(false, "Backup is not in COMPLETED state.");
        }

        var restoreLogPath = await GenerateRestoreEvidenceAsync(backup, targetEnvironment, cancellationToken);

        await WriteAuditAsync("BACKUP_RESTORE_EXECUTED", backupId.ToString(), new
        {
            TargetEnvironment = targetEnvironment,
            RestoreLogPath = restoreLogPath
        }, cancellationToken);

        return new BackupRestoreResultDto(true, "Restore command sequence generated.", restoreLogPath);
    }

    public async Task<BackupRestoreResultDto> RunMonthlyRestoreTestAsync(CancellationToken cancellationToken = default)
    {
        var latest = await _dbContext.BackupExecutions
            .AsNoTracking()
            .Where(x => x.Status == BackupExecutionStatus.Completed)
            .OrderByDescending(x => x.BackupStartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return new BackupRestoreResultDto(false, "No completed backup is available for restore test.");
        }

        return await RestoreAsync(latest.Id, "test", cancellationToken);
    }

    private static async Task<string> GenerateBackupArtifactAsync(Guid backupId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var backupsDir = Path.Combine("artifacts", "backups");
        Directory.CreateDirectory(backupsDir);

        var fileName = $"warehouse-{now:yyyyMMdd-HHmmss}-{backupId:N}.sql.gz";
        var fullPath = Path.Combine(backupsDir, fileName);

        var payload = $"-- Simulated pg_dump backup\n-- BackupId: {backupId}\n-- GeneratedAtUtc: {now:O}\n";

        await using var file = File.Create(fullPath);
        await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        await using var writer = new StreamWriter(gzip, Encoding.UTF8);
        await writer.WriteAsync(payload.AsMemory(), cancellationToken);

        return fullPath;
    }

    private static async Task<string> GenerateRestoreEvidenceAsync(BackupExecution backup, string targetEnvironment, CancellationToken cancellationToken)
    {
        var dir = Path.Combine("artifacts", "restores");
        Directory.CreateDirectory(dir);

        var fileName = $"restore-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{backup.Id:N}.log";
        var path = Path.Combine(dir, fileName);

        var payload = $"Restore simulation\nBackup: {backup.BlobPath}\nTarget: {targetEnvironment}\nAtUtc: {DateTimeOffset.UtcNow:O}\n";
        await File.WriteAllTextAsync(path, payload, cancellationToken);

        return path;
    }

    private static BackupExecutionDto ToDto(BackupExecution x)
    {
        return new BackupExecutionDto(
            x.Id,
            x.BackupStartedAt,
            x.BackupCompletedAt,
            x.Type.ToString().ToUpperInvariant(),
            x.BackupSizeBytes,
            x.BlobPath,
            x.Status.ToString().ToUpperInvariant(),
            x.ErrorMessage,
            x.Trigger);
    }

    private async Task WriteAuditAsync(string action, string resourceId, object payload, CancellationToken cancellationToken)
    {
        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                _currentUserService.GetCurrentUserId(),
                action,
                "BACKUP",
                resourceId,
                "system",
                "backup-service",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(payload)),
            cancellationToken);
    }
}

public sealed class DailyBackupRecurringJob
{
    private readonly IBackupService _backupService;

    public DailyBackupRecurringJob(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _backupService.TriggerBackupAsync("SCHEDULED", cancellationToken);
    }
}

public sealed class MonthlyRestoreTestRecurringJob
{
    private readonly IBackupService _backupService;

    public MonthlyRestoreTestRecurringJob(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public Task<BackupRestoreResultDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _backupService.RunMonthlyRestoreTestAsync(cancellationToken);
    }
}

public sealed record BackupExecutionDto(
    Guid Id,
    DateTimeOffset BackupStartedAt,
    DateTimeOffset? BackupCompletedAt,
    string Type,
    long BackupSizeBytes,
    string BlobPath,
    string Status,
    string? ErrorMessage,
    string Trigger);

public sealed record BackupRestoreResultDto(
    bool Success,
    string Message,
    string? EvidencePath = null);
