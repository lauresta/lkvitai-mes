using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Repositories;

public sealed class WorkCenterRepository : IWorkCenterRepository
{
    private readonly ShopfloorDbContext _db;

    public WorkCenterRepository(ShopfloorDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkCenter>> ListAsync(CancellationToken cancellationToken)
        => await _db.WorkCenters.AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<WorkCenter?> GetAsync(Guid id, CancellationToken cancellationToken)
        => _db.WorkCenters.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        var query = _db.WorkCenters.Where(x => x.Code == normalized);
        if (excludeId is { } id)
        {
            query = query.Where(x => x.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasStationsAsync(Guid workCenterId, CancellationToken cancellationToken)
        => _db.WorkStations.AnyAsync(x => x.WorkCenterId == workCenterId, cancellationToken);

    public void Add(WorkCenter workCenter) => _db.WorkCenters.Add(workCenter);

    public void Remove(WorkCenter workCenter) => _db.WorkCenters.Remove(workCenter);
}
