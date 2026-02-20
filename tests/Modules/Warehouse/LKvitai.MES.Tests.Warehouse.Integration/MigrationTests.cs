using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public sealed class MigrationTests
{
    [SkippableFact]
    public void MigrationScripts_ShouldContainAddColumnScenario()
    {
        var files = GetMigrationFiles();
        Assert.Contains(files, file => File.ReadAllText(file).Contains(".AddColumn<", StringComparison.Ordinal));
    }

    [SkippableFact]
    public void MigrationScripts_ShouldContainAddIndexScenario()
    {
        var files = GetMigrationFiles();
        Assert.Contains(files, file => File.ReadAllText(file).Contains(".CreateIndex(", StringComparison.Ordinal));
    }

    [SkippableFact]
    public void MigrationScripts_ShouldContainAddTableScenario()
    {
        var files = GetMigrationFiles();
        Assert.Contains(files, file => File.ReadAllText(file).Contains(".CreateTable(", StringComparison.Ordinal));
    }

    [SkippableFact]
    public void MigrationScripts_ShouldContainRenameOrDropColumnScenario()
    {
        var files = GetMigrationFiles();
        Assert.Contains(files, file =>
        {
            var content = File.ReadAllText(file);
            return content.Contains(".RenameColumn(", StringComparison.Ordinal) ||
                   content.Contains(".DropColumn(", StringComparison.Ordinal);
        });
    }

    [SkippableFact]
    public void LastFiveMigrations_ShouldDefineDownRollbackLogic()
    {
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

    [SkippableFact]
    public void MigrationScripts_ShouldContainDataIntegrityOperations()
    {
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
        var migrationDirectory = IntegrationTestAssets.ResolveMigrationsDirectoryOrSkip();
        return Directory.GetFiles(migrationDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(file => !file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.EndsWith("WarehouseDbContextModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
