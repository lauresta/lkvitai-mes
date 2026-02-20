using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using Marten;

namespace LKvitai.MES.Infrastructure.Persistence;

public class MartenReservationReadModelQueryService : IReservationReadModelQueryService
{
    private static readonly string AllocatedStatus = ReservationStatus.ALLOCATED.ToString();
    private static readonly string PickingStatus = ReservationStatus.PICKING.ToString();

    private readonly IDocumentStore _store;

    public MartenReservationReadModelQueryService(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<int> CountReservationsAsync(string? normalizedStatus, CancellationToken cancellationToken)
    {
        await using var session = _store.QuerySession();
        var query = BuildSummaryQuery(session, normalizedStatus);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReservationSummaryView>> GetReservationPageAsync(
        string? normalizedStatus,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var session = _store.QuerySession();
        var query = BuildSummaryQuery(session, normalizedStatus);
        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.ReservationId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, HandlingUnitView>> GetHandlingUnitsAsync(
        IReadOnlyCollection<Guid> huIds,
        CancellationToken cancellationToken)
    {
        if (huIds.Count == 0)
        {
            return new Dictionary<Guid, HandlingUnitView>();
        }

        await using var session = _store.QuerySession();
        var rows = await session.Query<HandlingUnitView>()
            .Where(x => huIds.Contains(x.HuId))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.HuId)
            .ToDictionary(x => x.Key, x => x.First());
    }

    private static IQueryable<ReservationSummaryView> BuildSummaryQuery(IQuerySession session, string? normalizedStatus)
    {
        var query = session.Query<ReservationSummaryView>();
        return normalizedStatus is null
            ? query.Where(x => x.Status == AllocatedStatus || x.Status == PickingStatus)
            : query.Where(x => x.Status == normalizedStatus);
    }
}
