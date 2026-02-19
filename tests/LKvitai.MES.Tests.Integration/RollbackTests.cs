using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class RollbackTests
{
    [Fact]
    public void RollbackScripts_ShouldExist()
    {
        Assert.True(File.Exists(GetRepoFile("scripts/rollback/rollback-api.sh")));
        Assert.True(File.Exists(GetRepoFile("scripts/rollback/rollback-database.sh")));
        Assert.True(File.Exists(GetRepoFile("scripts/rollback/rollback-full.sh")));
    }

    [Fact]
    public void ApiRollbackScript_ShouldRequireExplicitVersionTag()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/rollback/rollback-api.sh"));
        Assert.Contains("TARGET_VERSION", content, StringComparison.Ordinal);
        Assert.Contains("docker compose -f docker-compose.test.yml up -d api webui", content, StringComparison.Ordinal);
        Assert.Contains("IMAGE_TAG", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseRollbackScript_ShouldTargetSpecificMigration()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/rollback/rollback-database.sh"));
        Assert.Contains("TARGET_MIGRATION", content, StringComparison.Ordinal);
        Assert.Contains("dotnet ef database update", content, StringComparison.Ordinal);
    }

    [Fact]
    public void FullRollbackScript_ShouldOrchestrateApiDatabaseAndSmokeValidation()
    {
        var content = File.ReadAllText(GetRepoFile("scripts/rollback/rollback-full.sh"));
        Assert.Contains("rollback-api.sh", content, StringComparison.Ordinal);
        Assert.Contains("rollback-database.sh", content, StringComparison.Ordinal);
        Assert.Contains("smoke-tests.sh", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeConfig_ShouldUseVersionPinnedImageTags()
    {
        var compose = File.ReadAllText(GetRepoFile("docker-compose.test.yml"));
        Assert.Contains("${IMAGE_TAG:-latest}", compose, StringComparison.Ordinal);
    }

    private static string GetRepoFile(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", relativePath));
    }
}
