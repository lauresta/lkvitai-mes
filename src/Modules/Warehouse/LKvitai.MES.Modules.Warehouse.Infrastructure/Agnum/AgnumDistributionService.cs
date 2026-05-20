using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumDistributionService : IAgnumDistributionService
{
    private readonly WarehouseDbContext _dbContext;

    public AgnumDistributionService(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DistributionSummary> GetVirtualBalanceSummaryAsync(Guid virtualBalanceId, CancellationToken ct = default)
    {
        var balance = await _dbContext.AgnumVirtualWarehouseBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == virtualBalanceId, ct);

        if (balance is null)
        {
            throw new KeyNotFoundException($"Agnum virtual balance '{virtualBalanceId}' was not found.");
        }

        var distributions = await _dbContext.AgnumBalanceDistributions
            .AsNoTracking()
            .Where(x => x.VirtualBalanceId == virtualBalanceId)
            .OrderByDescending(x => x.DistributedAt)
            .Select(x => new AgnumBalanceDistributionDto(
                x.Id,
                x.VirtualBalanceId,
                x.SndId,
                x.AgnumProductId,
                x.Sku,
                x.LocationCode,
                x.WarehouseId,
                x.Quantity,
                x.StockMovementCommandId,
                x.DistributedAt,
                x.DistributedBy))
            .ToListAsync(ct);

        var distributedQty = distributions.Sum(x => x.Quantity);
        return new DistributionSummary(
            balance.Id,
            balance.Quantity,
            distributedQty,
            balance.Quantity - distributedQty,
            distributions);
    }
}
