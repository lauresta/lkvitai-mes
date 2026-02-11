using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Application.EventVersioning;

public interface IEventUpcaster
{
    Type SourceType { get; }
    Type TargetType { get; }
    string SourceVersion { get; }
    string TargetVersion { get; }

    DomainEvent Upcast(DomainEvent source);
}

public interface IEventUpcaster<in TSource, out TTarget> : IEventUpcaster
    where TSource : DomainEvent
    where TTarget : DomainEvent
{
    TTarget Upcast(TSource source);
}

public interface IEventSchemaVersionRegistry
{
    IReadOnlyCollection<string> GetSupportedVersions(Type eventType);
    DomainEvent UpcastToLatest(DomainEvent sourceEvent);
    void EnsureKnownVersion(Type eventType, string schemaVersion);
}
