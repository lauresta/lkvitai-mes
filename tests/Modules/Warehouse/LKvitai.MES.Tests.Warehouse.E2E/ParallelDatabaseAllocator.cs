using System.Collections.Concurrent;
using System.Threading;

namespace LKvitai.MES.Tests.Warehouse.E2E;

public static class ParallelDatabaseAllocator
{
    private static readonly ConcurrentDictionary<int, string> ThreadDatabases = new();
    private static int _nextSlot;

    public static string GetDatabaseName()
    {
        return ThreadDatabases.GetOrAdd(
            Environment.CurrentManagedThreadId,
            _ =>
            {
                var slot = (Interlocked.Increment(ref _nextSlot) - 1) % 4 + 1;
                return $"test-db-{slot}";
            });
    }
}
