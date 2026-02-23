namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

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

public sealed class RestoreBackupRequestDto
{
    public Guid BackupId { get; set; }
    public string TargetEnvironment { get; set; } = string.Empty;
}
