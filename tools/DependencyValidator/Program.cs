using System.Text;
using System.Xml.Linq;

try
{
    var repoRoot = Directory.GetCurrentDirectory();
    var projectFiles = Directory.EnumerateFiles(Path.Combine(repoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
        .Concat(Directory.Exists(Path.Combine(repoRoot, "tests"))
            ? Directory.EnumerateFiles(Path.Combine(repoRoot, "tests"), "*.csproj", SearchOption.AllDirectories)
            : Enumerable.Empty<string>())
        .ToList();

    var violations = new List<string>();

    foreach (var projectFile in projectFiles)
    {
        var relativePath = Path.GetRelativePath(repoRoot, projectFile).Replace('\\', '/');
        var projectName = Path.GetFileNameWithoutExtension(projectFile);

        XDocument doc;
        try
        {
            doc = XDocument.Load(projectFile);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse {relativePath}: {ex.Message}");
            Environment.ExitCode = 1;
            return;
        }

        var projectReferences = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();

        var packageReferences = doc.Descendants("PackageReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();

        if (projectName.Contains(".Application", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var packageReference in packageReferences)
            {
                if (packageReference.Contains("Marten", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{relativePath} | PackageReference | {packageReference} | Application references Marten");
                }
            }
        }

        if (projectName.Contains(".SharedKernel", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var packageReference in packageReferences)
            {
                if (packageReference.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{relativePath} | PackageReference | {packageReference} | SharedKernel references MediatR");
                }
            }
        }

        if (projectName.Contains(".Contracts", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var projectReference in projectReferences)
            {
                if (projectReference.Contains("SharedKernel", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{relativePath} | ProjectReference | {projectReference} | Contracts references SharedKernel");
                }
            }
        }
    }

    Console.WriteLine("Dependency baseline report (non-blocking)");
    Console.WriteLine($"Projects scanned: {projectFiles.Count}");
    Console.WriteLine($"Violations found: {violations.Count}");

    if (violations.Count > 0)
    {
        foreach (var violation in violations)
        {
            Console.WriteLine($"- {violation}");
        }
    }

    var reportPath = Path.Combine(repoRoot, "docs", "audit", "dependency-baseline.md");
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    var sb = new StringBuilder();
    sb.AppendLine("# Dependency Baseline Report");
    sb.AppendLine();
    sb.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    sb.AppendLine();
    sb.AppendLine("## Violations");
    sb.AppendLine();

    if (violations.Count == 0)
    {
        sb.AppendLine("- None");
    }
    else
    {
        foreach (var violation in violations)
        {
            sb.AppendLine($"- {violation}");
        }
    }

    sb.AppendLine();
    sb.AppendLine("## Known baseline violations (expected at this stage)");
    sb.AppendLine();
    sb.AppendLine("These findings are expected in early refactor phases and are report-only at P0.S2.T1. Strict enforcement is deferred to later phases.");

    File.WriteAllText(reportPath, sb.ToString());
}
catch (Exception ex)
{
    Console.Error.WriteLine($"DependencyValidator runtime failure: {ex.Message}");
    Environment.ExitCode = 1;
}
