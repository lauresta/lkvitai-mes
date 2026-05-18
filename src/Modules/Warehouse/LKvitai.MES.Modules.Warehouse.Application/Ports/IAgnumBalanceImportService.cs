namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

public interface IAgnumBalanceImportService
{
    Task<Guid> StartImportAsync(int sndId, CancellationToken ct = default);
    Task<AgnumBalanceImportRunStatus> GetRunStatusAsync(Guid runId, CancellationToken ct = default);
}

public sealed class AgnumBalanceImportRunStatus
{
    public Guid RunId { get; init; }
    public int SndId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ProductCount { get; init; }
    public int BalanceCount { get; init; }
    public int ErrorCount { get; init; }
    public string? ErrorSummary { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
