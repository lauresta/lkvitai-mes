namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Stable, machine-readable error codes returned to callers.
/// NEVER return raw exception messages — use these codes instead.
/// </summary>
public static class DomainErrorCodes
{
    // ── Generic idempotency (used by IdempotencyBehavior) ───────────
    public const string IdempotencyInProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string IdempotencyAlreadyProcessed = "IDEMPOTENCY_ALREADY_PROCESSED";

    // ── ReceiveGoods ────────────────────────────────────────────────
    public const string HandlingUnitSealed = "HANDLINGUNIT_SEALED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string ReceiveGoodsFailed = "RECEIVEGOODS_FAILED";

    // ── Allocation ──────────────────────────────────────────────────
    public const string ReservationNotFound = "RESERVATION_NOT_FOUND";
    public const string ReservationNotPending = "RESERVATION_NOT_PENDING";
    public const string InsufficientAvailableStock = "INSUFFICIENT_AVAILABLE_STOCK";
    public const string AllocationFailed = "ALLOCATION_FAILED";

    // ── PickStock ───────────────────────────────────────────────────
    public const string ReservationNotPicking = "RESERVATION_NOT_PICKING";
    public const string PickStockMovementFailed = "PICKSTOCK_MOVEMENT_FAILED";
    public const string PickStockConsumptionFailed = "PICKSTOCK_CONSUMPTION_FAILED";
    public const string PickStockConsumptionDeferred = "PICKSTOCK_CONSUMPTION_DEFERRED";
    public const string PickStockFailedPermanently = "PICKSTOCK_FAILED_PERMANENTLY";

    // ── Consistency ─────────────────────────────────────────────────
    public const string StuckReservationDetected = "STUCK_RESERVATION_DETECTED";
    public const string OrphanHardLockDetected = "ORPHAN_HARDLOCK_DETECTED";
}
