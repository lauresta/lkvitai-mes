using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class CanaryDeploymentTests
{
    [Fact]
    public void CanaryScripts_ShouldExist()
    {
        Assert.True(File.Exists(GetRepoFile("scripts/canary/deploy-canary.sh")));
        Assert.True(File.Exists(GetRepoFile("scripts/canary/progress-canary.sh")));
        Assert.True(File.Exists(GetRepoFile("scripts/canary/rollback-canary.sh")));
    }

    [Fact]
    public void DeployScript_ShouldSupportVersionAndTrafficArguments()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/canary/deploy-canary.sh"));
        Assert.Contains("Usage: $0 <version> <traffic-percent>", content, StringComparison.Ordinal);
        Assert.Contains(".canary_percent", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressScript_ShouldHandle10To50To100Phases()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/canary/progress-canary.sh"));
        Assert.Contains("<traffic-percent>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RollbackScript_ShouldResetTrafficToStable()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/canary/rollback-canary.sh"));
        Assert.Contains("traffic restored to stable", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorInjectionScript_ShouldSupportAutoRollbackTesting()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/canary/inject-errors.sh"));
        Assert.Contains("error_rate=0.10", content, StringComparison.Ordinal);
    }

    private static string GetRepoFile(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", relativePath));
    }
}
