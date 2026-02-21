using LKvitai.MES.Modules.Warehouse.Application.Ports;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// Marten-backed implementation of <see cref="IProcessedCommandStore"/>.
///
/// Atomic claim semantics:
///   TryStartAsync uses session.Insert (plain INSERT, no upsert).
///   If the document already exists, PostgreSQL raises a unique-key violation (23505).
///   This guarantees that exactly one concurrent caller wins the claim.
///
///   If the existing record is Failed, the store reclaims it via Store (upsert)
///   and returns Started. This small race window is acceptable because:
///     (a) it only applies to previously-failed commands being retried
///     (b) the handler's own concurrency checks (expected-version append) prevent
///         double-execution even if two retriers slip through
/// </summary>
public class MartenProcessedCommandStore : IProcessedCommandStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenProcessedCommandStore> _logger;

    public MartenProcessedCommandStore(
        IDocumentStore store,
        ILogger<MartenProcessedCommandStore> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CommandClaimResult> TryStartAsync(
        Guid commandId, string commandType, CancellationToken ct)
    {
        var id = commandId.ToString();

        // ── Step 1: Atomic INSERT attempt ──────────────────────────────
        try
        {
            await using var session = _store.LightweightSession();
            session.Insert(new ProcessedCommandRecord
            {
                Id = id,
                CommandId = commandId,
                CommandType = commandType,
                Status = ProcessedCommandStatus.InProgress,
                CreatedAt = DateTime.UtcNow
            });
            await session.SaveChangesAsync(ct);
            return CommandClaimResult.Started;
        }
        catch (Exception ex) when (IsDuplicateKeyViolation(ex))
        {
            // Document already exists — fall through to status check
        }

        // ── Step 2: Load existing record and decide ────────────────────
        await using var querySession = _store.QuerySession();
        var existing = await querySession.LoadAsync<ProcessedCommandRecord>(id, ct);

        if (existing == null)
        {
            // Shouldn't happen (INSERT failed due to duplicate, but row gone).
            // Treat as in-progress defensively.
            _logger.LogWarning(
                "Command {CommandId}: INSERT failed as duplicate but record not found on load", commandId);
            return CommandClaimResult.InProgress;
        }

        switch (existing.Status)
        {
            case ProcessedCommandStatus.Success:
                return CommandClaimResult.AlreadyCompleted;

            case ProcessedCommandStatus.InProgress:
                return CommandClaimResult.InProgress;

            case ProcessedCommandStatus.Failed:
                // Reclaim for retry: update to InProgress
                return await TryReclaimFailedAsync(existing, ct);

            default:
                return CommandClaimResult.InProgress;
        }
    }

    /// <inheritdoc />
    public async Task CompleteAsync(Guid commandId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var record = await session.LoadAsync<ProcessedCommandRecord>(commandId.ToString(), ct);
        if (record != null)
        {
            record.Status = ProcessedCommandStatus.Success;
            record.ResultError = null;
            record.CompletedAt = DateTime.UtcNow;
            session.Store(record);
            await session.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task FailAsync(Guid commandId, string? error, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var record = await session.LoadAsync<ProcessedCommandRecord>(commandId.ToString(), ct);
        if (record != null)
        {
            record.Status = ProcessedCommandStatus.Failed;
            record.ResultError = error;
            record.CompletedAt = DateTime.UtcNow;
            session.Store(record);
            await session.SaveChangesAsync(ct);
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Reclaims a Failed record by setting it back to InProgress.
    /// Uses Store (upsert) — small race window is acceptable (see class doc).
    /// </summary>
    private async Task<CommandClaimResult> TryReclaimFailedAsync(
        ProcessedCommandRecord existing, CancellationToken ct)
    {
        try
        {
            await using var session = _store.LightweightSession();
            existing.Status = ProcessedCommandStatus.InProgress;
            existing.ResultError = null;
            existing.CompletedAt = null;
            existing.CreatedAt = DateTime.UtcNow;
            session.Store(existing);
            await session.SaveChangesAsync(ct);
            return CommandClaimResult.Started;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to reclaim command {CommandId} from Failed → InProgress", existing.CommandId);
            return CommandClaimResult.InProgress;
        }
    }

    /// <summary>
    /// Checks whether the exception (or its inner exceptions) is a PostgreSQL
    /// unique-key violation (SQLSTATE 23505), which Marten raises when
    /// session.Insert hits an existing document ID.
    /// </summary>
    private static bool IsDuplicateKeyViolation(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is PostgresException { SqlState: "23505" })
                return true;
        }

        return false;
    }
}
