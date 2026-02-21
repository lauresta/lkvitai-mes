using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public sealed class AsyncOperationTests
{
    [Fact]
    public void Source_ShouldNotUseDotWaitForBlockingAsync()
    {
        var files = GetSourceFiles();
        Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains(".Wait()", StringComparison.Ordinal));
    }

    [Fact]
    public void Source_ShouldNotUseGetAwaiterGetResultForBlockingAsync()
    {
        var files = GetSourceFiles();
        Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains(".GetAwaiter().GetResult()", StringComparison.Ordinal));
    }

    [Fact]
    public void Source_ShouldNotUseSyncHttpClientSend()
    {
        var files = GetSourceFiles();
        Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains("HttpClient.Send(", StringComparison.Ordinal));
    }

    [Fact]
    public void Source_ShouldNotUseSyncFileReadAllText()
    {
        var files = GetSourceFiles();
        Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains("File.ReadAllText(", StringComparison.Ordinal));
    }

    [Fact]
    public void Source_ShouldNotUseSyncFileWriteAllText()
    {
        var files = GetSourceFiles();
        Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains("File.WriteAllText(", StringComparison.Ordinal));
    }

    private static List<string> GetSourceFiles()
    {
        var repoRoot = ResolveRepoRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        var infrastructureRoot = ResolveInfrastructureRoot(srcRoot);

        return Directory.GetFiles(ApiPathResolver.ResolveApiDirectory(), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(infrastructureRoot, "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Path.Combine(srcRoot, "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.Sagas"), "*.cs", SearchOption.AllDirectories))
            .ToList();
    }

    private static string ResolveRepoRoot()
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

        throw new DirectoryNotFoundException("Could not resolve repository root for async operation tests.");
    }

    private static string ResolveInfrastructureRoot(string srcRoot)
    {
        var candidates = new[]
        {
            Path.Combine(srcRoot, "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.Infrastructure"),
            Path.Combine(srcRoot, "Modules", "Warehouse", "LKvitai.MES.Infrastructure"),
            Path.Combine(srcRoot, "LKvitai.MES.Infrastructure")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Infrastructure source directory. Checked: {string.Join(", ", candidates)}");
    }
}
