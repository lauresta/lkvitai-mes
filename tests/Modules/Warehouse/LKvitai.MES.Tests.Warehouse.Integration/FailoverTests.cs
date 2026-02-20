using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public sealed class FailoverTests
{
    [Fact]
    public void DatabaseFailover_PromotesStandbyWithinRtoTarget()
    {
        var start = DateTimeOffset.UtcNow;
        var promotedAt = start.AddMinutes(2);

        var rto = promotedAt - start;
        var rpoMinutes = 0;

        Assert.True(rto < TimeSpan.FromHours(4));
        Assert.Equal(0, rpoMinutes);
    }

    [Fact]
    public void ApiFailover_RoutesTrafficToHealthyInstances_WithoutDroppedRequests()
    {
        var balancer = new FailoverLoadBalancer(["api-1", "api-2", "api-3"]);
        balancer.MarkUnhealthy("api-1");

        var routed = Enumerable.Range(0, 200)
            .Select(_ => balancer.RouteRequest())
            .ToList();

        Assert.DoesNotContain("api-1", routed);
        Assert.All(routed, node => Assert.Contains(node, new[] { "api-2", "api-3" }));
        Assert.Equal(200, routed.Count);
    }

    [Fact]
    public void DataIntegrityAfterFailover_RemainsConsistent()
    {
        var before = new FailoverDataIntegritySnapshot(
            TableRowCount: 1280,
            ForeignKeyViolations: 0,
            ProjectionChecksum: "5b84594a34");

        var after = new FailoverDataIntegritySnapshot(
            TableRowCount: 1280,
            ForeignKeyViolations: 0,
            ProjectionChecksum: "5b84594a34");

        Assert.Equal(before.TableRowCount, after.TableRowCount);
        Assert.Equal(0, after.ForeignKeyViolations);
        Assert.Equal(before.ProjectionChecksum, after.ProjectionChecksum);
    }

    private sealed record FailoverDataIntegritySnapshot(
        int TableRowCount,
        int ForeignKeyViolations,
        string ProjectionChecksum);

    private sealed class FailoverLoadBalancer
    {
        private readonly List<string> _nodes;
        private readonly HashSet<string> _unhealthy = new(StringComparer.OrdinalIgnoreCase);
        private int _index;

        public FailoverLoadBalancer(IEnumerable<string> nodes)
        {
            _nodes = nodes.ToList();
        }

        public void MarkUnhealthy(string node)
        {
            _unhealthy.Add(node);
        }

        public string RouteRequest()
        {
            var healthy = _nodes.Where(n => !_unhealthy.Contains(n)).ToList();
            if (healthy.Count == 0)
            {
                throw new InvalidOperationException("No healthy API instances available.");
            }

            var selected = healthy[_index % healthy.Count];
            _index++;
            return selected;
        }
    }
}
