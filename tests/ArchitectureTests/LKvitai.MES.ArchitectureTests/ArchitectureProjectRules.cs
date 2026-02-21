using System.Xml.Linq;

namespace LKvitai.MES.ArchitectureTests;

internal static class ArchitectureProjectRules
{
    internal static string ResolveRepoRoot()
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

    internal static XDocument LoadProject(string repoRoot, string relativePath)
    {
        var path = Path.Combine(repoRoot, relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Project file not found: {relativePath}", path);
        }

        return XDocument.Load(path);
    }

    internal static IReadOnlyList<string> GetProjectReferences(XDocument doc) =>
        doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Replace('\\', '/'))
            .ToList();

    internal static IReadOnlyList<string> GetPackageReferences(XDocument doc) =>
        doc.Descendants("PackageReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();
}
