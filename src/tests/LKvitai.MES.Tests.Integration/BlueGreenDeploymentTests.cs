using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class BlueGreenDeploymentTests
{
    [Fact]
    public void BlueGreenComposeFile_ShouldExist()
    {
        Assert.True(File.Exists(GetRepoFile("docker-compose.blue-green.yml")));
    }

    [Fact]
    public void SwitchScript_ShouldSetGreenAsActiveEnvironment()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/blue-green/switch-to-green.sh"));
        Assert.Contains("green", content, StringComparison.Ordinal);
        Assert.Contains(".active_environment", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RollbackScript_ShouldSetBlueAsActiveEnvironment()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/blue-green/rollback-to-blue.sh"));
        Assert.Contains("blue", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentFlow_ShouldSupportBlueToGreenAndRollback()
    {
        var deploy = File.ReadAllText(GetRepoFile("scripts/blue-green/deploy-green.sh"));
        var switchToGreen = File.ReadAllText(GetRepoFile("scripts/blue-green/switch-to-green.sh"));
        var rollback = File.ReadAllText(GetRepoFile("scripts/blue-green/rollback-to-blue.sh"));

        Assert.Contains("GREEN_IMAGE_TAG", deploy, StringComparison.Ordinal);
        Assert.Contains("Traffic switched to GREEN", switchToGreen, StringComparison.Ordinal);
        Assert.Contains("Traffic rolled back to BLUE", rollback, StringComparison.Ordinal);
    }

    private static string GetRepoFile(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", relativePath));
    }
}
