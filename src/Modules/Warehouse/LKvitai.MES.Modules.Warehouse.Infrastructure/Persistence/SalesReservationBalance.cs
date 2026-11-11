using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using Marten;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

public static class SalesReservationBalance
{
    public const string Purpose = "SALES_ORDER";

    public static List<AllocationDto> Remaining(IEnumerable<object> events)
    {
        var result = new List<AllocationDto>();
        foreach (var evt in events)
        {
            if (evt is StockAllocatedEvent { SalesOrderReservation: true } allocated)
                result.AddRange(allocated.Allocations.Select(x => new AllocationDto
                { SKU = x.SKU, Location = x.Location, WarehouseId = x.WarehouseId, Quantity = x.Quantity }));
            var releases = evt switch
            {
                ReservationConsumedEvent consumed => consumed.ReleasedSoftLines,
                ReservationCancelledEvent cancelled => cancelled.ReleasedSoftLines,
                _ => new List<AllocationDto>()
            };
            foreach (var release in releases)
            {
                var remaining = release.Quantity;
                foreach (var line in result.Where(x => x.WarehouseId == release.WarehouseId && x.Location == release.Location &&
                    string.Equals(x.SKU, release.SKU, StringComparison.OrdinalIgnoreCase)))
                {
                    var take = Math.Min(remaining, line.Quantity);
                    line.Quantity -= take;
                    remaining -= take;
                }
            }
        }
        return result.Where(x => x.Quantity > 0).ToList();
    }

    public static async Task<List<AllocationDto>> LoadAsync(IQuerySession session, Guid reservationId, CancellationToken ct)
        => Remaining((await session.Events.FetchStreamAsync(Reservation.StreamIdFor(reservationId), token: ct)).Select(x => x.Data));
}
