using LKvitai.MES.Contracts.Events;
using Marten;
using Marten.Events;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>Reads current and historical event aliases without relying on a projection.</summary>
public static class CommittedEventData
{
    public static Task<IReadOnlyList<T>> ReadAsync<T>(IQuerySession session, string? sku,
        CancellationToken cancellationToken) where T : class
    {
        // QueryRawEventDataOnly uses the default snake-case alias even after MapEventType.
        // Deployed Warehouse events retain the historical qualified alias.
        var aliases = new[] { EventMappingExtensions.GetEventTypeName(typeof(T)), $"LKvitai.MES.Contracts.Events.{typeof(T).Name}, LKvitai.MES.Contracts" };
        return sku is null
            ? session.QueryAsync<T>("SELECT data FROM warehouse_events.mt_events WHERE type = ANY(?) AND is_archived = FALSE", cancellationToken, (object)aliases)
            : session.QueryAsync<T>("SELECT data FROM warehouse_events.mt_events WHERE type = ANY(?) AND is_archived = FALSE AND lower(data->>'SKU') = ?", cancellationToken, aliases, sku.ToLowerInvariant());
    }

    /// <summary>
    /// Stock movements together with the warehouse of their stock-ledger stream. Movements
    /// written by goods receipt (including Agnum distribution) carry no warehouse in the
    /// event data; the stream id <c>stock-ledger:{warehouse}:{location}:{sku}</c> is the only source.
    /// </summary>
    public static Task<IReadOnlyList<StreamedStockMoved>> ReadStockMovedAsync(IQuerySession session, string? sku,
        CancellationToken cancellationToken)
    {
        var aliases = new[] { EventMappingExtensions.GetEventTypeName(typeof(StockMovedEvent)),
            $"LKvitai.MES.Contracts.Events.{nameof(StockMovedEvent)}, LKvitai.MES.Contracts" };
        const string select = "SELECT jsonb_build_object('Warehouse', split_part(stream_id, ':', 2), 'Event', data) " +
            "FROM warehouse_events.mt_events WHERE type = ANY(?) AND is_archived = FALSE AND stream_id LIKE 'stock-ledger:%'";
        return sku is null
            ? session.QueryAsync<StreamedStockMoved>(select, cancellationToken, (object)aliases)
            : session.QueryAsync<StreamedStockMoved>(select + " AND lower(data->>'SKU') = ?", cancellationToken, aliases, sku.ToLowerInvariant());
    }
}

public sealed class StreamedStockMoved
{
    public string Warehouse { get; set; } = string.Empty;
    public StockMovedEvent Event { get; set; } = new();
}
