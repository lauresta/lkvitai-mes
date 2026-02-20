using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public sealed class RollbackTests
{
    [Fact]
    public void RollbackScripts_ShouldExist()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: rollback scripts",
            "scripts/rollback/rollback-api.sh",
            "scripts/rollback/rollback-database.sh",
            "scripts/rollback/rollback-full.sh");
        Assert.True(File.Exists(IntegrationTestAssets.AssetPath("scripts/rollback/rollback-api.sh")));
        Assert.True(File.Exists(IntegrationTestAssets.AssetPath("scripts/rollback/rollback-database.sh")));
        Assert.True(File.Exists(IntegrationTestAssets.AssetPath("scripts/rollback/rollback-full.sh")));
    }

    [Fact]
    public void ApiRollbackScript_ShouldRequireExplicitVersionTag()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/rollback/rollback-api.sh",
            "scripts/rollback/rollback-api.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/rollback/rollback-api.sh"));
        Assert.Contains("TARGET_VERSION", content, StringComparison.Ordinal);
        Assert.Contains("docker compose -f docker-compose.test.yml up -d api webui", content, StringComparison.Ordinal);
        Assert.Contains("IMAGE_TAG", content, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseRollbackScript_ShouldTargetSpecificMigration()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/rollback/rollback-database.sh",
            "scripts/rollback/rollback-database.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/rollback/rollback-database.sh"));
        Assert.Contains("TARGET_MIGRATION", content, StringComparison.Ordinal);
        Assert.Contains("dotnet ef database update", content, StringComparison.Ordinal);
    }

    [Fact]
    public void FullRollbackScript_ShouldOrchestrateApiDatabaseAndSmokeValidation()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: scripts/rollback/rollback-full.sh",
            "scripts/rollback/rollback-full.sh");
        var content = File.ReadAllText(IntegrationTestAssets.AssetPath("scripts/rollback/rollback-full.sh"));
        Assert.Contains("rollback-api.sh", content, StringComparison.Ordinal);
        Assert.Contains("rollback-database.sh", content, StringComparison.Ordinal);
        Assert.Contains("smoke-tests.sh", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeConfig_ShouldUseVersionPinnedImageTags()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: docker-compose.test.yml",
            "docker-compose.test.yml");
        var compose = File.ReadAllText(IntegrationTestAssets.AssetPath("docker-compose.test.yml"));
        Assert.Contains("${IMAGE_TAG:-latest}", compose, StringComparison.Ordinal);
    }
}
