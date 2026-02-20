using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class MigrationTests
{
    private const string MigrationDirectoryRelativePath = "src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations";

    [Fact]
    public void MigrationScripts_ShouldContainAddColumnScenario()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations",
            MigrationDirectoryRelativePath);
        var files = GetMigrationFiles();
        Assert.Contains(files, file => File.ReadAllText(file).Contains(".AddColumn<", StringComparison.Ordinal));
    }

    [Fact]
    public void MigrationScripts_ShouldContainAddIndexScenario()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations",
            MigrationDirectoryRelativePath);
        var files = GetMigrationFiles();
        Assert.Contains(files, file => File.ReadAllText(file).Contains(".CreateIndex(", StringComparison.Ordinal));
    }

    [Fact]
    public void MigrationScripts_ShouldContainAddTableScenario()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations",
            MigrationDirectoryRelativePath);
        var files = GetMigrationFiles();
        Assert.Contains(files, file => File.ReadAllText(file).Contains(".CreateTable(", StringComparison.Ordinal));
    }

    [Fact]
    public void MigrationScripts_ShouldContainRenameOrDropColumnScenario()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations",
            MigrationDirectoryRelativePath);
        var files = GetMigrationFiles();
        Assert.Contains(files, file =>
        {
            var content = File.ReadAllText(file);
            return content.Contains(".RenameColumn(", StringComparison.Ordinal) ||
                   content.Contains(".DropColumn(", StringComparison.Ordinal);
        });
    }

    [Fact]
    public void LastFiveMigrations_ShouldDefineDownRollbackLogic()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations",
            MigrationDirectoryRelativePath);
        var latestFive = GetMigrationFiles()
            .OrderByDescending(Path.GetFileName)
            .Take(5)
            .ToList();

        Assert.Equal(5, latestFive.Count);
        Assert.All(latestFive, file =>
        {
            var content = File.ReadAllText(file);
            Assert.Contains("protected override void Down", content, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MigrationScripts_ShouldContainDataIntegrityOperations()
    {
        IntegrationTestAssets.SkipIfMissing(
            "External assets missing: src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations",
            MigrationDirectoryRelativePath);
        var files = GetMigrationFiles();
        var merged = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.True(
            merged.Contains("AddForeignKey", StringComparison.Ordinal) ||
            merged.Contains("table.ForeignKey", StringComparison.Ordinal),
            "Expected migration operations to include a foreign-key constraint definition.");
        Assert.Contains("CreateIndex", merged, StringComparison.Ordinal);
    }

    private static List<string> GetMigrationFiles()
    {
        var migrationDirectory = IntegrationTestAssets.AssetPath(MigrationDirectoryRelativePath);
        return Directory.GetFiles(migrationDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file => !file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.EndsWith("WarehouseDbContextModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
