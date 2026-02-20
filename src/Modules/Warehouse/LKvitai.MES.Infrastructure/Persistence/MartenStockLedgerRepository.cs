using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using Marten;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IStockLedgerRepository"/>.
/// Uses expected-version append (V-2) for optimistic concurrency.
/// Creates a fresh lightweight session per operation to support retry loops.
///
/// Stream IDs are produced by <see cref="LKvitai.MES.Modules.Warehouse.Domain.StockLedgerStreamId"/>
/// and passed in by the caller; this class does NOT compute stream IDs.
/// </summary>
public class MartenStockLedgerRepository : IStockLedgerRepository
{
    private readonly IDocumentStore _store;

    public MartenStockLedgerRepository(IDocumentStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<(StockLedger Ledger, long Version)> LoadAsync(
        string streamId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var ledger = await session.Events.AggregateStreamAsync<StockLedger>(
            streamId, token: ct) ?? new StockLedger();

        var state = await session.Events.FetchStreamStateAsync(streamId, ct);
        // Marten uses -2 for new streams (V-2 versioning scheme)
        // New stream: -2, After 1 event: -1, After 2 events: 0, etc.
        var version = state?.Version ?? -2;

        return (ledger, version);
    }

    /// <inheritdoc />
    public async Task AppendEventAsync(
        string streamId, StockMovedEvent evt, long expectedVersion, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        try
        {
            session.Events.Append(streamId, expectedVersion, evt);
            await session.SaveChangesAsync(ct);
        }
        catch (Marten.Exceptions.EventStreamUnexpectedMaxEventIdException ex)
        {
            throw new ConcurrencyException(
                $"Version conflict on stream '{streamId}': expected version {expectedVersion}", ex);
        }
    }
}
