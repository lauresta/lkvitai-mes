using LKvitai.MES.Application.Ports;
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

    public MassTransitEventBus(IBus bus)
    {
        _bus = bus;
    }

    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
    {
        return _bus.Publish(message, ct);
    }
}
