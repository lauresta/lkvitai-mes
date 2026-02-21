using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Integration.LabelPrinting;

/// <summary>
/// Label printing service interface per blueprint
/// Operational integration (<5s SLA)
/// </summary>
public interface ILabelPrintingService
{
    Task<Result> PrintLabel(Guid handlingUnitId, CancellationToken ct = default);
    Task<Result> ReprintLabel(Guid handlingUnitId, CancellationToken ct = default);
}
