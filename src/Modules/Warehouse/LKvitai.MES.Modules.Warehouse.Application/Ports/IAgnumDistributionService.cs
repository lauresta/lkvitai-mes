namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

public interface IAgnumDistributionService
{
    Task<DistributionSummary> GetVirtualBalanceSummaryAsync(Guid virtualBalanceId, CancellationToken ct = default);
}

public record DistributionSummary(
    Guid VirtualBalanceId,
    decimal TotalQty,
    decimal DistributedQty,
    decimal RemainingQty,
    IReadOnlyList<AgnumBalanceDistributionDto> Distributions);

public record AgnumBalanceDistributionDto(
    Guid Id,
    Guid VirtualBalanceId,
    int SndId,
    int AgnumProductId,
    string Sku,
    string LocationCode,
    string WarehouseId,
    decimal Quantity,
    Guid StockMovementCommandId,
    DateTime DistributedAt,
    string DistributedBy);
