using Xunit;

namespace LKvitai.MES.Tests.Integration;

internal static class IntegrationTestAssets
{
    private static readonly string[] MigrationDirectoryCandidates =
    [
        "src/Modules/Warehouse/LKvitai.MES.Infrastructure/Persistence/Migrations",
        "src/LKvitai.MES.Infrastructure/Persistence/Migrations"
    ];

    private static readonly string RepoRoot = ResolveRepoRoot();

    internal static bool AssetsPresent(params string[] relativePaths)
    {
        foreach (var relativePath in relativePaths)
        {
            var fullPath = Path.Combine(RepoRoot, relativePath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return false;
            }
        }

        return true;
    }

    internal static string AssetPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(RepoRoot, relativePath));
    }

    internal static void SkipIfMissing(string reason, params string[] relativePaths)
    {
        Skip.If(!AssetsPresent(relativePaths), reason);
    }

    internal static string ResolveMigrationsDirectoryOrSkip()
    {
        foreach (var relativePath in MigrationDirectoryCandidates)
        {
            var fullPath = AssetPath(relativePath);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        Skip.If(true, $"External assets missing: {string.Join(" OR ", MigrationDirectoryCandidates)}");
        return string.Empty;
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var hasSrcDirectory = Directory.Exists(Path.Combine(current.FullName, "src"));
            var hasSolution = Directory.EnumerateFiles(current.FullName, "*.sln", SearchOption.TopDirectoryOnly).Any();
            var hasCentralPackages = File.Exists(Path.Combine(current.FullName, "Directory.Packages.props"));

            if (hasSrcDirectory && (hasSolution || hasCentralPackages))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to resolve repository root for integration tests.");
    }
}
