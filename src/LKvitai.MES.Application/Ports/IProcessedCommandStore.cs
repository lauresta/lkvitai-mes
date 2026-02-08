namespace LKvitai.MES.Application.Ports;

/// <summary>
/// Result of an atomic command-claim attempt.
/// </summary>
public enum CommandClaimResult
{
    /// <summary>Claim succeeded — caller should execute the handler.</summary>
    Started,

    /// <summary>Command already completed successfully — caller should short-circuit OK.</summary>
    AlreadyCompleted,

    /// <summary>Another instance is currently executing this command — caller should return conflict.</summary>
    InProgress
}

/// <summary>
/// Status of a processed command record.
/// </summary>
public enum ProcessedCommandStatus
{
    InProgress = 0,
    Success = 1,
    Failed = 2
}

/// <summary>
/// Persisted record for command idempotency.
/// Keyed by CommandId (string representation of Guid).
/// </summary>
public class ProcessedCommandRecord
{
    /// <summary>Marten document ID: CommandId.ToString()</summary>
    public string Id { get; set; } = string.Empty;

    public Guid CommandId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public ProcessedCommandStatus Status { get; set; }
    public string? ResultError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Application port for atomic command dedup persistence.
/// Infrastructure provides the Marten-backed implementation.
///
/// Atomic claim semantics:
///   TryStartAsync  → INSERT-based claim; only one caller wins per commandId
///   CompleteAsync   → marks the claimed command as succeeded
///   FailAsync       → marks the claimed command as failed (allows future retry)
/// </summary>
public interface IProcessedCommandStore
{
    /// <summary>
    /// Atomically attempts to claim a command for execution.
    /// Uses INSERT (not upsert) so only one concurrent caller wins.
    ///
    /// Returns:
    ///   Started          — claim acquired; caller should run the handler
    ///   AlreadyCompleted — previously completed successfully; short-circuit OK
    ///   InProgress       — another instance is executing; return conflict
    ///
    /// If the existing record is Failed, the store reclaims it (sets InProgress)
    /// and returns Started to allow retry.
    /// </summary>
    Task<CommandClaimResult> TryStartAsync(Guid commandId, string commandType, CancellationToken ct);

    /// <summary>
    /// Marks a previously-claimed command as completed successfully.
    /// </summary>
    Task CompleteAsync(Guid commandId, CancellationToken ct);

    /// <summary>
    /// Marks a previously-claimed command as failed.
    /// A subsequent TryStartAsync for this commandId will return Started (retry).
    /// </summary>
    Task FailAsync(Guid commandId, string? error, CancellationToken ct);
}
