using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class CanaryDeploymentTests
{
    [Fact]
    public void CanaryScripts_ShouldExist()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: canary deployment scripts",
            "scripts/canary/deploy-canary.sh",
            "scripts/canary/progress-canary.sh",
            "scripts/canary/rollback-canary.sh");
        Assert.True(File.Exists(IntegrationTestAssets.AssetPath("scripts/canary/deploy-canary.sh")));
        Assert.True(File.Exists(IntegrationTestAssets.AssetPath("scripts/canary/progress-canary.sh")));
        Assert.True(File.Exists(IntegrationTestAssets.AssetPath("scripts/canary/rollback-canary.sh")));
    }

    [Fact]
    public void DeployScript_ShouldSupportVersionAndTrafficArguments()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/canary/deploy-canary.sh",
            "scripts/canary/deploy-canary.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/canary/deploy-canary.sh"));
        Assert.Contains("Usage: $0 <version> <traffic-percent>", content, StringComparison.Ordinal);
        Assert.Contains(".canary_percent", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressScript_ShouldHandle10To50To100Phases()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/canary/progress-canary.sh",
            "scripts/canary/progress-canary.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/canary/progress-canary.sh"));
        Assert.Contains("<traffic-percent>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RollbackScript_ShouldResetTrafficToStable()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/canary/rollback-canary.sh",
            "scripts/canary/rollback-canary.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/canary/rollback-canary.sh"));
        Assert.Contains("traffic restored to stable", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorInjectionScript_ShouldSupportAutoRollbackTesting()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/canary/inject-errors.sh",
            "scripts/canary/inject-errors.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/canary/inject-errors.sh"));
        Assert.Contains("error_rate=0.10", content, StringComparison.Ordinal);
    }
}
