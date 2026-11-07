using System.Collections.Concurrent;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Services;

/// <summary>Immediate sign-off refresh for open circuits on this WebUI instance; polling covers other instances.</summary>
public sealed class RoletaiSignOffNotifier
{
    private readonly ConcurrentDictionary<Guid, Action<long>> _subscribers = new();

    public Guid Subscribe(Action<long> callback)
    {
        var key = Guid.NewGuid();
        _subscribers[key] = callback;
        return key;
    }

    public void Unsubscribe(Guid key) => _subscribers.TryRemove(key, out _);

    public void Publish(long productId)
    {
        foreach (var callback in _subscribers.Values) callback(productId);
    }
}
