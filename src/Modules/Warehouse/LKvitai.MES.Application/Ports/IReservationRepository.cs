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

    /// <summary>
    /// Returns lightweight DTOs for all reservations currently in the given state.
    /// Used by consistency checks (e.g. StuckReservationCheck).
    /// </summary>
    Task<IReadOnlyList<ReservationStateDto>> GetReservationsInStateAsync(
        string status, CancellationToken ct);

    /// <summary>
    /// Returns the current status string of a reservation, or null if not found.
    /// Used by consistency checks (e.g. OrphanHardLockCheck).
    /// </summary>
    Task<string?> GetReservationStatusAsync(Guid reservationId, CancellationToken ct);
}

/// <summary>
/// Lightweight DTO for consistency check queries.
/// </summary>
public record ReservationStateDto
{
    public Guid ReservationId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? PickingStartedAt { get; init; }
}
