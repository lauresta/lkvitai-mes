using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Documents Marten 7 expected-version semantics for appending events.
/// Marten's expectedVersion parameter is the expected final stream version
/// after the append, while application services usually reason about the
/// current version before the append.
/// </summary>
public class EventSourcingVersionInitializationTests
{
    [Fact]
    public void NewStream_CurrentVersion_ShouldStartAtZero()
    {
        int? stateVersion = null;

        var currentVersion = stateVersion ?? 0;

        Assert.Equal(0, currentVersion);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(10, 10)]
    public void CurrentVersion_FromStreamState_ShouldUseMartenVersionOrZero(
        int? actualVersion, int expectedVersion)
    {
        var currentVersion = actualVersion ?? 0;

        Assert.Equal(expectedVersion, currentVersion);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(0, 2, 2)]
    [InlineData(1, 1, 2)]
    [InlineData(10, 3, 13)]
    public void MartenAppendExpectedVersion_ShouldBeFinalVersionAfterAppend(
        long currentVersion, int appendedEventCount, long expectedFinalVersion)
    {
        var martenExpectedVersion = currentVersion + appendedEventCount;

        Assert.Equal(expectedFinalVersion, martenExpectedVersion);
    }

    [Fact]
    public void BugScenario_NewStreamWithMinusTwo_WouldAskMartenForImpossibleVersion()
    {
        int? stateVersion = null;

        var wrongCurrentVersion = stateVersion ?? -2;
        var wrongMartenExpectedVersion = wrongCurrentVersion + 1;
        var correctCurrentVersion = stateVersion ?? 0;
        var correctMartenExpectedVersion = correctCurrentVersion + 1;

        Assert.Equal(-1, wrongMartenExpectedVersion);
        Assert.Equal(1, correctMartenExpectedVersion);
    }
}
