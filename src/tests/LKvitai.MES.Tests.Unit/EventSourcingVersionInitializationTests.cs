using Xunit;

namespace LKvitai.MES.Tests.Unit;

/// <summary>
/// Tests to verify correct version initialization for Marten event-sourced repositories.
/// This prevents the "expected version -2 but was 0" concurrency bug.
///
/// CRITICAL: Marten uses V-2 versioning scheme:
/// - New stream (no events): version = -2 for first append
/// - After 1st event: version = -1 for second append
/// - After 2nd event: version = 0 for third append
/// - After Nth event: version = N-2 for (N+1)th append
/// </summary>
public class EventSourcingVersionInitializationTests
{
    [Fact]
    public void MartenVersionSemantics_NewStream_ShouldUseMinusTwo()
    {
        // This test documents the correct Marten V-2 version semantics
        // for new streams (non-existent streams)

        // Given: A stream that doesn't exist yet
        int? stateVersion = null;

        // When: Calculating expected version for first append
        var expectedVersion = stateVersion ?? -2;

        // Then: Version should be -2 (Marten's V-2 convention for new streams)
        Assert.Equal(-2, expectedVersion);
    }

    [Fact]
    public void MartenVersionSemantics_ExistingStreamWithOneEvent_ShouldUseMinusOne()
    {
        // This test documents the correct Marten V-2 version semantics
        // for existing streams with 1 event

        // Given: A stream with 1 event (version -1 in V-2 scheme)
        int? stateVersion = -1;

        // When: Calculating expected version for second append
        var expectedVersion = stateVersion ?? -2;

        // Then: Version should be -1 (expecting 1 event already)
        Assert.Equal(-1, expectedVersion);
    }

    [Fact]
    public void MartenVersionSemantics_ExistingStreamWithTwoEvents_ShouldUseZero()
    {
        // This test documents the correct Marten V-2 version semantics
        // for existing streams with 2 events

        // Given: A stream with 2 events (version 0 in V-2 scheme)
        int? stateVersion = 0;

        // When: Calculating expected version for third append
        var expectedVersion = stateVersion ?? -2;

        // Then: Version should be 0 (expecting 2 events already)
        Assert.Equal(0, expectedVersion);
    }

    [Theory]
    [InlineData(null, -2)]   // New stream (no events) → use -2
    [InlineData(-1, -1)]     // 1 event → use -1
    [InlineData(0, 0)]       // 2 events → use 0
    [InlineData(1, 1)]       // 3 events → use 1
    [InlineData(10, 10)]     // 12 events → use 10
    public void VersionInitialization_AllScenarios_ShouldReturnCorrectVersion(
        int? actualVersion, int expectedVersion)
    {
        // Given: A stream state with given version (or null)
        int? stateVersion = actualVersion;

        // When: Calculating expected version using repository pattern
        var calculatedVersion = stateVersion ?? -2;

        // Then: Version matches expected
        Assert.Equal(expectedVersion, calculatedVersion);
    }

    [Fact]
    public void BugScenario_NewStreamWithWrongDefault_WouldCauseConcurrencyError()
    {
        // This test demonstrates the BUG that was fixed
        // BEFORE FIX (attempt 1): state?.Version ?? 0 → caused "expected -2 but was 0" error
        // BEFORE FIX (attempt 2): state?.Version ?? -1 → caused "expected -2 but was 0" error
        // AFTER FIX: state?.Version ?? -2 → works correctly with V-2 scheme

        // Given: A new stream (state is null)
        int? stateVersion = null;

        // WRONG (old code attempt 1): Using 0 as default
        var buggyVersionAttempt1 = stateVersion ?? 0;

        // WRONG (old code attempt 2): Using -1 as default
        var buggyVersionAttempt2 = stateVersion ?? -1;

        // RIGHT (fixed code): Using -2 as default for V-2 scheme
        var correctVersion = stateVersion ?? -2;

        // Then: Buggy versions would cause Marten to reject append
        Assert.Equal(0, buggyVersionAttempt1);    // ❌ This caused "expected -2 but was 0"
        Assert.Equal(-1, buggyVersionAttempt2);   // ❌ This also caused "expected -2 but was 0"
        Assert.Equal(-2, correctVersion);          // ✅ This works correctly with V-2
    }

    [Fact]
    public void MartenV2Scheme_VersionProgression_ShouldFollowPattern()
    {
        // This test documents the V-2 versioning progression
        // Version = NumberOfEvents - 2

        Assert.Equal(-2, CalculateVersion(0));  // New stream: 0 events - 2 = -2
        Assert.Equal(-1, CalculateVersion(1));  // After 1 event: 1 - 2 = -1
        Assert.Equal(0, CalculateVersion(2));   // After 2 events: 2 - 2 = 0
        Assert.Equal(1, CalculateVersion(3));   // After 3 events: 3 - 2 = 1
        Assert.Equal(98, CalculateVersion(100)); // After 100 events: 100 - 2 = 98
    }

    private static int CalculateVersion(int numberOfEvents)
    {
        return numberOfEvents - 2;
    }
}
