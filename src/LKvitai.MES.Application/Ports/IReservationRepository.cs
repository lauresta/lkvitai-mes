using LKvitai.MES.Domain.Aggregates;

namespace LKvitai.MES.Application.Ports;

/// <summary>
/// Port for Reservation event-stream persistence.
/// Application layer owns this interface; Infrastructure provides the Marten implementation.
/// </summary>
public interface IReservationRepository
{
    /// <summary>
    /// Loads the Reservation aggregate from the event stream.
    /// Returns the hydrated aggregate and the current stream version.
    /// For a non-existing stream, returns null.
    /// </summary>
    Task<(Reservation? Reservation, long Version)> LoadAsync(
        Guid reservationId, CancellationToken ct);
}
