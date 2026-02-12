using System.Diagnostics;
using FluentAssertions;
using LKvitai.MES.Api.Services;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class PerformanceRegressionTests
{
    [Fact]
    [Trait("Category", "Performance")]
    public void WaveCreation_100Waves_ShouldStayWithinBaseline()
    {
        var store = new AdvancedWarehouseStore();
        var watch = Stopwatch.StartNew();

        for (var i = 0; i < 100; i++)
        {
            var orders = Enumerable.Range(1, 10)
                .Select(x => Guid.NewGuid())
                .ToArray();

            store.CreateWave(orders, "picker-1");
        }

        watch.Stop();

        watch.ElapsedMilliseconds.Should().BeLessThan(2000);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void SerialSearch_1000Records_ShouldStayWithinBaseline()
    {
        var store = new AdvancedWarehouseStore();

        for (var i = 0; i < 1000; i++)
        {
            store.RegisterSerial(new SerialRegisterRequest(
                100 + (i % 10),
                $"SN-{i:000000}",
                "A1",
                null,
                "perf"));
        }

        var watch = Stopwatch.StartNew();
        var result = store.SearchSerials("SN-00", null, null);
        watch.Stop();

        result.Count.Should().BeGreaterThan(0);
        watch.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
