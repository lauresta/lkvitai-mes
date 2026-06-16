using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure.Repositories;

public sealed class WorkStationRepository : IWorkStationRepository
{
    private readonly ShopfloorDbContext _db;

    public WorkStationRepository(ShopfloorDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkStationWithCenter>> ListAsync(CancellationToken cancellationToken)
    {
        // Filtering/ordering must run against the joined entities (anonymous
        // type is transparent to EF). Composing operators over a record-
        // constructor projection is untranslatable, so the record is built
        // client-side after materialization.
        var rows = await (
            from station in _db.WorkStations.AsNoTracking()
            join center in _db.WorkCenters.AsNoTracking() on station.WorkCenterId equals center.Id
            orderby station.Code
            select new { Station = station, CenterName = center.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(x => new WorkStationWithCenter(x.Station, x.CenterName)).ToList();
    }

    public async Task<IReadOnlyList<WorkStationWithCenter>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from station in _db.WorkStations.AsNoTracking()
            join center in _db.WorkCenters.AsNoTracking() on station.WorkCenterId equals center.Id
            where station.IsActive
            orderby station.Code
            select new { Station = station, CenterName = center.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(x => new WorkStationWithCenter(x.Station, x.CenterName)).ToList();
    }

    public async Task<WorkStationWithCenter?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var station = await _db.WorkStations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (station is null)
        {
            return null;
        }

        var centerName = await _db.WorkCenters.AsNoTracking()
            .Where(c => c.Id == station.WorkCenterId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new WorkStationWithCenter(station, centerName ?? string.Empty);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = code.Trim();
        var query = _db.WorkStations.Where(x => x.Code == normalized);
        if (excludeId is { } id)
        {
            query = query.Where(x => x.Id != id);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetExistingIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return await _db.WorkStations.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(WorkStation workStation) => _db.WorkStations.Add(workStation);

    public void Remove(WorkStation workStation) => _db.WorkStations.Remove(workStation);
}
