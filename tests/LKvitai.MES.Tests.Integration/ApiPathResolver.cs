namespace LKvitai.MES.Tests.Integration;

internal static class ApiPathResolver
{
    public static string ResolveApiDirectory()
    {
        var repoRoot = ResolveRepositoryRoot();
        var candidates = new[]
        {
            Path.Combine(repoRoot, "src", "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.Api"),
            Path.Combine(repoRoot, "src", "Modules", "Warehouse", "LKvitai.MES.Api"),
            Path.Combine(repoRoot, "src", "LKvitai.MES.Api")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Api source directory. Checked: {string.Join(", ", candidates)}");
    }

    public static string ResolveApiFileOrFail(params string[] relativeSegments)
    {
        var candidate = Path.Combine(ResolveApiDirectory(), Path.Combine(relativeSegments));
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException($"Could not locate Api file: {candidate}");
        }

        return candidate;
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var srcExists = Directory.Exists(Path.Combine(directory.FullName, "src"));
            var hasSln = Directory.EnumerateFiles(directory.FullName, "*.sln", SearchOption.TopDirectoryOnly).Any();
            var hasCentralPackages = File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props"));
            if (srcExists && (hasSln || hasCentralPackages))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test runtime directory.");
    }
}
