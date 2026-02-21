using Xunit;

namespace LKvitai.MES.ArchitectureTests;

public class ContractsLayerTests
{
    [Fact]
    public void Contracts_Must_Have_Zero_Dependencies()
    {
        var repoRoot = ArchitectureProjectRules.ResolveRepoRoot();
        var project = ArchitectureProjectRules.LoadProject(
            repoRoot,
            Path.Combine("src", "Modules", "Warehouse", "LKvitai.MES.Modules.Warehouse.Contracts", "LKvitai.MES.Modules.Warehouse.Contracts.csproj"));

        var projectReferences = ArchitectureProjectRules.GetProjectReferences(project);
        var packageReferences = ArchitectureProjectRules.GetPackageReferences(project);

        Assert.Empty(projectReferences);
        Assert.Empty(packageReferences);
    }
}
