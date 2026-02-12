using FluentAssertions;
using LKvitai.MES.Api.Services;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class AdvancedWarehouseStoreTests
{
    [Fact]
    public void CreateWave_WithSameOrderSet_ShouldBeIdempotent()
    {
        var store = new AdvancedWarehouseStore();
        var orders = new[] { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") };

        var first = store.CreateWave(orders, "picker-1");
        var second = store.CreateWave(orders.Reverse().ToArray(), null);

        second.Id.Should().Be(first.Id);
        second.WaveNumber.Should().Be(first.WaveNumber);
    }

    [Fact]
    public void CreateWave_PickList_ShouldBeSortedByLocation()
    {
        var store = new AdvancedWarehouseStore();
        var orders = Enumerable.Range(1, 5)
            .Select(i => Guid.Parse($"00000000-0000-0000-0000-00000000000{i}"))
            .ToArray();

        var wave = store.CreateWave(orders, null);

        var sorted = wave.PickList
            .OrderBy(x => x.Location, StringComparer.Ordinal)
            .Select(x => x.Location)
            .ToArray();

        wave.PickList.Select(x => x.Location).ToArray().Should().Equal(sorted);
    }

    [Fact]
    public void SerialTransition_ShouldAppendHistory()
    {
        var store = new AdvancedWarehouseStore();
        var serial = store.RegisterSerial(new SerialRegisterRequest(1, "SN-001", "A1", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), "operator"));

        var transitioned = store.TransitionSerial(serial.Id, new SerialTransitionRequest("AVAILABLE", "A2", "operator"));

        transitioned.Should().NotBeNull();
        transitioned!.Status.Should().Be(SerialStatus.Available);
        transitioned.History.Should().HaveCount(2);
        transitioned.History.Last().NewStatus.Should().Be(SerialStatus.Available);
    }
}
