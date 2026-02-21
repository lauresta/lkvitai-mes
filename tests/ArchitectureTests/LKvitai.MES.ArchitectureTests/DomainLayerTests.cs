using Xunit;

namespace LKvitai.MES.ArchitectureTests;

public class DomainLayerTests
{
    [Fact]
    public void Domain_Must_Not_Reference_Infrastructure_Or_Tech_Packages()
    {
        var repoRoot = ArchitectureProjectRules.ResolveRepoRoot();
        var project = ArchitectureProjectRules.LoadProject(
            repoRoot,
            Path.Combine("src", "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.Domain", "LKvitai.MES.Modules.Warehouse.Domain.csproj"));

        var projectReferences = ArchitectureProjectRules.GetProjectReferences(project);
        var packageReferences = ArchitectureProjectRules.GetPackageReferences(project);

        Assert.DoesNotContain(projectReferences, r => r.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packageReferences, p => p.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packageReferences, p => p.Contains("Marten", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packageReferences, p => p.Contains("MassTransit", StringComparison.OrdinalIgnoreCase));
    }
}
