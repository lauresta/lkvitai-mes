namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

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

public sealed class UpsertRetentionPolicyRequestDto
{
    public string DataType { get; set; } = "AuditLogs";
    public int RetentionPeriodDays { get; set; }
    public int? ArchiveAfterDays { get; set; }
    public int? DeleteAfterDays { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class LegalHoldRequestDto
{
    public bool LegalHold { get; set; }
}
