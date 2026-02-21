namespace LKvitai.MES.Contracts.Events;

public sealed class AgnumExportStartedEvent : DomainEvent
{
    public Guid ExportId { get; set; }
    public string ExportNumber { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
}

public sealed class AgnumExportCompletedEvent : DomainEvent
{
    public Guid ExportId { get; set; }
    public string ExportNumber { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string? FilePath { get; set; }
}

public sealed class AgnumExportFailedEvent : DomainEvent
{
    public Guid ExportId { get; set; }
    public string ExportNumber { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; }
}
