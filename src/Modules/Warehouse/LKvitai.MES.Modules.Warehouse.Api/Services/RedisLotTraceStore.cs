using System.Text.Json;
using StackExchange.Redis;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class RedisLotTraceStore(IConnectionMultiplexer redis) : ILotTraceStore
{
    private const string ReportsKey = "warehouse:lot-traces";

    public void Save(LotTraceReport report) => redis.GetDatabase().HashSet(
        ReportsKey, report.TraceId.ToString("N"), JsonSerializer.Serialize(report));

    public bool TryGet(Guid traceId, out LotTraceReport? report) => Read(
        redis.GetDatabase().HashGet(ReportsKey, traceId.ToString("N")), out report);

    public bool TryGetAny(out LotTraceReport? report) => Read(
        redis.GetDatabase().HashScan(ReportsKey, pageSize: 1).FirstOrDefault().Value, out report);

    // One shared hash keeps reports and their count consistent, including retries.
    public int Count => checked((int)redis.GetDatabase().HashLength(ReportsKey));

    private static bool Read(RedisValue value, out LotTraceReport? report)
    {
        report = value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<LotTraceReport>(value.ToString());
        return report is not null;
    }
}
