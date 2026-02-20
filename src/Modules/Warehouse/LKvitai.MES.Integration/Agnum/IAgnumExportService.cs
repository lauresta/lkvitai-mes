using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Integration.Agnum;

/// <summary>
/// Agnum export service interface per blueprint
/// Financial integration (minutes SLA)
/// </summary>
public interface IAgnumExportService
{
    Task<Result> ExportSnapshot(ExportMode mode, CancellationToken ct = default);
}

public enum ExportMode
{
    ByPhysicalWarehouse,
    ByLogicalWarehouse,
    ByCategory,
    TotalSum
}
