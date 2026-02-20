using LKvitai.MES.Modules.Warehouse.Application.EventVersioning;
using LKvitai.MES.Contracts.Events;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace LKvitai.MES.Infrastructure.EventVersioning;

public abstract class EventUpcaster<TSource, TTarget> : IEventUpcaster<TSource, TTarget>
    where TSource : DomainEvent
    where TTarget : DomainEvent
{
    public abstract string SourceVersion { get; }
    public abstract string TargetVersion { get; }

    public Type SourceType => typeof(TSource);
    public Type TargetType => typeof(TTarget);

    public abstract TTarget Upcast(TSource source);

    DomainEvent IEventUpcaster.Upcast(DomainEvent source) => Upcast((TSource)source);
}

public sealed class StockMovedV1ToStockMovedEventUpcaster : EventUpcaster<StockMovedV1Event, StockMovedEvent>
{
    public override string SourceVersion => "v1";
    public override string TargetVersion => "v2";

    public override StockMovedEvent Upcast(StockMovedV1Event source)
    {
        return new StockMovedEvent
        {
            SchemaVersion = TargetVersion,
            EventId = source.EventId,
            Timestamp = source.Timestamp,
            Version = source.Version,
            MovementId = source.MovementId,
            SKU = source.SKU,
            Quantity = source.Quantity,
            FromLocation = source.From,
            ToLocation = source.To,
            MovementType = "TRANSFER",
            OperatorId = source.OperatorId,
            HandlingUnitId = source.HandlingUnitId,
            Reason = source.Reason
        };
    }
}

public sealed class EventSchemaVersionRegistry : IEventSchemaVersionRegistry
{
    private const int MaxUpcastSteps = 16;

    private readonly Dictionary<Type, HashSet<string>> _supportedVersions;
    private readonly Dictionary<UpcasterKey, IEventUpcaster> _upcasters;
    private readonly ILogger<EventSchemaVersionRegistry> _logger;

    public EventSchemaVersionRegistry(
        IEnumerable<IEventUpcaster> upcasters,
        ILogger<EventSchemaVersionRegistry> logger)
    {
        _logger = logger;
        _supportedVersions = BuildSupportedVersions(upcasters);
        _upcasters = BuildUpcasterLookup(upcasters);

        foreach (var upcaster in upcasters)
        {
            _logger.LogInformation(
                "Event upcaster registered: {FromType} {FromVersion} -> {ToType} {ToVersion}",
                upcaster.SourceType.Name,
                upcaster.SourceVersion,
                upcaster.TargetType.Name,
                upcaster.TargetVersion);
        }
    }

    public IReadOnlyCollection<string> GetSupportedVersions(Type eventType)
    {
        if (_supportedVersions.TryGetValue(eventType, out var versions))
        {
            return versions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        return Array.Empty<string>();
    }

    public void EnsureKnownVersion(Type eventType, string schemaVersion)
    {
        var normalized = NormalizeVersion(schemaVersion);
        if (!_supportedVersions.TryGetValue(eventType, out var versions) || !versions.Contains(normalized))
        {
            throw new InvalidOperationException(
                $"Unknown event version: {schemaVersion} for event type {eventType.Name}");
        }
    }

    public DomainEvent UpcastToLatest(DomainEvent sourceEvent)
    {
        EnsureKnownVersion(sourceEvent.GetType(), sourceEvent.SchemaVersion);

        var current = sourceEvent;
        var iterations = 0;

        while (iterations < MaxUpcastSteps)
        {
            iterations++;
            var key = new UpcasterKey(current.GetType(), NormalizeVersion(current.SchemaVersion));
            if (!_upcasters.TryGetValue(key, out var upcaster))
            {
                return current;
            }

            current = upcaster.Upcast(current);
            if (string.IsNullOrWhiteSpace(current.SchemaVersion))
            {
                throw new InvalidOperationException(
                    $"Upcaster {upcaster.GetType().Name} produced event without SchemaVersion.");
            }
        }

        throw new InvalidOperationException(
            $"Upcast chain exceeded {MaxUpcastSteps} steps for event type {sourceEvent.GetType().Name}.");
    }

    private static Dictionary<UpcasterKey, IEventUpcaster> BuildUpcasterLookup(IEnumerable<IEventUpcaster> upcasters)
    {
        var lookup = new Dictionary<UpcasterKey, IEventUpcaster>();
        foreach (var upcaster in upcasters)
        {
            var key = new UpcasterKey(upcaster.SourceType, NormalizeVersion(upcaster.SourceVersion));
            lookup[key] = upcaster;
        }

        return lookup;
    }

    private static Dictionary<Type, HashSet<string>> BuildSupportedVersions(IEnumerable<IEventUpcaster> upcasters)
    {
        var map = new Dictionary<Type, HashSet<string>>();

        foreach (var eventType in DiscoverDomainEventTypes())
        {
            map[eventType] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "v1" };
        }

        foreach (var upcaster in upcasters)
        {
            if (!map.TryGetValue(upcaster.SourceType, out var sourceVersions))
            {
                sourceVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "v1" };
                map[upcaster.SourceType] = sourceVersions;
            }

            sourceVersions.Add(NormalizeVersion(upcaster.SourceVersion));
            sourceVersions.Add(NormalizeVersion(upcaster.TargetVersion));

            if (!map.TryGetValue(upcaster.TargetType, out var targetVersions))
            {
                targetVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "v1" };
                map[upcaster.TargetType] = targetVersions;
            }

            targetVersions.Add(NormalizeVersion(upcaster.TargetVersion));
        }

        return map;
    }

    private static IEnumerable<Type> DiscoverDomainEventTypes()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
            {
                if (!type.IsAbstract && typeof(DomainEvent).IsAssignableFrom(type))
                {
                    yield return type;
                }
            }
        }
    }

    private static string NormalizeVersion(string schemaVersion)
    {
        return schemaVersion.Trim().ToLowerInvariant();
    }

    private readonly record struct UpcasterKey(Type EventType, string SchemaVersion);
}
