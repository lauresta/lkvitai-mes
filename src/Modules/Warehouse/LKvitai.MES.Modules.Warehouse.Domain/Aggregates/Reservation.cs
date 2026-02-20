using LKvitai.MES.Contracts.Events;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Domain.Aggregates;

/// <summary>
/// Reservation aggregate - Event sourced, manages hybrid locking (SOFT → HARD)
/// Per blueprint: Uses Marten expected-version append for HARD lock acquisition
/// Stream ID: reservation-{reservationId}
/// </summary>
public class Reservation
{
    /// <summary>Marten aggregate identity (matches stream key).</summary>
    public string Id { get; set; } = string.Empty;

    public Guid ReservationId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public ReservationLockType LockType { get; private set; }
    public int Priority { get; private set; }
    public List<ReservationLine> Lines { get; private set; } = new();

    // ----------------------------------------------------------------
    // Marten event application — called during aggregate hydration
    // ----------------------------------------------------------------

    public void Apply(ReservationCreatedEvent e)
    {
        ReservationId = e.ReservationId;
        Status = ReservationStatus.PENDING;
        Priority = e.Priority;
        Lines = e.RequestedLines.Select(l => new ReservationLine
        {
            SKU = l.SKU,
            RequestedQuantity = l.Quantity,
            AllocatedHUs = new List<Guid>()
        }).ToList();
    }

    public void Apply(StockAllocatedEvent e)
    {
        Status = ReservationStatus.ALLOCATED;
        LockType = ReservationLockType.SOFT;

        foreach (var allocation in e.Allocations)
        {
            var line = Lines.FirstOrDefault(l => l.SKU == allocation.SKU);
            if (line != null)
            {
                line.AllocatedHUs.AddRange(allocation.HandlingUnitIds);
                line.Location = allocation.Location;
                line.WarehouseId = allocation.WarehouseId;
                line.AllocatedQuantity = allocation.Quantity;
            }
        }
    }

    public void Apply(PickingStartedEvent e)
    {
        Status = ReservationStatus.PICKING;
        LockType = ReservationLockType.HARD;
    }

    public void Apply(ReservationConsumedEvent e)
    {
        Status = ReservationStatus.CONSUMED;
    }

    public void Apply(ReservationCancelledEvent e)
    {
        Status = ReservationStatus.CANCELLED;
    }

    public void Apply(ReservationBumpedEvent e)
    {
        Status = ReservationStatus.BUMPED;
    }

    // ----------------------------------------------------------------
    // Domain validation for StartPicking
    // ----------------------------------------------------------------

    /// <summary>
    /// Validates that the reservation can transition to PICKING (HARD lock).
    /// Does NOT perform balance/conflict checks — those happen in the handler.
    /// </summary>
    public void ValidateCanStartPicking()
    {
        if (Status != ReservationStatus.ALLOCATED)
            throw new DomainException(
                DomainErrorCodes.ReservationNotAllocated,
                $"Reservation {ReservationId} must be ALLOCATED to start picking. Current status: {Status}");

        if (LockType != ReservationLockType.SOFT)
            throw new DomainException(
                DomainErrorCodes.HardLockConflict,
                $"Reservation {ReservationId} must have SOFT lock to start picking. Current lock: {LockType}");

        if (!Lines.Any())
            throw new DomainException(
                DomainErrorCodes.ValidationError,
                $"Reservation {ReservationId} has no allocated lines.");
    }

    // ----------------------------------------------------------------
    // Stream ID helper
    // ----------------------------------------------------------------

    public static string StreamIdFor(Guid reservationId) => $"reservation-{reservationId}";
}

public enum ReservationStatus
{
    PENDING,
    ALLOCATED,
    PICKING,
    CONSUMED,
    CANCELLED,
    BUMPED
}

public enum ReservationLockType
{
    SOFT,
    HARD
}

public class ReservationLine
{
    public string SKU { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public string Location { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public List<Guid> AllocatedHUs { get; set; } = new();
}
