using Xunit;

namespace LKvitai.MES.Tests.Integration;

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
        var root = ResolveFromRepositoryRoot("src");
        return Directory.GetFiles(Path.Combine(root, "Modules", "Warehouse", "LKvitai.MES.Api"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "Modules", "Warehouse", "LKvitai.MES.Infrastructure"), "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Path.Combine(root, "Modules", "Warehouse", "LKvitai.MES.Sagas"), "*.cs", SearchOption.AllDirectories))
            .ToList();
    }

    private static string ResolveFromRepositoryRoot(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not resolve repository path: {relativePath}");
    }
}
