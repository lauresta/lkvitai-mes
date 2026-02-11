namespace LKvitai.MES.Contracts.Messages;

public sealed class StartAgnumExport
{
    public Guid CorrelationId { get; set; }
    public string Trigger { get; set; } = "SCHEDULED";
    public int RetryCount { get; set; }
}

public sealed class AgnumExportSucceeded
{
    public Guid CorrelationId { get; set; }
    public string ExportNumber { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string? FilePath { get; set; }
}

public sealed class AgnumExportFailed
{
    public Guid CorrelationId { get; set; }
    public string ExportNumber { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int RetryCount { get; set; }
}
