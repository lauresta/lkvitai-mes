using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

/// <summary>
/// Shared helper that gates Testcontainers-based integration tests.
///
/// Tests call <see cref="EnsureEnabled"/> (which throws <see cref="Xunit.SkipException"/>
/// when Docker/Testcontainers is not opted-in) so xUnit reports them as "Skipped" instead
/// of "Failed".
///
/// Opt-in:
///   TESTCONTAINERS_ENABLED=1 dotnet test src/tests/LKvitai.MES.Tests.Warehouse.Integration/
/// </summary>
public static class DockerRequirement
{
    private const string EnvVar = "TESTCONTAINERS_ENABLED";

    /// <summary>
    /// Returns <c>true</c> when the environment variable
    /// <c>TESTCONTAINERS_ENABLED</c> is set to <c>"1"</c>.
    /// </summary>
    public static bool IsEnabled =>
        string.Equals(
            Environment.GetEnvironmentVariable(EnvVar),
            "1",
            StringComparison.Ordinal);

    /// <summary>
    /// Human-readable reason shown in the test runner when a test is skipped.
    /// </summary>
    public const string SkipReason =
        $"Docker/Testcontainers integration tests are opt-in. " +
        $"Set TESTCONTAINERS_ENABLED=1 to run them.";

    /// <summary>
    /// Call at the top of every <c>[SkippableFact]</c> that needs Docker.
    /// Throws <see cref="Xunit.SkipException"/> when disabled, causing xUnit
    /// to report the test as skipped (not failed).
    /// </summary>
    public static void EnsureEnabled()
    {
        Skip.IfNot(IsEnabled, SkipReason);
    }
}
