using Xunit;

namespace LKvitai.MES.ArchitectureTests;

/// <summary>
/// Enforces the dependency rules for the Shopfloor module. Shopfloor follows the
/// scaffolded five-layer shape but adds a <c>Domain</c> layer (like Warehouse),
/// so it is validated separately from the pure five-layer modules:
///
///   Modules.Shopfloor.Contracts      -> (no module refs; BuildingBlocks only)
///   Modules.Shopfloor.Domain         -> Contracts (+ BuildingBlocks)
///   Modules.Shopfloor.Application    -> Contracts + Domain (+ BuildingBlocks)
///   Modules.Shopfloor.Infrastructure -> Application + Contracts + Domain (+ BuildingBlocks)
///   Modules.Shopfloor.Api            -> Application + Contracts + Infrastructure (+ BuildingBlocks)
///   Modules.Shopfloor.WebUI          -> Contracts (+ BuildingBlocks)
///
/// None of these projects may reference a project in a different module.
/// </summary>
public class ShopfloorModuleDependencyRulesTests
{
    private const string Module = "Shopfloor";

    [Fact]
    public void Contracts_Has_No_Module_References()
    {
        var refs = LoadProjectRefs("Contracts");

        Assert.DoesNotContain(refs, IsModuleProject);
    }

    [Fact]
    public void Domain_Only_References_Contracts_Of_Same_Module_And_BuildingBlocks()
    {
        var refs = LoadProjectRefs("Domain");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModule(r, "Contracts"),
                $"Domain has disallowed module reference: {r}");
        }
    }

    [Fact]
    public void Application_Only_References_Contracts_And_Domain_Of_Same_Module()
    {
        var refs = LoadProjectRefs("Application");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModule(r, "Contracts") || IsSameModule(r, "Domain"),
                $"Application has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, IsInfrastructure);
        Assert.DoesNotContain(refs, IsApi);
        Assert.DoesNotContain(refs, IsWebUI);
    }

    [Fact]
    public void Infrastructure_References_Stay_Within_Same_Module()
    {
        var refs = LoadProjectRefs("Infrastructure");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModule(r, "Application") || IsSameModule(r, "Contracts") || IsSameModule(r, "Domain"),
                $"Infrastructure has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, IsApi);
        Assert.DoesNotContain(refs, IsWebUI);
    }

    [Fact]
    public void Api_References_Stay_Within_Same_Module()
    {
        var refs = LoadProjectRefs("Api");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModule(r, "Application")
                    || IsSameModule(r, "Contracts")
                    || IsSameModule(r, "Infrastructure")
                    || IsSameModule(r, "Domain"),
                $"Api has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, IsWebUI);
    }

    [Fact]
    public void WebUI_Only_References_Contracts_Of_Same_Module()
    {
        var refs = LoadProjectRefs("WebUI");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModule(r, "Contracts"),
                $"WebUI has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, IsApplication);
        Assert.DoesNotContain(refs, IsInfrastructure);
        Assert.DoesNotContain(refs, IsApi);
    }

    [Fact]
    public void No_Cross_Module_References()
    {
        foreach (var layer in new[] { "Contracts", "Domain", "Application", "Infrastructure", "Api", "WebUI" })
        {
            var refs = LoadProjectRefs(layer);

            foreach (var r in refs)
            {
                if (!IsModuleProject(r)) continue;

                Assert.True(
                    r.Contains($"Modules.{Module}.", StringComparison.OrdinalIgnoreCase),
                    $"{Module}.{layer} has cross-module reference to: {r}");
            }
        }
    }

    private static IReadOnlyList<string> LoadProjectRefs(string layer)
    {
        var repoRoot = ArchitectureProjectRules.ResolveRepoRoot();
        var project = ArchitectureProjectRules.LoadProject(
            repoRoot,
            Path.Combine(
                "src", "Modules", Module,
                $"LKvitai.MES.Modules.{Module}.{layer}",
                $"LKvitai.MES.Modules.{Module}.{layer}.csproj"));

        return ArchitectureProjectRules.GetProjectReferences(project);
    }

    private static bool IsModuleProject(string reference) =>
        reference.Contains("LKvitai.MES.Modules.", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameModule(string reference, string layer) =>
        reference.EndsWith($"LKvitai.MES.Modules.{Module}.{layer}.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsApplication(string reference) =>
        reference.EndsWith(".Application.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsInfrastructure(string reference) =>
        reference.EndsWith(".Infrastructure.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsApi(string reference) =>
        reference.EndsWith(".Api.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsWebUI(string reference) =>
        reference.EndsWith(".WebUI.csproj", StringComparison.OrdinalIgnoreCase);
}
