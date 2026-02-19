using Xunit;

namespace LKvitai.MES.Tests.Integration;

internal static class IntegrationTestAssets
{
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

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, "Directory.Packages.props")) ||
                File.Exists(Path.Combine(current.FullName, "src", "LKvitai.MES.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to resolve repository root for integration tests.");
    }
}
