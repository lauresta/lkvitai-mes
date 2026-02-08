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
}
