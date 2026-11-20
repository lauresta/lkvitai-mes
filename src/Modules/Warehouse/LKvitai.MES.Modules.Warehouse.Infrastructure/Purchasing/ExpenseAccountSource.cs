using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;

/// <summary>ExpenseAccount of every active additional cost type with IncludedInCost = false (blueprint 4.4 / 4.6).</summary>
public sealed class AdditionalCostExpenseAccountSource : IExpenseAccountSource
{
    private readonly WarehouseDbContext _db;

    public AdditionalCostExpenseAccountSource(WarehouseDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> GetExpenseAccountsAsync(CancellationToken ct)
        => await _db.AdditionalCostTypes.AsNoTracking()
            .Where(t => t.IsActive && !t.IncludedInCost && t.ExpenseAccount != null)
            .Select(t => t.ExpenseAccount!)
            .Distinct()
            .ToListAsync(ct);
}
