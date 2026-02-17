using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.EventVersioning;
using LKvitai.MES.SharedKernel;
using LKvitai.MES.Api.ErrorHandling;
using MassTransit;
using System.Diagnostics;

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
    private static readonly ActivitySource ActivitySource = new("Warehouse.MassTransit");
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
            return PublishWithCorrelationAsync(upcasted, ct);
        }

        return PublishWithCorrelationAsync(message, ct);
    }

    private Task PublishWithCorrelationAsync<T>(T message, CancellationToken ct) where T : class
    {
        var correlationId = CorrelationContext.Current;
        using var activity = ActivitySource.StartActivity("MassTransit.Publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "topic");
        activity?.SetTag("messaging.message_type", typeof(T).Name);
        activity?.SetTag("correlation.id", correlationId);

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return _bus.Publish(message, ct);
        }

        return _bus.Publish(message, context =>
        {
            context.Headers.Set(CorrelationIdMiddleware.HeaderName, correlationId);
            if (Guid.TryParse(correlationId, out var guid))
            {
                context.CorrelationId = guid;
            }
        }, ct);
    }
}
