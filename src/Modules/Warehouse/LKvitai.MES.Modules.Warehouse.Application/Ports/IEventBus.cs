namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

/// <summary>
/// Application port for publishing messages to the event/message bus.
/// Infrastructure provides the MassTransit implementation.
/// Used by command handlers that need to defer work to durable saga retry paths.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
