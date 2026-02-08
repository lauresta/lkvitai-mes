using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain.Aggregates;
using Marten;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IReservationRepository"/>.
/// Uses string stream identity: "reservation-{reservationId}".
/// </summary>
public class MartenReservationRepository : IReservationRepository
{
    private readonly IDocumentStore _store;

    public MartenReservationRepository(IDocumentStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<(Reservation? Reservation, long Version)> LoadAsync(
        Guid reservationId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var streamId = Reservation.StreamIdFor(reservationId);

        var reservation = await session.Events.AggregateStreamAsync<Reservation>(
            streamId, token: ct);

        var state = await session.Events.FetchStreamStateAsync(streamId, ct);
        var version = state?.Version ?? 0;

        return (reservation, version);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReservationStateDto>> GetReservationsInStateAsync(
        string status, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        // Query event streams that start with "reservation-"
        // and filter by aggregating to check current status.
        // For efficiency, we scan all reservation streams.
        var allStreams = await session.Events
            .QueryAllRawEvents()
            .Where(e => e.EventTypeName == "reservation_created_event")
            .Select(e => e.StreamKey!)
            .ToListAsync(ct);

        var results = new List<ReservationStateDto>();

        foreach (var streamKey in allStreams.Distinct())
        {
            if (string.IsNullOrEmpty(streamKey)) continue;

            var reservation = await session.Events.AggregateStreamAsync<Reservation>(
                streamKey, token: ct);

            if (reservation is null) continue;

            if (reservation.Status.ToString() == status)
            {
                // Find the PickingStartedEvent timestamp
                DateTime? pickingStartedAt = null;
                if (status == "PICKING")
                {
                    var events = await session.Events.FetchStreamAsync(streamKey, token: ct);
                    var pickingEvent = events
                        .Select(e => e.Data)
                        .OfType<PickingStartedEvent>()
                        .LastOrDefault();

                    pickingStartedAt = pickingEvent?.Timestamp;
                }

                results.Add(new ReservationStateDto
                {
                    ReservationId = reservation.ReservationId,
                    Status = reservation.Status.ToString(),
                    PickingStartedAt = pickingStartedAt
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<string?> GetReservationStatusAsync(Guid reservationId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var streamId = Reservation.StreamIdFor(reservationId);
        var reservation = await session.Events.AggregateStreamAsync<Reservation>(
            streamId, token: ct);

        return reservation?.Status.ToString();
    }
}
