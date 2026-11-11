using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using Marten;
using Marten.Events;
using System.Data;
using System.Data.Common;
using Npgsql;
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

    private static readonly Dictionary<string, Type> EventTypes = new[] {
        typeof(StockAllocatedEvent), typeof(ReservationConsumedEvent), typeof(ReservationCancelledEvent)
    }.SelectMany(type => new[] {
        new KeyValuePair<string, Type>(EventMappingExtensions.GetEventTypeName(type), type),
        new KeyValuePair<string, Type>($"LKvitai.MES.Contracts.Events.{type.Name}, LKvitai.MES.Contracts", type)
    }).ToDictionary(x => x.Key, x => x.Value);

    public static List<AllocationDto> Load(IQuerySession session, Guid reservationId)
    {
        using var command = ReadCommand(session, reservationId);
        if (command.Connection!.State != ConnectionState.Open) command.Connection.Open();
        using var reader = command.ExecuteReader();
        var payloads = new List<object>();
        while (reader.Read()) payloads.Add(ReadPayload(reader));
        return Remaining(payloads);
    }

    public static async Task<List<AllocationDto>> LoadAsync(IQuerySession session, Guid reservationId, CancellationToken ct)
    {
        await using var command = ReadCommand(session, reservationId);
        if (command.Connection!.State != ConnectionState.Open) await command.Connection.OpenAsync(ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var payloads = new List<object>();
        while (await reader.ReadAsync(ct)) payloads.Add(ReadPayload(reader));
        return Remaining(payloads);
    }

    private static object ReadPayload(DbDataReader reader) =>
        JsonSerializer.Deserialize(reader.GetString(1), EventTypes[reader.GetString(0)])!;

    private static NpgsqlCommand ReadCommand(IQuerySession session, Guid reservationId)
    {
        // FetchStream also deserializes JSONB headers, which Marten 7 / Npgsql 8
        // cannot read without dynamic JSON opt-in. This calculation only needs payloads.
        var connection = session.Connection ?? throw new InvalidOperationException("A Marten connection is required.");
        var command = connection.CreateCommand();
        command.CommandText = "SELECT type, data::text FROM warehouse_events.mt_events WHERE stream_id=@stream AND type=ANY(@aliases) AND is_archived=FALSE ORDER BY seq_id";
        command.Parameters.AddWithValue("stream", Reservation.StreamIdFor(reservationId));
        command.Parameters.AddWithValue("aliases", EventTypes.Keys.ToArray());
        return command;
    }
}
