using Xunit;

namespace LKvitai.MES.ArchitectureTests;

public class ApplicationLayerTests
{
    [Fact]
    public void Application_Must_Not_Reference_Marten_Or_Infrastructure_Project()
    {
        var repoRoot = ArchitectureProjectRules.ResolveRepoRoot();
        var project = ArchitectureProjectRules.LoadProject(
            repoRoot,
            Path.Combine("src", "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.Application", "LKvitai.MES.Modules.Warehouse.Application.csproj"));

        var projectReferences = ArchitectureProjectRules.GetProjectReferences(project);
        var packageReferences = ArchitectureProjectRules.GetPackageReferences(project);

        Assert.DoesNotContain(projectReferences, r => r.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packageReferences, p => p.Contains("Marten", StringComparison.OrdinalIgnoreCase));
    }
}
