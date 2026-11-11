using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using Marten;
using Marten.Events;
using System.Data;
using System.Text.Json;

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
        => Remaining(await ReadEventsAsync(session, reservationId, ct));

    public static async Task<IReadOnlyList<object>> ReadEventsAsync(IQuerySession session, Guid reservationId, CancellationToken ct)
    {
        // Only payloads are needed. FetchStream also deserializes JSONB headers, which
        // Marten 7 / Npgsql 8 cannot read without a global dynamic-JSON mapping.
        var types = new[] { typeof(StockAllocatedEvent), typeof(ReservationConsumedEvent), typeof(ReservationCancelledEvent) };
        var aliases = types.SelectMany(type => new[] {
            new KeyValuePair<string, Type>(EventMappingExtensions.GetEventTypeName(type), type),
            new KeyValuePair<string, Type>($"LKvitai.MES.Contracts.Events.{type.Name}, LKvitai.MES.Contracts", type)
        }).ToDictionary(x => x.Key, x => x.Value);
        var connection = session.Connection ?? throw new InvalidOperationException("A Marten connection is required.");
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT type, data::text FROM warehouse_events.mt_events WHERE stream_id=@stream AND type=ANY(@aliases) AND is_archived=FALSE ORDER BY seq_id";
        command.Parameters.AddWithValue("stream", Reservation.StreamIdFor(reservationId));
        command.Parameters.AddWithValue("aliases", aliases.Keys.ToArray());
        await using var reader = await command.ExecuteReaderAsync(ct);
        var payloads = new List<object>();
        while (await reader.ReadAsync(ct))
            payloads.Add(JsonSerializer.Deserialize(reader.GetString(1), aliases[reader.GetString(0)])!);
        return payloads;
    }
}
