using System.Xml.Linq;
using Xunit;

namespace LKvitai.MES.ArchitectureTests;

public class BuildingBlocksLayerTests
{
    [Fact]
    public void BuildingBlocks_Must_Not_Reference_Modules()
    {
        var repoRoot = ResolveRepoRoot();
        var buildingBlocksPath = Path.Combine(repoRoot, "src", "BuildingBlocks");

        Assert.True(Directory.Exists(buildingBlocksPath), "src/BuildingBlocks directory not found.");

        var violations = new List<string>();

        foreach (var csproj in Directory.EnumerateFiles(buildingBlocksPath, "*.csproj", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(csproj);
            var references = doc.Descendants("ProjectReference")
                .Select(x => x.Attribute("Include")?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Replace('\\', '/'));

            foreach (var reference in references)
            {
                if (reference.Contains("Modules/", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{Path.GetRelativePath(repoRoot, csproj)} -> {reference}");
                }
            }
        }

        Assert.True(violations.Count == 0, "BuildingBlocks references Modules:\n" + string.Join("\n", violations));
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var srcPath = Path.Combine(current.FullName, "src");
            var hasSolution = Directory.EnumerateFiles(current.FullName, "*.sln", SearchOption.TopDirectoryOnly).Any();
            var hasPackagesProps = File.Exists(Path.Combine(current.FullName, "Directory.Packages.props"));

            if (Directory.Exists(srcPath) && (hasSolution || hasPackagesProps))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not resolve repository root from test base directory.");
    }
}
