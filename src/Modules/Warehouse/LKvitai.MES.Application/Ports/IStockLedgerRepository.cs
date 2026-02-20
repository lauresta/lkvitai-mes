using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain.Aggregates;

namespace LKvitai.MES.Application.Ports;

/// <summary>
/// Port for StockLedger event-stream persistence.
/// Application layer owns this interface; Infrastructure provides the Marten implementation.
/// </summary>
public interface IStockLedgerRepository
{
    /// <summary>
    /// Loads the StockLedger aggregate from the event stream for the given stream ID.
    /// The stream ID must be produced by <see cref="LKvitai.MES.Domain.StockLedgerStreamId.For"/>.
    /// Returns the hydrated aggregate and the current stream version.
    /// For a non-existing stream, returns a fresh aggregate and version 0.
    /// </summary>
    Task<(StockLedger Ledger, long Version)> LoadAsync(string streamId, CancellationToken ct);

    /// <summary>
    /// Appends a StockMovedEvent to the stream with expected-version check.
    /// </summary>
    /// <param name="streamId">Stream ID produced by <see cref="LKvitai.MES.Domain.StockLedgerStreamId.For"/>.</param>
    /// <param name="evt">The event to append.</param>
    /// <param name="expectedVersion">Expected stream version before the append (optimistic concurrency).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="LKvitai.MES.SharedKernel.ConcurrencyException">
    /// Thrown when the stream version does not match <paramref name="expectedVersion"/>.
    /// </exception>
    Task AppendEventAsync(string streamId, StockMovedEvent evt, long expectedVersion, CancellationToken ct);
}
