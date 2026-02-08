using LKvitai.MES.Contracts.Events;

namespace LKvitai.MES.Contracts.Messages;

/// <summary>
/// Messages for the PickStock saga (MassTransit state machine).
///
/// Flow:
///   PickStockRequested → (RecordMovement) → PickMovementRecorded → (ConsumeReservation)
///     → PickReservationConsumed (success) | PickReservationConsumptionFailed (retry/DLQ)
///
/// [MITIGATION V-3] StockMovement is recorded FIRST. HU projection updates asynchronously.
/// </summary>

// ── Trigger ─────────────────────────────────────────────────────────

/// <summary>
/// Published by PickStockCommandHandler to initiate the durable retry path
/// when in-process reservation consumption fails after StockMovement is committed.
/// </summary>
public record ConsumePickReservationDeferred
{
    public Guid CorrelationId { get; init; }
    public Guid ReservationId { get; init; }
    public decimal Quantity { get; init; }
    public Guid MovementId { get; init; }
    public string WarehouseId { get; init; } = string.Empty;
    public string FromLocation { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
    public List<HardLockLineDto> ReleasedHardLockLines { get; init; } = new();
}

/// <summary>
/// Scheduled retry message sent by the saga to itself.
/// </summary>
public record RetryConsumeReservation
{
    public Guid CorrelationId { get; init; }
}

/// <summary>
/// Published when reservation consumption succeeds (in saga retry path).
/// </summary>
public record PickReservationConsumed
{
    public Guid CorrelationId { get; init; }
    public Guid ReservationId { get; init; }
}

/// <summary>
/// Published when reservation consumption fails in the saga retry path.
/// </summary>
public record PickReservationConsumptionFailed
{
    public Guid CorrelationId { get; init; }
    public Guid ReservationId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int RetryCount { get; init; }
}

/// <summary>
/// Published when all retries are exhausted — permanent failure.
/// Supervisor should be alerted. Orphan hard locks may remain.
/// </summary>
public record PickStockFailedPermanentlyEvent
{
    public Guid CorrelationId { get; init; }
    public Guid ReservationId { get; init; }
    public Guid MovementId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; }
}
