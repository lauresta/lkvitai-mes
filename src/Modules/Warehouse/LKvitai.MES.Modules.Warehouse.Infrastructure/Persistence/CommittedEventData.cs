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
}
