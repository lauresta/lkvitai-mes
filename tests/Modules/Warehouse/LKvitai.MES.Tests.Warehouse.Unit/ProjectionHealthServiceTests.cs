using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ProjectionHealthServiceTests
{
    [Theory]
    [InlineData(null, "Healthy")]
    [InlineData(0.2, "Healthy")]
    [InlineData(1.0, "Degraded")]
    [InlineData(12.5, "Degraded")]
    [InlineData(60.0, "Degraded")]
    [InlineData(60.1, "Unhealthy")]
    public void ClassifyStatus_UsesExpectedThresholds(double? lagSeconds, string expected)
    {
        var status = ProjectionHealthService.ClassifyStatus(lagSeconds);
        status.Should().Be(expected);
    }

    [Fact]
    public void CaughtUpProjection_ShouldBeHealthy_WhenLagEventsIsZero()
    {
        var status = ProjectionHealthService.ClassifyStatus(0d);
        status.Should().Be("Healthy");
    }
}
