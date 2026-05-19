namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

public interface IAgnumNomenclatureImportService
{
    Task<AgnumImportPreview> PreviewAsync(int sndId, CancellationToken ct = default);
    Task<AgnumImportResult> ApplyAsync(int sndId, CancellationToken ct = default, bool importPartners = true);
}

public sealed class AgnumImportPreview
{
    public int SndId { get; init; }
    public int TotalProducts { get; init; }
    public List<AgnumImportCandidate> ToCreate { get; init; } = new();
    public List<AgnumImportCandidate> ToUpdate { get; init; } = new();
    public List<AgnumImportConflict> Conflicts { get; init; } = new();
}

public sealed class AgnumImportCandidate
{
    public int AgnumProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Pcs { get; init; } = string.Empty;
}

public sealed class AgnumImportConflict
{
    public int AgnumProductId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class AgnumImportResult
{
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public List<AgnumImportConflict> Conflicts { get; init; } = new();
}
