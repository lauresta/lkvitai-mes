using LKvitai.MES.Application.Ports;
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
}
