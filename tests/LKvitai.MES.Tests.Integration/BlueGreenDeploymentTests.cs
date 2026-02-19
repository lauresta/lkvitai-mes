using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class BlueGreenDeploymentTests
{
    [Fact]
    public void BlueGreenComposeFile_ShouldExist()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: docker-compose.blue-green.yml",
            "docker-compose.blue-green.yml");
        Assert.True(File.Exists(IntegrationTestAssets.AssetPath("docker-compose.blue-green.yml")));
    }

    [Fact]
    public void SwitchScript_ShouldSetGreenAsActiveEnvironment()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/blue-green/switch-to-green.sh",
            "scripts/blue-green/switch-to-green.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/blue-green/switch-to-green.sh"));
        Assert.Contains("green", content, StringComparison.Ordinal);
        Assert.Contains(".active_environment", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RollbackScript_ShouldSetBlueAsActiveEnvironment()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/blue-green/rollback-to-blue.sh",
            "scripts/blue-green/rollback-to-blue.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/blue-green/rollback-to-blue.sh"));
        Assert.Contains("blue", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentFlow_ShouldSupportBlueToGreenAndRollback()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: blue/green deployment scripts",
            "scripts/blue-green/deploy-green.sh",
            "scripts/blue-green/switch-to-green.sh",
            "scripts/blue-green/rollback-to-blue.sh");
        var deploy = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/blue-green/deploy-green.sh"));
        var switchToGreen = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/blue-green/switch-to-green.sh"));
        var rollback = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/blue-green/rollback-to-blue.sh"));

        Assert.Contains("GREEN_IMAGE_TAG", deploy, StringComparison.Ordinal);
        Assert.Contains("Traffic switched to GREEN", switchToGreen, StringComparison.Ordinal);
        Assert.Contains("Traffic rolled back to BLUE", rollback, StringComparison.Ordinal);
    }
}
