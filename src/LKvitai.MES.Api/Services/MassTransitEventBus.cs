using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.EventVersioning;
using LKvitai.MES.SharedKernel;
using MassTransit;

namespace LKvitai.MES.Api.Services;

/// <summary>
/// MassTransit implementation of <see cref="IEventBus"/>.
/// Registered in the Api project (composition root) because:
///   - Application layer cannot reference MassTransit (clean architecture)
///   - Infrastructure layer does not have MassTransit dependency
///   - Api is the composition root with MassTransit configured
/// </summary>
public class MassTransitEventBus : IEventBus
{
    private readonly IBus _bus;
    private readonly IEventSchemaVersionRegistry? _schemaVersionRegistry;

    public MassTransitEventBus(
        IBus bus,
        IEventSchemaVersionRegistry? schemaVersionRegistry = null)
    {
        _bus = bus;
        _schemaVersionRegistry = schemaVersionRegistry;
    }

    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        if (message is DomainEvent domainEvent && _schemaVersionRegistry is not null)
        {
            _schemaVersionRegistry.EnsureKnownVersion(domainEvent.GetType(), domainEvent.SchemaVersion);
            var upcasted = _schemaVersionRegistry.UpcastToLatest(domainEvent);
            return _bus.Publish(upcasted, ct);
        }

        return _bus.Publish(message, ct);
    }
}
