using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Marten;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>One change of stock value to be written next to a quantity event.</summary>
public sealed record StockValueRequest(
    string Sku,
    string Kind,
    decimal Qty,
    bool IsInflow,
    decimal? ValueEur,
    StockValueDocument Document)
{
    /// <summary>Purchase posting true-up: ValueEur is the difference, Qty the received quantity.</summary>
    public bool IsTrueUp { get; init; }

    /// <summary>
    /// Value request for a quantity movement between two location codes; null when the movement does not cross
    /// the stock boundary (it then does not change value).
    /// </summary>
    public static StockValueRequest? ForMovement(
        string sku, decimal qty, string fromLocation, string toLocation, StockValueDocument document)
    {
        if (qty <= 0m)
        {
            return null;
        }

        var fromInside = StockBoundary.IsInside(fromLocation);
        var toInside = StockBoundary.IsInside(toLocation);
        if (fromInside == toInside)
        {
            return null;
        }

        if (toInside)
        {
            var kind = fromLocation.Trim().ToUpperInvariant() is "CUSTOMER" or "PRODUCTION"
                ? StockValueKinds.ReturnIn
                : StockValueKinds.AdjustmentIn;
            return new StockValueRequest(sku, kind, qty, true, null, document);
        }

        var outKind = toLocation.Trim().ToUpperInvariant() switch
        {
            "CUSTOMER" or "PRODUCTION" => StockValueKinds.Issue,
            "SCRAP" => StockValueKinds.Scrap,
            "RETURN_TO_SUPPLIER" => StockValueKinds.SupplierReturn,
            _ => StockValueKinds.AdjustmentOut
        };
        return new StockValueRequest(sku, outKind, qty, false, null, document);
    }

    /// <summary>Quantity adjustment in one location: a gain is AdjustmentIn, a loss is AdjustmentOut.</summary>
    public static StockValueRequest? ForAdjustment(
        string sku, decimal qtyDelta, string location, StockValueDocument document)
    {
        if (qtyDelta == 0m || !StockBoundary.IsInside(location))
        {
            return null;
        }

        return qtyDelta > 0m
            ? new StockValueRequest(sku, StockValueKinds.AdjustmentIn, qtyDelta, true, null, document)
            : new StockValueRequest(sku, StockValueKinds.AdjustmentOut, -qtyDelta, false, null, document);
    }

    public static StockValueRequest ForTrueUp(string sku, decimal receivedQty, decimal differenceEur, StockValueDocument document)
        => new(sku, StockValueKinds.ReceiptTrueUp, receivedQty, true, differenceEur, document) { IsTrueUp = true };

    /// <summary>Receipt with the value of the received goods in EUR.</summary>
    public static StockValueRequest ForReceipt(string sku, decimal qty, decimal valueEur, StockValueDocument document)
        => new(sku, StockValueKinds.Receipt, qty, true, valueEur, document);
}

/// <summary>
/// Appends <see cref="StockValueChanged"/> events to the item valuation streams in the caller's session, so they
/// commit in the same transaction as the quantity events. The stream version check serialises concurrent writers
/// of one item: a competing commit fails with a concurrency exception and is retried by the caller.
/// </summary>
public static class StockValueWriter
{
    public static async Task<IReadOnlyList<StockValueChanged>> AppendAsync(
        IDocumentSession session,
        WarehouseDbContext db,
        IEnumerable<StockValueRequest?> requests,
        CancellationToken cancellationToken)
    {
        var list = requests.Where(x => x is not null).Select(x => x!).ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var skus = list.Select(x => x.Sku).Distinct().ToList();
        var itemIds = await db.Items.AsNoTracking()
            .Where(x => skus.Contains(x.InternalSKU))
            .Select(x => new { x.Id, x.InternalSKU })
            .ToDictionaryAsync(x => x.InternalSKU, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var written = new List<StockValueChanged>();
        foreach (var group in list.GroupBy(x => itemIds.TryGetValue(x.Sku, out var id)
                     ? id
                     : throw new InvalidOperationException($"Item with SKU '{x.Sku}' was not found for value accounting.")))
        {
            var streamId = ItemValuation.StreamIdFor(group.Key);
            var state = await session.Events.FetchStreamStateAsync(streamId, cancellationToken);
            var aggregate = state is null
                ? new ItemValuation()
                : await session.Events.AggregateStreamAsync<ItemValuation>(streamId, token: cancellationToken)
                  ?? new ItemValuation();

            var events = new List<StockValueChanged>();
            foreach (var request in group)
            {
                foreach (var evt in BuildMany(aggregate, group.Key, request))
                {
                    aggregate.Apply(evt);
                    events.Add(evt);
                }
            }

            if (events.Count == 0)
            {
                continue;
            }

            if (state is null)
            {
                session.Events.StartStream(streamId, events.Cast<object>().ToArray());
            }
            else
            {
                session.Events.Append(streamId, state.Version + events.Count, events.Cast<object>().ToArray());
            }

            written.AddRange(events);
        }

        return written;
    }

    public static IReadOnlyList<StockValueChanged> BuildMany(ItemValuation aggregate, int itemId, StockValueRequest request)
    {
        if (request.IsTrueUp)
        {
            return aggregate.TrueUp(itemId, request.Qty, request.ValueEur ?? 0m, request.Document);
        }

        var evt = Build(aggregate, itemId, request);
        return evt is null ? [] : [evt];
    }

    public static StockValueChanged? Build(ItemValuation aggregate, int itemId, StockValueRequest request)
    {
        if (request.IsInflow)
        {
            return request.ValueEur.HasValue
                ? aggregate.Inflow(itemId, request.Kind, request.Qty, request.ValueEur.Value, request.Document)
                : aggregate.InflowAtAverage(itemId, request.Kind, request.Qty, request.Document);
        }

        return aggregate.Outflow(itemId, request.Kind, request.Qty, request.Document);
    }

    /// <summary>Updates the OnHandValue read model after the transaction committed; a failure is logged, never thrown.</summary>
    public static async Task ProjectAsync(
        WarehouseDbContext db,
        ILogger logger,
        IReadOnlyList<StockValueChanged> events,
        CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        try
        {
            await StockValueProjection.ApplyAsync(db, logger, events, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OnHandValue projection update failed after commit; it can be rebuilt from the event store.");
        }
    }
}

/// <summary>Retries an operation whose value stream lost the version race against a concurrent writer of the same item.</summary>
public static class StockValueRetry
{
    private const int MaxAttempts = 5;

    public static async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsVersionConflict(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt), cancellationToken);
            }
        }
    }

    public static bool IsVersionConflict(Exception ex)
        => ex is Marten.Exceptions.EventStreamUnexpectedMaxEventIdException
            or Marten.Exceptions.ExistingStreamIdCollisionException
            || ex.InnerException is not null && IsVersionConflict(ex.InnerException);
}
